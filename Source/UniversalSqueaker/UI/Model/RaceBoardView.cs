using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// The plain fact bundle the facts adapter hands to the board projection (UIR-001/002/003 +
/// CORRECTIONS-R1 topic 1, OVT-001 clean cutover). ONE race, everything already read: the
/// REQUIRED directory identity, supply packs, the status of the race domain and of every xenotype
/// domain under it, the final-fallback facts. DEFINITION DISCOVERY belongs to the collector
/// (<see cref="VerseDefinitionCollector"/>) published by <see cref="RaceDefinitionCatalog"/> once
/// defs are ready; the adapter here only MERGES that directory with the sound supply, the settings
/// statuses and the fallback answer, and it carries no Verse member. The projection
/// (<see cref="RaceBoardProjection"/>) is ordinary data logic over the facts - which is what lets
/// the full state matrix be tested with real plain inputs, no game, no harness fake.
/// </summary>
public sealed class UsRaceBoardFacts
{
    public UsRaceBoardFacts(
        string defName,
        string label,
        IReadOnlyList<UsBoardPackFact> packs,
        IReadOnlyList<UsRaceDomainFact> domains,
        UsRaceIdentity identity,
        bool hasFallbackTable = false,
        bool fallbackIsPlayerTable = false,
        int fallbackSoundCount = 0)
    {
        DefName = defName ?? "";
        Label = label ?? defName ?? "";
        Packs = packs ?? Array.Empty<UsBoardPackFact>();
        Domains = domains ?? Array.Empty<UsRaceDomainFact>();
        HasFallbackTable = hasFallbackTable;
        FallbackIsPlayerTable = fallbackIsPlayerTable;
        FallbackSoundCount = fallbackSoundCount;
        // OVT-001 (PM clean-cutover): the identity is a REQUIRED plain fact - the directory answer
        // the collector produced. Unknown source / Other life kind remain legitimate collector
        // answers; there is no compatibility synthesis and no second classifier behind this field.
        Identity = identity;
    }

    public readonly string DefName;
    public readonly string Label;

    /// <summary>Every catalog pack supplying this race, either scope, with its own display identity.</summary>
    public readonly IReadOnlyList<UsBoardPackFact> Packs;

    /// <summary>Status per supplying domain: the race domain plus each xenotype domain under this race.</summary>
    public readonly IReadOnlyList<UsRaceDomainFact> Domains;

    /// <summary>A final-fallback table resolves for this race (maintainer ∪ player, per the store).</summary>
    public readonly bool HasFallbackTable;

    /// <summary>The resolved table exists because a player made it (no maintainer source beneath it).
    /// It does NOT prove anything about comp attachment - admission is never inferred here.</summary>
    public readonly bool FallbackIsPlayerTable;

    /// <summary>How many of the closed action keys the table maps to a non-empty sound. 0 mapped is
    /// "configured, no sounds yet" - never "ready".</summary>
    public readonly int FallbackSoundCount;

    /// <summary>OVT-001: the REQUIRED directory identity (source / HAR framework / life kind) as
    /// the collector answered it.</summary>
    public readonly UsRaceIdentity Identity;
}

/// <summary>One supply pack as a plain fact: identity, display label, and the exact domain it serves.</summary>
public readonly struct UsBoardPackFact
{
    public UsBoardPackFact(
        SqueakVoicePackScope scope, string targetDefName, string packKey, string packLabel,
        string targetLabel, bool biotechActive)
    {
        Scope = scope;
        TargetDefName = targetDefName ?? "";
        PackKey = packKey ?? "";
        PackLabel = packLabel ?? packKey ?? "";
        TargetLabel = targetLabel ?? targetDefName ?? "";
        BiotechActive = biotechActive;
    }

    public readonly SqueakVoicePackScope Scope;
    public readonly string TargetDefName;
    public readonly string PackKey;
    public readonly string PackLabel;
    /// <summary>Display name of the xenotype target ("" for race scope).</summary>
    public readonly string TargetLabel;
    /// <summary>False without Biotech: the xeno domain is dormant-by-environment, so the board must
    /// not offer a xeno-scoped action (the adapter answers ModsConfig once per pass).</summary>
    public readonly bool BiotechActive;
}

/// <summary>The selection status of one domain, reduced to plain data by the adapter.</summary>
public readonly struct UsRaceDomainFact
{
    public UsRaceDomainFact(
        SqueakVoicePackScope scope, string targetDefName, SqueakVoicePackDomainState state,
        IReadOnlyList<string> enabledKeys)
    {
        Scope = scope;
        TargetDefName = targetDefName ?? "";
        State = state;
        EnabledKeys = enabledKeys ?? Array.Empty<string>();
    }

    public readonly SqueakVoicePackScope Scope;
    public readonly string TargetDefName;
    public readonly SqueakVoicePackDomainState State;
    public readonly IReadOnlyList<string> EnabledKeys;
}

