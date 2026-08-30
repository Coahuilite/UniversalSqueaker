using UnityEngine;
using FerriteLib.UiKit.Widgets;

namespace FerriteLib.UiKit;

/// <summary>
/// Neutral typography helpers for the modern skin. Host widgets should use these instead of
/// touching <c>Text.Font</c>/<c>GUI.color</c> directly so the skin stays consistent.
/// </summary>
public static class UiText
{
    public static void DrawTitle(Rect rect, string text, Color? color = null)
    {
        UiKitGui.Label(rect, text, UiFont.Medium, TextAnchor.MiddleLeft, color ?? Palette.TextPrimary);
    }

    public static void DrawLabel(Rect rect, string text, Color? color = null)
    {
        UiKitGui.Label(rect, text, UiFont.Small, TextAnchor.MiddleLeft, color ?? Palette.TextPrimary);
    }

    public static void DrawCaption(Rect rect, string text, Color? color = null)
    {
        UiKitGui.Label(rect, text, UiFont.Tiny, TextAnchor.MiddleLeft, color ?? Palette.TextSecondary);
    }

    public static void DrawSection(Rect rect, string text, bool emphasized = true)
    {
        UiKitGui.Label(rect, text, emphasized ? UiFont.Small : UiFont.Tiny, TextAnchor.MiddleLeft,
            emphasized ? Palette.TextPrimary : Palette.TextSecondary);
        UiPanel.DrawDivider(new Rect(rect.x, rect.yMax - UiPanel.DividerThickness, rect.width, UiPanel.DividerThickness));
    }
}
