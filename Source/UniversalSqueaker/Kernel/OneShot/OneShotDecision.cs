using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker.OneShot;

internal static class TickDeadline
{
    internal static long Add(long tick, long nonnegativeInterval)
    {
        return tick > long.MaxValue - nonnegativeInterval ? long.MaxValue : tick + nonnegativeInterval;
    }
}

public enum SubjectFamily
{
    PawnRace,
    NonPawn
}

public enum Truth
{
    False,
    True,
    Unknown
}

public enum PlaybackResult
{
    Dispatched,
    AlreadyActive,
    Rejected,
    Indeterminate
}

public enum RandomPurpose
{
    Talking,
    Eligibility,
    Sound,
    StateWait,
    ModulationJitter
}

public interface IOneShotRandom
{
    double Next(RandomPurpose purpose);
}

public interface IOneShotBackend
{
    PlaybackReceipt Dispatch(PlaybackRequest request);
}

public readonly struct PlaybackReceipt
{
    public readonly PlaybackResult Result;
    public readonly string Reason;
    public PlaybackReceipt(PlaybackResult result, string reason = "")
    {
        Result = result;
        Reason = reason;
    }
}

/// <summary>A captured failure identity; logging never samples a provider again.</summary>
public readonly struct FactDiagnostic
{
    public readonly string Key;
    public readonly string Provider;
    public readonly string Reason;
    public FactDiagnostic(string key, string provider, string reason)
    {
        Key = key;
        Provider = provider;
        Reason = reason;
    }
}

public sealed class FactSnapshot
{
    public readonly IngestionResultFact? Ingestion;
    private readonly Dictionary<string, Truth> facts;
    private readonly Dictionary<string, FactDiagnostic> diagnostics;
    public FactSnapshot(IEnumerable<KeyValuePair<string, Truth>> values, IEnumerable<FactDiagnostic>? diagnostics = null, IngestionResultFact? ingestion = null)
    {
        Ingestion = ingestion;
        facts = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        this.diagnostics = (diagnostics ?? Array.Empty<FactDiagnostic>()).ToDictionary(x => x.Key, StringComparer.Ordinal);
    }

    public Truth Read(string key) => facts.TryGetValue(key, out var value) ? value : Truth.Unknown;
    public FactDiagnostic ExplainUnknown(string key)
    {
        if (diagnostics.TryGetValue(key, out var diagnostic))
        {
            return diagnostic;
        }

        return new FactDiagnostic(key, "", facts.ContainsKey(key) ? "ReportedUnknown" : "MissingFact");
    }
}

public sealed class Condition
{
    public readonly string Operation;
    public readonly string Fact;
    public IReadOnlyList<Condition> Children { get; }
    public Condition(string operation, string fact = "", params Condition[] children)
    {
        Operation = operation;
        Fact = fact;
        if (operation != "ref" && operation != "all" && operation != "any" && operation != "not")
            throw new ArgumentException("Unknown condition operation");
        if (children == null || children.Any(x => x == null)
            || operation == "not" && children.Length != 1
            || operation == "ref" && (string.IsNullOrWhiteSpace(fact) || children.Length != 0))
            throw new ArgumentException("Invalid condition shape");
        Children = Array.AsReadOnly((Condition[])children.Clone());
    }

    public Truth Evaluate(FactSnapshot facts)
    {
        if (Operation == "ref")
        {
            return facts.Read(Fact);
        }

        if (Operation == "not")
        {
            return Negate(Children[0].Evaluate(facts));
        }

        bool unknown = false;
        foreach (var child in Children)
        {
            var result = child.Evaluate(facts);
            if (Operation == "all" && result == Truth.False)
            {
                return Truth.False;
            }

            if (Operation == "any" && result == Truth.True)
            {
                return Truth.True;
            }

            unknown |= result == Truth.Unknown;
        }

        if (unknown)
        {
            return Truth.Unknown;
        }

        return Operation == "all" ? Truth.True : Truth.False;
    }

