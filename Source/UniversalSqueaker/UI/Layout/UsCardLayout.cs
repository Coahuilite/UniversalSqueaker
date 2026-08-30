using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Zero-Verse card height math shared by <see cref="UsCard"/> and UI logic tests.
/// Keeping this in the pure layout folder lets tests assert TitleHidden measure/draw parity
/// without referencing UnityEngine or Verse.
/// </summary>
public static class UsCardLayout
{
    public const float Padding = 12f;
    public const float HeaderHeight = 26f;
    public const float HeaderGap = 6f;

    public static float MeasureBody(float bodyHeight, bool titleHidden)
    {
        float safeBody = Math.Max(0f, bodyHeight);
        return titleHidden
            ? Padding + safeBody + Padding
            : Padding + HeaderHeight + HeaderGap + safeBody + Padding;
    }
}
