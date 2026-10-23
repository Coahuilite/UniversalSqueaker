#nullable disable
using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
namespace UniversalSqueaker
{
    public enum SqueakDevLoggingMode { Auto, Enabled, Disabled }
    public static class SqueakLog
    {
        public static bool EffectiveDevLogging { get; private set; }
        public static void Configure(SqueakDevLoggingMode mode) => EffectiveDevLogging = mode != SqueakDevLoggingMode.Disabled;
        public static void ResetSession() { }
        public static void LoggingModeChanged(SqueakDevLoggingMode mode, bool enabled) { }
        public static void FallbackProfileStoreFailed(string race, Exception ex) { }
    }
    // Only game lifecycle, native catalog/UI and diagnostic boundaries are doubled. The composition root,
    // settings, ExposeData, atomic file adapter and single save owner are linked production source.
    internal static class GameBootstrap
    {
        internal static bool PrerequisiteVerified => true;
        internal static void Start(Harmony harmony, GameSettings settings, GameSettingsPersistence persistence) { }
    }
    internal sealed class SettingsWindowOwner
    {
        internal SettingsWindowOwner(SqueakerMod mod, GameSettingsPersistence persistence) { }
        internal void DrawShell(UnityEngine.Rect rect) { }
        internal void OpenSettings() { }
        internal void RegisterSettingsWindow(Window window) { }
        internal bool IsOwnedSettingsWindow(Window window) => false;
        internal void NotifyClosing(Window window) { }
        internal static string BuildIdentity() => "controlled-settings-fixture";
    }
    public static class SqueakLabels { public static string SettingsCategory => "US"; }
    public static class SqueakerRuntime
    {
        public static BehaviorSourceDirectory BehaviorSources = new(Array.Empty<BehaviorSource>());
        internal static bool ImplementationAvailable(string key) => true;
    }
    public sealed class SqueakXenotypeCatalogSnapshot
    {
        public IReadOnlyList<string> RaceDefNames = Array.Empty<string>();
        public Dictionary<string, object> XenotypeByDefName = new();
        public IEnumerable<VoicePackInfo> GetVoicePackDomainPacks(SqueakVoicePackScope scope, string target) => Array.Empty<VoicePackInfo>();
    }
    public static class SqueakXenotypeCatalog
    {
        public static SqueakXenotypeCatalogSnapshot Current = new();
        public static void Refresh(GameSettings settings) { }
    }
    public static class ModsConfig { public static bool BiotechActive; }
    public static class GameRuntime
    {
        public static bool FailPublish;
        public static void PublishConfiguration(GameSettings settings)
        {
            if (FailPublish) throw new InvalidOperationException("injected native publication failure");
            settings.PublishBehaviorConfiguration();
        }
    }
    public static class SqueakKernelAdapter
    {
        public static Kernel.SelectionMode ToSelectionMode(SqueakVoicePackMode mode) => mode == SqueakVoicePackMode.Disabled
            ? Kernel.SelectionMode.Disabled : mode == SqueakVoicePackMode.Remix ? Kernel.SelectionMode.Remix
            : mode == SqueakVoicePackMode.Vanilla ? Kernel.SelectionMode.Off : Kernel.SelectionMode.Fallback;
    }
    public static class Patch_DebugTabMenu_Actions { public static void SetEnabled(bool enabled) { } }
    public static class SqueakDebug { public static bool ShowCameraIndicator; }
    public class CompSqueaker { public static void ApplyDistanceRange(FloatRange range) { } }
}
