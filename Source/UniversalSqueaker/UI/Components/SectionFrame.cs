using UnityEngine;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>
/// Compatibility surface frame forwarding to the neutral UiKit <see cref="SurfaceFrame"/>.
/// Kept only while diagnostics/shell code still references the old names.
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

    private static SurfaceFrame.SurfaceKind ToUi(SurfaceKind kind)
    {
        return kind switch
        {
            SurfaceKind.Raised => SurfaceFrame.SurfaceKind.Raised,
            SurfaceKind.Emphasized => SurfaceFrame.SurfaceKind.Raised,
            SurfaceKind.Warning => SurfaceFrame.SurfaceKind.Warning,
            SurfaceKind.Success => SurfaceFrame.SurfaceKind.Success,
            _ => SurfaceFrame.SurfaceKind.Base,
        };
    }
}
