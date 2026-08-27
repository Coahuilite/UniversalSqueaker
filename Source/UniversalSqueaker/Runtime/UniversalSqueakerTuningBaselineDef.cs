using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Read-only tuning-baseline preset Def: a layered tree of per-race and per-xenotype action/mood
/// baselines. The Def never writes back and never enters saves; empty source data is a valid
/// startup. Use the same data-driven pattern as <see cref="UniversalSqueakerFallbackProfileDef"/>:
/// no product literals, no race seeds, so third parties may ship their own preset Defs.
/// </summary>
public class UniversalSqueakerTuningBaselineDef : Def
{
    /// <summary>Human-facing preset name. Falls back to <see cref="Def.defName"/> when empty.</summary>
    public string presetLabel = "";

    /// <summary>Human-facing preset description shown in the import UI.</summary>
    public string presetDescription = "";

    /// <summary>Race-scoped baseline blocks. Each selected race imports into the Race layer.</summary>
    public List<BaselineRaceEntry> races = new();
}

/// <summary>One race's baseline block inside a preset.</summary>
public class BaselineRaceEntry
{
    public string raceDefName = "";
    public List<BaselineActionTuning> actions = new();
    public List<BaselineMoodTuning> moods = new();
    public List<BaselineXenotypeEntry> xenotypes = new();
}

/// <summary>One xenotype's baseline block nested under a race block.</summary>
public class BaselineXenotypeEntry
{
    public string xenotypeDefName = "";
    /// <summary>When true (default), the parent race's baseline is imported as this xenotype's base
    /// before the xenotype's own delta is applied on top.</summary>
    public bool inheritFromRace = true;
    public List<BaselineActionTuning> actions = new();
    public List<BaselineMoodTuning> moods = new();
}

/// <summary>One action's baseline tuning. Field semantics mirror <c>RuntimeActionDelta</c>.</summary>
public class BaselineActionTuning
{
    public string actionKey = "";                       // 内置键 = BuiltInActionKeys；外部键 = 注册方声明
    public SqueakActionScope scope = SqueakActionScope.AnyOccurrence;
    public float intervalMultiplier = 1f;
    public float probabilityMultiplier = 1f;
}

/// <summary>One mood's baseline modulation. Field semantics mirror <c>SqueakMoodMod</c>.</summary>
public class BaselineMoodTuning
{
    public SqueakMood mood = SqueakMood.Neutral;
    public float pitchFactor = 1f;
    public float volumeFactor = 1f;
    public FloatRange pitchJitter = FloatRange.One;
}
