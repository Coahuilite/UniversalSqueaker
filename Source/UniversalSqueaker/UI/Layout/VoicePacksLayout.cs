using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure layout/measurement for the VoicePacks page. No Verse/Unity references; text-dependent
/// measurements go through <see cref="ITextMetrics"/>. All methods are deterministic from
/// (width, view state, search, counts).
/// </summary>
public static class VoicePacksLayout
{
    public const float Padding = 8f;
    public const float Gap = 6f;
    public const float SmallGap = 3f;
    public const float TitleHeight = 26f;
    public const float RaceLayerRowHeight = 50f;
    public const float SearchFieldHeight = 30f;
    public const float VoicePackRowHeight = 74f;
    public const float EmptyStateHeight = 44f;
    public const float SectionHeaderHeight = 24f;

    /// <summary>Minimum height for a row that can hold two stacked text lines.</summary>
    public const float MinTwoLineRowHeight = 40f;
    /// <summary>Minimum height for a VoicePack row that shows the three-line detail layout.</summary>
    public const float MinVoicePackRowHeight = 74f;
    /// <summary>Minimum height for a race/xenotype layer row with optional detail line.</summary>
    public const float MinLayerRowHeight = 50f;

    public const float MinComfortableWidth = UiLayoutTier.MinComfortableWidth;
    public const float MinCompactWidth = UiLayoutTier.MinCompactWidth;
    public const float MinMinimalWidth = UiLayoutTier.MinMinimalWidth;

    public static LayoutTier ForWidth(float width) => UiLayoutTier.ForWidth(width);

    public static float ClampWidth(float width, float min) => UiLayoutTier.ClampWidth(width, min);

    public static float InnerWidth(float pageWidth)
    {
        return Math.Max(1f, pageWidth - Padding * 2f);
    }

