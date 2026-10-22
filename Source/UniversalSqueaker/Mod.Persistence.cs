using System;
using Verse;
using RimWorld;
using UnityEngine;

namespace UniversalSqueaker;

public partial class UniversalSqueakerMod
{
    private long requestedSaveGeneration;
    private long persistedSaveGeneration;
    private long failedSaveGeneration = -1;
    private long closeRetryGeneration = -1;
    private long failureNotifiedGeneration = -1;
    private bool saveQueued;
    private bool writeInProgress;
    private float saveDueAt;
    private float saveStatusUntil;
    private SettingsSaveState saveState;
    public long SaveQueueRequestCount { get; private set; }
    public long PhysicalSaveCount { get; private set; }

    internal enum SettingsSaveState { Idle, Saving, Saved, Failed }
    internal SettingsSaveState SaveState => saveState;
    internal bool IsSettingsDirty => requestedSaveGeneration > persistedSaveGeneration;
    internal bool SaveStatusVisible => saveState == SettingsSaveState.Saving || saveState == SettingsSaveState.Failed || Time.realtimeSinceStartup < saveStatusUntil;

    /// <summary>Framework entry point. Persistence always reaches the base implementation directly, never this override recursively.</summary>
    public override void WriteSettings()
    {
        if (Settings.IsPersistenceBlockedByMigrationFailure) return;
        SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);
        FlushQueuedSettingsSave(true);
    }

    /// <summary>Explicit domain operation. Null source means forget this behavior binding, not audio selection.</summary>
    public bool ChangeBehaviorSource(BehaviorDomain domain, string? sourceId, out string failure)
    {
        bool changed = Settings.TryChangeBehaviorSource(domain, sourceId, UsOneShotRuntime.BehaviorSources,
            PersistBehaviorCandidate, out failure);
        if (changed)
        {
            try
            {
                Settings.ApplyToRuntime();
            }
            catch (Exception error)
            {
                // Persistence is already committed. Report a publish failure without inventing rollback
                // or prompting the caller to repeat the successful disk transaction.
                failure = "Committed:RuntimePublishFailed:" + error.GetType().Name;
                Log.Warning("usdiag behavior source " + failure);
            }
        }
        return changed;
    }

    private bool PersistBehaviorCandidate(string candidate)
    {
        if (writeInProgress || Settings.IsPersistenceBlockedByMigrationFailure) return false;
        SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);
        long generation = requestedSaveGeneration;
        try
        {
            writeInProgress = true;
            // Vanilla WriteModSettings truncates its final path. Stage the SAME SettingsBlock shape
            // with the same Scribe serializer, then atomically replace only this mod's Config file.
            string target = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath,
                GenText.SanitizeFilename("Mod_" + Content.FolderName + "_" + GetType().Name + ".xml"));
            bool committed = Settings.TryWriteBehaviorCandidate(target, candidate, staging =>
            {
                try
                {
                    Scribe.saver.InitSaving(staging, "SettingsBlock");
                    var settings = Settings;
                    Scribe_Deep.Look(ref settings, "ModSettings");
                }
                finally { Scribe.saver.FinalizeSaving(); }
            }, out string failure);
            if (!committed)
            {
                Log.Warning("usdiag behavior source save failed: " + failure);
                return false;
            }
            ConfirmPersistedGeneration(generation);
            return true;
        }
        catch (Exception error)
        {
            Log.Warning("usdiag behavior source save failed: " + error.GetType().Name);
            return false;
        }
        finally
        {
            writeInProgress = false;
        }
    }

    public bool TryChangeBehaviorOverride(string family, string targetNamespace, string target, string child, string action,
        BehaviorValues values, out string failure)
    {
        bool committed = Settings.TryChangeBehaviorOverride(family, targetNamespace, target, child, action, values,
            () => PersistBehaviorCandidate(Settings.behaviorSourcesXml), out failure);
        if (committed)
        {
            try { Settings.ApplyToRuntime(); }
            catch (Exception error)
            {
                failure = "Committed:RuntimePublishFailed:" + error.GetType().Name;
                Log.Warning("usdiag player behavior " + failure);
            }
        }
        return committed;
    }

    internal void QueueSettingsSave()
    {
        if (Settings.IsPersistenceBlockedByMigrationFailure) return;
        SaveQueueRequestCount++;
        requestedSaveGeneration++;
        // A later business edit creates a new generation and is the supported automatic recovery path after failure.
        failedSaveGeneration = -1;
        saveQueued = true;
        saveDueAt = Time.realtimeSinceStartup + .35f;
        saveState = SettingsSaveState.Saving;
    }

    internal void FlushQueuedSettingsSave(bool force = false, bool allowFailedGenerationRetry = false)
    {
        if (Settings.IsPersistenceBlockedByMigrationFailure) return;
        SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);
        if (!saveQueued || requestedSaveGeneration <= persistedSaveGeneration) return;
        // A failed generation remains visibly dirty but is never retried by debounce or a following framework write.
        if (failedSaveGeneration == requestedSaveGeneration)
        {
            // Closing is a single explicit retry opportunity. A framework WriteSettings that follows the same
            // close sees this marker and cannot immediately perform a second physical write.
            if (!allowFailedGenerationRetry || closeRetryGeneration == requestedSaveGeneration) return;
            closeRetryGeneration = requestedSaveGeneration;
            failedSaveGeneration = -1;
            saveQueued = true;
        }
        if (!force && Time.realtimeSinceStartup < saveDueAt) return;
        PersistSettingsNow();
    }

    /// <summary>Explicit retry hook for a future UI; normal edits already create a retryable new generation.</summary>
    internal void RetrySettingsSave()
    {
        if (requestedSaveGeneration <= persistedSaveGeneration) return;
        failedSaveGeneration = -1;
        saveQueued = true;
        FlushQueuedSettingsSave(true);
    }

    internal void TickSettingsSaveForWindow()
    {
        TickQueuedSettingsSave();
    }

    private void TickQueuedSettingsSave()
    {
        if (saveQueued) FlushQueuedSettingsSave();
        else if (!saveQueued && saveState == SettingsSaveState.Saved && Time.realtimeSinceStartup >= saveStatusUntil) saveState = SettingsSaveState.Idle;
    }

    internal void PersistSettingsNow(bool applyRuntime = false)
    {
        if (Settings.IsPersistenceBlockedByMigrationFailure) return;
        if (writeInProgress || requestedSaveGeneration <= persistedSaveGeneration) return;
        SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);
        // Runtime flushing never queues persistence, so capture the write generation only after the forced boundary.
        long generation = requestedSaveGeneration;
        try
        {
            writeInProgress = true;
            saveState = SettingsSaveState.Saving;
            // Call the base serializer exactly once. Calling WriteSettings() here would recurse through this override.
            base.WriteSettings();
            ConfirmPersistedGeneration(generation);
            if (applyRuntime) Settings.ApplyToRuntime();
        }
        catch
        {
            // Do not advance persisted generation: the dirty generation remains retryable.
            failedSaveGeneration = generation;
            saveQueued = requestedSaveGeneration > persistedSaveGeneration;
            saveState = SettingsSaveState.Failed;
            saveStatusUntil = float.PositiveInfinity;
            if (failureNotifiedGeneration != generation)
            {
                failureNotifiedGeneration = generation;
                Messages.Message("US.Settings.Save.Failed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
        finally { writeInProgress = false; }
    }

    private void ConfirmPersistedGeneration(long generation)
    {
        PhysicalSaveCount++;
        persistedSaveGeneration = Math.Max(persistedSaveGeneration, generation);
        if (failedSaveGeneration <= generation) failedSaveGeneration = -1;
        if (closeRetryGeneration <= generation) closeRetryGeneration = -1;
        if (failureNotifiedGeneration <= generation) failureNotifiedGeneration = -1;
        saveQueued = requestedSaveGeneration > persistedSaveGeneration;
        if (saveQueued)
        {
            saveDueAt = Time.realtimeSinceStartup;
            saveState = SettingsSaveState.Saving;
        }
        else
        {
            saveState = SettingsSaveState.Saved;
            saveStatusUntil = Time.realtimeSinceStartup + 1.8f;
        }
    }
}

public partial class UniversalSqueakerSettings
{
    partial void PopulateBehaviorDirectory(ref BehaviorSourceDirectory directory) => directory = UsOneShotRuntime.BehaviorSources;
}

