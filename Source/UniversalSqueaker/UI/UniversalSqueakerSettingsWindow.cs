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
    private static readonly UiTheme WindowTheme = UiTheme.DarkGold;

    private readonly UniversalSqueakerMod mod;

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

    /// <summary>
    /// 维护者指定的安全区域：窗口在屏幕的 60%～75% 之间浮动，默认偏 72%/66%，
    /// 不盖满全屏，也不退回原版那种正中心小方窗。The shell's default is the game's own
    /// <see cref="Window.InitialSize"/>, so this policy is the only thing keeping that ruling alive.
    /// </summary>
    protected override Func<Vector2>? InitialSizePolicy => InitialSizeFromScreen;

    private static Vector2 InitialSizeFromScreen()
    {
        float width = Mathf.Clamp(
            Verse.UI.screenWidth * 0.72f,
            Verse.UI.screenWidth * 0.60f,
            Verse.UI.screenWidth * 0.75f);
        float height = Mathf.Clamp(
            Verse.UI.screenHeight * 0.66f,
            Verse.UI.screenHeight * 0.60f,
            Verse.UI.screenHeight * 0.75f);
        width = Mathf.Max(800f, width);
        height = Mathf.Max(600f, height);
        return new Vector2(width, height);
    }

    /// <summary>
    /// A carrier/consumer desync must never reach the draw path: the kernel page would die in a
    /// <see cref="TypeLoadException"/> and the player would see a generic notice instead of the real
    /// cause. The shell short-circuits to <see cref="UiWindowNotice.Prerequisite"/> on false.
    /// </summary>
    protected override bool PrerequisiteVerified => UniversalSqueakerMod.PrerequisiteVerified;

    protected override UiHost CreateHost()
    {
        UsTextFitAudit.Begin();
        return UsKernelSettingsHost.Create(new UsKernelSettingsSource(UniversalSqueakerMod.Settings));
    }

    /// <summary>
    /// The one piece of frame bookkeeping the page cannot do for itself: a settings edit queued while
    /// the window is open must still flush on its own timer, or the footer status lies. The hover-claim
    /// frame boundary used to be called from here; since FL P3 <c>UiHost.DrawFrame</c> runs it on the
    /// session, so the window owns no frame protocol at all.
    /// </summary>
    protected override void BeforeDraw(Rect contentRect)
    {
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
        UsTextFitAudit.End();
        // Disposing the page host and its session is the shell's job.
        base.PreClose();
    }
}
