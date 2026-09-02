using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace UniversalSqueaker;

internal enum SqueakLogVisibility { Daily, DevOnly }
internal enum SqueakLogLevel { Info, Warning, Error }
internal enum SqueakLogEvent { ModStartIdentity, ModStartReady, LoggingModeEnabled, LoggingModeDisabled, LoggingModeAutoEnabled, LoggingModeAutoDisabled, SettingsOpenApiUnavailable, SettingsOpenFailed, CatalogRefreshFailed, PackRejected, ResolverRebuildFailed, TargetRejected, XenotypeDiscoveryUnavailable, XenotypeDiscoveryFailed, XenotypeDiscoveryCandidate, TriggerAttemptFailed, AudioNoSound, AudioDispatchFailed, AudioDispatchOk, TriggerOutcomeSummary, HookAttackUnavailable, HookAttackTargetSkipped, HookMentalBreakUnavailable, HookMentalFitUnavailable, DiagnosticsHookUnavailable, DiagnosticsStartFailed, OverlayChanged, CameraChanged, WorkbenchOpenFailed, SettingsOrigin, AudioRouteSelected, AudioVanillaFallback, FallbackProfileStoreFailed, AudioDisabled, VoicePackCompAutoAttached, VoicePackCompAttachSkipped, VoicePackCompAttachFailed, LabelOverflow }

internal readonly struct SqueakLogData
{
    internal readonly string? Action, Target, Pack, Reason, Sound, Source, PawnName, PawnId, PawnFaction; internal readonly int? Count, Dispatched, SuppressedDetail; internal readonly bool? Enabled, Egg, PawnControlled; internal readonly Exception? Exception;
    // v2-only identity/route facts (0.3.1 wave 2c). Race/Xenotype carry exact DefNames; Tier carries the
    // protocol tier vocabulary; SettingsOrigin carries the session settings-source fact.
    internal readonly string? Race, Xenotype, Tier; internal readonly SqueakSettingsOrigin? SettingsOrigin;
    // UI text-fit audit facts: measured need vs the rect actually offered. Pixels carry one decimal so a
    // reviewer can tell a 1px rounding disagreement from a string that is twice as wide as its box.
    internal readonly float? Needed, Available;
    internal SqueakLogData(string? action = null, string? target = null, string? pack = null, string? reason = null, string? sound = null, string? source = null, int? count = null, int? dispatched = null, int? suppressedDetail = null, bool? enabled = null, Exception? exception = null, string? pawnName = null, string? pawnId = null, string? race = null, string? xenotype = null, string? tier = null, SqueakSettingsOrigin? settingsOrigin = null, bool? egg = null, bool? pawnControlled = null, string? pawnFaction = null, float? needed = null, float? available = null)
    { Action = action; Target = target; Pack = pack; Reason = reason; Sound = sound; Source = source; Count = count; Dispatched = dispatched; SuppressedDetail = suppressedDetail; Enabled = enabled; Exception = exception; PawnName = pawnName; PawnId = pawnId; Race = race; Xenotype = xenotype; Tier = tier; SettingsOrigin = settingsOrigin; Egg = egg; PawnControlled = pawnControlled; PawnFaction = pawnFaction; Needed = needed; Available = available; }
}

internal readonly struct SqueakLogDefinition
{
    internal readonly SqueakLogVisibility Visibility;
    internal readonly SqueakLogLevel Level;
    internal readonly string Human;
    // Protocol version of the record: 1 = the locked 28-event fmt=1 surface (bytes immutable);
    // 2 = fmt=2 extension records carrying v2-only facts (settings origin, race/xenotype, tier).
    internal readonly int Version;

    internal SqueakLogDefinition(SqueakLogVisibility visibility, SqueakLogLevel level, string human, int version = 1)
    {
        Visibility = visibility;
        Level = level;
        Human = human;
        Version = version;
    }
}

