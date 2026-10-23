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
        public bool onCamera;
        public Verse.FloatRange distRange = new(5, 20);
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
    public sealed partial class Pawn
    {
        public Verse.AI.Job? CurJob;
        public Verse.AI.Pawn_JobTracker? jobs;
        public Verse.AI.Pawn_PathFollower? pather;
        public Verse.AI.Pawn_MindState? mindState;
        public bool IsPlayerControlled, Downed;
        public FixtureHealth? health = new();
        public bool FixtureAwake = true;
        public bool Awake() => FixtureAwake;
        public Pawn_NeedsTracker? needs;
        public PawnPosture GetPosture() => PawnPosture.LayingInBed;
    }
    public sealed class FixtureHealth { public FixtureHediffSet? hediffSet = new(); }
    public sealed class FixtureHediffSet { public readonly List<Hediff> hediffs = new(); }
    public class Hediff { public float Severity; }
    public sealed class Hediff_Injury : Hediff { }
    public enum PawnPosture { LayingInBed }
    public sealed class Pawn_NeedsTracker { public object? rest; public FixtureMood? mood; }
    public sealed class FixtureMood { public float CurLevelPercentage = .5f; }
    public class Thing
    {
        public ThingDef def = new();
        public string ThingID = "Food1";
        public bool Destroyed;
        public bool IngestibleNow = true;
        public bool ThrowIngest;
        public float NutritionResult = .5f;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public float Ingested(Pawn ingester, float nutritionWanted)
        {
            if (Destroyed || !IngestibleNow) return 0;
            if (ThrowIngest) throw new InvalidOperationException("FixtureIngestFailure");
            return NutritionResult;
        }
    }
    public enum ThingCategory { Pawn, Item }
    public sealed class ThingDef : Def { public bool IsDrug, IsNutritionGivingIngestible; public object? ingestible; public object? race; public ThingCategory category = ThingCategory.Pawn; public Type? thingClass = typeof(Pawn); public List<CompProperties> comps = new(); }
    public class CompProperties { public Type? compClass; }
    public struct FloatRange { public float min, max; public float TrueMin => min; public float TrueMax => max; public FloatRange(float min, float max) { this.min = min; this.max = max; } }
    public sealed class Map { }
    public struct TargetInfo { public TargetInfo(Pawn pawn) { } }
    public static class Rand { public static float Value => .5f; }
    public static class ModsConfig { public static bool BiotechActive = true; }
    public class Def
    {
        public string defName = "";
        public string label = "";
        public string description = "";
        public ModContentPack? modContentPack;
    }
    public sealed class SoundDef : Def
    {
        public bool sustain;
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
        public static void Message(string line) => Lines.Add(line);
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
        public static bool RejectProductionInfo;
        public static bool TryCreateProductionInfo(Verse.SoundDef sound, Verse.Pawn pawn, out Verse.Sound.SoundInfo info, out SqueakSoundPlayability result)
        {
            info = Verse.Sound.SoundInfo.OnCamera();
            result = SqueakSoundPlayability.Playable;
            return !RejectProductionInfo;
        }
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
        public static void CompAttachSkipped(string target, string reason) { }
        public static void CompAttachFailed(Exception error) { }
        public static void CompAutoAttached(string target) { }
        public static void ResolverRebuildFailed(Exception error) { }
    }
    public static class SqueakFallbackProfileStore
    {
        public static UniversalSqueaker.Kernel.BuiltInFallbackTable? Current;
    }
    public sealed class SqueakVoicePackDef : Verse.Def
    {
        internal bool IsKernelDeclaration;
        internal IReadOnlyList<string> KernelActionKeys = Array.Empty<string>();
        public SqueakVoicePackScope scope;
        public string raceDefName = "";
        public string targetDefName = "";
        public float weight = 1;
        public string FixturePackKey = "";
        public List<SqueakVoicePackAction> actions = new();
        public List<SqueakVoicePackFallback> fallbacks = new();
        public bool TryGetPackKey(out string key)
        {
            key = FixturePackKey.Length == 0 ? modContentPack!.PackageId + ":" + defName : FixturePackKey;
            return true;
        }
    }
    public enum SqueakVoicePackScope { Unspecified, Race, Xenotype }
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
        public List<string> RaceDefNames = new();
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

