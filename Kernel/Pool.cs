using System;
using System.Collections.Generic;

namespace SqueakyRatkin.Kernel;

// 池与链求值类型（§4.1）。SoundKey = SoundDef.defName 字符串（边界收敛：SoundDef 在适配层解析）。

public enum AgeBucket { Baby, Toddler, Child, Adult }

/// <summary>一个动作变体的声音集合。AgeTag null = 全年龄；IsEgg 由 SelectionContext 过滤。</summary>
public sealed class ActionSoundSet
{
    public readonly IReadOnlyList<string> SoundKeys;
    public readonly AgeBucket? AgeTag;
    public readonly float Weight;
    public readonly bool IsEgg;

    public ActionSoundSet(IReadOnlyList<string> soundKeys, AgeBucket? ageTag, float weight, bool isEgg = false)
    {
        SoundKeys = soundKeys;
        AgeTag = ageTag;
        Weight = weight;
        IsEgg = isEgg;
    }

    public bool HasSounds => SoundKeys != null && SoundKeys.Count > 0;
}

/// <summary>一个 VoicePack 在内核中的投影（PackKey 序数排序成员）。每个动作可有年龄变体。</summary>
public sealed class VoicePackEntry
{
    public readonly string PackKey;
    public readonly AudioDomain Domain;
    public readonly float Weight;
    public readonly IReadOnlyDictionary<string, IReadOnlyList<ActionSoundSet>> Actions;
    public readonly IReadOnlyDictionary<string, string>? PackFallback;

    public VoicePackEntry(string packKey, AudioDomain domain, float weight, IReadOnlyDictionary<string, IReadOnlyList<ActionSoundSet>> actions, IReadOnlyDictionary<string, string>? packFallback = null)
    {
        PackKey = packKey;
        Domain = domain;
        Weight = weight;
        Actions = actions;
        PackFallback = packFallback;
    }

    public bool TryGetAction(string actionKey, out IReadOnlyList<ActionSoundSet> sets) => Actions.TryGetValue(actionKey, out sets!);
}

public enum ChainTier { XenotypePack, RacePack, PackFallback, BuiltInFallback }

/// <summary>选择结果；SoundKey/Tier/PoolStableKey 全 null = 无声。IsEgg 标记所选条目的彩蛋身份
/// （0.3.2 日志装配适配；BuiltIn/PackFallback 恒 false，普通条目 false）。</summary>
public readonly struct ChainResult
{
    public static readonly ChainResult None = default;

    public readonly string? SoundKey;
    public readonly ChainTier? Tier;
    public readonly string? PoolStableKey;
    public readonly bool IsEgg;

    public bool IsNone => SoundKey == null;

    public ChainResult(string? soundKey, ChainTier? tier, string? poolStableKey, bool isEgg = false)
    {
        SoundKey = soundKey;
        Tier = tier;
        PoolStableKey = poolStableKey;
        IsEgg = isEgg;
    }
}

/// <summary>选择上下文。ActionKey：内置=枚举名，外部=packageId.defName；AllowEggs 是快照路由输入。</summary>
public readonly struct SelectionContext
{
    public readonly AudioDomain Domain;
    public readonly string ActionKey;
    public readonly AgeBucket Age;
    public readonly bool Production;
    public readonly bool AllowEggs;

    public SelectionContext(AudioDomain domain, string actionKey, AgeBucket age, bool production, bool allowEggs = false)
    {
        Domain = domain;
        ActionKey = actionKey;
        Age = age;
        Production = production;
        AllowEggs = allowEggs;
    }
}

/// <summary>随机源：适配层提供运行时随机数；预览可注入确定性来源。相同 roll 序列必得相同结果。</summary>
public interface IRollSource
{
    double Next01();
}

/// <summary>playability 函子：适配层包 SqueakSoundAvailabilityCache。</summary>
public interface ISoundGate
{
    bool Playable(string soundKey, SelectionContext ctx);
}
