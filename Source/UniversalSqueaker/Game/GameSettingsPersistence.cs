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
            Log.Warning("usdiag settings save failed: " + Owner.Failure);
            Messages.Message("US.Settings.Save.Failed".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
    internal void Retry() { Owner.Retry(); Flush(true); }
    private void RepairPublication()
    {
        if (Owner.PublicationPending && !Owner.RepairPublication(settings.ApplyToRuntime, out string failure))
            Log.Warning("usdiag settings publication repair " + failure);
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
            if (!written) Log.Warning("usdiag behavior candidate save failed: " + failure);
            return written;
        }, out string commitFailure);
        if (!committed) Log.Warning("usdiag behavior candidate " + commitFailure);
        return committed;
    }
    internal bool ChangeBehaviorSource(BehaviorDomain domain, string? source, out string failure)
    {
        bool committed = settings.TryChangeBehaviorSource(domain, source, SqueakerRuntime.BehaviorSources, PersistBehaviorCandidate, out failure);
        if (committed) Owner.PublishCommitted(settings.ApplyToRuntime, out failure);
        return committed;
    }
    internal bool ChangeBehaviorOverride(string family, string targetNamespace, string target, string child, string action,
        BehaviorValues values, out string failure)
    {
        bool committed = settings.TryChangeBehaviorOverride(family, targetNamespace, target, child, action, values,
            () => PersistBehaviorCandidate(settings.behaviorSourcesXml), out failure);
        if (committed) Owner.PublishCommitted(settings.ApplyToRuntime, out failure);
        return committed;
    }
}

public partial class GameSettings
{
    partial void PopulateBehaviorDirectory(ref BehaviorSourceDirectory directory) => directory = SqueakerRuntime.BehaviorSources;
}
