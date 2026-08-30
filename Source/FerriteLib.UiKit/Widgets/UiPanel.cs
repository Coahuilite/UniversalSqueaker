using UnityEngine;
using Verse;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit;

/// <summary>
/// Neutral, modern flat-panel drawing helpers shared by all UiKit skins.
/// These are intentionally tiny composites over the existing <see cref="SurfaceFrame"/>
/// primitives so a host mod gets consistent cards, headers, pills and checkboxes.
/// </summary>
public static class UiPanel
{
    public const float DividerThickness = 1f;
    public const float AccentBarWidth = 4f;

    /// <summary>Draws a framed surface; a thin convenience over <see cref="SurfaceFrame.Draw(Rect, SurfaceFrame.SurfaceKind)"/>.</summary>
    public static void Draw(Rect rect, SurfaceFrame.SurfaceKind kind = SurfaceFrame.SurfaceKind.Panel)
    {
        SurfaceFrame.Draw(rect, kind);
    }

    /// <summary>Draws a 1px divider line. Use for visual grouping without a full panel.</summary>
    public static void DrawDivider(Rect rect)
    {
        VerseWidgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width > 1f ? rect.width : 1f, DividerThickness), Palette.Divider);
    }

    /// <summary>Draws a section header: small label + bottom divider.</summary>
    public static void DrawHeader(Rect rect, string text, bool emphasized = false)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        try
        {
            Text.Font = emphasized ? GameFont.Small : GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = emphasized ? Palette.TextPrimary : Palette.TextSecondary;
            VerseWidgets.Label(rect, text);
            DrawDivider(new Rect(rect.x, rect.yMax - DividerThickness, rect.width, DividerThickness));
        }
        finally
        {
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }
    }

    /// <summary>Draws a small status pill (segmented control / badge).</summary>
    public static void DrawPill(Rect rect, string text, bool active, bool danger = false)
    {
        bool hovered = Mouse.IsOver(rect);
        Color fill = danger
            ? (active ? Palette.Danger : hovered ? Palette.Hover : Palette.Raised)
            : active
                ? Palette.Selected
                : hovered ? Palette.Hover : Palette.Raised;
        Color border = danger
            ? (active ? Palette.Danger : hovered ? Palette.BorderStrong : Palette.Border)
            : active
                ? Palette.AccentGold
                : hovered ? Palette.BorderStrong : Palette.Border;
        Color textColor = danger
            ? Palette.TextOnDanger
            : active ? Palette.AccentGold : hovered ? Palette.TextPrimary : Palette.TextSecondary;

        VerseWidgets.DrawBoxSolid(rect, fill);
        SurfaceFrame.DrawBorder(rect, border);

        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        try
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = textColor;
            VerseWidgets.Label(rect, text);
        }
        finally
        {
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }
    }

    /// <summary>Draws a checkbox with the modern gold-on-selected treatment.</summary>
    public static void DrawCheckbox(Rect rect, bool value)
    {
        bool hovered = Mouse.IsOver(rect);
        Color fill = value ? Palette.Selected : hovered ? Palette.Hover : Palette.Panel;
        Color border = value ? Palette.AccentGold : hovered ? Palette.BorderStrong : Palette.Border;

        VerseWidgets.DrawBoxSolid(rect, fill);
        SurfaceFrame.DrawBorder(rect, border);

        if (value)
        {
            // Approximate a gold check with a few 2px bars (kept dependency-free).
            VerseWidgets.DrawBoxSolid(new Rect(rect.x + 3f, rect.y + rect.height * 0.55f, 4f, 2f), Palette.AccentGold);
            VerseWidgets.DrawBoxSolid(new Rect(rect.x + 5f, rect.y + rect.height * 0.45f, 2f, 4f), Palette.AccentGold);
            VerseWidgets.DrawBoxSolid(new Rect(rect.x + 7f, rect.y + rect.height * 0.35f, 2f, 5f), Palette.AccentGold);
            VerseWidgets.DrawBoxSolid(new Rect(rect.x + 9f, rect.y + rect.height * 0.25f, 2f, 5f), Palette.AccentGold);
            VerseWidgets.DrawBoxSolid(new Rect(rect.x + 11f, rect.y + rect.height * 0.35f, 2f, 4f), Palette.AccentGold);
            VerseWidgets.DrawBoxSolid(new Rect(rect.x + 13f, rect.y + rect.height * 0.45f, 2f, 3f), Palette.AccentGold);
        }
    }

    /// <summary>Draws a left accent bar, used for selected rows/cards.</summary>
    public static void DrawAccentBar(Rect rect, Color color)
    {
        VerseWidgets.DrawBoxSolid(new Rect(rect.x, rect.y, AccentBarWidth, rect.height > 1f ? rect.height : 1f), color);
    }
}
