using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Centered empty/error state. No state, no command output.</summary>
public static class EmptyState
{
    public static void Draw(Rect rect, string text)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        SectionFrame.Draw(rect);
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = UiPalette.Muted;
        Widgets.Label(rect.ContractedBy(16f), text);
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }
}