    public IEnumerable<string> RequiredFacts()
    {
        if (Operation == "ref")
        {
            yield return Fact;
            yield break;
        }
        foreach (var child in Children)
        {
            foreach (string key in child.RequiredFacts())
            {
                yield return key;
            }
        }
    }
    private static Truth Negate(Truth value)
    {
        if (value == Truth.Unknown)
        {
            return Truth.Unknown;
        }

        return value == Truth.True ? Truth.False : Truth.True;
    }
}

public sealed class ActionPlan
{
    public readonly string Key;
    public readonly int Priority;
    public readonly string BaseAction;
    public readonly Condition Condition;
    public readonly long Interval;
    public readonly bool CheckShared;
    public readonly bool WriteShared;
    public readonly double Chance;
    public readonly bool SkipTalking;
    public readonly bool RequiresVocal;
    public readonly double Volume;
    public readonly double Pitch;
    public readonly bool Sustained;
    public ActionPlan(string key, int priority, Condition condition, long interval, bool checkShared = true, bool writeShared = true, double chance = 1, string? baseAction = null,
        bool skipTalking = true, double volume = 1, double pitch = 1, bool sustained = false, bool requiresVocal = false)
    {
        if (interval < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (double.IsNaN(chance) || chance < 0 || chance > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chance));
        }
        if (double.IsNaN(volume) || double.IsInfinity(volume) || volume < 0 || volume > float.MaxValue
            || double.IsNaN(pitch) || double.IsInfinity(pitch) || pitch <= 0 || pitch > float.MaxValue)
            throw new ArgumentException("Invalid local parameters");

        Key = key;
        BaseAction = baseAction ?? key;
        Priority = priority;
        Condition = condition;
        Interval = interval;
        CheckShared = checkShared;
        WriteShared = writeShared;
        Chance = chance;
        SkipTalking = skipTalking;
        RequiresVocal = requiresVocal;
        Volume = volume;
        Pitch = pitch;
        Sustained = sustained;
    }
}

public sealed class StatePolicy
{
    public readonly long SharedInterval;
    public readonly long Wait;
    public const long Retry = 60;
    public StatePolicy(long sharedInterval, long wait)
    {
        if (sharedInterval < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sharedInterval));
        }

        if (wait < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wait));
        }

        SharedInterval = sharedInterval;
        Wait = wait;
    }
}

public sealed class DecisionState
{
    private readonly Dictionary<string, long> deadlines = new(StringComparer.Ordinal);
    public long SharedDeadline { get; internal set; }
    public long NextStateTick { get; internal set; }
    public long AttemptTick { get; internal set; } = long.MinValue;
    public long? ConfirmedTick { get; internal set; }
    public string ConfirmedAction { get; internal set; } = "";

    public long ActionDeadline(string key) => deadlines.TryGetValue(key, out var value) ? value : long.MinValue;
    internal void Protect(ActionPlan action, DecisionInput input, StatePolicy policy)
    {
        long now = input.Tick;
        deadlines[action.Key] = TickDeadline.Add(now, action.Interval);
        if (action.WriteShared)
        {
            SharedDeadline = Math.Max(SharedDeadline, TickDeadline.Add(now, policy.SharedInterval));
        }
    }

    public DecisionState(long firstOpportunity)
    {
        NextStateTick = firstOpportunity;
    }
}

