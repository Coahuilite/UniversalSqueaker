using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>Atomically published VoicePack catalog. Reflective HAR discovery is deferred; canonical
/// xenotypes are resolved only from assembled pack targets against the loaded Biotech Def database.</summary>
public static class SqueakXenotypeCatalog
{
    private static SqueakXenotypeCatalogSnapshot current = SqueakXenotypeCatalogSnapshot.Empty;
    public static SqueakXenotypeCatalogSnapshot Current => Volatile.Read(ref current);

    public static void Refresh(GameSettings? settings = null)
    {
        settings ??= SqueakerMod.Settings;
        try
        {
            Dictionary<string, List<VoicePackInfo>> groups = new(StringComparer.Ordinal);
            foreach (VoicePackInfo pack in EnumerateAllPackDefs())
            {
                if (pack.scope != SqueakVoicePackScope.Race && pack.scope != SqueakVoicePackScope.Xenotype) continue;
                // Catalog admission is neutral: every pack's declared raceDefName is a valid routing domain.
                if (!pack.TryGetPackKey(out string key)) continue;
                if (!groups.TryGetValue(key, out List<VoicePackInfo>? entries)) { entries = new List<VoicePackInfo>(); groups.Add(key, entries); }
                entries.Add(pack);
            }

            Dictionary<string, VoicePackInfo> packsByKey = new(StringComparer.Ordinal);
            List<VoicePackInfo> racePacks = new();
            foreach (KeyValuePair<string, List<VoicePackInfo>> group in groups)
            {
                if (group.Value.Count != 1) { WarnDuplicatePackKey(group.Key, group.Value.Count); continue; }
                VoicePackInfo pack = group.Value[0];
                packsByKey.Add(group.Key, pack);
                if (pack.scope == SqueakVoicePackScope.Race) racePacks.Add(pack);
            }

            // The Race domain is published before and independently from every optional DLC step.
            // Pack eligibility is strictly the declared, case-sensitive raceDefName/target strings.
            Dictionary<string, List<VoicePackInfo>> xenotypePacks = new(StringComparer.Ordinal);
            foreach (VoicePackInfo pack in packsByKey.Values)
            {
                if (pack.scope != SqueakVoicePackScope.Xenotype || string.IsNullOrWhiteSpace(pack.targetDefName)) continue;
                if (!xenotypePacks.TryGetValue(pack.targetDefName, out List<VoicePackInfo>? target)) { target = new List<VoicePackInfo>(); xenotypePacks.Add(pack.targetDefName, target); }
                target.Add(pack);
            }

            // The Race list is the union of every admitted pack's declared race, deduplicated ordinally.
            // There is no built-in race and no product seed; empty source data yields an empty catalog.
            List<string> raceDefNames = packsByKey.Values
                .Select(pack => pack.raceDefName)
                .Where(race => !string.IsNullOrEmpty(race))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(race => race, StringComparer.Ordinal)
                .ToList();

            Dictionary<string, XenotypeDef> canonical = new(StringComparer.Ordinal);
            HashSet<string> ambiguousCanonicalNames = new(StringComparer.Ordinal);
            HashSet<string> harHints = new(StringComparer.Ordinal);
            HashSet<string> officialHarHints = new(StringComparer.Ordinal);
            if (ModsConfig.BiotechActive)
            {
                try
                {
                    // TODO(HAR): generic HAR reflection discovery is deferred to a later version.
                    // US 0.1.x is assembled-only: canonical xenotypes are exactly the targets declared
                    // by packs, with no HAR-specific projection or product-race special-casing.
                    HashSet<string> assembledXenotypes = new(StringComparer.Ordinal);
                    foreach (string name in xenotypePacks.Keys) assembledXenotypes.Add(name);
                    foreach (XenotypeDef xenotype in DefDatabase<XenotypeDef>.AllDefs)
                    {
                        if (string.IsNullOrEmpty(xenotype.defName) || !assembledXenotypes.Contains(xenotype.defName)) continue;
                        if (canonical.ContainsKey(xenotype.defName))
                        {
                            canonical.Remove(xenotype.defName);
                            ambiguousCanonicalNames.Add(xenotype.defName);
                        }
                        else if (!ambiguousCanonicalNames.Contains(xenotype.defName)) canonical.Add(xenotype.defName, xenotype);
                    }
                }
                catch (Exception ex)
                {
                    if (SqueakLog.ShouldEmitDev) SqueakLog.XenotypeDiscoveryFailed(ex);
                    // Fail-closed：装配异常时丢弃已填充的部分 canonical，避免发布半成品。
                    canonical.Clear();
                    ambiguousCanonicalNames.Clear();
                }
            }
            Volatile.Write(ref current, new SqueakXenotypeCatalogSnapshot(canonical, ambiguousCanonicalNames, harHints, officialHarHints, packsByKey, racePacks, xenotypePacks, raceDefNames));
        }
        catch (Exception ex)
        {
            SqueakLog.CatalogRefreshFailed(ex);
            Volatile.Write(ref current, SqueakXenotypeCatalogSnapshot.Empty);
        }
    }

    private static IEnumerable<VoicePackInfo> EnumerateAllPackDefs()
    {
        foreach (VoicePackInfo pack in VoicePackDirectory.Directory)
        {
            yield return pack;
        }
    }

    private static void WarnDuplicatePackKey(string key, int count)
    {
        SqueakLog.PackRejected(key, count);
    }
}

