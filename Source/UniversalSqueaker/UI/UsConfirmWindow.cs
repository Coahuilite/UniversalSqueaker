using System;

using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;


namespace UniversalSqueaker.UI;

/// <summary>
/// VF1 (r5): ONE ordinary UiKit confirmation - a small window asking the question once with a
/// confirm and a cancel button. It is deliberately NOT the Remix double-confirmation: the
/// mixed-mode slot-swap exists because entering Remix is a cross-page mode change; deleting a
/// player's own table is a single reversible-by-recreating decision, and copying the double-swap
/// here would be cargo cult (user ruling 2026-10-07).
///
/// <para>
/// The shell wiring follows the established dialog pattern (RemixConfirmationFlow, SA1.3): a real
/// <see cref="UiPageWindow"/> opened through a <see cref="UiWindowCatalog"/> with
/// AbsorbInputAroundWindow=true, so the background cannot change the selection while the question
/// is on screen; HostAttached opens the dialog's OWN UsTextFitAudit scope and registers it with
/// the developer panel (UsDevPanelTargets.DialogHost), and HostDetached disposes the audit,
/// clears the target and DROPS the staged action - ESC, Cancel and any failed page all answer the
/// same way: nothing is written. The caller binds the action to what it asked about (e.g. the
/// race captured when the button was pressed), so a late selection change can never make the
/// confirm delete a different table.
/// </para>
/// </summary>
public sealed class UsConfirmWindow
{
    private const string Consumer = "coahuilite.universalsqueaker";
    private const string WindowKind = "us/confirm";

    private static readonly UiWindowKey WindowKey = new(Consumer, WindowKind, "");
    private static readonly UiTheme WindowTheme = UsTheme.Surface();

    private static UsConfirmWindow? active;

    private readonly UiWindowCatalog catalog;
    private readonly UiBindings bindings;
    private readonly ITextMetrics? dialogMetrics;

    // The stack this slot's catalog opens on. Open re-creates the slot when a caller names a
    // DIFFERENT stack (the harness injects a fresh one per lane); the same stack reuses the slot,
    // which is what lets a superseded question be closed through the same catalog.
    private readonly WindowStack stack;

    private string title = "";
    private string message = "";
    private string confirmLabel = "";
    private Action? onConfirm;
    private UiHost? dialogHost;
    private UsTextFitAudit? dialogAudit;
    private bool answered;

    private UsConfirmWindow(WindowStack stack, ITextMetrics? dialogMetrics)
    {
        this.stack = stack;
        this.dialogMetrics = dialogMetrics;
        UsKernelWidgetRegistrar.EnsureRegistered();
        bindings = new UiBindings();
        bindings.BindCommand("confirm-yes", Confirm);
        bindings.BindCommand("confirm-no", Cancel);
        catalog = new UiWindowCatalog(stack);
        // The registration is re-issued per Open (Register overwrites the key's entry): the window
        // size is a property of the QUESTION, measured below, not a shipped constant. The PM
        // boundary probe measured the old fixed 420x180 with the long restore copy and the
        // confirm-buttons row landing BELOW the content body - unreachable controls. UiPageWindow is
        // sealed, so the sizing rides the options, and the message additionally lives in a Scroll
        // container so anything the screen clamp cannot fit still scrolls without crowding buttons.
    }

    private UiWindowOptions OptionsFor(string message)
    {
        var options = new UiWindowOptions
        {
            // Enter must not answer the question; ESC closes, and a close IS a cancel - the
            // staged action only ever runs from the explicit confirm button.
            CloseOnAccept = false,
            AbsorbInputAroundWindow = true,
            InitialSize = new Vector2(BaseWidth, BaseHeight),
        };
        ITextMetrics metrics = dialogMetrics ?? VerseFerriteTextMetrics.Instance;
        float innerWidth = Math.Max(80f, BaseWidth - 2f * ShellSidePadding - 2f * RootPadding);
        float textHeight = metrics.MeasureText(message ?? "", WindowTheme.Styles.Font, innerWidth)
            + 2f * WindowTheme.Geometry.Padding;
        float desired = 2f * RootPadding + textHeight + RootGap + ButtonHeight
            + ShellTitleHeight + ShellSidePadding;
        float cap = Math.Max(BaseHeight, Verse.UI.screenHeight - 40f);
        float height = desired > cap ? cap : (desired < BaseHeight ? BaseHeight : desired);
        options.InitialSize = new Vector2(BaseWidth, height);
        return options;
    }

