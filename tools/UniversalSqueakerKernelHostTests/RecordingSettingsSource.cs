using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Recording fake of the <see cref="IUsKernelSettingsSource"/> business boundary. Every typed write
/// is captured so the harness can prove that Host bindings route to the business surface without
/// the RimWorld graph; <see cref="BuildView"/> returns a deterministic default projection so the
/// the real widgets can measure/draw with stable content.
/// </summary>
internal sealed class RecordingSettingsSource : IUsKernelSettingsSource
{
    private readonly VoicePacksPageState state = new();

    /// <summary>
    /// When true, <see cref="BuildView"/> returns non-empty dynamic lists (races, xenotype
    /// domains, action scopes, mood rows, baseline presets, filter options, authors) so the
    /// harness exercises the real dynamic-list widget draw paths instead of the empty projections.
    /// </summary>
    public bool RichData;
    public bool RichScaleFrequencyWithTalking = true;
    /// <summary>UIR-002: board rows the fake projection returns (the harness cannot build a real
    /// catalog snapshot - its constructor is internal by design; the rows themselves are the
    /// production value types the real projection emits).</summary>
    public System.Collections.Generic.IReadOnlyList<RaceBoardRow> SeedRaceBoard = System.Array.Empty<RaceBoardRow>();
    public System.Collections.Generic.IReadOnlyList<XenotypeBoardRow> SeedXenoBoard = System.Array.Empty<XenotypeBoardRow>();

    private int CountSeededWithPacks()
    {
        int n = 0;
        foreach (var r in SeedRaceBoard) if (r.RacePackCount + r.XenoPackCount > 0) n++;
        return n;
    }

    private int CountSeededEnabled()
    {
        int n = 0;
        foreach (var r in SeedRaceBoard) if (r.EnabledCount + r.XenoEnabledCount > 0) n++;
        return n;
    }

    /// <summary>
    /// When true the two Packs layer rows carry text that cannot fit a single line at 800px: a race row
    /// title long enough to wrap, and a short xenotype name whose race context makes the composed title
    /// long. The layer-height assertions need this because every other fixture string fits one line, and
    /// a one-line world cannot tell a measured band from a constant one.
    /// </summary>
    public bool WrappingDomainText;


    /// <summary>
    /// V3 instrument (audit item C): when true the tuning setters ALSO update the page state/view the host
    /// reads, so an "after the write" assertion observes the new state instead of the fixture's constructor
    /// constants. Opt-in: default false keeps every earlier lane's input exactly as it was.
    /// <para>Channels mirrored here: layer and domain (both live on the page state) and action scope (the
    /// drawn row set). A mood VALUE is deliberately NOT mirrored: reproducing the model's per-factor layer
    /// merge inside the fake would be a second implementation of the thing under test - the cross-channel
    /// increments are asserted on the write recorders instead, which fire on every write.</para>
    /// </summary>
    public bool MirrorTuningWrites;

    /// <summary>
    /// XG1.1 instrument: when true the fixture yields the production-SHAPED "xenotype layer with no tunable
    /// target" state - layer 2, race "" and xeno "", plus an empty <c>tuning-domains</c> list - while keeping
    /// the production-shaped action-scope and mood rows. Off by default, so no earlier lane's input changes.
    /// See <see cref="ApplyEmptyXenotypeTargetFixture"/> for what this does and does not prove.
    /// </summary>
    public bool EmptyXenotypeTarget;

    /// <summary>
    /// XG1.1 instrument: the rich view's tuning-domain option list. Null keeps the fixture's two-entry
    /// race-level list; a lane that needs a VALID layer-2 target supplies its own (race, label, xenotype)
    /// entries, and the empty-target lane leaves this null and sets <see cref="EmptyXenotypeTarget"/>.
    /// </summary>
    public TuningDomainOptionView[]? TuningDomains;

    /// <summary>
    /// V3 task-18 instrument: per-factor SUPPLYING LAYERS for the four rich mood rows, in row order
    /// (mood-major, factor-minor: pitch, volume, jitter), 0=Global 1=Race 2=Xenotype -1=no layer (default).
    /// Null keeps the fixture's production-shaped mix. This is the input the corrected readout renders.
    /// </summary>
    public ActionScopeRowView[]? TuningActionScopes;
    public EatPhase EatPhase = EatPhase.WholeProcess;
    public EatNutritionScope EatNutritionScope = EatNutritionScope.Any;
    public EatDrugScope EatDrugScope = EatDrugScope.Any;
    public readonly Dictionary<string, string> EatWrites = new();



    /// <summary>
    /// Author strings used by the rich BuildView (the pack/author filter options). The D9 lane
    /// injects a label far wider than the 143px filter column to prove the popup grows and the
    /// trigger ellipsizes instead of clipping.
    /// </summary>
    public string[] Authors = new[] { "AuthorA", "AuthorB" };

    // Basic writes.
    public SqueakVoicePackMode? LastMode;
    public float? LastGlobalVolume;
    public int GlobalVolumeWriteCount, PackFilterWriteCount, RaceFilterWriteCount, SearchWriteCount;
    public string? LastSearchRequest;
    public Func<string, string>? SearchTransform;
    public SqueakDistancePreset? LastDistancePreset;
    public float? LastDistanceRangeStart;
    public float? LastDistanceRangeEnd;
    public bool? LastTalkingScaling;
    public bool? LastCameraIndicator;
    public bool? LastEasterEggs;

    public int? LastMinIntervalTicks;
    public float? LastCooldownMultiplier;
    public SqueakDevLoggingMode? LastDevLoggingMode;
    public bool? LastLocalizeDebugActions;

    // View/navigation writes.
    public string? LastActiveTab;
    /// <summary>Bottom help panel visibility writes; must stay independent of the workspace tab.</summary>
    public bool? LastHelpPanelOpen;
    public string? LastScrollToSection;
    public int? LastTuningLayer;
    public string? LastTuningDomainRace;
    public string? LastTuningDomainTarget;
    public SqueakVoicePackScope? LastSelectedScope;
    public string? LastSelectedRace;
    public string? LastSelectedTarget;
    public SqueakDomainFilterKind? LastDomainFilterKind;
    public bool? LastDomainFilterFlag;
    public string? LastPackFilter;
    public string? LastRaceFilter;
    public string? LastXenotypeFilter;
    public string? LastSearchText;

    // Tuning writes.
    public string? LastActionKey;
    public BehaviorScope? LastActionScope;
    /// <summary>Mood of the last "reset to preset" write, or null when the control never fired.</summary>
    public string? LastBaselinePresetToggle;
    public string? LastBaselineRacePreset;
    public string? LastBaselineRace;
    public bool? LastBaselineRaceSelected;
    public string? LastBaselineXenoPreset;
    public string? LastBaselineXenoRace;
    public string? LastBaselineXeno;
    public bool? LastBaselineXenoSelected;
    public string? LastBaselineImport;

