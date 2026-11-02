using System;
using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>1.6 serialization/notification adapter for the single neutral save owner.</summary>
internal sealed class GameSettingsPersistence
{
    private readonly GameSettings settings;
    private readonly string folder, handle;
    private readonly Action frameworkWrite;
    private long notifiedFailure = -1;
    internal SettingsSaveOwner Owner { get; } = new();
    internal GameSettingsPersistence(GameSettings settings, string folder, string handle, Action frameworkWrite)
    { this.settings = settings; this.folder = folder; this.handle = handle; this.frameworkWrite = frameworkWrite; }

    internal void Queue() { if (!settings.IsConfigurationUnreadable) Owner.Edited(Time.realtimeSinceStartup); }
    internal void Tick()
    {
        RepairPublication();
        Flush();
        Owner.TickStatus(Time.realtimeSinceStartup);
    }
    internal void Flush(bool force = false, bool closing = false)
    {
        if (settings.IsConfigurationUnreadable) return;
        RepairPublication();
        Owner.Flush(Time.realtimeSinceStartup, frameworkWrite, force, closing);
        if (Owner.State == SettingsSaveState.Failed && notifiedFailure != Owner.Generation)
        {
            notifiedFailure = Owner.Generation;
            SqueakLog.SettingsSaveFailed(Owner.Generation, Owner.Failure);
            Messages.Message("US.Settings.Save.Failed".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
    internal void Retry() { Owner.Retry(); Flush(true); }
    private void RepairPublication()
    {
        if (Owner.PublicationPending && !Owner.RepairPublication(settings.ApplyToRuntime, out string failure))
            SqueakLog.PublicationRepairFailed(failure);
    }
    internal bool PersistBehaviorCandidate(string candidate)
    {
        if (settings.IsConfigurationUnreadable) return false;
        bool committed = Owner.CommitAtomic(Time.realtimeSinceStartup, () =>
        {
            string target = Path.Combine(GenFilePaths.ConfigFolderPath,
                GenText.SanitizeFilename("Mod_" + folder + "_" + handle + ".xml"));
            bool written = settings.TryWriteBehaviorCandidate(target, candidate, staging =>
            {
                try
                {
                    Scribe.saver.InitSaving(staging, "SettingsBlock");
                    var current = settings;
                    Scribe_Deep.Look(ref current, "ModSettings");
                }
                finally { Scribe.saver.FinalizeSaving(); }
            }, out string failure);
            if (!written) SqueakLog.BehaviorCandidateFailed(failure);
            return written;
        }, out string commitFailure);
        if (!committed) SqueakLog.BehaviorCandidateFailed(commitFailure);
        return committed;
    }
    internal bool TryCommitBehaviorSource(BehaviorDomain domain, string? source, out string diagnostic)
    {
        bool committed = settings.TryChangeBehaviorSource(domain, source, SqueakerRuntime.BehaviorSources, PersistBehaviorCandidate, out diagnostic);
        if (committed) Owner.PublishCommitted(settings.ApplyToRuntime, out diagnostic);
        return committed;
    }
    internal bool TryCommitBehaviorOverride(string family, string targetNamespace, string target, string child, string action,
        BehaviorValues values, out string diagnostic)
    {
        bool committed = settings.TryReplaceBehaviorOverride(family, targetNamespace, target, child, action, values,
            () => PersistBehaviorCandidate(settings.behaviorSourcesXml), out diagnostic);
        if (committed) Owner.PublishCommitted(settings.ApplyToRuntime, out diagnostic);
        return committed;
    }
}

public partial class GameSettings
{
    partial void PopulateBehaviorDirectory(ref BehaviorSourceDirectory directory) => directory = SqueakerRuntime.BehaviorSources;
}
