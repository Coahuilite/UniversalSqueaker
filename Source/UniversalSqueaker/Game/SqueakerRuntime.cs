using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using RimWorld;
using UnityEngine;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

public sealed class UsVoicePackDef : Def
{
    public VoicePackDeclaration declaration = new();
}

public sealed class UsActionDef : Def
{
    public ActionDeclaration declaration = new();
}

public sealed class UsNotificationDef : Def
{
    public string actionId = "";
    public long intervalTicks;
    public int priority;
}

public sealed class UsBehaviorSourceDef : Def
{
    public BehaviorSourceDeclaration declaration = new();
}

public sealed class UsProviderDef : Def
{
    public Type providerClass = null!;
}

/// <summary>1.6 fact/output adapter. The neutral ResponseSession is the sole decision/lifetime owner.</summary>
public sealed class PawnResponseAdapter : IResponseOutput, IResponseRandom
{
    private readonly Pawn pawn;
    private readonly DefinitionCatalog catalog;
    private readonly ResponseSession owner;
    private DecisionState state => owner.State;
    private UsResponsePolicy candidate;
    private readonly List<KeyValuePair<RandomPurpose, double>> draws = new();
    private SoundDef? preparedSound;
    private PlaybackRequest? lastRequest;
    private long lastObservation;
    private bool diagnosticFaultReported;
    private bool endFaultReported;
    private BehaviorConfiguration? reviewedConfiguration;
    private Kernel.AudioCatalog? reviewedAudio;
    private BehaviorConfiguration? configuration;
    private SessionObservation? decisionObservation;
    private double? decisionInteractionUntil;
    private long decisionAudioSnapshot;
    private string decisionEndReason = "";
    private StateScheduleObservation? decisionSchedule;
    private long? scheduleBasisConfiguration, scheduleBasisSequence;
    private long? maintenanceTick, maintenanceConfiguration, maintenanceAudioSnapshot, reviewTick, reviewConfigurationId, reviewAudioSnapshot;
    private double? maintenanceSeconds;
    private string maintenanceResult = "NotMaintained";
    private string slotAction = "", endedAction = "", endedReason = "";
    private long? slotConfiguration, slotAudioSnapshot, endObservedTick;
    private long observedEndRevision;
    private double? endObservedSeconds;
    private IReadOnlyDictionary<string, EffectiveBehavior> effectiveActions => owner.EffectiveActions;
    private string resultEvidence = "";
    private string ingestionEvidence = "";
    private string observationEvidence = "";
    private InteractionOpportunity? observedInteraction;
    private string observedEventFact = "";
    private string requestedAction = "";
    private string observedRole = "", observedRace = "", observedChild = "";
    internal bool HasEvaluation { get; private set; }
    internal long ObservationSequence { get; private set; }
    private bool detailedEvidenceCaptured;
    internal string DiagnosticText => !HasEvaluation ? "stage=NotEvaluated subject=" + owner.Subject
        : "tick=" + lastObservation + " action=" + LastOutcome.Action
        + " reason=" + LastOutcome.Reason + " receipt=" + LastOutcome.Receipt?.Result
        + " confirmed=" + state.ConfirmedTick + " sharedUntil=" + state.SharedDeadline
        + " actionUntil=" + state.ActionDeadline(LastOutcome.Action ?? "") + " nextState=" + state.NextStateTick
        + " gate=" + (state.LastGate == "" ? LastOutcome.Reason : state.LastGate)
        + " simulationAttemptTick=" + state.AttemptTick
        + " opportunity=" + (observedInteraction.HasValue ? "AcceptedInteraction" : requestedAction == "" ? "SimulationState" : "SimulationEvent")
        + " operation=" + observedInteraction?.OperationId + " eventFact=" + observedEventFact
        + " monotonicSeconds=" + (observedInteraction?.MonotonicSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable")
        + " secondsUntil=" + state.InteractionDeadline(LastOutcome.Action ?? "").ToString("R", System.Globalization.CultureInfo.InvariantCulture)
        + " observationScope=LatestDecisionAndCurrentSlot decisionAudioSnapshot=" + decisionAudioSnapshot
        + " active=" + owner.ActiveAction + " ended=" + owner.LastEndReason
        + " maintenanceTick=" + maintenanceTick + " maintenance=" + maintenanceResult
        + " subject=" + owner.Subject + " nativeLabel=" + pawn.ThingID + " role=" + lastRequest?.SubjectRole
        + " family=" + lastRequest?.Family + " domain=" + lastRequest?.Domain + " child=" + lastRequest?.ChildDomain
        + " sourceDomain=" + lastRequest?.Audio.SourceDomain + " tier=" + lastRequest?.Audio.SourceTier + " pack=" + lastRequest?.Audio.PackKey
        + " requestedEntry=" + lastRequest?.Audio.EntryKey + " requestedAudio=" + lastRequest?.Audio.AudioId
        + " localPitch=" + lastRequest?.Pitch + " localVolume=" + lastRequest?.Audio.Volume
        + " configView=" + configuration?.Id + " effective=" + string.Join(";", effectiveActions.Where(x => x.Key == (LastOutcome.Action == "" ? requestedAction : LastOutcome.Action)).Select(x => x.Key
            + (observedInteraction.HasValue ? ":intervalUnit=realSeconds:interval=" + x.Value.Read().InteractionIntervalSeconds
                : ":intervalUnit=gameTicks:interval=" + x.Value.Read().SimulationIntervalTicks)
            + ":" + string.Join(",", x.Value.Origins.Select(v => v.Key + "=" + v.Value)) + ":requires=" + string.Join(",", x.Value.Dependencies)))
        + " ingestion=" + ingestionEvidence
        + " result=" + resultEvidence
        + " observation=" + observationEvidence
        + " rolls=" + string.Join(",", draws.Select(x => x.Key + ":" + x.Value))
        + " missing=" + string.Join(",", (LastOutcome.Diagnostics ?? Array.Empty<FactDiagnostic>()).Select(x => x.Key + ":" + x.Reason));
    internal SqueakResponseLogData CaptureResponseLog() => new()
    {
        Subject = owner.Subject, RequestedAction = requestedAction, Action = LastOutcome.Action,
        Reason = LastOutcome.Reason, Receipt = LastOutcome.Receipt?.Result.ToString() ?? "NotRequested",
        ReceiptReason = LastOutcome.Receipt?.Reason ?? "", Tick = lastObservation,
        Opportunity = observedInteraction.HasValue ? "AcceptedInteraction" : requestedAction == "" ? "SimulationState" : "SimulationEvent",
        EventFact = observedEventFact, Operation = observedInteraction?.OperationId,
        MonotonicSeconds = observedInteraction?.MonotonicSeconds,
        SecondsUntil = observedInteraction.HasValue ? state.InteractionDeadline(LastOutcome.Action ?? "") : null,
        Confirmed = state.ConfirmedTick, SharedUntil = state.SharedDeadline, ActionUntil = state.ActionDeadline(LastOutcome.Action ?? ""),
        NextState = state.NextStateTick, SimulationAttempt = state.AttemptTick,
        Role = observedRole, Domain = observedRace,
        ChildDomain = observedChild, SourceDomain = lastRequest?.Audio.SourceDomain.ToString() ?? "",
        Tier = lastRequest?.Audio.SourceTier?.ToString() ?? "", Pack = lastRequest?.Audio.PackKey ?? "",
        Entry = lastRequest?.Audio.EntryKey ?? "", Audio = lastRequest?.Audio.AudioId ?? "",
        Configuration = configuration?.Id, Active = owner.ActiveAction, EndReason = owner.LastEndReason,
        Details = DiagnosticText
    };
    public IReadOnlyList<KeyValuePair<RandomPurpose, double>> LastRandomDraws => draws.AsReadOnly();
    public DecisionOutcome LastOutcome { get; private set; }
    public DecisionState State => state;
    internal SessionObservation Observation => owner.Observe();
    internal bool OnMap(Map map) => pawn.MapHeld == map;

    internal SqueakResponseEvidence CaptureDiagnosticEvidence()
    {
        string action = LastOutcome.Action ?? "";
        SessionObservation? current = HasEvaluation ? Observation : null;
        double? currentInteractionUntil = HasEvaluation && action != "" && observedInteraction.HasValue
            ? state.InteractionDeadline(action) : null;
        var evidence = new SqueakResponseEvidence(HasEvaluation ? "Evaluated" : "NotEvaluated", ObservationSequence,
            current, action,
            HasEvaluation ? LastOutcome.Receipt?.Result.ToString() ?? "NotRequested" : null,
            LastOutcome.Reason, observedInteraction.HasValue,
            currentInteractionUntil, decisionSchedule.HasValue ? "AdoptedSchedule" : "NotScheduledByDecision",
            "CurrentSlotAndLatestMaintenance", decisionObservation);
        evidence.Add("observation_scope", "LatestDecision;CurrentSlot;LatestMaintenance");
        evidence.Add("subject", owner.Subject); evidence.Add("next_state_tick", state.NextStateTick);
        evidence.Add("phase_input", state.PhaseInput.HasValue ? "Ready" : "NotReady");
        evidence.Add("phase_policy", candidate.State.Phase == null ? "NotAdopted" : StatePhasePolicy.AlgorithmVersion);
        evidence.Add("phase_active", state.PhaseActive(candidate.State));
        evidence.Add("current_active", owner.ActiveAction == "" ? "None" : owner.ActiveAction);
        evidence.Add("slot_start_configuration", slotConfiguration); evidence.Add("slot_start_audio_snapshot", slotAudioSnapshot);
        evidence.Add("maintenance_tick", maintenanceTick); evidence.Add("maintenance_monotonic_seconds", maintenanceSeconds);
        evidence.Add("maintenance_result", maintenanceResult); evidence.Add("maintenance_configuration", maintenanceConfiguration);
        evidence.Add("maintenance_audio_snapshot", maintenanceAudioSnapshot);
        evidence.Add("review_tick", reviewTick); evidence.Add("review_configuration", reviewConfigurationId);
        evidence.Add("review_audio_snapshot", reviewAudioSnapshot);
        evidence.Add("end_observed_tick", endObservedTick); evidence.Add("end_observed_monotonic_seconds", endObservedSeconds);
        evidence.Add("end_action", endedAction); evidence.Add("end_observed_reason", endedReason);
        evidence.Add("end_timing", "ObservedAtAdapterBoundary;NotNativeCompletionTime");
        // The adopted schedule survives configuration/policy changes until a real new schedule.
        // SharedRaise reuses its previous W/phase basis rather than assigning the latest config.
        if (decisionSchedule is StateScheduleObservation schedule)
        {
            evidence.Add("schedule_kind", schedule.Kind); evidence.Add("schedule_lower_bound_tick", schedule.LowerBound);
            evidence.Add("schedule_next_tick", schedule.NextTick); evidence.Add("schedule_w_ticks", schedule.EffectiveWait);
            evidence.Add("schedule_phase_window_ticks", schedule.Window); evidence.Add("schedule_phase_pattern", schedule.Pattern);
            evidence.Add("schedule_shared_raise_window_ticks", schedule.RaisedWindow);
            evidence.Add("schedule_basis_configuration", scheduleBasisConfiguration);
            evidence.Add("schedule_basis_sequence", scheduleBasisSequence);
            evidence.Add("schedule_basis", scheduleBasisSequence.HasValue ? "Decision" : "InitialPolicyOnly");
        }
        if (!HasEvaluation) return evidence;
        evidence.Add("decision_observation", decisionObservation.HasValue ? "Captured" : "Unavailable");
        evidence.Add("tick", lastObservation); evidence.Add("requested_action", requestedAction); evidence.Add("action", action);
        evidence.Add("reason", LastOutcome.Reason); evidence.Add("receipt", LastOutcome.Receipt?.Result.ToString() ?? "NotRequested");
        evidence.Add("receipt_reason", LastOutcome.Receipt?.Reason); evidence.Add("role", observedRole);
        evidence.Add("domain", observedRace); evidence.Add("child", observedChild); evidence.Add("root", "NotCaptured");
        evidence.Add("opportunity", observedInteraction.HasValue ? "AcceptedInteraction" : requestedAction == "" ? "SimulationState" : "SimulationEvent");
        evidence.Add("event_fact", observedEventFact); evidence.Add("operation", observedInteraction?.OperationId);
        evidence.Add("monotonic_seconds", observedInteraction?.MonotonicSeconds);
        evidence.Add("interaction_until_seconds", currentInteractionUntil);
        evidence.Add("action_until_tick", action != "" && !observedInteraction.HasValue ? current?.ActionDeadline : null);
        evidence.Add("shared_until_tick", current?.SharedDeadline); evidence.Add("confirmed_tick", current?.ConfirmedTick);
        evidence.Add("simulation_attempt_tick", state.AttemptTick); evidence.Add("configuration", configuration?.Id);
        evidence.Add("decision_audio_snapshot", decisionAudioSnapshot); evidence.Add("decision_active", decisionObservation?.ActiveAction);
        evidence.Add("decision_end_reason", decisionEndReason);
        evidence.Add("decision_interaction_until_seconds", decisionInteractionUntil);
        evidence.Add("decision_action_until_tick", action != "" && !observedInteraction.HasValue ? decisionObservation?.ActionDeadline : null);
        evidence.Add("decision_shared_until_tick", decisionObservation?.SharedDeadline);
        evidence.Add("decision_confirmed_tick", decisionObservation?.ConfirmedTick);
        evidence.Add("decision_next_state_tick", decisionObservation?.NextStateTick);
        evidence.Add("active", owner.ActiveAction); evidence.Add("end_reason", owner.LastEndReason);
        evidence.Add("source_domain", lastRequest?.Audio.SourceDomain); evidence.Add("tier", lastRequest?.Audio.SourceTier);
        evidence.Add("pack", lastRequest?.Audio.PackKey); evidence.Add("entry", lastRequest?.Audio.EntryKey);
        evidence.Add("audio", lastRequest?.Audio.AudioId); evidence.Add("local_pitch", lastRequest?.Pitch);
        evidence.Add("local_volume", lastRequest?.Audio.Volume);
        evidence.Add("details", detailedEvidenceCaptured ? "CapturedBounded" : "NotCaptured");
        if (detailedEvidenceCaptured)
        {
            if (effectiveActions.TryGetValue(action, out var effective))
                foreach (var origin in effective.Origins) evidence.Detail("origin." + SqueakResponseEvidence.Safe(origin.Key), origin.Value);
            foreach (var draw in draws) evidence.Detail("roll." + draw.Key, draw.Value);
            foreach (var missing in LastOutcome.Diagnostics ?? Array.Empty<FactDiagnostic>())
                evidence.Detail("missing." + SqueakResponseEvidence.Safe(missing.Key), missing.Reason);
        }
        return evidence;
    }

    private bool HasAvailableFact(string key) => SqueakerRuntime.ImplementationAvailable(key);

    internal PawnResponseAdapter(Pawn pawn, DefinitionCatalog catalog, UsResponsePolicy candidate)
    {
        this.pawn = pawn;
        this.catalog = catalog;
        this.candidate = candidate;
        long now = Find.TickManager.TicksGame;
        owner = new ResponseSession(SqueakerRuntime.SubjectKey(pawn), now, candidate.State, candidate.InitialSpread,
            pawn.thingIDNumber, SqueakerRuntime.PhaseInputFor(pawn), candidate.UsesNativeRhythm ? Find.TickManager.TickRateMultiplier : 1);
        decisionSchedule = state.Schedule;
    }

    internal void UsePolicy(UsResponsePolicy policy)
    {
        candidate = policy;
        owner.UsePolicy(policy.State);
        // The next existing maintenance pass reviews active authorization, even if configuration
        // is unchanged. Selection itself captures no facts and creates no response opportunity.
        reviewedConfiguration = null;
    }

    private ResponseFrame Capture(BehaviorConfiguration accepted, string? eventKey, bool activeCommand = false, string? registeredAction = null,
        UsIngestionObservation.Result? result = null, string? notificationFact = null,
        InteractionOpportunity? interaction = null, bool captureEvidence = true)
    {
        var snapshot = GameRuntime.Current;
        var context = snapshot.ResolveContext(pawn, accepted);
        var values = UsPawnFacts.Capture(pawn, eventKey, out var diagnostics);
        if (notificationFact != null) values[notificationFact] = Truth.True;
        var ingestion = result == null ? null : new IngestionResultFact(SqueakerRuntime.SubjectKey(result.Ingester), result.ItemId, result.ItemDef, result.Nutrition);
        values[EatPolicy.Result] = UsIngestionObservation.Ingested == null ? Truth.Unknown : ingestion == null ? Truth.False : Truth.True;
        bool detailed = pawn.GetComp<CompSqueaker>().WantsDetailedDiagnostics;
        if (captureEvidence)
        {
            resultEvidence = !detailed || result == null ? "" : "ingester=" + result.Ingester.ThingID + ",item=" + result.ItemId
                + ",def=" + result.ItemDef + ",nutrition=" + result.Nutrition;
            ingestionEvidence = detailed ? UsIngestionObservation.Describe(pawn) : "";
        }
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);
        if (result != null) numbers["ingestion.nutrition"] = result.Nutrition;
        var stage = pawn.ageTracker?.CurLifeStage;
        if (stage != null) { numbers["age.native"] = stage.voxPitch; numbers["age.nativeVolume"] = stage.voxVolume; }
        if (pawn.needs?.mood != null) numbers["mood.level"] = pawn.needs.mood.CurLevelPercentage;
        var vocal = pawn.GetComp<CompSqueaker>().ControlledOneShotVocal();
        return new ResponseFrame
        {
            Subject = owner.Subject, Domain = new BehaviorDomain("Pawn", "", pawn.def.defName, context.Xenotype?.defName ?? ""),
            Tick = Find.TickManager.TicksGame, StateOpportunity = eventKey == null, ActionFilter = registeredAction,
            EventFact = eventKey == null ? "" : notificationFact ?? (result != null ? EatPolicy.Result : BuiltInPawnContracts.FactKey(eventKey)), Interaction = interaction,
            SubjectRole = eventKey == CoreActions.Select ? "SelectedSubject" : eventKey == CoreActions.Attack ? "Attacker"
                : eventKey == CoreActions.Wounded ? "DamageRecipient" : result != null ? "ActualIngester" : "Subject",
            ActiveCommand = activeCommand || eventKey == null && values["us.core/ActiveCommand"] == Truth.True,
            Eligible = pawn.Spawned && !pawn.Destroyed && pawn.MapHeld == Find.CurrentMap
                && Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position)
                && accepted.SystemEnabled,
            HasVocalOrgans = vocal.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold,
            TalkingChance = accepted.TalkingScaling && vocal.TalkingChance < SqueakVocalCapability.TalkingCertainThreshold ? vocal.TalkingChance : 1,
            StateWaitFactor = candidate.UsesNativeRhythm && eventKey == null ? CompSqueaker.StateRhythmFactor() : 1,
            TargetSpeedFactor = candidate.UsesNativeRhythm && eventKey == null ? Find.TickManager.TickRateMultiplier : 1,
            Facts = catalog.Capture(owner.Subject, values, diagnostics, ingestion), NumericFacts = numbers,
            AvailableFacts = new HashSet<string>(catalog.FactKeys.Where(HasAvailableFact), StringComparer.Ordinal),
            Age = SqueakLifeStageResolver.Resolve(pawn),
            SoundGate = SqueakKernelAdapter.GateFor(pawn, pawn.MapHeld, null),

        };
    }

    internal void Tick(string? eventKey = null, bool activeCommand = false, string? registeredAction = null,
        UsIngestionObservation.Result? ingestionResult = null, string evidence = "", string? notificationFact = null,
        InteractionOpportunity? interaction = null)
    {
        long now = Find.TickManager.TicksGame;
        if (!state.PhaseInput.HasValue && SqueakerRuntime.PhaseInputFor(pawn) is StatePhaseInput phase)
            state.BindPhaseInput(phase);
        if (eventKey == null && now < owner.State.NextStateTick) return;
        detailedEvidenceCaptured = pawn.GetComp<CompSqueaker>().WantsDetailedDiagnostics;
        observationEvidence = !detailedEvidenceCaptured ? ""
            : evidence != "" ? evidence : eventKey == null
            ? "StateOpportunity:job=" + pawn.CurJob?.def?.defName + ",driver=" + pawn.jobs?.curDriver?.GetType().FullName : "Event:" + eventKey;
        configuration = SqueakerMod.Settings.CaptureBehaviorConfiguration();
        var view = new RuntimeView(catalog, configuration, SqueakerRuntime.Audio);
        decisionAudioSnapshot = SqueakerRuntime.AudioSnapshot;
        var frame = Capture(configuration, eventKey, activeCommand, registeredAction, ingestionResult, notificationFact, interaction);
        observedInteraction = interaction; observedEventFact = frame.EventFact; requestedAction = registeredAction ?? eventKey ?? "";
        observedRole = frame.SubjectRole; observedRace = frame.Domain.Target; observedChild = frame.Domain.Child;
        draws.Clear(); preparedSound = null; lastRequest = null;
        ObserveSlot();
        long previousSchedule = state.ScheduleRevision;
        LastOutcome = owner.Respond(view, frame, candidate, this, this);
        lastObservation = now;
        HasEvaluation = true;
        ObservationSequence++;
        ObserveSlot();
        decisionObservation = null;
        decisionInteractionUntil = null; decisionSchedule = null; decisionEndReason = "";
        try
        {
            decisionObservation = owner.Observe(); decisionEndReason = owner.LastEndReason;
            decisionInteractionUntil = LastOutcome.Action != "" && interaction.HasValue ? state.InteractionDeadline(LastOutcome.Action) : (double?)null;
            decisionSchedule = state.Schedule;
            if (state.ScheduleRevision != previousSchedule && state.Schedule?.Kind != StateScheduleKind.SharedRaise)
            {
                scheduleBasisConfiguration = configuration.Id;
                scheduleBasisSequence = ObservationSequence;
            }
        }
        catch (Exception error) when (SqueakLog.IsOrdinary(error)) { ReportDiagnosticFault(error); }
        try { pawn.GetComp<CompSqueaker>().RecordKernelOutcome(LastOutcome, preparedSound, lastRequest); }
        catch (Exception error) { ReportDiagnosticFault(error); }
    }
    private void ReportDiagnosticFault(Exception error)
    {
        if (!diagnosticFaultReported)
        {
            diagnosticFaultReported = true;
            SqueakLog.DiagnosticPublicationFailed(owner.Subject, error);
        }
    }

    public void Maintain(bool force = false)
    {
        // Preserve the original unavailable-subject exit: it does not require settings adoption.
        if (!pawn.Spawned || pawn.Destroyed || pawn.MapHeld != Find.CurrentMap)
        { EndSustained("SubjectOrServiceUnavailable"); return; }
        var configuration = SqueakerMod.Settings.CaptureBehaviorConfiguration();
        if (!configuration.SystemEnabled)
        { EndSustained("SubjectOrServiceUnavailable", configuration); return; }
        // Revision numbers are publisher-local; a replacement snapshot may reuse the same number.
        var audio = SqueakerRuntime.Audio;
        long audioSnapshot = SqueakerRuntime.AudioSnapshot;
        bool slotChanged = ObserveSlot();
        BeginMaintenance(configuration.Id, audioSnapshot, owner.ActiveAction == "" ? "NoOwnedSlot" : "HandleMaintainOnly");
        bool changed = !ReferenceEquals(configuration, reviewedConfiguration) || !ReferenceEquals(audio, reviewedAudio);
        bool reviewed = false;
        if (owner.ActiveAction != "" && (force || changed || owner.ReviewDue(Find.TickManager.TicksGame)))
        {
            maintenanceResult = owner.Review(new RuntimeView(catalog, configuration, audio),
                Capture(configuration, null, captureEvidence: false), candidate, force || changed);
            reviewedConfiguration = configuration;
            reviewedAudio = audio;
            reviewTick = Find.TickManager.TicksGame; reviewConfigurationId = configuration.Id; reviewAudioSnapshot = audioSnapshot;
            slotChanged |= ObserveSlot();
            reviewed = true;
            ReportEndFault();
        }
        owner.Maintain();
        slotChanged |= ObserveSlot();
        if (reviewed || slotChanged) SqueakDiagnosticsOverlay.RecordMaintenance(pawn, this);
    }
    public void Stop() => EndSustained("SessionReleased");
    private void EndSustained(string reason, BehaviorConfiguration? adopted = null)
    {
        bool changed = ObserveSlot();
        string previousResult = maintenanceResult;
        BeginMaintenance(adopted?.Id, null, reason);
        owner.Stop(reason); changed |= ObserveSlot(); ReportEndFault();
        if (changed || previousResult != maintenanceResult) SqueakDiagnosticsOverlay.RecordMaintenance(pawn, this);
    }
    private void BeginMaintenance(long? adoptedConfiguration, long? adoptedAudio, string result)
    {
        maintenanceTick = UnityData.IsInMainThread ? Find.TickManager?.TicksGame : null;
        maintenanceSeconds = (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        maintenanceConfiguration = adoptedConfiguration; maintenanceAudioSnapshot = adoptedAudio;
        maintenanceResult = result;
    }
    // Record an observed transition at the real mutation boundary, never during diagnostic reads.
    // Native handles may already be inactive; that time is detection, not an invented native end time.
    private bool ObserveSlot()
    {
        try
        {
            string active = owner.ActiveAction;
            bool ownerEnded = observedEndRevision != owner.EndRevision;
            observedEndRevision = owner.EndRevision;
            if (active == slotAction) return false;
            if (slotAction != "")
            {
                endedAction = slotAction; endObservedTick = UnityData.IsInMainThread ? Find.TickManager?.TicksGame : null;
                endObservedSeconds = (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
                endedReason = ownerEnded ? owner.LastEndReason : "NativeInactiveObserved";
            }
            slotAction = active;
            slotConfiguration = active == "" ? (long?)null : configuration?.Id;
            slotAudioSnapshot = active == "" ? (long?)null : decisionAudioSnapshot;
            return true;
        }
        catch (Exception error) when (SqueakLog.IsOrdinary(error)) { ReportDiagnosticFault(error); return false; }
    }
    private void ReportEndFault()
    {
        if (!endFaultReported && owner.LastEndReason.Contains(":EndFault:"))
        {
            endFaultReported = true;
            SqueakLog.SustainedEndUncertain(owner.Subject, owner.LastEndReason);
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
            if (request.Subject != owner.Subject || request.Domain != pawn.def.defName
                || request.Family != SubjectFamily.PawnRace || request.TargetNamespace != ""
                || request.Audio.Domain != request.Domain || request.Audio.SourceDomain.IsNonPawn
                || request.Audio.SourceDomain.Race.DefName != pawn.def.defName
                || request.Audio.SourceDomain.Xenotype.HasValue && request.Audio.SourceDomain.Xenotype.Value.DefName != request.ChildDomain
                || request.ChildDomain != (GameRuntime.Current.ResolveContext(pawn, configuration ?? SqueakerMod.Settings.CaptureBehaviorConfiguration()).Xenotype?.defName ?? ""))
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
            double globalVolume = SqueakerRuntime.CurrentSystemVolume;
            if (!OneShotOutput.TryPrepare(localVolume, localPitch, globalVolume, out float volume, out float pitch))
            {
                rejection = "InvalidFinalParameters";
                return false;
            }
            if (!UsInstanceOutput.CanDispatch(request.Sustained && sound.sustain, out rejection))
            {
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
            var result = owner.StartPrepared(request, sound!.sustain, () =>
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
            null);
        return receipt.Result == PlaybackResult.Rejected && receipt.Reason == "PreflightRejected"
            ? new PlaybackReceipt(PlaybackResult.Rejected, rejection)
            : receipt;
    }
}

public static class SqueakerRuntime
{
    private static readonly Dictionary<int, PawnResponseAdapter> sessions = new();
    internal static string SubjectKey(Pawn pawn) => "Pawn/" + pawn.thingIDNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
    internal static double CurrentSystemVolume => SqueakerMod.Settings.CaptureBehaviorConfiguration().SystemVolume;
    private static DefinitionCatalog catalog = new(720, 216, fullDevCandidate: true);
    private static AudioInputBuilder audioInputs = new(catalog);
    internal static Kernel.AudioCatalog Audio { get; private set; } = Kernel.AudioCatalog.Empty;
    // Publisher-local process identity for each replaced Audio reference; never a resource AudioId.
    internal static long AudioSnapshot { get; private set; }
    private static UsDevelopmentPolicy? selectedPolicy;
    private static UsResponsePolicy? ordinaryPolicy;
    internal static UsResponsePolicy? CurrentPolicy => selectedPolicy?.RuntimePolicy ?? ordinaryPolicy;
    private static System.WeakReference<Game>? sessionGame;
    private static Game? SessionGame => sessionGame != null && sessionGame.TryGetTarget(out var game) ? game : null;
    private static bool worldReady;
    internal static StatePhaseInput? PhaseInputFor(Pawn pawn)
        => worldReady && ReferenceEquals(Current.Game, SessionGame) && pawn.thingIDNumber > 0 && Find.World?.info != null
            ? new StatePhaseInput(Find.World.info.persistentRandomValue, pawn.thingIDNumber) : null;
    internal static void WorldReady(Game game)
    {
        ResetSession();
        sessionGame = new System.WeakReference<Game>(game);
        worldReady = true;
    }
    internal static bool DevelopmentActive => selectedPolicy != null;
    internal static bool AttackObservationActive => DevelopmentActive || ordinaryPolicy != null;
    public static string PolicyStatus => selectedPolicy != null ? "ExplicitDevelopmentCandidate"
        : ordinaryPolicy != null ? "OrdinaryActive" : "PolicySelectionRequired";
    public static IReadOnlyList<string> Issues => catalog.Issues;
    internal static bool DeclareNotification(NotificationContract contract)
    {
        if (sessions.Count != 0) throw new InvalidOperationException("NotificationDeclarationRequiresDefinitionPhase");
        var next = catalog.NextRevision();
        if (!next.DeclareNotification(contract)) return false;
        catalog = next.Publish();
        return true;
    }
    internal static bool TryProcessDeclaredNotification(Pawn pawn, string actionId)
    {
        if (!catalog.TryGetNotificationFact(actionId, out string fact)) return false;
        var session = SessionFor(pawn);
        if (session == null) return false;
        session.Tick(actionId, notificationFact: fact, evidence: "DeclaredNotification:" + actionId);
        return true;
    }

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
        if (ReferenceEquals(selectedPolicy, policy)) return;
        if (selectedPolicy != null && !ReferenceEquals(selectedPolicy, policy))
        {
            throw new InvalidOperationException("A development policy is already selected for this process");
        }
        selectedPolicy = policy;
        foreach (var session in sessions.Values) session.UsePolicy(policy.RuntimePolicy);
    }

    internal static void InstallOrdinaryPolicy(UsResponsePolicy policy)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        if (ReferenceEquals(ordinaryPolicy, policy)) return;
        if (sessions.Count != 0) throw new InvalidOperationException("OrdinaryPolicyInstallationRequiresNoActiveSubjects");
        ordinaryPolicy = policy;
    }

    private static PawnResponseAdapter? SessionFor(Pawn pawn)
    {
        var runtimePolicy = CurrentPolicy;
        if (!ReferenceEquals(Current.Game, SessionGame))
        {
            ResetSession();
            var game = Current.Game;
            sessionGame = game == null ? null : new System.WeakReference<Game>(game);
        }
        if (runtimePolicy == null)
        {
            return null;
        }
        if (sessions.TryGetValue(pawn.thingIDNumber, out var session))
        {
            return session;
        }
        if (pawn.GetComp<CompSqueaker>() == null || !pawn.Spawned || pawn.Destroyed)
        {
            return null;
        }
        if (session == null)
        {
            session = new PawnResponseAdapter(pawn, catalog, runtimePolicy);
            sessions.Add(pawn.thingIDNumber, session);
        }
        return session;
    }
    public static IEnumerable<string> DeclaredRaces => audioInputs.Entries.Where(x => x.Family == SubjectFamily.PawnRace).Select(x => x.Domain).Distinct(StringComparer.Ordinal);
    public static BehaviorSourceDirectory BehaviorSources => catalog.BehaviorSources;
    internal static DefinitionCatalog Definitions => catalog;
    internal static bool ImplementationAvailable(string key) => catalog.HasFact(key)
        && (key != UsSelectObservation.FactKey || UsSelectObservation.Ready)
        && (key != "us.core/AttackEvent" || UsAttackObservation.Ready)
        && (key != "us.core/WorkState" || UsWorkObservation.Ready)
        && UsPawnEventObservation.Available(key) && UsIngestionObservation.Available(key);
    internal static EffectiveBehavior Explain(BehaviorConfiguration configuration, string actionId, string race, string child)
    {
        var action = catalog.Actions.First(x => x.Key == actionId);
        var policy = CurrentPolicy;
        var defaults = policy == null ? BehaviorContract.DeclaredDefaults(action) : BehaviorContract.Defaults(action, policy);
        var effective = race == "" ? configuration.ResolveProfile("Pawn", "", actionId, defaults, catalog.BehaviorSources, ImplementationAvailable, action.Condition.RequiredFacts())
            : configuration.Resolve(new BehaviorDomain("Pawn", "", race, child), actionId, defaults, catalog.BehaviorSources, ImplementationAvailable, action.Condition.RequiredFacts());
        string invalid = BehaviorContract.InvalidParameters(action, effective.Read());
        return !effective.Paused && invalid != "" ? new EffectiveBehavior(effective.Read(), true, invalid,
            effective.Origins.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal), effective.Dependencies) : effective;
    }
    internal static string DiagnosticText(Pawn pawn) => sessions.TryGetValue(pawn.thingIDNumber, out var session)
        ? session.DiagnosticText : PolicyStatus;
    internal static SessionObservation? Observe(Pawn pawn) => sessions.TryGetValue(pawn.thingIDNumber, out var session) ? session.Observation : null;
    internal static SqueakResponseEvidence DiagnosticEvidence(Pawn pawn)
    {
        if (sessions.TryGetValue(pawn.thingIDNumber, out var session)) return session.CaptureDiagnosticEvidence();
        var evidence = new SqueakResponseEvidence("NoOwner");
        evidence.Add("subject", SubjectKey(pawn)); evidence.Add("policy", PolicyStatus);
        return evidence;
    }

    public static void Initialize()
    {
        ResetSession();
        ordinaryPolicy = null;
        catalog = new DefinitionCatalog(720, 216, fullDevCandidate: true);
        audioInputs = new AudioInputBuilder(catalog);
        foreach (var notification in SqueakerNotifications.Declarations) catalog.DeclareNotification(notification);
        foreach (var notification in DefDatabase<UsNotificationDef>.AllDefsListForReading)
        {
            try { catalog.DeclareNotification(new NotificationContract(notification.actionId, notification.intervalTicks, notification.priority)); }
            catch (Exception error) { catalog.AddIssue(notification.defName + ":" + error.Message, notification.defName, "InvalidNotification", detail: error.Message); }
        }
        foreach (var provider in DefDatabase<UsProviderDef>.AllDefsListForReading)
        {
            try
            {
                catalog.AddProvider((IUsDefinitionProvider)Activator.CreateInstance(provider.providerClass)!);
            }
            catch (Exception error)
            {
                catalog.AddIssue(provider.defName + ":" + error.Message, provider.defName, "InvalidProvider", detail: error.Message);
            }
        }

        catalog.AddActions(DefDatabase<UsActionDef>.AllDefsListForReading.Select(x => x.declaration));

        foreach (var source in DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading)
        {
            foreach (var target in source.declaration.Sources)
            {
                var declaration = target.Copy();
                declaration.Id = (source.modContentPack?.PackageId ?? "") + "/" + source.defName;
                catalog.AddBehaviorSource(declaration, declaration.Id);
            }
        }

        foreach (var pack in DefDatabase<UsVoicePackDef>.AllDefsListForReading)
        {
            try
            {
                var domain = pack.declaration.Domain;
                if (domain.Family != "Pawn")
                {
                    catalog.AddIssue(pack.defName + ":NonPawnHostUnavailable", pack.defName, "NonPawnHostUnavailable");
                    continue;
                }
                var target = DefDatabase<ThingDef>.GetNamedSilentFail(domain.Target);
                if (target?.race == null || target.category != ThingCategory.Pawn || target.thingClass == null
                    || !typeof(Pawn).IsAssignableFrom(target.thingClass)
                    || domain.Child != "" && (!ModsConfig.BiotechActive || DefDatabase<XenotypeDef>.GetNamedSilentFail(domain.Child) == null))
                {
                    catalog.AddIssue(pack.defName + ":TargetDomainUnavailable", pack.defName, "TargetDomainUnavailable");
                    continue;
                }
                var identity = new VoicePackInfo { defName = pack.defName, modContentPack = pack.modContentPack };
                if (identity.TryGetPackKey(out string selectionKey))
                {
                    audioInputs.AddPack(pack.modContentPack?.PackageId ?? "", pack.defName, pack.declaration, Bind, selectionKey, pack.defName);
                }
                else
                {
                    catalog.AddIssue(pack.defName + ":SelectionIdentityUnavailable", pack.defName, "SelectionIdentityUnavailable");
                }
            }
            catch (Exception error)
            {
                catalog.AddIssue(pack.defName + ":InvalidTarget:" + error.Message, pack.defName, "InvalidTarget", detail: error.Message);
            }
        }

        VoicePackDirectory.Refresh(audioInputs.Entries);
        RefreshAudio();
        catalog.Issues.AddRange(audioInputs.Issues);
        catalog.IssueRecords.AddRange(audioInputs.IssueRecords);
        catalog.Publish();
        foreach (var issue in catalog.IssueRecords)
        {
            SqueakLog.AuthoringRejectedKnown(issue.Detail, issue.Target, issue.Reason, issue.Source, issue.Action, issue.Claim);
        }
        // Public callers may still append unstructured issue text; never infer trusted fields from it.
        var described = new HashSet<string>(catalog.IssueRecords.Select(x => x.Message), StringComparer.Ordinal);
        foreach (string issue in catalog.Issues.Where(x => !described.Contains(x)))
        {
            SqueakLog.AuthoringRejected(issue);
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

    internal static SqueakDiagnosticLogResult LogResponse(Pawn pawn, bool exhausted, long suppressed = 0, double until = 0)
    {
        if (sessions.TryGetValue(pawn.thingIDNumber, out var session))
            return SqueakLog.TryKernelResponse(() => {
                var data = session.CaptureResponseLog();
                data.RateSuppressed = suppressed; data.RateWindowUntil = until;
                return data;
            }, exhausted);
        return SqueakDiagnosticLogResult.NotCaptured;
    }

    internal static void PublishFallbackAudio(Kernel.BuiltInFallbackTable committed)
    {
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("FallbackAudioOwnerThreadRequired");
        // Construct fully before replacing Audio. Existing sessions capture the new reference at their
        // next maintenance/selection; no session, opportunity, deadline, W or native comp is reset.
        var candidate = new Kernel.AudioCatalog(audioInputs.Entries, SqueakKernelAdapter.BuildBuiltIn(committed));
        Audio = candidate;
        AudioSnapshot++;
    }

    internal static void RefreshAudio()
    {
        Audio = new Kernel.AudioCatalog(audioInputs.Entries, SqueakKernelAdapter.BuildBuiltIn());
        AudioSnapshot++;
    }

    internal static void ReleasePawn(Pawn pawn)
    {
        if (sessions.TryGetValue(pawn.thingIDNumber, out var session))
        {
            session.Stop();
        }
        sessions.Remove(pawn.thingIDNumber);
    }
    internal static void ReleaseMap(Map map)
    {
        foreach (var pair in sessions.Where(x => x.Value.OnMap(map)).ToArray())
        {
            pair.Value.Stop();
            sessions.Remove(pair.Key);
        }
    }
    public static bool IsControlled(Pawn pawn) => sessions.ContainsKey(pawn.thingIDNumber);
    internal static void IngestionChanged(Pawn pawn)
    {
        if (sessions.TryGetValue(pawn.thingIDNumber, out var session)) session.Maintain(true);
    }
    internal static void IngestionResult(UsIngestionObservation.Result result)
    {
        // No consumer means no decision, owner allocation, random draw or settlement.
        if (!catalog.Actions.Any(BehaviorContract.ConsumesIngestionResult)) return;
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
            sessions.Remove(pawn.thingIDNumber);
            return true;
        }

        session.Maintain();
        if (SqueakerMod.Settings.CaptureBehaviorConfiguration().SystemEnabled && pawn.MapHeld == Find.CurrentMap)
        {
            session.Tick();
        }

        return true;
    }

    internal static bool TryObservedEvent(Pawn pawn, string key, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence,
        InteractionOpportunity? interaction = null)
    {
        if (!Kernel.BuiltInActionKeys.Contains(key) || !BuiltInPawnContracts.IsEvent(key) || !catalog.HasAction(key)) return false;
        var session = SessionFor(pawn);
        if (session == null)
        {
            return true;
        }

        var invocation = new SqueakTriggerInvocation(origin, source);
        if (SqueakerMod.Settings.CaptureBehaviorConfiguration().SystemEnabled && pawn.GetComp<CompSqueaker>().ControlledInvocationAllowed(invocation))
        {
            // Explicit fact consumers, including independent roots/children, share this owner.
            session.Tick(key, invocation.IsActiveCommand, evidence: evidence, interaction: interaction);
        }

        // Only declared contracts reach the owner. Unknown strings never synthesize a default plan.
        return true;
    }

    public static void ResetSession()
    {
        foreach (var session in sessions.Values)
        {
            session.Stop();
        }
        sessions.Clear();
        sessionGame = null;
        worldReady = false;
        UsIngestionObservation.Reset();
        SqueakLog.ResetSession();
    }
}

public sealed class ResponseSessionLifecycle : GameComponent
{
    private readonly Game game;
    public ResponseSessionLifecycle(Game game)
    {
        SqueakerRuntime.ResetSession();
        this.game = game;
        SqueakDiagnosticsOverlay.NotifyGameChanged();
    }

    public override void StartedNewGame() { SqueakerRuntime.WorldReady(game); SqueakDiagnosticsOverlay.NotifyGameChanged(); }
    public override void LoadedGame() { SqueakerRuntime.WorldReady(game); SqueakDiagnosticsOverlay.NotifyGameChanged(); }
}
