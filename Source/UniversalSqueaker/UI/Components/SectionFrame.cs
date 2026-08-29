using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Compatibility surface frame forwarding to <see cref="UsSurface"/>. Kept only while external page
/// shell code still references the old surface entry point.
/// </summary>
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
        UsSurface.DrawSurface(rect, ToUs(kind));
    }

    public static void DrawBorder(Rect rect)
    {
        UsSurface.DrawBorder(rect);
    }

    public static void DrawBorder(Rect rect, Color color)
    {
        UsSurface.DrawBorder(rect, color);
    }

    private static UsSurface.SurfaceKind ToUs(SurfaceKind kind)
    {
        return kind switch
        {
            SurfaceKind.Raised => UsSurface.SurfaceKind.Raised,
            SurfaceKind.Emphasized => UsSurface.SurfaceKind.Raised,
            SurfaceKind.Warning => UsSurface.SurfaceKind.Warning,
            SurfaceKind.Success => UsSurface.SurfaceKind.Success,
            _ => UsSurface.SurfaceKind.Base,
        };
    }
}
