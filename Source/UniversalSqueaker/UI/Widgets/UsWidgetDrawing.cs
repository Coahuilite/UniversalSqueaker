using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Small shared drawing helpers for the US Ferrite widgets.</summary>
internal static class UsWidgetDrawing
{
    public static void DrawTitle(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Medium;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(rect, text);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    public static void DrawSectionHeader(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Widgets.Label(rect, text);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), UsVisualTokens.Border);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }
}
