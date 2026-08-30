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
            UiPanel.DrawAccentBar(
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
        UiPanel.DrawPill(rect, label, selected);
    }

    public static void DrawCheckbox(Rect rect, bool value)
    {
        UiPanel.DrawCheckbox(rect, value);
    }

    public static void DrawHeader(Rect rect, string text)
    {
        UiText.DrawSection(rect, text);
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
