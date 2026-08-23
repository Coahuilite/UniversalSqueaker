using System;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// VoicePack checkbox domain: search + pack rows + empty/status/orphan handling.
/// Stateless aside from the caller-owned search string; emits TogglePack / ForgetUnavailable.
/// </summary>
public static class VoicePackChecklist
{
    public static void Draw(Rect rect, VoicePackDomainView domain, ref string search, Action<UiCommand> emit)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        float y = rect.y;

        if (domain.IsDormant)
            y = DrawBanner(rect, y, "Biotech is not active; this Xenotype domain is dormant.", SectionFrame.SurfaceKind.Warning);
        if (domain.IsTargetUnavailable)
            y = DrawBanner(rect, y, "The selected Xenotype target is not loaded. Selections are retained for recovery.", SectionFrame.SurfaceKind.Warning);
        if (domain.HasCanonicalConflict)
            y = DrawBanner(rect, y, "Multiple Xenotype Defs share this target; routing fails closed until resolved.", SectionFrame.SurfaceKind.Warning);

        Rect searchRect = new(rect.x, y, rect.width, VoicePacksLayout.SearchFieldHeight);
        SearchField.Draw(searchRect, ref search, "Search VoicePacks…");
        y += VoicePacksLayout.SearchFieldHeight + VoicePacksLayout.Gap;

        string query = search?.Trim() ?? "";
        int shown = 0;
        foreach (VoicePackRowView row in domain.Packs)
        {
            if (!MatchesSearch(row, query)) continue;
            Rect rowRect = new(rect.x, y, rect.width, VoicePacksLayout.VoicePackRowHeight);
            VoicePackRow.Draw(rowRect, row, domain.Scope, domain.RaceDefName, domain.TargetDefName, emit);
            y += VoicePacksLayout.VoicePackRowHeight;
            shown++;
        }

        if (shown == 0)
        {
            Rect emptyRect = new(rect.x, y, rect.width, VoicePacksLayout.EmptyStateHeight);
            EmptyState.Draw(emptyRect, domain.Packs.Count == 0
                ? "No VoicePacks are installed for this domain."
                : "No VoicePacks match the current search.");
            y += VoicePacksLayout.EmptyStateHeight;
        }

        if (domain.OrphanCount > 0)
        {
            float bannerHeight = VoicePacksLayout.BannerHeight(
                "Selected pack keys are no longer installed. Use Forget Unavailable to clean them.",
                rect.width, VerseTextMetrics.Instance);
            Rect banner = new(rect.x, y, rect.width, bannerHeight);
            DrawOrphanBanner(banner, domain, emit);
        }
    }

    private static float DrawBanner(Rect outer, float y, string text, SectionFrame.SurfaceKind kind)
    {
        float height = VoicePacksLayout.BannerHeight(text, outer.width, VerseTextMetrics.Instance);
        StatusBanner.Draw(new Rect(outer.x, y, outer.width, height), text, kind);
        return y + height + VoicePacksLayout.Gap;
    }

    private static void DrawOrphanBanner(Rect rect, VoicePackDomainView domain, Action<UiCommand> emit)
    {
        SectionFrame.Draw(rect, SectionFrame.SurfaceKind.Warning);
        Rect button = new(rect.xMax - 132f, rect.y + 5f, 124f, Math.Max(20f, rect.height - 10f));
        Rect text = new(rect.x + 8f, rect.y + 5f, Math.Max(1f, button.x - rect.x - 16f), Math.Max(1f, rect.height - 10f));
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(1f, .67f, .48f);
        Widgets.Label(text, "Selected pack keys are no longer installed. Use Forget Unavailable to clean them.");
        Text.Font = oldFont;
        GUI.color = oldColor;
        if (Widgets.ButtonText(button, "Forget Unavailable"))
        {
            emit?.Invoke(new UiCommand(
                UiCommandKind.ForgetUnavailable,
                scope: domain.Scope,
                raceDefName: domain.RaceDefName,
                targetDefName: domain.TargetDefName,
                arg: "",
                flag: false));
        }
    }

    private static bool MatchesSearch(VoicePackRowView row, string query)
    {
        if (query.Length == 0) return true;
        return row.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || row.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || row.DefName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || row.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
