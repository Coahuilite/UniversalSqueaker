using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>
/// US surface drawing helpers. Primitive fills/borders now delegate to the neutral UiKit
/// <see cref="SurfaceFrame"/>; this class keeps the US-specific composite helpers (rows, segments,
/// checkboxes, headers) and the old <see cref="SurfaceKind"/> names for compatibility.
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
        SurfaceFrame.Draw(rect, ToUi(kind));
    }

    public static void DrawBorder(Rect rect)
    {
        SurfaceFrame.DrawBorder(rect);
    }

    public static void DrawBorder(Rect rect, Color color)
    {
        SurfaceFrame.DrawBorder(rect, color);
    }

    public static void DrawRowSurface(Rect rect, bool hover, bool selected, bool danger)
    {
        Color fill = danger ? Palette.Danger
            : selected ? Palette.Selected
            : hover ? Palette.Hover
            : Palette.Panel;
        Color border = danger ? Palette.Danger
            : selected ? Palette.BorderStrong
            : Palette.Border;
        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        if (selected)
        {
            Widgets.DrawBoxSolid(
                new Rect(rect.x + 1f, rect.y + 1f, 4f, Mathf.Max(1f, rect.height - 2f)),
                Palette.AccentGold);
        }
    }

    public static void DrawCardSurface(Rect rect, bool hover, bool selected)
    {
        Color fill = selected ? Palette.Selected
            : hover ? Palette.Hover
            : Palette.Panel;
        Color border = selected ? Palette.BorderStrong : Palette.Border;
        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        if (selected)
        {
            Widgets.DrawBoxSolid(
                new Rect(rect.x + 1f, rect.yMax - 4f, Mathf.Max(1f, rect.width - 2f), 3f),
                Palette.AccentGold);
        }
    }

    public static void DrawSegment(Rect rect, string label, bool selected)
    {
        bool hovered = Mouse.IsOver(rect);
        Color fill = selected ? Palette.Selected
            : hovered ? Palette.Hover
            : Palette.Raised;
        Color border = selected ? Palette.AccentGold
            : hovered ? Palette.BorderStrong
            : Palette.Border;
        Color text = selected ? Palette.AccentGold : hovered ? Palette.TextPrimary : Palette.TextSecondary;

        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Tiny;
        GUI.color = text;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, label);
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    public static void DrawCheckbox(Rect rect, bool value)
    {
        bool hovered = Mouse.IsOver(rect);
        Color fill = value ? Palette.Selected : hovered ? Palette.Hover : Palette.Panel;
        Color border = value ? Palette.AccentGold : hovered ? Palette.BorderStrong : Palette.Border;

        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, border);

        if (value)
        {
            // Approximate a gold check with two 2px solid bars.
            Widgets.DrawBoxSolid(new Rect(rect.x + 3f, rect.y + rect.height * 0.55f, 4f, 2f), Palette.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 5f, rect.y + rect.height * 0.45f, 2f, 4f), Palette.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 7f, rect.y + rect.height * 0.35f, 2f, 5f), Palette.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 9f, rect.y + rect.height * 0.25f, 2f, 5f), Palette.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 11f, rect.y + rect.height * 0.35f, 2f, 4f), Palette.AccentGold);
            Widgets.DrawBoxSolid(new Rect(rect.x + 13f, rect.y + rect.height * 0.45f, 2f, 3f), Palette.AccentGold);
        }
    }

    public static void DrawHeader(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Text.Font = GameFont.Small;
        GUI.color = Palette.TextPrimary;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, text);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Palette.Border);
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static SurfaceFrame.SurfaceKind ToUi(SurfaceKind kind)
    {
        return kind switch
        {
            SurfaceKind.Raised => SurfaceFrame.SurfaceKind.Raised,
            SurfaceKind.Hover => SurfaceFrame.SurfaceKind.Hover,
            SurfaceKind.Panel => SurfaceFrame.SurfaceKind.Panel,
            SurfaceKind.Selected => SurfaceFrame.SurfaceKind.Selected,
            SurfaceKind.Warning => SurfaceFrame.SurfaceKind.Warning,
            SurfaceKind.Success => SurfaceFrame.SurfaceKind.Success,
            SurfaceKind.Danger => SurfaceFrame.SurfaceKind.Danger,
            _ => SurfaceFrame.SurfaceKind.Base,
        };
    }
}
