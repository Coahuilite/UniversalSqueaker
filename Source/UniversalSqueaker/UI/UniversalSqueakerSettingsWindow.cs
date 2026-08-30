using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>
/// Full-screen, custom-drawn settings window for Universal Squeaker.
///
/// RimWorld's stock <c>Dialog_ModSettings</c>/<c>Dialog_Options</c> are fixed-size centered
/// dialogs with the vanilla gray-black frame. This window covers nearly the whole screen,
/// disables the vanilla window background, and draws its own modern surface so the VoicePacks
/// page can use the full resolution while retaining a safe margin around the play field.
/// </summary>
public sealed class UniversalSqueakerSettingsWindow : Window
{
    private const float TitleBarHeight = 56f;
    private const float SidePadding = 20f;
    private const float CloseButtonWidth = 110f;
    private const float CloseButtonHeight = 30f;
    private const float AccentBarHeight = 3f;

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

    protected override float Margin => 0f;

    public override void DoWindowContents(Rect inRect)
    {
        DrawBackground(inRect);
        DrawTitleBar(inRect);

        Rect contentRect = new Rect(
            inRect.x + SidePadding,
            inRect.y + TitleBarHeight,
            Mathf.Max(1f, inRect.width - SidePadding * 2f),
            Mathf.Max(1f, inRect.height - TitleBarHeight - SidePadding));
        mod.DoSettingsWindowContents(contentRect);
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
