using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using RimWorld;
using UnityEngine;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

public sealed class UsVoicePackDef : Def
{
    public VoicePackDeclaration declaration = new();
}

public sealed class UsActionDef : Def
{
    public ActionDeclaration declaration = new();
}

public sealed class UsBehaviorSourceDef : Def
{
    public BehaviorSourceDeclaration declaration = new();
}

public sealed class UsProviderDef : Def
{
    public Type providerClass = null!;
}

/// <summary>Explicit developer session, not the unapproved replacement of the player's daily schedule.</summary>
public sealed class UsControlledOneShotSession : IOneShotBackend, IOneShotRandom
{
    private readonly Pawn pawn;
    private readonly AuthorCatalog catalog;
    private readonly DecisionState state;
    private readonly StatePolicy policy;
    private readonly long workTicks, woundedTicks;
    private readonly bool fullDevCandidate;
    private readonly OneShotDecision decision = new();
    private readonly SustainedPlayback sustained = new();
    private readonly List<KeyValuePair<RandomPurpose, double>> draws = new();
    public IReadOnlyList<KeyValuePair<RandomPurpose, double>> LastRandomDraws => draws.AsReadOnly();
    public DecisionOutcome LastOutcome { get; private set; }
    public DecisionState State => state;

    internal UsControlledOneShotSession(Pawn pawn, AuthorCatalog catalog, StatePolicy policy, long workTicks, long woundedTicks, bool fullDevCandidate)
    {
        this.pawn = pawn;
        this.catalog = catalog;
        this.policy = policy;
        state = new DecisionState(Find.TickManager.TicksGame);
        pawn.GetComp<CompSqueaker>().SeedControlledLegacyClock(state);
        this.workTicks = workTicks;
        this.woundedTicks = woundedTicks;
        this.fullDevCandidate = fullDevCandidate;
    }

