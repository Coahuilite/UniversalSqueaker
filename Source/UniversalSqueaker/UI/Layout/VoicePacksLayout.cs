using System;

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
    public const float ModeCardHeight = 64f;
    public const float RaceLayerRowHeight = 50f;
    public const float SearchFieldHeight = 30f;
    public const float VoicePackRowHeight = 74f;
    public const float FooterHeight = 26f;
    public const float EmptyStateHeight = 44f;
    public const float SectionHeaderHeight = 24f;

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

    public static int CountShownPacks(VoicePackDomainView domain, string search)
    {
        string query = search?.Trim() ?? "";
        if (query.Length == 0) return domain.Packs.Count;
        int count = 0;
        foreach (VoicePackRowView row in domain.Packs)
        {
            if (row.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.DefName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count++;
            }
        }
        return count;
    }

    public static float ChecklistHeight(VoicePackDomainView domain, string search, float width, ITextMetrics metrics)
    {
        float height = SearchFieldHeight + Gap;
        int shown = CountShownPacks(domain, search);
        height += shown > 0 ? shown * VoicePackRowHeight : EmptyStateHeight;
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

    public static float MeasureContentHeight(float pageWidth, VoicePacksViewState view, VoicePacksPageState state, ITextMetrics metrics)
    {
        float width = InnerWidth(pageWidth);
        float height = Padding * 2f + TitleHeight + Gap;

        if (!string.IsNullOrEmpty(view.BannerText))
            height += BannerHeight(view.BannerText, width, metrics) + Gap;

        height += ModeCardHeight + Gap;

        if (view.Races.Count > 0)
        {
            height += SectionHeaderHeightFor("Race Layer", width, metrics) + Gap;
            height += view.Races.Count * (RaceLayerRowHeight + Gap);
        }

        if (view.SelectedDomain != null)
        {
            height += SectionHeaderHeightFor("VoicePack Checklist", width, metrics) + Gap;
            height += ChecklistHeight(view.SelectedDomain.Value, state.SearchText, width, metrics) + Gap;
        }

        height += FooterHeight + Padding;
        return height;
    }
}
