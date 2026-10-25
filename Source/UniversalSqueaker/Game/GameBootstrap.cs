using System;
using System.Linq;
using HarmonyLib;
using Verse;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker;

/// <summary>Ordered 1.6 startup, ending at native audio readiness and a single configuration publication.</summary>
internal static class GameBootstrap
{
    internal static bool PrerequisiteVerified { get; private set; } = true;
    internal static void Start(Harmony Harmony, GameSettings Settings, GameSettingsPersistence persistence)
    {
        // Prerequisite contract check. RimWorld's modDependencies cannot carry a version (ModRequirement
        // parses only packageId / alternativePackageIds / displayName), so this is the one place where the
        // API range US was compiled against meets the API range that actually loaded. It reports and keeps
        // going: throwing from a Mod constructor buries the diagnosis behind the game's generic
        // "mod failed to load" and takes the Harmony patches down with it.
        PrerequisiteVerified = FerriteLibVersion.Require(new Version(0, 8, 0), new Version(0, 9, 0), SqueakerMod.PackageId, out string prerequisiteReport);
        if (!PrerequisiteVerified)
        {
            Log.Error("UniversalSqueaker cannot verify its FerriteLib prerequisite. " + prerequisiteReport);
        }

        // Loading may run on LongEvent's worker thread. PostLoadInit validates/captures the new settings format;
        // this constructor must not consume it, read Unity Time, initialize resolver/UI state, or publish runtime.
        // SettingsOrigin (usdiag v2): LoadedFromFile = Scribe deserialization completed (ExposeData ran);
        // FreshCreated = framework-created settings; unsupported new-format markers remain explicitly unreadable.
        SqueakLog.SettingsOrigin(Settings.SettingsLoadedFromFile ? SqueakSettingsOrigin.LoadedFromFile : SqueakSettingsOrigin.FreshCreated);
        SqueakLog.StartupIdentity();
        Harmony.PatchAll();
        UsInstanceOutput.Initialize(Harmony);

        // The first callback appends initialization after native ResolveReferences audio callbacks.
        // Executing LongEventHandler consumes its growing queue; no time delay or polling is involved.
        UsAudioBindings.InitializeAfterNativeAudioResolution(() =>
        {
            // ExecuteWhenFinished is the first Unity-main-thread operation in this startup path.
            // Bind before catalog/settings code can call any resolver mutator.
            GameRuntime.InitializeMainThread();
            // OVT-001 (WO-US-OVERVIEW-THEMES-20261015): the BROWSE definition directory is collected
            // and published ONCE here - the definition-ready main-thread entry - never at window
            // creation or first GUI pass. It is independent of the sound catalog below: it answers
            // "what did the game load" and feeds NO admission, NO comp mount, NO routing authority.
            RaceDefinitionCatalog.CollectAndPublish();
            UsAudioBindings.Publish();
            SqueakerRuntime.Initialize();
            if (!Settings.TryRefreshBehaviorSources(SqueakerRuntime.BehaviorSources, persistence.PersistBehaviorCandidate, out string sourceFailure))
                Log.Warning("usdiag behavior snapshot refresh paused: " + sourceFailure);
            // Catalog and resolver share the same published snapshot source.
            SqueakXenotypeCatalog.Refresh(Settings);
            // VF1 order: the final-table store loads BEFORE the mount decision, because the support
            // set is the union of pack-declared races and table races (maintainer or player). The
            // store rebuilds from the immutable data-driven source; copies are independent Config
            // artifacts outside the ModSettings debounce/write path.
            SqueakFallbackProfileStore.LoadOrRebuild(SqueakKernelAdapter.BuildBuiltInSource());
            SqueakerRuntime.RefreshAudio();
            // Route table: mount the default squeak comp on the support set, replacing the canonical
            // author patch. Reads the refreshed snapshot and the loaded tables; runs on the main
            // thread (ExecuteWhenFinished) before any pawn is generated. Author patches still win.
            VoicePackCompAttach.Apply(
                SqueakXenotypeCatalog.Current,
                SqueakFallbackProfileStore.SupportedRaceDefNames().Concat(SqueakerRuntime.DeclaredRaces));
            Settings.ApplyToRuntime();
            // Definitions, final native audio, comp admission and the configuration snapshot are ready.
            // Ordinary activation is process setup, never a side effect of selecting a pack or opening UI.
            SqueakerRuntime.InstallOrdinaryPolicy(UsResponsePolicy.CreateOrdinary());
            // Any new-format repair queued during loading is flushed only after main-thread binding.
            persistence.Flush(true);
            Settings.ApplySettingsRuntimeSideEffects(false);
            SqueakAudioPoolNotificationService.EvaluateAndMaybeShow(Settings, SqueakXenotypeCatalog.Current);
            SqueakLog.StartupReady(Harmony.GetPatchedMethods().Count());
        });

    }
}
