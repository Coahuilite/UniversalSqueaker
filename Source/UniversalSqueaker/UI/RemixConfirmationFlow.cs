using System;
using System.IO;

using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// SA1.3: the equal-weight-Remix double confirmation, owned by ONE parent settings window.
///
/// <para>
/// <b>Why a flow object instead of a dialog branch in the widget.</b> The contract's safety is a
/// SEQUENCE property - step one writes nothing, only the second step's explicit click commits, cancel
/// or ESC or a close keeps the old mode, and a click landing where step one's confirm button was must
/// meet Cancel or blank in step two. That sequence is state, and state needs an owner. The owner here
/// is this per-window object: the business confirmation state never enters a library global (the
/// carrier owns no key for it), and closing the parent window releases the whole flow.
/// </para>
///
/// <para>
/// <b>What the carrier provides.</b> The window is the library's own <see cref="UiPageWindow"/> -
/// the declarative page shell - opened through a per-flow <see cref="UiWindowCatalog"/> whose stack is
/// the game's. The two steps are ONE window with two gated sections (<c>confirm-step1</c> /
/// <c>confirm-step2</c>): the button row keeps its two slots and swaps their occupants, which is what
/// makes the same-coordinate click land on Cancel. Enter cannot bypass the two explicit choices
/// because the registration sets <c>CloseOnAccept=false</c>; ESC closes, and a close answers Cancel.
/// </para>
///
/// <para>
/// <b>Commit path.</b> <see cref="RequestRemix"/> receives the parent page's OWN typed write closure
/// (the same <c>source.SetMode + bump</c> the direct write would have run) and invokes it only from
/// the second step's enable command. Until then the mode binding has not been touched at all - there
/// is no staged value, no save, and nothing to roll back.
/// </para>
/// </summary>
public sealed class RemixConfirmationFlow
{
    private const string ManifestResourceName = "UniversalSqueaker.UI.Layout.RemixConfirm.Schema2.xml";
    private const string Consumer = "coahuilite.universalsqueaker";
    private const string WindowKind = "remix-confirm";

    /// <summary>0 = closed, 1 = first step, 2 = final step. Read-only outside this file.</summary>
    private int step;

    private readonly UiWindowCatalog catalog;
    private readonly UiWindowKey key;
    private readonly UiLayoutManifest manifest;
    private readonly UiTheme theme;
    private readonly UiBindings bindings;
    private UiHost? dialogHost;
    // DT1 (F03b closure): the dialog page gets its OWN audit scope over its OWN host, opened on the
    // single attach seam and disposed on the single detach seam - the same per-host rule the settings
    // window and the diagnostics panel follow. Before this the dialog inherited nothing: the parent's
    // scope never reached it, so outline/capture could not name its widgets (the playtest's missing
    // attribution). The scope exists in every logging mode; only the fit audit inside it follows the
    // logging policy (R3-B rule, mirrored from the panel).
    private UsTextFitAudit? dialogAudit;
    // Production passes null (the shell then uses the Verse metrics); the SA1.3 harness injects its
    // recording stub so the fit audit measures the SAME strings the real shell draws. Behaviour never
    // depends on this value - it only decides which ruler measures text.
    private readonly ITextMetrics? dialogMetrics;
    private Action? commit;
    private bool step1;
    private bool step2;

    public RemixConfirmationFlow(WindowStack stack, ITextMetrics? dialogMetrics = null)
    {
        if (stack == null) throw new ArgumentNullException(nameof(stack));
        this.dialogMetrics = dialogMetrics;

        UsKernelWidgetRegistrar.EnsureRegistered();
        manifest = UiLayoutManifest.Parse(ReadManifest());
        theme = UsTheme.Surface();

        bindings = new UiBindings();
        bindings.BindReadOnly<bool>("confirm-step1", () => step1);
        bindings.BindReadOnly<bool>("confirm-step2", () => step2);
        bindings.BindCommand("confirm-continue", Continue);
        bindings.BindCommand("confirm-cancel", Close);
        bindings.BindCommand("confirm-enable", Enable);

        catalog = new UiWindowCatalog(stack);
        key = new UiWindowKey(Consumer, WindowKind, "");
        catalog.Register(
            Consumer,
            WindowKind,
            new UiWindowOptions
            {
                // SA1.3: Enter must not answer the two explicit choices. ESC keeps the game's default
                // (close), and a close IS the cancel - the flow never commits on a close.
                CloseOnAccept = false,
                AbsorbInputAroundWindow = true,
                InitialSize = new Vector2(460f, 240f),
            },
            CreateWindow);
    }

    /// <summary>The flow's visible state: 0 closed, 1 first step, 2 final step. Lanes read this; the
    /// dialog's own visibility keys are derived from it, never the other way round.</summary>
    public int Step => step;

    /// <summary>
    /// The dialog page's own command/value table, exposed read-only so the harness can drive the exact
    /// commands the real buttons fire (<c>confirm-continue</c> / <c>confirm-cancel</c> /
    /// <c>confirm-enable</c>) without a game WindowStack loop. It is the SAME table the UiPageWindow
    /// draws against; opening the dialog in production wires it to that window.
    /// </summary>
    public IUiBindings DialogBindings => bindings;

