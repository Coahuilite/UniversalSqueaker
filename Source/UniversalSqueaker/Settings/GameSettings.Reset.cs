using System;
using Verse;
namespace UniversalSqueaker;
public enum SqueakResetOutcome { NoChange, Applied, Rejected }
public enum SqueakNormalResetArea { Distance, Basics, Diagnostics }
/// <summary>Exact player-owned resets. Author definitions and separate final-fallback files are never reset here.</summary>
public partial class GameSettings
{
    internal SqueakResetOutcome ResetAllSettings()
    {
        var d = new GameSettings();
        bool changed = voicePackMode != d.voicePackMode || scaleFrequencyWithTalking != d.scaleFrequencyWithTalking
            || globalVolumeFactor != d.globalVolumeFactor || activity != d.activity
            || distancePreset != d.distancePreset || !DistanceRangesEqual(distanceRange, d.distanceRange)
            || localizeDebugActions != d.localizeDebugActions || showCameraIndicator != d.showCameraIndicator
            || devLoggingMode != d.devLoggingMode || uiSwatchId != d.uiSwatchId || uiIntroCollapsed != d.uiIntroCollapsed
            || allowEasterEggSounds != d.allowEasterEggSounds || voicePackSelections.Count != 0 || actionTuning.Count != 0;
        if (!changed) return SqueakResetOutcome.NoChange;
        voicePackMode = d.voicePackMode; scaleFrequencyWithTalking = d.scaleFrequencyWithTalking;
        globalVolumeFactor = d.globalVolumeFactor; activity = d.activity;
        distancePreset = d.distancePreset; distanceRange = d.distanceRange;
        localizeDebugActions = d.localizeDebugActions; showCameraIndicator = d.showCameraIndicator;
        devLoggingMode = d.devLoggingMode; uiSwatchId = d.uiSwatchId; uiIntroCollapsed = d.uiIntroCollapsed;
        allowEasterEggSounds = d.allowEasterEggSounds;
        // Source binding intent and last-good author snapshots are not player tuning defaults.
        voicePackSelections = d.voicePackSelections; actionTuning = d.actionTuning;
        ApplySettingsRuntimeSideEffects(); ApplyToRuntime(); QueuePersistence();
        return SqueakResetOutcome.Applied;
    }
    internal SqueakResetOutcome ResetDistanceDefaults()
    {
        var target = GetDistancePresetRange(SqueakDistancePreset.Balanced);
        if (distancePreset == SqueakDistancePreset.Balanced && DistanceRangesEqual(distanceRange, target)) return SqueakResetOutcome.NoChange;
        distancePreset = SqueakDistancePreset.Balanced; distanceRange = target;
        NotifyDistanceRuntimeChanged(); QueuePersistence(); return SqueakResetOutcome.Applied;
    }
    internal SqueakResetOutcome ResetActionTuningRow(string action, string race, string child)
    {
        if (!Authoring.AuthorXml.ActionId(action) || !IsValidTuningDomain(race, child)) return SqueakResetOutcome.Rejected;
        if (actionTuning.RemoveAll(x => SameActionTuningIdentity(x, action, race, child)) == 0) return SqueakResetOutcome.NoChange;
        PublishBehaviorConfiguration(); QueuePersistence(); return SqueakResetOutcome.Applied;
    }
    internal SqueakResetOutcome ResetActionTuningArea(string race, string child)
    {
        if (!IsValidTuningDomain(race, child)) return SqueakResetOutcome.Rejected;
        if (actionTuning.RemoveAll(x => x.serviceFamily == "Pawn" && x.raceDefName == race && x.xenotypeDefName == child) == 0) return SqueakResetOutcome.NoChange;
        PublishBehaviorConfiguration(); QueuePersistence(); return SqueakResetOutcome.Applied;
    }
    internal SqueakResetOutcome ResetNormalArea(SqueakNormalResetArea area)
    {
        switch (area)
        {
            case SqueakNormalResetArea.Distance:
                var d = new GameSettings();
                if (globalVolumeFactor == d.globalVolumeFactor && distancePreset == d.distancePreset && DistanceRangesEqual(distanceRange, d.distanceRange)) return SqueakResetOutcome.NoChange;
                globalVolumeFactor = d.globalVolumeFactor; distancePreset = d.distancePreset; distanceRange = d.distanceRange;
                PublishBehaviorConfiguration(); NotifyDistanceRuntimeChanged(); QueuePersistence(); return SqueakResetOutcome.Applied;
            case SqueakNormalResetArea.Basics: return ResetBasicsArea();
            case SqueakNormalResetArea.Diagnostics:
                if (devLoggingMode == SqueakDevLoggingMode.Auto && !localizeDebugActions) return SqueakResetOutcome.NoChange;
                devLoggingMode = SqueakDevLoggingMode.Auto; localizeDebugActions = false;
                ApplyDevLoggingModeToRuntime(true); Patch_DebugTabMenu_Actions.SetEnabled(false); QueuePersistence();
                return SqueakResetOutcome.Applied;
            default: return SqueakResetOutcome.Rejected;
        }
    }
    private SqueakResetOutcome ResetBasicsArea()
    {
        var d = new GameSettings();
        bool changed = allowEasterEggSounds != d.allowEasterEggSounds
            || scaleFrequencyWithTalking != d.scaleFrequencyWithTalking || showCameraIndicator != d.showCameraIndicator;
        foreach (var row in actionTuning)
        {
            if (row.serviceFamily != "Pawn" || row.raceDefName != "" || row.xenotypeDefName != "") continue;
            if (row.actionKey == CoreActions.Select)
            {
                changed |= row.hasSelectRequirePlayerControlled || row.hasSelectRequireAwake || row.hasSelectRequireNotDowned;
                row.hasSelectRequirePlayerControlled = row.hasSelectRequireAwake = row.hasSelectRequireNotDowned = false;
            }
            if (row.actionKey != CoreActions.Eat) continue;
            changed |= row.hasEatPhase || row.hasNutritionScope || row.hasDrugScope;
            row.hasEatPhase = row.hasNutritionScope = row.hasDrugScope = false;
        }
        if (!changed) return SqueakResetOutcome.NoChange;
        allowEasterEggSounds = d.allowEasterEggSounds; scaleFrequencyWithTalking = d.scaleFrequencyWithTalking;
        showCameraIndicator = d.showCameraIndicator; SqueakDebug.ShowCameraIndicator = showCameraIndicator;
        PublishBehaviorConfiguration(); QueuePersistence(); return SqueakResetOutcome.Applied;
    }
    private static bool IsValidTuningDomain(string? race, string? child) => race != null && child != null
        && race == race.Trim() && child == child.Trim() && (race != "" || child == "");
    private static bool DistanceRangesEqual(FloatRange a, FloatRange b) => a.min == b.min && a.max == b.max;
}
