using System;
using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>New-line settings format only. The new Mod handle owns a different physical file.</summary>
public partial class GameSettings
{
    private int configurationFormat = 1;
    private bool configurationUnreadable;
    private bool settingsLoadedFromFile;
    private bool appearanceRepairPending;
    internal bool ConsumeAppearanceRepair()
    { bool pending = appearanceRepairPending; appearanceRepairPending = false; return pending; }
    public bool allowEasterEggSounds;
    internal bool SettingsLoadedFromFile => settingsLoadedFromFile;
    internal bool IsConfigurationUnreadable => configurationUnreadable;

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.LoadingVars) settingsLoadedFromFile = true;
        Scribe_Values.Look(ref configurationFormat, "responseConfigurationFormat", Scribe.mode == LoadSaveMode.LoadingVars ? 0 : 1, forceSave: true);
        Scribe_Values.Look(ref voicePackMode, "audioMode", SqueakVoicePackMode.Fallback);
        Scribe_Values.Look(ref globalVolumeFactor, "outputVolume", 1f);
        Scribe_Values.Look(ref activity, "activity", 1d);
        Scribe_Values.Look(ref scaleFrequencyWithTalking, "talkingFrequency", true);
        Scribe_Values.Look(ref distancePreset, "distancePreset", SqueakDistancePreset.Balanced);
        Scribe_Values.Look(ref distanceRange, "outputDistance", GetDistancePresetRange(SqueakDistancePreset.Balanced));
        Scribe_Values.Look(ref allowEasterEggSounds, "eggParticipation", false);
        Scribe_Values.Look(ref behaviorSourcesXml, "behaviorSources", "");
        Scribe_Collections.Look(ref voicePackSelections, "audioSelections", LookMode.Deep);
        Scribe_Collections.Look(ref actionTuning, "playerResponses", LookMode.Deep);
        Scribe_Values.Look(ref localizeDebugActions, "localizedDebugMenu", false);
        Scribe_Values.Look(ref showCameraIndicator, "cameraIndicator", false);
        Scribe_Values.Look(ref devLoggingMode, "diagnosticLogging", SqueakDevLoggingMode.Auto);
        Scribe_Values.Look(ref uiSwatchId, "appearanceSwatch", "core");
        Scribe_Values.Look(ref uiIntroCollapsed, "overviewIntroCollapsed", false);
        Scribe_Values.Look(ref showMainMenuEntry, "showMainMenuEntry", true);
        Scribe_Values.Look(ref showOptionsEntry, "showOptionsEntry", true);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        configurationUnreadable = configurationFormat != 1;
        if (configurationUnreadable) { Log.Warning("usdiag configuration=UnsupportedResponseConfigurationFormat:" + configurationFormat); return; }
        voicePackSelections ??= new List<VoicePackSelectionRecord>();
        actionTuning ??= new List<ActionTuningRecord>();
        if (!Enum.IsDefined(typeof(SqueakVoicePackMode), voicePackMode)) voicePackMode = SqueakVoicePackMode.Disabled;
        if (!Enum.IsDefined(typeof(SqueakDevLoggingMode), devLoggingMode)) devLoggingMode = SqueakDevLoggingMode.Auto;
        // Stored as a string, not an enum: removed DLC IDs and unknown values remain readable.
        // Capture repair only: GetSettings runs before the native persistence owner exists and
        // may be off the Unity thread. Main-thread startup queues the existing save funnel once.
        if (!UsSwatchIds.IsKnown(uiSwatchId)) { uiSwatchId = UsSwatchIds.Default; appearanceRepairPending = true; }
        distanceRange = ClampDistanceRange(distanceRange);
        PublishBehaviorConfiguration();
    }
}
