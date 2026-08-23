using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>Field-presence flags preserve inheritance separately from valid zero-valued overrides.</summary>
public class XenotypeMoodOverride : IExposable
{
    public SqueakMood mood = SqueakMood.Neutral;
    public bool hasPitchFactor;
    public float pitchFactor = 1f;
    public bool hasVolumeFactor;
    public float volumeFactor = 1f;
    public bool hasPitchJitter;
    public FloatRange pitchJitter = FloatRange.One;

    public void ExposeData()
    {
        Scribe_Values.Look(ref mood, "mood");
        Scribe_Values.Look(ref hasPitchFactor, "hasPitchFactor", false);
        Scribe_Values.Look(ref pitchFactor, "pitchFactor", 1f);
        Scribe_Values.Look(ref hasVolumeFactor, "hasVolumeFactor", false);
        Scribe_Values.Look(ref volumeFactor, "volumeFactor", 1f);
        Scribe_Values.Look(ref hasPitchJitter, "hasPitchJitter", false);
        Scribe_Values.Look(ref pitchJitter, "pitchJitter", FloatRange.One);
    }

    internal static XenotypeMoodOverride Clone(XenotypeMoodOverride value) => new() { mood = value.mood, hasPitchFactor = value.hasPitchFactor, pitchFactor = value.pitchFactor, hasVolumeFactor = value.hasVolumeFactor, volumeFactor = value.volumeFactor, hasPitchJitter = value.hasPitchJitter, pitchJitter = value.pitchJitter };
}

public class XenotypeActionBehaviorOverride : IExposable
{
    public SqueakAction action = SqueakAction.Call;
    public bool hasEnabled;
    public bool enabled;
    public bool hasIntervalMultiplier;
    public float intervalMultiplier = 1f;
    public bool hasProbabilityMultiplier;
    public float probabilityMultiplier = 1f;

    public void ExposeData()
    {
        Scribe_Values.Look(ref action, "action");
        Scribe_Values.Look(ref hasEnabled, "hasEnabled", false);
        Scribe_Values.Look(ref enabled, "enabled", false);
        Scribe_Values.Look(ref hasIntervalMultiplier, "hasIntervalMultiplier", false);
        Scribe_Values.Look(ref intervalMultiplier, "intervalMultiplier", 1f);
        Scribe_Values.Look(ref hasProbabilityMultiplier, "hasProbabilityMultiplier", false);
        Scribe_Values.Look(ref probabilityMultiplier, "probabilityMultiplier", 1f);
    }

    internal static XenotypeActionBehaviorOverride Clone(XenotypeActionBehaviorOverride value) => new() { action = value.action, hasEnabled = value.hasEnabled, enabled = value.enabled, hasIntervalMultiplier = value.hasIntervalMultiplier, intervalMultiplier = value.intervalMultiplier, hasProbabilityMultiplier = value.hasProbabilityMultiplier, probabilityMultiplier = value.probabilityMultiplier };
}

/// <summary>Global action scope. Legacy bool-only records migrate during PostLoadInit.</summary>
public class GlobalActionEnabledRecord : IExposable
{
    public SqueakAction action = SqueakAction.Call;
    public bool enabled = true;
    public SqueakActionScope scope = SqueakActionScope.AnyOccurrence;
    [System.NonSerialized] public bool scopeWasLoaded;
    public void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars) scopeWasLoaded = Scribe.loader?.curXmlParent?["scope"] != null;
        Scribe_Values.Look(ref action, "action");
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref scope, "scope", SqueakActionScope.AnyOccurrence);
    }
}

/// <summary>Persisted behavioral and mood overrides; audio-pool selection remains in a separate record.</summary>
public class XenotypePresetRecord : IExposable
{
    public string raceDefName = "";
    public string xenotypeDefName = "";
    public bool hasOverallIntervalMultiplier;
    public float overallIntervalMultiplier = 1f;
    public List<XenotypeMoodOverride> moodOverrides = new();
    public List<XenotypeActionBehaviorOverride> actionOverrides = new();

    public void ExposeData()
    {
        Scribe_Values.Look(ref raceDefName, "raceDefName", "");
        Scribe_Values.Look(ref xenotypeDefName, "xenotypeDefName", "");
        Scribe_Values.Look(ref hasOverallIntervalMultiplier, "hasOverallIntervalMultiplier", false);
        Scribe_Values.Look(ref overallIntervalMultiplier, "overallIntervalMultiplier", 1f);
        Scribe_Collections.Look(ref moodOverrides, "moodOverrides", LookMode.Deep);
        Scribe_Collections.Look(ref actionOverrides, "actionOverrides", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (moodOverrides == null) moodOverrides = new List<XenotypeMoodOverride>();
            if (actionOverrides == null) actionOverrides = new List<XenotypeActionBehaviorOverride>();
        }
    }

    internal static XenotypePresetRecord Clone(XenotypePresetRecord value) => new()
    {
        raceDefName = value.raceDefName,
        xenotypeDefName = value.xenotypeDefName,
        hasOverallIntervalMultiplier = value.hasOverallIntervalMultiplier,
        overallIntervalMultiplier = value.overallIntervalMultiplier,
        moodOverrides = (value.moodOverrides ?? new List<XenotypeMoodOverride>()).ConvertAll(XenotypeMoodOverride.Clone),
        actionOverrides = (value.actionOverrides ?? new List<XenotypeActionBehaviorOverride>()).ConvertAll(XenotypeActionBehaviorOverride.Clone)
    };
}
