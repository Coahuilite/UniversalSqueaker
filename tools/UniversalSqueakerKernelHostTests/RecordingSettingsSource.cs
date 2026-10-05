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

    /// <summary>
    /// When true the two Packs layer rows carry text that cannot fit a single line at 800px: a race row
    /// title long enough to wrap, and a short xenotype name whose race context makes the composed title
    /// long. The layer-height assertions need this because every other fixture string fits one line, and
    /// a one-line world cannot tell a measured band from a constant one.
    /// </summary>
    public bool WrappingDomainText;

    /// <summary>
    /// When set, the rich view's selected domain carries exactly these pack rows instead of the default
    /// two. The checklist projection lane needs rows whose search predicate accepts a STRICT, non-contiguous
    /// subset (so both directions of the list are observable) and a duplicated business key (so the
    /// engine's row-identity refusal is observable rather than assumed).
    /// </summary>
    public VoicePackRowView[]? ChecklistPacks;

    /// <summary>V2 P4 instrument: an explicit browse row set when a lane measures how far a long race list
    /// pushes the enable band down. Null keeps the built-in rows.</summary>
    public RaceLayerRowView[]? Races;

    /// <summary>V2 P2 instrument: when true the view follows the RECORDED selection, so a lane can assert
    /// that a browse write re-derives the enable band's scope line. Default false - the built-in fixture
    /// always selects its xenotype domain, which the other lanes rely on.</summary>
    public bool ReflectSelectionInView;

    /// <summary>V2 P2 instrument: production-shaped display fields for the fixture's xenotype domain
    /// (<c>DisplayName</c> and the race-context label). Null keeps this fixture's built-in values, whose
    /// "Sanguophage (Human)" display plus the defName fallback would double the race context in the scope
    /// line - a fixture artifact, not a product string, so the V2 scope lane supplies clean ones.</summary>
    public string? XenotypeDisplayName;
    public string? XenotypeRaceDisplay;
    // (No over-wide-domain knob: F5's consumer-side truncation is NOT landed - see TODO.)

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
    public int[]? MoodSourceLayers;

    /// <summary>
    /// V3 task-18 instrument: the record the FIRST rich mood row (Good) reports as its own. The PM's
    /// counterexample feeds a record whose factor flags are CLEARED but whose <c>sourcePresetDefName</c>
    /// anchor is RETAINED - the readout must never present that anchor as the origin of the shown numbers.
    /// </summary>
    public MoodTuningRecord? MoodOwnRecord;

    /// <summary>
    /// V3 task-18 instrument: the display label the resolved reset-to-preset TARGET carries for the rich
    /// rows whose <c>PresetReset</c> is Ready (the model projects the Def's own label). Empty keeps the
    /// built-in "Harness Baseline"; rows with no usable target get "".
    /// </summary>
    public string ResetTargetLabel = "Harness Baseline";

    /// <summary>V3 task-18 instrument: overrides the FIRST rich mood row's reset-to-preset availability, so
    /// the matrix can feed an anchor whose Def no longer resolves (PresetMissing). Null keeps Ready.</summary>
    public SqueakMoodResetPresetState? Row1PresetReset;

    private static readonly int[] DefaultMoodSourceLayers = { 0, 0, -1, -1, -1, -1, 1, 1, 0, 2, 0, -1 };

    /// <summary>Supplying layer of one factor slot: the lane's input when it feeds one, otherwise the
    /// production-shaped mix (Global+default, all-default, Race+Global, Xenotype+Global+default).</summary>
    private int SourceLayerAt(int index)
    {
        int[] layers = MoodSourceLayers ?? DefaultMoodSourceLayers;
        return index >= 0 && index < layers.Length ? layers[index] : -1;
    }

    private static string ResetTargetFor(SqueakMoodResetPresetState state, string label)
    {
        return state == SqueakMoodResetPresetState.Ready ? label : "";
    }

    /// <summary>
    /// V3 instrument: replaces the fixture's two production-shaped action-scope rows with the given set
    /// (empty = a DEGENERATE input no production model produces; the V3 lane uses it only as a labelled
    /// defensive probe of the mood area's coupling). Null keeps the two real rows.
    /// </summary>
    public ActionScopeRowView[]? TuningActionScopes;

    /// <summary>
    /// Eat-occurrence pair the fake's <see cref="BuildView"/> projects. Read by the parent toggle and
    /// its child row; the disabled-child lane flips the parent without writing anything, which is how
    /// the two drawn states are compared for identical geometry.
    /// </summary>
    public bool EatPrecisionEnabled = false;

    /// <summary>See <see cref="EatPrecisionEnabled"/>.</summary>
    public bool EatPrecisionIncludeDrugs = false;

    /// <summary>
    /// Author strings used by the rich BuildView (the pack/author filter options). The D9 lane
    /// injects a label far wider than the 143px filter column to prove the popup grows and the
    /// trigger ellipsizes instead of clipping.
    /// </summary>
    public string[] Authors = new[] { "AuthorA", "AuthorB" };

    // Basic writes.
    public SqueakVoicePackMode? LastMode;
    public float? LastGlobalVolume;
    public SqueakDistancePreset? LastDistancePreset;
    public float? LastDistanceRangeStart;
    public float? LastDistanceRangeEnd;
    public SqueakBasicToggle? LastBasicToggle;
    public bool? LastBasicToggleValue;
    public bool? LastCameraIndicator;
    public bool? LastEasterEggs;
    /// <summary>Parent eat-precision write, or null when the control never fired.</summary>
    public bool? LastEatPrecision;
    /// <summary>Child "include drugs" write, or null when the control never fired (the disabled-child
    /// lane asserts exactly this stays null while the parent is off).</summary>
    public bool? LastEatPrecisionIncludeDrugs;
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
    public SqueakActionScope? LastActionScope;
    public SqueakMood? LastMood;
    public SqueakMoodFactor? LastMoodFactor;
    public float? LastMoodValue;
    /// <summary>Mood of the last "reset to preset" write, or null when the control never fired.</summary>
    public SqueakMood? LastMoodPresetReset;
    public int LastMoodPresetResetCount;
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
            scaleCooldownWithTimeSpeed: true,
            scaleFrequencyWithTalking: true,
            scalePeriodicWithAudiblePopulation: true,
            showCameraIndicator: false,
            globalCooldownMultiplier: 1f,
            globalMinIntervalTicks: 216,
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
            moodTuningRows: Array.Empty<MoodTuningRowView>(),
            baselinePresets: Array.Empty<BaselinePresetView>(),
            buildIdentity: BuildIdentity,
            saveStatus: SaveStatus,
            isDirty: false,
            authors: Array.Empty<string>(),
            raceFilter: "",
            xenotypeFilter: "",
            raceFilterOptions: Array.Empty<FilterOptionView>(),
            xenotypeFilterOptions: Array.Empty<FilterOptionView>(),
            eatPrecisionEnabled: EatPrecisionEnabled,
            eatPrecisionIncludeDrugs: EatPrecisionIncludeDrugs, allowBabyActions: AllowBabyActions);
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
            XenotypeDisplayName ?? (WrappingDomainText ? "Sanguophage" : "Sanguophage (Human)"),
            "test-catalog",
            SqueakVoicePackDomainState.Available,
            isDormant: false,
            isTargetUnavailable: false,
            hasCanonicalConflict: false,
            enabledCount: 1,
            candidateCount: 2,
            orphanCount: 0,
            enabledKeys: new[] { "us.sang" },
            packs: ChecklistPacks ?? new[]
            {
                new VoicePackRowView("us.sang", "Sanguophage Voice Pack", "TestMod", "AuthorA", "def.sang", "full", "sang", isSelected: true),
                new VoicePackRowView("us.sang2", "Sanguophage Extra Pack", "TestMod2", "AuthorB", "def.sang2", "full", "extra", isSelected: false)
            },
            raceDisplay: XenotypeRaceDisplay);

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
                    moodCount: 3,
                    xenotypes: new[]
                    {
                        new BaselineXenotypeView("sanguophage", "Sanguophage", inheritFromRace: false, selected: false, actionCount: 5, moodCount: 3, image: XenotypeIcon)
                    })
            },
            selectedRaceCount: 1,
            selectedXenotypeCount: 0);

        return new VoicePacksViewState(
            SqueakVoicePackMode.Remix,
            allowEasterEggs: true,
            SqueakDistancePreset.Strong,
            scaleCooldownWithTimeSpeed: false,
            scaleFrequencyWithTalking: true,
            scalePeriodicWithAudiblePopulation: false,
            showCameraIndicator: true,
            globalCooldownMultiplier: 1f,
            globalMinIntervalTicks: 216,
            devLoggingMode: SqueakDevLoggingMode.Enabled,
            localizeDebugActions: true,
            globalVolumeFactor: 0.6f,
            distanceRangeMin: 20f,
            distanceRangeMax: 45f,
            biotechActive: true,
            bannerText: "rich harness catalog",
            races: Races ?? RaceRowsFor(filterSanguophage),
            xenotypeDomains: filterSanguophage ? Array.Empty<VoicePackDomainView>() : new[] { sang },
            selectedDomain: SelectedForView(sang, filterSanguophage),
            actionScopes: TuningActionScopes ?? new[]
            {
                new ActionScopeRowView("Eat", "Eat", ActionScopeGroup.SystemOrEvent, SqueakActionScope.AnyOccurrence, SqueakAction.Eat, hasOwnScope: true, effectiveScope: SqueakActionScope.AnyOccurrence),
                new ActionScopeRowView("Draft", "Draft", ActionScopeGroup.PlayerTriggered, SqueakActionScope.ActiveCommand, SqueakAction.Draft, hasOwnScope: false, effectiveScope: SqueakActionScope.ActiveCommand)
            },
            tuningLayer: EmptyXenotypeTarget ? 2 : 0,
            tuningRaceDefName: EmptyXenotypeTarget ? "" : "human",
            tuningXenotypeDefName: "",
            // XG1.1: the empty-target state carries NO domain options (that is what makes the target empty in
            // production); the override lets a control-case lane supply a real (race, xenotype) target.
            tuningDomains: EmptyXenotypeTarget
                ? Array.Empty<TuningDomainOptionView>()
                : TuningDomains ?? new[]
                {
                    new TuningDomainOptionView("human", "Human"),
                    new TuningDomainOptionView("testrace", "Test Race")
                },
            // All FOUR product moods, in the production enumeration order (VoicePacksPageModel
            // builds one row per SqueakMood: Good, Neutral, Bad, Break). Availability is a VIEW input
            // (the model computes it from flags/source), so this fake sets it directly instead of
            // materialising owned records: with own: null the controls would render correctly inert
            // and no interaction step could route a click. Every row carries DISTINCT effective
            // values, so the mood layout lane can identify a card by the write its own controls route
            // (a two-row fixture could not tell the third and fourth cards apart from the first two).
            // "reset to preset" stays ready on rows one and three and unavailable on two and four, so
            // both reset states are exercised on more than one card.
            moodTuningRows: new[]
            {
                new MoodTuningRowView(
                    SqueakMood.Good,
                    SqueakMood.Good.ToString(),
                    own: MoodOwnRecord,
                    effectivePitch: 1f,
                    effectiveVolume: 1f,
                    effectiveJitterHalf: 0f,
                    defaultReset: SqueakMoodResetDefaultState.Ready,
                    presetReset: Row1PresetReset ?? SqueakMoodResetPresetState.Ready,
                    pitchSourceLayer: SourceLayerAt(0), volumeSourceLayer: SourceLayerAt(1), jitterSourceLayer: SourceLayerAt(2), resetPresetTarget: ResetTargetFor(Row1PresetReset ?? SqueakMoodResetPresetState.Ready, ResetTargetLabel)),
                new MoodTuningRowView(
                    SqueakMood.Neutral,
                    SqueakMood.Neutral.ToString(),
                    own: null,
                    effectivePitch: 0.9f,
                    effectiveVolume: 0.8f,
                    effectiveJitterHalf: 0.1f,
                    defaultReset: SqueakMoodResetDefaultState.Ready,
                    presetReset: SqueakMoodResetPresetState.NotFromPreset,
                    pitchSourceLayer: SourceLayerAt(3), volumeSourceLayer: SourceLayerAt(4), jitterSourceLayer: SourceLayerAt(5), resetPresetTarget: ResetTargetFor(SqueakMoodResetPresetState.NotFromPreset, ResetTargetLabel)),
                new MoodTuningRowView(
                    SqueakMood.Bad,
                    SqueakMood.Bad.ToString(),
                    own: null,
                    effectivePitch: 0.75f,
                    effectiveVolume: 0.6f,
                    effectiveJitterHalf: 0.2f,
                    defaultReset: SqueakMoodResetDefaultState.Ready,
                    presetReset: SqueakMoodResetPresetState.Ready,
                    pitchSourceLayer: SourceLayerAt(6), volumeSourceLayer: SourceLayerAt(7), jitterSourceLayer: SourceLayerAt(8), resetPresetTarget: ResetTargetFor(SqueakMoodResetPresetState.Ready, "Harness Baseline")),
                new MoodTuningRowView(
                    SqueakMood.Break,
                    SqueakMood.Break.ToString(),
                    own: null,
                    effectivePitch: 0.6f,
                    effectiveVolume: 0.4f,
                    effectiveJitterHalf: 0.3f,
                    defaultReset: SqueakMoodResetDefaultState.Ready,
                    presetReset: SqueakMoodResetPresetState.NotFromPreset,
                    pitchSourceLayer: SourceLayerAt(9), volumeSourceLayer: SourceLayerAt(10), jitterSourceLayer: SourceLayerAt(11), resetPresetTarget: ResetTargetFor(SqueakMoodResetPresetState.NotFromPreset, ResetTargetLabel))
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
            eatPrecisionEnabled: EatPrecisionEnabled,
            eatPrecisionIncludeDrugs: EatPrecisionIncludeDrugs, allowBabyActions: AllowBabyActions);
    }

    /// <summary>
    /// The domain the view presents as selected. Opt-in (<see cref="ReflectSelectionInView"/>): with it on,
    /// a recorded RACE selection is projected as a race-scope domain so a lane can watch the enable band's
    /// scope line follow the browse write; otherwise the fixture keeps its built-in xenotype selection.
    /// </summary>
    private VoicePackDomainView? SelectedForView(VoicePackDomainView sang, bool filterSanguophage)
    {
        if (filterSanguophage) return null;
        if (!ReflectSelectionInView || LastSelectedScope != SqueakVoicePackScope.Race) return sang;

        string race = LastSelectedRace ?? "";
        foreach (RaceLayerRowView row in Races ?? RaceRowsFor(false))
        {
            if (!string.Equals(row.RaceDefName, race, StringComparison.Ordinal)) continue;
            return new VoicePackDomainView(
                SqueakVoicePackScope.Race, race, "", row.DisplayName, "test-catalog",
                SqueakVoicePackDomainState.Available, isDormant: false, isTargetUnavailable: false,
                hasCanonicalConflict: false, enabledCount: row.EnabledCount, candidateCount: row.CandidateCount,
                orphanCount: 0, enabledKeys: Array.Empty<string>(),
                packs: ChecklistPacks ?? Array.Empty<VoicePackRowView>(), raceDisplay: row.DisplayName);
        }

        return sang;
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

    public string SectionHelpKey(string sectionKey)
    {
        return VoicePacksPageModel.SectionHelpKeyOf(sectionKey);
    }

    public void SetMode(SqueakVoicePackMode mode) => LastMode = mode;

    public void SetGlobalVolume(float value) => LastGlobalVolume = value;

    public void SetDistancePreset(SqueakDistancePreset preset) => LastDistancePreset = preset;

    public void SetDistanceRange(float start, float end)
    {
        LastDistanceRangeStart = start;
        LastDistanceRangeEnd = end;
    }

    public void SetBasicToggle(SqueakBasicToggle key, bool value)
    {
        LastBasicToggle = key;
        LastBasicToggleValue = value;
    }

    public void SetCameraIndicator(bool value) => LastCameraIndicator = value;

    public void SetEasterEggs(bool value) => LastEasterEggs = value;

    public bool AllowBabyActions;
    public bool? LastBabyActions;
    public void SetBabyActions(bool value) { LastBabyActions = value; AllowBabyActions = value; }
    public void SetEatPrecision(bool value) => LastEatPrecision = value;

    public void SetEatPrecisionIncludeDrugs(bool value) => LastEatPrecisionIncludeDrugs = value;

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
        reportMirror = new UsKernelSettingsSource(new UniversalSqueakerSettings());
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

        // Mirror VoicePacksPageModel.ApplyActiveTab exactly: a real workspace switch also moves the
        // active section to that workspace's primary section, and help-section-key resolves through
        // it. Without this the fake would pin help-section-key to "us/mode-row" forever, so no lane
        // could observe the help topic following the workspace while the drawer stays open.
        if (!string.Equals(state.ActiveTab, normalized, StringComparison.Ordinal))
        {
            state.ActiveTab = normalized;
            state.ActiveSectionKey = normalized switch
            {
                "Distance" => "attenuation-editor",
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

    public void SetTuningLayer(int layer)
    {
        LastTuningLayer = layer;
        if (MirrorTuningWrites) state.TuningLayer = layer;
    }

    public void SetTuningDomain(string raceDefName, string targetDefName)
    {
        LastTuningDomainRace = raceDefName;
        LastTuningDomainTarget = targetDefName;
        if (MirrorTuningWrites)
        {
            state.TuningRaceDefName = raceDefName;
            state.TuningXenotypeDefName = targetDefName;
        }
    }

    public void SelectDomain(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        LastSelectedScope = scope;
        LastSelectedRace = raceDefName;
        LastSelectedTarget = targetDefName;
    }

    public void SetDomainFilter(SqueakDomainFilterKind kind, bool flag)
    {
        LastDomainFilterKind = kind;
        LastDomainFilterFlag = flag;
    }

    public void SetPackFilter(string author)
    {
        LastPackFilter = author;
        VoicePacksPageModel.SetPackFilter(state, author);
    }

    public void SetRaceFilter(string raceDefName) => LastRaceFilter = raceDefName;

    public void SetXenotypeFilter(string xenotypeDefName) => LastXenotypeFilter = xenotypeDefName;

    public void SetSearchText(string text)
    {
        // Mirror the production facade exactly: UsKernelSettingsSource.SetSearchText routes through
        // VoicePacksPageModel.SetSearchText, which writes ViewState.SearchText - the very field the
        // "search-text" binding READS. A fake that only recorded the write would leave the read-back
        // stale, and the projection lane's whole point is that a search write and the list it produces
        // are the same frame's answer.
        LastSearchText = text;
        state.SearchText = text ?? "";
    }
    // No SetHelpHover / BeginHelpHoverFrame on this fake: since FL P3 the hover claim is UiSession
    // state (ClaimHover/HoverClaim), not a business write, so the end-to-end lanes read it off the
    // host's session. SetHelpSelection stays retired with the D2 index-list cut.

    public void SetActionScope(string actionKey, SqueakActionScope? scope)
    {
        LastActionKey = actionKey;
        LastActionScope = scope;
    }

    public void SetMoodTuning(SqueakMood mood, SqueakMoodFactor factor, float? value)
    {
        LastMood = mood;
        LastMoodFactor = factor;
        LastMoodValue = value;
    }

    public void ResetMoodToPreset(SqueakMood mood)
    {
        LastMoodPresetReset = mood;
        LastMoodPresetResetCount++;
    }

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
    }

    public void ForgetUnavailable(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        LastForgetScope = scope;
        LastForgetRace = raceDefName;
        LastForgetTarget = targetDefName;
    }
}
