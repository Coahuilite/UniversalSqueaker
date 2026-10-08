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

    private string title = "";
    private string message = "";
    private string confirmLabel = "";
    private Action? onConfirm;
    private UiHost? dialogHost;
    private UsTextFitAudit? dialogAudit;
    private bool answered;

    private UsConfirmWindow(WindowStack stack, ITextMetrics? dialogMetrics)
    {
        this.dialogMetrics = dialogMetrics;
        UsKernelWidgetRegistrar.EnsureRegistered();
        bindings = new UiBindings();
        bindings.BindCommand("confirm-yes", Confirm);
        bindings.BindCommand("confirm-no", Cancel);
        catalog = new UiWindowCatalog(stack);
        catalog.Register(
            Consumer,
            WindowKind,
            new UiWindowOptions
            {
                // Enter must not answer the question; ESC closes, and a close IS a cancel - the
                // staged action only ever runs from the explicit confirm button.
                CloseOnAccept = false,
                AbsorbInputAroundWindow = true,
                InitialSize = new Vector2(420f, 180f),
            },
            CreateWindow);
    }

    /// <summary>Ask once. Opening a second question cancels (drops the staged action of) the first.
    /// Strings arrive RESOLVED; the action must carry its own captured identity. Production passes no
    /// stack (the game's WindowStack); the harness injects its own, the RemixConfirmationFlow
    /// precedent, so the REAL shell can be driven without Verse.Find.</summary>
    public static UsConfirmWindow? Open(string title, string message, string confirmLabel, Action onConfirm, WindowStack? stack = null)
    {
        if (onConfirm == null) throw new ArgumentNullException(nameof(onConfirm));
        WindowStack target = stack ?? Find.WindowStack;
        if (target == null) return null;

        UsConfirmWindow flow = active ??= new UsConfirmWindow(target, null);
        flow.CloseCurrent(); // one question at a time; the previous one is cancelled, never run
        flow.title = title ?? "";
        flow.message = message ?? "";
        flow.confirmLabel = confirmLabel ?? "";
        flow.onConfirm = onConfirm;
        flow.catalog.Open(WindowKey);
        return flow;
    }

    /// <summary>The dialog page's own command table (the Remix flow's seam): lanes drive the exact
    /// commands the real buttons fire, on the SAME table the UiPageWindow draws against.</summary>
    public IUiBindings DialogBindings => bindings;

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

    private static string PageXmlFor(string message, string confirmLabel)
        => "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
        + "  <Column Id=\"confirm-root\" Gap=\"8\" Padding=\"10\">"
        + "    <Widget Id=\"confirm-message\" Kind=\"text/wrapped\" Text=\"" + Escape(message) + "\" />"
        + "    <Row Id=\"confirm-buttons\" Gap=\"8\" Padding=\"0\">"
        + "      <Widget Id=\"confirm-yes\" Kind=\"input/button\" Text=\"" + Escape(confirmLabel) + "\" ActionBind=\"confirm-yes\" Height=\"26\" />"
        + "      <Widget Id=\"confirm-no\" Kind=\"input/button\" TextKey=\"US.Common.Cancel\" ActionBind=\"confirm-no\" Height=\"26\" Emphasis=\"Muted\" />"
        + "    </Row>"
        + "  </Column>"
        + "</UiPage>";
}
