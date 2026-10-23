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
    private PlaybackRequest? lastRequest;
    private long lastObservation;
    private bool diagnosticFaultReported;
    private bool endFaultReported;
    private BehaviorConfigurationSnapshot? configuration;
    private readonly Dictionary<string, EffectiveBehavior> effectiveActions = new(StringComparer.Ordinal);
    private string resultEvidence = "";
    private string ingestionEvidence = "";
    private string observationEvidence = "";
    internal string DiagnosticText => "tick=" + lastObservation + " action=" + LastOutcome.Action
        + " reason=" + LastOutcome.Reason + " receipt=" + LastOutcome.Receipt?.Result
        + " confirmed=" + state.ConfirmedTick + " sharedUntil=" + state.SharedDeadline
        + " actionUntil=" + state.ActionDeadline(LastOutcome.Action) + " nextState=" + state.NextStateTick
        + " active=" + sustained.ActiveKey + " ended=" + sustained.LastEndReason
        + " subject=" + pawn.ThingID + " role=" + lastRequest?.SubjectRole
        + " family=" + lastRequest?.Family + " domain=" + lastRequest?.Domain + " child=" + lastRequest?.ChildDomain
        + " sourceDomain=" + lastRequest?.Audio.SourceDomain + " tier=" + lastRequest?.Audio.SourceTier + " pack=" + lastRequest?.Audio.PackKey
        + " requestedEntry=" + lastRequest?.Audio.EntryKey + " requestedAudio=" + lastRequest?.Audio.AudioId
        + " localPitch=" + lastRequest?.Pitch + " localVolume=" + lastRequest?.Audio.Volume
        + " configView=" + configuration?.Id + " effective=" + string.Join(";", effectiveActions.Where(x => x.Key == LastOutcome.Action).Select(x => x.Key + ":" + string.Join(",", x.Value.Origins.Select(v => v.Key + "=" + v.Value)) + ":requires=" + string.Join(",", x.Value.Dependencies)))
        + " ingestion=" + ingestionEvidence
        + " result=" + resultEvidence
        + " observation=" + observationEvidence
        + " rolls=" + string.Join(",", draws.Select(x => x.Key + ":" + x.Value))
        + " missing=" + string.Join(",", (LastOutcome.Diagnostics ?? Array.Empty<FactDiagnostic>()).Select(x => x.Key + ":" + x.Reason));
    public IReadOnlyList<KeyValuePair<RandomPurpose, double>> LastRandomDraws => draws.AsReadOnly();
    public DecisionOutcome LastOutcome { get; private set; }
    public DecisionState State => state;

    private bool HasAvailableFact(string key) => catalog.HasFact(key)
        && (key != UsSelectObservation.FactKey || UsSelectObservation.Ready)
        && (key != "us.core/AttackEvent" || UsAttackObservation.Ready)
        && (key != "us.core/WorkState" || UsWorkObservation.Ready)
        && UsPawnEventObservation.Available(key) && UsIngestionObservation.Available(key);

    private static bool UsesEat(Condition condition) => condition.Operation == "ref" && (condition.Fact == EatPolicy.Process || condition.Fact == EatPolicy.Execution)
        || condition.Children.Any(UsesEat);
    internal static bool ConsumesIngestionResult(ActionPlan action)
        => action.Condition.RequiredFacts().Contains(EatPolicy.Result, StringComparer.Ordinal);
    private static Condition EffectiveCondition(ActionPlan action, BehaviorValues behavior)
    {
        var condition = UsesEat(action.Condition) ? new Condition("all", "", action.Condition, EatConditions.Restriction(
            behavior.Phase ?? EatPhase.WholeProcess, behavior.NutritionScope ?? EatNutritionScope.Any, behavior.DrugScope ?? EatDrugScope.Any)) : action.Condition;
        return SelectConditions.UsesSelection(action.Condition) && SelectPolicy.HasParameters(behavior)
            ? new Condition("all", "", condition, SelectConditions.Restriction(behavior)) : condition;
    }
    private BehaviorValues Defaults(ActionPlan action) => new()
    {
        Scope = pawn.GetComp<CompSqueaker>().ControlledOneShotScope(action.Key),
        Enabled = true, HardIntervalTicks = Kernel.ActionKey.SchemaKey(action.Key) != action.Key ? action.Key == "us.core/Wounded" ? woundedTicks : workTicks : action.Interval,
        TalkingEnabled = !action.SkipTalking, Volume = 1, Pitch = 1,
        Phase = UsesEat(action.Condition) ? EatPhase.WholeProcess : null,
        NutritionScope = UsesEat(action.Condition) ? EatNutritionScope.Any : null,
        DrugScope = UsesEat(action.Condition) ? EatDrugScope.Any : null,
        SelectRequirePlayerControlled = action.Key == "us.core/Select" ? true : null,
        SelectRequireAwake = action.Key == "us.core/Select" ? true : null,
        SelectRequireNotDowned = action.Key == "us.core/Select" ? true : null
    };

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

    internal void Tick(string? eventKey = null, bool activeCommand = false, SqueakTriggerInvocation? compatibilityInvocation = null,
        string? registeredAction = null, UsIngestionObservation.Result? ingestionResult = null, string evidence = "")
    {
        long now = Find.TickManager.TicksGame;
        if (eventKey == null && now < state.NextStateTick)
        {
            return;
        }

        var comp = pawn.GetComp<CompSqueaker>();
        observationEvidence = evidence != "" ? evidence : eventKey == null
            ? "StateOpportunity:job=" + pawn.CurJob?.def?.defName + ",driver=" + pawn.jobs?.curDriver?.GetType().FullName
            : "Event:" + eventKey;
        var vocal = comp.ControlledOneShotVocal();
        var values = UsPawnFacts.Capture(pawn, eventKey, out var capturedDiagnostics);
        bool commandContext = activeCommand || eventKey == null && values["us.core/ActiveCommand"] == Truth.True;
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var blockedReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        var snapshot = SqueakRuntimeResolver.Current;
        var context = snapshot.ResolveContext(pawn);
        var audioDomain = new Kernel.AudioDomain(new Kernel.RaceKey(pawn.def.defName),
            context.Xenotype == null ? null : new Kernel.XenotypeKey(context.Xenotype.defName));
        var domain = new BehaviorDomain("Pawn", "", pawn.def.defName,
            context.Xenotype?.defName ?? "");
        var plans = new List<ActionPlan>();
        var modulation = new Dictionary<string, ModulationPlan>(StringComparer.Ordinal);
        var numericalFacts = new Dictionary<string, double>(StringComparer.Ordinal);
        configuration = UniversalSqueakerMod.Settings.CaptureBehaviorConfiguration();
        ingestionEvidence = UsIngestionObservation.Describe(pawn);
        effectiveActions.Clear();
        var resultFact = ingestionResult == null ? null : new IngestionResultFact(ingestionResult.Ingester.ThingID,
            ingestionResult.ItemId, ingestionResult.ItemDef, ingestionResult.Nutrition);
        values[EatPolicy.Result] = UsIngestionObservation.Ingested == null ? Truth.Unknown : resultFact == null ? Truth.False : Truth.True;
        resultEvidence = "";
        if (ingestionResult != null)
        {
            numericalFacts["ingestion.nutrition"] = ingestionResult.Nutrition;
            resultEvidence = "ingester=" + ingestionResult.Ingester.ThingID + ",item=" + ingestionResult.ItemId
                + ",def=" + ingestionResult.ItemDef + ",nutrition=" + ingestionResult.Nutrition;
        }
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
            // A completion notification is an opportunity for its explicit fixed-contract consumers,
            // not an extra state opportunity for ordinary Eat (or another still-true state).
            // RequiredFacts traverses the compiled tree, including inherited/nested conditions.
            if (ingestionResult != null && !ConsumesIngestionResult(action))
            {
                continue;
            }
            if (registeredAction != null && action.Key != registeredAction)
            {
                continue;
            }
            string actionId = action.Key;
            var effective = configuration.Resolve(domain, actionId, Defaults(action), catalog.BehaviorSources, HasAvailableFact, action.Condition.RequiredFacts());
            effectiveActions[actionId] = effective;
            var behavior = effective.Read();
            if (effective.Paused) blockedReasons[actionId] = effective.Failure;
            bool invalidEatFields = !UsesEat(action.Condition) && (behavior.Phase.HasValue || behavior.NutritionScope.HasValue || behavior.DrugScope.HasValue);
            bool invalidSelectFields = !SelectConditions.UsesSelection(action.Condition) && SelectPolicy.HasParameters(behavior);
            if (invalidEatFields) blockedReasons[actionId] = "EatParametersRequireIngestionContract";
            if (invalidSelectFields) blockedReasons[actionId] = "SelectParametersRequireSelectionContract";
            if (!configuration.SystemEnabled || effective.Paused || invalidEatFields || invalidSelectFields || !comp.ControlledOneShotAllowed(actionId, commandContext, behavior, authorAction: true))
            {
                blocked.Add(action.Key);
            }
            if (actionId == "us.core/Equip" && (candidate.EquipQualification == UsEquipQualification.Unselected
                || values["us.core/PlayerEquipCommand"] != Truth.True))
            {
                blocked.Add(actionId);
                blockedReasons[actionId] = candidate.EquipQualification == UsEquipQualification.Unselected
                    ? "EquipQualificationRequired" : "ExplicitPlayerCoreEquipQualification";
            }
            bool builtIn = Kernel.ActionKey.SchemaKey(actionId) != actionId;
            plans.Add(new ActionPlan(action.Key, action.Priority, EffectiveCondition(action, behavior),
                behavior.HardIntervalTicks ?? (builtIn ? actionId == "us.core/Wounded" ? woundedTicks : workTicks : action.Interval),
                action.CheckShared, action.WriteShared, eventKey == null ? action.Chance : action.Chance * candidate.EventProbability, action.BaseAction,
                behavior.TalkingEnabled.HasValue ? !behavior.TalkingEnabled.Value : action.SkipTalking, behavior.Volume ?? 1, behavior.Pitch ?? 1,
                action.Sustained || comp.ControlledPlaybackSustained(actionId), action.RequiresVocal, action.SubjectRole));
            modulation[action.Key] = ResponseModulation.Resolve(pawn.ThingID, behavior, numericalFacts, catalog.Responses, resultFact);
        }
        if (compatibilityInvocation.HasValue)
        {
            string key = eventKey!;
            values["us.compat/Event"] = Truth.True;
            plans.Clear();
            var effective = configuration.Resolve(domain, key, new BehaviorValues(), catalog.BehaviorSources, HasAvailableFact);
            var behavior = effective.Read();
            plans.Add(new ActionPlan(key, 0, new Condition("ref", "us.compat/Event"),
                behavior.HardIntervalTicks ?? workTicks, chance: candidate.EventProbability,
                skipTalking: behavior.TalkingEnabled == false, volume: behavior.Volume ?? 1, pitch: behavior.Pitch ?? 1,
                sustained: comp.ControlledPlaybackSustained(key)));
            if (!configuration.SystemEnabled || effective.Paused || !comp.ControlledOneShotAllowed(key, activeCommand, behavior, authorAction: true))
            {
                blocked.Add(key);
            }
            modulation[key] = ResponseModulation.Resolve(pawn.ThingID, behavior, numericalFacts, catalog.Responses);
        }
        var input = new DecisionInput
        {
            Subject = pawn.ThingID,
            SubjectRole = eventKey == "us.core/Select" ? "SelectedSubject" : eventKey == "us.core/Attack" ? "Attacker"
                : eventKey == "us.core/Wounded" ? "DamageRecipient" : ingestionResult != null ? "ActualIngester" : "Subject",
            Domain = pawn.def.defName,
            ChildDomain = audioDomain.Xenotype?.DefName ?? "",
            Tick = now,
            ActiveSustainedKey = sustained.ActiveKey,
            Activity = configuration.Activity,
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
            Facts = catalog.Capture(pawn.ThingID, values, capturedDiagnostics, resultFact),
            Actions = plans.ToArray(),
            Audio = UsAuthorVoicePacks.Selected(catalog.Audio, snapshot.Registry, audioDomain),
            SourceMode = SqueakKernelAdapter.ToSelectionMode(SqueakRuntimeResolver.Current.VoicePackMode),
            Fallback = SqueakKernelAdapter.BuildBuiltIn(),
            BlockedActions = blocked,
            BlockedActionReasons = blockedReasons
        };
        draws.Clear();
        preparedSound = null;
        lastRequest = null;
        LastOutcome = decision.Run(state, policy, input, this, this);
        lastObservation = now;
        try
        {
            comp.RecordKernelOutcome(LastOutcome, preparedSound, lastRequest);
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

    public void Maintain(bool force = false)
    {
        if (!pawn.Spawned || pawn.Destroyed || pawn.MapHeld != Find.CurrentMap
            || SqueakRuntimeResolver.Current.VoicePackMode == SqueakVoicePackMode.Disabled)
        {
            EndSustained("SubjectOrServiceUnavailable");
            return;
        }
        long now = Find.TickManager.TicksGame;
        if (force || sustained.ReviewDue(now))
        {
            string active = sustained.ActiveKey;
            var action = catalog.Actions.FirstOrDefault(x => x.Key == active);
            string key = active;
            var domain = new BehaviorDomain("Pawn", "", pawn.def.defName,
                SqueakRuntimeResolver.Current.ResolveContext(pawn).Xenotype?.defName ?? "");
            var view = UniversalSqueakerMod.Settings.CaptureBehaviorConfiguration();
            var effective = view.Resolve(domain, key, action == null ? new BehaviorValues() : Defaults(action), catalog.BehaviorSources, HasAvailableFact, action?.Condition.RequiredFacts());
            var behavior = effective.Read();
            var comp = pawn.GetComp<CompSqueaker>();
            bool invalidEatFields = action != null && !UsesEat(action.Condition) && (behavior.Phase.HasValue || behavior.NutritionScope.HasValue || behavior.DrugScope.HasValue);
            bool invalidSelectFields = action != null && !SelectConditions.UsesSelection(action.Condition) && SelectPolicy.HasParameters(behavior);
            if (!view.SystemEnabled || effective.Paused || invalidEatFields || invalidSelectFields || !comp.ControlledOneShotAllowed(key,
                activeCommand: pawn.CurJob?.playerForced == true && pawn.IsPlayerControlled, behavior: behavior, authorAction: true)
                || action?.RequiresVocal == true && comp.ControlledOneShotVocal().VocalOrganEfficiency <= SqueakVocalCapability.VocalSilenceThreshold)
            {
                EndSustained("CurrentAuthorizationUnavailable");
                return;
            }
            sustained.Review(now, UsPawnFacts.CaptureSnapshot(catalog, pawn, null), action == null ? null : EffectiveCondition(action, behavior), force);
            ReportEndFault();
        }
        sustained.Maintain();
    }
    public void Stop() => EndSustained("SessionReleased");

    private void EndSustained(string reason)
    {
        sustained.Stop(reason);
        ReportEndFault();
    }

    private void ReportEndFault()
    {
        if (!endFaultReported && sustained.LastEndReason.Contains(":EndFault:"))
        {
            endFaultReported = true;
            Log.Warning("usdiag owned sustained end uncertain; slot detached, no repeated End: " + sustained.LastEndReason);
        }
    }
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
        lastRequest = request;
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
            if (request.Subject != pawn.ThingID || request.Domain != pawn.def.defName
                || request.Family != SubjectFamily.PawnRace || request.TargetNamespace != ""
                || request.Audio.Domain != request.Domain || request.Audio.SourceDomain.IsNonPawn
                || request.Audio.SourceDomain.Race.DefName != pawn.def.defName
                || request.Audio.SourceDomain.Xenotype.HasValue && request.Audio.SourceDomain.Xenotype.Value.DefName != request.ChildDomain
                || request.ChildDomain != (SqueakRuntimeResolver.Current.ResolveContext(pawn).Xenotype?.defName ?? ""))
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
            var result = sustained.Dispatch(request, sound!.sustain, () =>
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
            ReportEndFault();
            return result;
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
    internal static bool DevelopmentActive => selectedPolicy != null;
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
    public static IEnumerable<string> DeclaredRaces => catalog.Audio.Where(x => x.Family == SubjectFamily.PawnRace).Select(x => x.Domain).Distinct(StringComparer.Ordinal);
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

        catalog.AddActions(DefDatabase<UsActionDef>.AllDefsListForReading.Select(x => x.declaration));

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
            try
            {
                var domain = pack.declaration.Domain;
                if (domain.Family != "Pawn")
                {
                    catalog.Issues.Add(pack.defName + ":NonPawnHostUnavailable");
                    continue;
                }
                var target = DefDatabase<ThingDef>.GetNamedSilentFail(domain.Target);
                if (target?.race == null || target.category != ThingCategory.Pawn || target.thingClass == null
                    || !typeof(Pawn).IsAssignableFrom(target.thingClass)
                    || domain.Child != "" && (!ModsConfig.BiotechActive || DefDatabase<XenotypeDef>.GetNamedSilentFail(domain.Child) == null))
                {
                    catalog.Issues.Add(pack.defName + ":TargetDomainUnavailable");
                    continue;
                }
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
            catch (Exception error)
            {
                catalog.Issues.Add(pack.defName + ":InvalidTarget:" + error.Message);
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
    internal static void IngestionChanged(Pawn pawn)
    {
        if (sessions.TryGetValue(pawn, out var session)) session.Maintain(true);
    }
    internal static void IngestionResult(UsIngestionObservation.Result result)
    {
        // No consumer means no decision, owner allocation, random draw or settlement.
        if (!catalog.Actions.Any(UsPawnPlaybackSession.ConsumesIngestionResult)) return;
        SessionFor(result.Ingester)?.Tick(EatPolicy.Result, ingestionResult: result);
    }
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
        => TryObservedEvent(pawn, key, origin, source, "CallerSupplied");

    internal static bool TryObservedEvent(Pawn pawn, string key, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence)
    {
        var session = SessionFor(pawn);
        if (session == null)
        {
            return true;
        }

        key = Kernel.ActionKey.DefinitionId(key);
        var invocation = new SqueakTriggerInvocation(origin, source);
        if (SqueakRuntimeResolver.Current.VoicePackMode != SqueakVoicePackMode.Disabled && pawn.GetComp<CompSqueaker>().ControlledInvocationAllowed(invocation))
        {
            bool registered = catalog.Actions.Any(x => x.Key == key);
            session.Tick(key, invocation.IsActiveCommand,
                registered ? null : invocation,
                // A native notification publishes facts, not a forced wrapper identity.
                // Independent roots and narrowed children using that fact enter the same arbitration.
                // An explicit public call naming an author action still targets that definition.
                registered && Kernel.ActionKey.SchemaKey(key) == key ? key : null, evidence: evidence);
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
        UsIngestionObservation.Reset();
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

