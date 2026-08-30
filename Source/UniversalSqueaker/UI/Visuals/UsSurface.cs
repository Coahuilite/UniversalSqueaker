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
        SelectionButton.DrawSurface(
            rect,
            selected,
            danger,
            enabled: true,
            accent: SelectionButton.SelectionAccent.Left,
            hovered: hover,
            baseColor: Palette.Panel);
    }

    public static void DrawCardSurface(Rect rect, bool hover, bool selected)
    {
        SelectionButton.DrawSurface(
            rect,
            selected,
            danger: false,
            enabled: true,
            accent: SelectionButton.SelectionAccent.Bottom,
            hovered: hover,
            baseColor: Palette.Panel);
    }

    public static void DrawSegment(Rect rect, string label, bool selected)
    {
        SelectionButton.Draw(rect, label, selected, font: UiFont.Tiny);
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