internal static class SqueakLogRegistry
{
    internal static SqueakLogDefinition Definition(SqueakLogEvent e, string build, string buildId) => e switch
    {
        SqueakLogEvent.ModStartIdentity => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Universal Squeaker started with " + build + " build " + buildId + "."),
        SqueakLogEvent.ModStartReady => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Universal Squeaker startup completed."),
        SqueakLogEvent.LoggingModeEnabled => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Detailed diagnostic logging is enabled."),
        SqueakLogEvent.LoggingModeDisabled => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Detailed diagnostic logging is disabled."),
        SqueakLogEvent.LoggingModeAutoEnabled => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Detailed diagnostic logging is enabled by Auto mode."),
        SqueakLogEvent.LoggingModeAutoDisabled => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Detailed diagnostic logging is disabled by Auto mode."),
        SqueakLogEvent.SettingsOpenApiUnavailable => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "Mod Settings API is unavailable."),
        SqueakLogEvent.SettingsOpenFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "Mod Settings could not be opened."),
        SqueakLogEvent.CatalogRefreshFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Error, "VoicePack catalog refresh failed."),
        SqueakLogEvent.PackRejected => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "A VoicePack was rejected."),
        SqueakLogEvent.ResolverRebuildFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Error, "VoicePack resolver rebuild failed."),
        SqueakLogEvent.TargetRejected => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "A Xenotype VoicePack target was rejected."),
        SqueakLogEvent.XenotypeDiscoveryUnavailable => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Warning, "Xenotype discovery is unavailable."),
        SqueakLogEvent.XenotypeDiscoveryFailed => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Warning, "Xenotype display discovery failed."),
        SqueakLogEvent.XenotypeDiscoveryCandidate => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Info, "A HAR Xenotype discovery candidate was evaluated."),
        SqueakLogEvent.TriggerAttemptFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Error, "Squeak trigger attempt failed."),
        SqueakLogEvent.AudioNoSound => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "No fallback SoundDef was found."),
        SqueakLogEvent.AudioDispatchFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Error, "Squeak audio dispatch failed."),
        SqueakLogEvent.AudioDispatchOk => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Info, "Squeak audio dispatched."),
        SqueakLogEvent.TriggerOutcomeSummary => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Info, "Squeak trigger outcome summary was recorded."),
        SqueakLogEvent.HookAttackUnavailable => new(SqueakLogVisibility.Daily, SqueakLogLevel.Error, "Attack squeak hook is unavailable."),
        SqueakLogEvent.HookAttackTargetSkipped => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Warning, "An Attack hook target was skipped."),
        SqueakLogEvent.HookMentalBreakUnavailable => new(SqueakLogVisibility.Daily, SqueakLogLevel.Error, "Mental-break squeak hook is unavailable."),
        SqueakLogEvent.HookMentalFitUnavailable => new(SqueakLogVisibility.Daily, SqueakLogLevel.Error, "Baby-fits squeak hook is unavailable.", 2),
        SqueakLogEvent.DiagnosticsHookUnavailable => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Warning, "Diagnostics overlay hook is unavailable."),
        SqueakLogEvent.DiagnosticsStartFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "Diagnostics overlay could not start."),
        SqueakLogEvent.OverlayChanged => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Info, "Diagnostics overlay state changed."),
        SqueakLogEvent.CameraChanged => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Info, "Camera indicator state changed."),
        SqueakLogEvent.WorkbenchOpenFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "Animal Voice Workbench could not be opened."),
        SqueakLogEvent.SettingsOrigin => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Mod settings origin was recorded.", 2),
        SqueakLogEvent.AudioRouteSelected => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Info, "Audio route: <action> -> <sound> (<tier>[, egg]).", 2),
        SqueakLogEvent.AudioVanillaFallback => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Warning, "Audio dispatch fell back to vanilla: <action> -> <sound> (<tier>[, egg]).", 2),
        SqueakLogEvent.FallbackProfileStoreFailed => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Warning, "Fallback profile store operation failed.", 2),
        SqueakLogEvent.AudioDisabled => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "Squeak audio is disabled (true bypass): <action> not intercepted.", 2),
        SqueakLogEvent.VoicePackCompAutoAttached => new(SqueakLogVisibility.Daily, SqueakLogLevel.Info, "VoicePack comp auto-attached to race <race>.", 2),
        SqueakLogEvent.VoicePackCompAttachSkipped => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "VoicePack comp auto-attach skipped for race <race> (<reason>).", 2),
        SqueakLogEvent.VoicePackCompAttachFailed => new(SqueakLogVisibility.Daily, SqueakLogLevel.Warning, "VoicePack comp auto-attach failed.", 2),
        SqueakLogEvent.LabelOverflow => new(SqueakLogVisibility.DevOnly, SqueakLogLevel.Warning, "A settings label does not fit its rect: <target> (<reason>).", 2),
        _ => throw new ArgumentOutOfRangeException(nameof(e))
    };

    /// <summary>Human sentence for a record. settings.origin's sentence is parameterized by the closed
    /// two-value origin set (precedent: mod.start.identity parameterizes by runtime build/build_id).
    /// 0.3.2: audio.route.selected reads as a one-line dispatch summary for humans (action -> sound (tier) [egg]).</summary>
    internal static string HumanSentence(SqueakLogEvent e, SqueakLogDefinition definition, SqueakLogData data)
    {
        if (e == SqueakLogEvent.SettingsOrigin)
            return "Mod settings origin: " + (data.SettingsOrigin == SqueakSettingsOrigin.LoadedFromFile ? "LoadedFromFile" : "FreshCreated") + ".";
        if (e == SqueakLogEvent.AudioRouteSelected)
        {
            string actionText = string.IsNullOrEmpty(data.Action) ? "-" : data.Action!;
            string soundText = string.IsNullOrEmpty(data.Sound) ? "-" : data.Sound!;
            string tierText = string.IsNullOrEmpty(data.Tier) ? "-" : data.Tier!;
            string marker = (data.Egg == true ? ", egg" : "") + (data.PawnControlled == false ? ", nonplayer" : "");
            return "Audio route: " + actionText + " -> " + soundText + " (" + tierText + marker + ").";
        }
        if (e == SqueakLogEvent.AudioVanillaFallback)
        {
            string actionText = string.IsNullOrEmpty(data.Action) ? "-" : data.Action!;
            string soundText = string.IsNullOrEmpty(data.Sound) ? "-" : data.Sound!;
            string tierText = string.IsNullOrEmpty(data.Tier) ? "-" : data.Tier!;
            string marker = (data.Egg == true ? ", egg" : "") + (data.PawnControlled == false ? ", nonplayer" : "");
            return "Audio dispatch fell back to vanilla: " + actionText + " -> " + soundText + " (" + tierText + marker + ").";
        }
        if (e == SqueakLogEvent.AudioDisabled)
        {
            string actionText = string.IsNullOrEmpty(data.Action) ? "-" : data.Action!;
            return "Squeak audio is disabled (true bypass): " + actionText + " not intercepted.";
        }
        if (e == SqueakLogEvent.VoicePackCompAutoAttached)
            return "VoicePack comp auto-attached to race " + (string.IsNullOrEmpty(data.Race) ? "-" : data.Race) + ".";
        if (e == SqueakLogEvent.VoicePackCompAttachSkipped)
            return "VoicePack comp auto-attach skipped for race " + (string.IsNullOrEmpty(data.Race) ? "-" : data.Race) + " (" + (string.IsNullOrEmpty(data.Reason) ? "-" : data.Reason) + ").";
        if (e == SqueakLogEvent.LabelOverflow)
        {
            return "A settings label does not fit its rect: "
                + (string.IsNullOrEmpty(data.Target) ? "-" : data.Target)
                + " (" + (string.IsNullOrEmpty(data.Reason) ? "-" : data.Reason) + " axis, font "
                + (string.IsNullOrEmpty(data.Source) ? "-" : data.Source) + ", needs "
                + (data.Needed == null ? "-" : data.Needed.Value.ToString("0.0", CultureInfo.InvariantCulture))
                + "px, has " + (data.Available == null ? "-" : data.Available.Value.ToString("0.0", CultureInfo.InvariantCulture)) + "px).";
        }
        return definition.Human;
    }

    internal static string EventId(SqueakLogEvent e) => e switch
    {
        SqueakLogEvent.ModStartIdentity => "mod.start.identity",
        SqueakLogEvent.ModStartReady => "mod.start.ready",
        SqueakLogEvent.LoggingModeEnabled => "logging.mode.enabled",
        SqueakLogEvent.LoggingModeDisabled => "logging.mode.disabled",
        SqueakLogEvent.LoggingModeAutoEnabled => "logging.mode.auto_enabled",
        SqueakLogEvent.LoggingModeAutoDisabled => "logging.mode.auto_disabled",
        SqueakLogEvent.SettingsOpenApiUnavailable => "settings.open.api_unavailable",
        SqueakLogEvent.SettingsOpenFailed => "settings.open.failed",
        SqueakLogEvent.CatalogRefreshFailed => "voicepack.catalog.refresh_failed",
        SqueakLogEvent.PackRejected => "voicepack.pack.rejected",
        SqueakLogEvent.ResolverRebuildFailed => "voicepack.resolver.rebuild_failed",
        SqueakLogEvent.TargetRejected => "voicepack.target.rejected",
        SqueakLogEvent.XenotypeDiscoveryUnavailable => "xenotype.discovery.unavailable",
        SqueakLogEvent.XenotypeDiscoveryFailed => "xenotype.discovery.failed",
        SqueakLogEvent.XenotypeDiscoveryCandidate => "xenotype.discovery.candidate",
        SqueakLogEvent.TriggerAttemptFailed => "trigger.attempt.failed",
        SqueakLogEvent.AudioNoSound => "audio.dispatch.no_sound",
        SqueakLogEvent.AudioDispatchFailed => "audio.dispatch.failed",
        SqueakLogEvent.AudioDispatchOk => "audio.dispatch.ok",
        SqueakLogEvent.TriggerOutcomeSummary => "trigger.outcome.summary",
        SqueakLogEvent.HookAttackUnavailable => "hook.attack.unavailable",
        SqueakLogEvent.HookAttackTargetSkipped => "hook.attack.target_skipped",
        SqueakLogEvent.HookMentalBreakUnavailable => "hook.mental_break.unavailable",
        SqueakLogEvent.HookMentalFitUnavailable => "hook.mental_fit.unavailable",
        SqueakLogEvent.DiagnosticsHookUnavailable => "diagnostics.hook.unavailable",
        SqueakLogEvent.DiagnosticsStartFailed => "diagnostics.start.failed",
        SqueakLogEvent.FallbackProfileStoreFailed => "fallback.profile.store_failed",
        SqueakLogEvent.OverlayChanged => "devtools.overlay.changed",
        SqueakLogEvent.CameraChanged => "devtools.camera_indicator.changed",
        SqueakLogEvent.WorkbenchOpenFailed => "devtools.workbench.open_failed",
        SqueakLogEvent.SettingsOrigin => "settings.origin",
        SqueakLogEvent.AudioRouteSelected => "audio.route.selected",
        SqueakLogEvent.AudioVanillaFallback => "audio.dispatch.vanilla_fallback",
        SqueakLogEvent.AudioDisabled => "audio.disabled",
        SqueakLogEvent.VoicePackCompAutoAttached => "voicepack.comp.auto_attached",
        SqueakLogEvent.VoicePackCompAttachSkipped => "voicepack.comp.attach_skipped",
        SqueakLogEvent.VoicePackCompAttachFailed => "voicepack.comp.attach_failed",
        SqueakLogEvent.LabelOverflow => "ui.text.overflow",
        _ => throw new ArgumentOutOfRangeException(nameof(e))
    };
}

