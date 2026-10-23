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

    public static bool IsValid(string? defName) => !string.IsNullOrWhiteSpace(defName);

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

public enum AudioDomainFamily { Pawn, NonPawn }

public readonly struct AudioDomain : IEquatable<AudioDomain>
{
    public readonly RaceKey Race;
    public readonly XenotypeKey? Xenotype;
    public readonly AudioDomainFamily Family;
    public readonly string? ObjectNamespace;
    public readonly string? ObjectTarget;
    public bool IsNonPawn => Family == AudioDomainFamily.NonPawn;

    public AudioDomain(RaceKey race, XenotypeKey? xenotype)
    {
        Race = race;
        Xenotype = xenotype;
        Family = AudioDomainFamily.Pawn;
        ObjectNamespace = null;
        ObjectTarget = null;
    }

    private AudioDomain(string objectNamespace, string target)
    {
        if (string.IsNullOrWhiteSpace(objectNamespace) || objectNamespace != objectNamespace.Trim()
            || string.IsNullOrWhiteSpace(target) || target != target.Trim())
        {
            throw new ArgumentException("NonPawn requires a namespace and exact target");
        }
        Family = AudioDomainFamily.NonPawn;
        ObjectNamespace = objectNamespace;
        ObjectTarget = target;
        Race = default;
        Xenotype = null;
    }

    public static AudioDomain NonPawn(string objectNamespace, string target) => new(objectNamespace, target);
    public AudioDomain Parent => IsNonPawn ? this : new AudioDomain(Race, null);

    public bool IsRaceOnly => Family == AudioDomainFamily.Pawn && Xenotype == null;

    public bool Equals(AudioDomain other)
        => Family == other.Family && Race.Equals(other.Race) && Xenotype.Equals(other.Xenotype)
            && ObjectNamespace == other.ObjectNamespace && ObjectTarget == other.ObjectTarget;
    public override bool Equals(object? obj) => obj is AudioDomain other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            return (Race.GetHashCode() * 397) ^ (Xenotype?.GetHashCode() ?? 0) ^ (ObjectNamespace?.GetHashCode() ?? 0) ^ (ObjectTarget?.GetHashCode() ?? 0) ^ (int)Family;
        }
    }
    public static bool operator ==(AudioDomain a, AudioDomain b) => a.Equals(b);
    public static bool operator !=(AudioDomain a, AudioDomain b) => !a.Equals(b);

    public override string ToString()
    {
        if (IsNonPawn)
        {
            return "NonPawn:" + Uri.EscapeDataString(ObjectNamespace!) + "/" + Uri.EscapeDataString(ObjectTarget!);
        }
        return Xenotype == null ? Race.DefName : Race.DefName + "+" + Xenotype.Value.DefName;
    }
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
        if (string.IsNullOrWhiteSpace(raceDefName)) return false;
        RaceKey race = new(raceDefName!);
        // 空字符串继续视为“无 xeno”（race-only）；纯空白 xeno 是非法 defName，拒绝。
        if (string.IsNullOrEmpty(xenotypeDefName))
        {
            domain = new AudioDomain(race, null);
            return true;
        }
        if (string.IsNullOrWhiteSpace(xenotypeDefName)) return false;
        domain = new AudioDomain(race, new XenotypeKey(xenotypeDefName!));
        return true;
    }

    public static AudioDomain RaceOnly(string raceDefName)
    {
        if (string.IsNullOrWhiteSpace(raceDefName))
            throw new ArgumentException("Race defName must not be null or whitespace.", nameof(raceDefName));
        return new AudioDomain(new RaceKey(raceDefName), null);
    }

    public static bool TryParseNonPawn(string value, out AudioDomain domain)
    {
        domain = default;
        const string prefix = "NonPawn:";
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        int separator = value.IndexOf('/', prefix.Length);
        if (separator < 0)
        {
            return false;
        }
        string scope = Uri.UnescapeDataString(value.Substring(prefix.Length, separator - prefix.Length));
        string target = Uri.UnescapeDataString(value.Substring(separator + 1));
        if (string.IsNullOrWhiteSpace(scope) || scope != scope.Trim() || string.IsNullOrWhiteSpace(target) || target != target.Trim())
        {
            return false;
        }
        domain = AudioDomain.NonPawn(scope, target);
        return domain.ToString() == value;
    }

    public static IReadOnlyList<AudioDomain> CollectDomains(IEnumerable<AudioDomain> domains)
    {
        var result = new List<AudioDomain>(new HashSet<AudioDomain>(domains));
        result.Sort(Compare);
        return result;
    }

    public static int Compare(AudioDomain left, AudioDomain right)
    {
        int family = left.Family.CompareTo(right.Family);
        if (family != 0)
        {
            return family;
        }
        if (left.IsNonPawn)
        {
            int scope = StringComparer.Ordinal.Compare(left.ObjectNamespace, right.ObjectNamespace);
            return scope != 0 ? scope : StringComparer.Ordinal.Compare(left.ObjectTarget, right.ObjectTarget);
        }
        int race = StringComparer.Ordinal.Compare(left.Race.DefName, right.Race.DefName);
        return race != 0 ? race : StringComparer.Ordinal.Compare(left.Xenotype?.DefName, right.Xenotype?.DefName);
    }

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
