using System;
using System.Collections.Generic;

namespace UniversalSqueaker.Kernel;

// 域键：AudioDomain(Race, Xenotype?) 取代旧 scope+targetDefName 字符串域键。
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

/// <summary>域状态分类：装配+选择存在=Available；
/// 未装配但选择存在=TargetUnavailable；装配但无选择=Orphan（域感知后由适配层调用）。</summary>
public static class AudioDomainStatuses
{
    public static AudioDomainStatus Classify(AudioDomain domain, bool assembled, bool selectionExists, bool biotechActive)
    {
        if (!biotechActive && domain.Xenotype != null) return AudioDomainStatus.Dormant;
        if (!assembled) return selectionExists ? AudioDomainStatus.TargetUnavailable : AudioDomainStatus.Orphan;
        return selectionExists ? AudioDomainStatus.Available : AudioDomainStatus.Orphan;
    }
}

/// <summary>域键工具：构造/校验/去重/排序 <see cref="AudioDomain"/>。只处理键，不判断音频归属。</summary>
public static class AudioDomains
{
    public static bool TryCreate(string? raceDefName, string? xenotypeDefName, out AudioDomain domain)
    {
        domain = default;
        if (string.IsNullOrEmpty(raceDefName)) return false;
        RaceKey race = new(raceDefName!);
        if (string.IsNullOrEmpty(xenotypeDefName))
        {
            domain = new AudioDomain(race, null);
            return true;
        }
        domain = new AudioDomain(race, new XenotypeKey(xenotypeDefName!));
        return true;
    }

    public static AudioDomain RaceOnly(string raceDefName) => new(new RaceKey(raceDefName), null);

    public static IReadOnlyList<AudioDomain> Collect(IEnumerable<(string race, string? xeno)> sources)
    {
        List<AudioDomain> result = new();
        HashSet<AudioDomain> seen = new();
        foreach ((string race, string? xeno) in sources ?? Array.Empty<(string, string?)>())
        {
            if (!TryCreate(race, xeno, out AudioDomain domain)) continue;
            if (seen.Add(domain)) result.Add(domain);
        }
        result.Sort(static (a, b) =>
        {
            int byRace = StringComparer.Ordinal.Compare(a.Race.DefName, b.Race.DefName);
            if (byRace != 0) return byRace;
            string aXeno = a.Xenotype?.DefName ?? "";
            string bXeno = b.Xenotype?.DefName ?? "";
            return StringComparer.Ordinal.Compare(aXeno, bXeno);
        });
        return result;
    }
}
