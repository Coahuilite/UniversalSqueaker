using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Single business entry for the VoicePacks page. It projects the read-only view state from
/// settings/catalog and executes all UI commands. Business commands land on the existing write bridge
/// (<see cref="UniversalSqueakerSettings.SetVoicePackSelection"/> /
/// <see cref="UniversalSqueakerSettings.CommitVoicePackMode"/>), so resolver rebuild and
/// QueuePersistence semantics remain untouched.
/// </summary>
public static class VoicePacksPageModel
{
    public static VoicePacksViewState BuildView(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog, VoicePacksPageState state)
    {
        if (settings == null) settings = UniversalSqueakerMod.Settings ?? new UniversalSqueakerSettings();
        if (catalog == null) catalog = SqueakXenotypeCatalog.Current;

        SqueakVoicePackMode mode = NormalizeMode(settings.voicePackMode);
        bool biotech = ModsConfig.BiotechActive;

        List<RaceLayerRowView> races = new();
        foreach (string race in catalog.RaceDefNames)
        {
            if (string.IsNullOrEmpty(race)) continue;
            IReadOnlyList<SqueakVoicePackDef> packs = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, race);
            SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(SqueakVoicePackScope.Race, race);
            races.Add(new RaceLayerRowView(
                race,
                ResolveRaceLabel(race),
                status.EnabledKeys?.Count ?? 0,
                packs.Count,
                status.State));
        }

        List<VoicePackDomainView> xenotypes = BuildXenotypeDomains(settings, catalog);

