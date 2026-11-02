using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// The pure board projection and the pure navigation intent (CORRECTIONS-R1 topics 1/2). No Verse, no
/// Unity, no settings object, no catalog - everything here is ordinary data logic over the plain
/// fact bundles the thin adapter collects, which is what lets the full state matrix (race+xeno,
/// xeno-only, empty fallback table, saved-choice-missing, Disabled, Vanilla) be tested with real
/// inputs in the zero-game UiLogic harness.
/// </summary>
public static class RaceBoardProjection
{
    /// <summary>
    /// Row per race from plain facts. The mode is the only extra input (a plain enum): Fallback/Remix
    /// consult pack selections (the US-USE axis answers "packs + fallback"); Vanilla uses only the
    /// final fallback; Disabled bypasses the pool while every selection stays recorded. Orphan
    /// counting is membership over the domain's own supply keys, per domain - the two "missing"
    /// states stay distinct: a saved choice whose PACK is gone versus a pack whose TARGET unloaded.
    /// </summary>
    public static UsRaceBoardView Build(
        UsRaceBoardFactsBundle bundle,
        SqueakVoicePackMode mode,
        UsBoardBrowsePolicy.BoardGroup group = UsBoardBrowsePolicy.BoardGroup.PlayerRaces,
        UsBoardBrowsePolicy.BoardFilter filter = UsBoardBrowsePolicy.BoardFilter.None,
        string search = "",
        bool xenotypeDirectoryExpanded = false, string sourcePackageId = "", int requestedPage = 0, int pageSize = int.MaxValue)
    {
        var all = new List<RaceBoardRow>(bundle?.Races.Count ?? 0);
        IReadOnlyList<UsRaceBoardFacts> facts = bundle?.Races ?? Array.Empty<UsRaceBoardFacts>();
        if (bundle == null) return UsRaceBoardView.Empty;
        bool modeUsesPacks = mode == SqueakVoicePackMode.Fallback || mode == SqueakVoicePackMode.Remix;

        for (int i = 0; i < facts.Count; i++)
        {
            UsRaceBoardFacts race = facts[i];
            if (string.IsNullOrEmpty(race.DefName)) continue;

            var supplyCardKeys = new List<string>();
            var seenCard = new HashSet<string>(StringComparer.Ordinal);
            int racePacks = 0;
            int xenoPacks = 0;
            int xenoEnabled = 0;
            int xenoOrphan = 0;
            bool xenoTargetUnavailable = false;
            string firstXenoTarget = "";

            for (int p = 0; p < race.Packs.Count; p++)
            {
                UsBoardPackFact pack = race.Packs[p];
                if (string.IsNullOrEmpty(pack.PackKey)) continue;
                if (pack.Scope == SqueakVoicePackScope.Race) racePacks++;
                else
                {
                    xenoPacks++;
                    if (pack.TargetDefName.Length > 0 && firstXenoTarget.Length == 0) firstXenoTarget = pack.TargetDefName;
                }

                if (seenCard.Add(pack.PackKey)) supplyCardKeys.Add(pack.PackKey);
            }

            int enabled = 0;
            int orphan = 0;
            SqueakVoicePackDomainState state = SqueakVoicePackDomainState.Available;
            for (int d = 0; d < race.Domains.Count; d++)
            {
                UsRaceDomainFact domain = race.Domains[d];
                // Per-key membership: a recorded key counts as SAVED-CHOICE-MISSING exactly when no
                // supplying pack of THIS domain carries it (a stale key among real packs still reads
                // as missing; a domain with zero packs makes every key missing).
                HashSet<string> supplyKeys = DomainSupplyKeys(race.Packs, domain);
                int domainOrphan = 0;
                for (int e = 0; e < domain.EnabledKeys.Count; e++)
                {
                    if (!supplyKeys.Contains(domain.EnabledKeys[e])) domainOrphan++;
                }

                int domainEnabled = domain.EnabledKeys.Count - domainOrphan;
                if (domain.Scope == SqueakVoicePackScope.Race)
                {
                    enabled = domainEnabled;
                    orphan = domainOrphan;
                    state = domain.State;
                }
                else
                {
                    xenoEnabled += domainEnabled;
                    xenoOrphan += domainOrphan;
                    if (domain.State == SqueakVoicePackDomainState.TargetUnavailable) xenoTargetUnavailable = true;
                }
            }

            supplyCardKeys.Sort(StringComparer.Ordinal);

            // The ONE unambiguous quick-enable target: exactly one not-yet-enabled pack identity across
            // both scopes; a xeno candidate additionally needs Biotech (dormant-by-environment is never
            // a quick target). Anything wider navigates instead.
            string quickScope = "", quickTarget = "", quickKey = "", quickLabel = "", quickTargetLabel = "";
            int candidates = 0;
            for (int p = 0; p < race.Packs.Count; p++)
            {
                UsBoardPackFact pack = race.Packs[p];
                if (string.IsNullOrEmpty(pack.PackKey)) continue;
                if (pack.Scope == SqueakVoicePackScope.Xenotype)
                {
                    if (!pack.BiotechActive) continue;
                    if (DomainEnabledKeys(race.Domains, pack.Scope, pack.TargetDefName).Contains(pack.PackKey)) continue;
                }
                else if (DomainEnabledKeys(race.Domains, pack.Scope, "").Contains(pack.PackKey)) continue;

                candidates++;
                quickScope = pack.Scope == SqueakVoicePackScope.Xenotype ? "Xenotype" : "Race";
                quickTarget = pack.TargetDefName;
                quickKey = pack.PackKey;
                quickLabel = pack.PackLabel;
                quickTargetLabel = pack.TargetLabel;
            }

            if (candidates != 1)
            {
                quickScope = quickTarget = quickKey = quickLabel = quickTargetLabel = "";
            }

            all.Add(new RaceBoardRow(
                race.DefName,
                string.IsNullOrEmpty(race.Label) ? race.DefName : race.Label,
                racePacks,
                enabled,
                orphan,
                state,
                racePacks == 0 && xenoPacks > 0,
                xenoPacks,
                xenoEnabled,
                xenoOrphan,
                xenoTargetUnavailable,
                firstXenoTarget,
                race.HasFallbackTable,
                race.HasFallbackTable && race.FallbackIsPlayerTable,
                race.HasFallbackTable ? race.FallbackSoundCount : 0,
                modeUsesPacks,
                supplyCardKeys,
                quickScope, quickTarget, quickKey, quickLabel, quickTargetLabel,
                race.Identity));
        }

        // OVT-002: the browse reduction happens HERE, over the complete directory - a group, chip,
        // search or the sort can never remove a def from the directory itself, and the summary
        // counts are computed over ALL rows (the totals line never lies about what is loaded).
        (int totalAll, int withPacksAll, int enabledAll) = Summarize(all);
        var shown = new List<RaceBoardRow>();
        for (int i = 0; i < all.Count; i++)
        {
            UsRaceIdentity id = all[i].Identity;
            if (!UsBoardBrowsePolicy.InGroup(id, group)) continue;
            if (!UsBoardBrowsePolicy.InFilter(id, filter)) continue;
            if (!UsBoardBrowsePolicy.MatchesSearch(id, search)) continue;
            if (sourcePackageId.Length > 0 && id.SourcePackageId != sourcePackageId) continue;
            shown.Add(all[i]);
        }

        shown.Sort((left, right) => UsBoardBrowsePolicy.Compare(left.Identity, right.Identity));

        var xenoShown = new List<XenotypeBoardRow>();
        bool xenoBandVisible = xenotypeDirectoryExpanded
            && (group == UsBoardBrowsePolicy.BoardGroup.PlayerRaces || group == UsBoardBrowsePolicy.BoardGroup.Xenotypes || group == UsBoardBrowsePolicy.BoardGroup.All
                || group == UsBoardBrowsePolicy.BoardGroup.Human || group == UsBoardBrowsePolicy.BoardGroup.HarHumanlike)
            && filter == UsBoardBrowsePolicy.BoardFilter.None;
        IReadOnlyList<UsXenotypeBoardFacts> xenoFacts = bundle.Xenotypes;
        if (xenoBandVisible)
        {
            for (int i = 0; i < xenoFacts.Count; i++)
            {
                UsXenotypeBoardFacts x = xenoFacts[i];
                if (!UsBoardBrowsePolicy.MatchesXenotypeSearch(x.Identity, search ?? "")) continue;
                if (sourcePackageId.Length > 0 && x.Identity.SourcePackageId != sourcePackageId) continue;
                if (group == UsBoardBrowsePolicy.BoardGroup.Human || group == UsBoardBrowsePolicy.BoardGroup.HarHumanlike)
                {
                    bool parentMatches = false;
                    foreach (var race in facts)
                        if (UsBoardBrowsePolicy.InGroup(race.Identity, group))
                            foreach (var pack in race.Packs)
                                if (pack.Scope == SqueakVoicePackScope.Xenotype && pack.TargetDefName == x.Identity.DefName) parentMatches = true;
                    if (!parentMatches) continue;
                }

                xenoShown.Add(new XenotypeBoardRow(
                    x.Identity.DefName,
                    string.IsNullOrEmpty(x.Identity.Label) ? x.Identity.DefName : x.Identity.Label,
                    x.Identity.Source, x.PackCount, x.EnabledCount, x.RaceContextDefName,
                    x.QuickEnablePackKey, x.QuickEnablePackLabel, x.QuickEnableRaceDefName,
                    x.Identity.SourcePackageId, x.Identity.SourcePackageName));
            }

            xenoShown.Sort((left, right) =>
            {
                int byLabel = string.CompareOrdinal(left.Label, right.Label);
                return byLabel != 0 ? byLabel : string.CompareOrdinal(left.DefName, right.DefName);
            });
        }

        // CO review r1 item 7: the band rides the humanlike segment. Rows are tier-sorted, so the
        // index is simply the count of humanlike-tier rows (Tier <= 2); with none shown the band
        // still precedes every creature row (index 0).
        int bandIndex = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            if (UsBoardBrowsePolicy.Tier(shown[i].Identity) > 2) break;
            bandIndex = i + 1;
        }

