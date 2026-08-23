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

    public static void Refresh(UniversalSqueakerSettings? settings = null)
    {
        settings ??= UniversalSqueakerMod.Settings;
        try
        {
            Dictionary<string, List<SqueakVoicePackDef>> groups = new(StringComparer.Ordinal);
            foreach (SqueakVoicePackDef pack in EnumerateAllPackDefs())
            {
                if (!SqueakVoicePackValidator.IsValid(pack)) continue;
                if (pack.scope != SqueakVoicePackScope.Race && pack.scope != SqueakVoicePackScope.Xenotype) continue;
                // Catalog admission is neutral: every pack's declared raceDefName is a valid routing domain.
                if (!pack.TryGetPackKey(out string key)) continue;
                if (LegacyVoicePackBridge.IsLegacy(pack))
                    SqueakLog.LegacyVoicePackAdmitted(key, pack.raceDefName);
                if (!groups.TryGetValue(key, out List<SqueakVoicePackDef>? entries)) { entries = new List<SqueakVoicePackDef>(); groups.Add(key, entries); }
                entries.Add(pack);
            }

            Dictionary<string, SqueakVoicePackDef> packsByKey = new(StringComparer.Ordinal);
            List<SqueakVoicePackDef> racePacks = new();
            foreach (KeyValuePair<string, List<SqueakVoicePackDef>> group in groups)
            {
                if (group.Value.Count != 1) { WarnDuplicatePackKey(group.Key, group.Value.Count); continue; }
                SqueakVoicePackDef pack = group.Value[0];
                packsByKey.Add(group.Key, pack);
                if (pack.scope == SqueakVoicePackScope.Race) racePacks.Add(pack);
            }

            // The Race domain is published before and independently from every optional DLC step.
            // Pack eligibility is strictly the declared, case-sensitive raceDefName/target strings.
            Dictionary<string, List<SqueakVoicePackDef>> xenotypePacks = new(StringComparer.Ordinal);
            foreach (SqueakVoicePackDef pack in packsByKey.Values)
            {
                if (pack.scope != SqueakVoicePackScope.Xenotype || string.IsNullOrWhiteSpace(pack.targetDefName)) continue;
                if (!xenotypePacks.TryGetValue(pack.targetDefName, out List<SqueakVoicePackDef>? target)) { target = new List<SqueakVoicePackDef>(); xenotypePacks.Add(pack.targetDefName, target); }
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
            bool discoveryAvailable = false;
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
                catch (Exception ex) { if (SqueakLog.ShouldEmitDev) SqueakLog.XenotypeDiscoveryFailed(ex); discoveryAvailable = false; }
            }
            Volatile.Write(ref current, new SqueakXenotypeCatalogSnapshot(discoveryAvailable, canonical, ambiguousCanonicalNames, harHints, officialHarHints, packsByKey, racePacks, xenotypePacks, raceDefNames));
        }
        catch (Exception ex)
        {
            SqueakLog.CatalogRefreshFailed(ex);
            Volatile.Write(ref current, SqueakXenotypeCatalogSnapshot.Empty);
        }
    }

    private static IEnumerable<SqueakVoicePackDef> EnumerateAllPackDefs()
    {
        foreach (SqueakVoicePackDef pack in DefDatabase<SqueakVoicePackDef>.AllDefs) yield return pack;
        foreach (SqueakVoicePackDef legacy in LegacyVoicePackSource.CollectLegacy()) yield return legacy;
    }

    private static void WarnDuplicatePackKey(string key, int count)
    {
        SqueakLog.PackRejected(key, count);
    }
}

