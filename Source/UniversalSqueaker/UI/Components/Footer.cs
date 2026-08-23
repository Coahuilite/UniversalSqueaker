using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Page footer. Currently a static save/persistence hint; no command output.</summary>
public static class Footer
{
    public static void Draw(Rect rect, string text)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = UiPalette.Muted;
        Widgets.Label(rect.ContractedBy(2f, 0f), text);
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }
}