/// <summary>
/// One loaded race's BOARD row: the read-only, state-first answer the overview card paints, derived
/// by <see cref="RaceBoardProjection"/> from the plain facts. No Verse type crosses into it; nothing
/// here is authority - every write exit addresses exactly the identities the Packs page owns.
/// Two axes are separate on the row: the SUPPLY/SELECTION axis (packs, counts, statuses, fallback)
/// and the US-USE axis (ModeUsesPacks: Fallback and Remix consult pack selections; Vanilla uses ONLY
/// the final fallback; Disabled bypasses the pool and keeps every selection). A row never promises
/// playback and never reads pawns, so "no current Pawn" cannot appear as "race missing".
/// </summary>
public readonly struct RaceBoardRow
{
    public RaceBoardRow(
        string raceDefName,
        string label,
        int racePackCount,
        int enabledCount,
        int orphanCount,
        SqueakVoicePackDomainState state,
        bool xenotypeSupplyOnly,
        int xenoPackCount,
        int xenoEnabledCount,
        int xenoOrphanCount,
        bool xenoTargetUnavailable,
        string firstXenoTarget,
        bool hasFinalFallback,
        bool fallbackIsPlayerTable,
        int fallbackSoundCount,
        bool modeUsesPacks,
        IReadOnlyList<string> supplyCardKeys,
        string quickEnableScope,
        string quickEnableTarget,
        string quickEnablePackKey,
        string quickEnablePackLabel,
        string quickEnableTargetLabel,
        UsRaceIdentity identity)
    {
        RaceDefName = raceDefName ?? "";
        Label = label ?? raceDefName ?? "";
        RacePackCount = racePackCount;
        EnabledCount = enabledCount;
        OrphanCount = orphanCount;
        State = state;
        XenotypeSupplyOnly = xenotypeSupplyOnly;
        XenoPackCount = xenoPackCount;
        XenoEnabledCount = xenoEnabledCount;
        XenoOrphanCount = xenoOrphanCount;
        XenoTargetUnavailable = xenoTargetUnavailable;
        FirstXenoTarget = firstXenoTarget ?? "";
        HasFinalFallback = hasFinalFallback;
        FallbackIsPlayerTable = fallbackIsPlayerTable;
        FallbackSoundCount = fallbackSoundCount;
        ModeUsesPacks = modeUsesPacks;
        SupplyCardKeys = supplyCardKeys ?? Array.Empty<string>();
        QuickEnableScope = quickEnableScope ?? "";
        QuickEnableTarget = quickEnableTarget ?? "";
        QuickEnablePackKey = quickEnablePackKey ?? "";
        QuickEnablePackLabel = quickEnablePackLabel ?? "";
        QuickEnableTargetLabel = quickEnableTargetLabel ?? "";
        Identity = identity;
    }

    /// <summary>Machine identity (ThingDef defName). Never translated.</summary>
    public readonly string RaceDefName;

    /// <summary>Display label from the loaded def, falling back to the defName.</summary>
    public readonly string Label;

    /// <summary>Packs that serve this race at RACE scope in the current catalog (supply exists).</summary>
    public readonly int RacePackCount;

    /// <summary>Recorded selections in the race domain (a count, never a promise of playback).</summary>
    public readonly int EnabledCount;

    /// <summary>SAVED-CHOICE-MISSING count in the race domain (recorded keys whose pack is gone).</summary>
    public readonly int OrphanCount;

    /// <summary>The race domain's four-state status exactly as the selection status answers it.</summary>
    public readonly SqueakVoicePackDomainState State;

    /// <summary>No race-scope pack but xenotype packs under the race exist (the accurate reason a race
    /// card has no race-domain action to offer).</summary>
    public readonly bool XenotypeSupplyOnly;

    /// <summary>Packs serving this race's xenotype targets.</summary>
    public readonly int XenoPackCount;

    /// <summary>Recorded selections across those xeno domains.</summary>
    public readonly int XenoEnabledCount;

    /// <summary>Saved-choice-missing count across those xeno domains (distinct from a target unload).</summary>
    public readonly int XenoOrphanCount;

    /// <summary>A xeno domain's target is not loaded (pack EXISTS, its target does not) - stated apart
    /// from a saved choice whose pack is gone.</summary>
    public readonly bool XenoTargetUnavailable;

    /// <summary>First xenotype target seen for this race in pack order ("" when none): the precise
    /// xeno domain the navigation intent selects for a xeno-only race.</summary>
    public readonly string FirstXenoTarget;

    /// <summary>A final fallback table resolves for this race.</summary>
    public readonly bool HasFinalFallback;

    /// <summary>The table exists because a player created it (never proof about comp admission).</summary>
    public readonly bool FallbackIsPlayerTable;

    /// <summary>Mapped non-empty sounds in the fallback table. 0 means "configured, no sounds yet".</summary>
    public readonly int FallbackSoundCount;

    /// <summary>The US-USE axis: true when the mode actually consults pack selections.</summary>
    public readonly bool ModeUsesPacks;

    /// <summary>Every pack key supplying this race (either scope, ordinal-sorted) - the card keys the
    /// navigation intent expands; the scroll identity composes <c>pack-card-row#&lt;key&gt;</c> from them.</summary>
    public readonly IReadOnlyList<string> SupplyCardKeys;

    /// <summary>The ONE unambiguous quick-enable target: machine scope token ("Race" | "Xenotype"),
    /// "" scope/key when navigation is the honest answer (multi-pack supply, already enabled, no
    /// supply, or Biotech off for the only xeno candidate).</summary>
    public readonly string QuickEnableScope;
    public readonly string QuickEnableTarget;
    public readonly string QuickEnablePackKey;
    /// <summary>Display identity shown NEXT TO the enable button (UIR-003): the exact pack the press
    /// turns on, plus the exact xenotype label when the scope is Xenotype.</summary>
    public readonly string QuickEnablePackLabel;
    public readonly string QuickEnableTargetLabel;

    /// <summary>OVT-001: the directory identity carried through from the facts (source / HAR /
    /// life kind) - the browse axes the group chips, the ordering and the source display read.</summary>
    public readonly UsRaceIdentity Identity;
}

