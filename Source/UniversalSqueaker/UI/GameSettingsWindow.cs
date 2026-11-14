using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit.Kernel;

// The chrome is the library's (UiWindowHost): this type supplies the page, the vocabulary and the
// size policy, and paints nothing that the shell already paints.

namespace UniversalSqueaker.UI;

/// <summary>
/// Settings window for Universal Squeaker: the library-owned <see cref="UiWindowHost"/> shell around
/// exactly one kernel <see cref="UiHost"/> built from the real embedded Schema=2 page resource.
///
/// The window draws no chrome of its own. Background, accent rule, title, subtitle and the close
/// affordance belong to the shell, and every pixel of them goes through <c>UiThemeDraw</c> and every
/// hit test through <c>UiControls</c> — which is why this type is no longer an exemption in the UI
/// boundary gate (verify-local gate 14): there is nothing left here to exempt.
///
/// Closing disposes the host and its session (shell <see cref="UiWindowHost.PreClose"/>); reopening
/// creates a fresh pair, so a reopened window gets a clean retry. What this type still owns:
///  - the size policy (condition a: the game's own default is the library's, US's screen-fraction
///    clamp stays US's),
///  - the two notices, phrased in US's own translation keys (the library ships zero strings),
///  - the save-tick that keeps the footer status honest while the page is open,
///  - the text-fit audit lifetime, which begins with the page and ends when the window closes.
///
/// A whole-frame draw failure trips the notice on the NEXT pass and never retries the host for the
/// lifetime of this window instance; recovery for a local failure lives in the per-widget
/// <see cref="UiSessionGuard"/>, and there is deliberately no fallback page to keep in sync. That
/// state machine is the shell's (condition c, copied from this window's proven implementation), so it
/// is not rebuilt here.
///
/// RimWorld default input stays intact: Escape closes via the window stack and Enter closes the
/// window through the stock Mod Settings behavior. US deliberately does not intercept Enter; the only
/// kernel-owned input handling is the per-text-field focus/commit logic inside widgets.
/// </summary>
public sealed class GameSettingsWindow : UiWindowHost
{
    // UIR-006: the shell chrome draws from the process's shared chrome bag, which the palette write
    // path repaints live (colour-only). A palette chosen before this window opened is already in it;
    // a switch while it is open lands on the next pass. The PAGE bag is the host's own and follows
    // through the source's ApplyToLive invalidation - two bags, one decision, no rebuild.
    private static readonly UiTheme WindowTheme = UsTheme.ChromeTheme;

    private readonly SqueakerMod mod;

    // The page's per-window business boundary, kept so BeforeDraw can read the layout-report request and
    // tick the pending settings save. CreateHost builds it once per window instance.
    private UsKernelSettingsSource? source;
    private Rect lastContentRect;
    private const int UiCostSamplePasses = 32;
    private enum CostSampleState { Idle, Queued, Sampling, Complete, Partial }
    private CostSampleState uiCostState;
    private bool costRequested;
    private int uiCostFirstPass;
    private UsUiCostMetrics? uiCostMetrics;
    private UiHost? uiCostHost;
    private int uiCostSequence, uiCostSession, uiCostPasses, uiCostRevision, uiCostDefinition;
    private string uiCostPage = "", uiCostTrigger = "", uiCostEndReason = "";
    private bool uiCostSceneChanged, uiCostPanel, uiCostFit, uiCostDetail;
    private UsTextFitAudit.DevGeometryStatus uiCostGeometry;
    private Vector2 uiCostSize;
    private UiNode? uiCostScroll, uiCostHelpScroll;
    private UiLayoutPoint uiCostScrollStart, uiCostHelpStart;
    private bool uiCostExplicit;
    private long uiCostStarted;
    private int uiCostLines, uiCostVolumeKeys, uiCostLastVolumeKeyPass;
    private double uiCostLastVolumeKeyMs;
    private const int OperationSamplePassLimit = 4096;
    private const double OperationSampleSeconds = 20;

