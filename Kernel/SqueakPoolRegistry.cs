using System;
using System.Collections.Generic;

namespace SqueakyRatkin.Kernel;

/// <summary>一个域的池：条目按 PackKey 序数排序（跨重建稳定，orphan 语义沿用旧 GetSelected 的 Sort）。</summary>
public sealed class DomainPool
{
    public readonly AudioDomain Domain;
    public readonly IReadOnlyList<VoicePackEntry> Entries;

    internal DomainPool(AudioDomain domain, List<VoicePackEntry> entries)
    {
        Domain = domain;
        entries.Sort((a, b) => StringComparer.Ordinal.Compare(a.PackKey, b.PackKey));
        Entries = entries;
    }
}

/// <summary>
/// 不可变选择注册表（§4.1/§4.2）。0.3.0 语义规范 = 0.2.4 SqueakRuntimeSnapshot.Choose + ChoosePack
/// （等价评审锚）：
///   Off       → 仅内置表（旧 vanilla 字典）
///   Fallback  → XenotypePack → RacePack → PackFallback → BuiltInFallback 四级短路
///   Remix     → 四级（非 None）等权折叠，固定序 [xeno, race, pack fallback, builtin]
///   entry 级过滤 = 年龄变体选定 → egg 资格 → gate；sound 级过滤 = 旧 pack.Choose 内过滤；
///   等权抽取 = 旧 Rand.Range(0, N) 的分布等价（rolls.Next01() * N 取整）。
/// 0.3.1 带权 = entry.Weight 累计权重；ageTag 未声明、egg 关闭、无 PackFallback 时严格保留 0.3.0 行为。
/// </summary>
public sealed class SqueakPoolRegistry
{
    private readonly IReadOnlyDictionary<AudioDomain, DomainPool> poolsByDomain;
    private readonly BuiltInFallbackTable builtIn;
    private readonly DomainFilter filter;

    public static SqueakPoolRegistry Empty => new(Array.Empty<VoicePackEntry>(), BuiltInFallbackTable.Empty, DomainFilter.Everything);

    public SqueakPoolRegistry(IReadOnlyList<VoicePackEntry> entries, BuiltInFallbackTable builtIn, DomainFilter filter)
    {
        this.builtIn = builtIn ?? throw new ArgumentNullException(nameof(builtIn));
        this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
        Dictionary<AudioDomain, List<VoicePackEntry>> grouped = new();
        foreach (VoicePackEntry entry in entries ?? Array.Empty<VoicePackEntry>())
        {
            if (entry == null || entry.PackKey == null || entry.Actions == null) continue;
            if (!filter.Contains(entry.Domain.Race)) continue;
            if (!IsPositiveFinite(entry.Weight)) continue;
            if (!grouped.TryGetValue(entry.Domain, out List<VoicePackEntry>? list))
            {
                list = new List<VoicePackEntry>();
                grouped.Add(entry.Domain, list);
            }
            list.Add(entry);
        }
        Dictionary<AudioDomain, DomainPool> pools = new();
        foreach (KeyValuePair<AudioDomain, List<VoicePackEntry>> group in grouped) pools.Add(group.Key, new DomainPool(group.Key, group.Value));
        poolsByDomain = pools;
    }

    /// <summary>选择链求值。<see cref="SelectionMode"/> 是内核 API，适配层负责从设置枚举映射。</summary>
    public ChainResult Select(SelectionContext ctx, SelectionMode mode, ISoundGate gate, IRollSource rolls)
    {
        if (gate == null) throw new ArgumentNullException(nameof(gate));
        if (rolls == null) throw new ArgumentNullException(nameof(rolls));
        ChainResult vanilla = SelectBuiltIn(ctx, gate);
        if (mode == SelectionMode.Off) return vanilla;

        ChainResult x = ctx.Domain.Xenotype != null
            ? SelectTier(PoolFor(ctx.Domain), ChainTier.XenotypePack, ctx, gate, rolls)
            : ChainResult.None;
        ChainResult r = SelectTier(PoolFor(new AudioDomain(ctx.Domain.Race, null)), ChainTier.RacePack, ctx, gate, rolls);

        if (mode == SelectionMode.Fallback)
        {
            if (!x.IsNone) return x;
            if (!r.IsNone) return r;
            ChainResult fallback = SelectPackFallback(PoolFor(ctx.Domain), ctx, gate, rolls);
            return !fallback.IsNone ? fallback : vanilla;
        }

        // Preserve the frozen default-state draw stream only when this action has no declared fallback.
        // A declared but currently gated fallback remains a fourth, non-playable tier and is skipped.
        DomainPool? fallbackPool = PoolFor(ctx.Domain);
        if (!HasPackFallback(fallbackPool, ctx.ActionKey)) return SelectRemixThree(x, r, vanilla, rolls);
        ChainResult p = SelectPackFallback(fallbackPool, ctx, gate, rolls);
        return SelectRemixFour(x, r, p, vanilla, rolls);
    }

