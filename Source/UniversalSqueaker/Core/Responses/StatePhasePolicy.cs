using System;

namespace UniversalSqueaker.Responses;

internal enum StateScheduleKind { Initial, Success, Retry, SharedRaise }

/// <summary>Optional neutral identity, never a claim that a scheduling policy has been adopted.
/// Signed values are consumed as their exact 32-bit patterns; zero is a valid world seed.</summary>
public readonly struct StatePhaseInput
{
    public readonly int WorldSeed, SubjectId;
    public StatePhaseInput(int worldSeed, int subjectId) { WorldSeed = worldSeed; SubjectId = subjectId; }
    public uint Pattern
    {
        get
        {
            unchecked
            {
                uint value = (uint)WorldSeed ^ 0x55535031u; // US state-phase v1 domain
                value += (uint)SubjectId * 0x9e3779b9u;
                value ^= value >> 16;
                value *= 0x85ebca6bu;
                value ^= value >> 13;
                value *= 0xc2b2ae35u;
                return value ^ (value >> 16);
            }
        }
    }
}

/// <summary>Adopted ordinary budgets or explicit author windows. The identity maps to a normalized
/// position in each window; window/identity captured by a new schedule also own later H raises.</summary>
public sealed class StatePhasePolicy
{
    public const string AlgorithmVersion = "us.state-phase/1";
    public readonly int InitialWindow, SuccessWindow, RetryWindow, SharedRaiseWindow;
    private readonly bool ordinaryBudgets;
    internal static readonly StatePhasePolicy Ordinary = new(600, 120, 15, 15, true);
    public StatePhasePolicy(int initialWindow, int successWindow, int retryWindow, int sharedRaiseWindow)
    {
        if (initialWindow <= 0 || successWindow <= 0 || retryWindow <= 0 || sharedRaiseWindow <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialWindow), "Every phase window must be explicitly positive");
        InitialWindow = initialWindow; SuccessWindow = successWindow;
        RetryWindow = retryWindow; SharedRaiseWindow = sharedRaiseWindow;
    }
    private StatePhasePolicy(int initialWindow, int successWindow, int retryWindow, int sharedRaiseWindow, bool ordinaryBudgets)
        : this(initialWindow, successWindow, retryWindow, sharedRaiseWindow) => this.ordinaryBudgets = ordinaryBudgets;

    internal long Window(StateScheduleKind kind, long effectiveWait, double targetSpeedFactor)
    {
        if (kind == StateScheduleKind.Initial) return InitialWindow;
        if (!ordinaryBudgets) return kind == StateScheduleKind.Success ? SuccessWindow : RetryWindow;
        long budget = kind == StateScheduleKind.Success ? effectiveWait / 5 : StatePolicy.Retry / 2;
        if (budget <= 1 || double.IsNaN(targetSpeedFactor) || double.IsInfinity(targetSpeedFactor)) return 1;
        double cap = (kind == StateScheduleKind.Success ? SuccessWindow : RetryWindow) * Math.Max(1d, targetSpeedFactor);
        long capTicks = cap >= long.MaxValue ? long.MaxValue : (long)Math.Floor(cap);
        return Math.Max(1, Math.Min(budget, capTicks));
    }

    internal long RaisedWindow(StateScheduleKind kind, long window, double targetSpeedFactor)
        // An initial opportunity has not dispatched: H uses the short retry budget, never 600.
        => !ordinaryBudgets ? SharedRaiseWindow : kind == StateScheduleKind.Initial
            ? Window(StateScheduleKind.Retry, StatePolicy.Retry, targetSpeedFactor) : window;

    internal static long Align(long lowerBound, uint pattern, long window)
    {
        // floor((pattern / 2^32) * window), split to avoid overflowing a 64-bit product.
        ulong count = (ulong)window;
        long slot = (long)((ulong)pattern * (count >> 32) + (((ulong)pattern * (count & uint.MaxValue)) >> 32));
        long remainder = lowerBound % window;
        if (remainder < 0) remainder += window;
        long delta = slot >= remainder ? slot - remainder : window - (remainder - slot);
        return TickDeadline.Add(lowerBound, delta);
    }
}
