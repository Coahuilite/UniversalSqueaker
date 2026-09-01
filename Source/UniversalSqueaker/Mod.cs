using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

public class UniversalSqueakerMod : Mod
{
    public const string PackageId = "coahuilite.universalsqueaker";
    public static Harmony Harmony = null!;
    public static UniversalSqueakerSettings Settings = null!;
    public static UniversalSqueakerMod? Instance { get; private set; }
    private readonly HashSet<Window> settingsWindows = new();
    private readonly Dictionary<Type, FieldInfo?> optionsOwnerFields = new();
    private UniversalSqueaker.UI.UniversalSqueakerSettingsWindow? activeSettingsWindow;
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

#if US_STEAM
    private const string BuildFlavor = "steam";
#elif US_GITHUB
    private const string BuildFlavor = "github";
#else
    private const string BuildFlavor = "dev";
#endif

    public UniversalSqueakerMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Harmony = new Harmony(PackageId);
        Settings = GetSettings<UniversalSqueakerSettings>();
        // Loading may run on LongEvent's worker thread. PostLoadInit only records a pending migration;
        // this constructor must not consume it, read Unity Time, initialize resolver/UI state, or publish runtime.
        // SettingsOrigin (usdiag v2): LoadedFromFile = Scribe deserialization completed (ExposeData ran);
        // FreshCreated = no file or an unreadable file the framework replaced with a new defaults instance.
        SqueakLog.SettingsOrigin(Settings.SettingsLoadedFromFile ? SqueakSettingsOrigin.LoadedFromFile : SqueakSettingsOrigin.FreshCreated);
        SqueakLog.StartupIdentity();
        Harmony.PatchAll();

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            // ExecuteWhenFinished is the first Unity-main-thread operation in this startup path.
            // Bind before catalog/settings code can call any resolver mutator.
            SqueakRuntimeResolver.InitializeMainThread();
            // Catalog and resolver share the same published snapshot source.
            SqueakXenotypeCatalog.Refresh(Settings);
            // Route table: mount the default squeak comp on every race declared by an admitted pack,
            // replacing the canonical author patch. Reads the refreshed snapshot; runs on the main
            // thread (ExecuteWhenFinished) before any pawn is generated. Author patches still win.
            VoicePackCompAttach.Apply(SqueakXenotypeCatalog.Current);
            // Profile copies are independent Config artifacts; load/rebuild before the first resolver snapshot.
            // BuildBuiltIn consumes the resolved table and remains outside the ModSettings debounce/write path.
            SqueakFallbackProfileStore.LoadOrRebuild(SqueakKernelAdapter.BuildBuiltInSource());
            Settings.ApplyToRuntime();
            // The first and only startup consumption of a schema migration happens after main-thread binding.
            Settings.QueuePendingMigrationPersistence();
            // Schema migration may be the only change in a session; force its one queued generation out once startup is safe.
            FlushQueuedSettingsSave(true);
            Settings.ApplySettingsRuntimeSideEffects(false);
            SqueakAudioPoolNotificationService.EvaluateAndMaybeShow(Settings, SqueakXenotypeCatalog.Current);
            SqueakLog.StartupReady(Harmony.GetPatchedMethods().Count());
        });
    }

    internal static string BuildIdentity()
    {
        Assembly asm = typeof(UniversalSqueakerMod).Assembly;
        string informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";

#if US_DEV
        int plus = informational.IndexOf('+');
        if (plus >= 0 && plus + 1 < informational.Length)
        {
            string revision = informational[(plus + 1)..];
            if (revision.Length > 12)
            {
                revision = revision[..12];
            }

            return $"dev-{revision} ({informational})";
        }
#endif

        return informational;
    }

    public override string SettingsCategory() => SqueakLabels.SettingsCategory;

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.BeginSettingsSession();
        TickQueuedSettingsSave();
        Settings.DrawSettings(inRect);
    }

    public void OpenSettings(bool selectXenotypeTab = false)
    {
        if (Find.WindowStack == null) return;
        if (activeSettingsWindow != null)
        {
            Find.WindowStack.TryRemove(activeSettingsWindow, true);
            activeSettingsWindow = null;
        }

        try
        {
            var window = new UniversalSqueaker.UI.UniversalSqueakerSettingsWindow(this);
            activeSettingsWindow = window;
            RegisterSettingsWindow(window);
            if (selectXenotypeTab) Settings.RequestXenotypeTabOnNextDraw();
            Find.WindowStack.Add(window);
        }
        catch (Exception ex)
        {
            Settings.ClearXenotypeTabRequest();
            activeSettingsWindow = null;
            SqueakLog.SettingsOpenFailed(ex);
        }
    }

    internal void RegisterSettingsWindow(Window window)
    {
        settingsWindows.Add(window);
    }

    /// <summary>Framework entry point. Persistence always reaches the base implementation directly, never this override recursively.</summary>
    public override void WriteSettings()
    {
        if (Settings.IsPersistenceBlockedByMigrationFailure) return;
        SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);
        FlushQueuedSettingsSave(true);
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

    internal static void NotifySettingsWindowClosing(Window window)
    {
        if (Instance == null) return;
        if (!Instance.IsOwnedSettingsWindow(window)) return;
        Instance.settingsWindows.Remove(window);
        if (ReferenceEquals(Instance.activeSettingsWindow, window)) Instance.activeSettingsWindow = null;
        if (window is UniversalSqueaker.UI.UniversalSqueakerSettingsWindow custom && custom.UsesLegacySettingsSession)
        {
            Settings.EndSettingsSession();
        }
        Instance.FlushQueuedSettingsSave(true, true);
    }

    /// <summary>Accept only our registered dialog or a Dialog_Options instance whose stored Mod is this instance.</summary>
    internal bool IsOwnedSettingsWindow(Window window)
    {
        if (settingsWindows.Contains(window)) return true;
        try
        {
            Type type = window.GetType();
            if (!IsDialogOptionsType(type)) return false;
            if (!optionsOwnerFields.TryGetValue(type, out FieldInfo? ownerField))
            {
                ownerField = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(field => typeof(Mod).IsAssignableFrom(field.FieldType));
                optionsOwnerFields[type] = ownerField;
            }
            return ownerField?.GetValue(window) is Mod owner && ReferenceEquals(owner, this);
        }
        catch { return false; }
    }

    private static bool IsDialogOptionsType(Type type)
    {
        for (Type? current = type; current != null; current = current.BaseType)
            if (current.FullName == "Verse.Dialog_Options" || current.FullName == "RimWorld.Dialog_Options") return true;
        return false;
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
            PhysicalSaveCount++;
            persistedSaveGeneration = generation;
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
}