/// <summary>
/// OVT-003: one loaded XenotypeDef as BOARD facts - the xenotype DIRECTORY axis. Identity and
/// source come from the published definition snapshot; supply comes from packs that REALLY declare
/// this target (their own raceDefName is the only race association the row ever shows - the game
/// data model has no XenotypeDef.race and none is guessed). A directory xenotype with no packs is
/// a first-class, navigable, honestly-empty row.
/// </summary>
public readonly struct UsXenotypeBoardFacts
{
    public UsXenotypeBoardFacts(
        UsXenotypeIdentity identity,
        int packCount,
        int enabledCount,
        string raceContextDefName,
        string quickEnablePackKey,
        string quickEnablePackLabel,
        string quickEnableRaceDefName)
    {
        Identity = identity;
        PackCount = packCount;
        EnabledCount = enabledCount;
        RaceContextDefName = raceContextDefName ?? "";
        QuickEnablePackKey = quickEnablePackKey ?? "";
        QuickEnablePackLabel = quickEnablePackLabel ?? "";
        QuickEnableRaceDefName = quickEnableRaceDefName ?? "";
    }

    public readonly UsXenotypeIdentity Identity;
    /// <summary>Packs across ALL races that declare this exact xenotype target.</summary>
    public readonly int PackCount;
    /// <summary>Recorded selections across those (race, target) domains - a count, never playback.</summary>
    public readonly int EnabledCount;
    /// <summary>The one race whose packs name this target when that race is unambiguous ("" when
    /// zero or several): the context the row may honestly name.</summary>
    public readonly string RaceContextDefName;
    /// <summary>Quick-enable identity ONLY when exactly one pack supplies and it is not yet
    /// enabled, with an unambiguous race context (the same exactness rule as race cards).</summary>
    public readonly string QuickEnablePackKey;
    public readonly string QuickEnablePackLabel;
    public readonly string QuickEnableRaceDefName;
}

/// <summary>The rendered xenotype directory row (pure).</summary>
public readonly struct XenotypeBoardRow
{
    public XenotypeBoardRow(string defName, string label, UsRaceSourceKind source, int packCount,
        int enabledCount, string raceContextDefName, string quickEnablePackKey, string quickEnablePackLabel,
        string quickEnableRaceDefName, string sourcePackageId = "", string sourcePackageName = "")
    {
        DefName = defName ?? "";
        Label = label ?? defName ?? "";
        Source = source;
        SourcePackageId = sourcePackageId ?? "";
        SourcePackageName = sourcePackageName ?? "";
        PackCount = packCount;
        EnabledCount = enabledCount;
        RaceContextDefName = raceContextDefName ?? "";
        QuickEnablePackKey = quickEnablePackKey ?? "";
        QuickEnablePackLabel = quickEnablePackLabel ?? "";
        QuickEnableRaceDefName = quickEnableRaceDefName ?? "";
    }

    public readonly string DefName;
    public readonly string Label;
    public readonly UsRaceSourceKind Source;
    // PM source-display addendum: the REAL package identity rides the row (shown, not only searched).
    public readonly string SourcePackageId;
    public readonly string SourcePackageName;
    public readonly int PackCount;
    public readonly int EnabledCount;
    public readonly string RaceContextDefName;
    public readonly string QuickEnablePackKey;
    public readonly string QuickEnablePackLabel;
    public readonly string QuickEnableRaceDefName;
}

