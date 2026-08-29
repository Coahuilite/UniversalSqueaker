using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// A single sampled point of the distance audibility preview curve.
/// Audibility is normalized to the 0..1 range.
/// </summary>
public readonly struct DistanceSample
{
    public readonly float Distance;
    public readonly float Audibility;

    public DistanceSample(float distance, float audibility)
    {
        Distance = distance;
        Audibility = audibility;
    }
}

/// <summary>
/// Pure deterministic sampler for the distance preview chart. Zero Verse/Unity dependencies.
/// Semantics: d &lt;= min is fully audible (1), min &lt; d &lt; max fades linearly to 0,
/// and d &gt;= max is silent (0).
/// </summary>
public static class DistancePreview
{
    private const float DefaultMin = 15f;
    private const float DefaultMax = 50f;
    private const float DefaultGraphMin = 15f;
    private const float DefaultGraphMax = 65f;
    private const int MinSampleCount = 2;
    private const int MaxSampleCount = 512;

    public static IReadOnlyList<DistanceSample> SampleAudibilityCurve(
        float min,
        float max,
        int sampleCount,
        float graphMin,
        float graphMax)
    {
        if (float.IsNaN(min) || float.IsInfinity(min)
            || float.IsNaN(max) || float.IsInfinity(max))
        {
            min = DefaultMin;
            max = DefaultMax;
        }

        if (min > max)
        {
            float swap = min;
            min = max;
            max = swap;
        }

        if (sampleCount < MinSampleCount)
        {
            sampleCount = MinSampleCount;
        }
        else if (sampleCount > MaxSampleCount)
        {
            sampleCount = MaxSampleCount;
        }

        if (float.IsNaN(graphMin) || float.IsInfinity(graphMin)
            || float.IsNaN(graphMax) || float.IsInfinity(graphMax)
            || graphMin >= graphMax)
        {
            graphMin = DefaultGraphMin;
            graphMax = DefaultGraphMax;
        }

        DistanceSample[] samples = new DistanceSample[sampleCount];

        if (min == max)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                float distance = Lerp(graphMin, graphMax, (float)i / (sampleCount - 1));
                samples[i] = new DistanceSample(distance, 1f);
            }

            return samples;
        }

        float range = max - min;
        for (int i = 0; i < sampleCount; i++)
        {
            float distance = Lerp(graphMin, graphMax, (float)i / (sampleCount - 1));
            float audibility;
            if (distance <= min)
            {
                audibility = 1f;
            }
            else if (distance >= max)
            {
                audibility = 0f;
            }
            else
            {
                audibility = 1f - (distance - min) / range;
            }

            samples[i] = new DistanceSample(distance, audibility);
        }

        return samples;
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * t;
    }
}