    // An optional capability on the actual Host's existing root state, not another global index.
    private sealed class CostSampleTarget { internal GameSettingsWindow? Owner; }
    private static CostSampleTarget SampleTarget(UiHost host)
        => host.CreateContext(1).State("us/settings-cost-sample", () => new CostSampleTarget());
    internal static GameSettingsWindow? CostSampleOwner(UiHost? host)
    {
        if (host == null || !host.Session.IsActive) return null;
        var owner = SampleTarget(host).Owner;
        return ReferenceEquals(owner?.Host, host) ? owner : null;
    }

    /// <summary>SA1.3: ONE Remix confirmation flow per parent window. Created with the host (the mode
    /// write below is the only writer it gates) and released at PostClose: closing the parent can never
    /// leave a live dialog or a staged commit behind. The business state lives on this object - never on
    /// a library global.</summary>
    private RemixConfirmationFlow? remixFlow;

    public GameSettingsWindow(SqueakerMod mod)
    {
        this.mod = mod;
        // Modality is the consumer's call — the shell deliberately decides none of it. The chrome
        // flags it owns (background, close X/button, shadow) are not repeated here.
        layer = WindowLayer.Dialog;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        preventCameraMotion = true;
        HostAttached += host => {
            SampleTarget(host).Owner = this;
            Dev.UsDevPanelTargets.SettingsHost = host;
        };
        HostDetached += ReleaseCostSampleTarget;
    }

    protected override UiTheme Theme => WindowTheme;

    protected override string Title => mod.SettingsCategory() + " — " + "US.B.Variant".Translate();

    protected override string Subtitle => "";
    protected override float TitleBarHeight => WindowChromeLayout.TitleBarHeight;

    protected override string CloseText => Translator.Translate("US.Settings.Window.Close");

    // NO CloseButtonSize override any more (TODO:15, closed 2026-09-20). It was added when the shell
    // still drew a fixed 110x30 box; the shell now sizes that affordance itself, as
    // max(110, MeasureWidth(CloseText, CloseFont) + 2 x CloseButtonPadding) through its own Metrics seam -
    // one definition instead of two. This override was the second one, and it was wrong twice: it padded by
    // 16 instead of the shell's 2 x 10, and it read VerseFerriteTextMetrics.Instance directly instead of
    // the seam the shell and the audit both measure with (which is how a shell that had just made room for
    // a label could still be reported as overflowing it). For the shipped captions both rules return the
    // 110 floor, so removing the duplicate changes no pixel of the English or Chinese ship shape; for a
    // longer caption the shell's rule now applies, which is also the case the in-game finding really
    // belonged to (the 128px record was the DIAGNOSTICS panel's own long close text, not this one).

    /// <summary>
    /// US logical-screen size policy. The inherited resolution-change notification re-reads this
    /// dynamic InitialSize; the actual FL content rectangle, not an inferred chrome subtraction,
    /// selects the page layout in BeforeDraw. No independent scale or per-frame screen poll.
    /// </summary>
    protected override Func<Vector2>? InitialSizePolicy => InitialSizeFromScreen;

    private static Vector2 InitialSizeFromScreen()
    {
        float width = WindowChromeLayout.SettingsWindowWidth(Verse.UI.screenWidth, Verse.UI.screenHeight);
        float height = WindowChromeLayout.SettingsWindowHeight(Verse.UI.screenWidth, Verse.UI.screenHeight);
        return new Vector2(width, height);
    }

    /// <summary>
    /// A carrier/consumer desync must never reach the draw path: the kernel page would die in a
    /// <see cref="TypeLoadException"/> and the player would see a generic notice instead of the real
    /// cause. The shell short-circuits to <see cref="UiWindowNotice.Prerequisite"/> on false.
    /// </summary>
    protected override bool PrerequisiteVerified => SqueakerMod.PrerequisiteVerified;

