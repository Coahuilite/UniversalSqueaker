using System;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;
using Verse;
using Verse.Sound;

// Explicit platform and resolved-settings doubles. Decision, callers, author loading, dispatch and Harmony are production.
namespace Verse
{
    public class ThingComp { public virtual void CompTick() { } }
    public sealed class Game { }
    public class GameComponent
    {
        public virtual void StartedNewGame() { }
        public virtual void LoadedGame() { }
    }
    public sealed partial class Pawn
    {
        public string ThingID = "Pawn1";
        public int thingIDNumber = 1;
        public ThingDef def = new() { defName = "FixtureRace", race = new object() };
        public bool Spawned = true, Destroyed;
        public Map MapHeld = Find.CurrentMap;
        public object Position = new();
        public FixtureAgeTracker? ageTracker = new();
        public FixtureGenes? genes;
        public CompSqueaker Comp = null!;
        public T GetComp<T>() where T : class => Comp as T ?? null!;
    }
    public sealed class FixtureAgeTracker { public FixtureLifeStage CurLifeStage = new(); }
    public sealed class FixtureLifeStage { public float voxPitch = 1, voxVolume = 1; }
    public sealed class FixtureGenes { public Def? Xenotype; }
    public static class Find
    {
        public static readonly FixtureTicks TickManager = new();
        public static Map CurrentMap = new();
        public static readonly FixtureCamera CameraDriver = new();
    }
    public sealed class FixtureTicks { public int TicksGame = 1000; }
    public sealed class FixtureCamera { public FixtureRect CurrentViewRect = new(); }
    public sealed class FixtureRect
    {
        public FixtureRect ExpandedBy(int distance) => this;
        public bool Contains(object position) => true;
    }
    public static class Prefs { public static bool DevMode = true; }
    public static class UnityData { public static bool IsInMainThread = true; }
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup = 100; }
}
namespace UniversalSqueaker
{
    public partial class CompSqueaker
    {
        public Pawn Pawn = null!;
        public static bool ScaleFrequencyWithTalking;
        public bool Allowed = true;
        public bool ExternalAllowed = true;
        public bool Vocal = true;
        public bool FixtureSustained;
        public int MembershipUpdates;
        private void SynchronizePeriodicMembership() => MembershipUpdates++;
        internal SqueakVocalCapability ControlledOneShotVocal() => new(Vocal ? 1 : 0, 1);
        internal bool ControlledPlaybackSustained(string key) => FixtureSustained;
        internal bool ControlledInvocationAllowed(SqueakTriggerInvocation invocation) => true;
        internal bool ControlledOneShotAllowed(string key, bool activeCommand = false, BehaviorValues? behavior = null, bool authorAction = false)
            => UsControlledActionEligibility.Allows(false, authorAction, Allowed && (BuiltInActionKeys.Contains(key) || ExternalAllowed),
                behavior?.Enabled ?? true, behavior?.Scope ?? SqueakActionScope.AnyOccurrence, activeCommand, Pawn.IsPlayerControlled);
    }
    public readonly struct SqueakVocalCapability
    {
        public const float VocalSilenceThreshold = .001f, TalkingCertainThreshold = .999f;
        public readonly float VocalOrganEfficiency, TalkingChance;
        public SqueakVocalCapability(float vocal, float talking) { VocalOrganEfficiency = vocal; TalkingChance = talking; }
    }
    public static class UniversalSqueakerMod { public static readonly RuntimeSettings Settings = new(); }
    public sealed class RuntimeSettings
    {
        public double activity = 1;
        public BehaviorValues Values = new();
        public BehaviorValues ResolveBehaviorSource(BehaviorDomain domain, string action, BehaviorSourceDirectory directory,
            Func<string, bool> available, out bool paused, Action<string>? reportPause = null)
        {
            paused = false;
            return Values.Copy();
        }
    }
    public static class SqueakRuntimeResolver { public static readonly RuntimeSnapshot Current = new(); }
    public sealed class RuntimeSnapshot
    {
        public SqueakVoicePackMode VoicePackMode = SqueakVoicePackMode.Fallback;
        public SqueakPoolRegistry Registry = SqueakPoolRegistry.Empty;
        public bool AllowEggs;
        public RuntimeContext ResolveContext(Pawn pawn) => new();
    }
    public sealed class RuntimeContext { public Def? Xenotype; }
    public static class SqueakLifeStageResolver { public static AgeBucket Resolve(Pawn pawn) => AgeBucket.Adult; }
}
