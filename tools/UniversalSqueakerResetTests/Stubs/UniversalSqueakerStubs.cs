#nullable disable
using System;
using System.Collections.Generic;

namespace UniversalSqueaker
{
    public enum SqueakDevLoggingMode
    {
        Auto = 0,
        Enabled = 1,
        Disabled = 2,
    }

    /// <summary>Characterization stub for the log surface consumed by Settings/Fallback store.</summary>
    public static class SqueakLog
    {
        public static bool EffectiveDevLogging { get; private set; }

        public static void Configure(SqueakDevLoggingMode mode)
        {
            EffectiveDevLogging = mode != SqueakDevLoggingMode.Disabled;
        }

        public static void ResetSession()
        {
        }

        public static void LoggingModeChanged(SqueakDevLoggingMode requested, bool enabled)
        {
            UniversalSqueaker.Probe.LoggingModeChangedCount++;
        }

        public static void TargetRejected(string target, string reason)
        {
        }

        public static void FallbackProfileStoreFailed(string race, Exception ex)
        {
        }
    }

    /// <summary>Characterization stub: the store reads the permanent packageId from the mod class.</summary>
    public class UniversalSqueakerMod
    {
        public const string PackageId = "coahuilite.universalsqueaker";
        // The harness installs an instance so QueuePersistence() reaches the counting stub
        // (production code path is identical: UniversalSqueakerMod.Instance?.QueueSettingsSave()).
        public static UniversalSqueakerMod Instance = new UniversalSqueakerMod();

        public void QueueSettingsSave() => UniversalSqueaker.Probe.SaveQueuedCount++;
    }

    /// <summary>
    /// SqueakActionDefinitions lives in the production Runtime adapter but is not part of this harness's
    /// link set; this stub supplies the two members the linked Models/Settings files call.
    /// </summary>
    public static class SqueakActionDefinitions
    {
        public const int Count = 17;

        public static bool IsKnown(SqueakAction action) => (uint)action < Count;

        public static SqueakActionScope NormalizeScope(SqueakAction action, SqueakActionScope scope)
        {
            return scope;
        }
    }

    namespace UI
    {
        public static class VoicePacksPage
        {
            public static void BeginSession()
            {
            }

            public static void EndSession()
            {
            }

            public static void Draw(UnityEngine.Rect inRect)
            {
            }
        }
    }

    public class SqueakXenotypeCatalogSnapshot
    {
        public IReadOnlyList<string> RaceDefNames { get; } = new List<string>();
        public Dictionary<string, SqueakVoicePackDef> XenotypeByDefName { get; } = new Dictionary<string, SqueakVoicePackDef>(StringComparer.Ordinal);

        public IEnumerable<SqueakVoicePackDef> GetVoicePackDomainPacks(SqueakVoicePackScope scope, string targetDefName)
        {
            yield break;
        }
    }

    public static class SqueakXenotypeCatalog
    {
        public static SqueakXenotypeCatalogSnapshot Current { get; } = new SqueakXenotypeCatalogSnapshot();

        public static void Refresh(UniversalSqueakerSettings settings)
        {
        }
    }

    public static class ModsConfig
    {
        public static bool BiotechActive { get; set; }
    }

    public static class SqueakRuntimeResolver
    {
        public static void NotifyDiscreteResolverChange(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
        {
            UniversalSqueaker.Probe.DiscreteNotifyCount++;
        }

        public static void NotifyContinuousResolverChange(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
        {
            UniversalSqueaker.Probe.ContinuousNotifyCount++;
        }

        public static void FlushPendingRuntimeChanges(bool synchronous)
        {
            UniversalSqueaker.Probe.FlushCount++;
        }
    }

    public static class Patch_DebugTabMenu_Actions
    {
        public static void SetEnabled(bool enabled)
        {
            UniversalSqueaker.Probe.DebugMenuSetEnabledCount++;
        }
    }

    public static class SqueakDebug
    {
        public static bool ShowCameraIndicator;
    }

    public class CompSqueaker
    {
        public static bool ScaleCooldownWithTimeSpeed;
        public static bool ScaleFrequencyWithTalking;
        public static bool ScalePeriodicWithAudiblePopulation;
        public static float GlobalCooldownMultiplier = 1f;
        public static float GlobalVolumeFactor = 1f;
        public static int GlobalMinIntervalTicks = 216;
        // Recording mirror: the harness observes cheap statics directly; this list exists for future probes.
        public static readonly System.Collections.Generic.List<string> CheapPublishLog = new();
        // Mirror of the production cheap runtime statics the settings layer publishes (Pure/SqueakEatOccurrence
        // is part of this harness's compile set, so the defaults stay the single source).
        public static bool EatPrecisionEnabled = SqueakEatOccurrence.EatPrecisionDefault;
        public static bool EatPrecisionIncludeDrugs = SqueakEatOccurrence.EatPrecisionIncludeDrugsDefault;

        public static void ApplyDistanceRange(Verse.FloatRange range)
        {
            UniversalSqueaker.Probe.DistanceApplyCount++;
            UniversalSqueaker.Probe.LastDistanceRange = range;
        }
    }

    /// <summary>RESET1 probe: every publish/persistence channel the settings funnel uses is
    /// counted here so the harness can assert the Applied boundary (exactly one persistence
    /// request per applied restore) and the NoChange/Rejected boundary (zero side effects).</summary>
    public static class Probe
    {
        public static int SaveQueuedCount;
        public static int DiscreteNotifyCount;
        public static int ContinuousNotifyCount;
        public static int FlushCount;
        public static int DistanceApplyCount;
        public static int DebugMenuSetEnabledCount;
        public static int LoggingModeChangedCount;
        public static Verse.FloatRange LastDistanceRange;

        public static void Reset()
        {
            SaveQueuedCount = 0;
            DiscreteNotifyCount = 0;
            ContinuousNotifyCount = 0;
            FlushCount = 0;
            DistanceApplyCount = 0;
            DebugMenuSetEnabledCount = 0;
            LoggingModeChangedCount = 0;
            LastDistanceRange = default;
            CompSqueaker.CheapPublishLog.Clear();
        }

        public static string Snapshot()
        {
            return "save=" + SaveQueuedCount + " discrete=" + DiscreteNotifyCount + " continuous=" + ContinuousNotifyCount
                + " flush=" + FlushCount + " distanceApply=" + DistanceApplyCount + " debugMenu=" + DebugMenuSetEnabledCount
                + " loggingChanged=" + LoggingModeChangedCount;
        }
    }
}
