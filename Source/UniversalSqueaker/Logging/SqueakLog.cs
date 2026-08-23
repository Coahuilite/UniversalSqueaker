using System;
using System.Reflection;

namespace UniversalSqueaker;

public enum SqueakDevLoggingMode { Auto = 0, Enabled = 1, Disabled = 2 }

/// <summary>Session settings-source fact (usdiag v2, 0.3.1): FreshCreated = no settings file was
/// successfully deserialized (missing or unreadable file, framework returned field defaults);
/// LoadedFromFile = disk deserialization completed through Scribe.</summary>
public enum SqueakSettingsOrigin { FreshCreated, LoadedFromFile }

/// <summary>Closed logging facade. Event schema, human text, and protocol emission are not writable by business code.</summary>
public static class SqueakLog
{
    private const string Prefix = "[UniversalSqueaker] ";
    private static SqueakDevLoggingMode mode;
    private static string build = "dev", buildId = "unknown";

    public static bool EffectiveDevLogging { get; private set; }
    public static bool ShouldEmitDev => EffectiveDevLogging;
    public static SqueakDevLoggingMode Mode => mode;

    public static void Configure(SqueakDevLoggingMode requested)
    {
        mode = Enum.IsDefined(typeof(SqueakDevLoggingMode), requested) ? requested : SqueakDevLoggingMode.Auto;
#if US_DEV
        EffectiveDevLogging = mode != SqueakDevLoggingMode.Disabled;
#else
        EffectiveDevLogging = mode == SqueakDevLoggingMode.Enabled;
#endif
        Assembly assembly = typeof(UniversalSqueakerMod).Assembly;
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
#if US_STEAM
        build = "steam";
        buildId = informational.Split('+')[0];
#elif US_GITHUB
        build = "github";
        buildId = informational;
#else
        build = "dev";
        buildId = informational;
#endif
    }