public sealed class AudioCandidate
{
    public readonly string EntryKey;
    public readonly string PackKey;
    public readonly double PackWeight;
    public readonly string AudioId;
    public Kernel.AudioId? Resource => Kernel.AudioId.TryParse(AudioId, out var id) ? id : null;
    public readonly string Action;
    public readonly string Domain;
    public readonly SubjectFamily Family;
    public readonly string TargetNamespace;
    public readonly string ChildDomain;
    public readonly Kernel.AudioDomain SourceDomain;
    public readonly Kernel.ChainTier? SourceTier;
    public readonly double Weight;
    public readonly double Volume;
    public readonly double Pitch;
    public readonly IReadOnlyDictionary<string, bool> Participation;
    public AudioCandidate(string entryKey, string audioId, string action, string domain, double weight, double volume = 1, SubjectFamily family = SubjectFamily.PawnRace, string targetNamespace = "", double pitch = 1,
        IReadOnlyDictionary<string, bool>? participation = null, string packKey = "", double packWeight = 1, string childDomain = "",
        Kernel.AudioDomain? sourceDomain = null, Kernel.ChainTier? sourceTier = null)
    {
        if (string.IsNullOrEmpty(audioId)
            || double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0 || weight > float.MaxValue
            || double.IsNaN(volume) || double.IsInfinity(volume) || volume < 0 || volume > float.MaxValue
            || double.IsNaN(packWeight) || double.IsInfinity(packWeight) || packWeight <= 0 || packWeight > float.MaxValue)
        {
            throw new ArgumentException("Invalid audio candidate");
        }

        EntryKey = entryKey;
        PackKey = packKey;
        PackWeight = packWeight;
        AudioId = audioId;
        Action = action;
        Domain = domain;
        Weight = weight;
        Volume = volume;
        Pitch = pitch;
        Participation = new System.Collections.ObjectModel.ReadOnlyDictionary<string, bool>(participation == null
            ? new Dictionary<string, bool>() : participation.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
        Family = family;
        TargetNamespace = targetNamespace;
        ChildDomain = childDomain;
        SourceDomain = sourceDomain ?? (family == SubjectFamily.NonPawn
            ? Kernel.AudioDomain.NonPawn(targetNamespace, domain)
            : new Kernel.AudioDomain(new Kernel.RaceKey(domain), childDomain == "" ? null : new Kernel.XenotypeKey(childDomain)));
        SourceTier = sourceTier;
    }
}

public sealed class PlaybackRequest
{
    public readonly string Subject;
    public readonly string Domain;
    public readonly SubjectFamily Family;
    public readonly string TargetNamespace;
    public readonly string Action;
    public readonly string SubjectRole;
    public readonly string ChildDomain;
    public readonly AudioCandidate Audio;
    public readonly long Tick;
    public readonly double Pitch;
    public readonly bool Sustained;
    public readonly int Priority;
    public readonly Condition? Condition;
    public readonly IngestionResultFact? Ingestion;
    public PlaybackRequest(string subject, string domain, string action, AudioCandidate audio, long tick, double pitch = 1,
        bool sustained = false, int priority = 0, Condition? condition = null, string subjectRole = "Subject", string childDomain = "", IngestionResultFact? ingestion = null)
    {
        Subject = subject;
        Ingestion = ingestion;
        SubjectRole = subjectRole;
        ChildDomain = childDomain;
        Domain = domain;
        Action = action;
        Audio = audio;
        Tick = tick;
        Pitch = pitch;

        Sustained = sustained;
        Priority = priority;
        Condition = condition;
        Family = audio.Family;
        TargetNamespace = audio.TargetNamespace;
    }
}

public sealed class DecisionInput
{
    public string Subject = "";
    public string Domain = "";
    public SubjectFamily Family;
    public string TargetNamespace = "";
    public string ChildDomain = "";
    public string SubjectRole = "Subject";
    public long Tick;
    public bool StateOpportunity;
    public bool Eligible = true;
    public ISet<string> BlockedActions = new HashSet<string>(StringComparer.Ordinal);
    public IDictionary<string, string> BlockedActionReasons = new Dictionary<string, string>(StringComparer.Ordinal);
    public bool HasVocalOrgans = true;
    public double TalkingChance = 1;
    public FactSnapshot Facts = new(Array.Empty<KeyValuePair<string, Truth>>());
    public ActionPlan[] Actions = Array.Empty<ActionPlan>();
    public AudioCandidate[] Audio = Array.Empty<AudioCandidate>();
    public Kernel.SelectionMode SourceMode = Kernel.SelectionMode.Fallback;
    public Kernel.BuiltInFallbackTable Fallback = Kernel.BuiltInFallbackTable.Empty;
    public Kernel.SqueakPoolRegistry? AudioPool;
    public Kernel.SelectionContext? AudioContext;
    public Kernel.ISoundGate? SoundGate;
    public double Activity = 1;
    public double StateWaitFactor = 1;
    public ModulationPlan Modulation = new();
    public IReadOnlyDictionary<string, ModulationPlan> ActionModulation = new Dictionary<string, ModulationPlan>();
    public string ActiveSustainedKey = "";
    public bool MergeAudioPool;
}

public readonly struct DecisionOutcome
{
    public readonly string Action;
    public readonly string Reason;
    public readonly PlaybackReceipt? Receipt;
    public readonly IReadOnlyList<FactDiagnostic> Diagnostics;
    public DecisionOutcome(string action, string reason, PlaybackReceipt? receipt = null, IReadOnlyList<FactDiagnostic>? diagnostics = null)
    {
        Action = action;
        Reason = reason;
        Receipt = receipt;
        Diagnostics = diagnostics ?? Array.Empty<FactDiagnostic>();
    }
}

/// <summary>The only owner of new OneShot opportunity, attempt and result settlement rules.</summary>
public sealed class OneShotDecision
{
    public DecisionOutcome Run(DecisionState state, StatePolicy policy, DecisionInput input, IOneShotRandom random, IOneShotBackend backend)
    {
        if (input.StateOpportunity && input.Tick < state.NextStateTick)
        {
            return new DecisionOutcome("", "NotDue");
        }

        if (string.IsNullOrWhiteSpace(input.Subject)
            || string.IsNullOrWhiteSpace(input.Domain)
            || !Enum.IsDefined(typeof(SubjectFamily), input.Family)
            || string.IsNullOrWhiteSpace(input.SubjectRole)
            || (input.Family == SubjectFamily.PawnRace && input.TargetNamespace != "")
            || (input.Family == SubjectFamily.NonPawn && (string.IsNullOrWhiteSpace(input.TargetNamespace) || input.ChildDomain != ""))
            || (input.AudioContext.HasValue && !MatchesSubject(input, input.AudioContext.Value.Domain)))
        {
            return Stop(state, policy, input, "", "InvalidIdentity");
        }

        if (!input.Eligible)
        {
            return Stop(state, policy, input, "", "Ineligible");
        }
        if (double.IsNaN(input.Activity) || double.IsInfinity(input.Activity) || input.Activity <= 0
            || double.IsNaN(input.StateWaitFactor) || double.IsInfinity(input.StateWaitFactor) || input.StateWaitFactor <= 0)
        {
            return Stop(state, policy, input, "", "InvalidSoftRhythm");
        }

        if (state.AttemptTick == input.Tick)
        {
            return Stop(state, policy, input, "", "TickOccupied");
        }

        ActionPlan? action = ChooseAction(input, out string selectionReason, out var diagnostics);
        if (action == null)
        {
            return Stop(state, policy, input, "", selectionReason, diagnostics);
        }
        if (input.BlockedActions.Contains(action.Key))
        {
            state.AttemptTick = input.Tick;
            string reason = input.BlockedActionReasons.TryGetValue(action.Key, out var detail) ? detail : "ActionOrScopeRejected";
            return Stop(state, policy, input, action.Key, reason, diagnostics);
        }
        if (action.RequiresVocal && !input.HasVocalOrgans)
        {
            state.AttemptTick = input.Tick;
            return Stop(state, policy, input, action.Key, "NoVocalOrgans", diagnostics);
        }
        if (action.Sustained && action.Key == input.ActiveSustainedKey)
        {
            var existing = new PlaybackReceipt(PlaybackResult.AlreadyActive, "SameOwnedSustainer");
            Settle(state, policy, input, action, existing);
            return new DecisionOutcome(action.Key, existing.Reason, existing, diagnostics);
        }

        // Only a new attempt reserves this tick; maintenance still requires current authorization.
        state.AttemptTick = input.Tick;

        if (input.Tick < state.ActionDeadline(action.Key) || (action.CheckShared && input.Tick < state.SharedDeadline))
        {
            return Stop(state, policy, input, action.Key, "HardDeadline", diagnostics);
        }

        bool enteredBackend = false;
        try
        {
            if (!action.SkipTalking && !Pass(input.TalkingChance, RandomPurpose.Talking, random))
            {
                return Stop(state, policy, input, action.Key, "TalkingRejected", diagnostics);
            }

            double chance = input.StateOpportunity ? action.Chance : Math.Min(1, action.Chance * input.Activity);
            if (!Pass(chance, RandomPurpose.Eligibility, random))
            {
                return Stop(state, policy, input, action.Key, "ChanceRejected", diagnostics);
            }

            var audio = Select(input, action, random);
            if (audio == null)
            {
                return Stop(state, policy, input, action.Key, "NoBoundAudio", diagnostics);
            }

            var modulation = input.ActionModulation.TryGetValue(action.Key, out var configured) ? configured : input.Modulation;
            var local = ResponseModulation.Compose(audio.Pitch * action.Pitch, audio.Volume * action.Volume, modulation, audio.Participation, random);
            if (!local.Valid)
                return Stop(state, policy, input, action.Key, local.Reason, diagnostics);
            audio = new AudioCandidate(audio.EntryKey, audio.AudioId, audio.Action, audio.Domain, audio.Weight,
                local.Volume, audio.Family, audio.TargetNamespace, pitch: local.Pitch,
                participation: audio.Participation, packKey: audio.PackKey, packWeight: audio.PackWeight,
                childDomain: audio.ChildDomain, sourceDomain: audio.SourceDomain, sourceTier: audio.SourceTier);
            enteredBackend = true;
            var receipt = backend.Dispatch(new PlaybackRequest(input.Subject, input.Domain, action.Key, audio, input.Tick, local.Pitch,
                action.Sustained, action.Priority, action.Condition, input.SubjectRole, input.ChildDomain, input.Facts.Ingestion));
            Settle(state, policy, input, action, receipt);
            return new DecisionOutcome(action.Key, receipt.Reason, receipt, diagnostics);
        }
        catch (Exception error)
        {
            // Backends must translate side-effect uncertainty; an escaping backend fault is conservative.
            var receipt = new PlaybackReceipt(enteredBackend ? PlaybackResult.Indeterminate : PlaybackResult.Rejected, error.GetType().Name);
            Settle(state, policy, input, action, receipt);
            return new DecisionOutcome(action.Key, "UnhandledDispatchFault", receipt, diagnostics);
        }
    }

