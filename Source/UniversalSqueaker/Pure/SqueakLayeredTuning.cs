using System;

namespace UniversalSqueaker;

/// <summary>
/// 分层调音纯折叠模块（零 Verse，kernel 测试门直接覆盖）。
/// 层优先级：DefaultScope < Global < Race < Xenotype；字段级 last-wins（HasX=false 继承底层）。
/// 记录面（ActionTuningRecord / MoodTuningRecord）由适配层投影为 Layer* 源数据（From* 转换在
/// 适配层，保持本模块零 Verse）；本模块只做纯折叠与合并，供 kernel 测试直接断言语义。
/// </summary>
public readonly struct LayerActionDelta
{
    public readonly SqueakActionScope Scope;
    public readonly bool HasScope;
    public readonly bool HasIntervalMultiplier;
    public readonly float IntervalMultiplier;
    public readonly bool HasProbabilityMultiplier;
    public readonly float ProbabilityMultiplier;

    public LayerActionDelta(SqueakActionScope scope, bool hasScope, bool hasIntervalMultiplier, float intervalMultiplier, bool hasProbabilityMultiplier, float probabilityMultiplier)
    {
        Scope = scope;
        HasScope = hasScope;
        HasIntervalMultiplier = hasIntervalMultiplier;
        IntervalMultiplier = intervalMultiplier;
        HasProbabilityMultiplier = hasProbabilityMultiplier;
        ProbabilityMultiplier = probabilityMultiplier;
    }

    public bool IsEmpty => !HasScope && !HasIntervalMultiplier && !HasProbabilityMultiplier;

    /// <summary>同层多记录字段级合并（后写覆盖先写字段；HasX 取并集）。</summary>
    public LayerActionDelta Merge(LayerActionDelta other) => new(
        other.HasScope ? other.Scope : Scope,
        HasScope || other.HasScope,
        HasIntervalMultiplier || other.HasIntervalMultiplier,
        other.HasIntervalMultiplier ? other.IntervalMultiplier : IntervalMultiplier,
        HasProbabilityMultiplier || other.HasProbabilityMultiplier,
        other.HasProbabilityMultiplier ? other.ProbabilityMultiplier : ProbabilityMultiplier);

    /// <summary>单层覆盖：本层显式字段覆盖底层，未显式字段继承底层（空层 = 恒等）。</summary>
    public ResolvedActionDelta ApplyOver(ResolvedActionDelta baseDelta) => new(
        HasScope ? Scope : baseDelta.Scope,
        HasIntervalMultiplier ? IntervalMultiplier : baseDelta.IntervalMultiplier,
        HasProbabilityMultiplier ? ProbabilityMultiplier : baseDelta.ProbabilityMultiplier);
}

/// <summary>层链折叠后的具体动作调音（字段全部具体化，无 HasX）。</summary>
public readonly struct ResolvedActionDelta
{
    public readonly SqueakActionScope Scope;
    public readonly float IntervalMultiplier;
    public readonly float ProbabilityMultiplier;

    public bool Enabled => Scope != SqueakActionScope.Disabled;

    public ResolvedActionDelta(SqueakActionScope scope, float intervalMultiplier, float probabilityMultiplier)
    {
        Scope = scope;
        IntervalMultiplier = intervalMultiplier;
        ProbabilityMultiplier = probabilityMultiplier;
    }

    /// <summary>层链折叠：DefaultScope &lt; Global &lt; Race &lt; Xenotype（null = 该层无记录，跳过）。
    /// 空层记录经 ApplyOver 自然恒等，不吞继承。</summary>
    public static ResolvedActionDelta Resolve(SqueakActionScope defaultScope, LayerActionDelta? global, LayerActionDelta? race, LayerActionDelta? xeno)
    {
        ResolvedActionDelta current = new(defaultScope, 1f, 1f);
        if (global.HasValue) current = global.Value.ApplyOver(current);
        if (race.HasValue) current = race.Value.ApplyOver(current);
        if (xeno.HasValue) current = xeno.Value.ApplyOver(current);
        return current;
    }
}

