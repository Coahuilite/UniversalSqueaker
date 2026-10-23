namespace UniversalSqueaker;

// Native caller provenance, not a second action definition or eligibility policy.
public enum SqueakTriggerOrigin { Periodic, Wounded, Select, Death, Draft, Undraft, Attack, Equip, MentalBreak, External }
public enum SqueakInvocationSource { Periodic, StateEvent, PlayerSelection, ActiveCommand }

/// <summary>Current native provenance. Optional Pawn qualifications belong to configuration.</summary>
public readonly struct SqueakTriggerInvocation
{
    public readonly SqueakTriggerOrigin Origin;
    public readonly SqueakInvocationSource Source;
    public bool IsActiveCommand => Source == SqueakInvocationSource.ActiveCommand;

    public SqueakTriggerInvocation(SqueakTriggerOrigin origin, SqueakInvocationSource source) { Origin = origin; Source = source; }
}
