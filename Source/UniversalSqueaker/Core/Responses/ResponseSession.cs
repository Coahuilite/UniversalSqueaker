using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker.Responses;

/// <summary>
/// Serial owner for one stable subject. Current domain and captured configuration are inputs, not
/// session identity. The platform supplies synchronous facts/output; no native object is retained.
/// </summary>
public sealed class ResponseSession
{
    public string Subject { get; }
    public DecisionState State { get; }
    public DecisionOutcome LastOutcome { get; private set; }
    private StatePolicy policy;
    private readonly ResponseDecision decision = new();
    private readonly SustainedPlayback sustained = new();
    private readonly Dictionary<string, EffectiveBehavior> effectiveActions = new(StringComparer.Ordinal);
    private readonly System.Collections.ObjectModel.ReadOnlyDictionary<string, EffectiveBehavior> effectiveView;
    public IReadOnlyDictionary<string, EffectiveBehavior> EffectiveActions => effectiveView;
    private long observedTick;
    private bool checksShared;
    public SessionObservation Observe() => new(LastOutcome.Action ?? "", LastOutcome.Reason ?? "", LastOutcome.Receipt,
        observedTick, State.SharedDeadline, State.ActionDeadline(LastOutcome.Action ?? ""), State.NextStateTick,
        State.ConfirmedTick, checksShared, sustained.ActiveKey);