    /// <summary>
    /// The xenotype icon the rich fixture hands over as DATA (R4-B). Null is the default and the case the
    /// card must fall back on; a lane that wants the picture path sets it. It is a plain value - the fixture,
    /// like the production adapter, never loads a resource.
    /// </summary>
    public Texture2D? XenotypeIcon { get; set; }

    // Packs writes.
    public SqueakVoicePackScope? LastPackScope;
    public string? LastPackRace;
    public string? LastPackTarget;
    public string? LastPackKey;
    public bool? LastPackEnabled;
    public SqueakVoicePackScope? LastForgetScope;
    public string? LastForgetRace;
    public string? LastForgetTarget;

    public VoicePacksPageState ViewState => state;

    public string BuildIdentity => BuildIdentityOverride ?? "test-build";

    /// <summary>V4.4 instrument: feeds a LONG technical identity (the dev form Mod.cs:132-143 builds) so the
    /// footer lane can prove the band grows to fit it instead of clipping, in both languages.</summary>
    public string? BuildIdentityOverride;

    /// <summary>V4.2 instrument: the baseline preset's display label (null keeps the built-in short one).</summary>
    public string? PresetLabel;

    public string SaveStatus => SaveStatusOverride;
    public string SaveStatusOverride = "Idle";
    public bool SaveStatusVisible => SaveStatusVisibleOverride;
    public bool SaveStatusVisibleOverride = true;

    public bool IsDirty => IsDirtyOverride;
    public bool IsDirtyOverride;
    /// <summary>
    /// When set, the fake behaves like the production source's view cache: the view is built once
    /// and only rebuilt when this revision changes, counting rebuilds. The D1/D6 display-write
    /// contract lane needs exactly this - a cache-less fake makes "stale projection" unobservable
    /// (the blind spot the 2026-09-05 in-game report exposed).
    /// </summary>
    public Func<int>? RevisionSource;
    public int BuildViewCount;
    private VoicePacksViewState? cachedView;
    private int cachedRevision = -1;

    public VoicePacksViewState BuildView()
    {
        ApplyEmptyXenotypeTargetFixture();
        if (RevisionSource == null)
        {
            return RichData ? BuildRichView() : BuildEmptyView();
        }

        int revision = RevisionSource();
        if (cachedView == null || revision != cachedRevision)
        {
            cachedView = RichData ? BuildRichView() : BuildEmptyView();
            cachedRevision = revision;
            BuildViewCount++;
        }

        return cachedView;
    }

    /// <summary>
    /// US-ESC1 cancel-probe support (D1/D6 lane): rebuild the cached projection from the CURRENT page
    /// state at the CURRENT revision WITHOUT advancing the session clock and WITHOUT counting a rebuild.
    /// The tuning-target cancel gate reads the projected view (visible area), and the cancel probes
    /// pre-set that state directly (never through a bumping write, per the lane's own rule); without
    /// this sync the revision cache would serve the pre-write projection and the gated CanExecute would
    /// decline for a fixture-staleness reason, not a real one. Keeping the clock untouched preserves the
    /// contract under test: ONLY the actual cancel command may move it.
    /// </summary>
    public void ReprojectAtCurrentRevision()
    {
        if (RevisionSource == null) return;   // no cache to sync when the lane does not gate revisions
        ApplyEmptyXenotypeTargetFixture();
        cachedView = RichData ? BuildRichView() : BuildEmptyView();
        cachedRevision = RevisionSource();
    }

    /// <summary>
    /// XG1.1 fixture: the xenotype layer with NO tunable target. The production model reaches this state when
    /// <c>BuildTuningDomains</c> finds no (race, xenotype) tuning domain - it then leaves BOTH identity halves
    /// empty and returns an empty option list. The harness cannot run that model path (it needs the def
    /// database), so this knob reproduces the RESULTING state/view pair instead of faking the model:
    /// layer 2, race "" and xeno "" on the state the widget's bindings read, an empty <c>tuning-domains</c>
    /// list, and the same values on the view it projects. Off by default - every existing lane keeps the
    /// fixture it measured. What it does NOT prove: that <c>BuildTuningDomains</c> itself derives this state
    /// (see XenotypeEmptyTargetLaneTests' header - that half is unverified here by construction).
    /// </summary>
    private void ApplyEmptyXenotypeTargetFixture()
    {
        if (!EmptyXenotypeTarget) return;
        state.TuningLayer = 2;
        state.TuningRaceDefName = "";
        state.TuningXenotypeDefName = "";
    }

    private VoicePacksViewState BuildEmptyView()
    {
        return new VoicePacksViewState(
            SqueakVoicePackMode.Vanilla,
            allowEasterEggs: false,
            SqueakDistancePreset.Balanced,
            scaleFrequencyWithTalking: true,
            showCameraIndicator: false,
            devLoggingMode: SqueakDevLoggingMode.Auto,
            localizeDebugActions: false,
            globalVolumeFactor: 1f,
            distanceRangeMin: 15f,
            distanceRangeMax: 50f,
            biotechActive: false,
            bannerText: "Universal Squeaker — real-schema host regression",
            races: Array.Empty<RaceLayerRowView>(),
            xenotypeDomains: Array.Empty<VoicePackDomainView>(),
            selectedDomain: null,
            actionScopes: Array.Empty<ActionScopeRowView>(),
            tuningLayer: 0,
            tuningRaceDefName: "",
            tuningXenotypeDefName: "",
            tuningDomains: Array.Empty<TuningDomainOptionView>(),
            baselinePresets: Array.Empty<BaselinePresetView>(),
            buildIdentity: BuildIdentity,
            saveStatus: SaveStatus,
            isDirty: false,
            authors: Array.Empty<string>(),
            raceFilter: "",
            xenotypeFilter: "",
            raceFilterOptions: Array.Empty<FilterOptionView>(),
            xenotypeFilterOptions: Array.Empty<FilterOptionView>(),
            eatPhase: EatPhase,
            eatNutritionScope: EatNutritionScope, eatDrugScope: EatDrugScope,
            selectRequirePlayerControlled: SelectRequirePlayerControlled, selectRequireAwake: SelectRequireAwake, selectRequireNotDowned: SelectRequireNotDowned,
            packNoPacks: true,
            raceBoard: UsRaceBoardView.Empty);
    }

