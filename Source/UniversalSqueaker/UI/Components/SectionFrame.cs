using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Section surface frame: background fill + border. No state, no command output.</summary>
public static class SectionFrame
{
    public enum SurfaceKind
    {
        Base,
        Raised,
        Emphasized,
        Warning,
        Success
    }

    public static void Draw(Rect rect, SurfaceKind kind = SurfaceKind.Base)
    {
        Color fill = kind switch
        {
            SurfaceKind.Raised => UiPalette.Raised,
            SurfaceKind.Emphasized => UiPalette.Emphasized,
            SurfaceKind.Warning => UiPalette.Warning,
            SurfaceKind.Success => UiPalette.Success,
            _ => UiPalette.Ink
        };
        Widgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect);
    }

    public static void DrawBorder(Rect rect)
    {
        DrawBorder(rect, UiPalette.Border);
    }

    public static void DrawBorder(Rect rect, Color color)
    {
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), color);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 1f, rect.height), color);
        Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }
}
