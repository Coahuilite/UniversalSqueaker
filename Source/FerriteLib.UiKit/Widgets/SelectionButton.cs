using System;
using UnityEngine;
using Verse;
using FerriteLib.UiKit.Widgets;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit;

/// <summary>
/// Neutral "vanilla reference language" selectable button helpers.
///
/// These draw a framed surface whose selected state is marked by a gold (or danger-red)
/// accent bar: a bottom bar like the reference settings UI's Primary/SelectableCard, or a left bar
/// like SettingSelector. The helper is intentionally text-neutral and does not register
/// interaction; callers keep their existing UiInteract/Widgets button registration.
/// </summary>
public static class SelectionButton
{
    public enum SelectionAccent
    {
        Bottom,
        Left
    }

    public const float BottomAccentHeight = 3f;
    public const float LeftAccentWidth = 4f;
    public const float FieldTextPadding = 6f;
    public const float CaretSlotWidth = 14f;

    /// <summary>
    /// Draws only the framed surface and the selected accent bar. Useful for rows/cards that
    /// need custom label layout.
    /// </summary>
    public static void DrawSurface(
        Rect rect,
        bool selected,
        bool danger = false,
        bool enabled = true,
        SelectionAccent accent = SelectionAccent.Bottom,
        bool? hovered = null,
        Color? baseColor = null)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        bool isHovered = hovered ?? Mouse.IsOver(rect);
        Color fill = baseColor ?? (danger
            ? selected ? Palette.Danger : isHovered ? Palette.Hover : Palette.Raised
            : selected ? Palette.Selected : isHovered ? Palette.Hover : Palette.Raised);
        Color border = danger
            ? selected ? Palette.Danger : isHovered ? Palette.BorderStrong : Palette.Border
            : selected ? Palette.AccentGold : isHovered ? Palette.BorderStrong : Palette.Border;

        VerseWidgets.DrawBoxSolid(rect, fill);
        SurfaceFrame.DrawBorder(rect, border);

        if (selected && enabled)
        {
            DrawAccent(rect, danger ? Palette.Danger : Palette.AccentGold, accent);
        }
    }

    /// <summary>
    /// Draws a centered selectable button with a selected accent bar. The returned value is the
    /// effective hover state so callers that also register a button can share the computation.
    /// </summary>
    public static bool Draw(
        Rect rect,
        string label,
        bool selected,
        bool danger = false,
        bool enabled = true,
        SelectionAccent accent = SelectionAccent.Bottom,
        bool? hovered = null,
        UiFont font = UiFont.Small)
    {
        if (rect.width <= 1f || rect.height <= 1f) return hovered ?? Mouse.IsOver(rect);

        bool isHovered = hovered ?? Mouse.IsOver(rect);
        DrawSurface(rect, selected, danger, enabled, accent, isHovered);

        if (string.IsNullOrEmpty(label)) return isHovered;

        Color textColor = danger
            ? selected ? Palette.TextOnDanger : isHovered ? Palette.TextPrimary : Palette.TextSecondary
            : selected ? Palette.TextOnGold : isHovered ? Palette.TextPrimary : Palette.TextSecondary;

        UiKitGui.Label(rect, label, font, TextAnchor.MiddleCenter, textColor);
        return isHovered;
    }

    /// <summary>
    /// Draws a dropdown/select trigger field: framed surface, selected accent, left-aligned text
    /// and a simple caret. Keep dropdown interaction registration in the owning widget.
    /// </summary>
    public static void DrawField(
        Rect rect,
        string display,
        bool selected,
        bool danger = false,
        bool enabled = true,
        SelectionAccent accent = SelectionAccent.Bottom,
        bool? hovered = null,
        UiFont font = UiFont.Small)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        bool isHovered = hovered ?? Mouse.IsOver(rect);
        DrawSurface(rect, selected, danger, enabled, accent, isHovered);

        if (!string.IsNullOrEmpty(display))
        {
            float textWidth = Math.Max(1f, rect.width - FieldTextPadding * 2f - CaretSlotWidth);
            Rect textRect = new(
                rect.x + FieldTextPadding,
                rect.y,
                textWidth,
                rect.height);

            Color textColor = danger
                ? selected ? Palette.TextOnDanger : Palette.TextPrimary
                : selected ? Palette.TextOnGold : Palette.TextPrimary;
            UiKitGui.Label(textRect, display, font, TextAnchor.MiddleLeft, textColor);
        }

        DrawCaret(rect);
    }

    private static void DrawAccent(Rect rect, Color color, SelectionAccent accent)
    {
        switch (accent)
        {
            case SelectionAccent.Bottom:
                VerseWidgets.DrawBoxSolid(
                    new Rect(rect.x + 1f, rect.yMax - BottomAccentHeight, Math.Max(1f, rect.width - 2f), BottomAccentHeight),
                    color);
                break;
            case SelectionAccent.Left:
                VerseWidgets.DrawBoxSolid(
                    new Rect(rect.x + 1f, rect.y + 1f, LeftAccentWidth, Math.Max(1f, rect.height - 2f)),
                    color);
                break;
        }
    }

    private static void DrawCaret(Rect rect)
    {
        float caretX = rect.xMax - CaretSlotWidth + 2f;
        float caretY = rect.y + rect.height * 0.5f;
        VerseWidgets.DrawBoxSolid(new Rect(caretX, caretY - 1f, 8f, 1f), Palette.TextSecondary);
        VerseWidgets.DrawBoxSolid(new Rect(caretX, caretY, 8f, 1f), Palette.TextSecondary);
        VerseWidgets.DrawBoxSolid(new Rect(caretX, caretY + 1f, 8f, 1f), Palette.TextSecondary);
    }
}