    private VoicePacksViewState BuildRichView()
    {
        bool filterSanguophage = string.Equals(state.RaceFilter, "sanguophage", StringComparison.Ordinal);
        var sang = new VoicePackDomainView(
            // The two Packs layers compose their title from the xenotype name plus the race context, so
            // a long race defName is what makes the drawn title wrap while the bare name stays short.
            SqueakVoicePackScope.Xenotype,
            WrappingDomainText ? "a-very-long-race-definition-name-used-only-to-make-the-title-wrap" : "human",
            "sanguophage",
            WrappingDomainText ? "Sanguophage" : "Sanguophage (Human)",
            "test-catalog",
            SqueakVoicePackDomainState.Available,
            isDormant: false,
            isTargetUnavailable: false,
            hasCanonicalConflict: false,
            enabledCount: 1,
            candidateCount: 2,
            orphanCount: 0,
            enabledKeys: new[] { "us.sang" },
            packs: new[]
            {
                new VoicePackRowView("us.sang", "Sanguophage Voice Pack", "TestMod", "AuthorA", "def.sang", "full", "sang", isSelected: true),
                new VoicePackRowView("us.sang2", "Sanguophage Extra Pack", "TestMod2", "AuthorB", "def.sang2", "full", "extra", isSelected: false)
            },
            raceDisplay: null);

        var preset = new BaselinePresetView(
            "us.preset1",
            PresetLabel ?? "Balanced Test Preset",
            "Deterministic harness preset",
            expanded: PresetExpanded,
            races: new[]
            {
                new BaselineRaceView(
                    "human",
                    "Human",
                    selected: true,
                    actionCount: 5,
                    fieldCount: 3,
                    xenotypes: new[]
                    {
                        new BaselineXenotypeView("sanguophage", "Sanguophage", inheritFromRace: false, selected: false, actionCount: 5, fieldCount: 3, image: XenotypeIcon)
                    })
            },
            selectedRaceCount: 1,
            selectedXenotypeCount: 0);

        return new VoicePacksViewState(
            SqueakVoicePackMode.Remix,
            allowEasterEggs: true,
            SqueakDistancePreset.Strong,
            scaleFrequencyWithTalking: RichScaleFrequencyWithTalking,
            showCameraIndicator: true,
            devLoggingMode: SqueakDevLoggingMode.Enabled,
            localizeDebugActions: true,
            globalVolumeFactor: 0.6f,
            distanceRangeMin: 20f,
            distanceRangeMax: 45f,
            biotechActive: true,
            bannerText: "rich harness catalog",
            races: RaceRowsFor(filterSanguophage),
            xenotypeDomains: filterSanguophage ? Array.Empty<VoicePackDomainView>() : new[] { sang },
            // US-ESC1: the fake mirrors the production projection rule - a CANCELLED operation domain
            // projects to null (the guard in VoicePacksPageModel.ResolveSelectedDomain), never a
            // re-picked first row. Everything else the card shows stays intact.
            selectedDomain: state.DomainSelectionCanceled ? null : SelectedForView(sang, filterSanguophage),
            actionScopes: TuningActionScopes ?? new[]
            {
                new ActionScopeRowView(CoreActions.Eat, "Eat", ActionScopeGroup.SystemOrEvent, BehaviorScope.AnyOccurrence, CoreActions.Eat, hasOwnScope: true, effectiveScope: BehaviorScope.AnyOccurrence),
                new ActionScopeRowView(CoreActions.Draft, "Draft", ActionScopeGroup.PlayerTriggered, BehaviorScope.ActiveCommand, CoreActions.Draft, hasOwnScope: false, effectiveScope: BehaviorScope.ActiveCommand)
            },
            // US-ESC1: layer and area FOLLOW the fake's own page state (production builds both straight
            // from state); with the default state this equals the old constants.
            tuningLayer: EmptyXenotypeTarget ? 2 : state.TuningLayer,
            tuningArea: state.TuningArea,
            tuningRaceDefName: EmptyXenotypeTarget ? "" : "human",
            tuningXenotypeDefName: "",
            // US-ESC1: the fallback table answer mirrors production's projection shape - the shown race
            // is the selection, else the first entry, else NONE once the user cancelled the table.
            fallbackRaces: FallbackTableAutoRace.Length > 0
                ? new[] { new FallbackRaceView(FallbackTableAutoRace, FallbackTableAutoRace, false, 3) }
                : Array.Empty<FallbackRaceView>(),
            fallbackSelectedRace: FallbackSelectedRaceForView(),
            // XG1.1: the empty-target state carries NO domain options (that is what makes the target empty in
            // production); the override lets a control-case lane supply a real (race, xenotype) target.
            tuningDomains: EmptyXenotypeTarget
                ? Array.Empty<TuningDomainOptionView>()
                : TuningDomains ?? new[]
                {
                    new TuningDomainOptionView("human", "Human"),
                    new TuningDomainOptionView("testrace", "Test Race")
                },
            baselinePresets: new[] { preset },
            buildIdentity: BuildIdentity,
            saveStatus: SaveStatus,
            isDirty: true,
            authors: Authors,
            raceFilter: state.RaceFilter,
            xenotypeFilter: state.XenotypeFilter,
            raceFilterOptions: new[]
            {
                new FilterOptionView("All", ""),
                new FilterOptionView("Human", "human"),
                new FilterOptionView("Test Race", "testrace"),
                new FilterOptionView("Sanguophage Race", "sanguophage")
            },
            xenotypeFilterOptions: new[] { new FilterOptionView("All", ""), new FilterOptionView("Sanguophage", "sanguophage") },
            eatPhase: EatPhase,
            eatNutritionScope: EatNutritionScope, eatDrugScope: EatDrugScope,
            selectRequirePlayerControlled: SelectRequirePlayerControlled, selectRequireAwake: SelectRequireAwake, selectRequireNotDowned: SelectRequireNotDowned,
            // US-PACK1: the fake mirrors production's delegation exactly - fixture source pairs through
            // the SAME pure PackCardProjection.Build the model calls, reading the live page state's
            // filters and manual expansion set, so a lane drives the real rule end to end.
            packCards: PackCardsForView(sang),
            packResultsEmpty: PackCardsForView(sang).Count == 0,
            packNoPacks: false,
            raceBoard: new UsRaceBoardView(
                SeedRaceBoard, SeedXenoBoard, ViewState.BoardXenoExpanded,
                SeedRaceBoard.Count, SeedXenoBoard.Count,
                ViewState.BoardGroup, ViewState.BoardFilter,
                SeedRaceBoard.Count, CountSeededWithPacks(), CountSeededEnabled()));
    }

