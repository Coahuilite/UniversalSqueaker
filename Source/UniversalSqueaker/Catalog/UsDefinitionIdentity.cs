using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UniversalSqueaker;

/// <summary>
/// OVT-001 (WO-US-OVERVIEW-THEMES-20261015): the PLAIN identity vocabulary of the definition
/// directory. Ordinary identity, source and basic-classification facts ONLY - no Verse, no Unity,
/// no RimWorld, no HAR type - so they compile into the zero-game purity set (KernelTests links
/// Pure/*.cs) and every consumer (the Verse collector, the catalog publisher, the UI projection,
/// the harness fixtures) speaks the SAME data. Per the PM ownership decision the PLAYER-FACING
/// browse policy (groups, chips, tier order, search, source display words) is NOT here - it lives
/// with the UI projection (UI/Model/UsBoardBrowsePolicy.cs). The catalog answers "what did the
/// game load"; it is NOT the sound-pack admission set (SqueakXenotypeCatalog.RaceDefNames stays
/// exactly what it was) and nothing here mounts a comp or grants sound.
/// </summary>
public enum UsRaceSourceKind
{
    /// <summary>No owning content pack could be read (defensive; a loaded def normally has one).</summary>
    Unknown = 0,
    /// <summary>The official Core package (ModContentPack.CoreModPackageId).</summary>
    Core = 1,
    /// <summary>An official DLC package (Royalty/Ideology/Biotech/Anomaly/Odyssey).</summary>
    OfficialDlc = 2,
    /// <summary>Any other loaded package - a third-party mod, HAR-hosted or not.</summary>
    ThirdParty = 3,
}

/// <summary>
/// The vanilla life-kind answer, read from the REAL RaceProperties flags by the collector.
/// ONE value per def (the flags are mutually exclusive in practice; the precedence below is
/// explicit and stable, and a def carrying none of them is Other, never silently dropped).
/// Source (who shipped it), framework (HAR or not) and life kind are SEPARATE axes: a
/// mechanoid shipped by a HAR mod keeps all three facts (OVT-002 "机械HAR不丢身份").
/// </summary>
public enum UsRaceLifeKind
{
    Other = 0,
    Humanlike = 1,
    Animal = 2,
    Mechanoid = 3,
    /// <summary>RaceProperties.IsAnomalyEntity - the real 1.6 Anomaly flag, never an inference
    /// from "not animal, not humanlike, hostile".</summary>
    AnomalyEntity = 4,
}

/// <summary>One loaded living-pawn ThingDef as a plain browsing identity (OVT-001). Source
/// package ID AND the real package NAME are carried so the UI can show and search the actual
/// mod identity - every DLC or every third-party mod never collapses into one enum field.</summary>
public readonly struct UsRaceIdentity
{
    public UsRaceIdentity(
        string defName,
        string label,
        UsRaceSourceKind source,
        string sourcePackageId,
        string sourcePackageName,
        bool isHar,
        UsRaceLifeKind lifeKind)
    {
        DefName = defName ?? "";
        Label = label ?? "";
        Source = source;
        SourcePackageId = sourcePackageId ?? "";
        SourcePackageName = sourcePackageName ?? "";
        IsHar = isHar;
        LifeKind = lifeKind;
        IsHuman = false;
    }
    public UsRaceIdentity(string defName, string label, UsRaceSourceKind source, string sourcePackageId,
        string sourcePackageName, bool isHar, UsRaceLifeKind lifeKind, bool isHuman)
        : this(defName, label, source, sourcePackageId, sourcePackageName, isHar, lifeKind) => IsHuman = isHuman;

    public readonly string DefName;
    public readonly string Label;
    public readonly UsRaceSourceKind Source;
    public readonly string SourcePackageId;
    public readonly string SourcePackageName;

    /// <summary>True only when the loaded def's own runtime TYPE chain passes through the real
    /// AlienRace framework type (collector-verified; never a name whitelist, never a package-id
    /// proxy - source and framework are different facts). A cached Type answer, not per-GUI work.</summary>
    public readonly bool IsHar;
    public readonly bool IsHuman;

    public readonly UsRaceLifeKind LifeKind;

    public bool IsHumanlike => LifeKind == UsRaceLifeKind.Humanlike;
    public bool IsAnimal => LifeKind == UsRaceLifeKind.Animal;
    public bool IsMechanoid => LifeKind == UsRaceLifeKind.Mechanoid;
    public bool IsAnomalyEntity => LifeKind == UsRaceLifeKind.AnomalyEntity;
    /// <summary>Vanilla = shipped by Core or an official DLC, whatever the framework says.</summary>
    public bool IsVanilla => Source == UsRaceSourceKind.Core || Source == UsRaceSourceKind.OfficialDlc;
}

/// <summary>
/// One loaded XenotypeDef as a plain identity (OVT-003): the xenotype dimension is its OWN target
/// axis. There is no XenotypeDef.race in the game data model, so NO owning race is claimed here -
/// any race association may only come from a real pack declaration, which lives on the sound side.
/// The real source package (ID + NAME) is carried like the race identity: a DLC xenotype and a
/// third-party mod xenotype stay distinguishable.
/// </summary>
public readonly struct UsXenotypeIdentity
{
    public UsXenotypeIdentity(
        string defName, string label, UsRaceSourceKind source, string sourcePackageId, string sourcePackageName)
    {
        DefName = defName ?? "";
        Label = label ?? "";
        Source = source;
        SourcePackageId = sourcePackageId ?? "";
        SourcePackageName = sourcePackageName ?? "";
    }

    public readonly string DefName;
    public readonly string Label;
    public readonly UsRaceSourceKind Source;
    public readonly string SourcePackageId;
    public readonly string SourcePackageName;
}

/// <summary>
/// The immutable published snapshot (Catalog/RaceDefinitionCatalog holds exactly one). Built once
/// after defs are ready (Mod's ExecuteWhenFinished entry), swapped atomically, reused by every
/// window open - never rebuilt per GUI pass. IMMUTABILITY IS OWNERSHIP, not a comment (CO review
/// r1): the constructor COPIES the caller's lists and hands out ReadOnly wrappers, so mutating
/// the collector's working lists afterwards cannot change a published directory. Races arrive
/// defName-ordinal-sorted (stable identity tie-break for same-label defs).
/// </summary>
public sealed class UsDefinitionSnapshot
{
    public static readonly UsDefinitionSnapshot Empty = new UsDefinitionSnapshot(
        Array.Empty<UsRaceIdentity>(), Array.Empty<UsXenotypeIdentity>());

    public UsDefinitionSnapshot(
        IReadOnlyList<UsRaceIdentity> races, IReadOnlyList<UsXenotypeIdentity> xenotypes)
    {
        Races = ReadOnlyCopy(races);
        Xenotypes = ReadOnlyCopy(xenotypes);
    }

    public IReadOnlyList<UsRaceIdentity> Races { get; }
    public IReadOnlyList<UsXenotypeIdentity> Xenotypes { get; }

    private static IReadOnlyList<T> ReadOnlyCopy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0) return Array.Empty<T>();
        var copy = new T[source.Count];
        for (int i = 0; i < source.Count; i++) copy[i] = source[i];
        return new ReadOnlyCollection<T>(copy);
    }
}
