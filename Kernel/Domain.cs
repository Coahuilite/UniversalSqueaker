using System;

namespace SqueakyRatkin.Kernel;

// 域键（§4.2）：AudioDomain(Race, Xenotype?) 取代旧 scope+targetDefName 字符串域键。
// 零游戏运行时引用；PoolKey 序数排序保证跨重建稳定（orphan 语义沿用）。
// 手写值相等（net472 无 record）：AudioDomain 是池字典键。

public readonly struct RaceKey : IEquatable<RaceKey>
{
    public readonly string DefName;

    public RaceKey(string defName) => DefName = defName;

    public static bool IsValid(string? defName) => !string.IsNullOrEmpty(defName);

    public bool Equals(RaceKey other) => string.Equals(DefName, other.DefName, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is RaceKey other && Equals(other);
    public override int GetHashCode() => DefName == null ? 0 : StringComparer.Ordinal.GetHashCode(DefName);
    public static bool operator ==(RaceKey a, RaceKey b) => a.Equals(b);
    public static bool operator !=(RaceKey a, RaceKey b) => !a.Equals(b);
    public override string ToString() => DefName ?? "";
}

public readonly struct XenotypeKey : IEquatable<XenotypeKey>
{
    public readonly string DefName;

    public XenotypeKey(string defName) => DefName = defName;

    public bool Equals(XenotypeKey other) => string.Equals(DefName, other.DefName, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is XenotypeKey other && Equals(other);
    public override int GetHashCode() => DefName == null ? 0 : StringComparer.Ordinal.GetHashCode(DefName);
    public static bool operator ==(XenotypeKey a, XenotypeKey b) => a.Equals(b);
    public static bool operator !=(XenotypeKey a, XenotypeKey b) => !a.Equals(b);
    public override string ToString() => DefName ?? "";
}

public readonly struct AudioDomain : IEquatable<AudioDomain>
{
    public readonly RaceKey Race;
    public readonly XenotypeKey? Xenotype;

    public AudioDomain(RaceKey race, XenotypeKey? xenotype)
    {
        Race = race;
        Xenotype = xenotype;
    }

    public bool IsRaceOnly => Xenotype == null;

    public bool Equals(AudioDomain other) => Race.Equals(other.Race) && Xenotype.Equals(other.Xenotype);
    public override bool Equals(object? obj) => obj is AudioDomain other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            return (Race.GetHashCode() * 397) ^ (Xenotype?.GetHashCode() ?? 0);
        }
    }
    public static bool operator ==(AudioDomain a, AudioDomain b) => a.Equals(b);
    public static bool operator !=(AudioDomain a, AudioDomain b) => !a.Equals(b);

    public override string ToString() => Xenotype == null ? Race.DefName : Race.DefName + "+" + Xenotype.Value.DefName;
}

public enum AudioDomainStatus { Available, Dormant, TargetUnavailable, Orphan }

/// <summary>域状态分类（§4.2 语义与旧 SqueakVoicePackDomainState 对齐）：装配+选择存在=Available；
/// 未装配但选择存在=TargetUnavailable；装配但无选择=Orphan（0.3.x 域感知后由适配层调用）。</summary>
public static class AudioDomainStatuses
{
    public static AudioDomainStatus Classify(AudioDomain domain, bool assembled, bool selectionExists, bool biotechActive)
    {
        if (!biotechActive && domain.Xenotype != null) return AudioDomainStatus.Dormant;
        if (!assembled) return selectionExists ? AudioDomainStatus.TargetUnavailable : AudioDomainStatus.Orphan;
        return selectionExists ? AudioDomainStatus.Available : AudioDomainStatus.Orphan;
    }
}