internal static class SqueakLogOnce
{
    private const int Limit = 1024;
    private static readonly object sync = new();
    private static readonly HashSet<string> keys = new(StringComparer.Ordinal);

    internal static void Reset()
    {
        lock (sync) keys.Clear();
    }

    internal static bool Claim(SqueakLogEvent evt, SqueakLogData data, int version)
    {
        // Once domains are namespaced per protocol version: fmt=1 records claim log-v1 (byte-identical to the
        // locked v1 surface), fmt=2 records claim log-v2, so the two domains never collide.
        // Per-domain/per-sound events include their domain/sound dimensions in the claim key so one race or
        // sound failure does not suppress the same failure for a different race/sound.
        string key = "coahuilite.universalsqueaker|log-v" + version + "|" + evt + "|" + SqueakLogFormatter.Value(data.Action) + "|" + SqueakLogFormatter.Value(data.Target) + "|" + SqueakLogFormatter.Value(data.Pack) + "|" + SqueakLogFormatter.Value(data.Reason) + "|" + SqueakLogFormatter.Value(data.Race) + "|" + SqueakLogFormatter.Value(data.Xenotype) + "|" + SqueakLogFormatter.Value(data.Sound) + "|" + (data.Exception?.GetType().FullName ?? "-");
        lock (sync)
        {
            if (keys.Contains(key)) return false;
            if (keys.Count >= Limit) keys.Clear();
            keys.Add(key);
            return true;
        }
    }
}

