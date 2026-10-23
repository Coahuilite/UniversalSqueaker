using System;

namespace UniversalSqueaker;

public enum SettingsSaveState { Idle, Saving, Saved, Failed }

/// <summary>One synchronous physical-save owner. Callers supply realtime and the actual persistence boundary.</summary>
public sealed class SettingsSaveOwner
{
    private long requested, persisted, failed = -1, closeRetried = -1;
    private double due, statusUntil;
    private bool writing;
    public long RequestCount { get; private set; }
    public long PhysicalSaveCount { get; private set; }
    public SettingsSaveState State { get; private set; }
    public bool Dirty => requested > persisted;
    public bool PublicationPending { get; private set; }
    public long Generation => requested;
    public string Failure { get; private set; } = "";
    public bool StatusVisible(double realtime) => State == SettingsSaveState.Saving || State == SettingsSaveState.Failed || realtime < statusUntil;

    public void Edited(double realtime)
    {
        RequestCount++; requested++; failed = -1; due = realtime + .35;
        State = SettingsSaveState.Saving;
    }

    public bool Flush(double realtime, Action write, bool force = false, bool closing = false)
    {
        if (writing || !Dirty) return false;
        if (failed == requested)
        {
            if (!closing || closeRetried == requested) return false;
            closeRetried = requested; failed = -1;
        }
        if (!force && realtime < due) return false;
        long generation = requested;
        try
        {
            writing = true; State = SettingsSaveState.Saving;
            write();
            Confirm(generation, realtime);
            return true;
        }
        catch (Exception error)
        {
            failed = generation; State = SettingsSaveState.Failed;
            Failure = error.GetType().Name + ":" + error.Message;
            statusUntil = double.PositiveInfinity;
            return false;
        }
        finally { writing = false; }
    }

    public bool CommitAtomic(double realtime, Func<bool> persistCandidate, out string failure)
    {
        failure = "";
        if (writing) { failure = "SettingsWriteInProgress"; return false; }
        long generation = requested;
        try
        {
            writing = true;
            if (!persistCandidate()) { failure = "CandidatePersistenceRejected"; return false; }
            Confirm(generation, realtime);
            return true;
        }
        catch (Exception error) { failure = error.GetType().Name + ":" + error.Message; return false; }
        finally { writing = false; }
    }

    /// <summary>Disk is already committed. Failure schedules publication repair, never a second disk transaction.</summary>
    public bool PublishCommitted(Action publish, out string failure)
    {
        failure = "";
        try { publish(); PublicationPending = false; return true; }
        catch (Exception error)
        {
            PublicationPending = true;
            failure = "Committed:RuntimePublishFailed:" + error.GetType().Name;
            return false;
        }
    }
    public bool RepairPublication(Action publish, out string failure)
    {
        failure = "";
        return !PublicationPending || PublishCommitted(publish, out failure);
    }
    public void Retry() { if (Dirty) { failed = -1; due = double.NegativeInfinity; } }
    public void TickStatus(double realtime)
    { if (!Dirty && State == SettingsSaveState.Saved && realtime >= statusUntil) State = SettingsSaveState.Idle; }

    private void Confirm(long generation, double realtime)
    {
        PhysicalSaveCount++; persisted = Math.Max(persisted, generation);
        if (failed <= generation) failed = -1;
        if (closeRetried <= generation) closeRetried = -1;
        Failure = "";
        State = Dirty ? SettingsSaveState.Saving : SettingsSaveState.Saved;
        if (!Dirty) due = realtime;
        statusUntil = realtime + 1.8;
    }
}
