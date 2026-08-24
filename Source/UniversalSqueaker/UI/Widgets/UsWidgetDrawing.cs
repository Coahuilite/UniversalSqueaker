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
        GUI.color = Color.white;
        Widgets.Label(rect, text);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    public static void DrawSectionHeader(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = new Color(.95f, .92f, .84f);
        Widgets.Label(rect, text);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }
}