    /// <summary>
    /// This window's own audit scope over its own host subscription. R3-B fix 4: the scope is opened in EVERY
    /// logging mode (the developer geometry commands need a scope to target); only the text-fit audit inside it
    /// follows the logging policy.
    /// </summary>
    private UsTextFitAudit? audit;

    protected override UiHost CreateHost()
    {
        uiCostExplicit = false;
        // Explicit Enabled only: Auto (including the normal Release default) never starts timing.
        bool sampleCosts = SqueakerMod.Settings.devLoggingMode == SqueakDevLoggingMode.Enabled;
        long started = sampleCosts ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        source = new UsKernelSettingsSource(SqueakerMod.Settings, Execution.Images);
        source.SetContentLayout(WindowChromeLayout.UsesWideLayout(lastContentRect.width, lastContentRect.height));
        source.ViewState.RosterWideLayout = WindowChromeLayout.UsesWideRoster(lastContentRect.width);
        // BeforeDraw precedes first CreateHost: the opening layout already uses the actual content box.
        // SA1.3: the Remix confirmation flow is THIS window's - one per parent, built before the host so
        // the mode write registers against it, released in PostClose so no dialog or staged commit can
        // outlive the page that created it.
        remixFlow = new RemixConfirmationFlow(Find.WindowStack);
        uiCostMetrics = new UsUiCostMetrics(new VerseTextMetrics(VerseFerriteTextMetrics.Instance));
        UiHost host = UsKernelSettingsHost.Create(source, uiCostMetrics, out _, remixFlow);
        // Per-HOST audit, not the process-wide legacy channel: this window's findings land in THIS host's
        // subscription and are measured with THIS host's ruler.
        //
        // `SqueakLog.ShouldEmitDev` is still the policy, but it now selects the FIT AUDIT rather than whether
        // a scope exists at all: with detailed logging off this window is a geometry-only handle (nothing is
        // measured, the process-wide switch is untouched) and the layout-diagnosis controls still work.
        audit = UsTextFitAudit.OpenForWindow(host);
        if (sampleCosts)
        {
            costRequested = true;
            BeginUiCostObservation(host, "open", System.Diagnostics.Stopwatch.GetTimestamp() - started);
        }
        return host;
    }

    /// <summary>SA1.3: the parent's close releases the confirmation flow. The dialog rides the same
    /// WindowStack, so an un-aborted flow would strand an orphan dialog (and its staged commit) over the
    /// next window. Close is idempotent: Abort answers the same way as the dialog's own Cancel.</summary>
    public override void PostClose()
    {
        base.PostClose();
        remixFlow?.Abort();
        // DT1: closing the settings window releases the dev panel with it - the panel is a companion
        // of the settings workspace, not a standalone tool that outlives its subject.
        UniversalSqueaker.UI.Dev.UsDevPanelWindow.CloseIfOpen();
    }

