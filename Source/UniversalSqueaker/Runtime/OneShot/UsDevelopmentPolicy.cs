using System;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

/// <summary>Temporary process-only explicit candidate. Not a player setting or a publication default.</summary>
public enum UsSelectPawnQualification { Unselected, Unrestricted, ResponsivePlayerControlled }

public sealed class UsDevelopmentPolicy
{
    internal readonly StatePolicy State;
    internal readonly UsSelectPawnQualification SelectQualification;
    internal readonly long Interval;
    internal readonly long WoundedInterval;
    internal readonly long InitialSpread;
    internal readonly double EventProbability;
    internal readonly double StateWaitFactor;

    public UsDevelopmentPolicy(long waitTicks, long sharedTicks, long intervalTicks, long woundedTicks,
        long initialSpreadTicks, double eventProbability, double stateWaitFactor,
        UsSelectPawnQualification selectQualification = UsSelectPawnQualification.Unselected)
    {
        if (!Enum.IsDefined(typeof(UsSelectPawnQualification), selectQualification) || intervalTicks < 0 || woundedTicks < 0 || initialSpreadTicks < 0
            || double.IsNaN(eventProbability) || eventProbability < 0 || eventProbability > 1
            || double.IsNaN(stateWaitFactor) || double.IsInfinity(stateWaitFactor) || stateWaitFactor <= 0)
        {
            throw new ArgumentException("Invalid explicit development policy");
        }
        SelectQualification = selectQualification;
        State = new StatePolicy(sharedTicks, waitTicks);
        Interval = intervalTicks;
        WoundedInterval = woundedTicks;
        InitialSpread = initialSpreadTicks;
        EventProbability = eventProbability;
        StateWaitFactor = stateWaitFactor;
    }
}