/// <summary>分层心情源数据（jitter 用 (min,max) 对，绕开 Verse.FloatRange，保持零 Verse）。</summary>
public readonly struct LayerMoodDelta
{
    public readonly bool HasPitchFactor;
    public readonly float PitchFactor;
    public readonly bool HasVolumeFactor;
    public readonly float VolumeFactor;
    public readonly bool HasPitchJitter;
    public readonly float JitterMin;
    public readonly float JitterMax;

    public LayerMoodDelta(bool hasPitchFactor, float pitchFactor, bool hasVolumeFactor, float volumeFactor, bool hasPitchJitter, float jitterMin, float jitterMax)
    {
        HasPitchFactor = hasPitchFactor;
        PitchFactor = pitchFactor;
        HasVolumeFactor = hasVolumeFactor;
        VolumeFactor = volumeFactor;
        HasPitchJitter = hasPitchJitter;
        JitterMin = jitterMin;
        JitterMax = jitterMax;
    }

    public bool IsEmpty => !HasPitchFactor && !HasVolumeFactor && !HasPitchJitter;

    /// <summary>同层多记录字段级合并（后写覆盖先写字段；HasX 取并集）。</summary>
    public LayerMoodDelta Merge(LayerMoodDelta other) => new(
        HasPitchFactor || other.HasPitchFactor,
        other.HasPitchFactor ? other.PitchFactor : PitchFactor,
        HasVolumeFactor || other.HasVolumeFactor,
        other.HasVolumeFactor ? other.VolumeFactor : VolumeFactor,
        HasPitchJitter || other.HasPitchJitter,
        other.HasPitchJitter ? other.JitterMin : JitterMin,
        other.HasPitchJitter ? other.JitterMax : JitterMax);

    /// <summary>单层覆盖：本层显式因子覆盖底层，未显式因子继承底层（HasX 沿链合并）。</summary>
    public ResolvedMoodDelta ApplyOver(ResolvedMoodDelta baseDelta) => new(
        baseDelta.HasPitchFactor || HasPitchFactor,
        HasPitchFactor ? PitchFactor : baseDelta.PitchFactor,
        baseDelta.HasVolumeFactor || HasVolumeFactor,
        HasVolumeFactor ? VolumeFactor : baseDelta.VolumeFactor,
        baseDelta.HasPitchJitter || HasPitchJitter,
        HasPitchJitter ? JitterMin : baseDelta.JitterMin,
        HasPitchJitter ? JitterMax : baseDelta.JitterMax);
}

/// <summary>层链折叠后的心情调音（HasX = 该因子是否被任何层显式决定；GetMoodDelta 语义：全默认 = 无 delta）。</summary>
public readonly struct ResolvedMoodDelta
{
    public readonly bool HasPitchFactor;
    public readonly float PitchFactor;
    public readonly bool HasVolumeFactor;
    public readonly float VolumeFactor;
    public readonly bool HasPitchJitter;
    public readonly float JitterMin;
    public readonly float JitterMax;

    public bool IsDefault => !HasPitchFactor && !HasVolumeFactor && !HasPitchJitter;

    public ResolvedMoodDelta(bool hasPitchFactor, float pitchFactor, bool hasVolumeFactor, float volumeFactor, bool hasPitchJitter, float jitterMin, float jitterMax)
    {
        HasPitchFactor = hasPitchFactor;
        PitchFactor = pitchFactor;
        HasVolumeFactor = hasVolumeFactor;
        VolumeFactor = volumeFactor;
        HasPitchJitter = hasPitchJitter;
        JitterMin = jitterMin;
        JitterMax = jitterMax;
    }

    /// <summary>层链折叠（默认 1/1/(1,1)）；null = 该层无记录，跳过。</summary>
    public static ResolvedMoodDelta Resolve(LayerMoodDelta? global, LayerMoodDelta? race, LayerMoodDelta? xeno)
    {
        ResolvedMoodDelta current = new(false, 1f, false, 1f, false, 1f, 1f);
        if (global.HasValue) current = global.Value.ApplyOver(current);
        if (race.HasValue) current = race.Value.ApplyOver(current);
        if (xeno.HasValue) current = xeno.Value.ApplyOver(current);
        return current;
    }
}