using System;

namespace UniversalSqueaker.UI;

/// <summary>Business/view boundary of the kernel settings path. The Host adapter binds every typed widget
/// binding/action to this surface; the production implementation routes to
/// <see cref="UniversalSqueakerSettings"/> and <see cref="VoicePacksPageModel"/>. The interface is
/// deliberately narrow and typed so tests can substitute a recording fake without the RimWorld
/// graph. View state (filters, selections, tuning layer) is owned here and exposed via
/// <see cref="ViewState"/>; IMGUI transients (scroll, popup, focus, drag) stay in the UiKit session.
/// </summary>
public interface IUsKernelSettingsSource
{
    /// <summary>US-owned per-window view state (tabs, filters, selections, tuning layer).</summary>
    VoicePacksPageState ViewState { get; }

    /// <summary>Read-only projection of settings + catalog for the current view state.</summary>
    VoicePacksViewState BuildView();

    /// <summary>Help section key for a settings section key (e.g. "mode-row" → "us/mode-row").</summary>
    string SectionHelpKey(string sectionKey);

    string BuildIdentity { get; }

    string SaveStatus { get; }

    bool SaveStatusVisible { get; }

    bool IsDirty { get; }

    // Basic
    void SetMode(SqueakVoicePackMode mode);
    void SetGlobalVolume(float value);
    void SetDistancePreset(SqueakDistancePreset preset);
    void SetDistanceRange(float start, float end);
    void SetBasicToggle(SqueakBasicToggle key, bool value);
    void SetCameraIndicator(bool value);
    void SetEasterEggs(bool value);

    /// <summary>
    /// Parent switch of the eat-occurrence pair: false (the shipped default) keeps the whole
    /// <c>JobDefOf.Ingest</c> job counting as an Eat occurrence, true narrows it to genuinely
    /// ingesting food. Turning it off forces the child switch false in the same write.
    /// </summary>
    void SetEatPrecision(bool value);
    void SetBabyActions(bool value);

    /// <summary>Child option "include drugs"; only meaningful while the parent switch is on.</summary>
    void SetEatPrecisionIncludeDrugs(bool value);

    void SetGlobalMinIntervalTicks(int ticks);
    void SetGlobalCooldownMultiplier(float value);
    void SetDevLoggingMode(SqueakDevLoggingMode mode);
    void SetLocalizeDebugActions(bool value);

    // View/navigation state (US-owned, per window)
    void SetActiveTab(string tab);
    void ScrollToSection(string sectionKey);

    /// <summary>
    /// Bottom help panel visibility. Independent per-window view state, never the engine's
    /// active-tab gate: the Host writes it through its own "help-open" binding, and the revision
    /// bumper re-arranges the page (the panel is a declared element the engine hides, not a
    /// rebuilt tree) at the next arrange. Since BH1 it never changes the window width.
    /// </summary>
    void SetHelpPanelOpen(bool open);
    /// <summary>US-ESC1 tree cancel layers: the packs return-from-selection layer. Can is the engine's
    /// CanExecute answer (a vetoed layer is climbed past, never fired empty); Cancel exits the current
    /// operation domain while every filter, search and enabled state stays exactly where it was.</summary>
    bool CanCancelDomainSelection();
    void CancelDomainSelection();
    /// <summary>US-PACK1 (result layer, §4.1; review-1): the pack-card expand/collapse gesture writes
    /// ONLY the manual expansion set; the query's/dropdown's auto-expansion is derived and never enters
    /// it. Can is the engine's CanExecute for cancel-pack-results - the layer answers exactly while a
    /// manually opened card is VISIBLE in the current projection; a manual key the conditions hide is
    /// retained state, not an invisible layer, and a veto climbs to the page root.</summary>
    void TogglePackCard(string packKey);
    bool CanCancelPackResults();
    void CancelPackResults();
    /// <summary>US-ESC1: the tuning return ladder - fallback entry, then race table, then the selected
    /// action row, then the layer/domain/area context; one press answers one step.</summary>
    bool CanCancelTuningTarget();
    void CancelTuningTarget();
    void SetTuningLayer(int layer);
    void SetTuningDomain(string raceDefName, string targetDefName);
    void SelectDomain(SqueakVoicePackScope scope, string raceDefName, string targetDefName);
    void SetDomainFilter(SqueakDomainFilterKind kind, bool flag);
    void SetPackFilter(string author);
    void SetRaceFilter(string raceDefName);
    void SetXenotypeFilter(string xenotypeDefName);
    void SetSearchText(string text);
    // US-PACK1: the two domain lists' own search texts retired WITH the browse cards - the unified
    // keyword (SetSearchText) is the page's only text condition and it narrows the card rows.
    // No help-hover channel here any more (FL 0.3.0 P3): the claim is UiSession state, not business
    // state, so it never crosses this boundary. SectionHelpKey stays - that IS business resolution.
    // US-RESET1 (UI integration, review1-scoped): the restore operations the settings page exposes.
    // Each call goes through the backend's single publish/persistence funnel EXACTLY once (the UI
    // never re-calls it); the returned outcome is the only save claim the UI may make - NoChange/
    // Rejected wrote nothing and must leave every open edit exactly as it was. Ending a draft is
    // therefore NOT part of the restore call: the host command ends an edit only AFTER an Applied
    // result and only when the edit belongs to the fields the restore actually wrote (see
    // EndAffectedEdit below).
    SqueakResetOutcome ResetAllSettings();
    SqueakResetOutcome ResetDistanceDefaults();
    SqueakResetOutcome ResetNormalArea(SqueakNormalResetArea area);
    SqueakResetOutcome ResetActionTuningRow(string actionKey);
    /// <summary>True when the CURRENT tuning layer carries a valid domain identity for the area/row
    /// restores: layer 0 is legitimately Global, layer 1 needs the race, layer 2 needs race AND
    /// xenotype. An empty or invalid target must never fall back to a Global-area restore.</summary>
    bool CanResetTuningArea();
    /// <summary>Batch restore for the CURRENT layer+domain action area; the identity is the page's
    /// exact tuning layer, never a fallback to Global for an empty or invalid target.</summary>
    SqueakResetOutcome ResetActionTuningArea();
    SqueakResetOutcome ResetMoodTuningArea();
    /// <summary>US-RESET1 review1: end THIS host's open edit when, and only when, the edit belongs
    /// to the fields the just-Applied restore wrote. The decision (which element the operation
    /// affects) is the host command's; the mechanism is exactly one rung of the cancel ladder on
    /// this session - never a ladder loop, never a business return layer, never another window's
    /// capture. NoChange/Rejected callers never reach this.</summary>
    void EndAffectedEdit(Func<string, bool> editIsAffected);