        int filteredCount = shown.Count + xenoShown.Count;
        int pages = pageSize == int.MaxValue ? 1 : Math.Max(1, (filteredCount + pageSize - 1) / pageSize);
        int page = Math.Max(0, Math.Min(requestedPage, pages - 1));
        if (pageSize != int.MaxValue)
        {
            // One bounded page over the ordered race prefix, xenotype band, then remaining races.
            var pagedRaces = new List<RaceBoardRow>();
            var pagedXenos = new List<XenotypeBoardRow>();
            int start = page * pageSize, end = start + pageSize, index = 0, pagedBand = 0;
            for (int i = 0; i <= shown.Count; i++)
            {
                if (i == bandIndex)
                {
                    pagedBand = pagedRaces.Count;
                    foreach (XenotypeBoardRow x in xenoShown)
                    { if (index >= start && index < end) pagedXenos.Add(x); index++; }
                }
                if (i < shown.Count) { if (index >= start && index < end) pagedRaces.Add(shown[i]); index++; }
            }
            shown = pagedRaces; xenoShown = pagedXenos; bandIndex = pagedBand;
        }
        return new UsRaceBoardView(shown, xenoShown, xenoBandVisible, all.Count, xenoFacts.Count, group, filter,
            totalAll, withPacksAll, enabledAll, bandIndex, filteredCount, page, pages);
    }

    /// <summary>The board summary counts (plain numbers; the host composes the keyed sentence).</summary>
    public static (int Total, int WithPacks, int Enabled) Summarize(IReadOnlyList<RaceBoardRow> rows)
    {
        int withPacks = 0;
        int enabled = 0;
        if (rows == null) return (0, 0, 0);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].RacePackCount > 0 || rows[i].XenoPackCount > 0) withPacks++;
            if (rows[i].EnabledCount > 0 || rows[i].XenoEnabledCount > 0) enabled++;
        }

        return (rows.Count, withPacks, enabled);
    }

    /// <summary>
    /// The card's atomic navigation intent as pure data (UIR-002/003, CORRECTIONS-R1 topic 2):
    /// <list type="bullet">
    /// <item>ANY supply: keep the race condition (the context survives), clear the conditions that
    /// would MASK the target, select the precise domain - the race domain when race packs exist, else
    /// the first xeno domain (xeno-only arrives at its real target, not at an empty race domain) -
    /// expand every supplying card, and scroll to the primary header row through the FL-materialized
    /// identity <c>pack-card-row#&lt;packKey&gt;</c> (the grammar the Repeat composes and the engine's
    /// scroll-target resolution matches - no manual offsets, no copied scroll policy, no new API).</item>
    /// <item>NO supply: the race condition stays (a narrowed result naming this row's own race is a
    /// truthful context, not a lie about filters); the destination is the STATIC one - the final
    /// fallback editor section when a table resolves for this race, else "" (the narrowed empty with
    /// race context IS the accurate answer, and the caller must CLEAR any pending scroll target).</item>
    /// </list>
    /// Writes nothing persisted either way: no selection, no mode, no setting.
    /// </summary>
    public static UsRaceNavigationIntent DeriveIntent(RaceBoardRow row)
    {
        bool hasSupply = row.RacePackCount > 0 || row.XenoPackCount > 0;
        string domainScope = "Race";
        string domainTarget = "";
        if (hasSupply && row.RacePackCount == 0)
        {
            domainScope = "Xenotype";
            domainTarget = row.FirstXenoTarget;
        }

        string destination;
        if (hasSupply && row.SupplyCardKeys.Count > 0)
        {
            string? primary = null;
            for (int i = 0; i < row.SupplyCardKeys.Count; i++)
            {
                if (primary == null || string.CompareOrdinal(row.SupplyCardKeys[i], primary) < 0) primary = row.SupplyCardKeys[i];
            }

            destination = "pack-card-row#" + primary;
        }
        else if (row.HasFinalFallback)
        {
            destination = "packs-fallback";   // static destination: the table this race's audio falls back to
        }
        else
        {
            destination = "";                 // no supply, no table: the race-narrowed result region itself
        }

        return new UsRaceNavigationIntent(domainScope, domainTarget, row.SupplyCardKeys, destination);
    }

    /// <summary>The one exact quick-enable identity as machine values (scope, race, target, pack key).
    /// False when the row offers no unambiguous target - navigation is then the only honest action.</summary>
    public static bool TryQuickEnable(RaceBoardRow row, out SqueakVoicePackScope scope, out string target, out string packKey)
    {
        scope = SqueakVoicePackScope.Race;
        target = row.QuickEnableTarget ?? "";
        packKey = row.QuickEnablePackKey ?? "";
        if (packKey.Length == 0) return false;
        if (string.Equals(row.QuickEnableScope, "Xenotype", StringComparison.Ordinal)) scope = SqueakVoicePackScope.Xenotype;
        return true;
    }

    private static HashSet<string> DomainEnabledKeys(
        IReadOnlyList<UsRaceDomainFact> domains, SqueakVoicePackScope scope, string targetDefName)
    {
        for (int i = 0; i < domains.Count; i++)
        {
            if (domains[i].Scope != scope) continue;
            if (scope == SqueakVoicePackScope.Xenotype
                && !string.Equals(domains[i].TargetDefName, targetDefName, StringComparison.Ordinal)) continue;
            return new HashSet<string>(domains[i].EnabledKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Supply keys of one domain (for the per-key orphan membership test in Build).</summary>
    private static HashSet<string> DomainSupplyKeys(IReadOnlyList<UsBoardPackFact> packs, UsRaceDomainFact domain)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int p = 0; p < packs.Count; p++)
        {
            UsBoardPackFact pack = packs[p];
            if (pack.Scope != domain.Scope || string.IsNullOrEmpty(pack.PackKey)) continue;
            if (domain.Scope == SqueakVoicePackScope.Race
                || string.Equals(pack.TargetDefName, domain.TargetDefName, StringComparison.Ordinal))
            {
                keys.Add(pack.PackKey);
            }
        }

        return keys;
    }
}

/// <summary>The pure navigation intent the board's open exit emits (see RaceBoardProjection.DeriveIntent).</summary>
public readonly struct UsRaceNavigationIntent
{
    public UsRaceNavigationIntent(
        string domainScope, string domainTarget, IReadOnlyList<string> expandCardKeys, string scrollDestination)
    {
        DomainScope = domainScope ?? "Race";
        DomainTarget = domainTarget ?? "";
        ExpandCardKeys = expandCardKeys ?? Array.Empty<string>();
        ScrollDestination = scrollDestination ?? "";
    }

    /// <summary>Machine scope token for the domain the arrival selects ("Race" | "Xenotype").</summary>
    public readonly string DomainScope;
    public readonly string DomainTarget;
    public readonly IReadOnlyList<string> ExpandCardKeys;
    /// <summary>FL-materialized row identity ("pack-card-row#key"), the static section id
    /// ("packs-fallback"), or "" (arm nothing - the caller clears any old pending request).</summary>
    public readonly string ScrollDestination;
}
