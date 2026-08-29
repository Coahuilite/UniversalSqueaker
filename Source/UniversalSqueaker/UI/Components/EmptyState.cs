using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Centered empty/error state. No state, no command output.</summary>
public static class EmptyState
{
    public static void Draw(Rect rect, string text)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Panel);
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = UsVisualTokens.TextSecondary;
        Widgets.Label(rect.ContractedBy(16f), text);
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }
}