    /// <summary>
    /// US-PACK1 fixture pairs: the xenotype domain the card view already carries, PLUS the same packs
    /// under their race domain NOT enabled there - so one card shows two rows with two independent
    /// enable answers, which is exactly the cross-domain identity the non-crosstalk assertion presses.
    /// ChecklistPacks overrides flow through (they replace sang.Packs), so the projection lanes'
    /// injected rows reach the card list too.
    /// </summary>
    private List<PackCardView> PackCardsForView(VoicePackDomainView sang)
    {
        var source = new List<PackCardSourceRow>();
        foreach (VoicePackRowView pack in sang.Packs)
        {
            source.Add(new PackCardSourceRow(
                pack.Key, pack.Label, pack.DefName, pack.ModName, pack.Author, pack.Coverage,
                SqueakVoicePackScope.Xenotype, sang.RaceDefName, sang.TargetDefName, sang.DisplayName,
                IsEnabledLive(SqueakVoicePackScope.Xenotype, sang.RaceDefName, sang.TargetDefName, pack.Key),
                sang.HasCanonicalConflict, sang.IsDormant, sang.IsTargetUnavailable,
                sang.OrphanCount > 0));
            source.Add(new PackCardSourceRow(
                pack.Key, pack.Label, pack.DefName, pack.ModName, pack.Author, pack.Coverage,
                SqueakVoicePackScope.Race, sang.RaceDefName, "", sang.RaceDisplay,
                IsEnabledLive(SqueakVoicePackScope.Race, sang.RaceDefName, "", pack.Key),
                false, false, false, false));
        }

        return PackCardProjection.Build(
            source, state.SearchText, in state.PackFilter, state.RaceFilter, state.XenotypeFilter,
            in state.DomainFilter, state.PackCardsExpanded);
    }

    /// <summary>
    /// The domain the view presents as selected. Opt-in (<see cref="ReflectSelectionInView"/>): with it on,
    /// a recorded RACE selection is projected as a race-scope domain so a lane can watch the enable band's
    /// scope line follow the browse write; otherwise the fixture keeps its built-in xenotype selection.
    /// </summary>
    private VoicePackDomainView? SelectedForView(VoicePackDomainView sang, bool filterSanguophage)
    {
        // US-PACK1: the race-selection reflection this method once offered served the retired browse
        // cards' scope-line parity lane; the projection's answer is now simply the fixture domain,
        // gone when the race filter excludes it.
        return filterSanguophage ? null : sang;
    }

    /// <summary>
    /// Mirrors the production race-filter semantics for the parity lane: selecting a race narrows the
    /// race layer to that row. The view also drops non-matching xenotype domains and clears selection.
    /// </summary>
    private IReadOnlyList<RaceLayerRowView> RaceRowsFor(bool filterSanguophage)
    {
        if (filterSanguophage)
        {
            return new[]
            {
                new RaceLayerRowView("sanguophage", "Sanguophage Race", 1, 1, SqueakVoicePackDomainState.Available)
            };
        }

        return new[]
        {
            // Wrapping mode: the row title now carries the state too (the detail line keeps the counts), so a
            // title long enough to need a second line is what must grow the card.
            // Overwide mode: a domain title wider than the popup's viewport cap, which is what the
            // popup-overflow-identity lane needs - the author dropdown is no longer a valid source for it,
            // because the consumer now truncates author labels to half the window (F5, 2026-09-14).
            new RaceLayerRowView("human", WrappingDomainText ? "Human (a row title long enough that no single line can hold it)" : "Human", WrappingDomainText ? int.MaxValue : 2, WrappingDomainText ? int.MaxValue - 1 : 3, WrappingDomainText ? SqueakVoicePackDomainState.TargetUnavailable : SqueakVoicePackDomainState.Available),
            new RaceLayerRowView("testrace", WrappingDomainText ? "Test Race (a row title long enough that no single line can hold it)" : "Test Race", WrappingDomainText ? int.MaxValue : 1, WrappingDomainText ? int.MaxValue - 1 : 2, WrappingDomainText ? SqueakVoicePackDomainState.TargetUnavailable : SqueakVoicePackDomainState.Available),
            new RaceLayerRowView("sanguophage", "Sanguophage Race", 1, 1, SqueakVoicePackDomainState.Available)
        };
    }

    public string SectionHelpRef(string sectionKey)
    {
        return VoicePacksPageModel.SectionHelpRefOf(sectionKey);
    }

    public void SetMode(SqueakVoicePackMode mode) => LastMode = mode;

    public void SetGlobalVolume(float value) { GlobalVolumeWriteCount++; LastGlobalVolume = value; }

    public void SetDistancePreset(SqueakDistancePreset preset) => LastDistancePreset = preset;

    public void SetDistanceRange(float start, float end)
    {
        LastDistanceRangeStart = start;
        LastDistanceRangeEnd = end;
    }



    public string ResponseExplanation { get; set; } = "Fixture response details: explicit current input.";
    public void SetTalkingScaling(bool value) => LastTalkingScaling = value;
    public void SetCameraIndicator(bool value) => LastCameraIndicator = value;

    public void SetEasterEggs(bool value) => LastEasterEggs = value;

    public readonly Dictionary<string, string> SelectWrites = new();
    public bool? SelectRequirePlayerControlled, SelectRequireAwake, SelectRequireNotDowned;
    public void SetSelectParameter(string key, string value)
    {
        SelectWrites[key] = value;
        bool? parsed = value == "" ? null : bool.Parse(value);
        if (key == "playerControlled") SelectRequirePlayerControlled = parsed;
        if (key == "awake") SelectRequireAwake = parsed;
        if (key == "notDowned") SelectRequireNotDowned = parsed;
    }
    public void SetEatParameter(string key, string value)
    {
        EatWrites[key] = value;
        if (key == "phase") EatPhase = value == "" ? EatPhase.WholeProcess : (EatPhase)Enum.Parse(typeof(EatPhase), value);
        if (key == "nutritionScope") EatNutritionScope = value == "" ? EatNutritionScope.Any : (EatNutritionScope)Enum.Parse(typeof(EatNutritionScope), value);
        if (key == "drugScope") EatDrugScope = value == "" ? EatDrugScope.Any : (EatDrugScope)Enum.Parse(typeof(EatDrugScope), value);
    }



    public void SetGlobalMinIntervalTicks(int ticks) => LastMinIntervalTicks = ticks;

    public void SetGlobalCooldownMultiplier(float value) => LastCooldownMultiplier = value;

    public void SetDevLoggingMode(SqueakDevLoggingMode mode) => LastDevLoggingMode = mode;

    public void SetLocalizeDebugActions(bool value) => LastLocalizeDebugActions = value;

