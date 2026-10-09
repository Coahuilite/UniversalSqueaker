using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// US-PACK1: ONE input row of the pack-card projection - a (pack, domain) PAIR with everything the
/// composed filter reads, as plain values. The Verse side (settings + catalog) collects these pairs;
/// this file turns them into cards. The split is the same one <see cref="UsChecklistFilter"/> and
/// <see cref="VoicePacksFilters"/> established: the filter semantics live in a pure function both the
/// production projection and the harness drive, so a lane tests THE rule the page uses, not a copy of it.
/// </summary>
public readonly struct PackCardSourceRow
{
    // The pack half (identity as CreateVoicePackRow composes it; SearchText stays on the pack row).
    public readonly string PackKey;
    public readonly string Label;
    public readonly string DefName;
    public readonly string ModName;
    public readonly string Author;
    public readonly string Coverage;

    // The domain half.
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    public readonly string TargetDefName;
    public readonly string DomainDisplay;

    // Filter inputs: is THIS pack enabled in THIS domain, and the domain's state flags exactly as
    // VoicePacksFilters.DomainMatches takes them.
    public readonly bool PackEnabledInDomain;
    public readonly bool HasConflict;
    public readonly bool IsDormant;
    public readonly bool IsTargetUnavailable;
    public readonly bool DomainHasOrphan;

    public PackCardSourceRow(
        string packKey, string label, string defName, string modName, string author, string coverage,
        SqueakVoicePackScope scope, string raceDefName, string targetDefName, string domainDisplay,
        bool packEnabledInDomain, bool hasConflict, bool isDormant, bool isTargetUnavailable, bool domainHasOrphan)
    {
        PackKey = packKey ?? "";
        Label = label ?? "";
        DefName = defName ?? "";
        ModName = modName ?? "";
        Author = author ?? "";
        Coverage = coverage ?? "";
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
        DomainDisplay = domainDisplay ?? "";
        PackEnabledInDomain = packEnabledInDomain;
        HasConflict = hasConflict;
        IsDormant = isDormant;
        IsTargetUnavailable = isTargetUnavailable;
        DomainHasOrphan = domainHasOrphan;
    }
}

