namespace UniversalSqueaker;

public partial class CompSqueaker
{
    public override void CompTick()
    {
        SynchronizePeriodicMembership();
        UsOneShotRuntime.TryTick(Pawn);
    }

    /// <summary>Built-in and public string calls never fall back to another decision owner.</summary>
    public void NotifyExternalByKey(string actionKey, SqueakTriggerOrigin origin, SqueakInvocationSource source)
        => NotifyObservedByKey(actionKey, origin, source, "CallerSupplied");

    internal void NotifyObservedByKey(string actionKey, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence)
    {
        SynchronizePeriodicMembership();
        UsOneShotRuntime.TryObservedEvent(Pawn, actionKey, origin, source, evidence);
    }
}
