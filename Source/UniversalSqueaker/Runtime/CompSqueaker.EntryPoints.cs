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
    {
        SynchronizePeriodicMembership();
        UsOneShotRuntime.TryEvent(Pawn, actionKey, origin, source);
    }
}
