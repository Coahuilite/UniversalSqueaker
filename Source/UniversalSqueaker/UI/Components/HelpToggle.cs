using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Help "?" toggle. Stateless: returns whether the user clicked it this frame.</summary>
public static class HelpToggle
{
    public static bool Draw(Rect rect, bool active)
    {
        bool clicked = false;
        UsGuard.DrawOrFallback(
            rect,
            () =>
            {
                bool hovered = Mouse.IsOver(rect);
                UsSurface.DrawSurface(rect,
                    active ? UsSurface.SurfaceKind.Selected
                    : hovered ? UsSurface.SurfaceKind.Hover
                    : UsSurface.SurfaceKind.Panel);
                UsSurface.DrawBorder(rect, active || hovered ? UsVisualTokens.AccentGold : UsVisualTokens.Border);

                Color oldColor = GUI.color;
                TextAnchor oldAnchor = Text.Anchor;
                GameFont oldFont = Text.Font;
                GUI.color = active || hovered ? UsVisualTokens.AccentGold : UsVisualTokens.TextSecondary;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "?");
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;

                clicked = Widgets.ButtonInvisible(rect);
            },
            fallback =>
            {
                clicked = Widgets.ButtonText(fallback, "?");
            },
            "us/help-toggle");

        return clicked;
    }
}