    // Developer layout diagnosis (R3-B). These are per-WINDOW developer switches, not settings: they are
    // never persisted, never touch Config or a save, and they are deliberately NOT routed through
    // UniversalSqueakerSettings. The source holds the request; the window's own diagnostic scope applies it.
    /// <summary>Turn the layout capture on or off for this window's host. Off by default.</summary>
    void SetLayoutCapture(bool on);

    /// <summary>Turn the captured-rect outline on or off for this window's host.</summary>
    void SetLayoutOutline(bool on);

    /// <summary>Ask for exactly one layout report; the next drawn pass produces it and clears the request.</summary>
    void RequestLayoutReport();
    /// <summary>DT1: open (or focus) the developer geometry panel. Production reaches the real window
    /// seam; the harness records the call because Verse.Find is not stubbable in this assembly.</summary>
    void OpenDeveloperPanel();


    /// <summary>
    /// Read-only status of the layout capture for this window, as a user-facing sentence. A carrier without
    /// the development instrument reports that plainly, so a developer never reads "nothing captured" as
    /// "the layout is clean".
    /// </summary>
    string LayoutDiagnosisStatus { get; }

    /// <summary>
    /// What this window's Report control is currently saying, as a user-facing sentence printed immediately
    /// below the Report button (RPT1.1): the real reason a request was refused (capture off, no instrument,
    /// no diagnosis scope), the wait for the pass an accepted request is deferred to, or the pass a report
    /// was actually written from. Read-only - the button's own command is the only writer - and derived from
    /// the carrier's own report facts, so a success sentence appears only after a real report exists.
    /// </summary>
    string LayoutReportStatus { get; }

    /// <summary>Whether capture is currently on for this window (the switch's own checked state).</summary>
    bool LayoutCaptureOn { get; }

    /// <summary>Whether the captured-rect outline is currently on for this window.</summary>
    bool LayoutOutlineOn { get; }

    // Tuning
    void SetActionScope(string actionKey, SqueakActionScope? scope);
    // VF1定稿 A2/A4: one multiplier field of the CURRENT tuning layer's action identity; value null
    // clears the field (restore inheritance). reset writes the anchored preset's values back.
    void SetActionTuning(string actionKey, bool intervalField, float? value);
    void ResetActionToPreset(string actionKey);
    void SetMoodTuning(SqueakMood mood, SqueakMoodFactor factor, float? value);
    // VF1定稿: tuning area/selection and the final-fallback table editor commands.
    void SetTuningArea(int area);
    void SetTuningSelectedAction(string actionKey);
    void SetFallbackSelection(string? race, string? entryAction);
    void SetFallbackQueries(string? soundQuery, string? newRaceQuery);
    void SetFallbackEntry(string actionKey, string? soundDefName);
    void CreateFallbackTable(string raceDefName);
    void RestoreFallbackDefault();
    void DeleteFallbackTable(string raceDefName);

    /// <summary>「重置为预设」：把本层来源指向的预设基线重新写回（来源保持）。不可用时是空操作。</summary>
    void ResetMoodToPreset(SqueakMood mood);
    void ToggleBaselinePreset(string presetDefName);
    void ToggleBaselineRace(string presetDefName, string raceDefName, bool selected);
    void ToggleBaselineXenotype(string presetDefName, string raceDefName, string xenotypeDefName, bool selected);
    void ImportBaselinePreset(string presetDefName);

    // Packs
    void ToggleVoicePack(SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey, bool enabled);
    void ForgetUnavailable(SqueakVoicePackScope scope, string raceDefName, string targetDefName);
}
