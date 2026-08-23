using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Help "?" toggle. Stateless: returns whether the user clicked it this frame.</summary>
public static class HelpToggle
{
    public static bool Draw(Rect rect, bool active)
    {
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, active ? new Color(.28f, .22f, .13f, .96f)
            : hovered ? new Color(.20f, .17f, .12f, .96f)
            : new Color(.10f, .095f, .085f, .92f));
        SectionFrame.DrawBorder(rect, active || hovered ? UiPalette.Gold : UiPalette.Border);
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        GUI.color = active || hovered ? new Color(1f, .86f, .60f) : UiPalette.Muted;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, "?");
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
        return Widgets.ButtonInvisible(rect);
    }
}