    public ResponseSession(string subject, long firstOpportunity, StatePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Stable subject identity required", nameof(subject));
        Subject = subject;
        effectiveView = new System.Collections.ObjectModel.ReadOnlyDictionary<string, EffectiveBehavior>(effectiveActions);
        State = new DecisionState(firstOpportunity);
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    // Future settlements use the new input; all already-reserved state and owned audio stay here.
    internal void UsePolicy(StatePolicy policy) => this.policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public string ActiveAction => sustained.ActiveKey;
    public string LastEndReason => sustained.LastEndReason;
    public bool ReviewDue(long tick) => sustained.ReviewDue(tick);

    public DecisionOutcome Respond(DecisionInput input, IResponseRandom random, IResponseOutput output)
    {
        if (input.Subject != Subject) throw new ArgumentException("SubjectOwnerMismatch", nameof(input));
        input.ActiveSustainedKey = sustained.ActiveKey;
        // Decision reserves AttemptTick before output. Re-entry observes this same State immediately.
        LastOutcome = decision.Run(State, policy, input, random, output);
        observedTick = input.Tick;
        checksShared = input.Actions.FirstOrDefault(x => x.Key == LastOutcome.Action)?.CheckShared ?? false;
        return LastOutcome;
    }

    public DecisionOutcome Respond(RuntimeView view, ResponseFrame frame, UsDevelopmentPolicy selectedPolicy,
        IResponseRandom random, IResponseOutput output)
        => Respond(view, frame, selectedPolicy.RuntimePolicy, random, output);

    public DecisionOutcome Respond(RuntimeView view, ResponseFrame frame, UsResponsePolicy selectedPolicy,
        IResponseRandom random, IResponseOutput output)
    {
        var plans = new List<ActionPlan>();
        var modulation = new Dictionary<string, ModulationPlan>(StringComparer.Ordinal);
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        effectiveActions.Clear();
        foreach (var action in view.Definitions.Actions)
        {
            if (frame.IngestionResultOpportunity && !BehaviorContract.ConsumesIngestionResult(action)
                || frame.ActionFilter != null && action.Key != frame.ActionFilter) continue;
            var effective = view.Configuration.Resolve(frame.Domain, action.Key, BehaviorContract.Defaults(action, selectedPolicy),
                view.Definitions.BehaviorSources, frame.AvailableFacts.Contains, action.Condition.RequiredFacts());
            effectiveActions.Add(action.Key, effective);
            var behavior = effective.Read();
            string invalid = BehaviorContract.InvalidParameters(action, behavior);
            if (!BehaviorContract.Authorized(view.Configuration, effective, behavior, frame) || invalid != "")
            {
                blocked.Add(action.Key);
                if (effective.Paused) reasons[action.Key] = effective.Failure;
                else if (invalid != "") reasons[action.Key] = invalid;
            }
            if (action.Key == CoreActions.Equip && (selectedPolicy.EquipQualification == UsEquipQualification.Unselected
                || frame.Facts.Read("us.core/PlayerEquipCommand") != Truth.True))
            {
                blocked.Add(action.Key);
                reasons[action.Key] = selectedPolicy.EquipQualification == UsEquipQualification.Unselected
                    ? "EquipQualificationRequired" : "ExplicitPlayerCoreEquipQualification";
            }
            plans.Add(new ActionPlan(action.Key, action.Priority, BehaviorContract.EffectiveCondition(action, behavior),
                behavior.HardIntervalTicks ?? action.Interval, selectedPolicy.ChecksShared(action), action.WriteShared,
                frame.StateOpportunity ? action.Chance : action.Chance * selectedPolicy.EventProbability, action.BaseAction,
                behavior.TalkingEnabled.HasValue ? !behavior.TalkingEnabled.Value : action.SkipTalking,
                behavior.Volume ?? 1, behavior.Pitch ?? 1, action.Sustained, action.RequiresVocal, action.SubjectRole, action.Scope));
            modulation.Add(action.Key, ResponseModulation.Resolve(frame.Subject, behavior, frame.NumericFacts, view.Definitions.Responses, frame.Facts.Ingestion));
        }
        var domain = frame.Domain.Identity;
        return Respond(new DecisionInput
        {
            Subject = frame.Subject, Domain = frame.Domain.Target, ChildDomain = frame.Domain.Child,
            Family = frame.Domain.Family == "Pawn" ? SubjectFamily.PawnRace : SubjectFamily.NonPawn,
            TargetNamespace = frame.Domain.Namespace, SubjectRole = frame.SubjectRole, Tick = frame.Tick,
            StateOpportunity = frame.StateOpportunity, Eligible = frame.Eligible,
            HasVocalOrgans = frame.HasVocalOrgans, TalkingChance = frame.TalkingChance,
            Activity = view.Configuration.Activity, StateWaitFactor = selectedPolicy.StateWaitFactor * frame.StateWaitFactor,
            Facts = frame.Facts, Actions = plans.ToArray(), ActionModulation = modulation,
            BlockedActions = blocked, BlockedActionReasons = reasons, AudioCatalog = view.Audio,
            AudioContext = new Kernel.SelectionContext(domain, "", frame.Age, true, view.Configuration.AllowEggs),
            SoundGate = frame.SoundGate, SourceMode = view.Configuration.SourceMode, SelectedPacks = view.Configuration.IsPackSelected
        }, random, output);
    }

    public string Review(RuntimeView view, ResponseFrame frame, UsDevelopmentPolicy selectedPolicy, bool force)
        => Review(view, frame, selectedPolicy.RuntimePolicy, force);

    public string Review(RuntimeView view, ResponseFrame frame, UsResponsePolicy selectedPolicy, bool force)
    {
        var action = view.Definitions.Actions.FirstOrDefault(x => x.Key == ActiveAction);
        if (action == null) { Stop("CurrentContractUnavailable"); return LastEndReason; }
        var effective = view.Configuration.Resolve(frame.Domain, action.Key, BehaviorContract.Defaults(action, selectedPolicy),
            view.Definitions.BehaviorSources, frame.AvailableFacts.Contains, action.Condition.RequiredFacts());
        var behavior = effective.Read();
        return Review(frame.Tick, frame.Facts, BehaviorContract.EffectiveCondition(action, behavior),
            frame.Eligible && BehaviorContract.Authorized(view.Configuration, effective, behavior, frame)
            && BehaviorContract.InvalidParameters(action, behavior) == "" && (!action.RequiresVocal || frame.HasVocalOrgans), force);
    }

    public PlaybackReceipt StartPrepared(PlaybackRequest request, bool sustainedMaterial, Func<SustainedStart> start)
    {
        if (request.Subject != Subject) return new PlaybackReceipt(PlaybackResult.Rejected, "SubjectOwnerMismatch");
        return sustained.Dispatch(request, sustainedMaterial, start);
    }

    public string Review(long tick, FactSnapshot facts, Condition? currentCondition, bool authorized, bool force = false)
    {
        if (!authorized)
        {
            sustained.Stop("CurrentAuthorizationUnavailable");
            return sustained.LastEndReason;
        }
        return sustained.Review(tick, facts, currentCondition, force);
    }

    public void Maintain() => sustained.Maintain();
    public bool Stop(string reason = "SessionReleased") => sustained.Stop(reason);
}
