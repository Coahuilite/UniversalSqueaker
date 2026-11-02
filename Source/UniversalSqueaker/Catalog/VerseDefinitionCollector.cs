using System;
using System.Collections.Generic;

using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// The thin ORIGINAL-INTERFACE collector of the definition directory (OVT-001): the ONLY code on
/// this axis that reads the game's def database, and it runs ONCE from Mod's
/// ExecuteWhenFinished entry (defs are loaded there; the window is not involved). It turns real
/// 1.6 members into the plain UsDefinitionSnapshot - never a second routing authority: the sound
/// admission set (SqueakXenotypeCatalog.RaceDefNames) and comp attachment are NOT touched here.
///
/// <para>
/// The living-pawn boundary uses REAL facts, not label strings: ThingDef.category == Pawn AND
/// thingClass assignable to Verse.Pawn AND race != null. The 1.7 reference dataset proves the
/// corpse generator copies `thingDef.race = pawnDef.race` onto its Item-category corpse defs
/// (ThingDefGenerator_Corpses), so "race != null" ALONE admits corpses - the exact user-reported
/// failure this class exists to close. Items carry category Item and a null race; both fall out.
/// </para>
/// <para>
/// Source and framework are separate facts: source reads Def.modContentPack's real PackageId
/// against the official package constants (Core / five DLCs); HAR identity walks the loaded def's
/// OWN runtime type chain to the AlienRace framework type (official inheritance
/// AlienRace.ThingDef_AlienRace : ThingDef), cached per Type. No HAR assembly is referenced, no
/// directory is scanned, no name is whitelisted - HAR absent simply answers false and every other
/// identity stays. A mechanoid from a HAR mod keeps source, framework AND life kind at once.
/// </para>
/// </summary>
public static class VerseDefinitionCollector
{
    /// <summary>The real AlienRace base type's full name - the ONE string this class knows about
    /// HAR, used only to recognise a loaded def's own inheritance chain (never a defName/label
    /// heuristic, never a package-id proxy).</summary>
    private const string HarBaseTypeName = "AlienRace.ThingDef_AlienRace";

    private static readonly Dictionary<Type, bool> HarTypeCache = new Dictionary<Type, bool>();

    public static UsDefinitionSnapshot Collect()
    {
        var races = new List<UsRaceIdentity>();
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def == null || string.IsNullOrEmpty(def.defName) || def.race == null) continue;
            if (def.category != ThingCategory.Pawn) continue;
            // The LIVING boundary uses the thingClass (the Pawn runtime type); the HAR framework
            // chain is read from the DEF object itself (CO review r1): AlienRace extends ThingDef,
            // thingClass is a Pawn type and could never reach it.
            Type thingType = def.thingClass;
            if (thingType == null || !typeof(Pawn).IsAssignableFrom(thingType)) continue;

            UsRaceSourceKind source = SourceOf(def.modContentPack, out string packageId, out string packageName);
            races.Add(new UsRaceIdentity(
                def.defName,
                def.LabelCap,
                source,
                packageId,
                packageName,
                IsHarType(def.GetType()),
                LifeKindOf(def.race), ReferenceEquals(def, ThingDefOf.Human)));
        }

        // Directory order is the STABLE IDENTITY order (defName ordinal); the player-facing tier
        // order is the UI's reduction (UsBoardBrowsePolicy), never this publisher's.
        races.Sort((a, b) => string.CompareOrdinal(a.DefName, b.DefName));

        var xenotypes = new List<UsXenotypeIdentity>();
        foreach (XenotypeDef def in DefDatabase<XenotypeDef>.AllDefs)
        {
            if (def == null || string.IsNullOrEmpty(def.defName)) continue;
            UsRaceSourceKind source = SourceOf(def.modContentPack, out string packageId, out string packageName);
            string label = string.IsNullOrEmpty(def.label) ? def.defName : def.label;
            xenotypes.Add(new UsXenotypeIdentity(def.defName, label, source, packageId, packageName));
        }

        xenotypes.Sort((a, b) => string.CompareOrdinal(a.DefName, b.DefName));
        return new UsDefinitionSnapshot(races, xenotypes);
    }

    /// <summary>Real package identity only: the official constants are compile-time strings on
    /// ModContentPack (1.6 reference verified), so no extra member is read beyond PackageId.</summary>
    private static UsRaceSourceKind SourceOf(ModContentPack pack, out string packageId, out string packageName)
    {
        packageId = pack?.PackageId ?? "";
        packageName = pack?.Name ?? "";
        if (pack == null) return UsRaceSourceKind.Unknown;
        if (pack.IsCoreMod || packageId == ModContentPack.CoreModPackageId) return UsRaceSourceKind.Core;
        if (packageId == ModContentPack.RoyaltyModPackageId
            || packageId == ModContentPack.IdeologyModPackageId
            || packageId == ModContentPack.BiotechModPackageId
            || packageId == ModContentPack.AnomalyModPackageId
            || packageId == ModContentPack.OdysseyModPackageId)
        {
            return UsRaceSourceKind.OfficialDlc;
        }

        return UsRaceSourceKind.ThirdParty;
    }

    /// <summary>Life kind from the REAL RaceProperties flags, explicit precedence, Other is a
    /// visible answer (never dropped). AnomalyEntity is the real 1.6 flag - not an inference.</summary>
    private static UsRaceLifeKind LifeKindOf(RaceProperties race)
    {
        if (race.IsAnomalyEntity) return UsRaceLifeKind.AnomalyEntity;
        if (race.IsMechanoid) return UsRaceLifeKind.Mechanoid;
        if (race.Animal) return UsRaceLifeKind.Animal;
        if (race.Humanlike) return UsRaceLifeKind.Humanlike;
        return UsRaceLifeKind.Other;
    }

    /// <summary>Type-chain recognition of the loaded framework, fed the DEF object's runtime type
    /// (AlienRace.ThingDef_AlienRace extends ThingDef), cached per Type. The cache
    /// is a pure function of the Type (the def graph is frozen after load), so a window reopen or a
    /// second Collect never re-walks inheritance.</summary>
    private static bool IsHarType(Type type)
    {
        lock (HarTypeCache)
        {
            if (HarTypeCache.TryGetValue(type, out bool known)) return known;
        }

        bool isHar = false;
        for (Type current = type; current != null && current != typeof(ThingDef); current = current.BaseType)
        {
            if (string.Equals(current.FullName, HarBaseTypeName, StringComparison.Ordinal))
            {
                isHar = true;
                break;
            }
        }

        lock (HarTypeCache)
        {
            HarTypeCache[type] = isHar;
        }

        return isHar;
    }

    /// <summary>Test seam for the isolated rawDef harness ONLY (production never calls it): drops
    /// the per-Type HAR cache between fixture scenarios.</summary>
    internal static void ResetHarCacheForTests()
    {
        lock (HarTypeCache)
        {
            HarTypeCache.Clear();
        }
    }
}
