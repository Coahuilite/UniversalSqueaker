using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Full-screen, custom-drawn settings window for Universal Squeaker.
///
/// RimWorld's stock <c>Dialog_ModSettings</c>/<c>Dialog_Options</c> are fixed-size centered
/// dialogs with the vanilla gray-black frame. This window instead covers the whole screen,
/// disables the vanilla window background, and draws its own surface so the VoicePacks page can
/// use the full resolution (1920x1080, 2560x1440, etc.).
/// </summary>
public sealed class UniversalSqueakerSettingsWindow : Window
{
    private const float TitleBarHeight = 48f;
    private const float SidePadding = 16f;
    private const float CloseButtonWidth = 120f;
    private const float CloseButtonHeight = 32f;
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

    public override Vector2 InitialSize => new Vector2(Verse.UI.screenWidth, Verse.UI.screenHeight);

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
        Widgets.DrawBoxSolid(rect, UsVisualTokens.SurfaceBase);
        Widgets.DrawBoxSolid(
            new Rect(rect.x, rect.y, rect.width, AccentBarHeight),
            UsVisualTokens.AccentGold);
    }

    private void DrawTitleBar(Rect rect)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(
            new Rect(rect.x + SidePadding, rect.y + 8f, Mathf.Max(1f, rect.width * 0.6f), TitleBarHeight - 16f),
            mod.SettingsCategory());

        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;

        Rect closeRect = new Rect(
            rect.xMax - CloseButtonWidth - SidePadding,
            rect.y + 8f,
            CloseButtonWidth,
            CloseButtonHeight);
        if (Widgets.ButtonText(closeRect, "Close".Translate()))
        {
            Close();
        }
    }
}
