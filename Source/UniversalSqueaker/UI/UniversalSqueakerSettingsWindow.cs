using UnityEngine;
using Verse;
using FerriteLib.UiKit.Kernel;

// This window draws its own chrome with the kernel theme vocabulary; there is no second palette.

namespace UniversalSqueaker.UI;

/// <summary>
/// Custom-drawn settings window for Universal Squeaker.
///
/// The window owns exactly one kernel <see cref="UiHost"/> (created from the real embedded Schema=2
/// page resource) and draws that page and nothing else. Closing the window disposes the host and its
/// session; reopening creates a fresh host/session, so a reopened window gets a clean retry.
///
/// When the kernel page cannot be created, or a whole-frame draw throws, the window shows an honest
/// unavailable notice instead of a second implementation: recovery lives in the per-widget
/// <see cref="UiSessionGuard"/> for local failures, and there is deliberately no fallback page to
/// keep semantically in sync.
///
/// RimWorld default input stays intact: Escape closes via the window stack and Enter closes the
/// window through the stock Mod Settings behavior. US deliberately does not intercept Enter; the
/// only kernel-owned input handling is the per-text-field focus/commit logic inside widgets.
/// </summary>
public sealed class UniversalSqueakerSettingsWindow : Window
{
    private const float TitleBarHeight = 56f;
    private const float SidePadding = 20f;
    private const float CloseButtonWidth = 110f;
    private const float CloseButtonHeight = 30f;
    private const float AccentBarHeight = 3f;

    private static readonly UiTheme Theme = UiTheme.DarkGold;

    private UiHost? kernelHost;
    private IUsKernelSettingsSource? kernelSource;
    private bool pageUnavailable;
    private bool noticeDueNextFrame;
    private readonly UniversalSqueakerMod mod;

    public UniversalSqueakerSettingsWindow(UniversalSqueakerMod mod)
    {
        this.mod = mod;
        layer = WindowLayer.Dialog;
        forcePause = true;
        absorbInputAroundWindow = true;
        doCloseX = false;
        doCloseButton = false;
        doWindowBackground = false;
        drawShadow = false;
        closeOnClickedOutside = false;
        preventCameraMotion = true;
    }
    protected override float Margin => 0f;