    private static ChainResult SelectRemixFour(ChainResult x, ChainResult r, ChainResult p, ChainResult vanilla, IRollSource rolls)
    {
        ChainResult tier0 = x, tier1 = r, tier2 = p, tier3 = vanilla;
        int count = 0;
        if (!tier0.IsNone) count++;
        if (!tier1.IsNone) count++;
        if (!tier2.IsNone) count++;
        if (!tier3.IsNone) count++;
        if (count == 0) return ChainResult.None;
        if (count == 1)
        {
            if (!tier0.IsNone) return tier0;
            if (!tier1.IsNone) return tier1;
            return !tier2.IsNone ? tier2 : tier3;
        }
        int index = RollIndex(count, rolls);
        if (!tier0.IsNone && index-- == 0) return tier0;
        if (!tier1.IsNone && index-- == 0) return tier1;
        if (!tier2.IsNone && index-- == 0) return tier2;
        return tier3;
    }

    public DomainPool? PoolFor(AudioDomain domain) => poolsByDomain.TryGetValue(domain, out DomainPool? pool) ? pool : null;

    private static ChainResult SelectRemixThree(ChainResult x, ChainResult r, ChainResult vanilla, IRollSource rolls)
    {
        ChainResult tier0 = x, tier1 = r, tier2 = vanilla;
        int count = 0;
        if (!tier0.IsNone) count++;
        if (!tier1.IsNone) count++;
        if (!tier2.IsNone) count++;
        if (count == 0) return ChainResult.None;
        if (count == 1) return !tier0.IsNone ? tier0 : (!tier1.IsNone ? tier1 : tier2);
        int index = RollIndex(count, rolls);
        return index switch
        {
            0 => tier0,
            1 => tier1,
            _ => tier2,
        };
    }


    /// <summary>诊断/编辑器枚举（§4.1 PoolsFor）。</summary>
    public IReadOnlyList<DomainPool> PoolsFor(AudioDomain domain)
    {
        DomainPool? pool = PoolFor(domain);
        return pool == null ? Array.Empty<DomainPool>() : new[] { pool };
    }

    private static bool HasPackFallback(DomainPool? pool, string actionKey)
    {
        if (pool == null) return false;
        foreach (VoicePackEntry entry in pool.Entries)
            if (entry.PackFallback != null && entry.PackFallback.ContainsKey(actionKey)) return true;
        return false;
    }

    /// <summary>内置表 tier：TryParseBuiltIn（内置键 ↔ 枚举名一致性由 validator 双向锁）+ profile 键 + gate。</summary>
    private ChainResult SelectBuiltIn(SelectionContext ctx, ISoundGate gate)
    {
        if (!builtIn.TryGetSoundKey(ctx.Domain.Race, ctx.ActionKey, out string? key) || key == null) return ChainResult.None;
        return gate.Playable(key, ctx) ? new ChainResult(key, ChainTier.BuiltInFallback, null) : ChainResult.None;
    }

