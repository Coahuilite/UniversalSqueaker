using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Single business entry for the VoicePacks page. <see cref="BuildView"/> projects the read-only view
/// state from settings/catalog; the typed facade writes player intent. Every write lands on the
/// existing settings bridge (<see cref="UniversalSqueakerSettings.SetVoicePackSelection"/> /
/// <see cref="UniversalSqueakerSettings.CommitVoicePackMode"/>), so resolver rebuild and
/// QueuePersistence semantics stay where they were.
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
            IReadOnlyList<SqueakVoicePackDef> packs = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, race)
                ?? Array.Empty<SqueakVoicePackDef>();
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
        string buildIdentity = UniversalSqueakerMod.Instance != null ? UniversalSqueakerMod.BuildIdentity() : "US.Footer.Build.Unknown".Translate();
        string saveStatus = UniversalSqueakerMod.Instance?.SaveState.ToString() ?? "Unknown";
        bool isDirty = UniversalSqueakerMod.Instance?.IsSettingsDirty ?? false;
        return new VoicePacksViewState(mode, settings.AllowEasterEggSounds, settings.distancePreset, settings.scaleCooldownWithTimeSpeed, settings.scaleFrequencyWithTalking, settings.scalePeriodicWithAudiblePopulation, settings.showCameraIndicator, settings.globalCooldownMultiplier, settings.globalMinIntervalTicks, settings.devLoggingMode, settings.localizeDebugActions, settings.globalVolumeFactor, settings.distanceRange.min, settings.distanceRange.max, biotech, banner, filteredRaces, filteredXenotypes, selected, actionScopes, state.TuningLayer, tuningRace, tuningXeno, tuningDomains, moodTuningRows, baselinePresets, buildIdentity, saveStatus, isDirty, authors, state.RaceFilter, state.XenotypeFilter, raceFilterOptions, xenotypeFilterOptions);
    }

    private static void ApplyDomainFilter(VoicePacksPageState state, SqueakDomainFilterKind kind, bool flag)
    {
        UiDomainFilter filter = state.DomainFilter;
        state.DomainFilter = kind switch
        {
            SqueakDomainFilterKind.EnabledOnly => new UiDomainFilter(flag, filter.ConflictOnly, filter.OrphanOnly),
            SqueakDomainFilterKind.ConflictOnly => new UiDomainFilter(filter.EnabledOnly, flag, filter.OrphanOnly),
            SqueakDomainFilterKind.OrphanOnly => new UiDomainFilter(filter.EnabledOnly, filter.ConflictOnly, flag),
            _ => filter
        };
    }

    private static void ApplyDomainFilter(VoicePacksPageState state, string kind, bool flag)
    {
        UiDomainFilter filter = state.DomainFilter;
        if (string.Equals(kind, "EnabledOnly", StringComparison.OrdinalIgnoreCase))
        {
            filter = new UiDomainFilter(flag, filter.ConflictOnly, filter.OrphanOnly);
        }
        else if (string.Equals(kind, "ConflictOnly", StringComparison.OrdinalIgnoreCase))
        {
            filter = new UiDomainFilter(filter.EnabledOnly, flag, filter.OrphanOnly);
        }
        else if (string.Equals(kind, "OrphanOnly", StringComparison.OrdinalIgnoreCase))
        {
            filter = new UiDomainFilter(filter.EnabledOnly, filter.ConflictOnly, flag);
        }
        else
        {
            return;
        }

        state.DomainFilter = filter;
    }

    private static void ApplyPackFilter(VoicePacksPageState state, string author)
    {
        state.PackFilter = new UiPackFilter(author);
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

    private static string SectionGroup(string sectionKey)
    {
        if (string.Equals(sectionKey, "attenuation-editor", StringComparison.Ordinal)) return "Distance";
        if (string.Equals(sectionKey, "scope-tree", StringComparison.Ordinal)) return "Tuning";
        if (string.Equals(sectionKey, "preset-list", StringComparison.Ordinal)) return "Presets";
        if (string.Equals(sectionKey, "filter-bar", StringComparison.Ordinal)
            || string.Equals(sectionKey, "race-layer", StringComparison.Ordinal)
            || string.Equals(sectionKey, "xenotype-layer", StringComparison.Ordinal)
            || string.Equals(sectionKey, "checklist", StringComparison.Ordinal))
        {
            return "Packs";
        }

        return "Overview";
    }

    /// <summary>Workspace switch: normalises the incoming name and points the active section at the
    /// new workspace's primary target. Neither the scroll offset nor the help hover claim is this
    /// model's business any more: both are session state, reset or held by the Host/session.</summary>
    private static void ApplyActiveTab(VoicePacksPageState state, string tab)
    {
        string normalized;
        if (string.Equals(tab, "Overview", StringComparison.OrdinalIgnoreCase))
            normalized = "Overview";
        else if (string.Equals(tab, "Distance", StringComparison.OrdinalIgnoreCase))
            normalized = "Distance";
        else if (string.Equals(tab, "Packs", StringComparison.OrdinalIgnoreCase))
            normalized = "Packs";
        else if (string.Equals(tab, "Tuning", StringComparison.OrdinalIgnoreCase))
            normalized = "Tuning";
        else if (string.Equals(tab, "Presets", StringComparison.OrdinalIgnoreCase))
            normalized = "Presets";
        else
            return;

        if (!string.Equals(state.ActiveTab, normalized, StringComparison.Ordinal))
        {
            state.ActiveTab = normalized;
            state.ActiveSectionKey = WorkspacePrimarySection(normalized);
        }
    }

    /// <summary>Primary navigation target of each workspace.</summary>
    private static string WorkspacePrimarySection(string workspace)
    {
        return workspace switch
        {
            "Distance" => "attenuation-editor",
            "Packs" => "filter-bar",
            "Tuning" => "scope-tree",
            "Presets" => "preset-list",
            _ => "mode-row",
        };
    }

    private static void ApplyScrollToSection(VoicePacksPageState state, string sectionKey)
    {
        if (string.IsNullOrEmpty(sectionKey)) return;
        state.ActiveSectionKey = sectionKey;
        state.ActiveTab = SectionGroup(sectionKey);
    }

    /// <summary>Typed scope write；层域身份取当前 state 的 (TuningRaceDefName, TuningXenotypeDefName)。
    /// 非 Global 层必须具有完整域身份，避免空 catalog/损坏状态把 Race/Xeno 编辑误写成 Global。</summary>
    private static void ApplyActionTuningScope(UniversalSqueakerSettings settings, VoicePacksPageState state, string actionKey, SqueakActionScope? scope)
    {
        if (string.IsNullOrEmpty(actionKey)) return;
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(state.TuningRaceDefName)) return;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(state.TuningRaceDefName) || string.IsNullOrEmpty(state.TuningXenotypeDefName))) return;
        settings.SetActionTuningScope(actionKey, state.TuningRaceDefName, state.TuningXenotypeDefName, scope);
    }

    /// <summary>Typed field-level mood write；层域身份取当前 state 的 (TuningRaceDefName, TuningXenotypeDefName)。</summary>
    private static void ApplyMoodTuning(UniversalSqueakerSettings settings, VoicePacksPageState state, SqueakMood mood, string factor, float? value)
    {
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(state.TuningRaceDefName)) return;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(state.TuningRaceDefName) || string.IsNullOrEmpty(state.TuningXenotypeDefName))) return;
        settings.SetMoodTuning(mood, state.TuningRaceDefName, state.TuningXenotypeDefName, factor, value);
    }

    private static void ApplyMoodTuning(
        UniversalSqueakerSettings settings,
        VoicePacksPageState state,
        SqueakMood mood,
        SqueakMoodFactor factor,
        float? value)
    {
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(state.TuningRaceDefName)) return;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(state.TuningRaceDefName) || string.IsNullOrEmpty(state.TuningXenotypeDefName))) return;
        settings.SetMoodTuning(mood, state.TuningRaceDefName, state.TuningXenotypeDefName, factor, value);
    }

    /// <summary>Typed VoicePack checkbox write inside one domain.</summary>
    private static void ApplyTogglePack(
        UniversalSqueakerSettings settings,
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName,
        string packKey,
        bool enabled)
    {
        if (string.IsNullOrEmpty(packKey) || string.IsNullOrEmpty(raceDefName)) return;
        SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(scope, raceDefName, targetDefName);
        List<string> next = new(status.EnabledKeys ?? Array.Empty<string>());
        if (enabled)
        {
            if (!next.Contains(packKey, StringComparer.Ordinal)) next.Add(packKey);
        }
        else
        {
            next.RemoveAll(key => string.Equals(key, packKey, StringComparison.Ordinal));
        }
        settings.SetVoicePackSelection(scope, raceDefName, targetDefName, next);
    }

    /// <summary>Typed Forget Unavailable for one domain identity.</summary>
    private static void ApplyForgetUnavailable(
        UniversalSqueakerSettings settings,
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName)
    {
        if (string.IsNullOrEmpty(raceDefName)) return;
        SqueakXenotypeCatalogSnapshot catalog = SqueakXenotypeCatalog.Current;
        SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(scope, raceDefName, targetDefName);
        HashSet<string> domainKeys = new(StringComparer.Ordinal);
        IReadOnlyList<SqueakVoicePackDef> domainPacks = scope == SqueakVoicePackScope.Race
            ? catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, raceDefName) ?? Array.Empty<SqueakVoicePackDef>()
            : (catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Xenotype, targetDefName) ?? Array.Empty<SqueakVoicePackDef>())
                .Where(pack => string.Equals(pack.raceDefName, raceDefName, StringComparison.Ordinal))
                .ToList();
        foreach (SqueakVoicePackDef pack in domainPacks)
            if (pack.TryGetPackKey(out string key)) domainKeys.Add(key);
        List<string> retained = (status.EnabledKeys ?? Array.Empty<string>())
            .Where(key => domainKeys.Contains(key))
            .ToList();
        settings.SetVoicePackSelection(scope, raceDefName, targetDefName, retained);
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

            // The two reset actions are judged from flags and source alone (F-Q): a cleared row that kept
            // its source is unavailable for "reset to default" and ready for "reset to preset". The
            // preset Def/entry resolution happens here because this is the layer that may touch the Def
            // database; the decision itself lives in Pure so the matrix is harness-testable.
            SqueakMoodResetDefaultState defaultReset = SqueakMoodResetActions.EvaluateDefault(
                own?.hasPitchFactor == true, own?.hasVolumeFactor == true, own?.hasPitchJitter == true);
            string sourcePreset = own?.sourcePresetDefName ?? "";
            UniversalSqueakerTuningBaselineDef? presetDef = sourcePreset.Length > 0
                ? DefDatabase<UniversalSqueakerTuningBaselineDef>.GetNamedSilentFail(sourcePreset)
                : null;
            bool presetHasEntry = presetDef != null
                && UniversalSqueakerSettings.TryFindMoodBaseline(presetDef, mood, race, xeno, out _);
            SqueakMoodResetPresetState presetReset = SqueakMoodResetActions.EvaluatePreset(sourcePreset, presetDef != null, presetHasEntry);

            rows.Add(new MoodTuningRowView(mood, mood.ToString(), own, pitch, volume, jitterHalf, defaultReset, presetReset));
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
                SqueakVoicePackScope.Xenotype, key.TargetDefName) ?? Array.Empty<SqueakVoicePackDef>();
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
                rows,
                raceDisplay: ResolveRaceLabel(key.RaceDefName)));
        }
        return domains;
    }

    private static List<FilterOptionView> BuildRaceFilterOptions(IReadOnlyList<RaceLayerRowView> races)
    {
        races ??= Array.Empty<RaceLayerRowView>();
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
        xenotypes ??= Array.Empty<VoicePackDomainView>();
        var result = new List<FilterOptionView>();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (VoicePackDomainView domain in xenotypes)
        {
            if (string.IsNullOrEmpty(domain.TargetDefName)) continue;
            if (!VoicePacksFilters.RaceFilterMatches(raceFilter, domain.RaceDefName)) continue;
            string identity = domain.TargetDefName + "\n" + domain.RaceDefName;
            if (!seen.Add(identity)) continue;
            string display = string.IsNullOrEmpty(raceFilter)
                ? domain.DisplayName + " (" + domain.RaceDisplay + ")"
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
        races ??= Array.Empty<RaceLayerRowView>();
        xenotypes ??= Array.Empty<VoicePackDomainView>();

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
        IReadOnlyList<SqueakVoicePackDef> packs = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, raceDefName)
            ?? Array.Empty<SqueakVoicePackDef>();
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
        string coverage = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "US.Packs.Checklist.PackActions".Translate(),
            playable,
            SqueakActionDefinitions.Count);
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
            IReadOnlyList<SqueakVoicePackDef> racePacks = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, race)
                ?? Array.Empty<SqueakVoicePackDef>();
            foreach (SqueakVoicePackDef pack in racePacks)
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
        List<VoicePackRowView> filteredPacks = (domain.Packs ?? Array.Empty<VoicePackRowView>())
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
            filteredPacks,
            // Carry the resolved race context across the filter rebuild; dropping it here made
            // the SELECTED domain silently degrade to the bare raceDefName (D5 sister leak).
            domain.RaceDisplay);
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

    /// <summary>
    /// Page banner. Every line here is a Keyed string: the model has no kernel translation seam, so
    /// it resolves through the Verse Translator the same way the audio-pool notice does.
    /// </summary>
    private static string BuildBannerText(
        IReadOnlyList<RaceLayerRowView> races,
        IReadOnlyList<VoicePackDomainView> xenotypes,
        SqueakVoicePackMode mode,
        bool biotech)
    {
        List<string> messages = new();
        if (races.Count == 0 && xenotypes.Count == 0)
            messages.Add("US.Packs.Banner.NoDomains".Translate());
        if (!biotech && xenotypes.Count > 0)
            messages.Add("US.Packs.Banner.DormantBiotech".Translate());
        if (mode == SqueakVoicePackMode.Vanilla)
            messages.Add("US.Packs.Banner.VanillaMode".Translate());
        return string.Join("\n", messages);
    }

    private static string ResolveRaceLabel(string raceDefName)
    {
        ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
        return def != null && !string.IsNullOrEmpty(def.LabelCap) ? def.LabelCap : raceDefName;
    }

    private static string ResolveXenotypeLabel(SqueakXenotypeCatalogSnapshot catalog, string targetDefName)
    {
        // Single label outlet (D5): the canonical snapshot first, live DefDatabase on a miss - a
        // bare defName is only ever the last resort when no Def exists to label. The catalog is
        // routing/eligibility authority, not a label gate: domain rows legitimately reference
        // xenotypes the snapshot excludes (Biotech gating, name-conflict drops), and those rows
        // must still read as names. The preset tree's resolver (ResolveXenotypeDisplayName) is
        // the live half of this same outlet, so one entity can never render two different labels.
        if (catalog.XenotypeByDefName.TryGetValue(targetDefName, out XenotypeDef? def) && def != null
            && !string.IsNullOrEmpty(def.LabelCap))
        {
            return def.LabelCap;
        }

        return ResolveXenotypeDisplayName(targetDefName);
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

    // ---------------------------------------------------------------------------------------------
    // Typed facade used by the kernel settings Host. Every method delegates to the same private
    // write implementations the legacy command dispatcher used to route through, so there is exactly
    // one business source of truth. Callers pass typed values; there is no string command bridge.
    // ---------------------------------------------------------------------------------------------

    public static void SetActiveTab(VoicePacksPageState state, string tab)
    {
        if (state == null) return;
        ApplyActiveTab(state, tab);
    }

    public static void ScrollToSection(VoicePacksPageState state, string sectionKey)
    {
        if (state == null) return;
        ApplyScrollToSection(state, sectionKey);
    }

    public static string SectionGroupOf(string sectionKey)
    {
        return SectionGroup(sectionKey ?? "");
    }

    public static string SectionHelpKeyOf(string sectionKey)
    {
        return sectionKey switch
        {
            "mode-row" => "us/mode-row",
            "global-volume" => "us/global-volume",
            "attenuation-editor" => "us/attenuation-editor",
            "basic-tuning" => "us/basic-tuning",
            "timing" => "us/timing",
            "diagnostics" => "us/diagnostics",
            "scope-tree" => "us/scope-tree",
            "preset-list" => "us/preset-list",
            "filter-bar" => "us/filter-bar",
            "race-layer" => "us/race-layer",
            "xenotype-layer" => "us/xenotype-layer",
            "checklist" => "us/voice-pack-checklist",
            _ => "us/page-title",
        };
    }

    public static void SetTuningLayer(VoicePacksPageState state, int layer)
    {
        if (state == null) return;
        if (layer >= 0 && layer <= 2) state.TuningLayer = layer;
    }

    public static void SetTuningDomain(VoicePacksPageState state, string raceDefName, string targetDefName)
    {
        if (state == null) return;
        if (!string.IsNullOrEmpty(raceDefName))
        {
            state.TuningRaceDefName = raceDefName;
            state.TuningXenotypeDefName = targetDefName ?? "";
        }
    }

    public static void SelectDomain(VoicePacksPageState state, SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        if (state == null) return;
        state.SelectedScope = scope;
        state.SelectedRaceDefName = raceDefName ?? "";
        state.SelectedTargetName = scope == SqueakVoicePackScope.Xenotype ? targetDefName ?? "" : "";
    }

    public static void SetDomainFilter(VoicePacksPageState state, SqueakDomainFilterKind kind, bool flag)
    {
        if (state == null) return;
        ApplyDomainFilter(state, kind, flag);
    }

    public static void SetPackFilter(VoicePacksPageState state, string author)
    {
        if (state == null) return;
        ApplyPackFilter(state, author ?? "");
    }

    public static void SetRaceFilter(UniversalSqueakerSettings settings, VoicePacksPageState state, string raceDefName)
    {
        if (state == null) return;
        ExecuteSetRaceFilter(settings, state, raceDefName ?? "");
    }

    public static void SetXenotypeFilter(VoicePacksPageState state, string xenotypeDefName)
    {
        if (state == null) return;
        state.XenotypeFilter = xenotypeDefName ?? "";
    }

    public static void SetSearchText(VoicePacksPageState state, string text)
    {
        if (state == null) return;
        state.SearchText = text ?? "";
    }

    // The hover-claim machine (D10 grace) moved to UiSession with FL 0.3.0 P3: widgets claim through
    // UsKernelDraw.HelpHover -> Session.ClaimHover, and the panel/border read Session.HoverClaim. The
    // pinned-selection channel stays retired with the index list (D2 ruling, 2026-09-05) - hover is
    // still the only thing that changes what the help panel shows, it just has a session owner now.

    public static void SetActionScope(UniversalSqueakerSettings settings, VoicePacksPageState state, string actionKey, SqueakActionScope? scope)
    {
        if (state == null || string.IsNullOrEmpty(actionKey)) return;
        ApplyActionTuningScope(settings, state, actionKey, scope);
    }

    public static void SetMoodTuning(
        UniversalSqueakerSettings settings,
        VoicePacksPageState state,
        SqueakMood mood,
        SqueakMoodFactor factor,
        float? value)
    {
        if (state == null) return;
        ApplyMoodTuning(settings, state, mood, factor, value);
    }

    /// <summary>「重置为预设」：读本层末行的来源 → 解析预设 Def → 让 settings 把该 (mood,race,xeno) 的基线
    /// 因子值重新写回（来源保持）。不可用时（无来源 / Def 失效 / 无条目）什么都不做：可用性由
    /// <see cref="MoodTuningRowView.PresetReset"/> 在视图里表达，按钮只是禁用。</summary>
    public static void ResetMoodToPreset(UniversalSqueakerSettings settings, VoicePacksPageState state, SqueakMood mood)
    {
        if (state == null) return;
        string race = state.TuningRaceDefName ?? "";
        string xeno = state.TuningXenotypeDefName ?? "";

        // last-wins：与 BuildMoodTuningRows 取 Own 的口径一致，来源也取末行。
        string source = "";
        foreach (MoodTuningRecord record in settings.moodTuning ?? new List<MoodTuningRecord>())
        {
            if (record == null || record.mood != mood) continue;
            if (!string.Equals(record.raceDefName ?? "", race, StringComparison.Ordinal)) continue;
            if (!string.Equals(record.xenotypeDefName ?? "", xeno, StringComparison.Ordinal)) continue;
            source = record.sourcePresetDefName ?? "";
        }
        if (source.Length == 0) return;

        UniversalSqueakerTuningBaselineDef? preset = DefDatabase<UniversalSqueakerTuningBaselineDef>.GetNamedSilentFail(source);
        settings.ResetMoodTuningToPreset(mood, race, xeno, preset);
    }

    public static void ToggleBaselinePresetSelection(VoicePacksPageState state, string presetDefName)
    {
        if (state == null) return;
        ToggleBaselinePreset(state, presetDefName);
    }

    public static void ToggleBaselineRaceSelection(VoicePacksPageState state, string presetDefName, string raceDefName, bool selected)
    {
        if (state == null) return;
        ToggleBaselineRace(state, presetDefName, raceDefName, selected);
    }

    public static void ToggleBaselineXenotypeSelection(VoicePacksPageState state, string presetDefName, string raceDefName, string xenotypeDefName, bool selected)
    {
        if (state == null) return;
        ToggleBaselineXenotype(state, presetDefName, raceDefName, xenotypeDefName, selected);
    }

    public static void ImportBaselinePresetSelection(UniversalSqueakerSettings settings, string presetDefName, VoicePacksPageState state)
    {
        if (state == null) return;
        ImportBaselinePreset(settings, presetDefName, state);
    }

    public static void ToggleVoicePack(
        UniversalSqueakerSettings settings,
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName,
        string packKey,
        bool enabled)
    {
        if (string.IsNullOrEmpty(packKey) || string.IsNullOrEmpty(raceDefName)) return;
        ApplyTogglePack(settings, scope, raceDefName, targetDefName, packKey, enabled);
    }

    public static void ForgetUnavailable(
        UniversalSqueakerSettings settings,
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName)
    {
        if (string.IsNullOrEmpty(raceDefName)) return;
        ApplyForgetUnavailable(settings, scope, raceDefName, targetDefName);
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