public sealed class SqueakXenotypeCatalogSnapshot
{
    public static readonly SqueakXenotypeCatalogSnapshot Empty = new(false, new Dictionary<string, XenotypeDef>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, SqueakVoicePackDef>(StringComparer.Ordinal), new List<SqueakVoicePackDef>(), new Dictionary<string, List<SqueakVoicePackDef>>(StringComparer.Ordinal), new List<string>());
    public readonly bool DiscoveryAvailable;
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
    public readonly IReadOnlyDictionary<string, SqueakVoicePackDef> PackByKey;
    public readonly IReadOnlyList<SqueakVoicePackDef> RacePacks;
    public readonly IReadOnlyDictionary<string, IReadOnlyList<SqueakVoicePackDef>> XenotypePacksByDefName;
    internal SqueakXenotypeCatalogSnapshot(bool discoveryAvailable, Dictionary<string, XenotypeDef> xenotypes, HashSet<string> ambiguousCanonicalNames, HashSet<string> harHints, HashSet<string> officialHarHints, Dictionary<string, SqueakVoicePackDef> packs, List<SqueakVoicePackDef> racePacks, Dictionary<string, List<SqueakVoicePackDef>> xenotypePacks, IReadOnlyList<string> raceDefNames)
    {
        DiscoveryAvailable = discoveryAvailable;
        Dictionary<string, XenotypeDef> canonical = new(xenotypes, StringComparer.Ordinal);
        XenotypeByDefName = new ReadOnlyDictionary<string, XenotypeDef>(canonical);
        AmbiguousCanonicalDefNames = new ReadOnlyCollection<string>(ambiguousCanonicalNames.OrderBy(x => x, StringComparer.Ordinal).ToList());
        HarHintDefNames = new ReadOnlyCollection<string>(harHints.OrderBy(x => x, StringComparer.Ordinal).ToList());
        OfficialHarHintDefNames = new ReadOnlyCollection<string>(officialHarHints.OrderBy(x => x, StringComparer.Ordinal).ToList());
        Xenotypes = new ReadOnlyCollection<XenotypeDef>(canonical.Values.OrderBy(x => x.defName, StringComparer.Ordinal).ToList());
        PackByKey = new ReadOnlyDictionary<string, SqueakVoicePackDef>(new Dictionary<string, SqueakVoicePackDef>(packs, StringComparer.Ordinal));
        racePacks.Sort((a, b) => StringComparer.Ordinal.Compare(a.defName, b.defName));
        RacePacks = new ReadOnlyCollection<SqueakVoicePackDef>(new List<SqueakVoicePackDef>(racePacks));
        RaceDefNames = new ReadOnlyCollection<string>(raceDefNames == null ? new List<string>() : new List<string>(raceDefNames));
        Dictionary<string, IReadOnlyList<SqueakVoicePackDef>> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<SqueakVoicePackDef>> entry in xenotypePacks)
        {
            entry.Value.Sort((a, b) => StringComparer.Ordinal.Compare(a.defName, b.defName));
            copy.Add(entry.Key, new ReadOnlyCollection<SqueakVoicePackDef>(new List<SqueakVoicePackDef>(entry.Value)));
        }
        XenotypePacksByDefName = new ReadOnlyDictionary<string, IReadOnlyList<SqueakVoicePackDef>>(copy);
    }

    /// <summary>UI-facing assembled-only target union. Projects only assembled content (declared packs)
    /// plus explicit references (selections/presets keep orphan/dormant rows); HAR discovery is deferred
    /// and never projects rows.</summary>
    public IReadOnlyList<SqueakXenotypeTargetCandidate> GetTargetCandidates(IEnumerable<VoicePackSelectionRecord> selections, IEnumerable<XenotypePresetRecord> presets)
    {
        Dictionary<string, HashSet<string>> sources = new(StringComparer.Ordinal);
        void AddSource(string name, string source)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!sources.TryGetValue(name, out HashSet<string>? values)) { values = new HashSet<string>(StringComparer.Ordinal); sources.Add(name, values); }
            values.Add(source);
        }
        foreach (string name in XenotypePacksByDefName.Keys) AddSource(name, "declared_pack");
        foreach (VoicePackSelectionRecord selection in selections ?? Array.Empty<VoicePackSelectionRecord>())
            if (selection != null && selection.scope == SqueakVoicePackScope.Xenotype) AddSource(selection.xenotypeDefName, "selection");
        foreach (XenotypePresetRecord preset in presets ?? Array.Empty<XenotypePresetRecord>())
            if (preset != null) AddSource(preset.xenotypeDefName, "preset");
        if (SqueakLog.ShouldEmitDev)
        {
            foreach (KeyValuePair<string, HashSet<string>> entry in sources.OrderBy(x => x.Key, StringComparer.Ordinal))
                SqueakLog.XenotypeDiscoveryCandidate(entry.Key, string.Join("+", entry.Value.OrderBy(x => x, StringComparer.Ordinal)), true);
            // Generic HAR discovery is deferred; hints remain empty and are only logged for future instrumentation.
            foreach (string hint in HarHintDefNames.OrderBy(x => x, StringComparer.Ordinal))
                if (!sources.ContainsKey(hint)) SqueakLog.XenotypeDiscoveryCandidate(hint, "har_hint_filtered", false);
            foreach (string officialHint in OfficialHarHintDefNames.OrderBy(x => x, StringComparer.Ordinal))
                if (!sources.ContainsKey(officialHint)) SqueakLog.XenotypeDiscoveryCandidate(officialHint, "har_official_filtered", false);
        }
        return new ReadOnlyCollection<SqueakXenotypeTargetCandidate>(sources.Keys.OrderBy(x => x, StringComparer.Ordinal)
            .Select(name => new SqueakXenotypeTargetCandidate(name, XenotypeByDefName.TryGetValue(name, out XenotypeDef? canonical) ? canonical : null, HarHintDefNames.Contains(name), AmbiguousCanonicalDefNames.Contains(name), XenotypePacksByDefName.ContainsKey(name))).ToList());
    }

    /// <summary>Returns only packs declared for the exact selectable domain; never use PackByKey for domain eligibility.
    /// Race scope is filtered by the target race name so multiple races never share a domain pool.</summary>
    internal IReadOnlyList<SqueakVoicePackDef> GetVoicePackDomainPacks(SqueakVoicePackScope scope, string targetDefName)
    {
        if (scope == SqueakVoicePackScope.Race)
        {
            if (string.IsNullOrEmpty(targetDefName)) return RacePacks;
            return RacePacks.Where(pack => string.Equals(pack.raceDefName, targetDefName, StringComparison.Ordinal)).ToList();
        }
        return scope == SqueakVoicePackScope.Xenotype && XenotypePacksByDefName.TryGetValue(targetDefName ?? "", out IReadOnlyList<SqueakVoicePackDef>? packs)
            ? packs
            : Array.Empty<SqueakVoicePackDef>();
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
