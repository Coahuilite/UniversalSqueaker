namespace UniversalSqueaker;

public partial class CompSqueaker
{
    public override void CompTick()
    {
        SynchronizePeriodicMembership();
        SqueakerRuntime.TryTick(Pawn);
    }

    internal void NotifyObservedByKey(string actionKey, SqueakTriggerOrigin origin, SqueakInvocationSource source, string evidence,
        Responses.InteractionOpportunity? interaction = null)
    {
        SynchronizePeriodicMembership();
        SqueakerRuntime.TryObservedEvent(Pawn, actionKey, origin, source, evidence, interaction);
    }
}
