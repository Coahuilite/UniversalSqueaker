namespace UniversalSqueaker.UI;

/// <summary>
/// Business/view boundary of the kernel settings path. The Host adapter binds every typed widget
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
    /// Retractable help drawer visibility. Independent per-window view state, never the engine's
    /// active-tab gate: the Host writes it through its own "help-open" binding, and the revision
    /// bumper turns the change into the matching layout variant at the next arrange.
    /// </summary>
    void SetHelpDrawerOpen(bool open);
    void SetTuningLayer(int layer);
    void SetTuningDomain(string raceDefName, string targetDefName);
    void SelectDomain(SqueakVoicePackScope scope, string raceDefName, string targetDefName);
    void SetDomainFilter(SqueakDomainFilterKind kind, bool flag);
    void SetPackFilter(string author);
    void SetRaceFilter(string raceDefName);
    void SetXenotypeFilter(string xenotypeDefName);
    void SetSearchText(string text);
    // No help-hover channel here any more (FL 0.3.0 P3): the claim is UiSession state, not business
    // state, so it never crosses this boundary. SectionHelpKey stays - that IS business resolution.

    // Developer layout diagnosis (R3-B). These are per-WINDOW developer switches, not settings: they are
    // never persisted, never touch Config or a save, and they are deliberately NOT routed through
    // UniversalSqueakerSettings. The source holds the request; the window's own diagnostic scope applies it.
    /// <summary>Turn the layout capture on or off for this window's host. Off by default.</summary>
    void SetLayoutCapture(bool on);

    /// <summary>Turn the captured-rect outline on or off for this window's host.</summary>
    void SetLayoutOutline(bool on);

    /// <summary>Ask for exactly one layout report; the next drawn pass produces it and clears the request.</summary>
    void RequestLayoutReport();

    /// <summary>
    /// Read-only status of the layout capture for this window, as a user-facing sentence. A carrier without
    /// the development instrument reports that plainly, so a developer never reads "nothing captured" as
    /// "the layout is clean".
    /// </summary>
    string LayoutDiagnosisStatus { get; }

    /// <summary>Whether capture is currently on for this window (the switch's own checked state).</summary>
    bool LayoutCaptureOn { get; }

    /// <summary>Whether the captured-rect outline is currently on for this window.</summary>
    bool LayoutOutlineOn { get; }

    // Tuning
    void SetActionScope(string actionKey, SqueakActionScope? scope);
    void SetMoodTuning(SqueakMood mood, SqueakMoodFactor factor, float? value);

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
