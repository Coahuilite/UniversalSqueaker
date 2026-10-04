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

    // The page's per-window business boundary, kept so BeforeDraw can read the drawer state that
    // decides the window width. CreateHost builds it once per window instance.
    private UsKernelSettingsSource? source;

    // The drawer state this window has already sized for. Starts retracted: that is the page state's
    // default and the width InitialSizePolicy already opened with.
    private bool appliedDrawerExpanded;

    // The page box width the shell actually handed the page on the previous pass (negative until the first
    // pass). The presentation decision reads this through the host's feed; before the first pass the policy's
    // open page box stands in, which is the width that decision used before this feed existed.
    private float actualPageWidth = -1f;

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
    /// clamps 50% of the screen width into [800, 1600]. Height follows the closed width at 4:3,
    /// with a 600px floor. The retractable help drawer adds its column and gap (332 = 320 + 12),
    /// capped by the screen width (see <see cref="ApplyDrawerWidth"/>). At the 1024x768 game screen,
    /// the closed window is 800x600 and the open window is 1024x600. The shell's default would be
    /// the game's own <see cref="Window.InitialSize"/>, so this policy is
    /// what makes the opening size a product decision instead of an accident.
    /// </summary>
    protected override Func<Vector2>? InitialSizePolicy => InitialSizeFromScreen;

    private static Vector2 InitialSizeFromScreen()
    {
        float width = WindowChromeLayout.SettingsWindowWidth(Verse.UI.screenWidth, Verse.UI.screenHeight, drawerExpanded: false);
        float height = WindowChromeLayout.SettingsWindowHeight(Verse.UI.screenWidth, Verse.UI.screenHeight);
        return new Vector2(width, height);
    }

    /// <summary>
    /// Applies the drawer width BEFORE the base frame reads <c>windowRect</c> for this pass
    /// (<c>Verse.Window.WindowOnGUI</c> rounds it and hands it to <c>GUI.Window</c>; the library shell does
    /// not override this), so the pass that first presents the open drawer already has the resized window -
    /// and therefore the page box the shell computes from it. <see cref="BeforeDraw"/> keeps its call as the
    /// backstop for a pass that reaches the page without this one.
    /// </summary>
    public override void WindowOnGUI()
    {
        ApplyDrawerWidth();
        base.WindowOnGUI();
    }

    /// <summary>
    /// One-shot width change on the drawer STATE EDGE only: the page's header toggle is the only thing that
    /// flips it, and the width is written at the top of the pass after the toggle
    /// (<see cref="WindowOnGUI"/>, before the frame reads <c>windowRect</c>), so this never fights the
    /// player's own drag or a per-frame relayout. The window stays horizontally centred on its own centre and
    /// the result is clamped inside the screen.
    /// </summary>
    private void ApplyDrawerWidth()
    {
        UsKernelSettingsSource? current = source;
        if (current == null) return;

        bool expanded = current.ViewState.HelpDrawerOpen;
        if (expanded == appliedDrawerExpanded) return;
        appliedDrawerExpanded = expanded;

        float width = WindowChromeLayout.SettingsWindowWidth(Verse.UI.screenWidth, Verse.UI.screenHeight, expanded);
        float delta = width - windowRect.width;
        if (Mathf.Abs(delta) < 0.5f) return;

        float x = Mathf.Clamp(
            windowRect.x - delta * 0.5f,
            0f,
            Mathf.Max(0f, Verse.UI.screenWidth - width));
        windowRect = new Rect(x, windowRect.y, width, windowRect.height);
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
        // The page-width feed: the presentation decision reads the box the shell actually hands the page
        // (fed in BeforeDraw below). Before the first pass there is nothing fed, and the policy's open page
        // box stands in - the same width that decision used before the feed existed.
        UiHost host = UsKernelSettingsHost.Create(source, FedOrPolicyPageWidth);
        // Per-HOST audit, not the process-wide legacy channel: this window's findings land in THIS host's
        // subscription and are measured with THIS host's ruler.
        //
        // `SqueakLog.ShouldEmitDev` is still the policy, but it now selects the FIT AUDIT rather than whether
        // a scope exists at all: with detailed logging off this window is a geometry-only handle (nothing is
        // measured, the process-wide switch is untouched) and the layout-diagnosis controls still work.
        audit = UsTextFitAudit.Open(host, SqueakLog.ShouldEmitDev);
        return host;
    }

    /// <summary>The page box width to decide from: the last one fed, or the open-window policy before the
    /// first pass.</summary>
    private float FedOrPolicyPageWidth()
    {
        return actualPageWidth >= 0f
            ? actualPageWidth
            : WindowChromeLayout.SettingsOpenPageBoxWidth(Verse.UI.screenWidth, Verse.UI.screenHeight);
    }

    /// <summary>
    /// The one piece of frame bookkeeping the page cannot do for itself: a settings edit queued while
    /// the window is open must still flush on its own timer, or the footer status lies. The hover-claim
    /// frame boundary used to be called from here; since FL P3 <c>UiHost.DrawFrame</c> runs it on the
    /// session, so the window owns no frame protocol at all.
    /// <para>
    /// It also owns the presentation decision's INPUT: the width the shell actually handed this pass is fed
    /// here, before the page arranges, so the pre-resize box can never be presented with the wide column.
    /// When that input flips the shape, the layout clock is advanced (the toggle's own write does not cover
    /// a width-only change), because the engine's cached arrangement is keyed on the content revision.
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

        bool helpOpen = source?.ViewState.HelpDrawerOpen ?? false;
        bool sharesNow = helpOpen && WindowChromeLayout.DrawerSharesTheBody(contentRect.width);
        bool hadPrevious = actualPageWidth >= 0f;
        bool sharesBefore = helpOpen && hadPrevious
            && WindowChromeLayout.DrawerSharesTheBody(actualPageWidth);
        actualPageWidth = contentRect.width;
        if (hadPrevious && sharesNow != sharesBefore)
        {
            Host?.Session.ClosePopup();
            Host?.Session.BumpContentRevision();
        }

        // Backstop for WindowOnGUI's call: a pass that reaches the page without one still sizes the window.
        ApplyDrawerWidth();
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
        // Disposing the page host and its session is the shell's job.
        base.PreClose();
    }
}