internal static class SqueakLogFormatter
{
    internal static string Suffix(SqueakLogEvent evt, SqueakLogDefinition definition, SqueakLogData data, string build, string buildId)
    {
        if (definition.Version >= 2) return SuffixV2(evt, definition, data, build, buildId);

        // v1: the locked 28-event byte surface. Do not add fields here; v2-only facts must not leak into v1 records.
        StringBuilder builder = new("usdiag fmt=1 lvl=");
        builder.Append(definition.Level.ToString().ToLowerInvariant()).Append(" vis=").Append(definition.Visibility == SqueakLogVisibility.Daily ? "daily" : "dev_only").Append(" evt=").Append(Value(SqueakLogRegistry.EventId(evt))).Append(" action=").Append(Value(data.Action)).Append(" target=").Append(Value(data.Target)).Append(" pack=").Append(Value(data.Pack)).Append(" build=").Append(Value(build)).Append(" build_id=").Append(Value(buildId));
        Add(builder, "reason", data.Reason);
        Add(builder, "sound", data.Sound);
        Add(builder, "source", data.Source);
        Add(builder, "count", data.Count);
        Add(builder, "dispatched", data.Dispatched);
        Add(builder, "suppressed_detail", data.SuppressedDetail);
        Add(builder, "enabled", data.Enabled);
        Add(builder, "pawn", data.PawnName);
        Add(builder, "pawn_id", data.PawnId);
        if (data.Exception != null)
        {
            var site = data.Exception.TargetSite;
            Add(builder, "ex_type", data.Exception.GetType().FullName);
            Add(builder, "ex_inner", data.Exception.InnerException?.GetType().FullName);
            Add(builder, "ex_site", site == null ? null : site.DeclaringType?.FullName + "." + site.Name);
            Add(builder, "ex_msg", SqueakLogText.SanitizeExceptionMessage(data.Exception.Message));
        }

        return builder.ToString();
    }