    // Shell geometry constants: the UiWindowHost chrome (TitleBarHeight 56 / SidePadding 20) and the
    // page's own root (Padding 10, Gap 8, button band 26) - the same numbers PageXmlFor writes.
    private const float BaseWidth = 420f;
    private const float BaseHeight = 180f;
    private const float ShellTitleHeight = 56f;
    private const float ShellSidePadding = 20f;
    private const float RootPadding = 10f;
    private const float RootGap = 8f;
    private const float ButtonHeight = 26f;

    /// <summary>Ask once. Opening a second question cancels (drops the staged action of) the first.
    /// Strings arrive RESOLVED; the action must carry its own captured identity. Production passes no
    /// stack (the game's WindowStack); the harness injects its own, the RemixConfirmationFlow
    /// precedent, so the REAL shell can be driven without Verse.Find.</summary>
    public static UsConfirmWindow? Open(string title, string message, string confirmLabel, Action onConfirm, WindowStack? stack = null)
    {
        if (onConfirm == null) throw new ArgumentNullException(nameof(onConfirm));
        WindowStack target = stack ?? Find.WindowStack;
        if (target == null) return null;

        UsConfirmWindow? slot = active;
        if (slot != null && !ReferenceEquals(slot.stack, target))
        {
            // A different stack (the harness's per-lane stack): the old slot belongs to the old
            // stack, so start fresh rather than opening on a stack the caller no longer holds.
            slot = null;
        }
        UsConfirmWindow flow = slot ?? new UsConfirmWindow(target, null);
        active = flow;
        // One question at a time: close the previous window through THIS slot's own catalog (its
        // detach drops the staged action, so a superseded question is cancelled, never run). The
        // slot itself stays live - the old CloseCurrent call also nulled `active` right after the
        // ??= above, which made the field permanently null while a question was on screen AND let a
        // second Open leak an unclosable first window (a fresh instance cannot close an old
        // catalog's window). Found by the US-RESET1 ceremony lane driving the widget press.
        flow.catalog.Close(WindowKey);
        flow.title = title ?? "";
        flow.message = message ?? "";
        flow.confirmLabel = confirmLabel ?? "";
        flow.onConfirm = onConfirm;
        flow.catalog.Register(Consumer, WindowKind, flow.OptionsFor(flow.message), flow.CreateWindow);
        flow.catalog.Open(WindowKey);
        return flow;
    }

    /// <summary>The dialog page's own command table (the Remix flow's seam): lanes drive the exact
    /// commands the real buttons fire, on the SAME table the UiPageWindow draws against.</summary>
    public IUiBindings DialogBindings => bindings;
    /// <summary>The live question slot while one is staged (set by Open, cleared when an answer or
    /// CloseCurrent retires it; an ESC teardown leaves the slot for the next Open to reuse). A lane
    /// that drives a CEREMONY through the real widget press (not through Open's own return value)
    /// reaches the dialog's command table and its message text through this seam.</summary>
    public static UsConfirmWindow? Active => active;

    /// <summary>The question text currently staged - resolved display strings, so a lane can assert
    /// that a scope-naming confirmation actually NAMES the scope (the §4.3 wording rule).</summary>
    public string MessageText => message;

    /// <summary>The staged page xml (message + labels resolved into the shell's own manifest). The
    /// reachability lane builds a host over the SAME manifest and the SAME DialogBindings - the
    /// SA1.3 wiring precedent - so a real engine button press, not a bindings call, answers the
    /// question.</summary>
    public string PageXml => PageXmlFor(message, confirmLabel);


    /// <summary>The dialog page's audit scope while attached (null otherwise) - the DT1 developer
    /// panel targets it for outline/capture/report like any other US window.</summary>
    public UsTextFitAudit? DialogAudit => dialogAudit;

    /// <summary>The live window while the question is on screen; null otherwise. Lanes drive the
    /// REAL shell through this seam.</summary>
    public UiWindowHost? OpenedWindow => catalog.TryGet(WindowKey, out UiWindowHost? host) ? host : null;

