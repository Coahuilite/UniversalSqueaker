using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production implementation of <see cref="IUsKernelSettingsSource"/>. It owns one per-window
/// <see cref="VoicePacksPageState"/> (created by the settings window and passed in), builds the
/// read-only view once per Unity frame, and routes every typed write to the existing business
/// surface (<see cref="UniversalSqueakerSettings"/> and the typed
/// <see cref="VoicePacksPageModel"/> facade).
/// </summary>
public sealed class UsKernelSettingsSource : IUsKernelSettingsSource
{
    private readonly UniversalSqueakerSettings settings;
    private readonly VoicePacksPageState state;
    private VoicePacksViewState? cachedView;
    private Func<int>? revisionSource;
    private int cachedRevision = -1;

    public UsKernelSettingsSource(UniversalSqueakerSettings settings)
    {
        this.settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        state = new VoicePacksPageState();
    }

    public VoicePacksPageState ViewState => state;

    /// <summary>
    /// Wires the view cache to the Host session's content revision - the SAME clock the layout cache
    /// invalidates on. Caching by <c>Time.frameCount</c> instead let a write that happens in a frame's
    /// popup pass bump the layout revision and re-arrange against this frame's still-stale view, while
    /// the next frame's fresh view drew into that stale snapshot: content and geometry disagreed until
    /// the next bump (the 2026-09-04 "filter misaligns until a workspace switch" report). Until a
    /// source is attached (no Host yet) the frame clock remains the fallback.
    /// </summary>
    public void AttachRevisionSource(Func<int> revision)
    {
        revisionSource = revision ?? throw new System.ArgumentNullException(nameof(revision));
        cachedRevision = -1;
    }

    public VoicePacksViewState BuildView()
    {
        int revision = revisionSource != null ? revisionSource() : Time.frameCount;
        if (cachedView == null || revision != cachedRevision)
        {
            cachedView = VoicePacksPageModel.BuildView(settings, SqueakXenotypeCatalog.Current, state);
            cachedRevision = revision;
        }

        return cachedView;
    }

    public string SectionHelpKey(string sectionKey)
    {
        return VoicePacksPageModel.SectionHelpKeyOf(sectionKey);
    }

    // Fallback shares the footer's Keyed entry so a missing instance never leaks a raw English token
    // into a translated page. SaveStatus stays a raw token: the footer maps status tokens itself and
    // must keep matching on them.
    public string BuildIdentity => UniversalSqueakerMod.Instance != null ? UniversalSqueakerMod.BuildIdentity() : "US.Footer.Build.Unknown".Translate();

    public string SaveStatus => UniversalSqueakerMod.Instance?.SaveState.ToString() ?? "Unknown";

    public bool SaveStatusVisible => UniversalSqueakerMod.Instance?.SaveStatusVisible ?? true;

    public bool IsDirty => UniversalSqueakerMod.Instance?.IsSettingsDirty ?? false;

    public void SetMode(SqueakVoicePackMode mode)
    {
        settings.CommitVoicePackMode(mode);
    }

    public void SetGlobalVolume(float value)
    {
        settings.SetGlobalVolume(value);
    }

    public void SetDistancePreset(SqueakDistancePreset preset)
    {
        settings.SetDistancePreset(preset);
    }

    public void SetDistanceRange(float start, float end)
    {
        settings.SetDistanceRange(start, end);
    }

    public void SetBasicToggle(SqueakBasicToggle key, bool value)
    {
        settings.SetBasicTuning(key, value);
    }

    public void SetCameraIndicator(bool value)
    {
        settings.SetCameraIndicator(value);
    }

    public void SetEasterEggs(bool value)
    {
        settings.SetAllowEasterEggSounds(value);
    }

    public void SetBabyActions(bool value)
    {
        settings.SetAllowBabyActions(value);
        cachedView = null;
    }

    public void SetEatPrecision(bool value)
    {
        settings.SetEatPrecision(value);
    }

    public void SetEatPrecisionIncludeDrugs(bool value)
    {
        settings.SetEatPrecisionIncludeDrugs(value);
    }

    public void SetGlobalMinIntervalTicks(int ticks)
    {
        settings.SetGlobalMinIntervalTicks(ticks);
    }

    public void SetGlobalCooldownMultiplier(float value)
    {
        settings.SetGlobalCooldownMultiplier(value);
    }

    public void SetDevLoggingMode(SqueakDevLoggingMode mode)
    {
        settings.SetDevLoggingMode(mode);
    }