    public void Tick(string? eventKey = null, bool activeCommand = false, SqueakTriggerInvocation? compatibilityInvocation = null)
    {
        long now = Find.TickManager.TicksGame;
        if (eventKey == null && now < state.NextStateTick)
        {
            return;
        }

        var comp = pawn.GetComp<CompSqueaker>();
        var vocal = comp.ControlledOneShotVocal();
        var values = UsPawnFacts.Capture(pawn, eventKey);
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var blockedReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        var domain = new BehaviorDomain("Pawn", "", pawn.def.defName,
            ModsConfig.BiotechActive ? pawn.genes?.Xenotype?.defName ?? "" : "");
        var plans = new List<ActionPlan>();
        foreach (var action in catalog.Actions)
        {
            string legacyKey = action.BaseAction.Substring(action.BaseAction.LastIndexOf('/') + 1);
            if (!fullDevCandidate && legacyKey != "Work" && legacyKey != "Wounded" && legacyKey != "Call")
            {
                continue;
            }
            var behavior = UniversalSqueakerMod.Settings.ResolveBehaviorSource(domain, legacyKey, catalog.BehaviorSources, catalog.HasFact, out bool paused,
                reason => blockedReasons[action.Key] = reason);
            if (paused || !comp.ControlledOneShotAllowed(legacyKey, activeCommand, behavior, authorAction: fullDevCandidate)) blocked.Add(action.Key);
            plans.Add(new ActionPlan(action.Key, action.Priority, action.Condition,
                behavior.HardIntervalTicks ?? (legacyKey == "Wounded" ? woundedTicks : workTicks),
                action.CheckShared, action.WriteShared, action.Chance, action.BaseAction,
                legacyKey == "Death" || behavior.TalkingEnabled == false, behavior.Volume ?? 1, behavior.Pitch ?? 1));
        }
        LegacyTimingParameters? legacy = null;
        if (compatibilityInvocation.HasValue)
        {
            string key = eventKey!;
            var invocation = compatibilityInvocation.Value;
            legacy = comp.ControlledLegacyTiming(key, invocation);
            values["us.compat/Event"] = Truth.True;
            plans.Clear();
            double chance = invocation.SkipsRandomOneShotProbability || legacy.Plan.Mode != SqueakTriggerMode.RandomOneShot
                ? 1 : Math.Max(0, Math.Min(1, legacy.Plan.ProbabilityPerCheck * legacy.ProbabilityMultiplier)) / legacy.PopulationScale;
            plans.Add(new ActionPlan(key, 0, new Condition("ref", "us.compat/Event"), 0,
                !legacy.Plan.IgnoreGlobalCooldown, true, chance, key));
            if (!comp.ControlledOneShotAllowed(key, activeCommand)) blocked.Add(key);
        }
        var input = new DecisionInput
        {
            Subject = pawn.ThingID,
            Domain = pawn.def.defName,
            Tick = now,
            Realtime = Time.realtimeSinceStartup,
            ActiveSustainedKey = sustained.ActiveKey,
            StateOpportunity = eventKey == null,
            Eligible = pawn.Spawned
                && pawn.MapHeld == Find.CurrentMap
                && Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position)
                && SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled,
            HasVocalOrgans = vocal.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold,
            TalkingChance = CompSqueaker.ScaleFrequencyWithTalking && vocal.TalkingChance < SqueakVocalCapability.TalkingCertainThreshold ? vocal.TalkingChance : 1,
            Facts = catalog.Capture(pawn.ThingID, values),
            Actions = plans.ToArray(),
            Audio = catalog.Audio,
            SourceMode = SqueakKernelAdapter.ToSelectionMode(SqueakRuntimeResolver.Current.VoicePackMode),
            Fallback = SqueakKernelAdapter.BuildBuiltIn(),
            BlockedActions = blocked,
            BlockedActionReasons = blockedReasons
        };
        if (legacy != null)
        {
            var snapshot = SqueakRuntimeResolver.Current;
            var context = snapshot.ResolveContext(pawn);
            var audioDomain = new Kernel.AudioDomain(new Kernel.RaceKey(pawn.def.defName),
                context.Xenotype == null ? null : new Kernel.XenotypeKey(context.Xenotype.defName));
            input.CompatibilityTiming = legacy;
            input.UseLegacyModulation = true;
            input.AudioPool = snapshot.Registry;
            input.AudioContext = new Kernel.SelectionContext(audioDomain, eventKey!, SqueakLifeStageResolver.Resolve(pawn), true, snapshot.AllowEggs);
            input.SoundGate = SqueakKernelAdapter.GateFor(pawn, pawn.MapHeld, null);
        }
        draws.Clear();
        LastOutcome = decision.Run(state, policy, input, this, this);
    }

    public void Maintain()
    {
        if (!pawn.Spawned || pawn.Destroyed || pawn.MapHeld != Find.CurrentMap
            || SqueakRuntimeResolver.Current.VoicePackMode == SqueakVoicePackMode.Disabled)
        {
            sustained.Stop("SubjectOrServiceUnavailable");
            return;
        }
        long now = Find.TickManager.TicksGame;
        if (sustained.ReviewDue(now))
            sustained.Review(now, catalog.Capture(pawn.ThingID, UsPawnFacts.Capture(pawn, null)));
        sustained.Maintain();
    }
    public void Stop() => sustained.Stop("SessionReleased");
    private sealed class NativeSustainedHandle : ISustainedHandle
    {
        private readonly Sustainer native;
        public NativeSustainedHandle(Sustainer native) { this.native = native; }
        public bool Active => !native.Ended;
        public void Maintain() { if (Active) native.Maintain(); }
        public void End() { if (Active) native.End(); }
    }
    public double Next(RandomPurpose purpose)
    {
        double value = Rand.Value;
        draws.Add(new KeyValuePair<RandomPurpose, double>(purpose, value));
        return value;
    }

    public PlaybackReceipt Dispatch(PlaybackRequest request)
    {
        SoundDef? sound = null;
        SoundInfo info = default;
        string rejection = "DynamicPreflightRejected";
        bool Preflight()
        {
            if (!UnityData.IsInMainThread)
            {
                rejection = "MainThreadRequired";
                return false;
            }
            if (request.Subject != pawn.ThingID || request.Domain != pawn.def.defName)
            {
                rejection = "SubjectIdentityChanged";
                return false;
            }
            if (!pawn.Spawned || pawn.Destroyed || pawn.MapHeld != Find.CurrentMap
                || !Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position))
            {
                rejection = "SubjectOrLocationUnavailable";
                return false;
            }
            sound = UsAudioBindings.Resolve(request.Audio.AudioId);
            if (sound == null)
            {
                rejection = "MissingFinalBinding";
                return false;
            }
            if (sound.sustain && !request.Sustained)
            {
                rejection = "OneShotShapeRequired";
                return false;
            }
            if (!SqueakSoundAvailabilityCache.TryCreateProductionInfo(sound, pawn, out info, out var playability))
            {
                rejection = "DynamicPlayability:" + playability;
                return false;
            }
            double localVolume = request.Audio.Volume;
            double localPitch = request.Pitch;
            double globalVolume = CompSqueaker.GlobalVolumeFactor;
            if (!OneShotOutput.TryPrepare(localVolume, localPitch, globalVolume, out float volume, out float pitch))
            {
                rejection = "InvalidFinalParameters";
                return false;
            }
            if (!UsInstanceOutput.Available)
            {
                rejection = UsInstanceOutput.Failure;
                return false;
            }
            if (request.Sustained && sound.sustain) info = SoundInfo.InMap(new TargetInfo(pawn), MaintenanceType.PerTick);
            info.volumeFactor = 1;
            info.pitchFactor = pitch;
            return true;
        }

        void Play()
        {
            int token = UsInstanceOutput.Mark(ref info, new UsOutputContribution(request.Audio.Volume));
            try { sound!.PlayOneShot(info); }
            finally { UsInstanceOutput.ReleaseRequest(token); }
        }

        if (request.Sustained)
        {
            try
            {
                if (!Preflight()) return new PlaybackReceipt(PlaybackResult.Rejected, rejection);
            }
            catch (Exception error) { return new PlaybackReceipt(PlaybackResult.Rejected, "Preparation:" + error.GetType().Name); }
            return sustained.Dispatch(request, sound!.sustain, () =>
            {
                if (!sound.sustain)
                {
                    Play();
                    return new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched, "NonSustainOneShotFallback"));
                }
                int token = UsInstanceOutput.Mark(ref info, new UsOutputContribution(request.Audio.Volume));
                try
                {
                    Sustainer? native = sound.TrySpawnSustainer(info);
                    if (native == null || native.Ended)
                        return new SustainedStart(new PlaybackReceipt(PlaybackResult.Indeterminate, "NoEffectiveSustainerAfterCall"));
                    return new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched, "NativeSustainerEstablished"), new NativeSustainedHandle(native));
                }
                finally { UsInstanceOutput.ReleaseRequest(token); }
            });
        }
        var receipt = OneShotDispatch.Execute(
            Preflight,
            Play,
            () => Log.Message("usdiag OneShot " + request.Action + " " + request.Audio.AudioId + " NativeCallReturned"));
        return receipt.Result == PlaybackResult.Rejected && receipt.Reason == "PreflightRejected"
            ? new PlaybackReceipt(PlaybackResult.Rejected, rejection)
            : receipt;
    }
}

