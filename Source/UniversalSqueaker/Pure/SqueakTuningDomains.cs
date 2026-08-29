using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker;

/// <summary>
/// 双键调音域聚合与 context 选择纯逻辑（零 Verse）。
/// 职责：
///   1. 按 <see cref="AudioDomain"/> 聚合动作/心情/overall multiplier 源；
///   2. 根据 pawn 的 (race, xeno) 身份选择应使用的 context 域。
/// 外部适配层负责把 Scribe/Def 数据投影成纯输入，再调用本模块。
/// </summary>
public sealed class LayerBehaviorAggregate
{
    public float OverallIntervalMultiplier = 1f;
    public readonly Dictionary<string, LayerActionDelta> Actions = new(StringComparer.Ordinal);
    public readonly Dictionary<SqueakMood, LayerMoodDelta> Moods = new();

    internal void ApplyAction(string key, LayerActionDelta delta)
    {
        if (Actions.TryGetValue(key, out LayerActionDelta existing)) Actions[key] = existing.Merge(delta);
        else Actions[key] = delta;
    }

    internal void ApplyMood(SqueakMood mood, LayerMoodDelta delta)
    {
        if (Moods.TryGetValue(mood, out LayerMoodDelta existing)) Moods[mood] = existing.Merge(delta);
        else Moods[mood] = delta;
    }
}

public static class SqueakTuningAggregator
{
    public static IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> Aggregate(
        IEnumerable<(AudioDomain domain, string actionKey, LayerActionDelta delta)> actions,
        IEnumerable<(AudioDomain domain, SqueakMood mood, LayerMoodDelta delta)> moods,
        IEnumerable<(AudioDomain domain, float overallMultiplier)> multipliers)
    {
        Dictionary<AudioDomain, LayerBehaviorAggregate> result = new();

        foreach ((AudioDomain domain, string actionKey, LayerActionDelta delta) in actions ?? Array.Empty<(AudioDomain, string, LayerActionDelta)>())
        {
            if (string.IsNullOrEmpty(actionKey)) continue;
            if (!result.TryGetValue(domain, out LayerBehaviorAggregate? aggregate))
            {
                aggregate = new LayerBehaviorAggregate();
                result.Add(domain, aggregate);
            }
            aggregate.ApplyAction(actionKey, delta);
        }

        foreach ((AudioDomain domain, SqueakMood mood, LayerMoodDelta delta) in moods ?? Array.Empty<(AudioDomain, SqueakMood, LayerMoodDelta)>())
        {
            if (!result.TryGetValue(domain, out LayerBehaviorAggregate? aggregate))
            {
                aggregate = new LayerBehaviorAggregate();
                result.Add(domain, aggregate);
            }
            aggregate.ApplyMood(mood, delta);
        }

        foreach ((AudioDomain domain, float overallMultiplier) in multipliers ?? Array.Empty<(AudioDomain, float)>())
        {
            if (!result.TryGetValue(domain, out LayerBehaviorAggregate? aggregate))
            {
                aggregate = new LayerBehaviorAggregate();
                result.Add(domain, aggregate);
            }
            aggregate.OverallIntervalMultiplier = SanitizeOverallMultiplier(overallMultiplier);
        }

        return result;
    }

    /// <summary>与 resolver 的 Sanitize 一致：NaN/Infinity → 1f，其余 clamp 到 ≥0。</summary>
    private static float SanitizeOverallMultiplier(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, value);
}

public enum ContextSelectionKind { Global, Race, Xeno }

public readonly struct ContextSelection
{
    public readonly ContextSelectionKind Kind;
    public readonly AudioDomain? XenoDomain;

    public ContextSelection(ContextSelectionKind kind, AudioDomain? xenoDomain = null)
    {
        Kind = kind;
        XenoDomain = xenoDomain;
    }

    public static ContextSelection Global => new(ContextSelectionKind.Global);
    public static ContextSelection Race => new(ContextSelectionKind.Race);
    public static ContextSelection Xeno(AudioDomain domain) => new(ContextSelectionKind.Xeno, domain);
}

public static class SqueakContextSelector
{
    /// <summary>
    /// 选择 pawn 应使用的调音 context：
    /// 1. race 为空 → Global；
    /// 2. 无 Biotech 或 xeno 为空 → Race（有 raceContext）否则 Global；
    /// 3. (race, xeno) 不在可用域 → Race / Global，绝不跨 race；
    /// 4. xeno 在 ambiguous 集合 → Global（fail-closed）；
    /// 5. 命中 (race, xeno) → Xeno。
    /// </summary>
    public static ContextSelection Select(
        string raceDefName,
        string? xenoDefName,
        IEnumerable<AudioDomain> availableXenoDomains,
        IReadOnlyCollection<string> ambiguousXenoNames,
        bool hasRaceContext)
    {
        ContextSelection raceOrGlobal = hasRaceContext ? ContextSelection.Race : ContextSelection.Global;
        if (string.IsNullOrEmpty(raceDefName)) return ContextSelection.Global;
        if (string.IsNullOrEmpty(xenoDefName)) return raceOrGlobal;
        if (ambiguousXenoNames != null && ambiguousXenoNames.Contains(xenoDefName!)) return ContextSelection.Global;
        if (availableXenoDomains == null) return raceOrGlobal;
        foreach (AudioDomain domain in availableXenoDomains)
        {
            if (domain.Xenotype != null
                && string.Equals(domain.Race.DefName, raceDefName, StringComparison.Ordinal)
                && string.Equals(domain.Xenotype.Value.DefName, xenoDefName, StringComparison.Ordinal))
            {
                return ContextSelection.Xeno(domain);
            }
        }
        return raceOrGlobal;
    }
}
