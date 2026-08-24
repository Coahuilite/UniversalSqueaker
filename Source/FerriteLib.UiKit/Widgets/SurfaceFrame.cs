using UnityEngine;
using Verse;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Widgets;

internal enum SurfaceKind
{
    Base,
    Raised,
    Emphasized,
    Warning,
    Success
}

/// <summary>Neutral framed surface: background fill + 1px border.</summary>
internal static class SurfaceFrame
{
    public static void Draw(Rect rect, SurfaceKind kind = SurfaceKind.Base)
    {
        Color fill = kind switch
        {
            SurfaceKind.Raised => Palette.Raised,
            SurfaceKind.Emphasized => Palette.Emphasized,
            SurfaceKind.Warning => Palette.Warning,
            SurfaceKind.Success => Palette.Success,
            _ => Palette.Ink
        };
        VerseWidgets.DrawBoxSolid(rect, fill);
        DrawBorder(rect, Palette.Border);
    }

    public static void DrawBorder(Rect rect)
    {
        DrawBorder(rect, Palette.Border);
    }

    public static void DrawBorder(Rect rect, Color color)
    {
        VerseWidgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), color);
        VerseWidgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        VerseWidgets.DrawBoxSolid(new Rect(rect.x, rect.y, 1f, rect.height), color);
        VerseWidgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }
}