namespace Verse.AI
{
    public sealed class JobDef : Verse.Def { public RimWorld.JoyKindDef? joyKind; }
    public enum TargetIndex { None, A, B, C }
    public enum JobCondition { Succeeded, InterruptForced, Incompletable }
    public sealed class Job
    {
        public JobDef? def; public object? workGiverDef; public bool playerForced; public LocalTargetInfo targetA, targetB;
        public LocalTargetInfo GetTarget(TargetIndex index) => index == TargetIndex.A ? targetA : targetB;
    }
    public struct LocalTargetInfo { public Verse.Thing? Thing; public Verse.Pawn? Pawn; }
    public sealed class Pawn_JobTracker { public JobDriver? curDriver; }
    public sealed class Pawn_PathFollower { public bool Moving; }
    public sealed class Pawn_MindState { public bool IsIdle; }
    public class JobDriver
    {
        public bool asleep; public string CurToilString = "";
        public Verse.Pawn pawn = null!; public Job job = null!;
        public Toil? FixtureToil;
        protected Toil? CurToil => FixtureToil;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal void SetupToils() { }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Cleanup(JobCondition condition) { FixtureToil?.Finish(); }
    }
    public sealed class Toil
    {
        public Verse.Pawn actor = null!;
        public Action? initAction;
        public readonly List<Action> finishActions = new();
        public void AddFinishAction(Action action) => finishActions.Add(action);
        public void Finish() { foreach (var action in finishActions) action(); }
    }
}
namespace RimWorld
{
    public sealed class JobDriver_Ingest : Verse.AI.JobDriver { }
    public sealed class JobDriver_FoodFeedPatient : Verse.AI.JobDriver { }
    public static class Toils_Ingest
    {
        public static Action? FixtureInit;
#if !US_FIXTURE_EAT_MISSING_CHEW
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static Verse.AI.Toil ChewIngestible(Verse.Pawn chewer, float durationMultiplier, Verse.AI.TargetIndex index, Verse.AI.TargetIndex surface = Verse.AI.TargetIndex.None)
            => new() { initAction = FixtureInit };
#endif
    }
    public sealed class XenotypeDef : Verse.Def { }
    public sealed class JoyKindDef { }
    public static class JoyKindDefOf { public static readonly JoyKindDef Social = new(); }
    public static class JobDefOf { public static readonly Verse.AI.JobDef Ingest = new() { defName = "Ingest" }; public static readonly Verse.AI.JobDef Equip = new() { defName = "Equip" }; }
    public interface IEatingDriver { bool GainingNutritionNow { get; } }
}
namespace UniversalSqueaker
{
    public partial class CompSqueaker : Verse.ThingComp
    {
        public static bool EatPrecisionEnabled, EatPrecisionIncludeDrugs;
        public static float GlobalVolumeFactor = 1;
    }
}

namespace UniversalSqueaker
{
    public sealed class SqueakMoodMod
    {
        public SqueakMood mood;
        public float pitchFactor, volumeFactor;
        public Verse.FloatRange pitchJitter;
    }
}

