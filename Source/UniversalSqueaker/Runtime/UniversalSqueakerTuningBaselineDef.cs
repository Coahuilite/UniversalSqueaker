using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Read-only tuning-baseline Def: the player-tunable baseline overlap introduced by S4.
/// XML Defs of this class declare per-action scope/interval/probability baselines and per-mood
/// modulation baselines. The Def never writes back and never enters saves; empty source data is a
/// valid startup (callers fall back to built-in defaults). Use the same data-driven pattern as
/// <see cref="UniversalSqueakerFallbackProfileDef"/>: no product literals, no race seeds, so third
/// parties may ship their own baseline Defs to override the default.
/// </summary>
public class UniversalSqueakerTuningBaselineDef : Def
{
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