    /// <summary>Attach seam (production via HostAttached; a harness may wire a host it built over
    /// the same manifest/bindings, the SA1.3 precedent). One scope per live host; a re-attach
    /// replaces the previous scope so no stale scope stays targetable.</summary>
    public void AttachDialogHost(UiHost host)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        dialogHost = host;
        dialogAudit?.Dispose();
        dialogAudit = UsTextFitAudit.Open(host, SqueakLog.ShouldEmitDev);
        UniversalSqueaker.UI.Dev.UsDevPanelTargets.DialogHost = host;
    }

    private UiPageWindow CreateWindow(UiWindowKey key)
    {
        UiPageWindow window = new(
            key,
            UiLayoutManifest.Parse(PageXmlFor(message, confirmLabel)),
            bindings,
            WindowTheme,
            new UsKernelTranslation(),
            title,
            "US.Common.Cancel".Translate(),
            notice => (notice == UiWindowNotice.Prerequisite
                ? "US.Settings.Prerequisite.Title"
                : "US.Diagnostics.PageUnavailable.Title").Translate(),
            metrics: dialogMetrics);
        // F01/US-UI1: confirmation priority over the Dialog main window and the SubSuper dev tools
        // (measured 1.6 reference layer order; UsDevPanelWindow's constructor cites it).
        window.layer = WindowLayer.Super;

        window.HostAttached += AttachDialogHost;
        // ANY teardown - ESC, Cancel, a failed page - answers the same way: drop the staged action.
        window.HostDetached += _ =>
        {
            if (ReferenceEquals(UniversalSqueaker.UI.Dev.UsDevPanelTargets.DialogHost, dialogHost))
            {
                UniversalSqueaker.UI.Dev.UsDevPanelTargets.DialogHost = null;
            }
            dialogHost = null;
            dialogAudit?.Dispose();
            dialogAudit = null;
            onConfirm = null;
            answered = false;
        };
        return window;
    }

    private void Confirm()
    {
        if (answered) return;
        answered = true;
        Action? pending = onConfirm;
        onConfirm = null;
        CloseCurrent();
        // The close runs first so a throwing action cannot leave a live dialog; the action carries
        // the identity the caller captured when it asked.
        pending?.Invoke();
    }

    private void Cancel()
    {
        onConfirm = null; // drop BEFORE the close so the detach cannot run it
        answered = true;
        CloseCurrent();
    }

    private void CloseCurrent()
    {
        catalog.Close(WindowKey);
        if (ReferenceEquals(active, this)) active = null;
    }

    private static string Escape(string value) => (value ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // The message lives in the shell's own Scroll container (the FL structural container, the same
    // vocabulary the settings page uses for content-scroll): a long question body scrolls inside the
    // window, and the button row below it is ALWAYS arranged - reachability is a layout property,
    // not a size hope. The window additionally opens at the MEASURED height for the staged text
    // (ConfirmPageWindow.InitialSizePolicy), clamped to the screen, so ordinary questions need no
    // scrolling at all and only screen-exceeding ones do.
    private static string PageXmlFor(string message, string confirmLabel)
        => "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
        + "  <Column Id=\"confirm-root\" Gap=\"8\" Padding=\"10\">"
        + "    <Scroll Id=\"confirm-message-scroll\" Fill=\"true\" Gap=\"0\" Padding=\"0\">"
        + "      <Widget Id=\"confirm-message\" Kind=\"text/wrapped\" Text=\"" + Escape(message) + "\" />"
        + "    </Scroll>"
        + "    <Row Id=\"confirm-buttons\" Gap=\"8\" Padding=\"0\">"
        + "      <Widget Id=\"confirm-yes\" Kind=\"input/button\" Text=\"" + Escape(confirmLabel) + "\" ActionBind=\"confirm-yes\" Height=\"26\" />"
        + "      <Widget Id=\"confirm-no\" Kind=\"input/button\" TextKey=\"US.Common.Cancel\" ActionBind=\"confirm-no\" Height=\"26\" Emphasis=\"Muted\" />"
        + "    </Row>"
        + "  </Column>"
        + "</UiPage>";

    // (The sizing lives in OptionsFor above: UiPageWindow is sealed, so the measured height rides
    // the per-Open registration options rather than a subclass policy.)
}
