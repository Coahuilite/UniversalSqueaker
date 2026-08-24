using UnityEngine;
using Verse;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Widgets;

internal static class UiKitGui
{
    public static void Label(Rect rect, string text, UiFont font, TextAnchor anchor, Color color)
    {
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        try
        {
            Text.Font = UiKitFonts.ToGameFont(font);
            Text.Anchor = anchor;
            GUI.color = color;
            VerseWidgets.Label(rect, text);
        }
        finally
        {
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }
    }
}
