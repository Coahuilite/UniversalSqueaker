using System;
using UniversalSqueaker.Kernel;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

using UniversalSqueaker.Runtime;

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

        // D4: each domain list narrows additionally by its OWN search box (the one shared substring
        // rule, UsChecklistFilter.QueryMatches - display name and internal defName are both searchable)
        // and the xenotype list FOLLOWS the selected race: while a race row is the browsed domain, only
        // that race's xenotypes (plus race-less global ones) stay listed. Selecting a xenotype row
        // leaves the list untouched, and clearing the race selection (or the card's Clear control)
        // restores the full list - the browsed selection is the only input the follow reads.
        bool followRace = state.SelectedScope == SqueakVoicePackScope.Race
            && !string.IsNullOrEmpty(state.SelectedRaceDefName);
        List<RaceLayerRowView> filteredRaces = races
            .Where(race => VoicePacksFilters.DomainMatches(
                false,
                false,
                false,
                race.State == SqueakVoicePackDomainState.Orphan,
                race.EnabledCount > 0,
                in state.DomainFilter)
                && VoicePacksFilters.RaceFilterMatches(state.RaceFilter, race.RaceDefName)
                && UsChecklistFilter.QueryMatches(state.RaceSearchText, race.DisplayName, race.RaceDefName))
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
                    domain.TargetDefName)
                && (!followRace
                    || string.IsNullOrEmpty(domain.RaceDefName)
                    || string.Equals(domain.RaceDefName, state.SelectedRaceDefName, StringComparison.Ordinal))
                && UsChecklistFilter.QueryMatches(
                    state.XenotypeSearchText, domain.DisplayName, domain.TargetDefName, domain.RaceDefName))
            .ToList();

        // The per-list empty note shows when the list went empty while entries exist (or a search is
        // active) - an unadopted page with no search keeps the banner as its only message.
        bool raceListEmpty = filteredRaces.Count == 0
            && (races.Count > 0 || state.RaceSearchText.Trim().Length > 0);
        bool xenotypeListEmpty = filteredXenotypes.Count == 0
            && (xenotypes.Count > 0 || state.XenotypeSearchText.Trim().Length > 0);

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
        IReadOnlyList<ActionScopeRowView> actionScopes = BuildActionScopes(settings, state.TuningLayer, tuningRace, tuningXeno, settings.BabyActionsEnabled);
        IReadOnlyList<MoodTuningRowView> moodTuningRows = BuildMoodTuningRows(settings, state.TuningLayer, tuningRace, tuningXeno);
        IReadOnlyList<BaselinePresetView> baselinePresets = BuildBaselinePresets(state);
        string buildIdentity = UniversalSqueakerMod.Instance != null ? UniversalSqueakerMod.BuildIdentity() : "US.Footer.Build.Unknown".Translate();
        string saveStatus = UniversalSqueakerMod.Instance?.SaveState.ToString() ?? "Unknown";
        bool isDirty = UniversalSqueakerMod.Instance?.IsSettingsDirty ?? false;
        // VF1定稿: the fallback editor's projection - supported races (maintainer tables ∪ player
        // tables), the selected race's closed-17 entry rows, and the two query-filtered candidate
        // lists. Computed here, from the live store, so the editor and the routing read ONE answer.
        BuildFallbackProjection(state, out int tuningArea, out List<FallbackRaceView> fallbackRaces,
            out string fallbackRace, out List<FallbackEntryView> fallbackEntries,
            out List<FilterOptionView> fallbackSounds, out List<FilterOptionView> fallbackCandidates);

        return new VoicePacksViewState(mode, settings.AllowEasterEggSounds, settings.distancePreset, settings.scaleCooldownWithTimeSpeed, settings.scaleFrequencyWithTalking, settings.scalePeriodicWithAudiblePopulation, settings.showCameraIndicator, settings.globalCooldownMultiplier, settings.globalMinIntervalTicks, settings.devLoggingMode, settings.localizeDebugActions, settings.globalVolumeFactor, settings.distanceRange.min, settings.distanceRange.max, biotech, banner, filteredRaces, filteredXenotypes, selected, actionScopes, state.TuningLayer, tuningRace, tuningXeno, tuningDomains, moodTuningRows, baselinePresets, buildIdentity, saveStatus, isDirty, authors, state.RaceFilter, state.XenotypeFilter, raceFilterOptions, xenotypeFilterOptions, settings.eatPrecisionEnabled, settings.eatPrecisionIncludeDrugs, settings.allowBabyActions, raceListEmpty, xenotypeListEmpty, tuningArea, fallbackRaces, fallbackRace, fallbackEntries, fallbackSounds, fallbackCandidates, state.FallbackStatusKey);
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
        if (!MoodLayerHasIdentity(state)) return;
        settings.SetMoodTuning(mood, state.TuningRaceDefName, state.TuningXenotypeDefName, factor, value);
    }

    /// <summary>Typed field-level mood write (enum factor form). The guard is shared with the string-factor
    /// overload above so the two can never drift apart (they were duplicated line for line).</summary>
    private static void ApplyMoodTuning(
        UniversalSqueakerSettings settings,
        VoicePacksPageState state,
        SqueakMood mood,
        SqueakMoodFactor factor,
        float? value)
    {
        if (!MoodLayerHasIdentity(state)) return;
        settings.SetMoodTuning(mood, state.TuningRaceDefName, state.TuningXenotypeDefName, factor, value);
    }

    /// <summary>非 Global 层必须具有完整域身份，避免空 catalog/损坏状态把 Race/Xeno 编辑误写成 Global。</summary>
    private static bool MoodLayerHasIdentity(VoicePacksPageState state)
    {
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(state.TuningRaceDefName)) return false;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(state.TuningRaceDefName) || string.IsNullOrEmpty(state.TuningXenotypeDefName))) return false;
        return true;
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
    // The baby-eligibility flag arrives as a parameter (the caller reads settings.BabyActionsEnabled):
    // the fold itself must not touch ModsConfig, so the real-writer/real-fold lane can run in the
    // stub harness where Verse.ModsConfig is not loadable (the same reason BuildBannerText takes
    // biotech as an argument).
    private static IReadOnlyList<ActionScopeRowView> BuildActionScopes(UniversalSqueakerSettings settings, int layer, string race, string xeno, bool babyEnabled)
    {
        List<ActionScopeRowView> rows = new();
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            if (!SqueakActionDefinitions.IsKnown(action)) continue;
            if (!SqueakActionDefinitions.IsEligible(action, babyEnabled)) continue;
            string key = UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString();
            SqueakActionDefinition definition = SqueakActionDefinitions.Get(action);
            SqueakActionScope effective = definition.DefaultScope;
            ActionScopeGroup group = ActionScopeRules.GroupFor(action);
            bool hasOwn = false;
            SqueakActionScope own = effective;
            // PRE1/VF1定稿 A1: the three fields fold INDEPENDENTLY, exactly like the runtime merge
            // (field-level last-wins over Default < Global < Race < Xenotype). One loop pass reads
            // every matching record; each field keeps its own best layer and value, so a row can
            // mix an inherited interval with a local probability and still tell the truth about
            // both. The anchor (sourcePresetDefName) rides the CURRENT-layer identity like the
            // mood rows: it proves what reset-to-preset may target, never where a value came from.
            int bestScope = -1;
            int bestInterval = -1;
            int bestProbability = -1;
            float effectiveInterval = 1f;
            float effectiveProbability = 1f;
            bool hasOwnInterval = false;
            bool hasOwnProbability = false;
            float ownInterval = 1f;
            float ownProbability = 1f;
            string anchor = "";
            foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
            {
                if (record == null || record.IsValidLayer(out int recordLayer) == false) continue;
                if (!string.Equals(record.actionKey, key, StringComparison.Ordinal)) continue;
                bool layer0 = recordLayer == 0;
                bool layer1 = recordLayer == 1 && string.Equals(record.raceDefName, race, StringComparison.Ordinal);
                bool layer2 = recordLayer == 2 && string.Equals(record.raceDefName, race, StringComparison.Ordinal)
                    && string.Equals(record.xenotypeDefName, xeno, StringComparison.Ordinal);
                bool supplies = layer0 || layer1 || layer2;
                bool isOwn = recordLayer == layer
                    && (layer == 0
                        || (layer == 1 && string.Equals(record.raceDefName, race, StringComparison.Ordinal))
                        || (layer == 2 && string.Equals(record.raceDefName, race, StringComparison.Ordinal) && string.Equals(record.xenotypeDefName, xeno, StringComparison.Ordinal)));

                if (record.hasScope && supplies && recordLayer >= bestScope)
                {
                    bestScope = recordLayer;
                    effective = record.scope;
                }
                if (record.hasIntervalMultiplier && supplies && recordLayer >= bestInterval)
                {
                    bestInterval = recordLayer;
                    effectiveInterval = record.intervalMultiplier;
                }
                if (record.hasProbabilityMultiplier && supplies && recordLayer >= bestProbability)
                {
                    bestProbability = recordLayer;
                    effectiveProbability = record.probabilityMultiplier;
                }
                if (isOwn)
                {
                    if (record.hasScope) { hasOwn = true; own = record.scope; }
                    if (record.hasIntervalMultiplier) { hasOwnInterval = true; ownInterval = record.intervalMultiplier; }
                    if (record.hasProbabilityMultiplier) { hasOwnProbability = true; ownProbability = record.probabilityMultiplier; }
                    if (!string.IsNullOrEmpty(record.sourcePresetDefName)) anchor = record.sourcePresetDefName;
                }
            }
            Tuple<bool, bool, string> preset = ResolveActionPreset(anchor, key, race, xeno);
            rows.Add(new ActionScopeRowView(
                key, SqueakLabels.Action(action), group, own, action, hasOwn, effective,
                hasOwnInterval, ownInterval, hasOwnProbability, ownProbability,
                effectiveInterval, effectiveProbability,
                bestInterval, bestProbability,
                anchor.Length > 0, preset.Item1 && preset.Item2, preset.Item3));
        }
        return rows;
    }

    /// <summary>S5 分层心情编辑器投影：4 档心情 × 当前调音层。Own = 本层记录（null = 继承），
    /// Effective = 默认(1/1/One) &lt; Global &lt; Race &lt; Xeno 字段级 last-wins（与运行时同规则）。</summary>
    private static IReadOnlyList<MoodTuningRowView> BuildMoodTuningRows(UniversalSqueakerSettings settings, int layer, string race, string xeno)
    {
        return ProjectMoodTuningRows(settings.moodTuning ?? new List<MoodTuningRecord>(), layer, race, xeno, ResolveMoodPreset);
    }

    // The record fold and Ready-gated target projection are executed directly by the Host lane.
    // Only Def lookup is supplied by the caller; settings and persistence remain outside this seam.
    private static IReadOnlyList<MoodTuningRowView> ProjectMoodTuningRows(
        IEnumerable<MoodTuningRecord> records, int layer, string race, string xeno,
        Func<string, SqueakMood, string, string, Tuple<bool, bool, string>> resolvePreset)
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
            foreach (MoodTuningRecord record in records)
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
            Tuple<bool, bool, string> preset = resolvePreset(sourcePreset, mood, race, xeno);
            SqueakMoodResetPresetState presetReset = SqueakMoodResetActions.EvaluatePreset(sourcePreset, preset.Item1, preset.Item2);

            // V3 P2 (corrected in task-18): provenance is the PER-FACTOR SUPPLYING LAYER the fold above
            // already resolved (-1 = no layer supplies it, so the effective value is the default). The
            // persisted anchor `sourcePreset` is NOT provenance: it survives a clear (F-P). It is projected
            // ONLY as the reset-to-preset TARGET, via the resolved Def's own display label and only while
            // that target is actually usable (Ready); a PresetMissing/NoEntry/NotFromPreset row carries no
            // target at all so the readout cannot imply one.
            string resetTarget = presetReset == SqueakMoodResetPresetState.Ready
                ? preset.Item3
                : "";
            rows.Add(new MoodTuningRowView(
                mood, mood.ToString(), own, pitch, volume, jitterHalf, defaultReset, presetReset,
                bestPitchLayer, bestVolumeLayer, bestJitterLayer, resetTarget));
        }
        return rows;
    }

    private static Tuple<bool, bool, string> ResolveMoodPreset(string source, SqueakMood mood, string race, string xeno)
    {
        UniversalSqueakerTuningBaselineDef? preset = source.Length > 0
            ? SqueakGameDefs.BaselineByName(source)
            : null;
        bool hasEntry = preset != null && UniversalSqueakerSettings.TryFindMoodBaseline(preset, mood, race, xeno, out _);
        return Tuple.Create(preset != null, hasEntry, preset != null ? ResolvePresetLabel(preset) : "");
    }

    /// <summary>VF1定稿 A1: the action-side twin of <see cref="ResolveMoodPreset"/> - does the
    /// anchor resolve to a Def, and does that Def carry an entry for THIS action at this identity.
    /// The label is the reset TARGET's name, never a provenance claim.</summary>
    private static Tuple<bool, bool, string> ResolveActionPreset(string source, string actionKey, string race, string xeno)
    {
        UniversalSqueakerTuningBaselineDef? preset = string.IsNullOrEmpty(source)
            ? null
            : SqueakGameDefs.BaselineByName(source);
        bool hasEntry = preset != null && UniversalSqueakerSettings.TryFindActionBaseline(preset, actionKey, race, xeno, out _);
        return Tuple.Create(preset != null, hasEntry, preset != null ? ResolvePresetLabel(preset) : "");
    }

    /// <summary>Project the tuning-baseline preset Defs into a selectable tree for the import widget.</summary>
    private static IReadOnlyList<BaselinePresetView> BuildBaselinePresets(VoicePacksPageState state)
    {
        List<BaselinePresetView> result = new();
        foreach (UniversalSqueakerTuningBaselineDef preset in SqueakGameDefs.AllBaselines())
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
                        (xenotype.moods ?? new List<BaselineMoodTuning>()).Count(t => t != null),
                        ResolveXenotypeIcon(xenotype.xenotypeDefName)));
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
        XenotypeDef? def = ResolveXenotypeDef(xenotypeDefName);
        return def != null && !string.IsNullOrEmpty(def.LabelCap) ? def.LabelCap : xenotypeDefName;
    }

    /// <summary>The live def for a xenotype key, or null: an unloaded/no-Biotech/unknown-def miss is normal.</summary>
    private static XenotypeDef? ResolveXenotypeDef(string xenotypeDefName)
    {
        if (string.IsNullOrWhiteSpace(xenotypeDefName)) return null;
        return DefDatabase<XenotypeDef>.GetNamedSilentFail(xenotypeDefName);
    }

    /// <summary>
    /// R4-B adapter boundary: the ONE place this page turns a xenotype into its native icon. The resource
    /// lookup (<see cref="XenotypeDef.Icon"/>, which reads the def's <c>iconPath</c> through the content
    /// finder) happens here on the Verse side; the view carries the resulting value and the Kernel widget
    /// only hands it to the library's own image outlet. Null is a valid answer and every miss returns it:
    /// an absent definition, a game without Biotech (the DefDatabase has no such def), or a def whose
    /// texture failed to load all give null rather than throwing or inventing a placeholder.
    /// </summary>
    private static UnityEngine.Texture2D? ResolveXenotypeIcon(string xenotypeDefName)
    {
        return ResolveXenotypeDef(xenotypeDefName)?.Icon;
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
        UniversalSqueakerTuningBaselineDef? preset = SqueakGameDefs.BaselineByName(presetDefName);
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
                .Select(pack => CreateVoicePackRow(pack, status.EnabledKeys, settings.BabyActionsEnabled))
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
            .Select(pack => CreateVoicePackRow(pack, status.EnabledKeys, settings.BabyActionsEnabled))
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

    private static VoicePackRowView CreateVoicePackRow(SqueakVoicePackDef pack, IReadOnlyList<string> enabledKeys, bool includeBabyActions)
    {
        string key = pack.TryGetPackKey(out string packKey) ? packKey : pack.defName;
        string label = string.IsNullOrEmpty(pack.LabelCap) ? pack.defName : pack.LabelCap;
        string modName = pack.modContentPack?.Name ?? pack.modContentPack?.PackageId ?? "—";
        string author = pack.modContentPack?.ModMetaData?.AuthorsString ?? "";
        if (string.IsNullOrEmpty(author)) author = modName;
        int playable = pack.CountPlayableActions(includeBabyActions);
        string coverage = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "US.Packs.Checklist.PackActions".Translate(),
            playable,
            SqueakActionDefinitions.EligibleCount(includeBabyActions));
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
                string author = CreateVoicePackRow(pack, Array.Empty<string>(), settings.BabyActionsEnabled).Author;
                if (!string.IsNullOrEmpty(author)) authors.Add(author);
            }
        }

        foreach (KeyValuePair<string, IReadOnlyList<SqueakVoicePackDef>> pair in catalog.XenotypePacksByDefName)
        {
            foreach (SqueakVoicePackDef pack in pair.Value)
            {
                string author = CreateVoicePackRow(pack, Array.Empty<string>(), settings.BabyActionsEnabled).Author;
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

    /// <summary>D4: the race list's own search text (shared substring rule; empty = no narrowing).</summary>
    public static void SetRaceSearchText(VoicePacksPageState state, string text)
    {
        if (state == null) return;
        state.RaceSearchText = text ?? "";
    }

    /// <summary>D4: the xenotype list's own search text.</summary>
    public static void SetXenotypeSearchText(VoicePacksPageState state, string text)
    {
        if (state == null) return;
        state.XenotypeSearchText = text ?? "";
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

    /// <summary>VF1定稿 A2: multiplier write facade - the identity is the CURRENT tuning layer's
    /// (race, xeno), guarded exactly like <see cref="SetActionScope"/>; value == null clears the
    /// field (restore inheritance).</summary>
    public static void SetActionTuningMultiplier(
        UniversalSqueakerSettings settings, VoicePacksPageState state, string actionKey, bool intervalField, float? value)
    {
        if (settings == null || state == null || string.IsNullOrEmpty(actionKey)) return;
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(state.TuningRaceDefName)) return;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(state.TuningRaceDefName) || string.IsNullOrEmpty(state.TuningXenotypeDefName))) return;
        settings.SetActionTuning(actionKey, state.TuningRaceDefName, state.TuningXenotypeDefName, intervalField, value);
    }

    /// <summary>VF1定稿 A4: action reset-to-preset facade, the mood side's shape: the current-layer
    /// identity's last-wins anchor names the preset; no anchor or no resolvable entry = no-op.</summary>
    public static void ResetActionTuningToPreset(UniversalSqueakerSettings settings, VoicePacksPageState state, string actionKey)
    {
        if (settings == null || state == null || string.IsNullOrEmpty(actionKey)) return;
        string anchor = "";
        foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
        {
            if (record == null || !string.Equals(record.actionKey, actionKey, StringComparison.Ordinal)) continue;
            if (record.IsValidLayer(out int recordLayer) == false || recordLayer != state.TuningLayer) continue;
            if (state.TuningLayer >= 1 && !string.Equals(record.raceDefName ?? "", state.TuningRaceDefName ?? "", StringComparison.Ordinal)) continue;
            if (state.TuningLayer == 2 && !string.Equals(record.xenotypeDefName ?? "", state.TuningXenotypeDefName ?? "", StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(record.sourcePresetDefName)) anchor = record.sourcePresetDefName;
        }
        if (anchor.Length == 0) return;
        UniversalSqueakerTuningBaselineDef? preset = SqueakGameDefs.BaselineByName(anchor);
        settings.ResetActionTuningToPreset(actionKey, state.TuningRaceDefName ?? "", state.TuningXenotypeDefName ?? "", preset);
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
    /// <see cref="MoodTuningRowView.PresetReset"/> 在视图里表达，按钮只是禁用。
    /// <para>
    /// <b>XG1.2 - the identity boundary comes FIRST.</b> The (race, xenotype) pair is taken straight from the
    /// state, and a layer-2 state with no target carries ("","") - which is exactly the GLOBAL row's identity.
    /// Without this guard the scan below can match that row and rewrite a global baseline from an empty xenotype
    /// domain; <c>settings.ResetMoodTuningToPreset</c> cannot catch it either (it only rejects xeno-without-race).
    /// The guard is the SAME predicate the two mood writers use (<see cref="MoodLayerHasIdentity"/>), so the
    /// three entries cannot drift apart, and layer 0 (Global) stays legitimately allowed.
    /// </para>
    /// </summary>
    public static void ResetMoodToPreset(UniversalSqueakerSettings settings, VoicePacksPageState state, SqueakMood mood)
    {
        if (state == null) return;
        if (!MoodLayerHasIdentity(state)) return;
        ResetMoodToPresetForIdentity(settings, state, mood);
    }

    // Keep the identity refusal independent of the game-only Def lookup, including JIT type resolution.
    private static void ResetMoodToPresetForIdentity(UniversalSqueakerSettings settings, VoicePacksPageState state, SqueakMood mood)
    {
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

        UniversalSqueakerTuningBaselineDef? preset = SqueakGameDefs.BaselineByName(source);
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

    // -----------------------------------------------------------------------------------------
    // VF1 final-fallback editor: projection + commands (the store is the single authority)
    // -----------------------------------------------------------------------------------------

    /// <summary>The editor's projection over the LIVE store. Supported races = every table the
    /// store resolved (maintainer data ∪ player files, per the VF1 support-set rule); entries are
    /// the closed 17-key set; sound candidates are loaded SoundDefs filtered by the query and
    /// labelled with the current production availability state - an unprobed sound says "unknown",
    /// never "missing" (menu context has no map). Create candidates come from the loaded pawn
    /// ThingDefs, so a race with no VoicePack at all (Monolyn) is a legal new table.</summary>
    private static void BuildFallbackProjection(
        VoicePacksPageState state,
        out int tuningArea,
        out List<FallbackRaceView> races,
        out string selectedRace,
        out List<FallbackEntryView> entries,
        out List<FilterOptionView> soundOptions,
        out List<FilterOptionView> candidateOptions)
    {
        tuningArea = state.TuningArea;
        races = new List<FallbackRaceView>();
        entries = new List<FallbackEntryView>();
        soundOptions = new List<FilterOptionView>();
        candidateOptions = new List<FilterOptionView>();

        BuiltInFallbackTable? table = SqueakFallbackProfileStore.Current;
        if (table != null)
        {
            foreach (FallbackProfile profile in table.Profiles)
            {
                int sounds = 0;
                foreach (KeyValuePair<string, string> entry in profile.SoundKeys)
                {
                    if (!string.IsNullOrEmpty(entry.Value)) sounds++;
                }

                races.Add(new FallbackRaceView(
                    profile.Race.DefName,
                    ResolveRaceLabel(profile.Race.DefName),
                    !SqueakFallbackProfileStore.HasMaintainerSource(profile.Race),
                    sounds));
            }
        }

        races.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));

        string wanted = state.FallbackSelectedRace ?? "";
        selectedRace = "";
        if (races.Count > 0)
        {
            selectedRace = wanted;
            bool holds = false;
            for (int i = 0; i < races.Count; i++)
            {
                if (string.Equals(races[i].DefName, selectedRace, StringComparison.Ordinal)) { holds = true; break; }
            }

            if (!holds) selectedRace = races[0].DefName;
        }

        if (selectedRace.Length > 0)
        {
            RaceKey raceKey = new(selectedRace);
            FallbackProfile? resolved = table == null ? null : table.For(raceKey);
            FallbackProfile? maintainer = SqueakFallbackProfileStore.MaintainerSourceFor(raceKey);
            // PM review (2026-10-07): the OVERRIDE/INHERIT line is FIELD PRESENCE in the player's raw
            // delta, never value equality with the source - an override whose value happens to equal
            // the shipped default is still the player's and survives a future source update exactly
            // like any other override (ConfigCopy H pins the same rule at the store boundary).
            FallbackDelta? playerDelta = SqueakFallbackProfileStore.LoadPlayerDelta(raceKey);
            foreach (string key in UniversalSqueaker.Kernel.BuiltInActionKeys.All)
            {
                string sound = "";
                if (resolved != null)
                {
                    resolved.SoundKeys.TryGetValue(key, out sound);
                    sound ??= "";
                }

                string sourceValue = "";
                bool fromMaintainer = false;
                if (maintainer != null)
                {
                    fromMaintainer = maintainer.SoundKeys.TryGetValue(key, out sourceValue);
                    sourceValue ??= "";
                }

                int viewState;
                string soundLabel = "";
                if (sound.Length == 0)
                {
                    viewState = FallbackEntryView.Unset;
                }
                else
                {
                    SoundDef? def = DefDatabase<SoundDef>.GetNamedSilentFail(sound);
                    SqueakSoundAvailabilityState availability = SqueakSoundAvailabilityCache.PeekState(def);
                    viewState = def == null
                        || availability == SqueakSoundAvailabilityState.Empty
                        || availability == SqueakSoundAvailabilityState.Failed
                        ? FallbackEntryView.NoSound
                        : playerDelta != null && playerDelta.Overrides.ContainsKey(key)
                            ? FallbackEntryView.PlayerOverride
                            : fromMaintainer
                                ? FallbackEntryView.Maintainer
                                : FallbackEntryView.PlayerOverride;
                    soundLabel = def != null ? def.label ?? sound : sound;
                }

                string actionLabel = UniversalSqueaker.Kernel.ActionKey.TryParseBuiltIn(key, out SqueakAction action)
                    ? SqueakLabels.Action(action)
                    : key;
                entries.Add(new FallbackEntryView(key, actionLabel, sound, soundLabel, viewState));
            }
        }

        string soundQuery = (state.FallbackSoundQuery ?? "").Trim();
        // VF1定稿: candidates are the loaded CORE SoundDefs (Rimsage-confirmed: ModContentPack.IsCoreMod).
        // The cap is a DISPLAY cap on the filtered answer, not a reachability limit: the query filters
        // BEFORE the cap, so any legal candidate is reachable by typing its name or defName - no
        // advanced search is added.
        foreach (SoundDef def in DefDatabase<SoundDef>.AllDefs)
        {
            if (def == null || string.IsNullOrEmpty(def.defName)) continue;
            if (def.modContentPack == null || !def.modContentPack.IsCoreMod) continue;
            if (soundQuery.Length > 0
                && !UsChecklistFilter.QueryMatches(soundQuery, def.defName, def.label ?? "")) continue;
            SqueakSoundAvailabilityState availability = SqueakSoundAvailabilityCache.PeekState(def);
            string suffix = availability switch
            {
                SqueakSoundAvailabilityState.Empty => " (!)",
                SqueakSoundAvailabilityState.Failed => " (x)",
                _ => "",
            };
            soundOptions.Add(new FilterOptionView((def.label ?? def.defName) + "  " + def.defName + suffix, def.defName));
            if (soundOptions.Count >= 40) break;
        }

        string raceQuery = (state.FallbackNewRaceQuery ?? "").Trim();
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def == null || def.race == null || def.category != ThingCategory.Pawn) continue;
            bool supported = false;
            for (int i = 0; i < races.Count; i++)
            {
                if (string.Equals(races[i].DefName, def.defName, StringComparison.Ordinal)) { supported = true; break; }
            }

            if (supported) continue;
            if (raceQuery.Length > 0
                && !UsChecklistFilter.QueryMatches(raceQuery, def.defName, def.label ?? "")) continue;
            candidateOptions.Add(new FilterOptionView((def.label ?? def.defName) + "  " + def.defName, def.defName));
            if (candidateOptions.Count >= 20) break;
        }
    }

    /// <summary>VF1 selection/query writes (page state only; they move the layout clock through the
    /// Host funnel like every other display write).</summary>
    public static void SetFallbackSelection(VoicePacksPageState state, string? race, string? entryAction)
    {
        if (state == null) return;
        if (race != null) state.FallbackSelectedRace = race;
        if (entryAction != null) state.FallbackSelectedEntryAction = entryAction;
        state.FallbackStatusKey = "";
    }

    public static void SetFallbackQueries(VoicePacksPageState state, string? soundQuery, string? newRaceQuery)
    {
        if (state == null) return;
        if (soundQuery != null) state.FallbackSoundQuery = soundQuery;
        if (newRaceQuery != null) state.FallbackNewRaceQuery = newRaceQuery;
    }

    public static void SetTuningArea(VoicePacksPageState state, int area)
    {
        if (state == null) return;
        state.TuningArea = area is >= 0 and <= 2 ? area : 0;
    }

    public static void SetTuningSelectedAction(VoicePacksPageState state, string actionKey)
    {
        if (state == null) return;
        state.TuningSelectedAction = actionKey ?? "";
    }

    /// <summary>VF1 entry write (r5): the starting point is the player's REAL field-presence delta
    /// read from the copy - not a diff against the resolved table, which would silently drop other
    /// actions' delete markers and any override whose value happens to equal the shipped default.
    /// Three distinct answers: value = override, "" = explicit no-sound marker, null = restore
    /// INHERITANCE (remove the key; the entry follows future shipped updates again). The save result
    /// is observable; a failed write never reports Saved. The status distinguishes an already-ADMITTED
    /// race (the loaded pawn ThingDef carries a CompProperties_Squeaker - mounted at startup from a
    /// pack/table, or patched by the author - so the resolver refresh reaches it this session) from a
    /// first-time mount (new race: comp attach waits for the next full game start, and the editor
    /// says so). Checking the STORE for support instead would be self-referential: SaveProfile has
    /// just rebuilt that very table, so "saved support" always reads true (PM review 2026-10-07).</summary>
    public static void SetFallbackEntry(UniversalSqueakerSettings settings, VoicePacksPageState state, string actionKey, string? soundDefName)
    {
        if (settings == null || state == null || string.IsNullOrEmpty(actionKey)) return;
        string race = state.FallbackSelectedRace;
        if (race.Length == 0) return;
        if (soundDefName != null && soundDefName.Length > 0
            && DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName) == null) return;

        RaceKey raceKey = new(race);
        SqueakFallbackProfileStore.StoreOutcome outcome;
        {
            FallbackDelta? current = SqueakFallbackProfileStore.LoadPlayerDelta(raceKey);
            Dictionary<string, string> overrides = current == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(current.Overrides, StringComparer.Ordinal);
            if (soundDefName == null) overrides.Remove(actionKey);
            else overrides[actionKey] = soundDefName;
            outcome = SqueakFallbackProfileStore.SaveProfile(raceKey, new FallbackDelta(overrides));
        }

        if (outcome != SqueakFallbackProfileStore.StoreOutcome.Written)
        {
            state.FallbackStatusKey = "US.VF1.Status.SaveFailed";
            return;
        }

        settings.NotifyDiscreteResolverRuntimeChanged();
        ThingDef? raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(race);
        bool admitted = raceDef != null && raceDef.race != null
            && raceDef.comps != null && raceDef.comps.Any(comp => comp is CompProperties_Squeaker);
        state.FallbackStatusKey = admitted
            ? "US.VF1.Status.Saved"
            : "US.VF1.Status.SavedRestart";
    }

    /// <summary>VF1 create-table: an empty player table for a loaded pawn race. Support (and the
    /// comp mount) begins at the NEXT full launch; the status line says exactly that - and a failed
    /// write says failed.</summary>
    public static void CreateFallbackTable(UniversalSqueakerSettings settings, VoicePacksPageState state, string raceDefName)
    {
        if (settings == null || state == null) return;
        ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName ?? "");
        if (def == null || def.race == null) return;
        RaceKey raceKey = new(raceDefName!);
        if (SqueakFallbackProfileStore.Current?.For(raceKey) != null) return;
        if (SqueakFallbackProfileStore.SaveProfile(raceKey, new FallbackDelta(new Dictionary<string, string>()))
            != SqueakFallbackProfileStore.StoreOutcome.Written)
        {
            state.FallbackStatusKey = "US.VF1.Status.SaveFailed";
            return;
        }

        state.FallbackSelectedRace = raceDefName!;
        state.FallbackStatusKey = "US.VF1.Status.CreatedRestart";
    }

    /// <summary>VF1 restore-default: clear the player overrides back to the maintainer data. A
    /// player-only race is refused here (there is no maintainer data to restore to) - delete is
    /// that race's honest path; a failed write is reported as failed.</summary>
    public static void RestoreFallbackDefault(UniversalSqueakerSettings settings, VoicePacksPageState state)
    {
        if (settings == null || state == null) return;
        RaceKey raceKey = new(state.FallbackSelectedRace ?? "");
        if (raceKey.DefName.Length == 0) return;
        SqueakFallbackProfileStore.StoreOutcome outcome = SqueakFallbackProfileStore.RestoreDefault(raceKey);
        state.FallbackStatusKey = outcome switch
        {
            SqueakFallbackProfileStore.StoreOutcome.Written => "US.VF1.Status.Restored",
            SqueakFallbackProfileStore.StoreOutcome.RefusedNoSource => "US.VF1.Status.NoMaintainerData",
            _ => "US.VF1.Status.SaveFailed",
        };
        if (outcome == SqueakFallbackProfileStore.StoreOutcome.Written)
        {
            settings.NotifyDiscreteResolverRuntimeChanged();
        }
    }

    /// <summary>VF1 delete: removes ONLY the player table; if no pack or other table supports the
    /// race the support is withdrawn symmetrically (mount removal at the next full launch). Races
    /// with maintainer data cannot be deleted - the store refuses and the status says so; a failed
    /// file removal is reported as failed, not as deleted. r5: the target is the race the caller
    /// ASKED ABOUT when it opened the confirmation, read from the command payload - never the
    /// mutable selection at the later answer time, so a confirm can not delete a different table.</summary>
    public static void DeleteFallbackTable(UniversalSqueakerSettings settings, VoicePacksPageState state, string raceDefName)
    {
        if (settings == null || state == null) return;
        RaceKey raceKey = new(raceDefName ?? "");
        if (raceKey.DefName.Length == 0) return;
        SqueakFallbackProfileStore.StoreOutcome outcome = SqueakFallbackProfileStore.DeletePlayerTable(raceKey);
        state.FallbackStatusKey = outcome switch
        {
            SqueakFallbackProfileStore.StoreOutcome.Written => "US.VF1.Status.Deleted",
            SqueakFallbackProfileStore.StoreOutcome.RefusedMaintainer => "US.VF1.Status.DeleteRefusedMaintainer",
            _ => "US.VF1.Status.SaveFailed",
        };
        if (outcome == SqueakFallbackProfileStore.StoreOutcome.Written)
        {
            settings.NotifyDiscreteResolverRuntimeChanged();
        }
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
