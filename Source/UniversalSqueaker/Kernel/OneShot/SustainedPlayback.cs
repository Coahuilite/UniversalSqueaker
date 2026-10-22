using System;

namespace UniversalSqueaker.OneShot;

public interface ISustainedHandle
{
    bool Active { get; }
    void Maintain();
    void End();
}

public readonly struct SustainedStart
{
    public readonly PlaybackReceipt Receipt;
    public readonly ISustainedHandle? Handle;
    public SustainedStart(PlaybackReceipt receipt, ISustainedHandle? handle = null)
    {
        Receipt = receipt;
        Handle = handle;
    }
}

/// <summary>One owned slot. Preparation belongs before Dispatch; this owner never selects or retries audio.</summary>
public sealed class SustainedPlayback
{
    private ISustainedHandle? handle;
    private string key = "";
    private int priority;
    private Condition? condition;
    private long nextReview;
    public string LastEndReason { get; private set; } = "";
    public bool Active => handle?.Active == true;
    public string ActiveKey => Active ? key : "";
    public bool ReviewDue(long tick) => Active && tick >= nextReview;

    public PlaybackReceipt Dispatch(PlaybackRequest request, bool sustainedMaterial, Func<SustainedStart> startPrepared)
    {
        if (Active)
        {
            if (key == request.Action)
            {
                handle!.Maintain();
                return new PlaybackReceipt(PlaybackResult.AlreadyActive, "SameOwnedSustainer");
            }
            if (request.Priority <= priority)
            {
                return new PlaybackReceipt(PlaybackResult.Rejected, "ExistingHigherOrEqualSustainer");
            }
            if (!Stop("HigherPriorityReplacement"))
            {
                return new PlaybackReceipt(PlaybackResult.Rejected, "NewStartNotEntered:" + LastEndReason);
            }
        }
        SustainedStart result;
        try
        {
            result = startPrepared();
        }
        catch (Exception error)
        {
            return new PlaybackReceipt(PlaybackResult.Indeterminate, "StartFault:" + error.GetType().Name);
        }
        if (sustainedMaterial)
        {
            if (result.Receipt.Result != PlaybackResult.Dispatched)
            {
                return result.Receipt;
            }
            if (result.Handle?.Active != true)
            {
                return new PlaybackReceipt(PlaybackResult.Indeterminate, "NoEffectiveSustainerAfterCall");
            }
            handle = result.Handle;
            key = request.Action;
            priority = request.Priority;
            condition = request.Condition;
            nextReview = TickDeadline.Add(request.Tick, 60);
        }
        return result.Receipt;
    }

    public string Review(long tick, FactSnapshot facts)
        => Review(tick, facts, null, false);

    public string Review(long tick, FactSnapshot facts, Condition? currentCondition, bool force)
    {
        if (!Active || !force && !ReviewDue(tick))
        {
            return "NotDue";
        }
        nextReview = TickDeadline.Add(tick, 60);
        condition = currentCondition ?? condition;
        Truth truth;
        try
        {
            truth = condition?.Evaluate(facts) ?? Truth.Unknown;
        }
        catch (Exception)
        {
            truth = Truth.Unknown;
        }
        if (truth != Truth.True)
        {
            Stop(truth == Truth.False ? "StateBecameFalse" : "StateReviewUnknown");
            return LastEndReason;
        }
        return "StateStillTrue";
    }
    public void Maintain()
    {
        if (Active)
        {
            handle!.Maintain();
        }
        else
        {
            handle = null;
        }
    }
    public bool Stop(string reason)
    {
        var old = handle;
        handle = null;
        key = "";
        condition = null;
        LastEndReason = reason;
        try
        {
            if (old?.Active == true)
            {
                old.End();
            }
            return true;
        }
        catch (Exception error)
        {
            // The old end is uncertain, but the replacement callback has not been entered.
            // Detach first so lifecycle cleanup cannot repeat a non-idempotent End.
            LastEndReason = reason + ":EndFault:" + error.GetType().Name;
            return false;
        }
    }
}
