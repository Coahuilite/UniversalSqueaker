namespace UniversalSqueaker;

public partial class CompSqueaker
{
    public override void CompTick()
    {
        SynchronizePeriodicMembership();
        SqueakerRuntime.TryTick(Pawn);
    }

    /// <summary>Built-in and public string calls never fall back to another decision owner.</summary>
    internal void NotifyExternalByKey(string actionKey, SqueakTriggerOrigin origin, SqueakInvocationSource source)
        => NotifyObservedByKey(actionKey, origin, source, "CallerSupplied");

    internal void NotifyObservedByKey(string actionKey, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence)
    {
        SynchronizePeriodicMembership();
        SqueakerRuntime.TryObservedEvent(Pawn, actionKey, origin, source, evidence);
    }
}
