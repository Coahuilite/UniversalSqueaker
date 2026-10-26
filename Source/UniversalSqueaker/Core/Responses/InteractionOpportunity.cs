namespace UniversalSqueaker.Responses;

/// <summary>Platform-neutral evidence of one accepted native root operation, not an input frame.</summary>
public readonly struct InteractionOpportunity
{
    public readonly string EventFact;
    public readonly long OperationId;
    public readonly double MonotonicSeconds;
    public static bool IsOfficialInteractionFact(string fact)
        => fact == "us.core/SelectEvent" || fact == "us.core/DraftEvent" || fact == "us.core/UndraftEvent";
    public InteractionOpportunity(string eventFact, long operationId, double monotonicSeconds)
    { EventFact = eventFact; OperationId = operationId; MonotonicSeconds = monotonicSeconds; }
}
