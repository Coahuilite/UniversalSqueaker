using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker;

/// <summary>
/// 内核↔适配层接缝：完整AudioId编码键进入原选择器，SoundDef仅从已验证最终对象绑定解析；
/// ISoundGate核对动态资格，Verse.Rand收敛为IRollSource；
/// catalog 域包投影为 VoicePackEntry[]。域身份端到端 = AudioDomain（记录自身 raceDefName/xenotypeDefName），
/// 无内置种族特判。Race 候选按 pack 声明的 raceDefName 精确匹配域，跨 race 池隔离。
/// </summary>
internal static class SqueakKernelAdapter
{
    private sealed class KernelGate : ISoundGate
    {
        private readonly Pawn? pawn;
        private readonly Map? map;
        private readonly TargetInfo? target;

        public KernelGate(Pawn? pawn, Map? map, TargetInfo? target)
        {
            this.pawn = pawn;
            this.map = map;
            this.target = target;
        }

        public bool Playable(string soundKey, SelectionContext ctx)
        {
            if (soundKey == null) return false;
            SoundDef? sound = UsAudioBindings.Resolve(soundKey);
            if (sound == null) return false;
            return (ctx.Production ? SqueakSoundAvailabilityCache.GetProductionPlayability(sound, pawn) : SqueakSoundAvailabilityCache.GetNativePlayability(sound, map, target)) == SqueakSoundPlayability.Playable;
        }
    }

    private sealed class RandRollSource : IRollSource
    {
        public double Next01() => Rand.Value;
    }

    public static ISoundGate GateFor(Pawn? pawn, Map? map, TargetInfo? target)
        => new KernelGate(pawn, map, target);

    public static IRollSource Rolls => new RandRollSource();

    public static SelectionMode ToSelectionMode(SqueakVoicePackMode mode) => mode switch
    {
        SqueakVoicePackMode.Fallback => SelectionMode.Fallback,
        SqueakVoicePackMode.Remix => SelectionMode.Remix,
        SqueakVoicePackMode.Vanilla => SelectionMode.Off,
        SqueakVoicePackMode.Disabled => SelectionMode.Disabled,
        _ => SelectionMode.Off,
    };

    /// <summary>
    /// Creates the immutable formal kernel source table from data Defs. US ships no seed: an absent
    /// Def database yields BuiltInFallbackTable.Empty and startup remains fully functional.
    /// </summary>
    public static BuiltInFallbackTable BuildBuiltInSource()
    {
        List<FallbackProfile> profiles = new();
        try
        {
            foreach (UniversalSqueakerFallbackProfileDef def in DefDatabase<UniversalSqueakerFallbackProfileDef>.AllDefs)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.raceDefName)) continue;
                if (!TryBuildProfile(def, out FallbackProfile? profile) || profile == null)
                {
                    SqueakLog.TargetRejected(def.raceDefName, "invalid_fallback_profile");
                    continue;
                }
                profiles.Add(profile);
            }
        }
        catch (Exception ex)
        {
            SqueakLog.ResolverRebuildFailed(ex);
            return BuiltInFallbackTable.Empty;
        }
        return profiles.Count == 0 ? BuiltInFallbackTable.Empty : new BuiltInFallbackTable(profiles);
    }

    private static bool TryBuildProfile(UniversalSqueakerFallbackProfileDef def, out FallbackProfile? profile)
    {
        profile = null;
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        foreach (UniversalSqueakerFallbackEntry entry in def.entries ?? new List<UniversalSqueakerFallbackEntry>())
        {
            if (entry == null || !BuiltInActionKeys.Contains(entry.actionKey) || string.IsNullOrWhiteSpace(entry.soundKey))
                return false;
            keys[entry.actionKey] = entry.soundKey;
        }
        if (keys.Count == 0) return false;
        profile = new FallbackProfile(new RaceKey(def.raceDefName), Math.Max(1, def.profileVersion), keys);
        return true;
    }

    /// <summary>Returns the store-resolved table after startup initialization, or the data source as a safe fallback.</summary>
    public static BuiltInFallbackTable BuildBuiltIn()
    {
        var source = SqueakFallbackProfileStore.Current ?? BuildBuiltInSource();
        var profiles = new List<FallbackProfile>();
        foreach (var profile in source.Profiles)
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in profile.SoundKeys)
            {
                string? id = UsAudioBindings.KeyForName(entry.Value);
                if (id != null)
                {
                    keys.Add(entry.Key, id);
                }
            }
            profiles.Add(new FallbackProfile(profile.Race, profile.Version, keys));
        }
        return new BuiltInFallbackTable(profiles);
    }

}
