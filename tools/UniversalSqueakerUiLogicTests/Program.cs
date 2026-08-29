using System;
using System.Collections.Generic;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// Self-contained console gate for the zero-Verse UI pure logic.
/// Prints ALL GREEN and returns 0 on success; any assertion failure throws and returns non-zero.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        try
        {
            RunAll();
            Console.WriteLine("ALL GREEN");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
    }

    private static void RunAll()
    {
        TestDistancePreview();
        TestVoicePacksFilters();
        TestUiLayoutTier();
        TestAttenuationMath();
    }

    private static void TestUiLayoutTier()
    {
        Assert(UiLayoutTier.ForWidth(480f) == LayoutTier.Comfortable, "480 is Comfortable");
        Assert(UiLayoutTier.ForWidth(320f) == LayoutTier.Compact, "320 is Compact");
        Assert(UiLayoutTier.ForWidth(240f) == LayoutTier.Minimal, "240 is Minimal");
        Assert(UiLayoutTier.ForWidth(239f) == LayoutTier.Fallback, "239 is Fallback");
        AssertEqual(10f, UiLayoutTier.ClampWidth(5f, 10f), 0.0001f, "ClampWidth raises below min");
        AssertEqual(20f, UiLayoutTier.ClampWidth(20f, 10f), 0.0001f, "ClampWidth keeps above min");
    }

    private static void TestDistancePreview()
    {
        const float tolerance = 0.00001f;

        IReadOnlyList<DistanceSample> curve = DistancePreview.SampleAudibilityCurve(15f, 50f, 10, 15f, 65f);
        Assert(curve.Count == 10, "default curve should contain 10 samples");
        AssertEqual(15f, curve[0].Distance, tolerance, "default curve first sample should be at graphMin");
        AssertEqual(1f, curve[0].Audibility, tolerance, "default curve first sample should be fully audible");
        AssertEqual(65f, curve[curve.Count - 1].Distance, tolerance, "default curve last sample should be at graphMax");
        AssertEqual(0f, curve[curve.Count - 1].Audibility, tolerance, "default curve last sample should be silent");
        AssertMonotonicNonIncreasing(curve, "default curve");

        IReadOnlyList<DistanceSample> twoSamples = DistancePreview.SampleAudibilityCurve(15f, 50f, 2, 15f, 65f);
        Assert(twoSamples.Count == 2, "sampleCount=2 should produce exactly two samples");
        AssertEqual(1f, twoSamples[0].Audibility, tolerance, "two-sample curve starts at 1");
        AssertEqual(0f, twoSamples[1].Audibility, tolerance, "two-sample curve ends at 0");

        IReadOnlyList<DistanceSample> twoHundred = DistancePreview.SampleAudibilityCurve(15f, 50f, 200, 15f, 65f);
        Assert(twoHundred.Count == 200, "sampleCount=200 should produce 200 samples");
        AssertEqual(1f, twoHundred[0].Audibility, tolerance, "200-sample curve starts at 1");
        AssertEqual(0f, twoHundred[twoHundred.Count - 1].Audibility, tolerance, "200-sample curve ends at 0");
        AssertMonotonicNonIncreasing(twoHundred, "200-sample curve");

        IReadOnlyList<DistanceSample> degenerate = DistancePreview.SampleAudibilityCurve(30f, 30f, 6, 15f, 65f);
        Assert(degenerate.Count == 6, "min==max curve should keep requested sample count");
        foreach (DistanceSample sample in degenerate)
        {
            AssertEqual(1f, sample.Audibility, tolerance, "min==max curve should be all 1");
        }

        IReadOnlyList<DistanceSample> swapped = DistancePreview.SampleAudibilityCurve(50f, 15f, 10, 15f, 65f);
        AssertEqual(1f, swapped[0].Audibility, tolerance, "swapped min/max should behave like min=15,max=50 at graphMin");
        AssertEqual(0f, swapped[swapped.Count - 1].Audibility, tolerance, "swapped min/max should behave like min=15,max=50 at graphMax");

        IReadOnlyList<DistanceSample> nanDefended = DistancePreview.SampleAudibilityCurve(float.NaN, float.PositiveInfinity, 8, 15f, 65f);
        AssertEqual(1f, nanDefended[0].Audibility, tolerance, "NaN/Infinity min/max should fall back to 15/50 at graphMin");
        AssertEqual(0f, nanDefended[nanDefended.Count - 1].Audibility, tolerance, "NaN/Infinity min/max should fall back to 15/50 at graphMax");

        IReadOnlyList<DistanceSample> badGraph = DistancePreview.SampleAudibilityCurve(15f, 50f, 8, 65f, 15f);
        AssertEqual(15f, badGraph[0].Distance, tolerance, "invalid graphMin/graphMax should fall back to 15/65");
        AssertEqual(65f, badGraph[badGraph.Count - 1].Distance, tolerance, "invalid graphMin/graphMax should fall back to 15/65");

        IReadOnlyList<DistanceSample> clampedCount = DistancePreview.SampleAudibilityCurve(15f, 50f, 1000, 15f, 65f);
        Assert(clampedCount.Count == 512, "sampleCount > 512 should clamp to 512");
    }

    private static void TestVoicePacksFilters()
    {
        UiDomainFilter emptyDomain = new UiDomainFilter();
        Assert(VoicePacksFilters.DomainMatches(false, false, false, false, false, null, in emptyDomain),
            "empty domain filter should match an all-false row");
        Assert(VoicePacksFilters.DomainMatches(true, true, true, true, true, "Some Author", in emptyDomain),
            "empty domain filter should match a mixed row");

        UiPackFilter emptyPack = new UiPackFilter();
        Assert(VoicePacksFilters.PackMatches(null, null, false, in emptyPack),
            "empty pack filter should match an unselected row");

        UiDomainFilter enabledOnlyDomain = new UiDomainFilter(enabledOnly: true);
        Assert(VoicePacksFilters.DomainMatches(false, false, false, false, true, null, in enabledOnlyDomain),
            "EnabledOnly domain filter should keep enabled rows");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, false, null, in enabledOnlyDomain),
            "EnabledOnly domain filter should drop disabled rows");

        UiPackFilter enabledOnlyPack = new UiPackFilter(enabledOnly: true);
        Assert(VoicePacksFilters.PackMatches(null, null, true, in enabledOnlyPack),
            "EnabledOnly pack filter should keep selected rows");
        Assert(!VoicePacksFilters.PackMatches(null, null, false, in enabledOnlyPack),
            "EnabledOnly pack filter should drop unselected rows");

        UiDomainFilter conflictOnly = new UiDomainFilter(conflictOnly: true);
        Assert(VoicePacksFilters.DomainMatches(true, false, false, false, false, null, in conflictOnly),
            "ConflictOnly should match hasConflict");
        Assert(VoicePacksFilters.DomainMatches(false, true, false, false, false, null, in conflictOnly),
            "ConflictOnly should match isDormant");
        Assert(VoicePacksFilters.DomainMatches(false, false, true, false, false, null, in conflictOnly),
            "ConflictOnly should match isTargetUnavailable");
        Assert(VoicePacksFilters.DomainMatches(false, false, false, true, false, null, in conflictOnly),
            "ConflictOnly should match isOrphan");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, false, null, in conflictOnly),
            "ConflictOnly should drop rows with no conflict-like flag");

        UiDomainFilter orphanOnly = new UiDomainFilter(orphanOnly: true);
        Assert(VoicePacksFilters.DomainMatches(false, false, false, true, false, null, in orphanOnly),
            "OrphanOnly should match isOrphan");
        Assert(!VoicePacksFilters.DomainMatches(true, true, true, false, true, null, in orphanOnly),
            "OrphanOnly should ignore other flags when isOrphan is false");

        UiDomainFilter authorDomain = new UiDomainFilter(author: "AUTHOR");
        Assert(VoicePacksFilters.DomainMatches(false, false, false, false, true, "My Author", in authorDomain),
            "Domain author filter should match case-insensitive contains");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, true, "Someone Else", in authorDomain),
            "Domain author filter should drop non-matching authors");

        UiPackFilter authorPack = new UiPackFilter(author: "AUTHOR");
        Assert(VoicePacksFilters.PackMatches("My Author", null, false, in authorPack),
            "Pack author filter should match author");
        Assert(VoicePacksFilters.PackMatches(null, "AUTHOR MOD", false, in authorPack),
            "Pack author filter should match modName");
        Assert(!VoicePacksFilters.PackMatches("Other", "Other Mod", false, in authorPack),
            "Pack author filter should drop rows where neither author nor modName matches");

        UiDomainFilter combinedDomain = new UiDomainFilter(enabledOnly: true, author: "AUTHOR");
        Assert(VoicePacksFilters.DomainMatches(false, false, false, false, true, "My Author", in combinedDomain),
            "Combined domain filter should keep rows satisfying both conditions");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, false, "My Author", in combinedDomain),
            "Combined domain filter should drop rows failing EnabledOnly");
        Assert(!VoicePacksFilters.DomainMatches(false, false, false, false, true, "Other", in combinedDomain),
            "Combined domain filter should drop rows failing Author");

        UiPackFilter combinedPack = new UiPackFilter(enabledOnly: true, author: "AUTHOR");
        Assert(VoicePacksFilters.PackMatches("My Author", null, true, in combinedPack),
            "Combined pack filter should keep rows satisfying both conditions");
        Assert(!VoicePacksFilters.PackMatches("My Author", null, false, in combinedPack),
            "Combined pack filter should drop rows failing EnabledOnly");
        Assert(!VoicePacksFilters.PackMatches("Other", null, true, in combinedPack),
            "Combined pack filter should drop rows failing Author");
    }

    private static void TestAttenuationMath()
    {
        const float tolerance = 0.0001f;

        AssertEqual(0f, AttenuationMath.ChartX(0f, 100f, AttenuationMath.MinDistance), tolerance, "ChartX min maps to chart left");
        AssertEqual(100f, AttenuationMath.ChartX(0f, 100f, AttenuationMath.MaxDistance), tolerance, "ChartX max maps to chart right");
        AssertEqual(50f, AttenuationMath.ChartX(0f, 100f, 40f), tolerance, "ChartX 40/65 maps to middle");
        AssertEqual(AttenuationMath.MinDistance, AttenuationMath.DistanceFromX(0f, 100f, 0f), tolerance, "DistanceFromX left maps to MinDistance");
        AssertEqual(AttenuationMath.MaxDistance, AttenuationMath.DistanceFromX(0f, 100f, 100f), tolerance, "DistanceFromX right maps to MaxDistance");
        AssertEqual(40f, AttenuationMath.DistanceFromX(0f, 100f, 50f), tolerance, "DistanceFromX middle maps to 40");

        float min = float.NaN;
        float max = float.PositiveInfinity;
        AttenuationMath.SanitizeRange(ref min, ref max);
        AssertEqual(AttenuationMath.MinDistance, min, tolerance, "SanitizeRange NaN min becomes MinDistance");
        AssertEqual(50f, max, tolerance, "SanitizeRange Infinity max becomes 50");

        min = 10f;
        max = 70f;
        AttenuationMath.SanitizeRange(ref min, ref max);
        AssertEqual(AttenuationMath.MinDistance, min, tolerance, "SanitizeRange clamps min to 15");
        AssertEqual(AttenuationMath.MaxDistance, max, tolerance, "SanitizeRange clamps max to 65");

        min = 60f;
        max = 61f;
        AttenuationMath.SanitizeRange(ref min, ref max);
        Assert(max >= min + AttenuationMath.MinRange, "SanitizeRange enforces MinRange");

        Assert(AttenuationMath.FormatRange(15f, 50f) == "15|50", "FormatRange uses | separator");
        Assert(AttenuationMath.FormatRangeDisplay(15f, 50f).Contains("–"), "FormatRangeDisplay uses en dash");
    }

    private static void AssertMonotonicNonIncreasing(IReadOnlyList<DistanceSample> samples, string message)
    {
        const float tolerance = 0.00001f;
        for (int i = 0; i < samples.Count - 1; i++)
        {
            if (samples[i].Audibility + tolerance < samples[i + 1].Audibility)
            {
                throw new InvalidOperationException(
                    message + " is not monotonic non-increasing at index " + i
                    + ": " + samples[i].Audibility + " -> " + samples[i + 1].Audibility);
            }
        }
    }

    private static void AssertEqual(float expected, float actual, float tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(message + " (expected " + expected + ", got " + actual + ")");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