public static class UsOneShotRuntime
{
    private static readonly Dictionary<Pawn, UsControlledOneShotSession> sessions = new();
    private static AuthorCatalog catalog = new(720, 216, fullDevCandidate: true);
    public static IReadOnlyList<string> Issues => catalog.Issues;
    public static IEnumerable<string> DeclaredRaces => catalog.Audio.Select(x => x.Domain).Distinct(StringComparer.Ordinal);
    public static BehaviorSourceDirectory BehaviorSources => catalog.BehaviorSources;

    public static void Initialize()
    {
        ResetSession();
        catalog = new AuthorCatalog(720, 216, fullDevCandidate: true);
        foreach (var provider in DefDatabase<UsProviderDef>.AllDefsListForReading)
        {
            try
            {
                catalog.AddProvider((IUsDefinitionProvider)Activator.CreateInstance(provider.providerClass)!);
            }
            catch (Exception error)
            {
                catalog.Issues.Add(provider.defName + ":" + error.Message);
            }
        }

        foreach (var action in DefDatabase<UsActionDef>.AllDefsListForReading)
        {
            catalog.AddAction(action.declaration);
        }

        foreach (var source in DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading)
        {
            foreach (var target in source.declaration.Sources)
            {
                var declaration = target.Copy();
                declaration.Id = (source.modContentPack?.PackageId ?? "") + "/" + source.defName;
                catalog.AddBehaviorSource(declaration);
            }
        }

        foreach (var pack in DefDatabase<UsVoicePackDef>.AllDefsListForReading)
        {
            if (DefDatabase<ThingDef>.GetNamedSilentFail(pack.declaration.Race)?.race != null)
            {
                catalog.AddPack(pack.modContentPack?.PackageId ?? "", pack.defName, pack.declaration, Bind);
            }
            else
            {
                catalog.Issues.Add(pack.defName + ":TargetRaceUnavailable");
            }
        }

        foreach (string issue in catalog.Issues)
        {
            Log.Warning("usdiag authoring " + issue);
        }
    }