    /// <summary>
    /// The one piece of frame bookkeeping the page cannot do for itself: a settings edit queued while
    /// the window is open must still flush on its own timer, or the footer status lies. The hover-claim
    /// frame boundary used to be called from here; since FL P3 <c>UiHost.DrawFrame</c> runs it on the
    /// session, so the window owns no frame protocol at all.
    /// Actual content-size changes retire old popup/capture coordinates. Only the breakpoint edge
    /// updates the compatible definition; host, editor, selected domain and scroll remain owned here.
    /// </summary>
    protected override void BeforeDraw(Rect contentRect)
    {
        source?.SynchronizeRuntimePolicy();
        bool requested = SqueakLog.ShouldEmitDev && SqueakerMod.Settings.devLoggingMode == SqueakDevLoggingMode.Enabled;
        if (!requested && !uiCostExplicit) EndUiCostObservation(true, "US.DevPanel.Sample.ModeEnded");
        audit?.SynchronizePolicy(SqueakLog.ShouldEmitDev);
        // Drain the previous completed pass before arming a new observation window.
        audit?.Publish();
        ObserveUiCostScene();
        if (uiCostState == CostSampleState.Sampling && Host != null && (uiCostExplicit
            ? Host.Session.Frame - uiCostFirstPass >= OperationSamplePassLimit || UiCostElapsedMs >= OperationSampleSeconds * 1000
            : Host.Session.Frame - uiCostFirstPass >= UiCostSamplePasses))
            EndUiCostObservation(false);
        if (Host != null && requested != costRequested)
        {
            costRequested = requested;
            if (requested && !CanEndOperationSample && uiCostState != CostSampleState.Queued)
            {
                uiCostExplicit = false;
                BeginUiCostObservation(Host, "enabled", -1);
            }
        }
        if (Host != null && (requested || uiCostExplicit) && uiCostState == CostSampleState.Queued)
            BeginUiCostObservation(Host, uiCostExplicit ? "operation-window" : "dev-panel", -1);
        // Event category/target only: no draft characters or values are retained in the report.
        // KeyDown includes invalid/clamped numeric drafts that do not publish a business change.
        if (uiCostState == CostSampleState.Sampling && Event.current?.type == EventType.KeyDown
            && Host?.Session.ActiveEditNode?.ElementId == "global-volume-number")
        {
            uiCostVolumeKeys++;
            uiCostLastVolumeKeyPass = Host.Session.Frame;
            uiCostLastVolumeKeyMs = UiCostElapsedMs;
        }
        if (contentRect.size != lastContentRect.size)
        {
            bool wide = WindowChromeLayout.UsesWideLayout(contentRect.width, contentRect.height);
            bool changed = source != null && source.ViewState.WideLayout != wide;
            lastContentRect = contentRect;
            if (Host != null)
            {
                // Geometry invalidates popup/capture coordinates, never commits or cancels a text draft.
                Host.Session.ClosePopup();
                Host.Session.Interactions.End(UiInteractionEnd.Aborted);
                if (Host.Session.OwnedHotControl is UiControlKey capture) Host.Session.ReleaseHotControl(capture);
                source!.SetContentLayout(wide);
                if (changed) Host.UpdateDefinition(UsKernelSettingsHost.PageDefinition(wide, source!.ViewState.HelpHeight, source.ViewState.RosterIsWide, UsKernelSettingsHost.UsesDeferredRoster(Host)));
                else Host.Invalidate();
            }
        }
        // R3-B's one-shot report. It runs here, before the pass draws, so the capture it prints is the last
        // pass that COMPLETED - the request is made during a draw, and this is the next one. One request is
        // one report: the source clears its pending request in the same step that writes it, so the pass after
        // that writes nothing, and a request whose pass has not completed yet simply waits.
        //
        // RPT1: the generated pass is no longer discarded - the source retains it as the outcome the status
        // sentence beside the button prints. A produced report advances the same content clock every other
        // display write uses, so the band re-measures to the new sentence on the next pass instead of keeping
        // the pre-report band (the report is not itself a page write, so nothing else would move that clock).
        if (source?.ConsumeLayoutReportRequest() >= 0) Host?.Invalidate();
        source?.SynchronizeSaveState();
        if (Host != null) UsKernelSettingsHost.PreparePage(Host, new UiLayoutPoint(contentRect.width, contentRect.height));
    }