    // === Developer layout diagnosis (R3-B) =====================================================
    // Per-window, never persisted, and NOT routed through UniversalSqueakerSettings: these are developer
    // switches over this window's own diagnostic scope. The geometry instrument is per HOST, so enabling it
    // here cannot reach the diagnostics panel's host or any other window's.
    private UiHost? host;
    private bool layoutCaptureOn;
    private bool layoutOutlineOn;

    /// <summary>
    /// A report was asked for and has not been produced yet. The window consumes this after a real draw pass
    /// (see <see cref="ConsumeLayoutReportRequest"/>), which is what makes the report one-shot: the request is
    /// cleared in the same call that produces it, so the next pass reports nothing new.
    /// </summary>
    private bool layoutReportPending;

    /// <summary>
    /// What this window's Report control is currently saying (RPT1.1). It is written ONLY where one of the
    /// audit's own facts is known - a refused request, an accepted wait, or a produced pass - so a success
    /// sentence can never be built out of a click.
    /// </summary>
    private LayoutReportOutcome reportOutcome = LayoutReportOutcome.None;

    /// <summary>
    /// The pass the last PRODUCED report described, or -1. Naming it in the sentence is what makes a later
    /// re-click's newest result distinguishable from the previous one (RPT1.1).
    /// </summary>
    private int lastReportPass = -1;

    /// <summary>
    /// What the Report control says. <see cref="None"/> means "the instrument has not been asked anything
    /// since it last changed", and the sentence is then derived from the instrument's own state - so the line
    /// can never keep claiming a reason that the developer has just changed (RPT1.2's "real reason").
    /// </summary>
    private enum LayoutReportOutcome
    {
        None,
        Ready,
        CaptureOff,
        RefusedCaptureOff,
        Unavailable,
        NoScope,
        Waiting,
        Written,
        NotProduced,
    }

    /// <summary>
    /// The host this window's page is drawn by. The developer switches are per-host, and this is how a page
    /// command reaches the instrument of ITS OWN host rather than a process-wide flag. The production host
    /// factory attaches it (next to the revision source); a harness fake attaches its own.
    /// </summary>
    public void AttachHost(UiHost host)
    {
        this.host = host;
    }

    /// <summary>The switch's own checked state: what the instrument actually honoured, never the request.</summary>
    public bool LayoutCaptureOn => layoutCaptureOn;

    /// <summary>Whether the captured-rect outline is on for this window (blanked when capture is off).</summary>
    public bool LayoutOutlineOn => layoutOutlineOn;

    /// <summary>
    /// The status sentence for the developer. The "unavailable" state is derived from the CARRIER, not from a
    /// failed click: a development build on a release payload reports that the tool is missing, which is the
    /// difference between "nothing was captured" and "capture cannot run here".
    /// </summary>
    public string LayoutDiagnosisStatus
    {
        get
        {
            if (host == null) return "US.Diagnostics.Geometry.NoScope".Translate();
            switch (UsTextFitAudit.GetDevGeometryStatus(host))
            {
                case UsTextFitAudit.DevGeometryStatus.Unavailable:
                    return "US.Diagnostics.Geometry.Unavailable".Translate();
                case UsTextFitAudit.DevGeometryStatus.Overlay:
                    return "US.Diagnostics.Geometry.Overlay".Translate();
                case UsTextFitAudit.DevGeometryStatus.OutlineOnly:
                    return "US.Diagnostics.Geometry.OutlineOnly".Translate();
                case UsTextFitAudit.DevGeometryStatus.Active:
                    return "US.Diagnostics.Geometry.Active".Translate();
                case UsTextFitAudit.DevGeometryStatus.ScopeMissing:
                    return "US.Diagnostics.Geometry.NoScope".Translate();
                default:
                    return "US.Diagnostics.Geometry.Off".Translate();
            }
        }
    }