    /// <summary>v2 fixed core order: fmt lvl vis evt action target pack race [xenotype] build build_id, then
    /// event-specific fields in the fixed per-event order below. race is mandatory (missing = "-");
    /// xenotype is present only when the domain has one. action carries the string action key (built-in =
    /// enum name, byte-identical to v1; external = packageId.defName, "." is in the encoding whitelist).</summary>
    private static string SuffixV2(SqueakLogEvent evt, SqueakLogDefinition definition, SqueakLogData data, string build, string buildId)
    {
        StringBuilder builder = new("usdiag fmt=2 lvl=");
        builder.Append(definition.Level.ToString().ToLowerInvariant()).Append(" vis=").Append(definition.Visibility == SqueakLogVisibility.Daily ? "daily" : "dev_only").Append(" evt=").Append(Value(SqueakLogRegistry.EventId(evt))).Append(" action=").Append(Value(data.Action)).Append(" target=").Append(Value(data.Target)).Append(" pack=").Append(Value(data.Pack)).Append(" race=").Append(Value(data.Race));
        if (!string.IsNullOrEmpty(data.Xenotype)) builder.Append(" xenotype=").Append(Value(data.Xenotype));
        builder.Append(" build=").Append(Value(build)).Append(" build_id=").Append(Value(buildId));
        switch (evt)
        {
            case SqueakLogEvent.SettingsOrigin:
                Add(builder, "settings_origin", data.SettingsOrigin == null ? null : data.SettingsOrigin.Value == SqueakSettingsOrigin.LoadedFromFile ? "LoadedFromFile" : "FreshCreated");
                break;
            case SqueakLogEvent.AudioRouteSelected:
                Add(builder, "sound", data.Sound);
                Add(builder, "tier", data.Tier);
                Add(builder, "egg", data.Egg);
                Add(builder, "suppressed_detail", data.SuppressedDetail);
                Add(builder, "pawn", data.PawnName);
                Add(builder, "pawn_id", data.PawnId);
                Add(builder, "pawn_faction", data.PawnFaction);
                Add(builder, "pawn_ctrl", data.PawnControlled == null ? null : (data.PawnControlled.Value ? "player" : "nonplayer"));
                break;
            case SqueakLogEvent.AudioVanillaFallback:
                Add(builder, "sound", data.Sound);
                Add(builder, "tier", data.Tier);
                Add(builder, "egg", data.Egg);
                Add(builder, "pawn", data.PawnName);
                Add(builder, "pawn_id", data.PawnId);
                Add(builder, "pawn_faction", data.PawnFaction);
                Add(builder, "pawn_ctrl", data.PawnControlled == null ? null : (data.PawnControlled.Value ? "player" : "nonplayer"));
                break;
            case SqueakLogEvent.FallbackProfileStoreFailed:
                if (data.Exception != null)
                {
                    var site = data.Exception.TargetSite;
                    Add(builder, "ex_type", data.Exception.GetType().FullName);
                    Add(builder, "ex_inner", data.Exception.InnerException?.GetType().FullName);
                    Add(builder, "ex_site", site == null ? null : site.DeclaringType?.FullName + "." + site.Name);
                    Add(builder, "ex_msg", SqueakLogText.SanitizeExceptionMessage(data.Exception.Message));
                }
                break;
            case SqueakLogEvent.VoicePackCompAttachSkipped:
                Add(builder, "reason", data.Reason);
                break;
            case SqueakLogEvent.VoicePackCompAttachFailed:
                if (data.Exception != null)
                {
                    var site = data.Exception.TargetSite;
                    Add(builder, "ex_type", data.Exception.GetType().FullName);
                    Add(builder, "ex_inner", data.Exception.InnerException?.GetType().FullName);
                    Add(builder, "ex_site", site == null ? null : site.DeclaringType?.FullName + "." + site.Name);
                    Add(builder, "ex_msg", SqueakLogText.SanitizeExceptionMessage(data.Exception.Message));
                }
                break;
            case SqueakLogEvent.LabelOverflow:
                Add(builder, "reason", data.Reason);
                Add(builder, "source", data.Source);
                Add(builder, "need", data.Needed);
                Add(builder, "have", data.Available);
                break;
        }

        return builder.ToString();
    }