    /// <summary>Terminal state for this window instance: say what happened and how to recover, draw nothing else.</summary>
    protected override void DrawNotice(Rect rect, UiWindowNotice notice)
    {
        UiThemeDraw.Surface(Execution, VerseUiExecution.Rect(rect), WindowTheme, WindowTheme[UiPaletteRole.PanelSurface], WindowTheme[UiPaletteRole.Border]);
        Rect body = rect.ContractedBy(24f);

        // The library owns no strings, so the two notices are US's own vocabulary: the desync notice
        // names the prerequisite, the unavailable notice names the settings category to look under.
        if (notice == UiWindowNotice.Prerequisite)
        {
            UiThemeDraw.Label(Execution, VerseUiExecution.Rect(new Rect(body.x, body.y, body.width, 30f)), "US.Settings.Prerequisite.Title".Translate(), WindowTheme, WindowTheme[UiPaletteRole.Foreground], UiFont.Medium);
            UiThemeDraw.Label(Execution, VerseUiExecution.Rect(new Rect(body.x, body.y + 38f, body.width, Mathf.Max(1f, body.height - 38f))), "US.Settings.Prerequisite.Body".Translate(), WindowTheme, WindowTheme[UiPaletteRole.SecondaryForeground], UiFont.Small);
            return;
        }

        UiThemeDraw.Label(Execution, VerseUiExecution.Rect(new Rect(body.x, body.y, body.width, 30f)), "US.Settings.PageUnavailable.Title".Translate(), WindowTheme, WindowTheme[UiPaletteRole.Foreground], UiFont.Medium);
        UiThemeDraw.Label(Execution, VerseUiExecution.Rect(new Rect(body.x, body.y + 38f, body.width, Mathf.Max(1f, body.height - 38f))), "US.Settings.PageUnavailable.Body".Translate(SqueakLabels.SettingsCategory), WindowTheme, WindowTheme[UiPaletteRole.SecondaryForeground], UiFont.Small);
    }

    /// <summary>The named diagnostics record for a failed pass; the notice itself is drawn by the shell.</summary>
    protected override void OnDrawFailure(Exception error)
    {
        SqueakLog.SettingsOpenFailed(error);
    }

    internal bool CanQueueUiCostSample => Host?.Session.IsActive == true
        && SqueakLog.ShouldEmitDev && SqueakerMod.Settings.devLoggingMode == SqueakDevLoggingMode.Enabled
        && uiCostState != CostSampleState.Queued && uiCostState != CostSampleState.Sampling;

    internal bool CanQueueOperationSample => Host?.Session.IsActive == true
        && uiCostState != CostSampleState.Queued && uiCostState != CostSampleState.Sampling;
    internal bool CanEndOperationSample => uiCostExplicit
        && (uiCostState == CostSampleState.Queued || uiCostState == CostSampleState.Sampling);
    internal void QueueOperationSample()
    {
        if (!CanQueueOperationSample) return;
        QueueCostSample(true);
    }
    internal void EndOperationSample() { if (CanEndOperationSample) EndUiCostObservation(false); }

    internal void QueueUiCostSample()
    {
        if (!CanQueueUiCostSample) return;
        QueueCostSample(false);
    }

    private void QueueCostSample(bool explicitOperation)
    {
        uiCostExplicit = explicitOperation;
        uiCostState = CostSampleState.Queued;
        uiCostSession = Host!.Session.Identity;
        uiCostPage = source!.ViewState.Page;
        uiCostTrigger = explicitOperation ? "operation-window" : "dev-panel";
        uiCostPasses = 0;
        uiCostFirstPass = -1;
        uiCostSceneChanged = false;
        uiCostEndReason = "";
        // No page write, invalidation, edit/scroll operation or Begin call here.
    }