    /// <summary>
    /// The outcome of this window's Report control, as the sentence the page prints immediately below the
    /// Report button (RPT1.1). READ-ONLY for the page: the button's own command is the only writer, and every
    /// sentence is a Keyed entry, so the same translation mechanism carries it.
    /// <para>
    /// Success is tied to the pass the audit actually generated (<see cref="lastReportPass"/>), never to the
    /// click: <see cref="RequestLayoutReport"/> leaves the line at "requested", and only
    /// <see cref="ConsumeLayoutReportRequest"/> returning a pass turns it into "written". When capture is off
    /// or the carrier has no instrument, the same line names THAT reason instead of a generic failure
    /// (RPT1.2).
    /// </para>
    /// </summary>
    public string LayoutReportStatus
    {
        get
        {
            LayoutReportOutcome current = IdleReportOutcome(host);
            LayoutReportOutcome outcome = reportOutcome;
            if (outcome == LayoutReportOutcome.None || outcome == LayoutReportOutcome.Ready
                || outcome == LayoutReportOutcome.CaptureOff || outcome == LayoutReportOutcome.Unavailable
                || outcome == LayoutReportOutcome.NoScope)
            {
                outcome = current;
            }
            else if (outcome == LayoutReportOutcome.RefusedCaptureOff && current != LayoutReportOutcome.CaptureOff)
            {
                outcome = current;
            }
            switch (outcome)
            {
                case LayoutReportOutcome.Written:
                    return WrittenReportSentence(lastReportPass);
                case LayoutReportOutcome.Waiting:
                    return "US.Diagnostics.Geometry.Report.Waiting".Translate();
                case LayoutReportOutcome.NotProduced:
                    return "US.Diagnostics.Geometry.Report.NotProduced".Translate();
                case LayoutReportOutcome.RefusedCaptureOff:
                    return "US.Diagnostics.Geometry.Report.RefusedCaptureOff".Translate();
                case LayoutReportOutcome.Unavailable:
                    return "US.Diagnostics.Geometry.Report.Unavailable".Translate();
                case LayoutReportOutcome.NoScope:
                    return "US.Diagnostics.Geometry.Report.NoScope".Translate();
                case LayoutReportOutcome.Ready:
                    return "US.Diagnostics.Geometry.Report.Ready".Translate();
                default:
                    return "US.Diagnostics.Geometry.Report.CaptureOff".Translate();
            }
        }
    }

    /// <summary>
    /// The sentence for the state the instrument is in when no attempt is recorded. This is the "real reason"
    /// half of RPT1.2: it is read from the carrier's own status, so a shipped build says the tool is missing
    /// and a window without a scope says exactly that, rather than blaming a capture that never ran.
    /// </summary>
    private static LayoutReportOutcome IdleReportOutcome(UiHost? host)
    {
        switch (UsTextFitAudit.GetDevGeometryStatus(host))
        {
            case UsTextFitAudit.DevGeometryStatus.Unavailable:
                return LayoutReportOutcome.Unavailable;
            case UsTextFitAudit.DevGeometryStatus.ScopeMissing:
                return LayoutReportOutcome.NoScope;
            case UsTextFitAudit.DevGeometryStatus.Active:
            case UsTextFitAudit.DevGeometryStatus.Overlay:
                return LayoutReportOutcome.Ready;
            default:
                return LayoutReportOutcome.CaptureOff;
        }
    }

