using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Unified surface drawing helpers for the modern US skin. All fills/borders/emphasis lines go
/// through this entry point so widgets never hand-write surface colors.
/// </summary>
public static class UsSurface
{
    public enum SurfaceKind
    {
        Base,
        Panel,
        Raised,
        Hover,
        Selected,
        Warning,
        Success,
        Danger
    }

    public static void DrawSurface(Rect rect, SurfaceKind kind = SurfaceKind.Panel)
    {
        Color fill = kind switch
        {
            SurfaceKind.Base => UsVisualTokens.SurfaceBase,
            SurfaceKind.Raised => UsVisualTokens.Raised,
            SurfaceKind.Hover => UsVisualTokens.Hover,
            SurfaceKind.Selected => UsVisualTokens.Selected,
            SurfaceKind.Warning => UsVisualTokens.Warning,
            SurfaceKind.Success => UsVisualTokens.Success,
            SurfaceKind.Danger => UsVisualTokens.Danger,
            _ => UsVisualTokens.Panel,
        };
        Color border = kind switch
        {
            SurfaceKind.Selected => UsVisualTokens.BorderStrong,
            SurfaceKind.Warning => UsVisualTokens.Danger,
            SurfaceKind.Danger => UsVisualTokens.Danger,
            SurfaceKind.Success => UsVisualTokens.BorderStrong,
            _ => UsVisualTokens.Border,
        };
        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);
    }

    public static void DrawRowSurface(Rect rect, bool hover, bool selected, bool danger)
    {
        Color fill = danger ? UsVisualTokens.Danger
            : selected ? UsVisualTokens.Selected
            : hover ? UsVisualTokens.Hover
            : UsVisualTokens.Panel;
        Color border = danger ? UsVisualTokens.Danger
            : selected ? UsVisualTokens.BorderStrong
            : UsVisualTokens.Border;
        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        if (selected)
        {
            Widgets.DrawBoxSolid(
                new Rect(rect.x + 1f, rect.y + 1f, 4f, Mathf.Max(1f, rect.height - 2f)),
                UsVisualTokens.AccentGold);
        }
    }

    public static void DrawCardSurface(Rect rect, bool hover, bool selected)
    {
        Color fill = selected ? UsVisualTokens.Selected
            : hover ? UsVisualTokens.Hover
            : UsVisualTokens.Panel;
        Color border = selected ? UsVisualTokens.BorderStrong : UsVisualTokens.Border;
        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        if (selected)
        {
            Widgets.DrawBoxSolid(
                new Rect(rect.x + 1f, rect.yMax - 4f, Mathf.Max(1f, rect.width - 2f), 3f),
                UsVisualTokens.AccentGold);
        }
    }

    public static void DrawSegment(Rect rect, string label, bool selected)
    {
        bool hovered = Mouse.IsOver(rect);
        Color fill = selected ? UsVisualTokens.Selected
            : hovered ? UsVisualTokens.Hover
            : UsVisualTokens.Raised;
        Color border = selected ? UsVisualTokens.AccentGold
            : hovered ? UsVisualTokens.BorderStrong
            : UsVisualTokens.Border;
        Color text = selected ? UsVisualTokens.AccentGold : hovered ? UsVisualTokens.TextPrimary : UsVisualTokens.TextSecondary;

        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = text;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    public static void DrawCheckbox(Rect rect, bool value)
    {
        bool hovered = Mouse.IsOver(rect);
        Color fill = value ? UsVisualTokens.Selected : hovered ? UsVisualTokens.Hover : UsVisualTokens.Panel;
        Color border = value || hovered ? UsVisualTokens.BorderStrong : UsVisualTokens.Border;

        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        if (value)
        {
            // Approximate a gold check with two 2px solid bars.
            Widgets.DrawBoxSolid(new Rect(rect.x + 3f, rect.y + rect.height * 0.55f, 4f, 2f), UsVisualTokens.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 5f, rect.y + rect.height * 0.45f, 2f, 4f), UsVisualTokens.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 7f, rect.y + rect.height * 0.35f, 2f, 5f), UsVisualTokens.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 9f, rect.y + rect.height * 0.25f, 2f, 5f), UsVisualTokens.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 11f, rect.y + rect.height * 0.35f, 2f, 4f), UsVisualTokens.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 13f, rect.y + rect.height * 0.45f, 2f, 3f), UsVisualTokens.AccentGold);
        }
    }

    public static void DrawHeader(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UsVisualTokens.TextPrimary;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, text);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), UsVisualTokens.Border);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    public static void DrawBorder(Rect rect)
    {
        DrawBorder(rect, UsVisualTokens.Border);
    }

    public static void DrawBorder(Rect rect, Color color)
    {
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), color);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 1f, rect.height), color);
        Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }
}