    internal string UiCostSampleStatus
    {
        get
        {
            if (uiCostState == CostSampleState.Idle)
                return (CanQueueOperationSample ? "US.DevPanel.Sample.OperationReady" : "US.DevPanel.Sample.Unavailable").Translate();
            if (uiCostState == CostSampleState.Queued) return "US.DevPanel.Sample.Queued".Translate(uiCostSession);
            if (uiCostState == CostSampleState.Partial && uiCostFirstPass < 0)
                return "US.DevPanel.Sample.Cancelled".Translate(uiCostSession).ToString()
                    + (uiCostEndReason.Length == 0 ? "" : "\n" + uiCostEndReason.Translate().ToString());
            string state = uiCostState == CostSampleState.Sampling ? "US.DevPanel.Sample.Sampling"
                : uiCostState == CostSampleState.Complete ? "US.DevPanel.Sample.Complete" : "US.DevPanel.Sample.Partial";
            int passes = uiCostState == CostSampleState.Sampling && uiCostHost != null
                ? uiCostHost.Session.Frame - uiCostFirstPass : uiCostPasses;
            string display = "US.DevPanel.Sample.Status".Translate(state.Translate(), passes, UiCostSamplePasses,
                uiCostPage, uiCostSession, uiCostFirstPass, uiCostTrigger).ToString();
            if (uiCostExplicit) return "US.DevPanel.Sample.OperationStatus".Translate(state.Translate(), passes,
                OperationSamplePassLimit, uiCostPage, uiCostSession).ToString()
                + (uiCostEndReason.Length == 0 ? "" : "\n" + uiCostEndReason.Translate().ToString());
            return display + (uiCostSceneChanged ? "\n" + "US.DevPanel.Sample.SceneChanged".Translate().ToString() : "")
                + (uiCostEndReason.Length == 0 ? "" : "\n" + uiCostEndReason.Translate().ToString())
                + (SqueakerMod.Settings.devLoggingMode == SqueakDevLoggingMode.Enabled ? "" : "\n" + "US.DevPanel.Sample.NeedsEnabled".Translate().ToString());
        }
    }

    private void BeginUiCostObservation(UiHost host, string trigger, long creationTicks)
    {
        uiCostStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        uiCostLines = uiCostVolumeKeys = 0;
        uiCostLastVolumeKeyPass = -1; uiCostLastVolumeKeyMs = -1;
        uiCostHost = host;
        uiCostSequence++;
        uiCostSession = host.Session.Identity;
        uiCostFirstPass = host.Session.Frame;
        uiCostPage = source!.ViewState.Page;
        uiCostTrigger = trigger;
        uiCostEndReason = "";
        uiCostSceneChanged = false;
        uiCostRevision = host.Session.ContentRevision;
        uiCostDefinition = host.Session.DefinitionRevision;
        uiCostSize = lastContentRect.size;
        uiCostPanel = Dev.UsDevPanelWindow.Active != null;
        uiCostFit = UiFitAudit.Enabled;
        uiCostDetail = SqueakLog.ShouldEmitDev;
        uiCostGeometry = UsTextFitAudit.GetDevGeometryStatus(host);
        string scroll = uiCostPage switch { "Behavior" => "b-actions-scroll", "Voices" => source.ViewState.DetailOpen
            ? "voice-detail-scroll" : "voice-list-scroll", "Fallback" => "b-final-table-scroll", _ => "page-" + uiCostPage + "-scroll" };
        uiCostScroll = host.Session.GetNodeByElementId(scroll);
        uiCostHelpScroll = host.Session.GetNodeByElementId("help-scroll");
        uiCostScrollStart = host.Session.GetScrollPosition(uiCostScroll!);
        uiCostHelpStart = host.Session.GetScrollPosition(uiCostHelpScroll!);
        Action<Func<string>>? output = uiCostExplicit ? WriteUiCostTrace : null;
        audit?.BeginTimingObservation(output);
        source.BeginUiCostSample(creationTicks, output);
        uiCostMetrics?.Begin(UiFitAudit.Enabled, output);
        uiCostState = CostSampleState.Sampling;
        WriteUiCostTrace(() => UiCostSampleIdentity() + " state=started creation=" + (creationTicks < 0 ? "NotCaptured" : "Captured")
            + " dev_panel_at_start=" + uiCostPanel + " detail_at_start=" + uiCostDetail + " fit_at_start=" + uiCostFit + " geometry_at_start=" + uiCostGeometry);
    }

    private string UiCostSampleIdentity() => "UI.cost sample=" + uiCostSequence + " target=settings session=" + uiCostSession
        + " page_start=" + uiCostPage + " first_pass=" + uiCostFirstPass + " trigger=" + uiCostTrigger;
    private double UiCostElapsedMs => (System.Diagnostics.Stopwatch.GetTimestamp() - uiCostStarted) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    private void WriteUiCostTrace(Func<string> message)
    {
        if (!uiCostExplicit) { SqueakLog.LayoutTrace(message); return; }
        if (uiCostLines++ < 16) SqueakLog.SettingsCostSample(message);
    }

