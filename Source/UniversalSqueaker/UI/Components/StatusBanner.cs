using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Single status banner: framed surface + centered/multiline text. No state, no command output.</summary>
public static class StatusBanner
{
    public static void Draw(Rect rect, string text, SectionFrame.SurfaceKind kind = SectionFrame.SurfaceKind.Base)
    {
        if (rect.width <= 1f || rect.height <= 1f || string.IsNullOrEmpty(text)) return;
        SectionFrame.Draw(rect, kind);
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = kind switch
        {
            SectionFrame.SurfaceKind.Warning => new Color(1f, .67f, .48f),
            SectionFrame.SurfaceKind.Success => UiPalette.Gold,
            _ => UiPalette.Muted
        };
        Widgets.Label(rect.ContractedBy(8f, 4f), text);
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }
}
