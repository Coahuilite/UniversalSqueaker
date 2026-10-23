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
/// existing settings bridge (<see cref="GameSettings.SetVoicePackSelection"/> /
/// <see cref="GameSettings.CommitVoicePackMode"/>), so resolver rebuild and
/// QueuePersistence semantics stay where they were.
/// </summary>
public static class VoicePacksPageModel
{
    /// <summary>
    /// UIR-007 / CORRECTIONS-R1 topic 1: the board-facts seam. The thin Verse adapter collects the
    /// PLAIN facts (def enumeration cached, statuses read fresh per call); every row fact, summary
    /// number and navigation intent is then derived by the ordinary-data RaceBoardProjection - no
    /// Verse inside the projection, which is what lets the full state matrix be tested in the
    /// zero-game UiLogic harness with real plain inputs. One static seam, one owner (this model),
    /// the same precedent the text-metrics seam established; harnesses replace the seam wholesale.
    /// </summary>
    public static IUsRaceBoardFactsSource RaceBoardSource = VerseRaceBoardSource.Instance;
    public static VoicePacksViewState BuildView(GameSettings settings, SqueakXenotypeCatalogSnapshot catalog, VoicePacksPageState state)
    {
        if (settings == null) settings = SqueakerMod.Settings ?? new GameSettings();
        if (catalog == null) catalog = SqueakXenotypeCatalog.Current;

        SqueakVoicePackMode mode = NormalizeMode(settings.voicePackMode);
        bool biotech = ModsConfig.BiotechActive;

        List<RaceLayerRowView> races = new();
        foreach (string race in catalog.RaceDefNames)
        {
            if (string.IsNullOrEmpty(race)) continue;
            IReadOnlyList<VoicePackInfo> packs = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, race)
                ?? Array.Empty<VoicePackInfo>();
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

        // US-PACK1: the two domain lists are no longer drawn as browse cards, so their OWN search boxes
        // retired with them - the unified keyword narrows the CARD result instead. The lists stay as the
        // model's internal domain inventory: the state/dropdown narrowing still answers "which domain may
        // the projection fall back to" and feeds the banner's counts, exactly as before. The xenotype
        // inventory still FOLLOWS the browsed race: while a race is the operating domain, only that
        // race's xenotypes (plus race-less global ones) stay in the fallback inventory.
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
                    domain.TargetDefName)
                && (!followRace
                    || string.IsNullOrEmpty(domain.RaceDefName)
                    || string.Equals(domain.RaceDefName, state.SelectedRaceDefName, StringComparison.Ordinal)))
            .ToList();

        // US-PACK1 (§4.2): the ONE result body. Every pack the catalog provides becomes a card whose rows
        // are the (scope, race, xenotype) domains that pack serves; the composed filter (keyword, author,
        // race, xenotype, state) narrows ROWS, and a card survives while at least one row does. The
        // keyword's DOMAIN hit is what auto-expands - the manual expansion set is never touched by a query.
        List<PackCardView> packCards = BuildPackCards(settings, catalog, state);
        bool packNoPacks = CountCataloguedPacks(catalog) == 0;
        bool packResultsEmpty = !packNoPacks && packCards.Count == 0;

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

        IReadOnlyList<BaselinePresetView> baselinePresets = BuildBaselinePresets(state);
        string buildIdentity = SqueakerMod.Instance != null ? SqueakerMod.BuildIdentity() : "US.Footer.Build.Unknown".Translate();
        string saveStatus = SqueakerMod.Instance?.SaveState.ToString() ?? "Unknown";
        bool isDirty = SqueakerMod.Instance?.IsSettingsDirty ?? false;
        // VF1定稿: the fallback editor's projection - supported races (maintainer tables ∪ player
        // tables), the selected race's closed-17 entry rows, and the two query-filtered candidate
        // lists. Computed here, from the live store, so the editor and the routing read ONE answer.
        BuildFallbackProjection(state, out int tuningArea, out List<FallbackRaceView> fallbackRaces,
            out string fallbackRace, out List<FallbackEntryView> fallbackEntries,
            out List<FilterOptionView> fallbackSounds, out List<FilterOptionView> fallbackCandidates);

        // UIR-002: the ALL-loaded-race board, computed AFTER the fallback projection so the board reads
        // the SAME fallback answer the editor and the routing use — one fact, one place. Read-only.
        UsRaceBoardView raceBoard = RaceBoardProjection.Build(
            RaceBoardSource.Collect(settings, fallbackRaces, biotech), mode,
            state.BoardGroup, state.BoardFilter, state.BoardSearch, state.BoardXenoExpanded);

        var eat = settings.ReadEatParameters(tuningRace, tuningXeno);
        var select = settings.ReadSelectParameterOverrides(tuningRace, tuningXeno);
        return new VoicePacksViewState(
            mode, settings.AllowEasterEggSounds, settings.distancePreset,
            settings.scaleFrequencyWithTalking,
            settings.showCameraIndicator,
            settings.devLoggingMode, settings.localizeDebugActions, settings.globalVolumeFactor,
            settings.distanceRange.min, settings.distanceRange.max, biotech, banner, filteredRaces,
            filteredXenotypes, selected, actionScopes, state.TuningLayer, tuningRace, tuningXeno, tuningDomains,
             baselinePresets, buildIdentity, saveStatus, isDirty, authors, state.RaceFilter,
            state.XenotypeFilter, raceFilterOptions, xenotypeFilterOptions, eat.Phase ?? EatPhase.WholeProcess,
            eat.NutritionScope ?? EatNutritionScope.Any, eat.DrugScope ?? EatDrugScope.Any,
            packCards, packResultsEmpty, packNoPacks,
            tuningArea, fallbackRaces, fallbackRace, fallbackEntries, fallbackSounds, fallbackCandidates,
            state.FallbackStatusKey,
            raceBoard, select.SelectRequirePlayerControlled, select.SelectRequireAwake, select.SelectRequireNotDowned);
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

    private static void ExecuteSetRaceFilter(GameSettings settings, VoicePacksPageState state, string raceDefName)
    {
        state.RaceFilter = raceDefName ?? "";
        if (string.IsNullOrEmpty(state.RaceFilter) || string.IsNullOrEmpty(state.XenotypeFilter)) return;
        if (XenotypeFilterExistsForRace(settings, state.XenotypeFilter, state.RaceFilter)) return;
        state.XenotypeFilter = "";
    }

    private static bool XenotypeFilterExistsForRace(GameSettings settings, string xenotypeDefName, string raceDefName)
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
        // UIR-001/005 (WO-US-UI-REFACTOR-20261014): the five workspaces are now Overview / Packs /
        // Tuning / Presets / Advanced. The attenuation editor is the Overview's secondary quick area;
        // the basics+timing bands belong to the rules page; diagnostics, the global restore and the
        // theme picker belong to Advanced. Distance is retired as a workspace (a legacy set-tab
        // payload names nothing and is ignored below, exactly as before for an unknown name).
        if (string.Equals(sectionKey, "diagnostics", StringComparison.Ordinal)
            || string.Equals(sectionKey, "restore-center", StringComparison.Ordinal)
            || string.Equals(sectionKey, "theme-picker", StringComparison.Ordinal))
        {
            return "Advanced";
        }

        if (string.Equals(sectionKey, "scope-tree", StringComparison.Ordinal)
            || string.Equals(sectionKey, "basic-tuning", StringComparison.Ordinal)
            || string.Equals(sectionKey, "timing", StringComparison.Ordinal)) return "Tuning";
        if (string.Equals(sectionKey, "preset-list", StringComparison.Ordinal)) return "Presets";
        if (string.Equals(sectionKey, "filter-bar", StringComparison.Ordinal)
            || string.Equals(sectionKey, "packs-filter", StringComparison.Ordinal)
            || string.Equals(sectionKey, "packs-results", StringComparison.Ordinal)
            || string.Equals(sectionKey, "packs-fallback", StringComparison.Ordinal)
            || string.Equals(sectionKey, "checklist", StringComparison.Ordinal)
            || string.Equals(sectionKey, "fallback-editor", StringComparison.Ordinal))
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
        // UIR-001: the five workspaces. "Distance" is gone; a payload naming it normalises nowhere and
        // the switch declines (a stale writer can no longer land on a page that does not exist).
        if (string.Equals(tab, "Overview", StringComparison.OrdinalIgnoreCase))
            normalized = "Overview";
        else if (string.Equals(tab, "Packs", StringComparison.OrdinalIgnoreCase))
            normalized = "Packs";
        else if (string.Equals(tab, "Tuning", StringComparison.OrdinalIgnoreCase))
            normalized = "Tuning";
        else if (string.Equals(tab, "Presets", StringComparison.OrdinalIgnoreCase))
            normalized = "Presets";
        else if (string.Equals(tab, "Advanced", StringComparison.OrdinalIgnoreCase))
            normalized = "Advanced";
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
            // Advanced opens on the picker: it is the player-facing half of the page (UIR-005/006).
            "Advanced" => "theme-picker",
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
    private static void ApplyActionTuningScope(GameSettings settings, VoicePacksPageState state, string actionKey, BehaviorScope? scope)
    {
        if (string.IsNullOrEmpty(actionKey)) return;
        if (state.TuningLayer == 1 && string.IsNullOrEmpty(state.TuningRaceDefName)) return;
        if (state.TuningLayer == 2 && (string.IsNullOrEmpty(state.TuningRaceDefName) || string.IsNullOrEmpty(state.TuningXenotypeDefName))) return;
        settings.SetActionTuningScope(actionKey, state.TuningRaceDefName, state.TuningXenotypeDefName, scope);
    }







    /// <summary>Typed VoicePack checkbox write inside one domain.</summary>
    private static void ApplyTogglePack(
        GameSettings settings,
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
        GameSettings settings,
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName)
    {
        if (string.IsNullOrEmpty(raceDefName)) return;
        SqueakXenotypeCatalogSnapshot catalog = SqueakXenotypeCatalog.Current;
        SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(scope, raceDefName, targetDefName);
        HashSet<string> domainKeys = new(StringComparer.Ordinal);
        IReadOnlyList<VoicePackInfo> domainPacks = scope == SqueakVoicePackScope.Race
            ? catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, raceDefName) ?? Array.Empty<VoicePackInfo>()
            : (catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Xenotype, targetDefName) ?? Array.Empty<VoicePackInfo>())
                .Where(pack => string.Equals(pack.raceDefName, raceDefName, StringComparison.Ordinal))
                .ToList();
        foreach (VoicePackInfo pack in domainPacks)
            if (pack.TryGetPackKey(out string key)) domainKeys.Add(key);
        List<string> retained = (status.EnabledKeys ?? Array.Empty<string>())
            .Where(key => domainKeys.Contains(key))
            .ToList();
        settings.SetVoicePackSelection(scope, raceDefName, targetDefName, retained);
    }

    /// <summary>S5 调音层域选项：Global 层空；Race 层 = catalog 全 race；Xenotype 层 = (race,xeno) 域联合。
    /// 未选中/失效时自动归一为首个选项（空 catalog → 空域）。</summary>
    private static IReadOnlyList<TuningDomainOptionView> BuildTuningDomains(
        GameSettings settings,
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

    /// <summary>Current catalog + the very same effective configuration fold consumed by ResponseSession.</summary>
    private static IReadOnlyList<ActionScopeRowView> BuildActionScopes(GameSettings settings, int layer, string race, string xeno)
    {
        var rows = new List<ActionScopeRowView>();
        var configuration = settings.CaptureBehaviorConfiguration();
        foreach (var action in SqueakerRuntime.Definitions.Actions)
        {
            var effective = SqueakerRuntime.Explain(configuration, action.Key, race, xeno);
            var value = effective.Read();
            var own = settings.ReadPlayerActionOverride(action.Key, layer == 0 ? "" : race, layer == 2 ? xeno : "");
            string options = "";
            if (value.Phase.HasValue) options += " Eat=" + value.Phase + "/" + value.NutritionScope + "/" + value.DrugScope;
            if (SelectPolicy.HasParameters(value)) options += " Select=" + value.SelectRequirePlayerControlled + "/" + value.SelectRequireAwake + "/" + value.SelectRequireNotDowned;
            options += " Age=" + value.AgeEnabled + " Mood=" + value.MoodEnabled
                + "\n" + string.Join("; ", value.ResponseParameters.Select(x => x.Key + "=" + x.Value))
                + "\n" + string.Join("; ", value.ResponseEnabled.Select(x => x.Key + "=" + x.Value))
                + "\n" + string.Join("; ", value.Participation.Select(x => x.Key + "=" + x.Value));
            string anchor = settings.actionTuning.LastOrDefault(x => x.serviceFamily == "Pawn" && x.actionKey == action.Key
                && x.raceDefName == (layer == 0 ? "" : race) && x.xenotypeDefName == (layer == 2 ? xeno : ""))?.sourcePresetDefName ?? "";
            var reset = ResolveActionPreset(anchor, action.Key, layer == 0 ? "" : race, layer == 2 ? xeno : "");
            if (anchor != "") options += "\n" + "US.Tuning.ResetToPreset".Translate() + ": "
                + (reset.Item2 ? reset.Item3 : "US.Tuning.ResetPresetUnavailable".Translate().ToString());
            string explanation = string.Format(System.Globalization.CultureInfo.CurrentCulture, "US.Response.Details".Translate().ToString(), action.Key, value.Enabled, value.Scope,
                value.HardIntervalTicks?.ToString() ?? "US.Response.PolicyPending".Translate().ToString(), value.Volume, value.Pitch,
                string.Join(", ", effective.Dependencies), options,
                string.Join("; ", effective.Origins.Select(x => x.Key + "=" + x.Value)),
                effective.Paused ? effective.Failure : SqueakerRuntime.PolicyStatus);
            rows.Add(new ActionScopeRowView(action.Key, Kernel.BuiltInActionKeys.Contains(action.Key) ? SqueakLabels.Action(action.Key) : action.Key,
                ActionScopeRules.GroupFor(action.Key), own.Scope ?? action.Scope, action.Key, own.Scope.HasValue,
                value.Enabled == false ? BehaviorScope.Disabled : value.Scope ?? action.Scope,
                hasPresetAnchor: anchor != "", presetResetReady: reset.Item2, resetPresetTarget: reset.Item3, explanation: explanation));
        }
        return rows;
    }


    // The record fold and Ready-gated target projection are executed directly by the Host lane.
    // Only Def lookup is supplied by the caller; settings and persistence remain outside this seam.




    /// <summary>VF1定稿 A1: the action-side twin of <see cref="ResolveMoodPreset"/> - does the
    /// anchor resolve to a Def, and does that Def carry an entry for THIS action at this identity.
    /// The label is the reset TARGET's name, never a provenance claim.</summary>
    private static Tuple<bool, bool, string> ResolveActionPreset(string source, string actionKey, string race, string xeno)
    {
        UsBehaviorPresetDef? preset = string.IsNullOrEmpty(source)
            ? null
            : SqueakGameDefs.BaselineByName(source);
        bool hasEntry = preset != null && GameSettings.TryFindActionBaseline(preset, actionKey, race, xeno, out _);
        return Tuple.Create(preset != null, hasEntry, preset != null ? ResolvePresetLabel(preset) : "");
    }

    /// <summary>Project the tuning-baseline preset Defs into a selectable tree for the import widget.</summary>
    private static IReadOnlyList<BaselinePresetView> BuildBaselinePresets(VoicePacksPageState state)
    {
        List<BaselinePresetView> result = new();
        foreach (UsBehaviorPresetDef preset in SqueakGameDefs.AllBaselines())
        {
            if (preset == null) continue;
            BaselinePresetSelection selection = GetOrCreatePresetSelection(state, preset.defName);

            List<BaselineRaceView> races = new();
            int selectedRaces = 0;
            int selectedXenotypes = 0;
            foreach (PresetRaceEntry race in preset.races ?? new List<PresetRaceEntry>())
            {
                if (race == null || string.IsNullOrWhiteSpace(race.raceDefName)) continue;

                List<BaselineXenotypeView> xenotypes = new();
                foreach (PresetXenotypeEntry xenotype in race.xenotypes ?? new List<PresetXenotypeEntry>())
                {
                    if (xenotype == null || string.IsNullOrWhiteSpace(xenotype.xenotypeDefName)) continue;
                    bool xenoSelected = selection.SelectedXenotypeDomainKeys.Contains(
                        BehaviorPresetImporter.XenotypeDomainKey(race.raceDefName, xenotype.xenotypeDefName));
                    if (xenoSelected) selectedXenotypes++;
                    xenotypes.Add(new BaselineXenotypeView(
                        xenotype.xenotypeDefName,
                        ResolveXenotypeDisplayName(xenotype.xenotypeDefName),
                        xenotype.inheritFromRace,
                        xenoSelected,
                        xenotype.actions.Count(t => t != null && t.ActionId != ""),
                        xenotype.actions.Sum(t => t.Values.PresentKeys().Count()),
                        ResolveXenotypeIcon(xenotype.xenotypeDefName)));
                }

                bool raceSelected = selection.SelectedRaceDefNames.Contains(race.raceDefName);
                if (raceSelected) selectedRaces++;
                races.Add(new BaselineRaceView(
                    race.raceDefName,
                    ResolveRaceLabel(race.raceDefName),
                    raceSelected,
                    race.actions.Count(t => t != null && t.ActionId != ""),
                    race.actions.Sum(t => t.Values.PresentKeys().Count()),
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

    private static string ResolvePresetLabel(UsBehaviorPresetDef preset)
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
        string key = BehaviorPresetImporter.XenotypeDomainKey(raceDefName, xenotypeDefName);
        if (selected) selection.SelectedXenotypeDomainKeys.Add(key);
        else selection.SelectedXenotypeDomainKeys.Remove(key);
    }

    private static void ImportBaselinePreset(GameSettings settings, string presetDefName, VoicePacksPageState state)
    {
        if (string.IsNullOrEmpty(presetDefName)) return;
        UsBehaviorPresetDef? preset = SqueakGameDefs.BaselineByName(presetDefName);
        if (preset == null) return;
        BaselinePresetSelection selection = GetOrCreatePresetSelection(state, presetDefName);
        BehaviorPresetImporter.Selection importSelection = new();
        foreach (string race in selection.SelectedRaceDefNames) importSelection.RaceDefNames.Add(race);
        foreach (string key in selection.SelectedXenotypeDomainKeys) importSelection.XenotypeDomainKeys.Add(key);
        try { settings.ImportBaselinePreset(preset, importSelection); }
        catch (ArgumentException error)
        {
            Log.Warning("usdiag preset=" + preset.defName + " rejected=" + error.Message);
            Messages.Message("US.Preset.Invalid".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }

    private static List<VoicePackDomainView> BuildXenotypeDomains(GameSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        List<XenotypeDomainKey> keys = CollectXenotypeDomains(settings, catalog);
        List<VoicePackDomainView> domains = new();
        foreach (XenotypeDomainKey key in keys)
        {
            IReadOnlyList<VoicePackInfo> allTargetPacks = catalog.GetVoicePackDomainPacks(
                SqueakVoicePackScope.Xenotype, key.TargetDefName) ?? Array.Empty<VoicePackInfo>();
            List<VoicePackInfo> packs = allTargetPacks
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
        // OVT-003: the race filter answers the FULL directory, not just pack-owning races - a
        // no-pack HAR race must be selectable and readable in the filter. Pack display names win
        // where a domain exists (unchanged wording); directory-only races join by identity.
        var byDef = new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyList<UsRaceIdentity> directory = RaceDefinitionCatalog.Current.Races;
        foreach (UsRaceIdentity id in directory)
        {
            if (string.IsNullOrEmpty(id.DefName)) continue;
            byDef[id.DefName] = string.IsNullOrEmpty(id.Label) ? id.DefName : id.Label;
        }

        foreach (RaceLayerRowView race in races)
        {
            if (string.IsNullOrEmpty(race.RaceDefName)) continue;
            byDef[race.RaceDefName] = race.DisplayName;
        }

        var result = new List<FilterOptionView>(byDef.Count);
        foreach (KeyValuePair<string, string> pair in byDef)
        {
            result.Add(new FilterOptionView(pair.Value, pair.Key));
        }

        result.Sort((left, right) =>
        {
            int byLabel = StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
            return byLabel != 0 ? byLabel : StringComparer.Ordinal.Compare(left.Value, right.Value);
        });
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

        // OVT-003: directory xenotypes with NO pack domain are filter options too (a no-pack xeno
        // must read back in the filter after arrival). They carry no race context, so a race filter
        // narrows them out - the same honest rule as the pack domains above.
        if (string.IsNullOrEmpty(raceFilter))
        {
            HashSet<string> seenTargets = new(StringComparer.Ordinal);
            foreach (VoicePackDomainView domain in xenotypes)
            {
                if (!string.IsNullOrEmpty(domain.TargetDefName)) seenTargets.Add(domain.TargetDefName);
            }

            foreach (UsXenotypeIdentity xeno in RaceDefinitionCatalog.Current.Xenotypes)
            {
                if (string.IsNullOrEmpty(xeno.DefName) || !seenTargets.Add(xeno.DefName)) continue;
                result.Add(new FilterOptionView(
                    string.IsNullOrEmpty(xeno.Label) ? xeno.DefName : xeno.Label, xeno.DefName));
            }
        }

        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName));
        return result;
    }

    private static List<XenotypeDomainKey> CollectXenotypeDomains(GameSettings settings, SqueakXenotypeCatalogSnapshot catalog)
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

        foreach (KeyValuePair<string, IReadOnlyList<VoicePackInfo>> pair in catalog.XenotypePacksByDefName)
            foreach (VoicePackInfo pack in pair.Value)
                if (pack != null) Add(pack.raceDefName, pair.Key);

        foreach (VoicePackSelectionRecord record in settings.voicePackSelections ?? new List<VoicePackSelectionRecord>())
            if (record != null && record.scope == SqueakVoicePackScope.Xenotype)
                Add(record.raceDefName, record.xenotypeDefName);

        foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
            if (record != null && record.serviceFamily == "Pawn" && record.IsValidLayer(out int actionLayer) && actionLayer == 2)
                Add(record.raceDefName, record.xenotypeDefName);
        result.Sort((left, right) =>
        {
            int byTarget = StringComparer.Ordinal.Compare(left.TargetDefName, right.TargetDefName);
            return byTarget != 0 ? byTarget : StringComparer.Ordinal.Compare(left.RaceDefName, right.RaceDefName);
        });
        return result;
    }

    private static VoicePackDomainView? ResolveSelectedDomain(
        GameSettings settings,
        SqueakXenotypeCatalogSnapshot catalog,
        VoicePacksPageState state,
        IReadOnlyList<RaceLayerRowView> races,
        IReadOnlyList<VoicePackDomainView> xenotypes)
    {
        races ??= Array.Empty<RaceLayerRowView>();
        xenotypes ??= Array.Empty<VoicePackDomainView>();
        // US-ESC1: the user CANCELLED the operation domain this session - do not re-pick the first row.
        // First entry (never cancelled, or re-selected, or a workspace switch) keeps the auto default.
        if (state.DomainSelectionCanceled) return null;

        // OVT-003 (PM navigation supplement): an EXPLICIT identity - one the overview board or a
        // filter arrival just named - is honoured EXACTLY. It may never fall back to the first
        // domain, and a domain with no packs answers its own honest empty state. The readonly-struct
        // trap is closed by explicit loops: FirstOrDefault over a struct list yields default(...)
        // for "no match", which is NOT null, and the old code then wrote that empty default back
        // onto the state (identity erased).
        if (state.SelectedScope == SqueakVoicePackScope.Xenotype && !string.IsNullOrEmpty(state.SelectedTargetName))
        {
            for (int i = 0; i < xenotypes.Count; i++)
            {
                VoicePackDomainView domain = xenotypes[i];
                if (string.Equals(domain.RaceDefName, state.SelectedRaceDefName, StringComparison.Ordinal)
                    && string.Equals(domain.TargetDefName, state.SelectedTargetName, StringComparison.Ordinal))
                {
                    return domain;
                }
            }

            return BuildEmptyXenotypeDomain(settings, state.SelectedRaceDefName, state.SelectedTargetName);
        }

        if (!string.IsNullOrEmpty(state.SelectedRaceDefName))
        {
            for (int i = 0; i < races.Count; i++)
            {
                if (string.Equals(races[i].RaceDefName, state.SelectedRaceDefName, StringComparison.Ordinal))
                {
                    state.SelectedScope = SqueakVoicePackScope.Race;
                    state.SelectedTargetName = "";
                    return BuildRaceDomain(settings, catalog, races[i].RaceDefName);
                }
            }

            // A directory race with no pack domain (no-pack HAR arrival): the SAME builder the
            // pack races use answers zero candidates + the recorded status - an empty state that
            // still names exactly this race. Identity stays put; nothing is rewritten.
            state.SelectedScope = SqueakVoicePackScope.Race;
            state.SelectedTargetName = "";
            return BuildRaceDomain(settings, catalog, state.SelectedRaceDefName);
        }

        if (state.SelectedScope == SqueakVoicePackScope.Xenotype && xenotypes.Count > 0)
        {
            state.SelectedScope = SqueakVoicePackScope.Xenotype;
            state.SelectedRaceDefName = xenotypes[0].RaceDefName;
            state.SelectedTargetName = xenotypes[0].TargetDefName;
            return xenotypes[0];
        }

        // Nothing explicit yet (first entry / re-selection): the legacy auto default chain.
        if (races.Count > 0)
        {
            state.SelectedScope = SqueakVoicePackScope.Race;
            state.SelectedRaceDefName = races[0].RaceDefName;
            state.SelectedTargetName = "";
            return BuildRaceDomain(settings, catalog, races[0].RaceDefName);
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

    /// <summary>OVT-003: the honest empty state for an explicitly-named xenotype target that owns
    /// no pack domain right now (a directory xenotype with no packs, or a race context whose packs
    /// were removed). Display comes from the published directory label when present; the recorded
    /// selection status still answers, so a saved choice never silently disappears.</summary>
    /// <summary>PM state addendum: the FOUR honest answers for a directory xenotype the settings
    /// layer could not see (its TargetUnavailable fires BEFORE the orphan scan, so saved keys may
    /// ride under it): loaded + saved keys -> SAVED-CHOICE-MISSING (Orphan, the old meaning kept);
    /// loaded + zero keys -> no supply (Available empty); not loaded -> target not loaded
    /// (TargetUnavailable); Biotech off -> Dormant (the settings layer already answers that first,
    /// untouched). The sound canonical set and the records are NOT widened or deleted to prettify
    /// a state - the browse side reads the same records and states the truth.</summary>
    private static SqueakVoicePackDomainState ShownXenoState(
        SqueakVoicePackDomainStatus status, bool loadedInDirectory)
    {
        if (status.State == SqueakVoicePackDomainState.TargetUnavailable && loadedInDirectory)
        {
            return status.EnabledKeys != null && status.EnabledKeys.Count > 0
                ? SqueakVoicePackDomainState.Orphan
                : SqueakVoicePackDomainState.Available;
        }

        return status.State;
    }

    private static VoicePackDomainView BuildEmptyXenotypeDomain(
        GameSettings settings, string raceDefName, string targetDefName)
    {
        SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(
            SqueakVoicePackScope.Xenotype, raceDefName ?? "", targetDefName);
        string display = targetDefName;
        // CO review r1 item 6: the settings layer answers TargetUnavailable from the SOUND catalog
        // (assembled pack targets only). A xenotype that IS in the browse directory but has no
        // packs is "loaded, no supply" - an Available empty, never "target not loaded". Saved-
        // missing (Orphan) and Biotech-off (Dormant) keep their old meanings; the sound canonical
        // set and admission are NOT touched to make a UI answer prettier.
        bool loadedInDirectory = false;
        IReadOnlyList<UsXenotypeIdentity> directory = RaceDefinitionCatalog.Current.Xenotypes;
        for (int i = 0; i < directory.Count; i++)
        {
            if (string.Equals(directory[i].DefName, targetDefName, StringComparison.Ordinal))
            {
                display = directory[i].Label;
                loadedInDirectory = true;
                break;
            }
        }

        return new VoicePackDomainView(
            SqueakVoicePackScope.Xenotype,
            raceDefName ?? "",
            targetDefName,
            display,
            "",
            ShownXenoState(status, loadedInDirectory),
            !ModsConfig.BiotechActive,
            ShownXenoState(status, loadedInDirectory) == SqueakVoicePackDomainState.TargetUnavailable,
            false,
            status.EnabledKeys?.Count ?? 0,
            0,
            status.EnabledKeys?.Count ?? 0,
            status.EnabledKeys ?? Array.Empty<string>(),
            Array.Empty<VoicePackRowView>());
    }

    private static VoicePackDomainView BuildRaceDomain(GameSettings settings, SqueakXenotypeCatalogSnapshot catalog, string raceDefName)
    {
        IReadOnlyList<VoicePackInfo> packs = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, raceDefName)
            ?? Array.Empty<VoicePackInfo>();
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

    private static VoicePackRowView CreateVoicePackRow(VoicePackInfo pack, IReadOnlyList<string> enabledKeys)
    {
        string key = pack.TryGetPackKey(out string packKey) ? packKey : pack.defName;
        string label = string.IsNullOrEmpty(pack.LabelCap) ? pack.defName : pack.LabelCap;
        string modName = pack.modContentPack?.Name ?? pack.modContentPack?.PackageId ?? "—";
        string author = pack.modContentPack?.ModMetaData?.AuthorsString ?? "";
        if (string.IsNullOrEmpty(author)) author = modName;
        int playable = pack.CountPlayableActions();
        string coverage = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "US.Packs.Checklist.PackActions".Translate(),
            playable,
            ActionPresentations.Count);
        string searchText = label + "\n" + pack.defName + "\n" + modName + "\n" + author + "\n" + key;
        bool selected = enabledKeys != null && enabledKeys.Contains(key);
        return new VoicePackRowView(key, label, modName, author, pack.defName, coverage, searchText, selected);
    }

    private static IReadOnlyList<string> CollectAuthors(GameSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        HashSet<string> authors = new(StringComparer.Ordinal);
        foreach (string race in catalog.RaceDefNames)
        {
            if (string.IsNullOrEmpty(race)) continue;
            IReadOnlyList<VoicePackInfo> racePacks = catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, race)
                ?? Array.Empty<VoicePackInfo>();
            foreach (VoicePackInfo pack in racePacks)
            {
                string author = CreateVoicePackRow(pack, Array.Empty<string>()).Author;
                if (!string.IsNullOrEmpty(author)) authors.Add(author);
            }
        }

        foreach (KeyValuePair<string, IReadOnlyList<VoicePackInfo>> pair in catalog.XenotypePacksByDefName)
        {
            foreach (VoicePackInfo pack in pair.Value)
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

    private static int CountOrphanKeys(IReadOnlyList<string>? enabledKeys, IReadOnlyList<VoicePackInfo> packs)
    {
        if (enabledKeys == null || enabledKeys.Count == 0) return 0;
        HashSet<string> domainKeys = new(StringComparer.Ordinal);
        foreach (VoicePackInfo pack in packs)
            if (pack.TryGetPackKey(out string key)) domainKeys.Add(key);
        int orphan = 0;
        foreach (string key in enabledKeys)
            if (!string.IsNullOrEmpty(key) && !domainKeys.Contains(key)) orphan++;
        return orphan;
    }


    /// <summary>
    /// US-PACK1 (§4.2): the pack-card projection - the page's ONE result body. The scan visits every
    /// domain the catalog and the player records produce (race domains in catalog order, then the sorted
    /// xenotype inventory), and every pack that serves a domain contributes a ROW to that pack's CARD.
    /// The composed filter narrows ROWS: the author/race/xenotype dropdowns and the state flags apply to
    /// the row's domain exactly as the old browse lists applied them to their cards, and the keyword
    /// accepts a row when it matches the PACK half (label/DefName/Mod/author/key - the same
    /// <see cref="UsChecklistFilter.QueryMatches"/> substring rule, no second predicate) or the DOMAIN
    /// half (domain display name and defNames). A card survives while one row does; a keyword hit on the
    /// DOMAIN half is what auto-expands the card, while a pack-only hit lists the card collapsed - the
    /// manual expansion set is never written by a query, so clearing the keyword provably restores it.
    /// Enable answers are read per domain identity (the same double-keyed status lookup the settings
    /// layer uses), which is what keeps two rows of one pack independent writes.
    /// </summary>
    private static List<PackCardView> BuildPackCards(
        GameSettings settings, SqueakXenotypeCatalogSnapshot catalog, VoicePacksPageState state)
    {
        // The Verse side COLLECTS, the pure side FILTERS (PackCardProjection): the same split
        // UsChecklistFilter/VoicePacksFilters established, so the harness lane drives the real rule
        // without a second copy of it. Scan order is the card order's first-appearance input: race
        // domains in catalog order, then the sorted xenotype inventory.
        var source = new List<PackCardSourceRow>();

        foreach (string race in catalog.RaceDefNames)
        {
            if (string.IsNullOrEmpty(race)) continue;
            IReadOnlyList<VoicePackInfo> packs =
                catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Race, race) ?? Array.Empty<VoicePackInfo>();
            if (packs.Count == 0) continue;
            SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(SqueakVoicePackScope.Race, race);
            IReadOnlyList<string> enabledKeys = status.EnabledKeys ?? Array.Empty<string>();
            string display = ResolveRaceLabel(race);
            bool orphan = status.State == SqueakVoicePackDomainState.Orphan || CountOrphanKeys(enabledKeys, packs) > 0;
            foreach (VoicePackInfo pack in packs)
            {
                if (pack == null) continue;
                VoicePackRowView identity = CreateVoicePackRow(pack, enabledKeys);
                if (string.IsNullOrEmpty(identity.Key)) continue;
                source.Add(new PackCardSourceRow(
                    identity.Key, identity.Label, identity.DefName, identity.ModName, identity.Author, identity.Coverage,
                    SqueakVoicePackScope.Race, race, "", display,
                    identity.IsSelected, false, false, false, orphan));
            }
        }

        foreach (XenotypeDomainKey domainKey in CollectXenotypeDomains(settings, catalog))
        {
            IReadOnlyList<VoicePackInfo> targetPacks = catalog.GetVoicePackDomainPacks(
                SqueakVoicePackScope.Xenotype, domainKey.TargetDefName) ?? Array.Empty<VoicePackInfo>();
            List<VoicePackInfo> packs = new();
            foreach (VoicePackInfo pack in targetPacks)
                if (pack != null && string.Equals(pack.raceDefName, domainKey.RaceDefName, StringComparison.Ordinal))
                    packs.Add(pack);
            if (packs.Count == 0) continue;
            SqueakVoicePackDomainStatus status = settings.GetVoicePackSelectionStatus(
                SqueakVoicePackScope.Xenotype, domainKey.RaceDefName, domainKey.TargetDefName);
            IReadOnlyList<string> enabledKeys = status.EnabledKeys ?? Array.Empty<string>();
            string display = ResolveXenotypeLabel(catalog, domainKey.TargetDefName);
            bool dormant = !ModsConfig.BiotechActive;
            bool targetUnavailable = ModsConfig.BiotechActive && !catalog.XenotypeByDefName.ContainsKey(domainKey.TargetDefName);
            bool hasConflict = catalog.AmbiguousCanonicalDefNames.Contains(domainKey.TargetDefName);
            bool orphan = status.State == SqueakVoicePackDomainState.Orphan || CountOrphanKeys(enabledKeys, packs) > 0;
            foreach (VoicePackInfo pack in packs)
            {
                if (pack == null) continue;
                VoicePackRowView identity = CreateVoicePackRow(pack, enabledKeys);
                if (string.IsNullOrEmpty(identity.Key)) continue;
                source.Add(new PackCardSourceRow(
                    identity.Key, identity.Label, identity.DefName, identity.ModName, identity.Author, identity.Coverage,
                    SqueakVoicePackScope.Xenotype, domainKey.RaceDefName, domainKey.TargetDefName, display,
                    identity.IsSelected, hasConflict, dormant, targetUnavailable, orphan));
            }
        }

        return PackCardProjection.Build(
            source, state.SearchText, in state.PackFilter, state.RaceFilter, state.XenotypeFilter,
            in state.DomainFilter, state.PackCardsExpanded);
    }

    /// <summary>Distinct pack keys the catalog carries at all - the "no packs here" sentence's denominator.</summary>
    private static int CountCataloguedPacks(SqueakXenotypeCatalogSnapshot catalog)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (VoicePackInfo pack in catalog.RacePacks ?? Array.Empty<VoicePackInfo>())
            if (pack != null && pack.TryGetPackKey(out string k)) keys.Add(k);
        foreach (KeyValuePair<string, IReadOnlyList<VoicePackInfo>> pair in catalog.XenotypePacksByDefName)
            foreach (VoicePackInfo pack in pair.Value)
                if (pack != null && pack.TryGetPackKey(out string k)) keys.Add(k);
        return keys.Count;
    }

    /// <summary>US-PACK1: the player's expand/collapse gesture on a card header (manual set only - the
    /// query's auto-expansion is derived and cannot be toggled into the user's state).</summary>
    public static void TogglePackCard(VoicePacksPageState state, string packKey)
    {
        if (state == null || string.IsNullOrEmpty(packKey)) return;
        if (!state.PackCardsExpanded.Remove(packKey)) state.PackCardsExpanded.Add(packKey);
    }

    /// <summary>US-ESC1 (PACK1 result layer, §4.1; review-1 correction 2): the result layer answers ONLY
    /// while a MANUALLY opened card is VISIBLE in the current projection. A manual expansion the active
    /// author/query conditions filter out is NOT an executable layer - it is the player's own state kept
    /// for when the conditions return - so it can never become an invisible Esc that consumes the key with
    /// nothing on screen changing. The decision reads the production view (including its empty result: zero
    /// visible cards means zero visible manual expansions means the layer declines), never the raw set.</summary>
    public static bool CanCancelPackResults(VoicePacksPageState state, VoicePacksViewState view)
        => view != null && CanCancelPackResults(state, view.PackCards);

    /// <summary>The cards-list form the host and the boundary witness call directly: the decision is a
    /// function of the CURRENTLY VISIBLE cards (the projection already applied every condition), so a
    /// caller that has the projected list need not rebuild the whole view.
    /// ESC1 closure (pm-pack1-collapse probe run-e45eb2e, defect B): the manual expansion must also be
    /// the REASON the card is open. A card that is manual AND auto-expanded (the keyword hits its rows)
    /// stays open when the manual flag collapses - clearing that latent state has no visible effect and
    /// would be an empty return; the manual state is retained so the query clear restores it visibly.</summary>
    public static bool CanCancelPackResults(VoicePacksPageState state, IReadOnlyList<PackCardView> visibleCards)
    {
        if (state == null || visibleCards == null) return false;
        for (int i = 0; i < visibleCards.Count; i++)
            if (visibleCards[i].ManualExpanded && !visibleCards[i].AutoExpanded) return true;
        return false;
    }

    /// <summary>One press, one layer: collapse exactly the VISIBLE manual expansions (the cards the player
    /// opened AND the current conditions still show). Hidden manual keys and the query's auto state are
    /// retained, so changing or clearing a filter restores the player's view. Never touches filters,
    /// selection or any persisted value.</summary>
    public static void CancelPackResults(VoicePacksPageState state, VoicePacksViewState view)
    {
        if (view != null) CancelPackResults(state, view.PackCards);
    }

    /// <summary>The cards-list form: removes from the manual set exactly the manual-only VISIBLE keys
    /// (same predicate as the CanCancel above - a masked or hidden manual state is retained, never
    /// half-collapsed).</summary>
    public static void CancelPackResults(VoicePacksPageState state, IReadOnlyList<PackCardView> visibleCards)
    {
        if (state == null || visibleCards == null) return;
        for (int i = 0; i < visibleCards.Count; i++)
        {
            PackCardView card = visibleCards[i];
            if (card.ManualExpanded && !card.AutoExpanded) state.PackCardsExpanded.Remove(card.Key);
        }
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
        // US-ESC1: switching workspace RE-ESTABLISHES the active branch (§4.1): the cancelled marks are
        // last session's return, not this page's, so each page opens with its accepted initial default
        // (projection auto-select ON) again - and the tuning branch is ACTIVE until a Cancel exits it.
        state.DomainSelectionCanceled = false;
        state.FallbackTableCanceled = false;
        state.TuningContextActive = true;
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
            "packs-filter" => "us/filter-bar",
            "packs-results" => "us/pack-cards",
            "checklist" => "us/voice-pack-checklist",
            // UIR-002/003/004/006: the new workspaces' own help sections.
            "overview-intro" => "us/overview-intro",
            "race-board" => "us/race-board",
            "theme-picker" => "us/theme-picker",
            "packs-fallback" => "us/fallback-editor",
            "fallback-editor" => "us/fallback-editor",
            "restore-center" => "us/reset-center",
            _ => "us/page-title",
        };
    }

    public static void SetTuningLayer(VoicePacksPageState state, int layer)
    {
        if (state == null) return;
        if (layer >= 0 && layer <= 2) { state.TuningLayer = layer; state.TuningContextActive = true; }
    }

    public static void SetTuningDomain(VoicePacksPageState state, string raceDefName, string targetDefName)
    {
        if (state == null) return;
        if (!string.IsNullOrEmpty(raceDefName))
        {
            state.TuningRaceDefName = raceDefName;
            state.TuningXenotypeDefName = targetDefName ?? "";
            state.TuningContextActive = true;
        }
    }

    public static void SelectDomain(VoicePacksPageState state, SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        if (state == null) return;
        state.SelectedScope = scope;
        state.SelectedRaceDefName = raceDefName ?? "";
        state.SelectedTargetName = scope == SqueakVoicePackScope.Xenotype ? targetDefName ?? "" : "";
        // US-ESC1: a fresh selection replaces the cancel - auto-selection is permitted again.
        state.DomainSelectionCanceled = false;
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

    public static void SetRaceFilter(GameSettings settings, VoicePacksPageState state, string raceDefName)
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

    public static void SetActionScope(GameSettings settings, VoicePacksPageState state, string actionKey, BehaviorScope? scope)
    {
        if (state == null || string.IsNullOrEmpty(actionKey)) return;
        state.TuningContextActive = true;
        ApplyActionTuningScope(settings, state, actionKey, scope);
    }



    /// <summary>VF1定稿 A4: action reset-to-preset facade, the mood side's shape: the current-layer
    /// identity's last-wins anchor names the preset; no anchor or no resolvable entry = no-op.</summary>
    public static void ResetActionTuningToPreset(GameSettings settings, VoicePacksPageState state, string actionKey)
    {
        if (settings == null || state == null || string.IsNullOrEmpty(actionKey)) return;
        state.TuningContextActive = true;
        string anchor = "";
        foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
        {
            if (record == null || !string.Equals(record.actionKey, actionKey, StringComparison.Ordinal)) continue;
            if (record.serviceFamily != "Pawn" || record.IsValidLayer(out int recordLayer) == false || recordLayer != state.TuningLayer) continue;
            if (state.TuningLayer >= 1 && !string.Equals(record.raceDefName ?? "", state.TuningRaceDefName ?? "", StringComparison.Ordinal)) continue;
            if (state.TuningLayer == 2 && !string.Equals(record.xenotypeDefName ?? "", state.TuningXenotypeDefName ?? "", StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(record.sourcePresetDefName)) anchor = record.sourcePresetDefName;
        }
        if (anchor.Length == 0) return;
        UsBehaviorPresetDef? preset = SqueakGameDefs.BaselineByName(anchor);
        settings.ResetActionTuningToPreset(actionKey, state.TuningRaceDefName ?? "", state.TuningXenotypeDefName ?? "", preset);
    }






    // Keep the identity refusal independent of the game-only Def lookup, including JIT type resolution.


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

    public static void ImportBaselinePresetSelection(GameSettings settings, string presetDefName, VoicePacksPageState state)
    {
        if (state == null) return;
        ImportBaselinePreset(settings, presetDefName, state);
    }

    public static void ToggleVoicePack(
        GameSettings settings,
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
        GameSettings settings,
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
        // US-ESC1: a cancelled table stays cancelled - the projection must not answer the Esc a second
        // time by re-picking the first race. The list itself (races) is untouched: cancellation is a
        // selection state, never a data change.
        if (races.Count > 0 && !state.FallbackTableCanceled)
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

                string actionLabel = Kernel.BuiltInActionKeys.Contains(key)
                    ? SqueakLabels.Action(key)
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
        state.TuningContextActive = true;
        if (race != null)
        {
            state.FallbackSelectedRace = race;
            // US-ESC1: naming a table again IS the selection; the cancelled mark only survives until one.
            state.FallbackTableCanceled = false;
        }
        if (entryAction != null) state.FallbackSelectedEntryAction = entryAction;
        state.FallbackStatusKey = "";
    }

    public static void SetFallbackQueries(VoicePacksPageState state, string? soundQuery, string? newRaceQuery)
    {
        if (state == null) return;
        if (soundQuery != null) state.FallbackSoundQuery = soundQuery;
        if (newRaceQuery != null) state.FallbackNewRaceQuery = newRaceQuery;
        state.TuningContextActive = true;
    }

    public static void SetTuningArea(VoicePacksPageState state, int area)
    {
        if (state == null) return;
        // UIR-004: the third area retired with the fallback editor's move to the Packs page - an area
        // write naming it (a stale caller, a hand-set value) reposes to the action branch, so the
        // tuning tree can never render an area it no longer owns.
        state.TuningArea = area is >= 0 and <= 1 ? area : 0;
        state.TuningContextActive = true;
    }

    public static void SetTuningSelectedAction(VoicePacksPageState state, string actionKey)
    {
        if (state == null) return;
        state.TuningSelectedAction = actionKey ?? "";
        state.TuningContextActive = true;
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
    public static void SetFallbackEntry(GameSettings settings, VoicePacksPageState state, string actionKey, string? soundDefName)
    {
        if (settings == null || state == null || string.IsNullOrEmpty(actionKey)) return;
        string race = state.FallbackSelectedRace;
        if (race.Length == 0) return;
        state.TuningContextActive = true;
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
    public static void CreateFallbackTable(GameSettings settings, VoicePacksPageState state, string raceDefName)
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
        state.FallbackTableCanceled = false; // US-ESC1: the created table is a fresh selection, not a cancelled one.
        state.TuningContextActive = true;
        state.FallbackStatusKey = "US.VF1.Status.CreatedRestart";
    }

    /// <summary>VF1 restore-default: clear the player overrides back to the maintainer data. A
    /// player-only race is refused here (there is no maintainer data to restore to) - delete is
    /// that race's honest path; a failed write is reported as failed.</summary>
    public static void RestoreFallbackDefault(GameSettings settings, VoicePacksPageState state)
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
    public static void DeleteFallbackTable(GameSettings settings, VoicePacksPageState state, string raceDefName)
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
    // ---------------------------------------------------------------------------------------------
    // US-ESC1: the tree cancel layers (FL-IC2 CancelBind). One press answers ONE layer; executability is
    // the command's own CanExecute answer, so a layer that has nothing left to undo is CLIMBED PAST by
    // the engine instead of consuming the key with an empty gesture, and the settings main layer closes
    // only when every declared layer has declined. Filters, queries and expansion state are never a
    // cancel target (the spec keeps them), and no write below touches settings/persistence at all.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Packs page: can Esc return from the currently shown operation domain? (Result layer:
    /// the card already shows no domain, or the user already cancelled → the layer declines.)
    /// ESC1 closure (pm-pack1-collapse probe, defect A): the selection must also be VISIBLE — a row
    /// of the current filtered result inside an EXPANDED card. A selection kept latent while its card
    /// is collapsed, or hidden by the active conditions, is not an executable layer: consuming the key
    /// there is an invisible Esc with nothing on screen changing. Retaining the hidden selection for
    /// when the conditions return stays allowed; answering the key with it is not.</summary>
    public static bool CanCancelDomainSelection(VoicePacksPageState state, VoicePacksViewState view)
    {
        if (state == null || view == null) return false;
        return !state.DomainSelectionCanceled
            && view.SelectedDomain.HasValue
            && IsSelectedDomainVisible(view);
    }

    /// <summary>True when the selected domain is drawn right now: an EXPANDED card of the projected
    /// result emits a row with exactly this domain identity. The row set is already filtered, so this
    /// reads the same answer the screen shows.</summary>
    public static bool IsSelectedDomainVisible(VoicePacksViewState view)
    {
        if (view == null || !view.SelectedDomain.HasValue) return false;
        VoicePackDomainView d = view.SelectedDomain.Value;
        IReadOnlyList<PackCardView> cards = view.PackCards;
        for (int i = 0; i < cards.Count; i++)
        {
            if (!cards[i].Expanded) continue;
            IReadOnlyList<PackCardDomainRowView> rows = cards[i].Rows;
            for (int r = 0; r < rows.Count; r++)
            {
                if (rows[r].Scope == d.Scope
                    && rows[r].RaceDefName == d.RaceDefName
                    && rows[r].TargetDefName == d.TargetDefName) return true;
            }
        }
        return false;
    }

    /// <summary>Packs return layer: exit the operation domain. The filter conditions, the two domain
    /// searches, the pack search and every enabled state are untouched; the page stays open.</summary>
    public static void CancelDomainSelection(VoicePacksPageState state)
    {
        if (state == null) return;
        state.DomainSelectionCanceled = true;
    }

    /// <summary>The ROW layer of the tuning tree (FL-IC2 CancelBind on the scope-tree element): only the
    /// branch the player can SEE answers here - action area: the selected action's editor row. A row
    /// target selected in another area is invisible here and is NOT a layer of this branch (PM
    /// observation 10); the context return above (observation 1) owns repose. Declining sends the key
    /// one level up the real parent chain.
    /// <para>
    /// UIR-004 (WO-US-UI-REFACTOR-20261014): the fallback editor is no longer a third tuning AREA -
    /// it migrated to the Packs page as its own composite with its own return layer (see
    /// <see cref="CanCancelFallbackTable"/>), so this ladder only ever answers the action branch.
    /// A persisted-in-session area value of 2 can no longer be produced (the tabs write 0/1 only),
    /// and the source's SetTuningArea clamps anything else back to 0.</para>
    /// </summary>
    public static bool CanCancelTuningTarget(VoicePacksPageState state, VoicePacksViewState view)
    {
        if (state == null || view == null) return false;
        return view.TuningArea == 0 && !string.IsNullOrEmpty(state.TuningSelectedAction);
    }

    /// <summary>One press, one step, visible branch only. State-only; never persists, never routes.</summary>
    public static void CancelTuningTarget(VoicePacksPageState state, VoicePacksViewState view)
    {
        if (state == null || view == null) return;
        if (view.TuningArea == 0 && !string.IsNullOrEmpty(state.TuningSelectedAction))
        {
            state.TuningSelectedAction = "";
        }
    }

    /// <summary>UIR-004: the fallback editor's OWN return layer on the Packs page (CancelBind on its
    /// Section). It carries the two visible-branch steps the tuning tree used to own for area 2, in the
    /// same order: one press answers the selected ENTRY, the next releases the selected TABLE; a
    /// cancelled table stays cancelled (the projection must not answer the Esc a second time by
    /// re-picking a race), and a table state the filter masked is retained, never an invisible press.
    /// Declining climbs to the page root like any other layer.</summary>
    public static bool CanCancelFallbackTable(VoicePacksPageState state, VoicePacksViewState view)
    {
        if (state == null || view == null) return false;
        if (!string.IsNullOrEmpty(state.FallbackSelectedEntryAction)) return true;
        return !state.FallbackTableCanceled && !string.IsNullOrEmpty(view.FallbackSelectedRace);
    }

    /// <summary>One press, one step; state only.</summary>
    public static void CancelFallbackTable(VoicePacksPageState state, VoicePacksViewState view)
    {
        if (state == null || view == null) return;
        if (!string.IsNullOrEmpty(state.FallbackSelectedEntryAction))
        {
            state.FallbackSelectedEntryAction = "";
            return;
        }

        if (!state.FallbackTableCanceled && !string.IsNullOrEmpty(view.FallbackSelectedRace))
        {
            state.FallbackTableCanceled = true;
            state.FallbackSelectedRace = "";
        }
    }

    /// <summary>The CONTEXT return layer above the row steps, declared on the container that wraps the
    /// composite (PM observation 1, per §4.1 "当前层域/区域 → 设置主层"): the branch's activity is its own
    /// business state, so even the untouched default Global/Actions page answers one press before the
    /// settings main layer may close. The exit reposes the branch to its initial defaults - it changes NO
    /// settings, selects no domain (the Global layer has none to auto-pick, the BuildTuningDomains
    /// layer-0 branch), and any later tuning write re-arms the context.</summary>
    public static bool CanCancelTuningContext(VoicePacksPageState state)
        => state != null && state.TuningContextActive;

    public static void CancelTuningContext(VoicePacksPageState state)
    {
        if (state == null) return;
        state.TuningContextActive = false;
        state.TuningLayer = 0;
        state.TuningRaceDefName = "";
        state.TuningXenotypeDefName = "";
        state.TuningArea = 0;
        state.TuningSelectedAction = "";
        // UIR-004: the fallback selection state is NOT reposed here any more - the editor lives on the
        // Packs page and owns its state through its own return layer. Clearing it from a tuning-context
        // return would be one page silently rewriting another page's browse state.
    }

    // -----------------------------------------------------------------------------------------
    // UIR-002/003 (WO-US-UI-REFACTOR-20261014 + CORRECTIONS-R1): the board's two exits, now pure.
    // The row facts arrive from RaceBoardProjection (plain inputs only); this region applies the
    // derived navigation intent to the page state and routes the exact quick-enable identity.
    // No def database, no catalog, no settings-status reading happens here any more - the adapter
    // did all of that when the projection was built.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// UIR-003 / CORRECTIONS-R1 topic 2: apply the card's atomic navigation intent to the page
    /// state. The standing workspace-switch contract runs first (SetActiveTab re-establishes the
    /// branch); then the masking conditions clear, the race condition STAYS as the arrival context
    /// (a narrowed result naming this row's race is a truthful context, not a lie about filters),
    /// the precise domain goes where the projection found the supply (race domain, or the first xeno
    /// target for a xeno-only race), and every supplying card expands so the FL-materialized row
    /// identity can be scrolled into view. Returns the destination for the host to arm - a
    /// "pack-card-row#<key>" identity, the static "packs-fallback" section, or "" (arm nothing;
    /// the caller clears any old pending request); it writes NO settings.
    /// </summary>
    public static string ApplyOpenRaceTarget(VoicePacksPageState state, RaceBoardRow row)
    {
        if (state == null || string.IsNullOrEmpty(row.RaceDefName)) return "";
        UsRaceNavigationIntent intent = RaceBoardProjection.DeriveIntent(row);

        SetActiveTab(state, "Packs");
        state.SearchText = "";
        state.PackFilter = default;
        state.XenotypeFilter = "";
        state.DomainFilter = default;
        state.RaceFilter = row.RaceDefName;
        state.SelectedScope = string.Equals(intent.DomainScope, "Xenotype", StringComparison.Ordinal)
            ? SqueakVoicePackScope.Xenotype
            : SqueakVoicePackScope.Race;
        state.SelectedRaceDefName = row.RaceDefName;
        state.SelectedTargetName = intent.DomainTarget;
        for (int i = 0; i < intent.ExpandCardKeys.Count; i++)
        {
            if (!string.IsNullOrEmpty(intent.ExpandCardKeys[i])) state.PackCardsExpanded.Add(intent.ExpandCardKeys[i]);
        }

        return intent.ScrollDestination;
    }

    /// <summary>OVT-003: the xenotype directory row's open exit - the SAME atomic shape as the race
    /// card's (tab + clear what would mask the target + name the target), on the xenotype axis.
    /// The race context is set ONLY when the directory fact carries one (a single race's packs
    /// name this target); a directory-only xenotype arrives with no race claim at all - the game
    /// data model has no XenotypeDef.race and none is invented. Destination is "" (no pack card is
    /// guaranteed): the caller clears any pending scroll, and the narrowed checklist - or its
    /// honest empty with the target named - IS the answer. Writes nothing persisted.</summary>
    public static string ApplyOpenXenotypeTarget(VoicePacksPageState state, XenotypeBoardRow row)
    {
        if (state == null || string.IsNullOrEmpty(row.DefName)) return "";
        SetActiveTab(state, "Packs");
        state.SearchText = "";
        state.PackFilter = default;
        state.DomainFilter = default;
        state.RaceFilter = row.RaceContextDefName;
        state.XenotypeFilter = row.DefName;
        state.SelectedScope = SqueakVoicePackScope.Xenotype;
        state.SelectedRaceDefName = row.RaceContextDefName;
        state.SelectedTargetName = row.DefName;
        return "";
    }

    /// <summary>UIR-003: the quick enable's exact identity - the same per-domain whole-selection
    /// write the Packs row switch uses (<see cref="ToggleVoicePack"/>), never the mode and never a
    /// second write chain. The board offers it only for the single unambiguous target the pure
    /// projection found (TryQuickEnable), and its button names that exact pack (and xeno target).</summary>
    public static void QuickEnableRacePack(
        GameSettings settings,
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName,
        string packKey)
    {
        ToggleVoicePack(settings, scope, raceDefName, targetDefName, packKey, true);
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