    private void ObserveUiCostScene()
    {
        var host = uiCostHost;
        if (uiCostState != CostSampleState.Sampling || host == null) return;
        uiCostSceneChanged |= host.Session.ContentRevision != uiCostRevision || host.Session.DefinitionRevision != uiCostDefinition
            || source?.ViewState.Page != uiCostPage || lastContentRect.size != uiCostSize
            || host.Session.GetScrollPosition(uiCostScroll!) != uiCostScrollStart || host.Session.GetScrollPosition(uiCostHelpScroll!) != uiCostHelpStart
            || (Dev.UsDevPanelWindow.Active != null) != uiCostPanel || UiFitAudit.Enabled != uiCostFit
            || SqueakLog.ShouldEmitDev != uiCostDetail || UsTextFitAudit.GetDevGeometryStatus(host) != uiCostGeometry;
    }

    private void EndUiCostObservation(bool partial, string reason = "")
    {
        if (uiCostState == CostSampleState.Queued)
        {
            uiCostState = CostSampleState.Partial;
            uiCostEndReason = reason;
            return;
        }
        if (uiCostState != CostSampleState.Sampling || uiCostHost == null) return;
        ObserveUiCostScene();
        uiCostPasses = uiCostHost.Session.Frame - uiCostFirstPass;
        uiCostState = partial ? CostSampleState.Partial : CostSampleState.Complete;
        uiCostEndReason = reason;
        WriteUiCostTrace(() => UiCostSampleIdentity() + " state=" + (partial ? "partial" : "complete")
            + " last_pass=" + uiCostHost.Session.Frame + " passes=" + uiCostPasses + " scene_changed=" + uiCostSceneChanged
            + " page_end=" + source?.ViewState.Page + " revision_start=" + uiCostRevision + " revision_end=" + uiCostHost.Session.ContentRevision
            + " dev_panel_at_end=" + (Dev.UsDevPanelWindow.Active != null) + " detail_at_end=" + SqueakLog.ShouldEmitDev
            + " fit_at_end=" + UiFitAudit.Enabled + " geometry_at_end=" + UsTextFitAudit.GetDevGeometryStatus(uiCostHost)
            + " elapsed_ms=" + UiCostElapsedMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            + " volume_keydowns=" + uiCostVolumeKeys + " last_volume_key_pass=" + uiCostLastVolumeKeyPass
            + " last_volume_key_ms=" + uiCostLastVolumeKeyMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        // Drain both phase accumulators before disabling sampling/disposal, including early Close.
        audit?.EndTimingObservation(partial);
        source?.EndUiCostSample(uiCostPasses, partial);
        uiCostMetrics?.Complete(uiCostPasses, partial);
        uiCostHost = null;
        uiCostScroll = uiCostHelpScroll = null;
    }

    private void ReleaseCostSampleTarget(UiHost host)
    {
        try { EndUiCostObservation(true, "US.DevPanel.Sample.TargetEnded"); }
        finally
        {
            source?.DetachHost(host);
            source = null;
            SampleTarget(host).Owner = null;
            if (ReferenceEquals(Dev.UsDevPanelTargets.SettingsHost, host)) Dev.UsDevPanelTargets.SettingsHost = null;
            try { audit?.Dispose(); }
            finally
            {
                audit = null;
                uiCostHost = null;
                uiCostScroll = uiCostHelpScroll = null;
                uiCostMetrics = null;
                remixFlow?.Abort();
                remixFlow = null;
            }
        }
    }

    public override void PreClose()
    {
        try
        {
            EndUiCostObservation(true, "US.DevPanel.Sample.TargetEnded");
            // Live writes already reached the queue; retained-only drafts are abandoned on close.
            if (Host?.InputState.IsEditing == true) Host.TryHandleCancel();
        }
        finally { base.PreClose(); }
    }
}