    public static float BannerHeight(string text, float width, ITextMetrics metrics)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        float contentWidth = Math.Max(1f, width - 16f);
        return Math.Max(34f, metrics.CalcHeight(text, contentWidth) + 12f);
    }

    public static float SectionHeaderHeightFor(string text, float width, ITextMetrics metrics)
    {
        if (string.IsNullOrEmpty(text)) return SectionHeaderHeight;
        return Math.Max(SectionHeaderHeight, metrics.CalcHeight(text, Math.Max(1f, width)) + 2f);
    }

    /// <summary>
    /// Caption shown under a race/xenotype layer row. Rows with no installed packs say
    /// "No available packs" instead of "0 / 0 enabled" so players do not read them as unsupported.
    /// </summary>
    public static string LayerDetailText(int enabledCount, int candidateCount, string stateSuffix = "")
    {
        if (candidateCount <= 0)
        {
            return "No available packs" + stateSuffix;
        }

        return enabledCount + " / " + candidateCount + " enabled" + stateSuffix;
    }

    /// <summary>
    /// Height for a generic single-line row that may wrap. <paramref name="contentWidth"/> is the
    /// actual width available to the label; padding adds breathing room above and below the text.
    /// </summary>
    public static float MeasuredRowHeight(string text, float contentWidth, ITextMetrics metrics, float minHeight)
    {
        float textHeight = metrics.CalcHeight(text ?? "", Math.Max(1f, contentWidth));
        return Math.Max(minHeight, 4f + textHeight + 4f);
    }

    /// <summary>
    /// Height for a two-line composite row (primary label + secondary caption). Both lines are
    /// measured through <paramref name="metrics"/>; callers may pass a font-specific adapter when
    /// the two lines use different fonts.
    /// </summary>
    public static float TwoLineRowHeight(string firstLine, string secondLine, float width, ITextMetrics metrics)
    {
        float contentWidth = Math.Max(1f, width - 20f);
        float firstHeight = metrics.CalcHeight(firstLine ?? "", contentWidth);
        float secondHeight = metrics.CalcHeight(secondLine ?? "", contentWidth);
        return Math.Max(MinTwoLineRowHeight, 4f + firstHeight + 2f + secondHeight + 4f);
    }

    /// <summary>
    /// Height for a race/xenotype layer row. The detail line is only shown on comfortable widths,
    /// matching the row renderer's responsive behavior.
    /// </summary>
    public static float LayerRowHeightFor(string displayName, string detail, float width, ITextMetrics metrics)
    {
        bool showDetail = ForWidth(width) == LayoutTier.Comfortable;
        float contentWidth = Math.Max(1f, width - 24f);
        float nameHeight = metrics.CalcHeight(displayName ?? "", contentWidth);
        float detailHeight = showDetail ? metrics.CalcHeight(detail ?? "", contentWidth) : 0f;
        float contentHeight = 4f + nameHeight + (showDetail ? 2f + detailHeight : 0f) + 4f;
        return Math.Max(MinLayerRowHeight, contentHeight);
    }

    /// <summary>
    /// Height for a VoicePack checklist row. Comfortable widths show Label + Mod/Author + Coverage;
    /// narrower widths omit Coverage and use the two-line minimum.
    /// </summary>
    public static float VoicePackRowHeightFor(string label, string modAuthor, string coverage, float width, ITextMetrics metrics)
    {
        bool showCoverage = ForWidth(width) == LayoutTier.Comfortable;
        float contentWidth = Math.Max(1f, width - 44f);
        float labelHeight = Math.Max(20f, metrics.CalcHeight(label ?? "", contentWidth));
        float metaHeight = Math.Max(16f, metrics.CalcHeight(modAuthor ?? "", contentWidth));
        float coverageHeight = showCoverage ? Math.Max(16f, metrics.CalcHeight(coverage ?? "", contentWidth)) : 0f;
        float contentHeight = 3f + labelHeight + 2f + metaHeight + (showCoverage ? 2f + coverageHeight : 0f) + 4f;
        return Math.Max(showCoverage ? MinVoicePackRowHeight : MinTwoLineRowHeight, contentHeight);
    }

    public static float VoicePackRowHeightFor(VoicePackRowView row, float width, ITextMetrics metrics)
    {
        return VoicePackRowHeightFor(
            row.Label,
            row.ModName + " · " + row.Author,
            row.Coverage,
            width,
            metrics);
    }

    public static int CountShownPacks(VoicePackDomainView domain, string search)
    {
        IReadOnlyList<VoicePackRowView> packs = domain.Packs ?? Array.Empty<VoicePackRowView>();
        string query = search?.Trim() ?? "";
        if (query.Length == 0) return packs.Count;
        int count = 0;
        foreach (VoicePackRowView row in packs)
        {
            if (MatchesSearch(row, query)) count++;
        }
        return count;
    }

    public static float ChecklistHeight(VoicePackDomainView domain, string search, float width, ITextMetrics metrics)
    {
        float height = SearchFieldHeight + Gap;
        IReadOnlyList<VoicePackRowView> packs = domain.Packs ?? Array.Empty<VoicePackRowView>();
        string query = search?.Trim() ?? "";
        int shown = 0;
        foreach (VoicePackRowView row in packs)
        {
            if (!MatchesSearch(row, query)) continue;
            height += VoicePackRowHeightFor(row, width, metrics);
            shown++;
        }

        if (shown == 0) height += EmptyStateHeight;
        if (domain.OrphanCount > 0)
            height += BannerHeight("Selected pack keys are no longer installed. Use Forget Unavailable to clean them.", width, metrics) + Gap;
        if (domain.IsDormant)
            height += BannerHeight("Biotech is not active; this Xenotype domain is dormant.", width, metrics) + Gap;
        if (domain.IsTargetUnavailable)
            height += BannerHeight("The selected Xenotype target is not loaded. Its selections are retained for recovery.", width, metrics) + Gap;
        if (domain.HasCanonicalConflict)
            height += BannerHeight("Multiple Xenotype Defs share this target; routing fails closed until the conflict is resolved.", width, metrics) + Gap;
        return height + Gap;
    }

    private static bool MatchesSearch(VoicePackRowView row, string query)
    {
        return query.Length == 0
            || row.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || row.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || row.DefName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || row.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
