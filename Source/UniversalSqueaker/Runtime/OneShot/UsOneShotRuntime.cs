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

/// <summary>The single per-Pawn production decision, settlement and owned playback lifecycle. Policy selection is a separate temporary process gate.</summary>
public sealed class UsPawnPlaybackSession : IOneShotBackend, IOneShotRandom
{
    private readonly Pawn pawn;
    private readonly AuthorCatalog catalog;
    private readonly DecisionState state;
    private readonly StatePolicy policy;
    private readonly long workTicks, woundedTicks;
    private readonly UsDevelopmentPolicy candidate;
    private readonly OneShotDecision decision = new();
    private readonly SustainedPlayback sustained = new();
    private readonly List<KeyValuePair<RandomPurpose, double>> draws = new();
    private SoundDef? preparedSound;
    private long lastObservation;
    private bool diagnosticFaultReported;
    internal string DiagnosticText => "tick=" + lastObservation + " action=" + LastOutcome.Action
        + " reason=" + LastOutcome.Reason + " receipt=" + LastOutcome.Receipt?.Result
        + " confirmed=" + state.ConfirmedTick + " sharedUntil=" + state.SharedDeadline
        + " actionUntil=" + state.ActionDeadline(LastOutcome.Action) + " nextState=" + state.NextStateTick
        + " active=" + sustained.ActiveKey + " ended=" + sustained.LastEndReason;
    public IReadOnlyList<KeyValuePair<RandomPurpose, double>> LastRandomDraws => draws.AsReadOnly();
    public DecisionOutcome LastOutcome { get; private set; }
    public DecisionState State => state;

    internal UsPawnPlaybackSession(Pawn pawn, AuthorCatalog catalog, UsDevelopmentPolicy candidate)
    {
        this.pawn = pawn;
        this.catalog = catalog;
        policy = candidate.State;
        long now = Find.TickManager.TicksGame;
        long offset = candidate.InitialSpread == 0 ? 0 : (long)((uint)pawn.thingIDNumber % (ulong)candidate.InitialSpread);
        state = new DecisionState(now > long.MaxValue - offset ? long.MaxValue : now + offset);
        workTicks = candidate.Interval;
        woundedTicks = candidate.WoundedInterval;
        this.candidate = candidate;
    }

