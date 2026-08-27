using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>Read-only projection of the VoicePack settings page. Built from settings + catalog each frame.</summary>
public sealed class VoicePacksViewState
{
    public SqueakVoicePackMode Mode { get; }
    public bool AllowEasterEggs { get; }
    public SqueakDistancePreset DistancePreset { get; }
    public bool ScaleCooldownWithTimeSpeed { get; }
    public bool ScaleFrequencyWithTalking { get; }
    public bool ScalePeriodicWithAudiblePopulation { get; }
    public bool ShowCameraIndicator { get; }
    public float GlobalCooldownMultiplier { get; }
    public bool BiotechActive { get; }
    public string BannerText { get; }
    public IReadOnlyList<RaceLayerRowView> Races { get; }
    public IReadOnlyList<VoicePackDomainView> XenotypeDomains { get; }
    public VoicePackDomainView? SelectedDomain { get; }
    public IReadOnlyList<ActionScopeRowView> ActionScopes { get; }
    public IReadOnlyList<BaselinePresetView> BaselinePresets { get; }

    public VoicePacksViewState(
        SqueakVoicePackMode mode,
        bool allowEasterEggs,
        SqueakDistancePreset distancePreset,
        bool scaleCooldownWithTimeSpeed,
        bool scaleFrequencyWithTalking,
        bool scalePeriodicWithAudiblePopulation,
        bool showCameraIndicator,
        float globalCooldownMultiplier,
        bool biotechActive,
        string bannerText,
        IReadOnlyList<RaceLayerRowView> races,
        IReadOnlyList<VoicePackDomainView> xenotypeDomains,
        VoicePackDomainView? selectedDomain,
        IReadOnlyList<ActionScopeRowView> actionScopes,
        IReadOnlyList<BaselinePresetView> baselinePresets)
    {
        Mode = mode;
        AllowEasterEggs = allowEasterEggs;
        DistancePreset = distancePreset;
        ScaleCooldownWithTimeSpeed = scaleCooldownWithTimeSpeed;
        ScaleFrequencyWithTalking = scaleFrequencyWithTalking;
        ScalePeriodicWithAudiblePopulation = scalePeriodicWithAudiblePopulation;
        ShowCameraIndicator = showCameraIndicator;
        GlobalCooldownMultiplier = globalCooldownMultiplier;
        BiotechActive = biotechActive;
        BannerText = bannerText ?? "";
        Races = races ?? Array.Empty<RaceLayerRowView>();
        XenotypeDomains = xenotypeDomains ?? Array.Empty<VoicePackDomainView>();
        SelectedDomain = selectedDomain;
        ActionScopes = actionScopes ?? Array.Empty<ActionScopeRowView>();
        BaselinePresets = baselinePresets ?? Array.Empty<BaselinePresetView>();
    }
}

/// <summary>One race domain row in the Race layer.</summary>
public readonly struct RaceLayerRowView
{
    public readonly string RaceDefName;
    public readonly string DisplayName;
    public readonly int EnabledCount;
    public readonly int CandidateCount;
    public readonly SqueakVoicePackDomainState State;

    public RaceLayerRowView(string raceDefName, string displayName, int enabledCount, int candidateCount, SqueakVoicePackDomainState state)
    {
        RaceDefName = raceDefName ?? "";
        DisplayName = displayName ?? raceDefName ?? "";
        EnabledCount = enabledCount;
        CandidateCount = candidateCount;
        State = state;
    }
}

/// <summary>One selectable VoicePack domain (Race or Xenotype). Components receive this as read-only state.</summary>
public readonly struct VoicePackDomainView
{
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    public readonly string TargetDefName;
    public readonly string DisplayName;
    public readonly string SourceText;
    public readonly SqueakVoicePackDomainState State;
    public readonly bool IsDormant;
    public readonly bool IsTargetUnavailable;
    public readonly bool HasCanonicalConflict;
    public readonly int EnabledCount;
    public readonly int CandidateCount;
    public readonly int OrphanCount;
    public readonly IReadOnlyList<string> EnabledKeys;
    public readonly IReadOnlyList<VoicePackRowView> Packs;

    public VoicePackDomainView(
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName,
        string displayName,
        string sourceText,
        SqueakVoicePackDomainState state,
        bool isDormant,
        bool isTargetUnavailable,
        bool hasCanonicalConflict,
        int enabledCount,
        int candidateCount,
        int orphanCount,
        IReadOnlyList<string> enabledKeys,
        IReadOnlyList<VoicePackRowView> packs)
    {
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
        DisplayName = displayName ?? targetDefName ?? "";
        SourceText = sourceText ?? "";
        State = state;
        IsDormant = isDormant;
        IsTargetUnavailable = isTargetUnavailable;
        HasCanonicalConflict = hasCanonicalConflict;
        EnabledCount = enabledCount;
        CandidateCount = candidateCount;
        OrphanCount = orphanCount;
        EnabledKeys = enabledKeys ?? Array.Empty<string>();
        Packs = packs ?? Array.Empty<VoicePackRowView>();
    }

    public string DomainIdentity => (Scope == SqueakVoicePackScope.Xenotype ? TargetDefName : RaceDefName) ?? "";
}