/// <summary>
/// OVT-002: the ONE answer the overview board renders - the projected, grouped, filtered, searched
/// and stably ordered rows, the xenotype directory under the vanilla-humanlike host, and the
/// displayed/total counts. Built by RaceBoardProjection from plain facts + the published directory
/// snapshot; the widget only draws it. Read-only view state: no group, filter or search here has
/// ever touched a setting, a selection or the admission set.
/// </summary>
public sealed class UsRaceBoardView
{
    public UsRaceBoardView(
        IReadOnlyList<RaceBoardRow> rows,
        IReadOnlyList<XenotypeBoardRow> xenotypes,
        bool xenotypeDirectoryExpanded,
        int totalRaces,
        int totalXenotypes,
        UsBoardBrowsePolicy.BoardGroup group,
        UsBoardBrowsePolicy.BoardFilter filter,
        int summaryTotal = 0,
        int summaryWithPacks = 0,
        int summaryEnabled = 0,
        int xenotypeBandIndex = 0, int filteredCount = 0, int page = 0, int pages = 1)
    {
        XenotypeBandIndex = xenotypeBandIndex < 0 ? 0
            : (xenotypeBandIndex > (rows?.Count ?? 0) ? (rows?.Count ?? 0) : xenotypeBandIndex);
        SummaryTotal = summaryTotal;
        SummaryWithPacks = summaryWithPacks;
        SummaryEnabled = summaryEnabled;
        Rows = rows ?? Array.Empty<RaceBoardRow>();
        Xenotypes = xenotypes ?? Array.Empty<XenotypeBoardRow>();
        XenotypeDirectoryExpanded = xenotypeDirectoryExpanded;
        TotalRaces = totalRaces;
        TotalXenotypes = totalXenotypes;
        Group = group;
        Filter = filter;
        FilteredCount = filteredCount; Page = page; Pages = pages;
    }

    public readonly IReadOnlyList<RaceBoardRow> Rows;
    public readonly IReadOnlyList<XenotypeBoardRow> Xenotypes;
    public readonly bool XenotypeDirectoryExpanded;

    /// <summary>CO review r1 item 7: where the expanded xenotype band sits among the race rows -
    /// right after the humanlike tier (the "智人与异种随后" segment; before every creature when no
    /// humanlike is shown). Measure and Draw both walk rows[0..Index), band, rows[Index..). Never
    /// after the animals/mechs/entities of an All screen.</summary>
    public readonly int XenotypeBandIndex;
    public readonly int TotalRaces;
    public readonly int TotalXenotypes;
    public readonly int FilteredCount, Page, Pages;
    public readonly UsBoardBrowsePolicy.BoardGroup Group;
    public readonly UsBoardBrowsePolicy.BoardFilter Filter;

    /// <summary>Directory-wide summary (computed BEFORE group/filter/search - the totals line
    /// never lies about what is loaded): races, races with supply, races with a selection.</summary>
    public readonly int SummaryTotal;
    public readonly int SummaryWithPacks;
    public readonly int SummaryEnabled;

    /// <summary>Displayed race rows after group+filter+search (the "显示数" half of the count line).</summary>
    public int DisplayedRaces => Rows.Count;
    public int DisplayedXenotypes => Xenotypes.Count;

    public static readonly UsRaceBoardView Empty = new UsRaceBoardView(
        Array.Empty<RaceBoardRow>(), Array.Empty<XenotypeBoardRow>(), false, 0, 0,
        UsBoardBrowsePolicy.BoardGroup.PlayerRaces, UsBoardBrowsePolicy.BoardFilter.None);
}

/// <summary>OVT-001/003: what the facts seam answers - race rows AND the xenotype directory rows,
/// both plain. The projection consumes the bundle; no consumer re-reads the def database.</summary>
public sealed class UsRaceBoardFactsBundle
{
    public UsRaceBoardFactsBundle(
        IReadOnlyList<UsRaceBoardFacts> races, IReadOnlyList<UsXenotypeBoardFacts> xenotypes)
    {
        Races = races ?? Array.Empty<UsRaceBoardFacts>();
        Xenotypes = xenotypes ?? Array.Empty<UsXenotypeBoardFacts>();
    }

    public readonly IReadOnlyList<UsRaceBoardFacts> Races;
    public readonly IReadOnlyList<UsXenotypeBoardFacts> Xenotypes;
}

/* The seam interface IUsRaceBoardSource lives in LoadedRaceSource.cs (next to its only
   Verse-typed implementation), so this file stays pure plain data and compiles into the
   zero-game UiLogic harness for the projection matrix tests. */