public sealed class SqueakXenotypeCatalogSnapshot
{
    public static readonly SqueakXenotypeCatalogSnapshot Empty = new(new Dictionary<string, XenotypeDef>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, VoicePackInfo>(StringComparer.Ordinal), new List<VoicePackInfo>(), new Dictionary<string, List<VoicePackInfo>>(StringComparer.Ordinal), new List<string>());
    public readonly IReadOnlyList<XenotypeDef> Xenotypes;
    public readonly IReadOnlyDictionary<string, XenotypeDef> XenotypeByDefName;
    /// <summary>Names with multiple loaded Def instances. Runtime must fail closed for these.</summary>
    public readonly IReadOnlyCollection<string> AmbiguousCanonicalDefNames;
    /// <summary>HAR discovery hints are reserved for a later generic HAR discovery pass.
    /// In the assembled-only US 0.1.x projection they are always empty and never projected as rows.</summary>
    public readonly IReadOnlyCollection<string> HarHintDefNames;
    /// <summary>Official HAR-only hints; reserved for the same later generic pass, never projected as rows.</summary>
    public readonly IReadOnlyCollection<string> OfficialHarHintDefNames;
    /// <summary>All race domains admitted by the current catalog, deduplicated ordinally.</summary>
    public readonly IReadOnlyList<string> RaceDefNames;
    public readonly IReadOnlyDictionary<string, VoicePackInfo> PackByKey;
    public readonly IReadOnlyList<VoicePackInfo> RacePacks;
    public readonly IReadOnlyDictionary<string, IReadOnlyList<VoicePackInfo>> XenotypePacksByDefName;
    internal SqueakXenotypeCatalogSnapshot(Dictionary<string, XenotypeDef> xenotypes, HashSet<string> ambiguousCanonicalNames, HashSet<string> harHints, HashSet<string> officialHarHints, Dictionary<string, VoicePackInfo> packs, List<VoicePackInfo> racePacks, Dictionary<string, List<VoicePackInfo>> xenotypePacks, IReadOnlyList<string> raceDefNames)
    {
        Dictionary<string, XenotypeDef> canonical = new(xenotypes, StringComparer.Ordinal);
        XenotypeByDefName = new ReadOnlyDictionary<string, XenotypeDef>(canonical);
        AmbiguousCanonicalDefNames = new ReadOnlyCollection<string>(ambiguousCanonicalNames.OrderBy(x => x, StringComparer.Ordinal).ToList());
        HarHintDefNames = new ReadOnlyCollection<string>(harHints.OrderBy(x => x, StringComparer.Ordinal).ToList());
        OfficialHarHintDefNames = new ReadOnlyCollection<string>(officialHarHints.OrderBy(x => x, StringComparer.Ordinal).ToList());
        Xenotypes = new ReadOnlyCollection<XenotypeDef>(canonical.Values.OrderBy(x => x.defName, StringComparer.Ordinal).ToList());
        PackByKey = new ReadOnlyDictionary<string, VoicePackInfo>(new Dictionary<string, VoicePackInfo>(packs, StringComparer.Ordinal));
        List<VoicePackInfo> racePacksCopy = new(racePacks);
        racePacksCopy.Sort((a, b) => StringComparer.Ordinal.Compare(a.defName, b.defName));
        RacePacks = new ReadOnlyCollection<VoicePackInfo>(racePacksCopy);
        RaceDefNames = new ReadOnlyCollection<string>(raceDefNames == null ? new List<string>() : new List<string>(raceDefNames));
        Dictionary<string, IReadOnlyList<VoicePackInfo>> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<VoicePackInfo>> entry in xenotypePacks)
        {
            List<VoicePackInfo> entries = new(entry.Value);
            entries.Sort((a, b) => StringComparer.Ordinal.Compare(a.defName, b.defName));
            copy.Add(entry.Key, new ReadOnlyCollection<VoicePackInfo>(entries));
        }
        XenotypePacksByDefName = new ReadOnlyDictionary<string, IReadOnlyList<VoicePackInfo>>(copy);
    }



    /// <summary>Returns only packs declared for the exact selectable domain; never use PackByKey for domain eligibility.
    /// Race scope is filtered by the target race name so multiple races never share a domain pool.</summary>
    internal IReadOnlyList<VoicePackInfo> GetVoicePackDomainPacks(SqueakVoicePackScope scope, string targetDefName)
    {
        if (scope == SqueakVoicePackScope.Race)
        {
            if (string.IsNullOrEmpty(targetDefName)) return RacePacks;
            return RacePacks.Where(pack => string.Equals(pack.raceDefName, targetDefName, StringComparison.Ordinal)).ToList();
        }
        return scope == SqueakVoicePackScope.Xenotype && XenotypePacksByDefName.TryGetValue(targetDefName ?? "", out IReadOnlyList<VoicePackInfo>? packs)
            ? packs
            : Array.Empty<VoicePackInfo>();
    }
}

/// <summary>Candidate DTO for settings UI: use DefName for selection/search identity, Canonical only for LabelCap/Icon.</summary>
public readonly struct SqueakXenotypeTargetCandidate
{
    public readonly string DefName;
    public readonly XenotypeDef? Canonical;
    public readonly bool IsHarHint;
    public readonly bool HasCanonicalConflict;
    public readonly bool HasDeclaredPacks;
    internal SqueakXenotypeTargetCandidate(string defName, XenotypeDef? canonical, bool isHarHint, bool hasCanonicalConflict, bool hasDeclaredPacks)
    { DefName = defName; Canonical = canonical; IsHarHint = isHarHint; HasCanonicalConflict = hasCanonicalConflict; HasDeclaredPacks = hasDeclaredPacks; }
}
