using System;
using System.Reflection;

namespace UniversalSqueaker;

internal enum SqueakDiagnosticLogResult { NotCaptured, ModeOff, FormatFailed, SinkFailed, SinkReturned }

/// <summary>Session settings-source fact (usdiag v2, 0.3.1): FreshCreated = no settings file was
/// successfully deserialized (missing or unreadable file, framework returned field defaults);
/// LoadedFromFile = disk deserialization completed through Scribe.</summary>
public enum SqueakSettingsOrigin { FreshCreated, LoadedFromFile }

/// <summary>Closed logging facade. Event schema, human text, and protocol emission are not writable by business code.</summary>
public static class SqueakLog
{
    private const string Prefix = "[UniversalSqueaker.Kernel] ";
    private static SqueakDevLoggingMode mode;
    private static string build = "dev", buildId = "unknown";

    static SqueakLog() => Configure(SqueakDevLoggingMode.Auto);

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
        try
        {
            Assembly assembly = typeof(SqueakLog).Assembly;
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
        catch (Exception error) when (IsOrdinary(error)) { }
    }

    public static void ResetSession()
    {
        SqueakLogOnce.Reset();
        ReportedPopupTraces.Clear();
        layoutTraceBudget = 512;
    }
    public static void StartupIdentity() => Emit(SqueakLogEvent.ModStartIdentity, default, false);
    public static void StartupReady(int patchedMethods) => Emit(SqueakLogEvent.ModStartReady, new SqueakLogData(count: patchedMethods), false);
    public static void LoggingModeChanged(SqueakDevLoggingMode requested, bool enabled) => Emit(requested == SqueakDevLoggingMode.Enabled ? SqueakLogEvent.LoggingModeEnabled : requested == SqueakDevLoggingMode.Disabled ? SqueakLogEvent.LoggingModeDisabled : enabled ? SqueakLogEvent.LoggingModeAutoEnabled : SqueakLogEvent.LoggingModeAutoDisabled, new SqueakLogData(enabled: enabled), false);
    public static void SettingsOpenApiUnavailable() => Emit(SqueakLogEvent.SettingsOpenApiUnavailable, default, false);
    public static void SettingsOpenFailed(Exception ex) => Emit(SqueakLogEvent.SettingsOpenFailed, new SqueakLogData(exception: ex), true);
    public static void CatalogRefreshFailed(Exception ex) => Emit(SqueakLogEvent.CatalogRefreshFailed, new SqueakLogData(exception: ex), true);
    public static void PackRejected(string pack, int count, string reason = "duplicate_key") => Emit(SqueakLogEvent.PackRejected, new SqueakLogData(pack: pack, reason: reason, count: count), true);
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
    public static void DiagnosticsHookUnavailable() { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.DiagnosticsHookUnavailable, default, true); }
    public static void DiagnosticsStartFailed() => Emit(SqueakLogEvent.DiagnosticsStartFailed, default, true);

    /// <summary>
    /// The in-world diagnostics head mark could not be drawn. Once-semantics per exception type: the
    /// calling hook runs every frame, and an undeduplicated per-frame line is exactly the first in-game
    /// incident (8558 lines, 83.8% of the log). The bound - one line per exception type per session, no
    /// matter how many frames or pawns fail - is asserted by the log lane.
    /// </summary>
    public static void DiagnosticsMarkDrawFailed(Exception ex) => Emit(SqueakLogEvent.DiagnosticsMarkDrawFailed, new SqueakLogData(exception: ex), true);
    public static void OverlayChanged(bool enabled) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.OverlayChanged, new SqueakLogData(enabled: enabled), false); }
    public static void CameraChanged(bool enabled) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.CameraChanged, new SqueakLogData(enabled: enabled), false); }
    public static void WorkbenchOpenFailed(Exception ex) => Emit(SqueakLogEvent.WorkbenchOpenFailed, new SqueakLogData(exception: ex), true);
    public static void SettingsOrigin(SqueakSettingsOrigin origin) => Emit(SqueakLogEvent.SettingsOrigin, new SqueakLogData(settingsOrigin: origin), true);
    public static void AudioRouteSelected(string actionKey, string race, string? xenotype, string target, string sound, string tier, string? pack = null, bool isEgg = false, int suppressed = 0, string? pawnName = null, string? pawnId = null, bool? pawnControlled = null, string? pawnFaction = null) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.AudioRouteSelected, new SqueakLogData(action: actionKey, race: race, xenotype: xenotype, target: target, sound: sound, tier: tier, pack: pack, egg: isEgg, suppressedDetail: suppressed, pawnName: pawnName, pawnId: pawnId, pawnControlled: pawnControlled, pawnFaction: pawnFaction), false); }
    /// <summary>MVP: a dispatch that fell back to vanilla audio is a warning-level route record, not a normal
    /// info route. Every detected vanilla fallback emits one line while detailed logging is effective.</summary>
    public static void AudioVanillaFallback(string actionKey, string race, string? xenotype, string target, string sound, string tier = "vanilla", string? pack = null, bool isEgg = false, string? pawnName = null, string? pawnId = null, bool? pawnControlled = null, string? pawnFaction = null) { if (!ShouldEmitDev) return; Emit(SqueakLogEvent.AudioVanillaFallback, new SqueakLogData(action: actionKey, race: race, xenotype: xenotype, target: target, sound: sound, tier: tier, pack: pack, egg: isEgg, pawnName: pawnName, pawnId: pawnId, pawnControlled: pawnControlled, pawnFaction: pawnFaction), false); }
    public static void FallbackProfileStoreFailed(string race, Exception ex) => FallbackProfileStoreFailed(race, ex, "store");
    public static void FallbackProfileStoreFailed(string race, Exception ex, string stage)
        => Emit(SqueakLogEvent.FallbackProfileStoreFailed, new SqueakLogData(race: race, exception: ex, stage: stage), true);
    public static void AudioDisabled(string action) => Emit(SqueakLogEvent.AudioDisabled, new SqueakLogData(action: action), true);
    public static void CompAutoAttached(string race) => Emit(SqueakLogEvent.VoicePackCompAutoAttached, new SqueakLogData(race: race), false);
    public static void CompAttachSkipped(string race, string reason) => Emit(SqueakLogEvent.VoicePackCompAttachSkipped, new SqueakLogData(race: race, reason: reason), false);
    public static void CompAttachFailed(Exception ex) => Emit(SqueakLogEvent.VoicePackCompAttachFailed, new SqueakLogData(exception: ex), true);
    /// <summary>UI text-fit audit finding. The audit itself deduplicates by element+text, so this is
    /// emitted once per distinct defect rather than once per frame.</summary>
    public static void LabelOverflow(string elementPath, string axis, string font, float needed, float available)
    {
        if (!ShouldEmitDev) return;
        Emit(SqueakLogEvent.LabelOverflow, new SqueakLogData(target: elementPath, reason: axis, source: font, needed: needed, available: available), false);
    }

    /// <summary>Forensic click-routing trace from the library's neutral Trace hook. Deliberately
    /// out-of-protocol: it exists to answer "who ate this click" in a running game, where no stub
    /// harness can reproduce native IMGUI event order.</summary>
    /// <summary>The library's publisher fires ptrace once per FRAME while a popup is open, which made one
    /// popup session 1,6k log lines in the 2026-09-14 run - noise, not evidence. Identical messages are
    /// therefore printed once per session: the whole message is the key, so a moved rect or a changed owner
    /// still prints until the bounded session window fills; reset reopens that window.
    /// The 512th distinct line marks exhaustion.</summary>
    private static readonly System.Collections.Generic.HashSet<string> ReportedPopupTraces
        = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

    private static int layoutTraceBudget = 512;

    public static void PopupTrace(string message)
    {
        if (!ShouldEmitDev) return;
        try
        {
            if (string.IsNullOrEmpty(message)) return;
            int pointerAt = message.IndexOf(" pointerLocal=", StringComparison.Ordinal);
            string key = pointerAt > 0 ? message.Substring(0, pointerAt) : message;
            if (ReportedPopupTraces.Contains(key) || ReportedPopupTraces.Count >= 512) return;
            ReportedPopupTraces.Add(key);
            SqueakLogSink.Emit(SqueakLogLevel.Info, Prefix + "ptrace: " + message
                + (ReportedPopupTraces.Count == 512 ? " traceBudget=exhausted" : ""));
        }
        catch (Exception error) when (IsOrdinary(error)) { }
    }

    /// <summary>Named forensic channel; formatting executes inside the protected mode gate.</summary>
    public static void LayoutTrace(Func<string> message)
    {
        if (!ShouldEmitDev || layoutTraceBudget <= 0) return;
        try
        {
            string text = message();
            if (string.IsNullOrEmpty(text)) return;
            layoutTraceBudget--;
            SqueakLogSink.Emit(SqueakLogLevel.Info, Prefix + "ltrace: " + text
                + (layoutTraceBudget == 0 ? " traceBudget=exhausted" : ""));
        }
        catch (Exception error) when (IsOrdinary(error)) { }
    }
    public static void LayoutTrace(string message) => LayoutTrace(() => message);

    // Only the settings window's explicit, bounded cost observation calls this. It must remain
    // usable with detail/fit off and after the ordinary trace budget was exhausted.
    internal static void SettingsCostSample(Func<string> message)
    {
        try { SqueakLogSink.Emit(SqueakLogLevel.Info, Prefix + "ltrace: " + message()); }
        catch (Exception error) when (IsOrdinary(error)) { }
    }

    // Production callers choose a named result, never raw text, level or an arbitrary event ID.
    public static void DiagnosticPublicationFailed(string subject, Exception error) => Production(SqueakProductionEvent.DiagnosticPublicationFailed, new(target: subject, exception: error));
    public static void SustainedEndUncertain(string subject, string reason) => Production(SqueakProductionEvent.SustainedEndUncertain, new(target: subject, reason: reason));
    public static void AuthoringRejected(string issue)
        => Production(SqueakProductionEvent.AuthoringRejected, new(reason: "UnstructuredIssue", detail: issue));
    public static void AuthoringRejected(string? issue, string target, string reason, string? source = null, string? action = null)
        => Production(SqueakProductionEvent.AuthoringRejected,
            new(target: target, reason: reason, source: source, action: action, detail: issue));
    // Only captured owner identities and fixed reason codes may bypass free-text cleaning.
    internal static void AuthoringRejectedKnown(string? issue, string target, string reason, string? source = null, string? action = null, string? claim = null)
        => Production(SqueakProductionEvent.AuthoringRejected,
            new(target: target, reason: reason, source: source, action: action, detail: issue, authoringFieldsKnown: true, authoringClaim: claim));
    public static void AudioObservationFailed(string stage, Exception error) => Production(SqueakProductionEvent.AudioObservationFailed, new(source: stage, exception: error));
    public static void AudioBindingRejected(string sound, string reason) => Production(SqueakProductionEvent.AudioBindingRejected, new(sound: sound, reason: reason));
    public static void OutputVolumeInvalid() => Production(SqueakProductionEvent.OutputVolumeInvalid, default);
    public static void CapabilityUnavailable(string capability, string fact, string reason) => Production(SqueakProductionEvent.CapabilityUnavailable, new(source: capability, target: fact, reason: reason));
    public static void PrerequisiteUnverified(string reason) => Production(SqueakProductionEvent.PrerequisiteUnverified, new(reason: reason));
    public static void BehaviorRefreshPaused(string reason) => Production(SqueakProductionEvent.BehaviorRefreshPaused, new(reason: reason));
    public static void SettingsSaveFailed(long generation, string reason) => Production(SqueakProductionEvent.SettingsSaveFailed, new(target: generation.ToString(System.Globalization.CultureInfo.InvariantCulture), reason: reason));
    public static void PublicationRepairFailed(string reason) => Production(SqueakProductionEvent.PublicationRepairFailed, new(reason: reason));
    public static void BehaviorCandidateFailed(string reason) => Production(SqueakProductionEvent.BehaviorCandidateFailed, new(reason: reason));
    public static void ConfigurationUnsupported(int format) => Production(SqueakProductionEvent.ConfigurationUnsupported, new(count: format));
    public static void PresetImportRejected(string preset, Exception error) => Production(SqueakProductionEvent.PresetImportRejected, new(target: preset, exception: error));
    public static void DiagnosticsUiFailed(string stage, Exception error, string? subject = null) => Production(SqueakProductionEvent.DiagnosticsUiFailed, new(source: stage, target: subject, exception: error));
    public static void NativeEntryUnavailable(string stage) => Production(SqueakProductionEvent.NativeEntryUnavailable, new(source: stage));
    public static void OverlayFallback(string stage, Exception error) => Production(SqueakProductionEvent.OverlayFallback, new(source: stage, exception: error));
    public static void SaveStatusUnknown(string token) => Production(SqueakProductionEvent.SaveStatusUnknown, new(target: token));
    public static void ThemeFallback(string resource, Exception error) => Production(SqueakProductionEvent.ThemeFallback, new(target: resource, exception: error));
    public static void CanonicalIdentityFailed(Exception error) => Production(SqueakProductionEvent.CanonicalIdentityFailed, new(exception: error));
    public static void FallbackAudioPublishFailed(string race, Exception error) => Production(SqueakProductionEvent.FallbackAudioPublishFailed, new(race: race, exception: error));

    public static void KernelResponse(Func<SqueakResponseLogData> capture, bool exhausted)
        => TryKernelResponse(capture, exhausted);

    internal static SqueakDiagnosticLogResult TryKernelResponse(Func<SqueakResponseLogData> capture, bool exhausted)
    {
        if (!ShouldEmitDev) return SqueakDiagnosticLogResult.ModeOff;
        string text;
        try { text = Prefix + SqueakProductionProtocol.Response(capture(), build, buildId, exhausted); }
        catch (Exception error) when (IsOrdinary(error)) { return SqueakDiagnosticLogResult.FormatFailed; }
        try { SqueakLogSink.Emit(SqueakLogLevel.Info, text); return SqueakDiagnosticLogResult.SinkReturned; }
        catch (Exception error) when (IsOrdinary(error)) { return SqueakDiagnosticLogResult.SinkFailed; }
    }

    private static void Production(SqueakProductionEvent evt, SqueakLogData data)
    {
        try
        {
            // A bounded once domain suppresses repeated persistent failures, not their first report.
            if (!SqueakProductionProtocol.Claim(evt, data)) return;
            SqueakLogSink.Emit(SqueakProductionProtocol.Level(evt), Prefix + SqueakProductionProtocol.Failure(evt, data, build, buildId));
        }
        catch (Exception error) when (IsOrdinary(error)) { }
    }

    internal static bool IsOrdinary(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException
        && error is not System.Threading.ThreadAbortException;

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
        catch (Exception error) when (IsOrdinary(error)) { }
    }
}
