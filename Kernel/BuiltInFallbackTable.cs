using System;
using System.Collections.Generic;

namespace SqueakyRatkin.Kernel;

/// <summary>
/// Field-presence fallback override. A listed action key is an explicit player override even when
/// its value equals the source value; omitted keys continue to inherit the source profile.
/// </summary>
public sealed class FallbackDelta
{
    public readonly IReadOnlyDictionary<string, string> Overrides;

    public FallbackDelta(IReadOnlyDictionary<string, string> overrides)
    {
        Overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
        foreach (KeyValuePair<string, string> entry in Overrides)
        {
            if (!BuiltInActionKeys.Contains(entry.Key))
                throw new ArgumentException("Fallback delta contains a non-built-in action key: " + entry.Key, nameof(overrides));
            if (string.IsNullOrWhiteSpace(entry.Value))
                throw new ArgumentException("Fallback delta contains an empty sound key for action: " + entry.Key, nameof(overrides));
        }
    }

    public bool IsEmpty => Overrides.Count == 0;
}

public enum CopyDisposition
{
    KeepCopy,
    RebuildFromSource,
    MergeDelta,
}

/// <summary>Pure Config-copy lifecycle decisions. File absence is normalized by the store to corrupt=true.</summary>
public static class FallbackProfileOperations
{
    /// <summary>
    /// Corrupt or older copies are replaced from the current source. A version-current copy with
    /// a field-presence delta is merged; a version-current copy without one is already the source.
    /// </summary>
    public static CopyDisposition DecideCopy(FallbackProfile source, FallbackDelta? delta, int copyVersion, bool copyCorrupt)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (copyCorrupt || copyVersion < source.Version) return CopyDisposition.RebuildFromSource;
        return delta != null ? CopyDisposition.MergeDelta : CopyDisposition.KeepCopy;
    }

    /// <summary>Returns a new profile with the source's race/version and source keys overridden per present delta key.</summary>
    public static FallbackProfile Merge(FallbackProfile source, FallbackDelta delta)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (delta == null) throw new ArgumentNullException(nameof(delta));
        Dictionary<string, string> merged = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in source.SoundKeys) merged.Add(entry.Key, entry.Value);
        foreach (KeyValuePair<string, string> entry in delta.Overrides) merged[entry.Key] = entry.Value;
        return new FallbackProfile(source.Race, source.Version, merged);
    }
}

/// <summary>
/// 内置 fallback 单源表（§4.6，内核 C# 编译期冻结）。profile 的动作键始终是字符串，
/// 并由 <see cref="BuiltInActionKeys"/> 封闭校验；0.3.1 的 Ratkin 种子镜像 15 个
/// <c>SqueakActionDefinitions.AudioKey</c>，Crying/Giggling 无内置 SoundDef 映射（pack 声明才发声，
/// 内置表不列条目，§4.7）。
/// </summary>
public sealed class FallbackProfile
{
    public readonly RaceKey Race;
    public readonly int Version;
    public readonly IReadOnlyDictionary<string, string> SoundKeys;

    public FallbackProfile(RaceKey race, int version, IReadOnlyDictionary<string, string> soundKeys)
    {
        Race = race;
        Version = version;
        SoundKeys = soundKeys ?? throw new ArgumentNullException(nameof(soundKeys));
        foreach (KeyValuePair<string, string> entry in SoundKeys)
        {
            if (!BuiltInActionKeys.Contains(entry.Key))
                throw new ArgumentException("Fallback profile contains a non-built-in action key: " + entry.Key, nameof(soundKeys));
            if (string.IsNullOrWhiteSpace(entry.Value))
                throw new ArgumentException("Fallback profile contains an empty sound key for action: " + entry.Key, nameof(soundKeys));
        }
    }

    /// <summary>纯字符串查表：只有内置清单内的键可以命中 profile。</summary>
    public bool TryGetSoundKey(string actionKey, out string? soundKey)
    {
        soundKey = null;
        return BuiltInActionKeys.Contains(actionKey) && SoundKeys.TryGetValue(actionKey, out soundKey);
    }
}
/// <summary>
/// Compile-time formal fallback data. It intentionally does not reference product action metadata:
/// the 15 shipped mappings are explicit, while Crying/Giggling have no built-in SoundDef (default silence).
/// </summary>
public static class BuiltInFallbackCatalog
{
    public const int RatkinProfileVersion = 1;

    public static BuiltInFallbackTable Create(string ratkinRaceDefName)
    {
        Dictionary<string, string> keys = new(StringComparer.Ordinal)
        {
            ["Call"] = "SR_Call",
            ["Eat"] = "SR_Eat",
            ["Sleep"] = "SR_Sleep",
            ["Wounded"] = "SR_Wounded",
            ["Select"] = "SR_Select",
            ["Move"] = "SR_Move",
            ["Social"] = "SR_Social",
            ["Joy"] = "SR_Joy",
            ["Death"] = "SR_Death",
            ["Draft"] = "SR_Draft",
            ["Undraft"] = "SR_Undraft",
            ["Attack"] = "SR_Attack",
            ["Work"] = "SR_Work",
            ["Equip"] = "SR_Equip",
            ["MentalBreak"] = "SR_MentalBreak",
        };
        return new BuiltInFallbackTable(new[] { new FallbackProfile(new RaceKey(ratkinRaceDefName), RatkinProfileVersion, keys) });
    }
}


public sealed class BuiltInFallbackTable
{
    public static readonly BuiltInFallbackTable Empty = new(Array.Empty<FallbackProfile>());

    private readonly IReadOnlyDictionary<RaceKey, FallbackProfile> byRace;

    public BuiltInFallbackTable(IReadOnlyList<FallbackProfile> profiles)
    {
        Dictionary<RaceKey, FallbackProfile> map = new();
        foreach (FallbackProfile profile in profiles ?? Array.Empty<FallbackProfile>())
        {
            if (profile == null) continue;
            map[profile.Race] = profile;
        }
        byRace = map;
    }
    public IEnumerable<FallbackProfile> Profiles => byRace.Values;

    public FallbackProfile? For(RaceKey race) => byRace.TryGetValue(race, out FallbackProfile? profile) ? profile : null;

    /// <summary>Select 内置 tier 查表：字符串 action key 经内置清单校验后映射到 profile SoundDef key。</summary>
    public bool TryGetSoundKey(RaceKey race, string actionKey, out string? soundKey)
    {
        soundKey = null;
        FallbackProfile? profile = For(race);
        return profile != null && profile.TryGetSoundKey(actionKey, out soundKey);
    }
}

/// <summary>域过滤器（§4.4，0.4.x 删除）。0.3.0 = Everything；0.3.1 配置化白名单。</summary>
public sealed class DomainFilter
{
    public static readonly DomainFilter Everything = new(null);

    private readonly ISet<RaceKey>? allowed;

    public DomainFilter(ISet<RaceKey>? allowed) => this.allowed = allowed;

    public bool Contains(RaceKey race) => allowed == null || allowed.Contains(race);
}