    private static ChainResult SelectTier(DomainPool? pool, ChainTier tier, SelectionContext ctx, ISoundGate gate, IRollSource rolls)
    {
        if (pool == null || pool.Entries.Count == 0) return ChainResult.None;
        // 每个 pack 先按年龄选一个变体，再按 egg 资格与 playability 过滤；绝不跨变体回退。
        List<VoicePackEntry> valid = new(pool.Entries.Count);
        Dictionary<VoicePackEntry, ActionSoundSet> selectedSets = new();
        foreach (VoicePackEntry entry in pool.Entries)
        {
            if (!entry.TryGetAction(ctx.ActionKey, out IReadOnlyList<ActionSoundSet>? variants)) continue;
            ActionSoundSet? set = SelectVariant(variants, ctx);
            if (set == null || !set.HasSounds || (set.IsEgg && !ctx.AllowEggs)) continue;
            if (!HasPlayableKey(set, ctx, gate)) continue;
            valid.Add(entry);
            selectedSets.Add(entry, set);
        }
        if (valid.Count == 0) return ChainResult.None;
        VoicePackEntry chosen = DrawEntry(valid, rolls);
        ActionSoundSet chosenSet = selectedSets[chosen];
        string? key = DrawSoundKey(chosenSet, ctx, gate, rolls);
        return key == null ? ChainResult.None : new ChainResult(key, tier, chosen.PackKey, chosenSet.IsEgg);
    }

    /// <summary>PackFallback 始终求 ctx.Domain 的精确池；xeno ctx 不越域读取 race fallback。</summary>
    private static ChainResult SelectPackFallback(DomainPool? pool, SelectionContext ctx, ISoundGate gate, IRollSource rolls)
    {
        if (pool == null || pool.Entries.Count == 0) return ChainResult.None;
        List<VoicePackEntry> valid = new(pool.Entries.Count);
        foreach (VoicePackEntry entry in pool.Entries)
        {
            if (entry.PackFallback == null || !entry.PackFallback.TryGetValue(ctx.ActionKey, out string? key) || key == null) continue;
            if (gate.Playable(key, ctx)) valid.Add(entry);
        }
        if (valid.Count == 0) return ChainResult.None;
        VoicePackEntry chosen = DrawEntry(valid, rolls);
        string? fallbackKey = chosen.PackFallback![ctx.ActionKey];
        return new ChainResult(fallbackKey, ChainTier.PackFallback, chosen.PackKey);
    }

    /// <summary>exact age 优先；exact 存在即使 egg 关闭或全 mute 也不退回 all-age。</summary>
    private static ActionSoundSet? SelectVariant(IReadOnlyList<ActionSoundSet>? variants, SelectionContext ctx)
    {
        if (variants == null) return null;
        foreach (ActionSoundSet set in variants)
            if (set != null && set.AgeTag == ctx.Age) return set;
        foreach (ActionSoundSet set in variants)
            if (set != null && set.AgeTag == null) return set;
        return null;
    }

    private static bool HasPlayableKey(ActionSoundSet set, SelectionContext ctx, ISoundGate gate)
    {
        foreach (string soundKey in set.SoundKeys)
        {
            if (soundKey != null && gate.Playable(soundKey, ctx)) return true;
        }
        return false;
    }

    private static VoicePackEntry DrawEntry(List<VoicePackEntry> valid, IRollSource rolls)
    {
        if (valid.Count == 1) return valid[0];
        // 构造期已过滤无效 Weight，此处权重恒为有限正数。
        double total = 0;
        foreach (VoicePackEntry entry in valid) total += entry.Weight;
        double roll = rolls.Next01() * total;
        double cumulative = 0;
        foreach (VoicePackEntry entry in valid)
        {
            cumulative += entry.Weight;
            if (roll < cumulative) return entry;
        }
        return valid[valid.Count - 1];
    }

    private static string? DrawSoundKey(ActionSoundSet set, SelectionContext ctx, ISoundGate gate, IRollSource rolls)
    {
        if (set.SoundKeys == null || set.SoundKeys.Count == 0) return null;
        // sound 级过滤（= 旧 pack.Choose 内过滤）：逐个 playable 等权抽取。
        List<string> playable = new(set.SoundKeys.Count);
        foreach (string soundKey in set.SoundKeys)
        {
            if (soundKey != null && gate.Playable(soundKey, ctx)) playable.Add(soundKey);
        }
        if (playable.Count == 0) return null;
        return playable.Count == 1 ? playable[0] : playable[RollIndex(playable.Count, rolls)];
    }

    /// <summary>等权索引：floor(Next01() * count)，clamp 到 count-1（分布等价旧 Rand.Range(0, count)）。</summary>
    private static int RollIndex(int count, IRollSource rolls)
    {
        int index = (int)(rolls.Next01() * count);
        if (index < 0) index = 0;
        if (index >= count) index = count - 1;
        return index;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