    private static string? Bind(string name)
    {
        string? key = UsAudioBindings.KeyForName(name);
        if (key == null)
        {
            return null;
        }
        var sound = UsAudioBindings.Resolve(key);
        if (sound == null)
        {
            return null;
        }
        return key;
    }

    public static void EnableControlled(Pawn pawn, long waitTicks, long sharedTicks, long workTicks, long woundedTicks, bool fullDevCandidate = false)
    {
        if (!Prefs.DevMode)
        {
            throw new InvalidOperationException("Controlled OneShot requires developer mode and explicit policy");
        }

        if (pawn.GetComp<CompSqueaker>() == null)
        {
            throw new InvalidOperationException("Pawn needs an existing CompSqueaker");
        }

        if (pawn.GetComp<CompSqueaker>().HasActiveSustainer)
        {
            throw new InvalidOperationException("End existing sustained playback before opting into OneShot control");
        }

        DisableControlled(pawn);
        sessions[pawn] = new UsControlledOneShotSession(pawn, catalog, new StatePolicy(sharedTicks, waitTicks), workTicks, woundedTicks, fullDevCandidate);
    }

    public static void DisableControlled(Pawn pawn)
    {
        if (sessions.TryGetValue(pawn, out var session)) session.Stop();
        sessions.Remove(pawn);
    }
    public static bool IsControlled(Pawn pawn) => sessions.ContainsKey(pawn);
    public static bool TryTick(Pawn pawn)
    {
        if (!sessions.TryGetValue(pawn, out var session))
        {
            return false;
        }

        if (!pawn.Spawned || pawn.Destroyed)
        {
            session.Stop();
            sessions.Remove(pawn);
            return true;
        }

        session.Maintain();
        if (SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled && pawn.MapHeld == Find.CurrentMap)
        {
            session.Tick();
        }

        return true;
    }

    public static bool TryEvent(Pawn pawn, string key, SqueakTriggerOrigin origin, SqueakInvocationSource source)
    {
        if (!sessions.TryGetValue(pawn, out var session))
        {
            return false;
        }

        var invocation = new SqueakTriggerInvocation(origin, source);
        if (SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled && pawn.GetComp<CompSqueaker>().ControlledInvocationAllowed(invocation))
        {
            session.Tick(key, invocation.IsActiveCommand, BuiltInPawnContracts.CandidateOrder.Contains(key) ? null : invocation);
        }

        // Registered and unregistered keys share this owner; Comp's old cooldown writer is never called.
        return true;
    }

    public static void ResetSession()
    {
        foreach (var session in sessions.Values) session.Stop();
        sessions.Clear();
    }
}

public sealed class UsOneShotSessionLifecycle : GameComponent
{
    public UsOneShotSessionLifecycle(Game game)
    {
        UsOneShotRuntime.ResetSession();
    }

    public override void StartedNewGame() => UsOneShotRuntime.ResetSession();
    public override void LoadedGame() => UsOneShotRuntime.ResetSession();
}

