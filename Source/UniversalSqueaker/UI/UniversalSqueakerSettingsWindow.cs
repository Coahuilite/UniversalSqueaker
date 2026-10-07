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
/// hit test through <c>UiNative</c> — which is why this type is no longer an exemption in the UI
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
public sealed class UniversalSqueakerSettingsWindow : UiWindowHost
{
    private static readonly UiTheme WindowTheme = UsTheme.Surface();

    private readonly UniversalSqueakerMod mod;

    // The page's per-window business boundary, kept so BeforeDraw can read the layout-report request and
    // tick the pending settings save. CreateHost builds it once per window instance.
    private UsKernelSettingsSource? source;

    /// <summary>SA1.3: ONE Remix confirmation flow per parent window. Created with the host (the mode
    /// write below is the only writer it gates) and released at PostClose: closing the parent can never
    /// leave a live dialog or a staged commit behind. The business state lives on this object - never on
    /// a library global.</summary>
    private RemixConfirmationFlow? remixFlow;

    public UniversalSqueakerSettingsWindow(UniversalSqueakerMod mod)
    {
        this.mod = mod;
        // Modality is the consumer's call — the shell deliberately decides none of it. The chrome
        // flags it owns (background, close X/button, shadow) are not repeated here.
        layer = WindowLayer.Dialog;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        preventCameraMotion = true;
    }

    protected override UiTheme Theme => WindowTheme;

    protected override string Title => mod.SettingsCategory();

    protected override string Subtitle => Translator.Translate("US.Settings.Window.Subtitle");

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
    /// The window opens NARROW like the vanilla ModSettings window: <see cref="WindowChromeLayout"/>
    /// clamps 50% of the screen width into [800, 1600] and the height is that width at 4:3 with a 600px
    /// floor. BH1 removed the drawer-expanded branch: the help panel reserves height inside the page above
    /// the footer, so this is the window's size in BOTH help states. At the 1024x768 game screen that is
    /// 800x600, and the page box the shell hands the page is 760x524 open or closed. The shell's default
    /// would be the game's own <see cref="Window.InitialSize"/>, so this policy is
    /// what makes the opening size a product decision instead of an accident.
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
    protected override bool PrerequisiteVerified => UniversalSqueakerMod.PrerequisiteVerified;

    /// <summary>
    /// This window's own audit scope over its own host subscription. R3-B fix 4: the scope is opened in EVERY
    /// logging mode (the developer geometry commands need a scope to target); only the text-fit audit inside it
    /// follows the logging policy.
    /// </summary>
    private UsTextFitAudit? audit;

