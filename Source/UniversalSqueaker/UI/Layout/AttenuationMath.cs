using System.Globalization;

namespace UniversalSqueaker.UI;

/// <summary>
/// Zero-Verse math for the camera-height attenuation editor. Kept free of UnityEngine/Verse so the
/// UI logic test project can compile and verify it directly.
/// </summary>
public static class AttenuationMath
{
    public const float MinDistance = 15f;
    public const float MaxDistance = 65f;
    public const float MinRange = 5f;

    public static float Clamp(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return min;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static void SanitizeRange(ref float min, ref float max)
    {
        if (float.IsNaN(min) || float.IsInfinity(min)) min = MinDistance;
        if (float.IsNaN(max) || float.IsInfinity(max)) max = 50f;
        min = Clamp(min, MinDistance, MaxDistance - MinRange);
        max = Clamp(max, MinDistance + MinRange, MaxDistance);
        if (max < min + MinRange)
        {
            max = Min(MaxDistance, min + MinRange);
        }
    }

    public static float ChartX(float chartX, float chartWidth, float distance)
    {
        float t = InverseLerp(MinDistance, MaxDistance, distance);
        return chartX + Clamp01(t) * chartWidth;
    }

    public static float DistanceFromX(float chartX, float chartWidth, float mouseX)
    {
        float t = InverseLerp(chartX, chartX + chartWidth, mouseX);
        return Lerp(MinDistance, MaxDistance, Clamp01(t));
    }

    public static string FormatRange(float start, float end)
    {
        return start.ToString("0.###", CultureInfo.InvariantCulture) + "|" + end.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string FormatRangeDisplay(float start, float end)
    {
        return start.ToString("0.#", CultureInfo.InvariantCulture) + "–" + end.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static float Min(float a, float b) => a < b ? a : b;

    private static float InverseLerp(float a, float b, float value)
    {
        if (a == b) return 0f;
        return (value - a) / (b - a);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}
