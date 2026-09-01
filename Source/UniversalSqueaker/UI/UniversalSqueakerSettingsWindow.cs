using UnityEngine;
using Verse;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Full-screen, custom-drawn settings window for Universal Squeaker.
///
/// Normal path: the window owns exactly one kernel <see cref="UiHost"/> (created from the real
/// embedded Schema=2 page resource) and draws ONLY the kernel page. Closing the window disposes the
/// host and its session; reopening creates a fresh host/session. Creation-time contract failures
/// (schema/binding/kind/attribute) and whole-frame draw failures fall back to the legacy full page,
/// which is never drawn together with the kernel page.
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

    private UiHost? kernelHost;
    private IUsKernelSettingsSource? kernelSource;
    private bool kernelPageFailed;
    private bool fallbackNextFrame;
    private bool legacySessionActive;
    private readonly UniversalSqueakerMod mod;

    internal bool UsesLegacySettingsSession => legacySessionActive;

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
        if (fallbackNextFrame)
        {
            fallbackNextFrame = false;
            kernelPageFailed = true;
            DrawLegacyPage(contentRect);
            return;
        }

        if (kernelPageFailed)
        {
            DrawLegacyPage(contentRect);
            return;
        }

        try
        {
            kernelHost ??= CreateKernelHost();
            kernelHost.DrawFrame(contentRect);
        }
        catch (System.Exception ex)
        {
            fallbackNextFrame = true;
            kernelHost?.Dispose();
            kernelHost = null;
            kernelSource = null;
            SqueakLog.SettingsOpenFailed(ex);
        }
    }

    private UiHost CreateKernelHost()
    {
        kernelSource = new UsKernelSettingsSource(UniversalSqueakerMod.Settings);
        return UsKernelSettingsHost.Create(kernelSource);
    }

    private void DrawLegacyPage(Rect rect)
    {
        legacySessionActive = true;
        mod.DoSettingsWindowContents(rect);
    }

    public override void PreClose()
    {
        kernelHost?.Dispose();
        kernelHost = null;
        kernelSource = null;
        base.PreClose();
    }

    private void DrawBackground(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, Palette.Canvas);
        Widgets.DrawBoxSolid(
            new Rect(rect.x, rect.y, rect.width, AccentBarHeight),
            Palette.AccentGold);
    }

    private void DrawTitleBar(Rect rect)
    {
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;

        Rect titleRect = new(
            rect.x + SidePadding,
            rect.y + 8f,
            Mathf.Max(1f, rect.width * 0.6f),
            TitleBarHeight - 16f);

        UiText.DrawTitle(new Rect(titleRect.x, titleRect.y, titleRect.width, 24f), mod.SettingsCategory());
        UiText.DrawCaption(new Rect(titleRect.x, titleRect.y + 24f, titleRect.width, 16f), "Universal Squeaker — VoicePack Routing");

        Rect closeRect = new(
            rect.xMax - CloseButtonWidth - SidePadding,
            rect.y + (TitleBarHeight - CloseButtonHeight) * 0.5f,
            CloseButtonWidth,
            CloseButtonHeight);
        DrawCloseButton(closeRect);

        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }

    private void DrawCloseButton(Rect rect)
    {
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, hovered ? Palette.Hover : Palette.Panel);
        SurfaceFrame.DrawBorder(rect, hovered ? Palette.BorderStrong : Palette.Border);

        UiText.DrawCaption(rect, "Close", hovered ? Palette.TextPrimary : Palette.TextSecondary);
        if (Widgets.ButtonInvisible(rect))
        {
            Close();
        }
    }
}
