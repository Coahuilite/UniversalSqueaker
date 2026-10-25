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
    private readonly UsResponsePolicy candidate;
    private readonly List<KeyValuePair<RandomPurpose, double>> draws = new();
    private SoundDef? preparedSound;
    private PlaybackRequest? lastRequest;
    private long lastObservation;
    private bool diagnosticFaultReported;
    private bool endFaultReported;
    private long reviewedConfiguration = -1;
    private BehaviorConfiguration? configuration;
    private IReadOnlyDictionary<string, EffectiveBehavior> effectiveActions => owner.EffectiveActions;
    private string resultEvidence = "";
    private string ingestionEvidence = "";
    private string observationEvidence = "";
    internal string DiagnosticText => "tick=" + lastObservation + " action=" + LastOutcome.Action
        + " reason=" + LastOutcome.Reason + " receipt=" + LastOutcome.Receipt?.Result
        + " confirmed=" + state.ConfirmedTick + " sharedUntil=" + state.SharedDeadline
        + " actionUntil=" + state.ActionDeadline(LastOutcome.Action) + " nextState=" + state.NextStateTick
        + " active=" + owner.ActiveAction + " ended=" + owner.LastEndReason
        + " subject=" + owner.Subject + " nativeLabel=" + pawn.ThingID + " role=" + lastRequest?.SubjectRole
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
    internal SessionObservation Observation => owner.Observe();

    private bool HasAvailableFact(string key) => SqueakerRuntime.ImplementationAvailable(key);

    internal PawnResponseAdapter(Pawn pawn, DefinitionCatalog catalog, UsResponsePolicy candidate)
    {
        this.pawn = pawn;
        this.catalog = catalog;
        this.candidate = candidate;
        long now = Find.TickManager.TicksGame;
        long offset = candidate.InitialSpread == 0 ? 0 : (long)((uint)pawn.thingIDNumber % (ulong)candidate.InitialSpread);
        owner = new ResponseSession(SqueakerRuntime.SubjectKey(pawn), now > long.MaxValue - offset ? long.MaxValue : now + offset, candidate.State);
    }

    private ResponseFrame Capture(BehaviorConfiguration accepted, string? eventKey, bool activeCommand = false, string? registeredAction = null,
        UsIngestionObservation.Result? result = null, string? notificationFact = null)
    {
        var snapshot = GameRuntime.Current;
        var context = snapshot.ResolveContext(pawn, accepted);
        var values = UsPawnFacts.Capture(pawn, eventKey, out var diagnostics);
        if (notificationFact != null) values[notificationFact] = Truth.True;
        var ingestion = result == null ? null : new IngestionResultFact(SqueakerRuntime.SubjectKey(result.Ingester), result.ItemId, result.ItemDef, result.Nutrition);
        values[EatPolicy.Result] = UsIngestionObservation.Ingested == null ? Truth.Unknown : ingestion == null ? Truth.False : Truth.True;
        resultEvidence = result == null ? "" : "ingester=" + result.Ingester.ThingID + ",item=" + result.ItemId
            + ",def=" + result.ItemDef + ",nutrition=" + result.Nutrition;
        ingestionEvidence = UsIngestionObservation.Describe(pawn);
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
            IngestionResultOpportunity = result != null,
            SubjectRole = eventKey == CoreActions.Select ? "SelectedSubject" : eventKey == CoreActions.Attack ? "Attacker"
                : eventKey == CoreActions.Wounded ? "DamageRecipient" : result != null ? "ActualIngester" : "Subject",
            ActiveCommand = activeCommand || eventKey == null && values["us.core/ActiveCommand"] == Truth.True,
            Eligible = pawn.Spawned && !pawn.Destroyed && pawn.MapHeld == Find.CurrentMap
                && Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position)
                && accepted.SystemEnabled,
            HasVocalOrgans = vocal.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold,
            TalkingChance = accepted.TalkingScaling && vocal.TalkingChance < SqueakVocalCapability.TalkingCertainThreshold ? vocal.TalkingChance : 1,
            StateWaitFactor = candidate.UsesNativeRhythm && eventKey == null ? CompSqueaker.StateRhythmFactor() : 1,
            Facts = catalog.Capture(owner.Subject, values, diagnostics, ingestion), NumericFacts = numbers,
            AvailableFacts = new HashSet<string>(catalog.FactKeys.Where(HasAvailableFact), StringComparer.Ordinal),
            Age = SqueakLifeStageResolver.Resolve(pawn),
            SoundGate = SqueakKernelAdapter.GateFor(pawn, pawn.MapHeld, null),

        };
    }

    internal void Tick(string? eventKey = null, bool activeCommand = false, string? registeredAction = null,
        UsIngestionObservation.Result? ingestionResult = null, string evidence = "", string? notificationFact = null)
    {
        long now = Find.TickManager.TicksGame;
        if (eventKey == null && now < owner.State.NextStateTick) return;
        observationEvidence = evidence != "" ? evidence : eventKey == null
            ? "StateOpportunity:job=" + pawn.CurJob?.def?.defName + ",driver=" + pawn.jobs?.curDriver?.GetType().FullName : "Event:" + eventKey;
        configuration = SqueakerMod.Settings.CaptureBehaviorConfiguration();
        var view = new RuntimeView(catalog, configuration, SqueakerRuntime.Audio);
        var frame = Capture(configuration, eventKey, activeCommand, registeredAction, ingestionResult, notificationFact);
        draws.Clear(); preparedSound = null; lastRequest = null;
        LastOutcome = owner.Respond(view, frame, candidate, this, this);
        lastObservation = now;
        try { pawn.GetComp<CompSqueaker>().RecordKernelOutcome(LastOutcome, preparedSound, lastRequest); }
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
            || !SqueakerMod.Settings.CaptureBehaviorConfiguration().SystemEnabled)
        { EndSustained("SubjectOrServiceUnavailable"); return; }
        var configuration = SqueakerMod.Settings.CaptureBehaviorConfiguration();
        bool changed = configuration.Id != reviewedConfiguration;
        if (owner.ActiveAction != "" && (force || changed || owner.ReviewDue(Find.TickManager.TicksGame)))
        {
            owner.Review(new RuntimeView(catalog, configuration, SqueakerRuntime.Audio), Capture(configuration, null), candidate, force || changed);
            reviewedConfiguration = configuration.Id;
            ReportEndFault();
        }
        owner.Maintain();
    }
    public void Stop() => EndSustained("SessionReleased");
    private void EndSustained(string reason) { owner.Stop(reason); ReportEndFault(); }
    private void ReportEndFault()
    {
        if (!endFaultReported && owner.LastEndReason.Contains(":EndFault:"))
        {
            endFaultReported = true;
            Log.Warning("usdiag owned sustained end uncertain; slot detached, no repeated End: " + owner.LastEndReason);
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
            () => Log.Message("usdiag OneShot " + request.Action + " " + request.Audio.AudioId + " NativeCallReturned"));
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
    private static UsDevelopmentPolicy? selectedPolicy;
    private static UsResponsePolicy? ordinaryPolicy;
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
        if (Kernel.BuiltInActionKeys.Contains(actionId) || !catalog.HasAction(actionId)) return false;
        var session = SessionFor(pawn);
        if (session == null) return false;
        string fact = actionId + "/Notification";
        session.Tick(actionId, registeredAction: actionId, notificationFact: catalog.HasFact(fact) ? fact : null,
            evidence: "DeclaredNotification:" + actionId);
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
        if (sessions.Count != 0) throw new InvalidOperationException("DevelopmentSelectionRequiresNoActiveSubjects");
        selectedPolicy = policy;
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
        var runtimePolicy = selectedPolicy?.RuntimePolicy ?? ordinaryPolicy;
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
        var policy = selectedPolicy?.RuntimePolicy ?? ordinaryPolicy;
        var defaults = policy == null ? BehaviorContract.DeclaredDefaults(action) : BehaviorContract.Defaults(action, policy);
        return race == "" ? configuration.ResolveProfile("Pawn", "", actionId, defaults, catalog.BehaviorSources, ImplementationAvailable, action.Condition.RequiredFacts())
            : configuration.Resolve(new BehaviorDomain("Pawn", "", race, child), actionId, defaults, catalog.BehaviorSources, ImplementationAvailable, action.Condition.RequiredFacts());
    }
    internal static string DiagnosticText(Pawn pawn) => sessions.TryGetValue(pawn.thingIDNumber, out var session)
        ? session.DiagnosticText : PolicyStatus;
    internal static SessionObservation? Observe(Pawn pawn) => sessions.TryGetValue(pawn.thingIDNumber, out var session) ? session.Observation : null;

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
            catch (Exception error) { catalog.Issues.Add(notification.defName + ":" + error.Message); }
        }
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
                var identity = new VoicePackInfo { defName = pack.defName, modContentPack = pack.modContentPack };
                if (identity.TryGetPackKey(out string selectionKey))
                {
                    audioInputs.AddPack(pack.modContentPack?.PackageId ?? "", pack.defName, pack.declaration, Bind, selectionKey);
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

        VoicePackDirectory.Refresh(audioInputs.Entries);
        RefreshAudio();
        catalog.Issues.AddRange(audioInputs.Issues);
        catalog.Publish();
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

    internal static void RefreshAudio() => Audio = new Kernel.AudioCatalog(audioInputs.Entries, SqueakKernelAdapter.BuildBuiltIn());

    internal static void ReleasePawn(Pawn pawn)
    {
        if (sessions.TryGetValue(pawn.thingIDNumber, out var session))
        {
            session.Stop();
        }
        sessions.Remove(pawn.thingIDNumber);
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

    internal static bool TryObservedEvent(Pawn pawn, string key, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence)
    {
        if (!catalog.Actions.Any(x => x.Key == key)) return false;
        var session = SessionFor(pawn);
        if (session == null)
        {
            return true;
        }

        var invocation = new SqueakTriggerInvocation(origin, source);
        if (SqueakerMod.Settings.CaptureBehaviorConfiguration().SystemEnabled && pawn.GetComp<CompSqueaker>().ControlledInvocationAllowed(invocation))
        {
            session.Tick(key, invocation.IsActiveCommand,
                // A native notification publishes facts, not a forced wrapper identity.
                // Independent roots and narrowed children using that fact enter the same arbitration.
                // An explicit public call naming an author action still targets that definition.
                !Kernel.BuiltInActionKeys.Contains(key) ? key : null, evidence: evidence);
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
        UsIngestionObservation.Reset();
    }
}

public sealed class ResponseSessionLifecycle : GameComponent
{
    public ResponseSessionLifecycle(Game game)
    {
        SqueakerRuntime.ResetSession();
    }

    public override void StartedNewGame() => SqueakerRuntime.ResetSession();
    public override void LoadedGame() => SqueakerRuntime.ResetSession();
}