    protected override UiHost CreateHost()
    {
        source = new UsKernelSettingsSource(UniversalSqueakerMod.Settings);
        // BH1: no page-width feed any more. The help panel's presentation does not depend on the box the
        // shell hands the page - there is exactly one presentation, and the engine hides it declaratively -
        // so the window has no width-shaped decision left to feed.
        // SA1.3: the Remix confirmation flow is THIS window's - one per parent, built before the host so
        // the mode write registers against it, released in PostClose so no dialog or staged commit can
        // outlive the page that created it.
        remixFlow = new RemixConfirmationFlow(Find.WindowStack);
        UiHost host = UsKernelSettingsHost.Create(source, remixFlow);
        // Per-HOST audit, not the process-wide legacy channel: this window's findings land in THIS host's
        // subscription and are measured with THIS host's ruler.
        //
        // `SqueakLog.ShouldEmitDev` is still the policy, but it now selects the FIT AUDIT rather than whether
        // a scope exists at all: with detailed logging off this window is a geometry-only handle (nothing is
        // measured, the process-wide switch is untouched) and the layout-diagnosis controls still work.
        audit = UsTextFitAudit.Open(host, SqueakLog.ShouldEmitDev);
        // DT1: the dev panel's default target - registered with the host, cleared in PreClose.
        UniversalSqueaker.UI.Dev.UsDevPanelTargets.SettingsHost = host;
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
    /// <para>
    /// BH1 retired the rest of what this method used to own: feeding the shell's actual page-box width to
    /// the host's presentation decision, bumping the layout clock when that width flipped the shape, and
    /// resizing the window on the drawer edge. The help panel changes no width, so a toggle is a page write
    /// like any other and the toggle's own bumper covers it.
    /// </para>
    /// </summary>
    protected override void BeforeDraw(Rect contentRect)
    {
        // The per-host channel is a BOUNDED RING, not a push sink: drain what the previous pass's chrome
        // and page draw published before this pass adds to it, so a frame's findings cannot be pushed out
        // of the ring unread (FL-20).
        audit?.Publish();
        // R3-B's one-shot report. It runs here, before the pass draws, so the capture it prints is the last
        // pass that COMPLETED - the request is made during a draw, and this is the next one. One request is
        // one report: the source clears its pending request in the same step that writes it, so the pass after
        // that writes nothing, and a request whose pass has not completed yet simply waits.
        //
        // RPT1: the generated pass is no longer discarded - the source retains it as the outcome the status
        // sentence beside the button prints. A produced report advances the same content clock every other
        // display write uses, so the band re-measures to the new sentence on the next pass instead of keeping
        // the pre-report band (the report is not itself a page write, so nothing else would move that clock).
        if (source?.ConsumeLayoutReportRequest() >= 0) Host?.Session.BumpContentRevision();
        mod.TickSettingsSaveForWindow();
    }

    /// <summary>Terminal state for this window instance: say what happened and how to recover, draw nothing else.</summary>
    protected override void DrawNotice(Rect rect, UiWindowNotice notice)
    {
        UiThemeDraw.Surface(rect, WindowTheme, WindowTheme.Panel, WindowTheme.Border);
        Rect body = rect.ContractedBy(24f);

        // The library owns no strings, so the two notices are US's own vocabulary: the desync notice
        // names the prerequisite, the unavailable notice names the settings category to look under.
        if (notice == UiWindowNotice.Prerequisite)
        {
            UiThemeDraw.Label(
                new Rect(body.x, body.y, body.width, 30f),
                "US.Settings.Prerequisite.Title".Translate(),
                WindowTheme, WindowTheme.TextPrimary, UiFont.Medium);
            UiThemeDraw.Label(
                new Rect(body.x, body.y + 38f, body.width, Mathf.Max(1f, body.height - 38f)),
                "US.Settings.Prerequisite.Body".Translate(),
                WindowTheme, WindowTheme.TextSecondary, UiFont.Small);
            return;
        }

        UiThemeDraw.Label(
            new Rect(body.x, body.y, body.width, 30f),
            "US.Settings.PageUnavailable.Title".Translate(),
            WindowTheme, WindowTheme.TextPrimary, UiFont.Medium);
        UiThemeDraw.Label(
            new Rect(body.x, body.y + 38f, body.width, Mathf.Max(1f, body.height - 38f)),
            "US.Settings.PageUnavailable.Body".Translate(SqueakLabels.SettingsCategory),
            WindowTheme, WindowTheme.TextSecondary, UiFont.Small);
    }

    /// <summary>The named diagnostics record for a failed pass; the notice itself is drawn by the shell.</summary>
    protected override void OnDrawFailure(Exception error)
    {
        SqueakLog.SettingsOpenFailed(error);
    }

    public override void PreClose()
    {
        // Final drain + release this window's hold on the process-wide detection switch, BEFORE the shell
        // disposes the host and with it the subscription.
        audit?.Dispose();
        audit = null;
        if (UniversalSqueaker.UI.Dev.UsDevPanelTargets.SettingsHost == Host)
        {
            UniversalSqueaker.UI.Dev.UsDevPanelTargets.SettingsHost = null;
        }
        // Disposing the page host and its session is the shell's job.
        base.PreClose();
    }
}