    /// <summary>The success sentence, carrying the pass the report was generated from as its format argument.</summary>
    private static string WrittenReportSentence(int pass)
    {
        string template = "US.Diagnostics.Geometry.Report.Written".Translate();
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, pass);
    }

    public void SetLayoutCapture(bool on)
    {
        bool honoured = UsTextFitAudit.SetGeometryCapture(host, on);
        // A refusal is a status, not a silent no-op: the switch stays where the instrument actually is, and a
        // refused capture cannot leave an outline switched on over nothing.
        layoutCaptureOn = honoured && on;
        if (!honoured && on) layoutOutlineOn = false;
        // The instrument moved, so a sentence that described the OLD instrument state ("capture is off") must
        // not stay up: drop back to "nothing asked yet" and let the sentence follow the instrument (RPT1.2).
        if (reportOutcome != LayoutReportOutcome.Waiting
            && reportOutcome != LayoutReportOutcome.Written
            && reportOutcome != LayoutReportOutcome.NotProduced)
        {
            reportOutcome = LayoutReportOutcome.None;
        }
    }

    public void SetLayoutOutline(bool on)
    {
        bool honoured = UsTextFitAudit.SetGeometryOverlay(host, on);
        layoutOutlineOn = honoured && on;
    }

    public void OpenDeveloperPanel() => UniversalSqueaker.UI.Dev.UsDevPanelWindow.OpenOrFocus();

    public void RequestLayoutReport()
    {
        // A request that cannot be honoured is NOT left pending: the audit refuses it at request time, so
        // nothing here can be satisfied by a later enable (R3-B fix 7).
        layoutReportPending = UsTextFitAudit.RequestGeometryReport(host);
        if (layoutReportPending)
        {
            // Accepted: the line now says what happens next, not that it already succeeded (RPT1.1).
            reportOutcome = LayoutReportOutcome.Waiting;
            return;
        }

        // Refused: the line says the REAL reason (RPT1.2). "No report written because capture is off" is a
        // different sentence from the idle "capture is off", so a click that cannot produce a report still
        // changes what the developer reads - a click with no visible answer at all was the reported defect.
        LayoutReportOutcome reason = IdleReportOutcome(host);
        reportOutcome = reason == LayoutReportOutcome.CaptureOff
            ? LayoutReportOutcome.RefusedCaptureOff
            : reason;
    }

    /// <summary>
    /// Produces the pending report if a pass has completed since it was requested, clearing the request in the
    /// same step. A return of -1 means "nothing was written" - no request, or the pass it waits on has not
    /// completed - so the request survives to the next pass. One request is one report.
    /// </summary>
    public int ConsumeLayoutReportRequest()
    {
        if (!layoutReportPending || host == null) return -1;

        int pass = UsTextFitAudit.PublishGeometryReport(host);
        if (pass >= 0)
        {
            layoutReportPending = false;
            // The pass IS the result the line prints, so a re-click can be told from the previous report by
            // the number alone (RPT1.1).
            lastReportPass = pass;
            reportOutcome = LayoutReportOutcome.Written;
            return pass;
        }

        // No report yet. Retire the request ONLY when it can no longer be honoured - the capture is off or
        // the carrier has no instrument - so a Dev/OFF request still cannot dangle into a later enable.
        // Retiring merely because an EARLIER report exists was the D1 defect: after the first report the
        // request was cleared on its first consume attempt, which runs right after the click and before the
        // next pass, so every later request in the same window died instead of waiting for its pass.
        UsTextFitAudit.DevGeometryStatus status = UsTextFitAudit.GetDevGeometryStatus(host);
        if (status != UsTextFitAudit.DevGeometryStatus.Active
            && status != UsTextFitAudit.DevGeometryStatus.Overlay)
        {
            layoutReportPending = false;
            // The reason the report can no longer be produced, from the instrument itself (RPT1.2).
            reportOutcome = IdleReportOutcome(host);
            return -1;
        }

        // Still instrumented, yet the audit no longer holds the request: it retired it without a report (the
        // completed pass carried no capture). Say THAT, rather than leaving "requested" up for ever.
        if (UsTextFitAudit.FindOpenScope(host)?.IsGeometryReportPending != true)
        {
            layoutReportPending = false;
            reportOutcome = LayoutReportOutcome.NotProduced;
        }

        return -1;
    }

    /// <summary>
    /// The structured rendering of the last explicit geometry report, from the SAME capture as the text the
    /// developer log carries (R3-B fix 5 / B1's "two renderings, one capture"). Null until a report exists.
    /// <para>
    /// internal, and here rather than on the audit, for the API-tier reason the audit's own accessor records:
    /// the carrier's snapshot type is public there but unlisted in its <c>docs/api-tiers.md</c>, so a US
    /// surface that names it must not be public. The harness reads it from this assembly against the real
    /// bound commands, which is where the same-pass claim is asserted.
    /// </para>
    /// </summary>
    internal UiDevGeometrySnapshot? LayoutReportSnapshot =>
        host != null ? UsTextFitAudit.FindOpenScope(host)?.LatestGeometryReport : null;

    public void SetLocalizeDebugActions(bool value)
    {
        settings.SetLocalizeDebugActions(value);
    }

    public void SetActiveTab(string tab)
    {
        VoicePacksPageModel.SetActiveTab(state, tab);
    }

    public void ScrollToSection(string sectionKey)
    {
        VoicePacksPageModel.ScrollToSection(state, sectionKey);
    }

    public void SetHelpPanelOpen(bool open)
    {
        state.HelpPanelOpen = open;
    }
    // US-ESC1 tree cancel layers. Can reads the SAME projection the card shows, so "is there a selection
    // to return from" has one answer; Cancel is state-only - it never touches settings, persistence or
    // routing, and it never clears filters (§4.1).
    public bool CanCancelDomainSelection()
        => VoicePacksPageModel.CanCancelDomainSelection(state, BuildView());
    public void CancelDomainSelection()
        => VoicePacksPageModel.CancelDomainSelection(state);
    public bool CanCancelTuningTarget()
        => VoicePacksPageModel.CanCancelTuningTarget(state, BuildView());
    public void CancelTuningTarget()
        => VoicePacksPageModel.CancelTuningTarget(state, BuildView());

    public void SetTuningLayer(int layer)
    {
        VoicePacksPageModel.SetTuningLayer(state, layer);
    }

    public void SetTuningDomain(string raceDefName, string targetDefName)
    {
        VoicePacksPageModel.SetTuningDomain(state, raceDefName, targetDefName);
    }

    public void SelectDomain(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        VoicePacksPageModel.SelectDomain(state, scope, raceDefName, targetDefName);
    }

    public void SetDomainFilter(SqueakDomainFilterKind kind, bool flag)
    {
        VoicePacksPageModel.SetDomainFilter(state, kind, flag);
    }

    public void SetPackFilter(string author)
    {
        VoicePacksPageModel.SetPackFilter(state, author);
    }

    public void SetRaceFilter(string raceDefName)
    {
        VoicePacksPageModel.SetRaceFilter(settings, state, raceDefName);
    }

    public void SetXenotypeFilter(string xenotypeDefName)
    {
        VoicePacksPageModel.SetXenotypeFilter(state, xenotypeDefName);
    }

    public void SetSearchText(string text)
    {
        VoicePacksPageModel.SetSearchText(state, text);
    }

    // D4: the two domain lists' own searches; same page-state channel as the pack search, so the
    // one-frame write->projection contract holds for all three boxes.
    public void SetRaceSearchText(string text)
    {
        VoicePacksPageModel.SetRaceSearchText(state, text);
    }

    public void SetXenotypeSearchText(string text)
    {
        VoicePacksPageModel.SetXenotypeSearchText(state, text);
    }

    // VF1定稿 A2/A4: the multiplier facade rides the same state identity as the scope facade.
    public void SetActionTuning(string actionKey, bool intervalField, float? value)
    {
        VoicePacksPageModel.SetActionTuningMultiplier(settings, state, actionKey, intervalField, value);
    }

    public void ResetActionToPreset(string actionKey)
    {
        VoicePacksPageModel.ResetActionTuningToPreset(settings, state, actionKey);
    }

    // VF1定稿: the area/selection commands are page-state writes; the table commands go through the
    // model facade onto the store (the single authority for per-race final tables).
    public void SetTuningArea(int area) => VoicePacksPageModel.SetTuningArea(state, area);
    public void SetTuningSelectedAction(string actionKey) => VoicePacksPageModel.SetTuningSelectedAction(state, actionKey);
    public void SetFallbackSelection(string? race, string? entryAction) => VoicePacksPageModel.SetFallbackSelection(state, race, entryAction);
    public void SetFallbackQueries(string? soundQuery, string? newRaceQuery) => VoicePacksPageModel.SetFallbackQueries(state, soundQuery, newRaceQuery);
    public void SetFallbackEntry(string actionKey, string? soundDefName) => VoicePacksPageModel.SetFallbackEntry(settings, state, actionKey, soundDefName);
    public void CreateFallbackTable(string raceDefName) => VoicePacksPageModel.CreateFallbackTable(settings, state, raceDefName);
    public void RestoreFallbackDefault() => VoicePacksPageModel.RestoreFallbackDefault(settings, state);
    public void DeleteFallbackTable(string raceDefName) => VoicePacksPageModel.DeleteFallbackTable(settings, state, raceDefName);

    public void SetActionScope(string actionKey, SqueakActionScope? scope)
    {
        VoicePacksPageModel.SetActionScope(settings, state, actionKey, scope);
    }

    public void SetMoodTuning(SqueakMood mood, SqueakMoodFactor factor, float? value)
    {
        VoicePacksPageModel.SetMoodTuning(settings, state, mood, factor, value);
    }

    public void ResetMoodToPreset(SqueakMood mood)
    {
        VoicePacksPageModel.ResetMoodToPreset(settings, state, mood);
    }

    public void ToggleBaselinePreset(string presetDefName)
    {
        VoicePacksPageModel.ToggleBaselinePresetSelection(state, presetDefName);
    }

    public void ToggleBaselineRace(string presetDefName, string raceDefName, bool selected)
    {
        VoicePacksPageModel.ToggleBaselineRaceSelection(state, presetDefName, raceDefName, selected);
    }

    public void ToggleBaselineXenotype(string presetDefName, string raceDefName, string xenotypeDefName, bool selected)
    {
        VoicePacksPageModel.ToggleBaselineXenotypeSelection(state, presetDefName, raceDefName, xenotypeDefName, selected);
    }

    public void ImportBaselinePreset(string presetDefName)
    {
        VoicePacksPageModel.ImportBaselinePresetSelection(settings, presetDefName, state);
    }

    public void ToggleVoicePack(SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey, bool enabled)
    {
        VoicePacksPageModel.ToggleVoicePack(settings, scope, raceDefName, targetDefName, packKey, enabled);
    }

    public void ForgetUnavailable(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        VoicePacksPageModel.ForgetUnavailable(settings, scope, raceDefName, targetDefName);
    }
}