    public static void ResetSession() => SqueakLogOnce.Reset();
    public static void StartupIdentity() => Emit(SqueakLogEvent.ModStartIdentity, default, false);
    public static void StartupReady(int patchedMethods) => Emit(SqueakLogEvent.ModStartReady, new SqueakLogData(count: patchedMethods), false);
    public static void LoggingModeChanged(SqueakDevLoggingMode requested, bool enabled) => Emit(requested == SqueakDevLoggingMode.Enabled ? SqueakLogEvent.LoggingModeEnabled : requested == SqueakDevLoggingMode.Disabled ? SqueakLogEvent.LoggingModeDisabled : enabled ? SqueakLogEvent.LoggingModeAutoEnabled : SqueakLogEvent.LoggingModeAutoDisabled, new SqueakLogData(enabled: enabled), false);
    public static void SettingsOpenApiUnavailable() => Emit(SqueakLogEvent.SettingsOpenApiUnavailable, default, false);
    public static void SettingsOpenFailed(Exception ex) => Emit(SqueakLogEvent.SettingsOpenFailed, new SqueakLogData(exception: ex), true);
    public static void CatalogRefreshFailed(Exception ex) => Emit(SqueakLogEvent.CatalogRefreshFailed, new SqueakLogData(exception: ex), true);
    public static void PackRejected(string pack, int count, string reason = "duplicate_key") => Emit(SqueakLogEvent.PackRejected, new SqueakLogData(pack: pack, reason: reason, count: count), true);
    public static void LegacyVoicePackAdmitted(string pack, string race) => Emit(SqueakLogEvent.LegacyVoicePackAdmitted, new SqueakLogData(pack: pack, race: race), false);
    public static void ResolverRebuildFailed(Exception ex) => Emit(SqueakLogEvent.ResolverRebuildFailed, new SqueakLogData(exception: ex), true);
    public static void TargetRejected(string target, string reason) => Emit(SqueakLogEvent.TargetRejected, new SqueakLogData(target: target, reason: reason), true);
    public static void XenotypeDiscoveryUnavailable(string reason) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.XenotypeDiscoveryUnavailable, new SqueakLogData(reason: reason), true); }
    public static void XenotypeDiscoveryFailed(Exception ex) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.XenotypeDiscoveryFailed, new SqueakLogData(exception: ex), true); }
    public static void XenotypeDiscoveryCandidate(string target, string source, bool retained) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.XenotypeDiscoveryCandidate, new SqueakLogData(target: target, reason: source, source: source, enabled: retained), true); }
    public static void TriggerAttemptFailed(string action, Exception ex) => Emit(SqueakLogEvent.TriggerAttemptFailed, new SqueakLogData(action: action, exception: ex), true);
    public static void AudioNoSound(string action) => Emit(SqueakLogEvent.AudioNoSound, new SqueakLogData(action: action), true);
    public static void AudioDispatchFailed(string action, string sound, Exception ex) => Emit(SqueakLogEvent.AudioDispatchFailed, new SqueakLogData(action: action, sound: sound, exception: ex), true);
    public static void AudioDispatchOk(string action, string target, string sound, int suppressed, string? pawnName = null, string? pawnId = null) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.AudioDispatchOk, new SqueakLogData(action: action, target: target, sound: sound, suppressedDetail: suppressed, pawnName: pawnName, pawnId: pawnId), false); }
    public static void TriggerOutcomeSummary(int dispatched, int suppressed) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.TriggerOutcomeSummary, new SqueakLogData(dispatched: dispatched, suppressedDetail: suppressed), false); }
    public static void HookAttackUnavailable() => Emit(SqueakLogEvent.HookAttackUnavailable, default, true);
    public static void HookAttackTargetSkipped(string target, string reason) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.HookAttackTargetSkipped, new SqueakLogData(target: target, reason: reason), true); }
    public static void HookMentalBreakUnavailable() => Emit(SqueakLogEvent.HookMentalBreakUnavailable, default, true);
    public static void HookMentalFitUnavailable() => Emit(SqueakLogEvent.HookMentalFitUnavailable, default, true);
    public static void DiagnosticsHookUnavailable() { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.DiagnosticsHookUnavailable, default, true); }
    public static void DiagnosticsStartFailed() => Emit(SqueakLogEvent.DiagnosticsStartFailed, default, true);
    public static void OverlayChanged(bool enabled) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.OverlayChanged, new SqueakLogData(enabled: enabled), false); }
    public static void CameraChanged(bool enabled) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.CameraChanged, new SqueakLogData(enabled: enabled), false); }
    public static void WorkbenchOpenFailed(Exception ex) => Emit(SqueakLogEvent.WorkbenchOpenFailed, new SqueakLogData(exception: ex), true);
    public static void SettingsOrigin(SqueakSettingsOrigin origin) => Emit(SqueakLogEvent.SettingsOrigin, new SqueakLogData(settingsOrigin: origin), true);
    public static void AudioRouteSelected(string actionKey, string race, string? xenotype, string target, string sound, string tier, string? pack = null, bool isEgg = false, int suppressed = 0, string? pawnName = null, string? pawnId = null, bool? pawnControlled = null, string? pawnFaction = null) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.AudioRouteSelected, new SqueakLogData(action: actionKey, race: race, xenotype: xenotype, target: target, sound: sound, tier: tier, pack: pack, egg: isEgg, suppressedDetail: suppressed, pawnName: pawnName, pawnId: pawnId, pawnControlled: pawnControlled, pawnFaction: pawnFaction), false); }
    public static void FallbackProfileStoreFailed(string race, Exception ex) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.FallbackProfileStoreFailed, new SqueakLogData(race: race, exception: ex), true); }

    private static void Emit(SqueakLogEvent evt, SqueakLogData data, bool once)
    {
        try
        {
            SqueakLogDefinition definition = SqueakLogRegistry.Definition(evt, build, buildId);
            if (definition.Visibility == SqueakLogVisibility.DevOnly && !EffectiveDevLogging) return;
            if (once && !SqueakLogOnce.Claim(evt, data, definition.Version)) return;

            string text = Prefix + SqueakLogRegistry.HumanSentence(evt, definition, data) + (EffectiveDevLogging ? " || " + SqueakLogFormatter.Suffix(evt, definition, data, build, buildId) : "");
            SqueakLogSink.Emit(definition.Level, text);
        }
        catch
        {
        }
    }
}
