using System;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

/// <summary>Temporary process-only explicit candidate. Not a player setting or a publication default.</summary>
public sealed class UsDevelopmentPolicy
{
    internal readonly StatePolicy State;
    internal readonly long Interval;
    internal readonly long WoundedInterval;
    internal readonly long InitialSpread;
    internal readonly double EventProbability;
    internal readonly double SoftRhythmFactor;

    public UsDevelopmentPolicy(long waitTicks, long sharedTicks, long intervalTicks, long woundedTicks,
        long initialSpreadTicks, double eventProbability, double softRhythmFactor)
    {
        if (intervalTicks < 0 || woundedTicks < 0 || initialSpreadTicks < 0
            || double.IsNaN(eventProbability) || eventProbability < 0 || eventProbability > 1
            || double.IsNaN(softRhythmFactor) || double.IsInfinity(softRhythmFactor) || softRhythmFactor <= 0)
        {
            throw new ArgumentException("Invalid explicit development policy");
        }
        State = new StatePolicy(sharedTicks, waitTicks);
        Interval = intervalTicks;
        WoundedInterval = woundedTicks;
        InitialSpread = initialSpreadTicks;
        EventProbability = eventProbability;
        SoftRhythmFactor = softRhythmFactor;
    }
}
