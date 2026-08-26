using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker;

/// <summary>
/// 内核↔适配层接缝：SoundDef 收敛为 string key + ISoundGate 函子；Verse.Rand 收敛为 IRollSource；
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
            SoundDef? sound = DefDatabase<SoundDef>.GetNamedSilentFail(soundKey);
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
        SqueakVoicePackMode.Disabled => SelectionMode.Off,
        _ => SelectionMode.Off,
    };

    /// <summary>
    /// Creates the immutable formal kernel source table from data Defs. US ships no seed: an absent
    /// Def database yields BuiltInFallbackTable.Empty and startup remains fully functional.
    /// </summary>
    public static BuiltInFallbackTable BuildBuiltInSource()
    {
        List<FallbackProfile> profiles = new();
        Dictionary<RaceKey, FallbackProfile> byRace = new();
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
                byRace[profile.Race] = profile;
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
        return SqueakFallbackProfileStore.Current ?? BuildBuiltInSource();
    }

    /// <summary>选择集按记录自身 (raceDefName, xenotypeDefName) 域键组织；候选 pack 按声明的
    /// raceDefName 精确匹配域（跨 race 池隔离），无内置字面量域。</summary>
    public static List<VoicePackEntry> BuildEntries(SqueakXenotypeCatalogSnapshot catalog, IReadOnlyDictionary<AudioDomain, HashSet<string>> selections)
    {
        List<VoicePackEntry> entries = new();
        foreach (KeyValuePair<AudioDomain, HashSet<string>> pair in selections)
        {
            AudioDomain domain = pair.Key;
            if (pair.Value == null || pair.Value.Count == 0) continue;
            IReadOnlyList<SqueakVoicePackDef>? candidates = null;
            if (domain.Xenotype == null)
                candidates = catalog.RacePacks;
            else if (ModsConfig.BiotechActive && catalog.XenotypePacksByDefName.TryGetValue(domain.Xenotype.Value.DefName, out IReadOnlyList<SqueakVoicePackDef>? packs))
                candidates = packs;
            AddDomain(entries, candidates, pair.Value, domain);
        }
        return entries;
    }

    /// <summary>KnownMapSoundDefs 收集：catalog 全量 pack 音频（含未选择），排除 _Preview 后缀 transport。</summary>
    public static HashSet<SoundDef> CollectKnownSounds(SqueakXenotypeCatalogSnapshot catalog)
    {
        HashSet<SoundDef> known = new();
        foreach (SqueakVoicePackDef pack in catalog.PackByKey.Values)
        {
            foreach (SoundDef sound in ProjectSounds(pack)) known.Add(sound);
        }
        return known;
    }

    /// <summary>ChainResult → SqueakSoundChoice（tier → source 映射；key 查表缺失 → None 防御）。</summary>
    public static SqueakSoundChoice ToChoice(ChainResult result)
    {
        if (result.SoundKey == null) return SqueakSoundChoice.None;
        SoundDef? sound = DefDatabase<SoundDef>.GetNamedSilentFail(result.SoundKey);
        if (sound == null) return SqueakSoundChoice.None;
        SqueakSoundSource source = result.Tier switch
        {
            ChainTier.XenotypePack => SqueakSoundSource.XenotypePack,
            ChainTier.RacePack => SqueakSoundSource.RacePack,
            ChainTier.PackFallback => SqueakSoundSource.RacePack,
            _ => SqueakSoundSource.Vanilla,
        };
        return new SqueakSoundChoice(sound, source, result.PoolStableKey, result.IsEgg);
    }

    private static void AddDomain(List<VoicePackEntry> entries, IReadOnlyList<SqueakVoicePackDef>? candidates, HashSet<string> keys, AudioDomain domain)
    {
        if (candidates == null || keys == null || keys.Count == 0) return;
        foreach (SqueakVoicePackDef pack in candidates)
        {
            if (!string.Equals(pack.raceDefName, domain.Race.DefName, StringComparison.Ordinal)) continue;
            if (!pack.TryGetPackKey(out string key) || !keys.Contains(key)) continue;
            VoicePackEntry? entry = BuildEntry(pack, key, domain);
            if (entry != null) entries.Add(entry);
        }
    }

    private static VoicePackEntry? BuildEntry(SqueakVoicePackDef pack, string key, AudioDomain domain)
    {
        Dictionary<string, List<ActionSoundSet>> variants = new();
        foreach (SqueakVoicePackAction entry in pack.actions ?? new List<SqueakVoicePackAction>())
        {
            if (entry == null) continue;
            string? actionKey = ActionKey.For(entry.action);
            if (actionKey == null) continue;
            List<string> sounds = ProjectSounds(entry);
            if (sounds.Count == 0) continue;
            if (!variants.TryGetValue(actionKey, out List<ActionSoundSet>? sets))
            {
                sets = new List<ActionSoundSet>();
                variants.Add(actionKey, sets);
            }
            sets.Add(new ActionSoundSet(sounds, entry.ageTag, 1f, entry.IsEgg));
        }
        if (variants.Count == 0) return null;
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        foreach (KeyValuePair<string, List<ActionSoundSet>> pair in variants)
            actions.Add(pair.Key, pair.Value.AsReadOnly());

        Dictionary<string, string> fallback = new();
        foreach (SqueakVoicePackFallback entry in pack.fallbacks ?? new List<SqueakVoicePackFallback>())
        {
            if (entry == null || entry.sound == null) continue;
            string? actionKey = ActionKey.For(entry.action);
            string? soundKey = entry.sound.defName;
            if (actionKey == null || string.IsNullOrWhiteSpace(soundKey) || fallback.ContainsKey(actionKey)) continue;
            fallback.Add(actionKey, soundKey);
        }
        return new VoicePackEntry(key, domain, pack.weight, actions, fallback.Count == 0 ? null : fallback);
    }

    private static List<string> ProjectSounds(SqueakVoicePackAction entry)
    {
        return (entry.sounds ?? new List<SoundDef>())
            .Where(s => s != null && !s.defName.EndsWith("_Preview", StringComparison.Ordinal))
            .Select(s => s!.defName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static List<SoundDef> ProjectSounds(SqueakVoicePackDef pack)
    {
        List<SoundDef> result = new();
        foreach (SqueakVoicePackAction entry in pack.actions ?? new List<SqueakVoicePackAction>())
        {
            if (entry == null) continue;
            foreach (SoundDef sound in entry.sounds ?? new List<SoundDef>())
            {
                if (sound == null || sound.defName.EndsWith("_Preview", StringComparison.Ordinal) || result.Contains(sound)) continue;
                result.Add(sound);
            }
        }
        foreach (SqueakVoicePackFallback entry in pack.fallbacks ?? new List<SqueakVoicePackFallback>())
        {
            SoundDef? sound = entry?.sound;
            if (sound == null || sound.defName.EndsWith("_Preview", StringComparison.Ordinal) || result.Contains(sound)) continue;
            result.Add(sound);
        }
        return result;
    }
}
