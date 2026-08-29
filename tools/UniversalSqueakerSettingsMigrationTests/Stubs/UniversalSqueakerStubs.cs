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
        public static UniversalSqueakerMod Instance = null;

        public void QueueSettingsSave()
        {
        }
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
        }

        public static void NotifyContinuousResolverChange(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
        {
        }

        public static void FlushPendingRuntimeChanges(bool synchronous)
        {
        }
    }

    public static class Patch_DebugTabMenu_Actions
    {
        public static void SetEnabled(bool enabled)
        {
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
        public static int GlobalMinIntervalTicks = 216;

        public static void ApplyDistanceRange(Verse.FloatRange range)
        {
        }
    }
}