        VoicePackDomainView? selected = ResolveSelectedDomain(settings, catalog, state, races, xenotypes);
        string banner = BuildBannerText(catalog, races, xenotypes, mode, biotech);
        return new VoicePacksViewState(mode, settings.AllowEasterEggSounds, settings.distancePreset, settings.scaleCooldownWithTimeSpeed, settings.scaleFrequencyWithTalking, settings.scalePeriodicWithAudiblePopulation, settings.globalCooldownMultiplier, biotech, banner, races, xenotypes, selected);
    }

    public static void ExecuteAll(UniversalSqueakerSettings settings, IEnumerable<UiCommand> commands, VoicePacksPageState state)
    {
        if (commands == null) return;
        foreach (UiCommand command in commands) Execute(settings, command, state);
    }

    public static void Execute(UniversalSqueakerSettings settings, UiCommand command, VoicePacksPageState state)
    {
        if (settings == null) settings = UniversalSqueakerMod.Settings;
        if (settings == null) return;
        if (state == null) return;

        switch (command.Kind)
        {
            case UiCommandKind.SetMode:
                settings.CommitVoicePackMode(command.Mode);
                break;
            case UiCommandKind.SelectDomain:
                state.SelectedScope = command.Scope;
                state.SelectedTargetName = command.Scope == SqueakVoicePackScope.Xenotype
                    ? command.TargetDefName
                    : command.RaceDefName;
                break;
            case UiCommandKind.TogglePack:
                ExecuteTogglePack(settings, command);
                break;
            case UiCommandKind.ForgetUnavailable:
                ExecuteForgetUnavailable(settings, command);
                break;
            case UiCommandKind.ToggleEgg:
                settings.SetAllowEasterEggSounds(command.Flag);
                break;
            case UiCommandKind.SetDistancePreset:
                if (Enum.TryParse(command.Arg, true, out SqueakDistancePreset preset)) settings.SetDistancePreset(preset);
                break;
            case UiCommandKind.ToggleBasic:
                settings.SetBasicTuning(command.Arg, command.Flag);
                break;
        }
    }

    private static void ExecuteTogglePack(UniversalSqueakerSettings settings, UiCommand command)
    {
        if (string.IsNullOrEmpty(command.Arg) || string.IsNullOrEmpty(command.RaceDefName)) return;
        SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(
            command.Scope, command.RaceDefName, command.TargetDefName);
        List<string> next = new(status.EnabledKeys ?? Array.Empty<string>());
        if (command.Flag)
        {
            if (!next.Contains(command.Arg, StringComparer.Ordinal)) next.Add(command.Arg);
        }
        else
        {
            next.RemoveAll(key => string.Equals(key, command.Arg, StringComparison.Ordinal));
        }
        settings.SetVoicePackSelection(command.Scope, command.RaceDefName, command.TargetDefName, next);
    }

    private static void ExecuteForgetUnavailable(UniversalSqueakerSettings settings, UiCommand command)
    {
        if (string.IsNullOrEmpty(command.RaceDefName)) return;
        SqueakXenotypeCatalogSnapshot catalog = SqueakXenotypeCatalog.Current;
        SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(
            command.Scope, command.RaceDefName, command.TargetDefName);
        HashSet<string> domainKeys = new(StringComparer.Ordinal);
        IReadOnlyList<SqueakVoicePackDef> domainPacks = command.Scope == SqueakVoicePackScope.Race
            ? catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, command.RaceDefName)
            : catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Xenotype, command.TargetDefName)
                .Where(pack => string.Equals(pack.raceDefName, command.RaceDefName, StringComparison.Ordinal))
                .ToList();
        foreach (SqueakVoicePackDef pack in domainPacks)
            if (pack.TryGetPackKey(out string key)) domainKeys.Add(key);
        List<string> retained = (status.EnabledKeys ?? Array.Empty<string>())
            .Where(key => domainKeys.Contains(key))
            .ToList();
        settings.SetVoicePackSelection(command.Scope, command.RaceDefName, command.TargetDefName, retained);
    }

    private static List<VoicePackDomainView> BuildXenotypeDomains(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        List<XenotypeDomainKey> keys = CollectXenotypeDomains(settings, catalog);
        List<VoicePackDomainView> domains = new();
        foreach (XenotypeDomainKey key in keys)
        {
            IReadOnlyList<SqueakVoicePackDef> allTargetPacks = catalog.GetVoicePackDomainPacks(
                SqueakVoicePackScope.Xenotype, key.TargetDefName);
            List<SqueakVoicePackDef> packs = allTargetPacks
                .Where(pack => string.Equals(pack.raceDefName, key.RaceDefName, StringComparison.Ordinal))
                .ToList();
            SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(
                SqueakVoicePackScope.Xenotype, key.RaceDefName, key.TargetDefName);
            string displayName = ResolveXenotypeLabel(catalog, key.TargetDefName);
            int orphanCount = CountOrphanKeys(status.EnabledKeys, packs);
            List<VoicePackRowView> rows = packs
                .Select(pack => CreateVoicePackRow(pack, status.EnabledKeys))
                .ToList();
            bool targetUnavailable = ModsConfig.BiotechActive && !catalog.XenotypeByDefName.ContainsKey(key.TargetDefName);
            bool hasConflict = catalog.AmbiguousCanonicalDefNames.Contains(key.TargetDefName);
            domains.Add(new VoicePackDomainView(
                SqueakVoicePackScope.Xenotype,
                key.RaceDefName,
                key.TargetDefName,
                displayName,
                "Xenotype",
                status.State,
                !ModsConfig.BiotechActive,
                targetUnavailable,
                hasConflict,
                status.EnabledKeys?.Count ?? 0,
                packs.Count,
                orphanCount,
                status.EnabledKeys ?? Array.Empty<string>(),
                rows));
        }
        return domains;
    }

    private static List<XenotypeDomainKey> CollectXenotypeDomains(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        List<XenotypeDomainKey> result = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        void Add(string race, string target)
        {
            if (string.IsNullOrEmpty(race) || string.IsNullOrEmpty(target)) return;
            string identity = race + "\n" + target;
            if (!seen.Add(identity)) return;
            result.Add(new XenotypeDomainKey(race, target));
        }

        foreach (KeyValuePair<string, IReadOnlyList<SqueakVoicePackDef>> pair in catalog.XenotypePacksByDefName)
            foreach (SqueakVoicePackDef pack in pair.Value)
                if (pack != null) Add(pack.raceDefName, pair.Key);

        foreach (VoicePackSelectionRecord record in settings.voicePackSelections ?? new List<VoicePackSelectionRecord>())
            if (record != null && record.scope == SqueakVoicePackScope.Xenotype)
                Add(record.raceDefName, record.xenotypeDefName);

        result.Sort((left, right) =>
        {
            int byTarget = StringComparer.Ordinal.Compare(left.TargetDefName, right.TargetDefName);
            return byTarget != 0 ? byTarget : StringComparer.Ordinal.Compare(left.RaceDefName, right.RaceDefName);
        });
        return result;
    }

    private static VoicePackDomainView? ResolveSelectedDomain(
        UniversalSqueakerSettings settings,
        SqueakXenotypeCatalogSnapshot catalog,
        VoicePacksPageState state,
        IReadOnlyList<RaceLayerRowView> races,
        IReadOnlyList<VoicePackDomainView> xenotypes)
    {
        if (state.SelectedScope == SqueakVoicePackScope.Xenotype)
        {
            VoicePackDomainView? match = null;
            foreach (VoicePackDomainView domain in xenotypes)
            {
                if (string.Equals(domain.TargetDefName, state.SelectedTargetName, StringComparison.Ordinal))
                {
                    match = domain;
                    break;
                }
            }
            if (match == null && xenotypes.Count > 0) match = xenotypes[0];
            if (match != null) return match;
        }

        if (state.SelectedScope == SqueakVoicePackScope.Race || races.Count > 0)
        {
            RaceLayerRowView race = races.FirstOrDefault(row =>
                string.Equals(row.RaceDefName, state.SelectedTargetName, StringComparison.Ordinal));
            if (race.RaceDefName == null) race = races.FirstOrDefault();
            if (race.RaceDefName != null) return BuildRaceDomain(settings, catalog, race.RaceDefName);
        }

        return xenotypes.Count > 0 ? xenotypes[0] : (VoicePackDomainView?)null;
    }

    private static VoicePackDomainView BuildRaceDomain(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog, string raceDefName)
    {
        IReadOnlyList<SqueakVoicePackDef> packs = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, raceDefName);
        SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(SqueakVoicePackScope.Race, raceDefName);
        List<VoicePackRowView> rows = packs
            .Select(pack => CreateVoicePackRow(pack, status.EnabledKeys))
            .ToList();
        return new VoicePackDomainView(
            SqueakVoicePackScope.Race,
            raceDefName,
            "",
            ResolveRaceLabel(raceDefName),
            "Race",
            status.State,
            false,
            false,
            false,
            status.EnabledKeys?.Count ?? 0,
            packs.Count,
            CountOrphanKeys(status.EnabledKeys, packs),
            status.EnabledKeys ?? Array.Empty<string>(),
            rows);
    }

    private static VoicePackRowView CreateVoicePackRow(SqueakVoicePackDef pack, IReadOnlyList<string> enabledKeys)
    {
        string key = pack.TryGetPackKey(out string packKey) ? packKey : pack.defName;
        string label = string.IsNullOrEmpty(pack.LabelCap) ? pack.defName : pack.LabelCap;
        string modName = pack.modContentPack?.Name ?? pack.modContentPack?.PackageId ?? "—";
        string author = pack.modContentPack?.ModMetaData?.AuthorsString ?? "";
        if (string.IsNullOrEmpty(author)) author = modName;
        int playable = CountPlayableActions(pack);
        string coverage = "Actions " + playable + "/" + SqueakActionDefinitions.Count;
        bool isLegacy = LegacyVoicePackBridge.IsLegacy(pack);
        string searchText = label + "\n" + pack.defName + "\n" + modName + "\n" + author + "\n" + key + (isLegacy ? "\nlegacy sr" : "");
        bool selected = enabledKeys != null && enabledKeys.Contains(key);
        return new VoicePackRowView(key, label, modName, author, pack.defName, coverage, searchText, selected, isLegacy);
    }

    private static int CountOrphanKeys(IReadOnlyList<string>? enabledKeys, IReadOnlyList<SqueakVoicePackDef> packs)
    {
        if (enabledKeys == null || enabledKeys.Count == 0) return 0;
        HashSet<string> domainKeys = new(StringComparer.Ordinal);
        foreach (SqueakVoicePackDef pack in packs)
            if (pack.TryGetPackKey(out string key)) domainKeys.Add(key);
        int orphan = 0;
        foreach (string key in enabledKeys)
            if (!string.IsNullOrEmpty(key) && !domainKeys.Contains(key)) orphan++;
        return orphan;
    }

    private static string BuildBannerText(
        SqueakXenotypeCatalogSnapshot catalog,
        IReadOnlyList<RaceLayerRowView> races,
        IReadOnlyList<VoicePackDomainView> xenotypes,
        SqueakVoicePackMode mode,
        bool biotech)
    {
        List<string> messages = new();
        if (races.Count == 0 && xenotypes.Count == 0)
            messages.Add("No VoicePack domains are currently installed. Add a VoicePack that declares a raceDefName to configure audio.");
        int legacyCount = catalog.PackByKey.Values.Count(LegacyVoicePackBridge.IsLegacy);
        if (legacyCount > 0)
            messages.Add(legacyCount + " legacy Squeaky Ratkin VoicePack(s) are loaded through the compatibility bridge and marked as old SR content.");
        if (!biotech && xenotypes.Count > 0)
            messages.Add("Biotech is not active. Xenotype VoicePack selections are dormant and will not route to pawns.");
        if (mode == SqueakVoicePackMode.Vanilla)
            messages.Add("VoicePack mode is Vanilla. Enabled packs are retained, but VoicePack audio is not routed.");
        return string.Join("\n", messages);
    }

    private static string ResolveRaceLabel(string raceDefName)
    {
        ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
        return def != null && !string.IsNullOrEmpty(def.LabelCap) ? def.LabelCap : raceDefName;
    }

    private static string ResolveXenotypeLabel(SqueakXenotypeCatalogSnapshot catalog, string targetDefName)
    {
        if (catalog.XenotypeByDefName.TryGetValue(targetDefName, out XenotypeDef? def) && def != null)
            return string.IsNullOrEmpty(def.LabelCap) ? targetDefName : def.LabelCap;
        return targetDefName;
    }

    private static int CountPlayableActions(SqueakVoicePackDef pack)
    {
        if (pack.actions == null) return 0;
        int count = 0;
        foreach (SqueakVoicePackAction action in pack.actions)
        {
            if (action == null || action.sounds == null) continue;
            foreach (SoundDef sound in action.sounds)
            {
                if (sound != null) { count++; break; }
            }
        }
        return count;
    }

    private static SqueakVoicePackMode NormalizeMode(SqueakVoicePackMode mode)
    {
        return mode == SqueakVoicePackMode.Fallback || mode == SqueakVoicePackMode.Remix || mode == SqueakVoicePackMode.Disabled
            ? mode
            : SqueakVoicePackMode.Vanilla;
    }

    private readonly struct XenotypeDomainKey
    {
        public readonly string RaceDefName;
        public readonly string TargetDefName;

        public XenotypeDomainKey(string raceDefName, string targetDefName)
        {
            RaceDefName = raceDefName ?? "";
            TargetDefName = targetDefName ?? "";
        }
    }
}