/// <summary>
/// US-PACK1 (§4.2): the pure pack-card projection. Every input pair becomes a row of its pack's card
/// when the COMPOSED filter accepts it - and a card exists while one row does.
/// <para>
/// <b>The conditions and where each acts.</b> The author dropdown is a PACK condition
/// (<see cref="VoicePacksFilters.PackMatches"/>); the race/xenotype dropdowns are DOMAIN conditions
/// (a race-filtered pair passes by its race half; a xenotype-filtered pair must BE that xenotype's
/// domain - race domains never carry one); the state flags are DOMAIN conditions over the same four
/// answers <see cref="VoicePacksFilters.DomainMatches"/> already defines; and the keyword accepts a
/// pair when it matches the PACK half (label/DefName/Mod/author/key) OR the DOMAIN half (display name
/// and both defNames) - one shared substring rule, <see cref="UsChecklistFilter.QueryMatches"/>, no
/// second predicate, no tokenization (PM ruling 2026-10-07 unchanged).
/// </para>
/// <para>
/// <b>Expansion.</b> The manual set is the player's own gesture and the ONLY thing a result-layer
/// cancel collapses - and only the VISIBLE part of it (the host's cancel consults this projection, so
/// a manual key the current conditions filter out stays retained and returns when the conditions do).
/// Auto-expansion is derived from the active XENOTYPE condition: a keyword hit on a XENOTYPE domain's
/// half, OR the xenotype dropdown naming the domain (review-1: the dropdown alone must expose the
/// matching content), opens the card that provides it (the user-confirmed §1 rule); a pack-name hit
/// lists the card collapsed. Clearing the condition restores exactly the manual set - and neither
/// condition ever writes it, which is the same boundary the filter never writes enable.
/// </para>
/// <para>
/// <b>Order.</b> Cards follow the FIRST appearance of each pack key across the input scan; rows follow
/// scan order. The scan (model side) visits race domains in catalog order then the sorted xenotype
/// inventory, so the answer is deterministic without this function knowing any of that.
/// </para>
/// </summary>
public static class PackCardProjection
{
    public static List<PackCardView> Build(
        IReadOnlyList<PackCardSourceRow> source,
        string query,
        in UiPackFilter author,
        string raceFilter,
        string xenotypeFilter,
        in UiDomainFilter state,
        IReadOnlyCollection<string> manualExpanded)
    {
        var cards = new List<PackCardView>();
        if (source == null || source.Count == 0) return cards;

        string trimmed = (query ?? "").Trim();
        bool keyword = trimmed.Length > 0;
        bool xenoFilterActive = !string.IsNullOrEmpty(xenotypeFilter);
        var keys = new List<string>();
        var rowsByKey = new Dictionary<string, List<PackCardDomainRowView>>(StringComparer.Ordinal);
        var headByKey = new Dictionary<string, PackCardSourceRow>(StringComparer.Ordinal);

        for (int i = 0; i < source.Count; i++)
        {
            PackCardSourceRow pair = source[i];
            if (string.IsNullOrEmpty(pair.PackKey)) continue;

            // Author: the pack half, same predicate the composite row loop uses.
            if (!VoicePacksFilters.PackMatches(pair.Author, pair.ModName, in author)) continue;

            // Race/xenotype dropdowns: the domain half.
            if (pair.Scope == SqueakVoicePackScope.Xenotype)
            {
                if (!VoicePacksFilters.XenotypeFilterMatches(
                        raceFilter, xenotypeFilter, pair.RaceDefName, pair.TargetDefName)) continue;
            }
            else
            {
                if (!VoicePacksFilters.RaceFilterMatches(raceFilter, pair.RaceDefName)) continue;
                if (!string.IsNullOrEmpty(xenotypeFilter)) continue; // a xenotype condition never selects a race domain
            }

            // State flags: the domain's four answers plus THIS pair's enable.
            if (!VoicePacksFilters.DomainMatches(
                    pair.HasConflict, pair.IsDormant, pair.IsTargetUnavailable,
                    pair.DomainHasOrphan, pair.PackEnabledInDomain, in state)) continue;
            // Keyword: pack half OR domain half. The auto-expand trigger (review-1 correction 1) is a
            // hit on the XENOTYPE domain's half from EITHER condition of the region that names a
            // xenotype: the keyword matching the domain half, or the ACTIVE xenotype dropdown - a row
            // that survived that dropdown IS the matching content §4.2 demands be exposed, even with an
            // empty keyword. The trigger stays a per-row flag: it never enters the manual set and
            // never writes enable, so clearing the dropdown restores exactly the player's own state.
            bool packHit = keyword && UsChecklistFilter.QueryMatches(
                trimmed, pair.Label, pair.DefName, pair.ModName, pair.Author, pair.PackKey);
            bool domainHit = keyword && UsChecklistFilter.QueryMatches(
                trimmed, pair.DomainDisplay, pair.TargetDefName, pair.RaceDefName);
            if (keyword && !packHit && !domainHit) continue;
            bool autoTrigger = pair.Scope == SqueakVoicePackScope.Xenotype
                && (domainHit || xenoFilterActive);

            if (!rowsByKey.TryGetValue(pair.PackKey, out List<PackCardDomainRowView> rows))
            {
                rows = new List<PackCardDomainRowView>();
                rowsByKey[pair.PackKey] = rows;
                headByKey[pair.PackKey] = pair;
                keys.Add(pair.PackKey);
            }

            rows.Add(new PackCardDomainRowView(
                pair.Scope, pair.RaceDefName, pair.TargetDefName, pair.DomainDisplay,
                pair.PackEnabledInDomain, autoTrigger));
        }

        for (int c = 0; c < keys.Count; c++)
        {
            string key = keys[c];
            PackCardSourceRow head = headByKey[key];
            List<PackCardDomainRowView> rows = rowsByKey[key];
            bool manual = ContainsOrdinal(manualExpanded, key);
            bool auto = false;
            for (int r = 0; r < rows.Count; r++) if (rows[r].AutoTrigger) { auto = true; break; }
            cards.Add(new PackCardView(
                key, head.Label, head.DefName, head.ModName, head.Author, head.Coverage,
                manual, auto, rows));
        }

        return cards;
    }

    /// <summary>Ordinal membership over the read-only collection - the manual set's identity rule is
    /// the same one the page state's HashSet was built with.</summary>
    private static bool ContainsOrdinal(IReadOnlyCollection<string> keys, string key)
    {
        if (keys == null) return false;
        foreach (string opened in keys)
            if (string.Equals(opened, key, StringComparison.Ordinal)) return true;
        return false;
    }
}
