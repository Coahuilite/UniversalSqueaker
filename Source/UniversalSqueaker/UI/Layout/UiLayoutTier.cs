using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Zero-Verse layout tier classification for the VoicePacks page.
/// </summary>
public enum LayoutTier
{
    Comfortable,
    Compact,
    Minimal,
    Fallback
}

/// <summary>
/// Pure width-tier helpers. No Verse/Unity dependencies.
/// </summary>
public static class UiLayoutTier
{
    public const float MinComfortableWidth = 480f;
    public const float MinCompactWidth = 320f;
    public const float MinMinimalWidth = 240f;

    public static LayoutTier ForWidth(float width)
    {
        if (width >= MinComfortableWidth) return LayoutTier.Comfortable;
        if (width >= MinCompactWidth) return LayoutTier.Compact;
        if (width >= MinMinimalWidth) return LayoutTier.Minimal;
        return LayoutTier.Fallback;
    }

    public static float ClampWidth(float width, float min)
    {
        return Math.Max(min, width);
    }
}