    internal static string Value(object? value)
    {
        if (value == null) return "-";
        if (value is bool boolean) return boolean ? "true" : "false";
        return SqueakLogText.PercentEncode(value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString());
    }

    private static void Add(StringBuilder builder, string key, object? value)
    {
        if (value != null) builder.Append(' ').Append(key).Append('=').Append(Value(value));
    }
}

internal static class SqueakLogText
{
    private static readonly Regex Path = new(@"(?ix)(?:\\\\[?.][\\/][^\s]+|\\\\[^\\/\s]+[\\/][^\s]+|[a-z]:[\\/][^\s]+|[a-z]:(?![\\/])[^\s:]+|(?<![\\\w])\\(?![\\?.])[^\\\s]+(?:\\[^\s]+)*|file:(?://)?[^\s]+|(?<!\w)/(?:[^\s]+)|(?<!\w)\.\.?[\\/][^\s]+)", RegexOptions.Compiled);
    internal static string SanitizeExceptionMessage(string? text) { if (string.IsNullOrEmpty(text)) return "-"; string clean = new string(Array.FindAll(text!.Replace('\r', ' ').Replace('\n', ' ').ToCharArray(), c => !char.IsControl(c))); clean = Path.Replace(clean, "<path>"); return clean.Length > 256 ? clean.Substring(0, 256) : clean; }
    internal static string PercentEncode(string? text) { if (string.IsNullOrEmpty(text) || text == "N/A") return "-"; byte[] bytes = Encoding.UTF8.GetBytes(text); StringBuilder builder = new(); foreach (byte value in bytes) { bool safe = value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z' || value >= '0' && value <= '9' || value == '.' || value == '_' || value == '~' || value == ':' || value == '/' || value == '@' || value == '+' || value == '-'; if (safe) builder.Append((char)value); else builder.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture)); } return builder.ToString(); }
}

internal static class SqueakLogSink
{
    internal static void Emit(SqueakLogLevel level, string text)
    {
        if (level == SqueakLogLevel.Warning) Log.Warning(text);
        else if (level == SqueakLogLevel.Error) Log.Error(text);
        else Log.Message(text);
    }
}
