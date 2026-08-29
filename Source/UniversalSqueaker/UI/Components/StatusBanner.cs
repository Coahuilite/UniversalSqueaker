using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Single status banner: framed surface + centered/multiline text. No state, no command output.</summary>
public static class StatusBanner
{
    public static void Draw(Rect rect, string text, SectionFrame.SurfaceKind kind)
    {
        Draw(rect, text, ToUs(kind));
    }

    public static void Draw(Rect rect, string text, UsSurface.SurfaceKind kind = UsSurface.SurfaceKind.Panel)
    {
        if (rect.width <= 1f || rect.height <= 1f || string.IsNullOrEmpty(text)) return;
        UsSurface.DrawSurface(rect, kind);
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = kind switch
        {
            UsSurface.SurfaceKind.Warning => UsVisualTokens.TextOnDanger,
            UsSurface.SurfaceKind.Danger => UsVisualTokens.TextOnDanger,
            UsSurface.SurfaceKind.Success => UsVisualTokens.TextOnGold,
            _ => UsVisualTokens.TextSecondary,
        };
        Widgets.Label(rect.ContractedBy(8f, 4f), text);
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }

    private static UsSurface.SurfaceKind ToUs(SectionFrame.SurfaceKind kind)
    {
        return kind switch
        {
            SectionFrame.SurfaceKind.Raised => UsSurface.SurfaceKind.Raised,
            SectionFrame.SurfaceKind.Emphasized => UsSurface.SurfaceKind.Raised,
            SectionFrame.SurfaceKind.Warning => UsSurface.SurfaceKind.Warning,
            SectionFrame.SurfaceKind.Success => UsSurface.SurfaceKind.Success,
            _ => UsSurface.SurfaceKind.Base,
        };
    }
}
