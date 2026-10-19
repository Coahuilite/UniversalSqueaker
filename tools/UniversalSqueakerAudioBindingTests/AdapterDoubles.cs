// Controlled adapter doubles. This project is NOT the platform-free kernel evidence.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using UniversalSqueaker;

namespace UnityEngine
{
    public enum AudioDataLoadState { Unloaded, Loading, Loaded, Failed }
    public sealed class AudioClip
    {
        public AudioDataLoadState loadState = AudioDataLoadState.Loaded;
    }
}
namespace Verse.Sound
{
    public class ResolvedGrain { }
    public sealed class ResolvedGrain_Clip : ResolvedGrain
    {
        public UnityEngine.AudioClip? clip = new();
    }
    public sealed class ResolvedGrain_Silence : ResolvedGrain { }
    public sealed class SubSoundDef
    {
        private readonly List<ResolvedGrain> resolvedGrains;
        private readonly int distinctResolvedGrainsCount;
        public SubSoundDef(List<ResolvedGrain> grains, int count)
        {
            resolvedGrains = grains;
            distinctResolvedGrainsCount = count;
        }
        public int FixtureCount => distinctResolvedGrainsCount + resolvedGrains.Count;
    }
}
namespace Verse
{
    public sealed class Pawn { }
    public sealed class Map { }
    public struct TargetInfo { }
    public static class Rand { public static float Value => .5f; }
    public static class ModsConfig { public static bool BiotechActive = true; }
    public class Def
    {
        public string defName = "";
    }
    public sealed class SoundDef : Def
    {
        public readonly List<Verse.Sound.SubSoundDef> subSounds = new();
        public string FixtureValue = "";
    }
    public sealed class ModContentPack
    {
        public string PackageId = "";
    }
    public sealed class LoadableXmlAsset
    {
        public ModContentPack mod = null!;
    }
    public static class DefDatabase<T> where T : Def
    {
        public static readonly List<T> AllDefsListForReading = new();
        public static IEnumerable<T> AllDefs => AllDefsListForReading;
        public static T? GetNamedSilentFail(string name) => AllDefsListForReading.Find(item => item.defName == name);
    }
    public static class Log
    {
        public static readonly List<string> Lines = new();
        public static void Warning(string line) => Lines.Add(line);
        public static void Error(string line) => Lines.Add(line);
    }
    public static class GenTypes
    {
        public static bool FailOnFixtureType;
        public static Type? GetTypeInAnyAssembly(string name)
        {
            if (name == "FixtureFailure" && FailOnFixtureType)
            {
                throw new InvalidOperationException("controlled original-observation failure");
            }
            return name == "SoundDef" ? typeof(SoundDef) : null;
        }
    }
    public static class LongEventHandler
    {
        private static readonly List<Action> pending = new();
        public static void ExecuteWhenFinished(Action action) => pending.Add(action);
        public static void FixtureExecuteGrowingQueue()
        {
            for (int i = 0; i < pending.Count; i++)
            {
                pending[i]();
            }
            pending.Clear();
        }
    }
    public static class XmlInheritance
    {
        public static readonly Dictionary<XmlNode, XmlNode> ResolvedNodes = new();
        public static XmlNode GetResolvedNodeFor(XmlNode node)
            => ResolvedNodes.TryGetValue(node, out var resolved) ? resolved : node;
    }
    public static class LoadedModManager
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static XmlDocument CombineIntoUnifiedXML(List<LoadableXmlAsset> xmls, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
            => FixtureDocument!;
        public static XmlDocument? FixtureDocument;
    }
    public static class DirectXmlLoader
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Def? DefFromNode(XmlNode node, LoadableXmlAsset? asset)
            => FixtureConstruction.Create(node);
    }
    public static class DirectXmlToObjectNew
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Def? DefFromNodeNew(XmlNode node, LoadableXmlAsset? asset)
            => FixtureConstruction.Create(node);
    }
    internal static class FixtureConstruction
    {
        internal static SoundDef? Create(XmlNode node)
        {
            node = XmlInheritance.GetResolvedNodeFor(node);
            if (node.Attributes?["Fail"] != null)
            {
                return null;
            }
            var sound = new SoundDef
            {
                defName = node.SelectSingleNode("defName")!.InnerText,
                FixtureValue = node.SelectSingleNode("value")?.InnerText ?? ""
            };
            var grain = new Verse.Sound.ResolvedGrain_Clip();
            if (node.Attributes?["Unsafe"] != null)
            {
                grain.clip = null;
            }
            sound.subSounds.Add(new Verse.Sound.SubSoundDef(new List<Verse.Sound.ResolvedGrain> { grain }, 1));
            return sound;
        }
    }
}
namespace UniversalSqueaker
{
    public enum SqueakSoundPlayability { Playable, Failed }
    public enum SqueakSoundAvailabilityState { Available, Failed }
    public sealed class FixtureAvailability
    {
        public SqueakSoundAvailabilityState State;
    }
    public static class SqueakSoundAvailabilityCache
    {
        public static SqueakSoundPlayability GetProductionPlayability(Verse.SoundDef sound, Verse.Pawn? pawn) => SqueakSoundPlayability.Playable;
        public static SqueakSoundPlayability GetNativePlayability(Verse.SoundDef sound, Verse.Map? map, Verse.TargetInfo? target) => SqueakSoundPlayability.Playable;
        public static int ResolveCalls;
        public static FixtureAvailability Resolve(Verse.SoundDef sound)
        {
            ResolveCalls++;
            bool ready = sound.subSounds.Count > 0;
            foreach (var subSound in sound.subSounds)
            {
                ready &= UsNativeAudioPool.Read(subSound) != null;
            }
            return new FixtureAvailability { State = ready ? SqueakSoundAvailabilityState.Available : SqueakSoundAvailabilityState.Failed };
        }
    }
}
namespace UniversalSqueaker
{
    public enum SqueakVoicePackMode { Fallback, Remix, Vanilla, Disabled }
    public sealed class UniversalSqueakerFallbackProfileDef : Verse.Def
    {
        public string raceDefName = "";
        public int profileVersion = 1;
        public List<UniversalSqueakerFallbackEntry> entries = new();
    }
    public sealed class UniversalSqueakerFallbackEntry
    {
        public string actionKey = "";
        public string soundKey = "";
    }
    public static class SqueakLog
    {
        public static void TargetRejected(string target, string reason) { }
        public static void ResolverRebuildFailed(Exception error) { }
    }
    public static class SqueakFallbackProfileStore
    {
        public static UniversalSqueaker.Kernel.BuiltInFallbackTable? Current;
    }
    public sealed class SqueakVoicePackDef : Verse.Def
    {
        public string raceDefName = "";
        public float weight = 1;
        public string FixturePackKey = "";
        public List<SqueakVoicePackAction> actions = new();
        public List<SqueakVoicePackFallback> fallbacks = new();
        public bool TryGetPackKey(out string key)
        {
            key = FixturePackKey;
            return true;
        }
    }
    public sealed class SqueakVoicePackAction
    {
        public SqueakAction action;
        public UniversalSqueaker.Kernel.AgeBucket? ageTag;
        public bool IsEgg;
        public List<Verse.SoundDef> sounds = new();
    }
    public sealed class SqueakVoicePackFallback
    {
        public SqueakAction action;
        public Verse.SoundDef? sound;
    }
    public sealed class SqueakXenotypeCatalogSnapshot
    {
        public List<SqueakVoicePackDef> RacePacks = new();
        public Dictionary<string, IReadOnlyList<SqueakVoicePackDef>> XenotypePacksByDefName = new();
        public Dictionary<string, SqueakVoicePackDef> PackByKey = new();
    }
    public enum SqueakSoundSource { None, XenotypePack, RacePack, Vanilla }
    public readonly struct SqueakSoundChoice
    {
        public static readonly SqueakSoundChoice None = default;
        public readonly Verse.SoundDef? Sound;
        public readonly UniversalSqueaker.Kernel.ChainResult Selection;
        internal SqueakSoundChoice(Verse.SoundDef? sound, SqueakSoundSource source, string? packKey,
            bool egg, UniversalSqueaker.Kernel.ChainTier? tier, UniversalSqueaker.Kernel.ChainResult selection)
        {
            Sound = sound;
            Selection = selection;
        }
    }
}