// Native-shape fixtures below exercise REAL Harmony patches, not an alternative output implementation.
namespace UnityEngine
{
    public sealed class AudioSource
    {
        public float volume, pitch, minDistance, maxDistance;
        public bool Playing;
        public void Play() { Playing = true; }
    }
}
namespace Verse.Sound
{
    public struct SoundInfo
    {
        private Dictionary<string, float>? parameters;
        public float volumeFactor, pitchFactor;
        public IEnumerable<KeyValuePair<string, float>> DefinedParameters => parameters ?? (IEnumerable<KeyValuePair<string, float>>)Array.Empty<KeyValuePair<string, float>>();
        public void SetParameter(string key, float value)
        {
            parameters ??= new Dictionary<string, float>();
            parameters[key] = value;
        }
        public static SoundInfo OnCamera() => new() { volumeFactor = 1, pitchFactor = 1 };
        public static SoundInfo InMap(Verse.TargetInfo target, MaintenanceType maintenance) => OnCamera();
    }
    public enum MaintenanceType { PerTick }
    public abstract class Sample
    {
        public SubSoundDef subDef;
        public UnityEngine.AudioSource source = null!;
        public abstract SoundInfo Info { get; }
        public float FixtureMapping = .5f;
        public float FixtureContext = .8f;
        protected virtual float Volume => 2 * Info.volumeFactor * FixtureMapping * FixtureContext;
        public float SanitizedVolume => Volume;
        public Sample(SubSoundDef definition) { subDef = definition; }
        public virtual void Update() { source.volume = SanitizedVolume; source.pitch = Info.pitchFactor; }
        public virtual void SampleCleanup() { }
    }
    public sealed class SampleOneShot : Sample
    {
        public SoundInfo info;
        public override SoundInfo Info => info;
        public SampleOneShot(SubSoundDef definition, SoundInfo info) : base(definition) { this.info = info; }
        public static SampleOneShot TryMakeAndPlay(SubSoundDef definition, SoundInfo info, UnityEngine.AudioSource source)
        {
            var sample = new SampleOneShot(definition, info) { source = source };
            source.volume = sample.SanitizedVolume;
            source.minDistance = definition.distRange.TrueMin;
            source.maxDistance = definition.distRange.TrueMax;
            sample.Update();
            source.Play();
            return sample;
        }
    }
    public sealed class SubSustainer
    {
        public readonly Sustainer parent;
        public readonly SubSoundDef subDef;
        public SoundInfo Info => parent.info;
        public SubSustainer(Sustainer parent, SubSoundDef subDef) { this.parent = parent; this.subDef = subDef; }
    }
    public sealed class SampleSustainer : Sample
    {
        public SubSustainer subSustainer;
        public override SoundInfo Info => subSustainer.Info;
        protected override float Volume => base.Volume * .25f;
        public SampleSustainer(SubSustainer parent) : base(parent.subDef) { subSustainer = parent; }
        public static SampleSustainer TryMakeAndPlay(SubSustainer subSus, UnityEngine.AudioClip clip, float scheduledEndTime, float startTime = 0f)
        {
            var sample = new SampleSustainer(subSus) { source = new UnityEngine.AudioSource() };
            sample.Update();
            sample.source.Play();
            return sample;
        }
    }
    public sealed class Sustainer
    {
        public SoundInfo info;
        public SampleSustainer Initial;
        public bool Ended;
        public int EndCalls;
          public string EndFailure = "";
        public bool FixtureCamera;
        public Sustainer(Verse.SoundDef definition, SoundInfo info)
        {
            this.info = info;
            Initial = new SampleSustainer(new SubSustainer(this, definition.subSounds[0])) { source = new UnityEngine.AudioSource() };
            Initial.source.volume = Initial.SanitizedVolume; // The constructor-time sample precedes parent capture postfix.
            Initial.source.minDistance = Initial.subDef.distRange.TrueMin;
            Initial.source.maxDistance = Initial.subDef.distRange.TrueMax;
            Initial.Update();
            Initial.source.Play();
        }
          public void End()
          {
              EndCalls++;
              if (EndFailure == "before") throw new InvalidOperationException("before End");
              Ended = true;
              PlaySoundOnMapOrCamera(new Verse.SoundDef { subSounds = { Initial.subDef } });
              if (EndFailure == "after") throw new InvalidOperationException("after auxiliary");
          }
        public void Maintain() { }
        private void Cleanup() { Initial.SampleCleanup(); PlaySoundOnMapOrCamera(new Verse.SoundDef { subSounds = { Initial.subDef } }); }
        public void FixtureCleanup() => Cleanup();
        private void PlaySoundOnMapOrCamera(Verse.SoundDef sound)
        {
            if (FixtureCamera) sound.PlayOneShot(SoundInfo.OnCamera());
            else sound.PlayOneShot(SoundInfo.OnCamera());
        }
    }
    public static class SoundStarter
    {
        public static readonly List<Sustainer> Spawned = new();
        public static string SpawnFailure = "";
        public static Sustainer? TrySpawnSustainer(this Verse.SoundDef definition, SoundInfo info)
        {
            if (SpawnFailure == "null")
            {
                return null;
            }
            var parent = new Sustainer(definition, info);
            Spawned.Add(parent);
            if (SpawnFailure == "ended")
            {
                parent.End();
            }
            return parent;
        }
        public static readonly List<SampleOneShot> Played = new();
        public static Action? NestedCall;
        public static void PlayOneShot(this Verse.SoundDef definition, SoundInfo info)
        {
            var nested = NestedCall;
            NestedCall = null;
            nested?.Invoke();
            Played.Add(SampleOneShot.TryMakeAndPlay(definition.subSounds[0], info, new UnityEngine.AudioSource()));
        }
    }
}