    public override Vector2 InitialSize
    {
        get
        {
            // 维护者指定的安全区域：窗口在屏幕的 60%～75% 之间浮动，默认偏 72%/66%，
            // 不盖满全屏，也不退回原版那种正中心小方窗。
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
    }

    public override void DoWindowContents(Rect inRect)
    {
        DrawBackground(inRect);
        DrawTitleBar(inRect);

        Rect contentRect = new Rect(
            inRect.x + SidePadding,
            inRect.y + TitleBarHeight,
            Mathf.Max(1f, inRect.width - SidePadding * 2f),
            Mathf.Max(1f, inRect.height - TitleBarHeight - SidePadding));
        mod.TickSettingsSaveForWindow();

        // A carrier/consumer desync must never reach the draw path: the kernel page would die in a
        // TypeLoadException and the player would see a generic notice instead of the real cause.
        if (!UniversalSqueakerMod.PrerequisiteVerified)
        {
            DrawPrerequisiteNotice(contentRect);
            return;
        }

        if (pageUnavailable)
        {
            DrawUnavailableNotice(contentRect);
            return;
        }

        // A failed draw must not switch pages inside the same IMGUI pass that threw: that pass has
        // already claimed layout state for the kernel page. Trip the notice on the next frame so it is
        // drawn from a clean pass.
        if (noticeDueNextFrame)
        {
            noticeDueNextFrame = false;
            pageUnavailable = true;
            DrawUnavailableNotice(contentRect);
            return;
        }

        try
        {
            kernelHost ??= CreateKernelHost();
            // C+A help model + D10 grace: the hover claim is a per-frame transient, but the
            // release to the section overview waits out a short grace window so moving the
            // pointer between adjacent controls never flashes the overview
            // (VoicePacksPageModel.BeginHelpHoverFrame owns the rule; the claim is still cleared
            // here, the one place that owns the frame boundary, and widgets re-claim during the draw).
            kernelSource!.BeginHelpHoverFrame();
            kernelHost.DrawFrame(contentRect);
        }
        catch (System.Exception ex)
        {
            noticeDueNextFrame = true;
            kernelHost?.Dispose();
            kernelHost = null;
            kernelSource = null;
            SqueakLog.SettingsOpenFailed(ex);
        }
    }

    private UiHost CreateKernelHost()
    {
        kernelSource = new UsKernelSettingsSource(UniversalSqueakerMod.Settings);
        UsTextFitAudit.Begin();
        return UsKernelSettingsHost.Create(kernelSource);
    }

    /// <summary>Terminal state for this window instance: say what happened and how to recover, draw nothing else.</summary>
    private void DrawUnavailableNotice(Rect rect)
    {
        UiThemeDraw.Surface(rect, Theme, Theme.Panel, Theme.Border);
        Rect body = rect.ContractedBy(24f);
        UiThemeDraw.Label(
            new Rect(body.x, body.y, body.width, 30f),
            "US.Settings.PageUnavailable.Title".Translate(),
            Theme,
            Theme.TextPrimary,
            UiFont.Medium);
        UiThemeDraw.Label(
            new Rect(body.x, body.y + 38f, body.width, Mathf.Max(1f, body.height - 38f)),
            "US.Settings.PageUnavailable.Body".Translate(SqueakLabels.SettingsCategory),
            Theme,
            Theme.TextSecondary,
            UiFont.Small);
    }

    /// <summary>Named desync notice: the installed carrier mod is older than the API this build compiled against.</summary>
    private void DrawPrerequisiteNotice(Rect rect)
    {
        UiThemeDraw.Surface(rect, Theme, Theme.Panel, Theme.Border);
        Rect body = rect.ContractedBy(24f);
        UiThemeDraw.Label(
            new Rect(body.x, body.y, body.width, 30f),
            "US.Settings.Prerequisite.Title".Translate(),
            Theme,
            Theme.TextPrimary,
            UiFont.Medium);
        UiThemeDraw.Label(
            new Rect(body.x, body.y + 38f, body.width, Mathf.Max(1f, body.height - 38f)),
            "US.Settings.Prerequisite.Body".Translate(),
            Theme,
            Theme.TextSecondary,
            UiFont.Small);
    }

    public override void PreClose()
    {
        UsTextFitAudit.End();
        kernelHost?.Dispose();
        kernelHost = null;
        kernelSource = null;
        base.PreClose();
    }

    private void DrawBackground(Rect rect)
    {
        UiThemeDraw.Workspace(rect, Theme);
        UiThemeDraw.Surface(
            new Rect(rect.x, rect.y, rect.width, AccentBarHeight),
            Theme,
            Theme.AccentGold,
            Theme.AccentGold);
    }

    private void DrawTitleBar(Rect rect)
    {
        Rect titleRect = new(
            rect.x + SidePadding,
            rect.y + 6f,
            Mathf.Max(1f, rect.width * 0.6f),
            TitleBarHeight - 16f);

        UiThemeDraw.Label(
            new Rect(titleRect.x, titleRect.y, titleRect.width, 30f),
            mod.SettingsCategory(),
            Theme,
            Theme.TextPrimary,
            UiFont.Medium);
        UiThemeDraw.Label(
            new Rect(titleRect.x, titleRect.y + 30f, titleRect.width, 18f),
            Translator.Translate("US.Settings.Window.Subtitle"),
            Theme,
            Theme.TextSecondary,
            UiFont.Tiny);

        DrawCloseButton(new Rect(
            rect.xMax - CloseButtonWidth - SidePadding,
            rect.y + (TitleBarHeight - CloseButtonHeight) * 0.5f,
            CloseButtonWidth,
            CloseButtonHeight));
    }

    private void DrawCloseButton(Rect rect)
    {
        bool hovered = Mouse.IsOver(rect);
        UiThemeDraw.Surface(
            rect,
            Theme,
            hovered ? Theme.Hover : Theme.Panel,
            hovered ? Theme.BorderStrong : Theme.Border);
        UiThemeDraw.Label(
            rect,
            Translator.Translate("US.Settings.Window.Close"),
            Theme,
            hovered ? Theme.TextPrimary : Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleCenter);

        // Native IMGUI hit test: the window is host chrome, not a kernel widget, so it owns no session.
        if (Widgets.ButtonInvisible(rect))
        {
            Close();
        }
    }
}
