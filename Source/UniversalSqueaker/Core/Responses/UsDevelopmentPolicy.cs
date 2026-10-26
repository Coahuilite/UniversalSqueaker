using System;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>Temporary process-only explicit candidate. Not a player setting or a publication default.</summary>
public enum UsEquipQualification { Unselected, PlayerCoreEquip }

public sealed class UsDevelopmentPolicy
{
    internal readonly UsResponsePolicy RuntimePolicy;
    internal readonly StatePolicy State;
    internal readonly UsEquipQualification EquipQualification;
    internal readonly long Interval;
    internal readonly long WoundedInterval;
    internal readonly long InitialSpread;
    internal readonly double EventProbability;
    internal readonly double StateWaitFactor;
    internal readonly double InteractionIntervalSeconds;

    public UsDevelopmentPolicy(long waitTicks, long sharedTicks, long intervalTicks, long woundedTicks,
        long initialSpreadTicks, double eventProbability, double stateWaitFactor,
        UsEquipQualification equipQualification = UsEquipQualification.Unselected, double interactionIntervalSeconds = 0)
    {
        if (!Enum.IsDefined(typeof(UsEquipQualification), equipQualification)
            || intervalTicks < 0 || woundedTicks < 0 || initialSpreadTicks < 0
            || double.IsNaN(eventProbability) || eventProbability < 0 || eventProbability > 1
            || double.IsNaN(stateWaitFactor) || double.IsInfinity(stateWaitFactor) || stateWaitFactor <= 0
            || double.IsNaN(interactionIntervalSeconds) || double.IsInfinity(interactionIntervalSeconds) || interactionIntervalSeconds < 0)
        {
            throw new ArgumentException("Invalid explicit development policy");
        }
        EquipQualification = equipQualification;
        State = new StatePolicy(sharedTicks, waitTicks);
        Interval = intervalTicks;
        WoundedInterval = woundedTicks;
        InitialSpread = initialSpreadTicks;
        EventProbability = eventProbability;
        StateWaitFactor = stateWaitFactor;
        InteractionIntervalSeconds = interactionIntervalSeconds;
        RuntimePolicy = new UsResponsePolicy(this);
    }
}