    public void Tick(string? eventKey = null, bool activeCommand = false, SqueakTriggerInvocation? compatibilityInvocation = null,
        string? registeredAction = null)
    {
        long now = Find.TickManager.TicksGame;
        if (eventKey == null && now < state.NextStateTick)
        {
            return;
        }

        var comp = pawn.GetComp<CompSqueaker>();
        var vocal = comp.ControlledOneShotVocal();
        var requested = registeredAction == null ? null : catalog.Actions.Single(x => x.Key == registeredAction);
        string? factEvent = requested == null ? eventKey : requested.BaseAction.Substring(requested.BaseAction.LastIndexOf('/') + 1);
        var values = UsPawnFacts.Capture(pawn, factEvent);
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var blockedReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        var domain = new BehaviorDomain("Pawn", "", pawn.def.defName,
            ModsConfig.BiotechActive ? pawn.genes?.Xenotype?.defName ?? "" : "");
        var plans = new List<ActionPlan>();
        var modulation = new Dictionary<string, ModulationPlan>(StringComparer.Ordinal);
        var numericalFacts = new Dictionary<string, double>(StringComparer.Ordinal);
        var stage = pawn.ageTracker?.CurLifeStage;
        if (stage != null)
        {
            numericalFacts["age.native"] = stage.voxPitch;
            numericalFacts["age.nativeVolume"] = stage.voxVolume;
        }
        var mood = pawn.needs?.mood;
        if (mood != null)
        {
            numericalFacts["mood.level"] = mood.CurLevelPercentage;
        }
        foreach (var action in catalog.Actions)
        {
            if (registeredAction != null && action.Key != registeredAction)
            {
                continue;
            }
            string legacyKey = action.BaseAction.Substring(action.BaseAction.LastIndexOf('/') + 1);
            var behavior = UniversalSqueakerMod.Settings.ResolveBehaviorSource(domain, legacyKey, catalog.BehaviorSources, catalog.HasFact, out bool paused,
                reason => blockedReasons[action.Key] = reason);
            if (paused || !comp.ControlledOneShotAllowed(legacyKey, activeCommand, behavior, authorAction: true))
            {
                blocked.Add(action.Key);
            }
            plans.Add(new ActionPlan(action.Key, action.Priority, action.Condition,
                behavior.HardIntervalTicks ?? (legacyKey == "Wounded" ? woundedTicks : workTicks),
                action.CheckShared, action.WriteShared, eventKey == null ? action.Chance : candidate.EventProbability, action.BaseAction,
                legacyKey == "Death" || behavior.TalkingEnabled == false, behavior.Volume ?? 1, behavior.Pitch ?? 1,
                action.Sustained || comp.ControlledPlaybackSustained(legacyKey)));
            modulation[action.Key] = ResponseModulation.Resolve(pawn.ThingID, behavior, numericalFacts, catalog.Responses);
        }
        if (compatibilityInvocation.HasValue)
        {
            string key = eventKey!;
            values["us.compat/Event"] = Truth.True;
            plans.Clear();
            var behavior = UniversalSqueakerMod.Settings.ResolveBehaviorSource(domain, key, catalog.BehaviorSources,
                catalog.HasFact, out bool paused);
            plans.Add(new ActionPlan(key, 0, new Condition("ref", "us.compat/Event"),
                behavior.HardIntervalTicks ?? workTicks, chance: candidate.EventProbability,
                skipTalking: behavior.TalkingEnabled == false, volume: behavior.Volume ?? 1, pitch: behavior.Pitch ?? 1,
                sustained: comp.ControlledPlaybackSustained(key)));
            if (paused || !comp.ControlledOneShotAllowed(key, activeCommand, behavior, authorAction: true))
            {
                blocked.Add(key);
            }
            modulation[key] = ResponseModulation.Resolve(pawn.ThingID, behavior, numericalFacts, catalog.Responses);
        }
        var snapshot = SqueakRuntimeResolver.Current;
        var context = snapshot.ResolveContext(pawn);
        var audioDomain = new Kernel.AudioDomain(new Kernel.RaceKey(pawn.def.defName),
            context.Xenotype == null ? null : new Kernel.XenotypeKey(context.Xenotype.defName));
        var input = new DecisionInput
        {
            Subject = pawn.ThingID,
            Domain = pawn.def.defName,
            Tick = now,
            ActiveSustainedKey = sustained.ActiveKey,
            Activity = UniversalSqueakerMod.Settings.activity,
            StateWaitFactor = candidate.StateWaitFactor,
            ActionModulation = modulation,
            AudioPool = snapshot.Registry,
            MergeAudioPool = true,
            AudioContext = new Kernel.SelectionContext(audioDomain, "", SqueakLifeStageResolver.Resolve(pawn), true, snapshot.AllowEggs),
            SoundGate = SqueakKernelAdapter.GateFor(pawn, pawn.MapHeld, null),
            StateOpportunity = eventKey == null,
            Eligible = pawn.Spawned
                && pawn.MapHeld == Find.CurrentMap
                && Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position)
                && SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled,
            HasVocalOrgans = vocal.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold,
            TalkingChance = CompSqueaker.ScaleFrequencyWithTalking && vocal.TalkingChance < SqueakVocalCapability.TalkingCertainThreshold ? vocal.TalkingChance : 1,
            Facts = catalog.Capture(pawn.ThingID, values),
            Actions = plans.ToArray(),
            Audio = UsAuthorVoicePacks.Selected(catalog.Audio, snapshot.Registry, audioDomain),
            SourceMode = SqueakKernelAdapter.ToSelectionMode(SqueakRuntimeResolver.Current.VoicePackMode),
            Fallback = SqueakKernelAdapter.BuildBuiltIn(),
            BlockedActions = blocked,
            BlockedActionReasons = blockedReasons
        };
        draws.Clear();
        preparedSound = null;
        LastOutcome = decision.Run(state, policy, input, this, this);
        lastObservation = now;
        try
        {
            comp.RecordKernelOutcome(LastOutcome, preparedSound);
        }
        catch (Exception error)
        {
            if (!diagnosticFaultReported)
            {
                diagnosticFaultReported = true;
                Log.Warning("usdiag committed kernel result; diagnostic publication failed: " + error.GetType().Name);
            }
        }
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
        {
            string active = sustained.ActiveKey;
            var action = catalog.Actions.FirstOrDefault(x => x.Key == active);
            string key = action == null ? active : action.BaseAction.Substring(action.BaseAction.LastIndexOf('/') + 1);
            var domain = new BehaviorDomain("Pawn", "", pawn.def.defName,
                ModsConfig.BiotechActive ? pawn.genes?.Xenotype?.defName ?? "" : "");
            var behavior = UniversalSqueakerMod.Settings.ResolveBehaviorSource(domain, key, catalog.BehaviorSources,
                catalog.HasFact, out bool paused);
            var comp = pawn.GetComp<CompSqueaker>();
            if (paused || !comp.ControlledOneShotAllowed(key, behavior: behavior, authorAction: true)
                || comp.ControlledOneShotVocal().VocalOrganEfficiency <= SqueakVocalCapability.VocalSilenceThreshold)
            {
                sustained.Stop("CurrentAuthorizationUnavailable");
                return;
            }
            sustained.Review(now, catalog.Capture(pawn.ThingID, UsPawnFacts.Capture(pawn, null)));
        }
        sustained.Maintain();
    }
    public void Stop() => sustained.Stop("SessionReleased");
    private sealed class NativeSustainedHandle : ISustainedHandle
    {
        private readonly Sustainer native;
        public NativeSustainedHandle(Sustainer native)
        {
            this.native = native;
        }
        public bool Active => !native.Ended;
        public void Maintain()
        {
            if (Active)
            {
                native.Maintain();
            }
        }
        public void End()
        {
            if (Active)
            {
                native.End();
            }
        }
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
            if (request.Sustained && sound.sustain)
            {
                info = SoundInfo.InMap(new TargetInfo(pawn), MaintenanceType.PerTick);
            }
            info.volumeFactor = 1;
            info.pitchFactor = pitch;
            preparedSound = sound;
            return true;
        }

        void Play()
        {
            int token = UsInstanceOutput.Mark(ref info, new UsOutputContribution(request.Audio.Volume));
            try
            {
                sound!.PlayOneShot(info);
            }
            finally
            {
                UsInstanceOutput.ReleaseRequest(token);
            }
        }

        if (request.Sustained)
        {
            try
            {
                if (!Preflight())
                {
                    return new PlaybackReceipt(PlaybackResult.Rejected, rejection);
                }
            }
            catch (Exception error)
            {
                return new PlaybackReceipt(PlaybackResult.Rejected, "Preparation:" + error.GetType().Name);
            }
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
                    Sustainer? native = sound!.TrySpawnSustainer(info);
                    if (native == null || native.Ended)
                    {
                        return new SustainedStart(new PlaybackReceipt(PlaybackResult.Indeterminate, "NoEffectiveSustainerAfterCall"));
                    }
                    return new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched, "NativeSustainerEstablished"), new NativeSustainedHandle(native));
                }
                finally
                {
                    UsInstanceOutput.ReleaseRequest(token);
                }
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
    private static readonly Dictionary<Pawn, UsPawnPlaybackSession> sessions = new();
    private static AuthorCatalog catalog = new(720, 216, fullDevCandidate: true);
    private static UsDevelopmentPolicy? selectedPolicy;
    public static string PolicyStatus => selectedPolicy == null ? "PolicySelectionRequired" : "ExplicitDevelopmentCandidate";
    public static IReadOnlyList<string> Issues => catalog.Issues;

    public static void SelectDevelopmentPolicy(UsDevelopmentPolicy policy)
    {
        if (!Prefs.DevMode)
        {
            throw new InvalidOperationException("Explicit development selection requires developer mode");
        }
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }
        if (selectedPolicy != null && !ReferenceEquals(selectedPolicy, policy))
        {
            throw new InvalidOperationException("A development policy is already selected for this process");
        }
        selectedPolicy = policy;
    }

    private static UsPawnPlaybackSession? SessionFor(Pawn pawn)
    {
        if (selectedPolicy == null)
        {
            return null;
        }
        if (sessions.TryGetValue(pawn, out var session))
        {
            return session;
        }
        if (pawn.GetComp<CompSqueaker>() == null || !pawn.Spawned || pawn.Destroyed)
        {
            return null;
        }
        if (session == null)
        {
            session = new UsPawnPlaybackSession(pawn, catalog, selectedPolicy);
            sessions.Add(pawn, session);
        }
        return session;
    }
    public static IEnumerable<string> DeclaredRaces => catalog.Audio.Select(x => x.Domain).Distinct(StringComparer.Ordinal);
    public static BehaviorSourceDirectory BehaviorSources => catalog.BehaviorSources;
    internal static string DiagnosticText(Pawn pawn) => sessions.TryGetValue(pawn, out var session)
        ? session.DiagnosticText : PolicyStatus;

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
                var identity = new SqueakVoicePackDef { defName = pack.defName, modContentPack = pack.modContentPack };
                if (identity.TryGetPackKey(out string selectionKey))
                {
                    catalog.AddPack(pack.modContentPack?.PackageId ?? "", pack.defName, pack.declaration, Bind, selectionKey);
                }
                else
                {
                    catalog.Issues.Add(pack.defName + ":SelectionIdentityUnavailable");
                }
            }
            else
            {
                catalog.Issues.Add(pack.defName + ":TargetRaceUnavailable");
            }
        }

        UsAuthorVoicePacks.Refresh(catalog);
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

    internal static void ReleasePawn(Pawn pawn)
    {
        if (sessions.TryGetValue(pawn, out var session))
        {
            session.Stop();
        }
        sessions.Remove(pawn);
    }
    public static bool IsControlled(Pawn pawn) => sessions.ContainsKey(pawn);
    public static bool TryTick(Pawn pawn)
    {
        var session = SessionFor(pawn);
        if (session == null)
        {
            return true;
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
        var session = SessionFor(pawn);
        if (session == null)
        {
            return true;
        }

        var invocation = new SqueakTriggerInvocation(origin, source);
        if (SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled && pawn.GetComp<CompSqueaker>().ControlledInvocationAllowed(invocation))
        {
            bool registered = catalog.Actions.Any(x => x.Key == key);
            session.Tick(key, invocation.IsActiveCommand,
                registered || BuiltInPawnContracts.CandidateOrder.Contains(key) ? null : invocation,
                registered ? key : null);
        }

        // Registered and unregistered keys share this owner; Comp's old cooldown writer is never called.
        return true;
    }

    public static void ResetSession()
    {
        foreach (var session in sessions.Values)
        {
            session.Stop();
        }
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