/// <summary>One built-in action's effective Global-layer scope, projected for the scope tree.
/// Carries the built-in <see cref="SqueakAction"/> so the widget can skip scope states the action
/// does not support (e.g. Draft/Undraft/Equip are ActiveCommand-only).</summary>
public readonly struct ActionScopeRowView
{
    public readonly string ActionKey;
    public readonly string DisplayName;
    public readonly SqueakActionScope Scope;
    public readonly SqueakAction Action;

    public ActionScopeRowView(string actionKey, string displayName, SqueakActionScope scope, SqueakAction action)
    {
        ActionKey = actionKey ?? "";
        DisplayName = displayName ?? actionKey ?? "";
        Scope = scope;
        Action = action;
    }
}

/// <summary>One VoicePack row inside a domain checklist.</summary>
public readonly struct VoicePackRowView
{
    public readonly string Key;
    public readonly string Label;
    public readonly string ModName;
    public readonly string Author;
    public readonly string DefName;
    public readonly string Coverage;
    public readonly string SearchText;
    public readonly bool IsSelected;

    public VoicePackRowView(
        string key,
        string label,
        string modName,
        string author,
        string defName,
        string coverage,
        string searchText,
        bool isSelected)
    {
        Key = key ?? "";
        Label = label ?? defName ?? key ?? "";
        ModName = modName ?? "";
        Author = author ?? "";
        DefName = defName ?? "";
        Coverage = coverage ?? "";
        SearchText = searchText ?? "";
        IsSelected = isSelected;
    }
}

/// <summary>One tuning-baseline preset projected for the import UI.</summary>
public sealed class BaselinePresetView
{
    public readonly string DefName;
    public readonly string Label;
    public readonly string Description;
    public readonly bool Expanded;
    public readonly IReadOnlyList<BaselineRaceView> Races;
    public readonly int SelectedRaceCount;
    public readonly int SelectedXenotypeCount;

    public BaselinePresetView(string defName, string label, string description, bool expanded,
        IReadOnlyList<BaselineRaceView> races, int selectedRaceCount, int selectedXenotypeCount)
    {
        DefName = defName ?? "";
        Label = label ?? defName ?? "";
        Description = description ?? "";
        Expanded = expanded;
        Races = races ?? Array.Empty<BaselineRaceView>();
        SelectedRaceCount = selectedRaceCount;
        SelectedXenotypeCount = selectedXenotypeCount;
    }
}

/// <summary>One race row inside a baseline preset tree.</summary>
public sealed class BaselineRaceView
{
    public readonly string RaceDefName;
    public readonly string DisplayName;
    public readonly bool Selected;
    public readonly int ActionCount;
    public readonly int MoodCount;
    public readonly IReadOnlyList<BaselineXenotypeView> Xenotypes;

    public BaselineRaceView(string raceDefName, string displayName, bool selected, int actionCount, int moodCount, IReadOnlyList<BaselineXenotypeView> xenotypes)
    {
        RaceDefName = raceDefName ?? "";
        DisplayName = displayName ?? raceDefName ?? "";
        Selected = selected;
        ActionCount = actionCount;
        MoodCount = moodCount;
        Xenotypes = xenotypes ?? Array.Empty<BaselineXenotypeView>();
    }
}

/// <summary>One xenotype row inside a baseline preset race block.</summary>
public sealed class BaselineXenotypeView
{
    public readonly string XenotypeDefName;
    public readonly string DisplayName;
    public readonly bool InheritFromRace;
    public readonly bool Selected;
    public readonly int ActionCount;
    public readonly int MoodCount;

    public BaselineXenotypeView(string xenotypeDefName, string displayName, bool inheritFromRace, bool selected, int actionCount, int moodCount)
    {
        XenotypeDefName = xenotypeDefName ?? "";
        DisplayName = displayName ?? xenotypeDefName ?? "";
        InheritFromRace = inheritFromRace;
        Selected = selected;
        ActionCount = actionCount;
        MoodCount = moodCount;
    }
}
