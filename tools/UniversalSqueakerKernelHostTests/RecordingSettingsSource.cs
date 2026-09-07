using System;
using System.Collections.Generic;
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
    /// When true the two Packs layer rows carry text that cannot fit a single line at 800px: short race
    /// names with an oversized "n / m enabled · state" detail, and a short xenotype name whose race
    /// context makes the composed title long. The layer-height assertions need this because every other
    /// fixture string fits one line, and a one-line world cannot tell a measured band from a constant one.
    /// </summary>
    public bool WrappingDomainText;

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

    // View/navigation writes.
    public string? LastActiveTab;
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
    public string? LastBaselinePresetToggle;
    public string? LastBaselineRacePreset;
    public string? LastBaselineRace;
    public bool? LastBaselineRaceSelected;
    public string? LastBaselineXenoPreset;
    public string? LastBaselineXenoRace;
    public string? LastBaselineXeno;
    public bool? LastBaselineXenoSelected;
    public string? LastBaselineImport;

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

    public string BuildIdentity => "test-build";

    public string SaveStatus => "Idle";

    public bool IsDirty => false;
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
            xenotypeFilterOptions: Array.Empty<FilterOptionView>());
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
            });

        var preset = new BaselinePresetView(
            "us.preset1",
            "Balanced Test Preset",
            "Deterministic harness preset",
            expanded: true,
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
                        new BaselineXenotypeView("sanguophage", "Sanguophage", inheritFromRace: false, selected: false, actionCount: 5, moodCount: 3)
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
            globalVolumeFactor: 0.6f,
            distanceRangeMin: 20f,
            distanceRangeMax: 45f,
            biotechActive: true,
            bannerText: "rich harness catalog",
            races: RaceRowsFor(filterSanguophage),
            xenotypeDomains: filterSanguophage ? Array.Empty<VoicePackDomainView>() : new[] { sang },
            selectedDomain: filterSanguophage ? null : sang,
            actionScopes: new[]
            {
                new ActionScopeRowView("Eat", "Eat", ActionScopeGroup.Autonomous, SqueakActionScope.AnyOccurrence, SqueakAction.Eat, hasOwnScope: true, effectiveScope: SqueakActionScope.AnyOccurrence),
                new ActionScopeRowView("Draft", "Draft", ActionScopeGroup.Operable, SqueakActionScope.ActiveCommand, SqueakAction.Draft, hasOwnScope: false, effectiveScope: SqueakActionScope.ActiveCommand)
            },
            tuningLayer: 0,
            tuningRaceDefName: "human",
            tuningXenotypeDefName: "",
            tuningDomains: new[]
            {
                new TuningDomainOptionView("human", "Human"),
                new TuningDomainOptionView("testrace", "Test Race")
            },
            moodTuningRows: new[]
            {
                new MoodTuningRowView(SqueakMood.Good, "Good", own: null, effectivePitch: 1f, effectiveVolume: 1f, effectiveJitterHalf: 0f),
                new MoodTuningRowView(SqueakMood.Neutral, "Neutral", own: null, effectivePitch: 1f, effectiveVolume: 1f, effectiveJitterHalf: 0f)
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
            xenotypeFilterOptions: new[] { new FilterOptionView("All", ""), new FilterOptionView("Sanguophage", "sanguophage") });
    }

    /// <summary>
    /// Mirrors the production race-filter semantics for the parity lane: selecting a race narrows the
    /// race layer to that row, drops non-matching xenotype domains, and clears the selection. The
    /// fake must respond to the filter or the harness can never compare "filtered live" against
    /// "filtered from the start".
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
            new RaceLayerRowView("human", "Human", WrappingDomainText ? int.MaxValue : 2, WrappingDomainText ? int.MaxValue - 1 : 3, WrappingDomainText ? SqueakVoicePackDomainState.TargetUnavailable : SqueakVoicePackDomainState.Available),
            new RaceLayerRowView("testrace", "Test Race", WrappingDomainText ? int.MaxValue : 1, WrappingDomainText ? int.MaxValue - 1 : 2, WrappingDomainText ? SqueakVoicePackDomainState.TargetUnavailable : SqueakVoicePackDomainState.Available),
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
        state.ActiveTab = normalized;
    }

    public void ScrollToSection(string sectionKey) => LastScrollToSection = sectionKey;

    public void SetTuningLayer(int layer) => LastTuningLayer = layer;

    public void SetTuningDomain(string raceDefName, string targetDefName)
    {
        LastTuningDomainRace = raceDefName;
        LastTuningDomainTarget = targetDefName;
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

    public void SetSearchText(string text) => LastSearchText = text;
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

    public void ToggleBaselinePreset(string presetDefName) => LastBaselinePresetToggle = presetDefName;

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
