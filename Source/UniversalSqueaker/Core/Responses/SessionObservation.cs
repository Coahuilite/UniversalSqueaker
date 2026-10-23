namespace UniversalSqueaker.Responses;

/// <summary>Read-only settled facts. Protection expiry does not predict a future dispatch.</summary>
public readonly struct SessionObservation
{
    public readonly string Action, Reason, ActiveAction;
    public readonly PlaybackReceipt? Receipt;
    public readonly long ObservedTick, SharedDeadline, ActionDeadline, NextStateTick;
    public readonly long? ConfirmedTick;
    public readonly bool ChecksShared;
    public SessionObservation(string action, string reason, PlaybackReceipt? receipt, long observedTick,
        long sharedDeadline, long actionDeadline, long nextStateTick, long? confirmedTick, bool checksShared, string activeAction)
    {
        Action = action; Reason = reason; Receipt = receipt; ObservedTick = observedTick;
        SharedDeadline = sharedDeadline; ActionDeadline = actionDeadline; NextStateTick = nextStateTick;
        ConfirmedTick = confirmedTick; ChecksShared = checksShared; ActiveAction = activeAction;
    }
    public bool ProtectedAt(long now) => now < ActionDeadline || ChecksShared && now < SharedDeadline;
}