    /// <summary>
    /// Takes a request to enter Remix and returns true when the flow owns it - the caller then writes
    /// NOTHING. Re-entering while open is swallowed (SR's own guard: one confirmation flow, no stacked
    /// dialogs); the commit closure is kept verbatim until step two or a close.
    /// </summary>
    public bool RequestRemix(Action commitToRun)
    {
        if (commitToRun == null) throw new ArgumentNullException(nameof(commitToRun));
        if (step != 0) return true;

        commit = commitToRun;
        step = 1;
        step1 = true;
        step2 = false;
        catalog.Open(key);
        Refresh();
        return true;
    }

    /// <summary>The parent window is going away: release the flow (close the dialog if open, drop the
    /// staged commit). A later click on the parent's Remix option starts a FRESH flow.</summary>
    public void Abort()
    {
        Close();
    }

    private void Continue()
    {
        if (step != 1) return;
        step = 2;
        step1 = false;
        step2 = true;
        Refresh();
    }

    private void Enable()
    {
        if (step != 2) return;
        Action? pending = commit;
        Close();
        // The close runs first so a throwing commit cannot leave a live dialog claiming an open flow;
        // the write itself is the parent page's own closure (SetMode + revision bump), not a copy.
        pending?.Invoke();
    }

    private void Close()
    {
        step = 0;
        step1 = false;
        step2 = false;
        commit = null;
        catalog.Close(key);
    }

    private UiWindowHost CreateWindow(UiWindowKey windowKey)
    {
        UiPageWindow window = new(
            windowKey,
            manifest,
            bindings,
            theme,
            new UsKernelTranslation(),
            "US.Remix.Confirm.Title".Translate(),
            "US.Common.Cancel".Translate(),
            notice => (notice == UiWindowNotice.Prerequisite
                ? "US.Remix.Notice.Prerequisite"
                : "US.Remix.Notice.Unavailable").Translate(),
            metrics: dialogMetrics);

        window.HostAttached += AttachDialogHost;
        // ANY teardown of the dialog - ESC, the Cancel command, a failed page - answers the same way:
        // the flow is closed and the staged commit is dropped, never run.
        window.HostDetached += _ =>
        {
            if (UniversalSqueaker.UI.Dev.UsDevPanelTargets.DialogHost == dialogHost)
            {
                UniversalSqueaker.UI.Dev.UsDevPanelTargets.DialogHost = null;
            }
            dialogHost = null;
            dialogAudit?.Dispose();
            dialogAudit = null;
            step = 0;
            step1 = false;
            step2 = false;
            commit = null;
        };
        return window;
    }

    /// <summary>
    /// The ONE path by which a dialog page host starts driving this flow: production reaches it through
    /// the window's <c>HostAttached</c> event, and the harness reaches it by building a real UiHost over
    /// the same manifest and binding table (the SA1.3 lane) and attaching it here. Either way the flow's
    /// step flips publish through <see cref="Refresh"/> into THIS host's session clock - there is no
    /// second, harness-only notification channel to drift.
    /// </summary>
    public void AttachDialogHost(UiHost host)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        dialogHost = host;
        // One scope per live host: a re-attach (the harness, or a reopen that skipped detach) replaces
        // the previous scope before the new one opens, so no stale scope can be targeted by the
        // developer commands after its host is gone.
        dialogAudit?.Dispose();
        dialogAudit = UsTextFitAudit.Open(host, SqueakLog.ShouldEmitDev);
        UniversalSqueaker.UI.Dev.UsDevPanelTargets.DialogHost = host;
    }

    /// <summary>The dialog page's own audit scope while the host is attached (null otherwise); the
    /// DT1 developer panel targets it for outline/capture/report like any other US window.</summary>
    public UsTextFitAudit? DialogAudit => dialogAudit;

    /// <summary>The dialog page's manifest and theme, exposed with the attach seam so a harness can build
    /// the SAME page the window draws - the two-step geometry proof needs the real arrange, not a copy.</summary>
    public UiLayoutManifest DialogManifest => manifest;
    public UiTheme DialogTheme => theme;

    /// <summary>The live dialog window while the flow owns one; null when the flow is closed. The SA1.3
    /// lane drives the REAL shell through this seam (windowRect + WindowOnGUI), so the page is measured
    /// at the content box the shell chrome actually leaves - not at a lane-invented box.</summary>
    public UiWindowHost? OpenedWindow => catalog.TryGet(key, out UiWindowHost? host) ? host : null;

    /// <summary>Publish the swapped visibility to the dialog page: the value cache first, then the
    /// clock the layout snapshot compares (the same two-step every US declarative flip uses).</summary>
    private void Refresh()
    {
        bindings.NotifyChanged("confirm-step1", "confirm-step2");
        dialogHost?.Session.BumpContentRevision();
    }

    private static string ReadManifest()
    {
        using Stream? stream = typeof(RemixConfirmationFlow).Assembly
            .GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException("US.Remix.ManifestMissing: " + ManifestResourceName);
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