    private static ActionPlan? ChooseAction(DecisionInput input, out string reason, out IReadOnlyList<FactDiagnostic> diagnostics)
    {
        reason = "NoTrueAction";
        var failures = new List<FactDiagnostic>();
        diagnostics = failures.AsReadOnly();
        foreach (var action in input.Actions.OrderByDescending(x => x.Priority).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            Truth result;
            try
            {
                result = action.Condition.Evaluate(input.Facts);
            }
            catch (Exception error)
            {
                result = Truth.Unknown;
                reason = "ContractFault";
                failures.Add(new FactDiagnostic(action.Key, "Condition", error.GetType().Name + ":" + error.Message));
            }

            if (result == Truth.True)
            {
                return action;
            }

            if (result == Truth.Unknown)
            {
                foreach (string key in action.Condition.RequiredFacts().Distinct(StringComparer.Ordinal))
                {
                    if (input.Facts.Read(key) == Truth.Unknown && !failures.Any(x => x.Key == key))
                    {
                        failures.Add(input.Facts.ExplainUnknown(key));
                    }
                }

                if (reason != "ContractFault")
                {
                    reason = "UnknownFacts";
                }
            }
        }

        return null;
    }

    private static AudioCandidate? Select(DecisionInput input, ActionPlan plan, IOneShotRandom random)
    {
        string action = plan.Key;
        string routeKey = plan.Key;
        var candidates = input.Audio
            .Where(candidate => candidate.Action == action
                && candidate.Domain == input.Domain
                && candidate.Family == input.Family
                && candidate.TargetNamespace == input.TargetNamespace
                && (candidate.ChildDomain == "" || candidate.ChildDomain == input.ChildDomain))
            .OrderBy(candidate => candidate.EntryKey, StringComparer.Ordinal)
            .ToArray();
        var domain = input.Family == SubjectFamily.NonPawn
            ? Kernel.AudioDomain.NonPawn(input.TargetNamespace, input.Domain)
            : new Kernel.AudioDomain(new Kernel.RaceKey(input.Domain), null);
        var metadata = new Dictionary<Kernel.ActionSoundSet, AudioCandidate>();
        var entries = new List<Kernel.VoicePackEntry>();
        foreach (var pack in candidates.GroupBy(x => (Domain: x.SourceDomain, Pack: x.PackKey.Length == 0 ? x.EntryKey : x.PackKey)))
        {
            var variants = new List<Kernel.ActionSoundSet>();
            foreach (var candidate in pack)
            {
                var variant = new Kernel.ActionSoundSet(new[] { candidate.AudioId }, null, (float)candidate.Weight);
                variants.Add(variant);
                metadata.Add(variant, candidate);
            }
            var first = pack.First();
            entries.Add(new Kernel.VoicePackEntry(pack.Key.Pack, pack.Key.Domain,
                (float)(first.PackKey.Length == 0 ? first.Weight : first.PackWeight),
                new Dictionary<string, IReadOnlyList<Kernel.ActionSoundSet>> { [routeKey] = variants }));
        }
        var contextDomain = input.AudioContext?.Domain ?? (input.Family == SubjectFamily.PawnRace && input.ChildDomain != ""
            ? new Kernel.AudioDomain(new Kernel.RaceKey(input.Domain), new Kernel.XenotypeKey(input.ChildDomain)) : domain);
        var pool = input.AudioPool?.WithDefinitionIds(contextDomain);
        if (pool == null || input.MergeAudioPool)
        {
            var combined = new List<Kernel.VoicePackEntry>(entries);
            if (pool != null)
            {
                var selectedDomain = contextDomain;
                var domains = new[] { selectedDomain, domain }.Distinct();
                foreach (var selected in domains)
                {
                    var existing = pool.PoolFor(selected);
                    if (existing != null)
                    {
                        combined.AddRange(existing.Entries);
                    }
                }
            }
            pool = new Kernel.SqueakPoolRegistry(combined, input.Fallback);
        }
        var context = input.AudioContext ?? new Kernel.SelectionContext(input.Family == SubjectFamily.PawnRace && input.ChildDomain != ""
            ? new Kernel.AudioDomain(new Kernel.RaceKey(input.Domain), new Kernel.XenotypeKey(input.ChildDomain)) : domain,
            routeKey, Kernel.AgeBucket.Adult, true);
        if (context.ActionKey != routeKey)
        {
            context = new Kernel.SelectionContext(context.Domain, routeKey, context.Age, context.Production, context.AllowEggs);
        }
        var chosen = pool.Select(
            context,
            input.SourceMode,
            input.SoundGate ?? new BoundAudioGate(),
            new SelectionRandom(random));
        if (chosen.IsNone)
        {
            return null;
        }
        var selectedCandidate = chosen.SelectedVariant != null && metadata.TryGetValue(chosen.SelectedVariant, out var mapped) ? mapped : null;
        return new AudioCandidate(selectedCandidate?.EntryKey ?? chosen.SelectedEntryKey!, chosen.SoundKey!, action,
            input.Domain, selectedCandidate?.Weight ?? chosen.SelectedVariant?.Weight ?? 1,
            selectedCandidate?.Volume ?? 1, input.Family, input.TargetNamespace, selectedCandidate?.Pitch ?? 1,
            selectedCandidate?.Participation, selectedCandidate?.PackKey ?? chosen.PoolStableKey ?? "",
            selectedCandidate?.PackWeight ?? 1, input.ChildDomain,
            chosen.SelectedContext!.Value.Domain, chosen.Tier);
    }

