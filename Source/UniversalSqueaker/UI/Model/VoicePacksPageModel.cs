using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
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
        List<FilterOptionView> raceFilterOptions = BuildRaceFilterOptions(races);
        List<FilterOptionView> xenotypeFilterOptions = BuildXenotypeFilterOptions(xenotypes, state.RaceFilter);

        List<RaceLayerRowView> filteredRaces = races
            .Where(race => VoicePacksFilters.DomainMatches(
                false,
                false,
                false,
                race.State == SqueakVoicePackDomainState.Orphan,
                race.EnabledCount > 0,
                in state.DomainFilter)
                && VoicePacksFilters.RaceFilterMatches(state.RaceFilter, race.RaceDefName))
            .ToList();
        List<VoicePackDomainView> filteredXenotypes = xenotypes
            .Where(domain => VoicePacksFilters.DomainMatches(
                domain.HasCanonicalConflict,
                domain.IsDormant,
                domain.IsTargetUnavailable,
                domain.OrphanCount > 0,
                domain.EnabledCount > 0,
                in state.DomainFilter)
                && VoicePacksFilters.XenotypeFilterMatches(
                    state.RaceFilter,
                    state.XenotypeFilter,
                    domain.RaceDefName,
                    domain.TargetDefName))
            .ToList();

        IReadOnlyList<string> authors = CollectAuthors(settings, catalog);

        VoicePackDomainView? selected = ResolveSelectedDomain(settings, catalog, state, filteredRaces, filteredXenotypes);
        if (selected != null)
        {
            selected = FilterDomainPacks(selected.Value, in state.PackFilter);
        }

        string banner = BuildBannerText(filteredRaces, filteredXenotypes, mode, biotech);
        // S5 调音编辑器：当前层 + 层域归一（未选中时取首个选项），再投影分层 scope 与心情行。
        string tuningRace = state.TuningRaceDefName;
        string tuningXeno = state.TuningXenotypeDefName;
        IReadOnlyList<TuningDomainOptionView> tuningDomains = BuildTuningDomains(settings, catalog, state.TuningLayer, ref tuningRace, ref tuningXeno);
        state.TuningRaceDefName = tuningRace;
        state.TuningXenotypeDefName = tuningXeno;
        IReadOnlyList<ActionScopeRowView> actionScopes = BuildActionScopes(settings, state.TuningLayer, tuningRace, tuningXeno);
        IReadOnlyList<MoodTuningRowView> moodTuningRows = BuildMoodTuningRows(settings, state.TuningLayer, tuningRace, tuningXeno);
        IReadOnlyList<BaselinePresetView> baselinePresets = BuildBaselinePresets(state);
        string buildIdentity = UniversalSqueakerMod.Instance != null ? UniversalSqueakerMod.BuildIdentity() : "unknown";
        string saveStatus = UniversalSqueakerMod.Instance?.SaveState.ToString() ?? "Unknown";
        bool isDirty = UniversalSqueakerMod.Instance?.IsSettingsDirty ?? false;
        return new VoicePacksViewState(mode, settings.AllowEasterEggSounds, settings.distancePreset, settings.scaleCooldownWithTimeSpeed, settings.scaleFrequencyWithTalking, settings.scalePeriodicWithAudiblePopulation, settings.showCameraIndicator, settings.globalCooldownMultiplier, settings.globalVolumeFactor, settings.distanceRange.min, settings.distanceRange.max, biotech, banner, filteredRaces, filteredXenotypes, selected, actionScopes, state.TuningLayer, tuningRace, tuningXeno, tuningDomains, moodTuningRows, baselinePresets, buildIdentity, saveStatus, isDirty, authors, state.RaceFilter, state.XenotypeFilter, raceFilterOptions, xenotypeFilterOptions);
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
                state.SelectedRaceDefName = command.RaceDefName ?? "";
                state.SelectedTargetName = command.Scope == SqueakVoicePackScope.Xenotype
                    ? command.TargetDefName ?? ""
                    : "";
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
            case UiCommandKind.SetGlobalVolume:
                if (float.TryParse(command.Arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float globalVolume))
                    settings.SetGlobalVolume(globalVolume);
                break;
            case UiCommandKind.SetDistanceRange:
                ExecuteSetDistanceRange(settings, command);
                break;
            case UiCommandKind.ToggleBasic:
                settings.SetBasicTuning(command.Arg, command.Flag);
                break;
            case UiCommandKind.SetActionTuningScope:
                ExecuteSetActionTuningScope(settings, command, state);
                break;
            case UiCommandKind.SetTuningLayer:
                if (int.TryParse(command.Arg, out int tuningLayer) && tuningLayer >= 0 && tuningLayer <= 2)
                    state.TuningLayer = tuningLayer;
                break;
            case UiCommandKind.SetTuningDomain:
                if (!string.IsNullOrEmpty(command.RaceDefName))
                {
                    state.TuningRaceDefName = command.RaceDefName;
                    state.TuningXenotypeDefName = command.TargetDefName ?? "";
                }
                break;
            case UiCommandKind.SetMoodTuning:
                ExecuteSetMoodTuning(settings, command, state);
                break;
            case UiCommandKind.ToggleBaselinePreset:
                ToggleBaselinePreset(state, command.Arg);
                break;
            case UiCommandKind.ToggleBaselineRace:
                ToggleBaselineRace(state, command.Arg, command.RaceDefName, command.Flag);
                break;
            case UiCommandKind.ToggleBaselineXenotype:
                ToggleBaselineXenotype(state, command.Arg, command.RaceDefName, command.TargetDefName, command.Flag);
                break;
            case UiCommandKind.ImportBaselinePreset:
                ImportBaselinePreset(settings, command.Arg, state);
                break;
            case UiCommandKind.SetActiveTab:
                ExecuteSetActiveTab(state, command.Arg);
                break;
            case UiCommandKind.ScrollToSection:
                ExecuteScrollToSection(state, command.Arg);
                break;
            case UiCommandKind.SetDomainFilter:
                ExecuteSetDomainFilter(state, command);
                break;
            case UiCommandKind.SetPackFilter:
                ExecuteSetPackFilter(state, command);
                break;
            case UiCommandKind.SetRaceFilter:
                ExecuteSetRaceFilter(settings, state, command.Arg ?? "");
                break;
            case UiCommandKind.SetXenotypeFilter:
                state.XenotypeFilter = command.Arg ?? "";
                break;
        }
    }

    private static void ExecuteSetDomainFilter(VoicePacksPageState state, UiCommand command)
    {
        UiDomainFilter filter = state.DomainFilter;
        if (string.Equals(command.Arg, "EnabledOnly", StringComparison.OrdinalIgnoreCase))
        {
            filter = new UiDomainFilter(command.Flag, filter.ConflictOnly, filter.OrphanOnly);
        }
        else if (string.Equals(command.Arg, "ConflictOnly", StringComparison.OrdinalIgnoreCase))
        {
            filter = new UiDomainFilter(filter.EnabledOnly, command.Flag, filter.OrphanOnly);
        }
        else if (string.Equals(command.Arg, "OrphanOnly", StringComparison.OrdinalIgnoreCase))
        {
            filter = new UiDomainFilter(filter.EnabledOnly, filter.ConflictOnly, command.Flag);
        }
        else
        {
            return;
        }

        state.DomainFilter = filter;
    }

    private static void ExecuteSetPackFilter(VoicePacksPageState state, UiCommand command)
    {
        UiPackFilter filter = state.PackFilter;
        if (command.Arg.StartsWith("Author|", StringComparison.OrdinalIgnoreCase))
        {
            string author = command.Arg.Substring("Author|".Length);
            filter = new UiPackFilter(author);
        }
        else
        {
            return;
        }

        state.PackFilter = filter;
    }

    private static void ExecuteSetRaceFilter(UniversalSqueakerSettings settings, VoicePacksPageState state, string raceDefName)
    {
        state.RaceFilter = raceDefName ?? "";
        if (string.IsNullOrEmpty(state.RaceFilter) || string.IsNullOrEmpty(state.XenotypeFilter)) return;
        if (XenotypeFilterExistsForRace(settings, state.XenotypeFilter, state.RaceFilter)) return;
        state.XenotypeFilter = "";
    }

    private static bool XenotypeFilterExistsForRace(UniversalSqueakerSettings settings, string xenotypeDefName, string raceDefName)
    {
        SqueakXenotypeCatalogSnapshot catalog = SqueakXenotypeCatalog.Current;
        foreach (XenotypeDomainKey key in CollectXenotypeDomains(settings, catalog))
        {
            if (string.Equals(key.RaceDefName, raceDefName, StringComparison.Ordinal)
                && string.Equals(key.TargetDefName, xenotypeDefName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ExecuteSetActiveTab(VoicePacksPageState state, string tab)
    {
        string normalized;
        if (string.Equals(tab, "Basic", StringComparison.OrdinalIgnoreCase))
            normalized = "Basic";
        else if (string.Equals(tab, "Tuning", StringComparison.OrdinalIgnoreCase))
            normalized = "Tuning";
        else if (string.Equals(tab, "Packs", StringComparison.OrdinalIgnoreCase))
            normalized = "Packs";
        else
            return;

        if (!string.Equals(state.ActiveTab, normalized, StringComparison.Ordinal))
        {
            state.ActiveTab = normalized;
            state.ScrollPosition = Vector2.zero;
        }
    }

    private static void ExecuteScrollToSection(VoicePacksPageState state, string sectionKey)
    {
        if (string.IsNullOrEmpty(sectionKey)) return;
        state.ScrollTargetKey = sectionKey;
        state.ActiveSectionKey = sectionKey;
        state.ActiveTab = SectionGroup(sectionKey);
    }

    private static string SectionGroup(string sectionKey)
    {
        if (string.Equals(sectionKey, "mode-row", StringComparison.Ordinal)
            || string.Equals(sectionKey, "global-volume", StringComparison.Ordinal)
            || string.Equals(sectionKey, "attenuation-editor", StringComparison.Ordinal)
            || string.Equals(sectionKey, "basic-tuning", StringComparison.Ordinal)
            || string.Equals(sectionKey, "camera-indicator", StringComparison.Ordinal))
        {
            return "Basic";
        }

        if (string.Equals(sectionKey, "scope-tree", StringComparison.Ordinal)
            || string.Equals(sectionKey, "preset-list", StringComparison.Ordinal))
        {
            return "Tuning";
        }

        return "Packs";
    }

    /// <summary>分层 scope 写桥执行：arg = "scope|actionKey"（scope 空 = 清本层记录），
    /// 层域身份取命令自带的 (raceDefName, xenotypeDefName)。</summary>
    private static void ExecuteSetActionTuningScope(UniversalSqueakerSettings settings, UiCommand command, VoicePacksPageState state)
    {
        if (string.IsNullOrEmpty(command.Arg)) return;
        // 非 Global 层必须具有完整域身份，避免空 catalog/损坏状态把 Race/Xeno 编辑误写成 Global。
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(command.RaceDefName)) return;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(command.RaceDefName) || string.IsNullOrEmpty(command.TargetDefName))) return;
        string[] parts = command.Arg.Split('|');
        SqueakActionScope? scope = parts.Length > 0 && !string.IsNullOrEmpty(parts[0])
            && Enum.TryParse(parts[0], true, out SqueakActionScope parsedScope) ? parsedScope : (SqueakActionScope?)null;
        string actionKey = parts.Length > 1 ? parts[1] : "";
        if (string.IsNullOrEmpty(actionKey)) return;
        settings.SetActionTuningScope(actionKey, command.RaceDefName, command.TargetDefName, scope);
    }

    /// <summary>S5 心情调音执行：arg = "MoodName|factor|value" | "MoodName|clear"。</summary>
    private static void ExecuteSetMoodTuning(UniversalSqueakerSettings settings, UiCommand command, VoicePacksPageState state)
    {
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(command.RaceDefName)) return;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(command.RaceDefName) || string.IsNullOrEmpty(command.TargetDefName))) return;
        string[] parts = command.Arg.Split('|');
        if (parts.Length < 2) return;
        if (!Enum.TryParse(parts[0], true, out SqueakMood mood)) return;
        string factor = parts[1];
        float? value = null;
        if (parts.Length > 2 && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            value = parsed;
        settings.SetMoodTuning(mood, command.RaceDefName, command.TargetDefName, factor, value);
    }

    private static void ExecuteSetDistanceRange(UniversalSqueakerSettings settings, UiCommand command)
    {
        if (string.IsNullOrEmpty(command.Arg)) return;
        string[] parts = command.Arg.Split('|');
        if (parts.Length < 2) return;
        if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float start)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float end))
        {
            settings.SetDistanceRange(start, end);
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

    /// <summary>S5 调音层域选项：Global 层空；Race 层 = catalog 全 race；Xenotype 层 = (race,xeno) 域联合。
    /// 未选中/失效时自动归一为首个选项（空 catalog → 空域）。</summary>
    private static IReadOnlyList<TuningDomainOptionView> BuildTuningDomains(
        UniversalSqueakerSettings settings,
        SqueakXenotypeCatalogSnapshot catalog,
        int layer,
        ref string race,
        ref string xeno)
    {
        // ref 参数不得被 lambda 捕获（CS1628）：先取局部副本，结束时写回。
        string currentRace = race;
        string currentXeno = xeno;
        List<TuningDomainOptionView> options = new();
        if (layer == 1)
        {
            foreach (string raceDefName in catalog.RaceDefNames)
            {
                if (string.IsNullOrEmpty(raceDefName)) continue;
                options.Add(new TuningDomainOptionView(raceDefName, ResolveRaceLabel(raceDefName)));
            }
            if (string.IsNullOrEmpty(currentRace) || !options.Any(o => string.Equals(o.RaceDefName, currentRace, StringComparison.Ordinal)))
            {
                currentRace = options.Count > 0 ? options[0].RaceDefName : "";
            }
            currentXeno = "";
        }
        else if (layer == 2)
        {
            foreach (XenotypeDomainKey key in CollectXenotypeDomains(settings, catalog))
            {
                options.Add(new TuningDomainOptionView(
                    key.RaceDefName,
                    ResolveXenotypeLabel(catalog, key.TargetDefName) + " (" + key.TargetDefName + ")",
                    key.TargetDefName));
            }
            if (string.IsNullOrEmpty(currentRace) || string.IsNullOrEmpty(currentXeno)
                || !options.Any(o => string.Equals(o.RaceDefName, currentRace, StringComparison.Ordinal) && string.Equals(o.TargetDefName, currentXeno, StringComparison.Ordinal)))
            {
                currentRace = options.Count > 0 ? options[0].RaceDefName : "";
                currentXeno = options.Count > 0 ? options[0].TargetDefName : "";
            }
        }
        else
        {
            currentRace = "";
            currentXeno = "";
        }
        race = currentRace;
        xeno = currentXeno;
        return options;
    }

    /// <summary>S5 分层 scope 投影：17 内置动作 × 当前调音层。行携带本层记录（HasOwnScope/Scope）与
    /// 有效作用域（DefaultScope &lt; Global &lt; Race &lt; Xenotype，字段级 last-wins，与运行时同规则）。
    /// 外部动作键不在编辑器范围内（与 BuildGlobalActions 的 YAGNI 契约一致）。</summary>
    private static IReadOnlyList<ActionScopeRowView> BuildActionScopes(UniversalSqueakerSettings settings, int layer, string race, string xeno)
    {
        List<ActionScopeRowView> rows = new();
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            if (!SqueakActionDefinitions.IsKnown(action)) continue;
            if (ActionScopeRules.IsHiddenByDefault(action)) continue;
            string key = UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString();
            SqueakActionDefinition definition = SqueakActionDefinitions.Get(action);
            SqueakActionScope effective = definition.DefaultScope;
            ActionScopeGroup group = ActionScopeRules.GroupFor(definition);
            bool hasOwn = false;
            SqueakActionScope own = effective;
            // 按层优先级折叠（Default < Global < Race < Xeno）；同层多条按列表顺序后写胜出（与运行时 Merge 一致）。
            int bestLayer = -1;
            foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
            {
                if (record == null || record.IsValidLayer(out int recordLayer) == false || !record.hasScope) continue;
                if (!string.Equals(record.actionKey, key, StringComparison.Ordinal)) continue;
                bool layer0 = recordLayer == 0;
                bool layer1 = recordLayer == 1 && string.Equals(record.raceDefName, race, StringComparison.Ordinal);
                bool layer2 = recordLayer == 2 && string.Equals(record.raceDefName, race, StringComparison.Ordinal)
                    && string.Equals(record.xenotypeDefName, xeno, StringComparison.Ordinal);
                if ((layer0 || layer1 || layer2) && recordLayer >= bestLayer)
                {
                    bestLayer = recordLayer;
                    effective = record.scope;
                }
                bool isOwn = recordLayer == layer
                    && (layer == 0
                        || (layer == 1 && string.Equals(record.raceDefName, race, StringComparison.Ordinal))
                        || (layer == 2 && string.Equals(record.raceDefName, race, StringComparison.Ordinal) && string.Equals(record.xenotypeDefName, xeno, StringComparison.Ordinal)));
                if (isOwn)
                {
                    hasOwn = true;
                    own = record.scope;
                }
            }
            rows.Add(new ActionScopeRowView(key, SqueakLabels.Action(action), group, own, action, hasOwn, effective));
        }
        return rows;
    }

    /// <summary>S5 分层心情编辑器投影：4 档心情 × 当前调音层。Own = 本层记录（null = 继承），
    /// Effective = 默认(1/1/One) &lt; Global &lt; Race &lt; Xeno 字段级 last-wins（与运行时同规则）。</summary>
    private static IReadOnlyList<MoodTuningRowView> BuildMoodTuningRows(UniversalSqueakerSettings settings, int layer, string race, string xeno)
    {
        List<MoodTuningRowView> rows = new();
        foreach (SqueakMood mood in Enum.GetValues(typeof(SqueakMood)))
        {
            float pitch = 1f;
            float volume = 1f;
            FloatRange jitter = FloatRange.One;
            MoodTuningRecord? own = null;
            // 按层优先级逐因子折叠（Global < Race < Xeno）；同层多条按列表顺序后写胜出（与运行时 Merge 一致）。
            int bestPitchLayer = -1;
            int bestVolumeLayer = -1;
            int bestJitterLayer = -1;
            foreach (MoodTuningRecord record in settings.moodTuning ?? new List<MoodTuningRecord>())
            {
                if (record == null || record.mood != mood || record.IsValidLayer(out int recordLayer) == false) continue;
                bool layer0 = recordLayer == 0;
                bool layer1 = recordLayer == 1 && string.Equals(record.raceDefName, race, StringComparison.Ordinal);
                bool layer2 = recordLayer == 2 && string.Equals(record.raceDefName, race, StringComparison.Ordinal)
                    && string.Equals(record.xenotypeDefName, xeno, StringComparison.Ordinal);
                if (layer0 || layer1 || layer2)
                {
                    if (record.hasPitchFactor && recordLayer >= bestPitchLayer) { bestPitchLayer = recordLayer; pitch = record.pitchFactor; }
                    if (record.hasVolumeFactor && recordLayer >= bestVolumeLayer) { bestVolumeLayer = recordLayer; volume = record.volumeFactor; }
                    if (record.hasPitchJitter && recordLayer >= bestJitterLayer) { bestJitterLayer = recordLayer; jitter = record.pitchJitter; }
                }
                bool isOwn = recordLayer == layer
                    && (layer == 0
                        || (layer == 1 && string.Equals(record.raceDefName, race, StringComparison.Ordinal))
                        || (layer == 2 && string.Equals(record.raceDefName, race, StringComparison.Ordinal) && string.Equals(record.xenotypeDefName, xeno, StringComparison.Ordinal)));
                if (isOwn) own = record;
            }
            float jitterHalf = bestJitterLayer >= 0 ? Math.Max(0f, jitter.max - 1f) : 0f;
            rows.Add(new MoodTuningRowView(mood, mood.ToString(), own, pitch, volume, jitterHalf));
        }
        return rows;
    }

    /// <summary>Project the tuning-baseline preset Defs into a selectable tree for the import widget.</summary>
    private static IReadOnlyList<BaselinePresetView> BuildBaselinePresets(VoicePacksPageState state)
    {
        List<BaselinePresetView> result = new();
        foreach (UniversalSqueakerTuningBaselineDef preset in DefDatabase<UniversalSqueakerTuningBaselineDef>.AllDefs)
        {
            if (preset == null) continue;
            BaselinePresetSelection selection = GetOrCreatePresetSelection(state, preset.defName);

            List<BaselineRaceView> races = new();
            int selectedRaces = 0;
            int selectedXenotypes = 0;
            foreach (BaselineRaceEntry race in preset.races ?? new List<BaselineRaceEntry>())
            {
                if (race == null || string.IsNullOrWhiteSpace(race.raceDefName)) continue;

                List<BaselineXenotypeView> xenotypes = new();
                foreach (BaselineXenotypeEntry xenotype in race.xenotypes ?? new List<BaselineXenotypeEntry>())
                {
                    if (xenotype == null || string.IsNullOrWhiteSpace(xenotype.xenotypeDefName)) continue;
                    bool xenoSelected = selection.SelectedXenotypeDomainKeys.Contains(
                        BaselinePresetImporter.XenotypeDomainKey(race.raceDefName, xenotype.xenotypeDefName));
                    if (xenoSelected) selectedXenotypes++;
                    xenotypes.Add(new BaselineXenotypeView(
                        xenotype.xenotypeDefName,
                        ResolveXenotypeDisplayName(xenotype.xenotypeDefName),
                        xenotype.inheritFromRace,
                        xenoSelected,
                        (xenotype.actions ?? new List<BaselineActionTuning>()).Count(t => t != null && !string.IsNullOrWhiteSpace(t.actionKey)),
                        (xenotype.moods ?? new List<BaselineMoodTuning>()).Count(t => t != null)));
                }

                bool raceSelected = selection.SelectedRaceDefNames.Contains(race.raceDefName);
                if (raceSelected) selectedRaces++;
                races.Add(new BaselineRaceView(
                    race.raceDefName,
                    ResolveRaceLabel(race.raceDefName),
                    raceSelected,
                    (race.actions ?? new List<BaselineActionTuning>()).Count(t => t != null && !string.IsNullOrWhiteSpace(t.actionKey)),
                    (race.moods ?? new List<BaselineMoodTuning>()).Count(t => t != null),
                    xenotypes));
            }

            result.Add(new BaselinePresetView(
                preset.defName,
                ResolvePresetLabel(preset),
                preset.presetDescription,
                selection.Expanded,
                races,
                selectedRaces,
                selectedXenotypes));
        }
        return result;
    }

    private static BaselinePresetSelection GetOrCreatePresetSelection(VoicePacksPageState state, string defName)
    {
        if (!state.BaselinePresets.TryGetValue(defName, out BaselinePresetSelection? selection))
        {
            selection = new BaselinePresetSelection();
            state.BaselinePresets[defName] = selection;
        }
        return selection;
    }

    private static string ResolvePresetLabel(UniversalSqueakerTuningBaselineDef preset)
    {
        if (!string.IsNullOrEmpty(preset.presetLabel)) return preset.presetLabel;
        if (!string.IsNullOrEmpty(preset.label)) return preset.label;
        return preset.defName;
    }

    private static string ResolveXenotypeDisplayName(string xenotypeDefName)
    {
        XenotypeDef? def = DefDatabase<XenotypeDef>.GetNamedSilentFail(xenotypeDefName);
        return def != null && !string.IsNullOrEmpty(def.LabelCap) ? def.LabelCap : xenotypeDefName;
    }

    private static void ToggleBaselinePreset(VoicePacksPageState state, string presetDefName)
    {
        if (string.IsNullOrEmpty(presetDefName)) return;
        BaselinePresetSelection selection = GetOrCreatePresetSelection(state, presetDefName);
        selection.Expanded = !selection.Expanded;
    }

    private static void ToggleBaselineRace(VoicePacksPageState state, string presetDefName, string raceDefName, bool selected)
    {
        if (string.IsNullOrEmpty(presetDefName) || string.IsNullOrEmpty(raceDefName)) return;
        BaselinePresetSelection selection = GetOrCreatePresetSelection(state, presetDefName);
        if (selected) selection.SelectedRaceDefNames.Add(raceDefName);
        else selection.SelectedRaceDefNames.Remove(raceDefName);
    }

    private static void ToggleBaselineXenotype(VoicePacksPageState state, string presetDefName, string raceDefName, string xenotypeDefName, bool selected)
    {
        if (string.IsNullOrEmpty(presetDefName) || string.IsNullOrEmpty(raceDefName) || string.IsNullOrEmpty(xenotypeDefName)) return;
        BaselinePresetSelection selection = GetOrCreatePresetSelection(state, presetDefName);
        string key = BaselinePresetImporter.XenotypeDomainKey(raceDefName, xenotypeDefName);
        if (selected) selection.SelectedXenotypeDomainKeys.Add(key);
        else selection.SelectedXenotypeDomainKeys.Remove(key);
    }

    private static void ImportBaselinePreset(UniversalSqueakerSettings settings, string presetDefName, VoicePacksPageState state)
    {
        if (string.IsNullOrEmpty(presetDefName)) return;
        UniversalSqueakerTuningBaselineDef? preset = DefDatabase<UniversalSqueakerTuningBaselineDef>.GetNamedSilentFail(presetDefName);
        if (preset == null) return;
        BaselinePresetSelection selection = GetOrCreatePresetSelection(state, presetDefName);
        BaselinePresetImporter.Selection importSelection = new();
        foreach (string race in selection.SelectedRaceDefNames) importSelection.RaceDefNames.Add(race);
        foreach (string key in selection.SelectedXenotypeDomainKeys) importSelection.XenotypeDomainKeys.Add(key);
        settings.ImportBaselinePreset(preset, importSelection);
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

    private static List<FilterOptionView> BuildRaceFilterOptions(IReadOnlyList<RaceLayerRowView> races)
    {
        var result = new List<FilterOptionView>();
        foreach (RaceLayerRowView race in races)
        {
            if (string.IsNullOrEmpty(race.RaceDefName)) continue;
            result.Add(new FilterOptionView(race.DisplayName, race.RaceDefName));
        }

        return result;
    }

    private static List<FilterOptionView> BuildXenotypeFilterOptions(
        IReadOnlyList<VoicePackDomainView> xenotypes,
        string raceFilter)
    {
        var result = new List<FilterOptionView>();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (VoicePackDomainView domain in xenotypes)
        {
            if (string.IsNullOrEmpty(domain.TargetDefName)) continue;
            if (!VoicePacksFilters.RaceFilterMatches(raceFilter, domain.RaceDefName)) continue;
            string identity = domain.TargetDefName + "\n" + domain.RaceDefName;
            if (!seen.Add(identity)) continue;
            string display = string.IsNullOrEmpty(raceFilter)
                ? domain.DisplayName + " (" + domain.RaceDefName + ")"
                : domain.DisplayName;
            result.Add(new FilterOptionView(display, domain.TargetDefName));
        }

        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName));
        return result;
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

        foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
            if (record != null && record.IsValidLayer(out int actionLayer) && actionLayer == 2)
                Add(record.raceDefName, record.xenotypeDefName);
        foreach (MoodTuningRecord record in settings.moodTuning ?? new List<MoodTuningRecord>())
            if (record != null && record.IsValidLayer(out int moodLayer) && moodLayer == 2)
                Add(record.raceDefName, record.xenotypeDefName);
        foreach (XenotypePresetRecord record in settings.xenotypePresets ?? new List<XenotypePresetRecord>())
            if (record != null && !string.IsNullOrEmpty(record.xenotypeDefName))
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
            VoicePackDomainView? match = xenotypes.FirstOrDefault(domain =>
                string.Equals(domain.RaceDefName, state.SelectedRaceDefName, StringComparison.Ordinal)
                && string.Equals(domain.TargetDefName, state.SelectedTargetName, StringComparison.Ordinal));
            if (match == null && xenotypes.Count > 0) match = xenotypes[0];
            if (match != null)
            {
                state.SelectedScope = SqueakVoicePackScope.Xenotype;
                state.SelectedRaceDefName = match.Value.RaceDefName;
                state.SelectedTargetName = match.Value.TargetDefName;
                return match;
            }
        }

        if (state.SelectedScope == SqueakVoicePackScope.Race || races.Count > 0)
        {
            RaceLayerRowView race = races.FirstOrDefault(row =>
                string.Equals(row.RaceDefName, state.SelectedRaceDefName, StringComparison.Ordinal));
            if (race.RaceDefName == null) race = races.FirstOrDefault();
            if (race.RaceDefName != null)
            {
                state.SelectedScope = SqueakVoicePackScope.Race;
                state.SelectedRaceDefName = race.RaceDefName;
                state.SelectedTargetName = "";
                return BuildRaceDomain(settings, catalog, race.RaceDefName);
            }
        }

        if (xenotypes.Count > 0)
        {
            state.SelectedScope = SqueakVoicePackScope.Xenotype;
            state.SelectedRaceDefName = xenotypes[0].RaceDefName;
            state.SelectedTargetName = xenotypes[0].TargetDefName;
            return xenotypes[0];
        }

        return null;
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
        string searchText = label + "\n" + pack.defName + "\n" + modName + "\n" + author + "\n" + key;
        bool selected = enabledKeys != null && enabledKeys.Contains(key);
        return new VoicePackRowView(key, label, modName, author, pack.defName, coverage, searchText, selected);
    }

    private static IReadOnlyList<string> CollectAuthors(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        HashSet<string> authors = new(StringComparer.Ordinal);
        foreach (string race in catalog.RaceDefNames)
        {
            if (string.IsNullOrEmpty(race)) continue;
            foreach (SqueakVoicePackDef pack in catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, race))
            {
                string author = CreateVoicePackRow(pack, Array.Empty<string>()).Author;
                if (!string.IsNullOrEmpty(author)) authors.Add(author);
            }
        }

        foreach (KeyValuePair<string, IReadOnlyList<SqueakVoicePackDef>> pair in catalog.XenotypePacksByDefName)
        {
            foreach (SqueakVoicePackDef pack in pair.Value)
            {
                string author = CreateVoicePackRow(pack, Array.Empty<string>()).Author;
                if (!string.IsNullOrEmpty(author)) authors.Add(author);
            }
        }

        List<string> result = authors.ToList();
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static VoicePackDomainView FilterDomainPacks(VoicePackDomainView domain, in UiPackFilter filter)
    {
        UiPackFilter localFilter = filter;
        List<VoicePackRowView> filteredPacks = domain.Packs
            .Where(pack => VoicePacksFilters.PackMatches(pack.Author, pack.ModName, in localFilter))
            .ToList();
        return new VoicePackDomainView(
            domain.Scope,
            domain.RaceDefName,
            domain.TargetDefName,
            domain.DisplayName,
            domain.SourceText,
            domain.State,
            domain.IsDormant,
            domain.IsTargetUnavailable,
            domain.HasCanonicalConflict,
            domain.EnabledCount,
            domain.CandidateCount,
            domain.OrphanCount,
            domain.EnabledKeys,
            filteredPacks);
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
        IReadOnlyList<RaceLayerRowView> races,
        IReadOnlyList<VoicePackDomainView> xenotypes,
        SqueakVoicePackMode mode,
        bool biotech)
    {
        List<string> messages = new();
        if (races.Count == 0 && xenotypes.Count == 0)
            messages.Add("No VoicePack domains are currently installed. Add a VoicePack that declares a raceDefName to configure audio.");
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