    // === Developer layout diagnosis (R3-B) =====================================================
    // The fake carries the same three commands and the same status surface as the production source, and it
    // routes them through the SAME per-host helper the production path uses - so a lane that drives a real
    // bound command exercises the real per-host separation rather than a fake that agrees with itself.

    /// <summary>The host this fake's page draws into; attached explicitly by a lane that needs per-host
    /// behaviour (the production factory attaches the real one).</summary>
    internal UiHost? DiagnosisHost { get; private set; }

    /// <summary>
    /// The production report/status surface for this fixture's host (RPT1). The SENTENCE and the report state
    /// machine have exactly one author, so the fixture forwards this one member to a real
    /// <see cref="UsKernelSettingsSource"/> on the same host instead of carrying a copy that can drift from
    /// the production wording and from the audit's own facts. Created on <see cref="AttachHost"/>, i.e. only
    /// for lanes that really drive this fixture as a window would.
    /// </summary>
    private UsKernelSettingsSource? reportMirror;

    /// <summary>Records the host, mirroring <c>UsKernelSettingsSource.AttachHost</c>.</summary>
    public void AttachHost(UiHost host)
    {
        DiagnosisHost = host;
        reportMirror = new UsKernelSettingsSource(new GameSettings());
        reportMirror.AttachHost(host);
    }

    public bool LayoutCaptureOn { get; private set; }

    public bool LayoutOutlineOn { get; private set; }

    public string LayoutDiagnosisStatus
    {
        get
        {
            if (DiagnosisHost == null) return "US.Diagnostics.Geometry.NoScope";
            switch (UsTextFitAudit.GetDevGeometryStatus(DiagnosisHost))
            {
                case UsTextFitAudit.DevGeometryStatus.Unavailable: return "US.Diagnostics.Geometry.Unavailable";
                case UsTextFitAudit.DevGeometryStatus.Overlay: return "US.Diagnostics.Geometry.Overlay";
                case UsTextFitAudit.DevGeometryStatus.Active: return "US.Diagnostics.Geometry.Active";
                case UsTextFitAudit.DevGeometryStatus.OutlineOnly: return "US.Diagnostics.Geometry.OutlineOnly";
                case UsTextFitAudit.DevGeometryStatus.ScopeMissing: return "US.Diagnostics.Geometry.NoScope";
                default: return "US.Diagnostics.Geometry.Off";
            }
        }
    }

    public void SetLayoutCapture(bool on)
    {
        reportMirror?.SetLayoutCapture(on);
        bool honoured = reportMirror != null
            ? reportMirror.LayoutCaptureOn == on
            : UsTextFitAudit.SetGeometryCapture(DiagnosisHost, on);
        LayoutCaptureOn = honoured && on;
        if (!honoured && on) LayoutOutlineOn = false;
        // Keep the production mirror's sentence in step with the switch this fixture really threw: RPT1.2's
        // whole point is that the line follows the INSTRUMENT, so a fixture that left a stale "capture is off"
        // sentence behind after the switch moved would assert the wrong product behaviour.
    }

    public void SetLayoutOutline(bool on)
    {
        bool honoured = UsTextFitAudit.SetGeometryOverlay(DiagnosisHost, on);
        LayoutOutlineOn = honoured && on;
    }

    /// <summary>Counts report requests, so a lane can assert "one request, one report" without the log.</summary>
    public int LayoutReportRequests { get; private set; }

    /// <summary>
    /// Whether a report request is outstanding. Mirrors the production source's pending flag: a request made
    /// while capture is off or the instrument is unavailable is REFUSED, so this stays false and no later
    /// enable can satisfy a stale click (R3-B fix 7).
    /// </summary>
    internal bool LayoutReportPending { get; private set; }