    private static bool MatchesSubject(DecisionInput input, Kernel.AudioDomain domain) => input.Family == SubjectFamily.NonPawn
        ? domain.IsNonPawn && domain.ObjectNamespace == input.TargetNamespace && domain.ObjectTarget == input.Domain
        : !domain.IsNonPawn && domain.Race.DefName == input.Domain && (domain.Xenotype?.DefName ?? "") == input.ChildDomain;

    private sealed class BoundAudioGate : Kernel.ISoundGate
    {
        public bool Playable(string audioId, Kernel.SelectionContext context) => true;
    }

    private sealed class SelectionRandom : Kernel.IRollSource
    {
        private readonly IOneShotRandom source;
        public SelectionRandom(IOneShotRandom source)
        {
            this.source = source;
        }

        public double Next01() => Roll(source, RandomPurpose.Sound);
    }

    private static bool Pass(double chance, RandomPurpose purpose, IOneShotRandom random)
    {
        if (double.IsNaN(chance) || chance < 0 || chance > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chance));
        }

        return chance >= 1 || (chance > 0 && Roll(random, purpose) < chance);
    }

    private static double Roll(IOneShotRandom random, RandomPurpose purpose)
    {
        double value = random.Next(purpose);
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }

    private static DecisionOutcome Stop(DecisionState state, StatePolicy policy, DecisionInput input, string action, string reason, IReadOnlyList<FactDiagnostic>? diagnostics = null)
    {
        if (input.StateOpportunity)
        {
            state.NextStateTick = Math.Max(TickDeadline.Add(input.Tick, StatePolicy.Retry), state.SharedDeadline);
        }

        return new DecisionOutcome(action, reason, diagnostics: diagnostics);
    }

    private static void Settle(DecisionState state, StatePolicy policy, DecisionInput input, ActionPlan action, PlaybackReceipt receipt)
    {
        if (receipt.Result == PlaybackResult.Dispatched || receipt.Result == PlaybackResult.Indeterminate)
        {
            state.Protect(action, input, policy);
        }

        if (receipt.Result == PlaybackResult.Dispatched)
        {
            state.ConfirmedTick = input.Tick;
            state.ConfirmedAction = action.Key;
        }

        if (input.StateOpportunity)
        {
            long applicableShared = action.CheckShared ? state.SharedDeadline : input.Tick;
            if (receipt.Result == PlaybackResult.Dispatched)
            {
                double softWait = Math.Ceiling(policy.Wait * (input.StateWaitFactor / input.Activity));
                long wait = softWait >= long.MaxValue ? long.MaxValue : Math.Max(1, (long)softWait);
                state.NextStateTick = TickDeadline.Add(Math.Max(input.Tick, applicableShared), wait);
            }
            else
            {
                state.NextStateTick = Math.Max(TickDeadline.Add(input.Tick, StatePolicy.Retry), applicableShared);
            }
        }
        else
        {
            state.NextStateTick = Math.Max(state.NextStateTick, state.SharedDeadline);
        }
    }
}

