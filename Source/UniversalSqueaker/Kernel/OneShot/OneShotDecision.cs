using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker.OneShot;

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
    StateWait
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
    private readonly Dictionary<string, Truth> facts;
    private readonly Dictionary<string, FactDiagnostic> diagnostics;
    public FactSnapshot(IEnumerable<KeyValuePair<string, Truth>> values, IEnumerable<FactDiagnostic>? diagnostics = null)
    {
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
    public readonly Condition[] Children;
    public Condition(string operation, string fact = "", params Condition[] children)
    {
        Operation = operation;
        Fact = fact;
        Children = children;
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
    public ActionPlan(string key, int priority, Condition condition, long interval, bool checkShared = true, bool writeShared = true, double chance = 1, string? baseAction = null)
    {
        if (interval < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (double.IsNaN(chance) || chance < 0 || chance > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chance));
        }

        Key = key;
        BaseAction = baseAction ?? key;
        Priority = priority;
        Condition = condition;
        Interval = interval;
        CheckShared = checkShared;
        WriteShared = writeShared;
        Chance = chance;
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
    internal void Protect(ActionPlan action, long now, StatePolicy policy)
    {
        deadlines[action.Key] = now + action.Interval;
        if (action.WriteShared)
        {
            SharedDeadline = Math.Max(SharedDeadline, now + policy.SharedInterval);
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
    public readonly string AudioId;
    public readonly string Action;
    public readonly string Domain;
    public readonly SubjectFamily Family;
    public readonly string TargetNamespace;
    public readonly double Weight;
    public readonly double Volume;
    public AudioCandidate(string entryKey, string audioId, string action, string domain, double weight, double volume = 1, SubjectFamily family = SubjectFamily.PawnRace, string targetNamespace = "")
    {
        if (string.IsNullOrEmpty(audioId)
            || double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0 || weight > float.MaxValue
            || double.IsNaN(volume) || double.IsInfinity(volume) || volume < 0 || volume > float.MaxValue)
        {
            throw new ArgumentException("Invalid audio candidate");
        }

        EntryKey = entryKey;
        AudioId = audioId;
        Action = action;
        Domain = domain;
        Weight = weight;
        Volume = volume;
        Family = family;
        TargetNamespace = targetNamespace;
    }
}

public sealed class PlaybackRequest
{
    public readonly string Subject;
    public readonly string Domain;
    public readonly SubjectFamily Family;
    public readonly string TargetNamespace;
    public readonly string Action;
    public readonly AudioCandidate Audio;
    public readonly long Tick;
    public PlaybackRequest(string subject, string domain, string action, AudioCandidate audio, long tick)
    {
        Subject = subject;
        Domain = domain;
        Action = action;
        Audio = audio;
        Tick = tick;
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
    public long Tick;
    public bool StateOpportunity;
    public bool Eligible = true;
    public ISet<string> BlockedActions = new HashSet<string>(StringComparer.Ordinal);
    public bool HasVocalOrgans = true;
    public double TalkingChance = 1;
    public FactSnapshot Facts = new(Array.Empty<KeyValuePair<string, Truth>>());
    public ActionPlan[] Actions = Array.Empty<ActionPlan>();
    public AudioCandidate[] Audio = Array.Empty<AudioCandidate>();
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
            || (input.Family == SubjectFamily.NonPawn && string.IsNullOrWhiteSpace(input.TargetNamespace)))
        {
            return Stop(state, policy, input, "", "InvalidIdentity");
        }

        if (!input.Eligible)
        {
            return Stop(state, policy, input, "", "Ineligible");
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

        // Selection reserves this tick even when a later scope or hard gate rejects it.
        state.AttemptTick = input.Tick;
        if (input.BlockedActions.Contains(action.Key))
        {
            return Stop(state, policy, input, action.Key, "ActionOrScopeRejected", diagnostics);
        }

        if (input.Tick < state.ActionDeadline(action.Key) || (action.CheckShared && input.Tick < state.SharedDeadline))
        {
            return Stop(state, policy, input, action.Key, "HardDeadline", diagnostics);
        }

        bool enteredBackend = false;
        try
        {
            if (!input.HasVocalOrgans)
            {
                return Stop(state, policy, input, action.Key, "NoVocalOrgans", diagnostics);
            }

            if (!Pass(input.TalkingChance, RandomPurpose.Talking, random))
            {
                return Stop(state, policy, input, action.Key, "TalkingRejected", diagnostics);
            }

            if (!Pass(action.Chance, RandomPurpose.Eligibility, random))
            {
                return Stop(state, policy, input, action.Key, "ChanceRejected", diagnostics);
            }

            var audio = Select(input, action.Key, random);
            if (audio == null)
            {
                return Stop(state, policy, input, action.Key, "NoBoundAudio", diagnostics);
            }

            enteredBackend = true;
            var receipt = backend.Dispatch(new PlaybackRequest(input.Subject, input.Domain, action.Key, audio, input.Tick));
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

    private static AudioCandidate? Select(DecisionInput input, string action, IOneShotRandom random)
    {
        var candidates = input.Audio
            .Where(candidate => candidate.Action == action
                && candidate.Domain == input.Domain
                && candidate.Family == input.Family
                && candidate.TargetNamespace == input.TargetNamespace)
            .OrderBy(candidate => candidate.EntryKey, StringComparer.Ordinal)
            .ToArray();
        var domain = new Kernel.AudioDomain(new Kernel.RaceKey(input.Domain), null);
        var entries = candidates.Select(candidate => new Kernel.VoicePackEntry(
            candidate.EntryKey,
            domain,
            (float)candidate.Weight,
            new Dictionary<string, IReadOnlyList<Kernel.ActionSoundSet>>
            {
                [action] = new[]
                {
                    new Kernel.ActionSoundSet(new[] { candidate.AudioId }, null, 1)
                }
            })).ToArray();
        var pool = new Kernel.SqueakPoolRegistry(entries, Kernel.BuiltInFallbackTable.Empty);
        var chosen = pool.Select(
            new Kernel.SelectionContext(domain, action, Kernel.AgeBucket.Adult, true),
            Kernel.SelectionMode.Fallback,
            new BoundAudioGate(),
            new SelectionRandom(random));
        return candidates.FirstOrDefault(x => x.EntryKey == chosen.PoolStableKey);
    }

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
            state.NextStateTick = Math.Max(input.Tick + StatePolicy.Retry, state.SharedDeadline);
        }

        return new DecisionOutcome(action, reason, diagnostics: diagnostics);
    }

    private static void Settle(DecisionState state, StatePolicy policy, DecisionInput input, ActionPlan action, PlaybackReceipt receipt)
    {
        if (receipt.Result == PlaybackResult.Dispatched || receipt.Result == PlaybackResult.Indeterminate)
        {
            state.Protect(action, input.Tick, policy);
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
                state.NextStateTick = Math.Max(input.Tick, applicableShared) + Math.Max(1, policy.Wait);
            }
            else
            {
                state.NextStateTick = Math.Max(input.Tick + StatePolicy.Retry, applicableShared);
            }
        }
        else
        {
            state.NextStateTick = Math.Max(state.NextStateTick, state.SharedDeadline);
        }
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