    /// <summary>
    /// The structured rendering of the last explicit report, from the SAME capture as its text (R3-B fix 5).
    /// Mirrors the production source's accessor, which is internal for the API-tier reason recorded there.
    /// </summary>
    internal UiDevGeometrySnapshot? LayoutReportSnapshot
    {
        get
        {
            if (DiagnosisHost == null) return null;
            // The production audit is in a separate assembly without friend access.
            var find = typeof(UsTextFitAudit).GetMethod("FindOpenScope",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing audit scope accessor.");
            var report = typeof(UsTextFitAudit).GetProperty("LatestGeometryReport",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing retained report accessor.");
            object? scope = find.Invoke(null, new object[] { DiagnosisHost });
            return scope == null ? null : (UiDevGeometrySnapshot?)report.GetValue(scope);
        }
    }

    /// <summary>DT1: the fake records the open request; the real window seam needs Verse.Find.</summary>
    public int DeveloperPanelOpenCalls { get; private set; }

    public void OpenDeveloperPanel() => DeveloperPanelOpenCalls++;

    public void RequestLayoutReport()
    {
        reportMirror?.RequestLayoutReport();
        LayoutReportPending = reportMirror != null
            ? (bool)(typeof(UsKernelSettingsSource).GetField("layoutReportPending",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing production pending flag."))
                .GetValue(reportMirror)!
            : UsTextFitAudit.RequestGeometryReport(DiagnosisHost);
        if (LayoutReportPending) LayoutReportRequests++;
        // Same click, same audit facts, production sentence: the mirror records the outcome the page prints.
    }

    /// <summary>
    /// The sentence the page prints immediately below the Report button (RPT1.1). Forwarded to the production
    /// source so a lane that drives this fixture reads the production wording and the production state
    /// machine; before a host is attached (the full-page sweep fixtures) it reports the same "no diagnosis
    /// scope" reason the production source reports for an unattached host.
    /// </summary>
    public string LayoutReportStatus
    {
        get
        {
            if (reportMirror != null) return reportMirror.LayoutReportStatus;
            return "US.Diagnostics.Geometry.Report.NoScope".Translate();
        }
    }

    /// <summary>
    /// The settings window's own one-shot consumer (<c>BeforeDraw</c>), mirrored so a lane can drive the
    /// fixture through the report lifecycle. Not on the interface: production's consumer is the window's
    /// frame call, not a business-boundary member.
    /// </summary>
    internal int ConsumeLayoutReportRequest()
    {
        int pass = reportMirror?.ConsumeLayoutReportRequest() ?? -1;
        if (pass >= 0) LayoutReportPending = false;
        return pass;
    }

    public void SetActiveTab(string tab)
    {
        LastActiveTab = tab;
        // Mirror the production facade: the engine Tab gate reads state.ActiveTab, so the fake
        string normalized;
        if (string.Equals(tab, "Overview", StringComparison.OrdinalIgnoreCase))
            normalized = "Overview";
        else if (string.Equals(tab, "Advanced", StringComparison.OrdinalIgnoreCase))
            normalized = "Advanced";
        else if (string.Equals(tab, "Packs", StringComparison.OrdinalIgnoreCase))
            normalized = "Packs";
        else if (string.Equals(tab, "Tuning", StringComparison.OrdinalIgnoreCase))
            normalized = "Tuning";
        else if (string.Equals(tab, "Presets", StringComparison.OrdinalIgnoreCase))
            normalized = "Presets";
        else
            return;

        // Mirror VoicePacksPageModel.ApplyActiveTab exactly: a real workspace switch also moves the
        // active section to that workspace's primary section, and help-section-ref resolves through
        // it. Without this the fake would pin help-section-ref to "us/mode-row" forever, so no lane
        // could observe the help topic following the workspace while the drawer stays open.
        if (!string.Equals(state.ActiveTab, normalized, StringComparison.Ordinal))
        {
            // US-ESC1: a workspace switch re-establishes the active branch (mirrors production
            // VoicePacksPageModel.SetActiveTab): last page's return marks never carry over.
            state.DomainSelectionCanceled = false;
            state.FallbackTableCanceled = false;
            state.ActiveTab = normalized;
            state.ActiveSectionKey = normalized switch
            {
                "Advanced" => "theme-picker",
                "Packs" => "filter-bar",
                "Tuning" => "scope-tree",
                "Presets" => "preset-list",
                _ => "mode-row"
            };
        }
    }

    public void ScrollToSection(string sectionKey) => LastScrollToSection = sectionKey;

    public void SetHelpPanelOpen(bool open)
    {
        LastHelpPanelOpen = open;
        // Mirror the production source: the engine Tab gate reads state.ActiveTab, and the help-open
        // binding reads ViewState.HelpPanelOpen, so the fake must answer the read-back too.
        state.HelpPanelOpen = open;
    }

    // US-ESC1: the cancel surface routes through the SAME production statics the real source calls, so a
    // lane that drives the bindings exercises the real ladder-step order and the real veto conditions.
    public int CancelDomainSelectionCount;
    public int CancelTuningTargetCount;

    public bool CanCancelDomainSelection()
        => VoicePacksPageModel.CanCancelDomainSelection(state, BuildView());

    public void CancelDomainSelection()
    {
        CancelDomainSelectionCount++;
        VoicePacksPageModel.CancelDomainSelection(state);
    }

    public bool CanCancelTuningTarget()
        => VoicePacksPageModel.CanCancelTuningTarget(state, BuildView());

    public void CancelTuningTarget()
    {
        CancelTuningTargetCount++;
        VoicePacksPageModel.CancelTuningTarget(state, BuildView());
    }

    /// <summary>US-ESC1 fixture: the race a projection would show when nothing is selected yet (the
    /// auto first entry); empty keeps today's no-table answer for every other lane, and the view's
    /// fallback list follows it. The doubles do not load real SoundDefs.</summary>
    public string FallbackTableAutoRace = "";

    private string FallbackSelectedRaceForView()
    {
        if (state.FallbackTableCanceled) return "";
        return state.FallbackSelectedRace.Length > 0 ? state.FallbackSelectedRace : FallbackTableAutoRace;
    }

    public void SetTuningLayer(int layer)
    {
        LastTuningLayer = layer;
        // Route through the SAME production static the real source calls (review1: the re-arm of the
        // tuning CONTEXT is part of the write's meaning, not an extra the fake may forget). The
        // MirrorTuningWrites gate keeps lanes that never entered the branch seeing stable state.
        if (MirrorTuningWrites) VoicePacksPageModel.SetTuningLayer(state, layer);
    }

    public void SetTuningDomain(string raceDefName, string targetDefName)
    {
        LastTuningDomainRace = raceDefName;
        LastTuningDomainTarget = targetDefName;
        if (MirrorTuningWrites) VoicePacksPageModel.SetTuningDomain(state, raceDefName, targetDefName);
    }

    public void SelectDomain(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        LastSelectedScope = scope;
        LastSelectedRace = raceDefName;
        LastSelectedTarget = targetDefName;
        // US-ESC1: mirror the production rule - a fresh selection replaces the cancel mark.
        state.DomainSelectionCanceled = false;
    }

    public void SetDomainFilter(SqueakDomainFilterKind kind, bool flag)
    {
        LastDomainFilterKind = kind;
        LastDomainFilterFlag = flag;
    }

    public void SetPackFilter(string author)
    {
        PackFilterWriteCount++;
        LastPackFilter = author;
        VoicePacksPageModel.SetPackFilter(state, author);
    }

    public void SetRaceFilter(string raceDefName) { RaceFilterWriteCount++; LastRaceFilter = raceDefName; }

    public void SetXenotypeFilter(string xenotypeDefName) => LastXenotypeFilter = xenotypeDefName;

    public void SetSearchText(string text)
    {
        SearchWriteCount++; LastSearchRequest = text;
        if (SearchTransform != null) text = SearchTransform(text);
        // Mirror the production facade exactly: UsKernelSettingsSource.SetSearchText routes through
        // VoicePacksPageModel.SetSearchText, which writes ViewState.SearchText - the very field the
        // "search-text" binding READS. A fake that only recorded the write would leave the read-back
        // stale, and the projection lane's whole point is that a search write and the list it produces
        // are the same frame's answer.
        LastSearchText = text;
        state.SearchText = text ?? "";
    }
    // US-PACK1: the card gesture and the result-layer return route through the SAME production
    // statics the real source calls, so a lane driving the bindings exercises the real manual-set
    // rule and the real veto condition. Counts let a lane assert the write happened at the facade.
    public int TogglePackCardCount;
    public int CancelPackResultsCount;

    public void TogglePackCard(string packKey)
    {
        TogglePackCardCount++;
        VoicePacksPageModel.TogglePackCard(state, packKey);
    }

    public bool CanCancelPackResults()
        => VoicePacksPageModel.CanCancelPackResults(state, BuildView());

    public void CancelPackResults()
    {
        CancelPackResultsCount++;
        VoicePacksPageModel.CancelPackResults(state, BuildView());
    }
    // US-RESET1 UI integration: the fake mirrors the production facade's DECISIONS (layer-identity
    // guard, draft cleanup through the attached host) and records the calls; the real publish/
    // persistence funnel counts stay the dedicated reset backend harness's boundary (120 checks).
    // Lanes inject the outcome (default Applied) so the Applied-only bump and the NoChange/Rejected
    // silence are each drivable.
    public int ResetAllCount;
    public int ResetDistanceCount;
    public int ResetNormalAreaCount;
    public int ResetActionRowCount;
    public int ResetActionAreaCount;
    public int ResetMoodAreaCount;
    public SqueakResetOutcome NextResetOutcome = SqueakResetOutcome.Applied;
    public SqueakNormalResetArea? LastNormalResetArea;
    public string? LastResetActionKey;

    public bool CanResetTuningArea() => state.TuningLayer switch
    {
        0 => true,
        1 => state.TuningRaceDefName.Length > 0,
        2 => state.TuningRaceDefName.Length > 0 && state.TuningXenotypeDefName.Length > 0,
        _ => false,
    };

    public SqueakResetOutcome ResetAllSettings()
    {
        ResetAllCount++;
        return NextResetOutcome;
    }

    public SqueakResetOutcome ResetDistanceDefaults()
    {
        ResetDistanceCount++;
        return NextResetOutcome;
    }

    public SqueakResetOutcome ResetNormalArea(SqueakNormalResetArea area)
    {
        ResetNormalAreaCount++;
        LastNormalResetArea = area;
        return NextResetOutcome;
    }

    public SqueakResetOutcome ResetActionTuningRow(string actionKey)
    {
        ResetActionRowCount++;
        LastResetActionKey = actionKey;
        return CanResetTuningArea() ? NextResetOutcome : SqueakResetOutcome.Rejected;
    }

    public SqueakResetOutcome ResetActionTuningArea()
    {
        ResetActionAreaCount++;
        return CanResetTuningArea() ? NextResetOutcome : SqueakResetOutcome.Rejected;
    }

    public SqueakResetOutcome ResetMoodTuningArea()
    {
        ResetMoodAreaCount++;
        return CanResetTuningArea() ? NextResetOutcome : SqueakResetOutcome.Rejected;
    }

    /// <summary>review1: the fake mirrors the production split - the restore call itself never
    /// touches an edit; only the host command's scoped EndAffectedEdit (after Applied) can, and it
    /// walks the SAME static the production facade calls, on the host this fake attached.</summary>
    public void EndAffectedEdit(Func<string, bool> editIsAffected)
        => UsKernelSettingsHost.EndAffectedEdit(DiagnosisHost, editIsAffected);
    // No SetHelpHover / BeginHelpHoverFrame on this fake: since FL P3 the hover claim is UiSession
    // state (ClaimHover/HoverClaim), not a business write, so the end-to-end lanes read it off the
    // host's session. SetHelpSelection stays retired with the D2 index-list cut.

    public void SetActionScope(string actionKey, BehaviorScope? scope)
    {
        LastActionKey = actionKey;
        LastActionScope = scope;
    }

    // VF1定稿 A2/A4: the multiplier writes are recorded; lanes that need the view to answer set
    // TuningActionScopes explicitly (the fake's view rows are fixture data by design).
    public string? LastActionTuningKey { get; private set; }
    public bool LastActionTuningIntervalField { get; private set; }
    public float? LastActionTuningValue { get; private set; }
    public string? LastActionPresetResetKey { get; private set; }

    public void SetActionTuning(string actionKey, bool intervalField, float? value)
    {
        LastActionTuningKey = actionKey;
        LastActionTuningIntervalField = intervalField;
        LastActionTuningValue = value;
    }

    public void ResetActionToPreset(string actionKey)
    {
        LastActionPresetResetKey = actionKey;
    }

    // VF1定稿: the fake mirrors the production facade exactly (page state + store-free commands);
    // the table commands are recorded, because the fake's view rows are fixture data by design.
    public int LastTuningArea { get; set; } = -1;
    public string? LastTuningSelectedAction { get; private set; }
    public string? LastFallbackRace { get; private set; }
    public string? LastFallbackEntryAction { get; private set; }
    public string? LastFallbackEntrySound { get; private set; }
    public string? LastFallbackCreatedRace { get; private set; }
    public int FallbackRestoreCalls { get; private set; }
    public int FallbackDeleteCalls { get; private set; }

    public void SetTuningArea(int area) { LastTuningArea = area; VoicePacksPageModel.SetTuningArea(state, area); }
    public void SetTuningSelectedAction(string actionKey) { LastTuningSelectedAction = actionKey; VoicePacksPageModel.SetTuningSelectedAction(state, actionKey); }
    public void SetFallbackSelection(string? race, string? entryAction)
    {
        LastFallbackRace = race;
        LastFallbackEntryAction = entryAction;
        VoicePacksPageModel.SetFallbackSelection(state, race, entryAction);
    }

    public void SetFallbackQueries(string? soundQuery, string? newRaceQuery)
        => VoicePacksPageModel.SetFallbackQueries(state, soundQuery, newRaceQuery);

    public void SetFallbackEntry(string actionKey, string? soundDefName)
    {
        LastFallbackEntryAction = actionKey;
        LastFallbackEntrySound = soundDefName;
    }

    public void CreateFallbackTable(string raceDefName) { LastFallbackCreatedRace = raceDefName; }
    public void RestoreFallbackDefault() { FallbackRestoreCalls++; }
    public void DeleteFallbackTable(string raceDefName) { LastFallbackDeletedRace = raceDefName; FallbackDeleteCalls++; }

    /// <summary>r5: the delete command carries the race the confirmation ASKED ABOUT; the fake
    /// records it so a lane can prove the payload identity survives to the boundary.</summary>
    public string? LastFallbackDeletedRace { get; private set; }





    public void ToggleBaselinePreset(string presetDefName)
    {
        LastBaselinePresetToggle = presetDefName;
        // R4-B: the rich fixture's preset starts expanded, and the card's only source of that answer is the
        // model. A lane that needs to observe "collapsing hides the descendants" flips it through the real
        // write, so the assert measures the card rather than a field on the fake.
        if (PresetToggleFlipsExpandedState) PresetExpanded = !PresetExpanded;
    }

    /// <summary>When true, <see cref="ToggleBaselinePreset"/> flips <see cref="PresetExpanded"/>.</summary>
    public bool PresetToggleFlipsExpandedState { get; set; }

    /// <summary>The rich fixture preset's expanded answer, as the next view build will project it.</summary>
    public bool PresetExpanded { get; set; } = true;

    public void ToggleBaselineRace(string presetDefName, string raceDefName, bool selected)
    {
        LastBaselineRacePreset = presetDefName;
        LastBaselineRace = raceDefName;
        LastBaselineRaceSelected = selected;
    }

    public void ToggleBaselineXenotype(string presetDefName, string raceDefName, string xenotypeDefName, bool selected)
    {
        LastBaselineXenoPreset = presetDefName;
        LastBaselineXenoRace = raceDefName;
        LastBaselineXeno = xenotypeDefName;
        LastBaselineXenoSelected = selected;
    }

    public void ImportBaselinePreset(string presetDefName) => LastBaselineImport = presetDefName;

    public void ToggleVoicePack(SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey, bool enabled)
    {
        LastPackScope = scope;
        LastPackRace = raceDefName;
        LastPackTarget = targetDefName;
        LastPackKey = packKey;
        LastPackEnabled = enabled;
        // US-PACK1: the fake mirrors the settings store's per-domain identity (the same double-keyed
        // whole-selection replacement the real SetVoicePackSelection performs), so a card lane can read
        // a switch back and prove two rows of one pack never address each other.
        if (enabled) SelectionEnabled(DomainKey(scope, raceDefName, targetDefName)).Add(packKey);
        else SelectionEnabled(DomainKey(scope, raceDefName, targetDefName)).Remove(packKey);
    }

    private static string DomainKey(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
        => scope + "|" + (raceDefName ?? "") + "|" + (targetDefName ?? "");

    private readonly Dictionary<string, HashSet<string>> selectionStore = new(StringComparer.Ordinal);

    private HashSet<string> SelectionEnabled(string domainKey)
    {
        if (!selectionStore.TryGetValue(domainKey, out HashSet<string>? keys))
        {
            keys = new HashSet<string>(StringComparer.Ordinal);
            selectionStore[domainKey] = keys;
        }

        return keys;
    }

    /// <summary>Live enable answer for one (domain, pack) pair: the seeded fixture selection first,
    /// then every toggle this session wrote - one answer, read the same way the projection reads it.</summary>
    private bool IsEnabledLive(SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey)
    {
        if (selectionStore.TryGetValue(DomainKey(scope, raceDefName, targetDefName), out HashSet<string>? written))
        {
            return written.Contains(packKey);
        }

        // Fixture seed: the xenotype domain enables us.sang only; the race domain enables nothing.
        return scope == SqueakVoicePackScope.Xenotype && string.Equals(packKey, "us.sang", StringComparison.Ordinal);
    }

    public void ForgetUnavailable(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        LastForgetScope = scope;
        LastForgetRace = raceDefName;
        LastForgetTarget = targetDefName;
    }

    // UIR-002/003/006: the board's exits, the intro and the swatch, mirrored like every other
    // facade member here - the fakes answer the read-back exactly as production does.
    public bool LastIntroOpen = true;
    public string LastRaceBoardOpenRace = "";
    public string LastRaceBoardScrollKey = "";
    public string RaceBoardOpenCardKeyToReturn = "";

    public SqueakVoicePackScope LastQuickScope = SqueakVoicePackScope.Race;
    public string LastQuickRace = "", LastQuickTarget = "", LastQuickPackKey = "";
    public int QuickEnableCount;
    public string LastUiSwatch = "core";
    public int SwatchWriteCount;

    /// <summary>What the intro setter persisted (CORRECTIONS-R1 topic 3: the preference persists;
    /// a lane asserts the persisted value, the state mirror, and that NO audio write happened).</summary>
    public bool? LastIntroPersistedCollapsed;
    public void SetIntroOpen(bool open)
    {
        LastIntroOpen = open;
        LastIntroPersistedCollapsed = !open;
        state.IntroOpen = open;
    }

    public void ToggleIntro()
    {
        SetIntroOpen(!state.IntroOpen);
    }

    public string OpenXenotypeTarget(string targetDefName)
    {
        IReadOnlyList<XenotypeBoardRow> rows = SeedXenoBoard;
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].DefName, targetDefName, System.StringComparison.Ordinal))
            {
                return VoicePacksPageModel.ApplyOpenXenotypeTarget(ViewState, rows[i]);
            }
        }

        return "";
    }

    public string OpenRaceTarget(string raceDefName)
    {
        LastRaceBoardOpenRace = raceDefName ?? "";
        // CORRECTIONS-R1 topic 2: the fake no longer MIRRORS the intent - it runs the PRODUCTION
        // statics over the seeded plain row, so the lane exercises the real pure derivation
        // (DeriveIntent + ApplyOpenRaceTarget). No settings write happens on this path.
        IReadOnlyList<RaceBoardRow> rows = SeedRaceBoard;
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].RaceDefName, LastRaceBoardOpenRace, StringComparison.Ordinal))
            {
                string destination = VoicePacksPageModel.ApplyOpenRaceTarget(state, rows[i]);
                LastRaceBoardScrollKey = destination;
                return destination;
            }
        }

        LastRaceBoardScrollKey = "";
        return "";
    }
    /// <summary>Lanes seed board rows through this one factory so the plain 22-field struct is
    /// constructed in exactly one place (production's own constructor - no fake row type).</summary>
    public static RaceBoardRow SeedRow(
        string defName, string label = "",
        int racePacks = 0, int enabled = 0, int orphan = 0,
        SqueakVoicePackDomainState domainState = SqueakVoicePackDomainState.Available,
        bool xenoOnly = false, int xenoPacks = 0, int xenoEnabled = 0, int xenoOrphan = 0,
        bool xenoTargetMissing = false, string firstXenoTarget = "",
        bool hasFallback = false, bool fallbackPlayer = false, int fallbackSounds = 0,
        bool modeUsesPacks = true, IReadOnlyList<string>? supplyKeys = null,
        string quickScope = "", string quickTarget = "", string quickKey = "",
        string quickLabel = "", string quickTargetLabel = "", bool har = false)
        => new RaceBoardRow(
            defName, label.Length > 0 ? label : defName,
            racePacks, enabled, orphan, domainState, xenoOnly, xenoPacks, xenoEnabled, xenoOrphan,
            xenoTargetMissing, firstXenoTarget, hasFallback, fallbackPlayer, fallbackSounds,
            modeUsesPacks, supplyKeys ?? Array.Empty<string>(),
            quickScope, quickTarget, quickKey, quickLabel, quickTargetLabel,
            // OVT-001 cutover: the row identity is required plain data; the seeded fixtures
            // answer the ordinary collector values unless they declare HAR.
            new UsRaceIdentity(defName, label, har ? UsRaceSourceKind.ThirdParty : UsRaceSourceKind.Unknown,
                "", "", har, UsRaceLifeKind.Other));

    public void QuickEnableRacePack(SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey)
    {
        QuickEnableCount++;
        LastQuickScope = scope;
        LastQuickRace = raceDefName ?? "";
        LastQuickTarget = targetDefName ?? "";
        LastQuickPackKey = packKey ?? "";
        // The SAME write the row switch uses; it must never touch the mode.
        ToggleVoicePack(scope, raceDefName ?? "", targetDefName ?? "", packKey ?? "", true);
    }

    public string UiSwatchId => LastUiSwatch;

    public void SetUiSwatch(string swatchId)
    {
        SwatchWriteCount++;
        LastUiSwatch = swatchId;
    }
}