/// <summary>Native float boundary, not a new modulation range or a clamp.</summary>
public static class OneShotOutput
{
    public static bool TryPrepare(double localVolume, double localPitch, double globalVolume, out float volume, out float pitch)
    {
        double finalVolume = localVolume * globalVolume;
        volume = 0;
        pitch = 0;
        if (double.IsNaN(localVolume) || double.IsInfinity(localVolume) || localVolume < 0
            || double.IsNaN(globalVolume) || double.IsInfinity(globalVolume) || globalVolume < 0
            || double.IsNaN(finalVolume) || double.IsInfinity(finalVolume) || finalVolume < 0 || finalVolume > float.MaxValue
            || double.IsNaN(localPitch) || double.IsInfinity(localPitch) || localPitch <= 0 || localPitch > float.MaxValue)
        {
            return false;
        }
        volume = (float)finalVolume;
        pitch = (float)localPitch;
        return !float.IsInfinity(volume) && !float.IsInfinity(pitch) && pitch > 0 && (finalVolume == 0 || volume > 0);
    }
}

/// <summary>Translates platform evidence without letting diagnostic errors rewrite a receipt.</summary>
public static class OneShotDispatch
{
    public static PlaybackReceipt Execute(Func<bool> preflight, Action nativeCall, Action? diagnostics = null)
    {
        try
        {
            if (!preflight())
            {
                return new PlaybackReceipt(PlaybackResult.Rejected, "PreflightRejected");
            }
        }
        catch (Exception e)
        {
            return new PlaybackReceipt(PlaybackResult.Rejected, "Preflight:" + e.GetType().Name);
        }

        try
        {
            nativeCall();
        }
        catch (Exception e)
        {
            return new PlaybackReceipt(PlaybackResult.Indeterminate, "NativeCall:" + e.GetType().Name);
        }

        try
        {
            diagnostics?.Invoke();
        }
        catch (Exception error)
        {
            return new PlaybackReceipt(PlaybackResult.Dispatched, "NativeCallReturned;Diagnostic:" + error.GetType().Name);
        }

        return new PlaybackReceipt(PlaybackResult.Dispatched, "NativeCallReturned");
    }
}
