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

    // View/navigation state (US-owned, per window)
    void SetActiveTab(string tab);
    void ScrollToSection(string sectionKey);
    void SetTuningLayer(int layer);
    void SetTuningDomain(string raceDefName, string targetDefName);
    void SelectDomain(SqueakVoicePackScope scope, string raceDefName, string targetDefName);
    void SetDomainFilter(SqueakDomainFilterKind kind, bool flag);
    void SetPackFilter(string author);
    void SetRaceFilter(string raceDefName);
    void SetXenotypeFilter(string xenotypeDefName);
    void SetSearchText(string text);
    void SetHelpHover(string key);

    /// <summary>Per-frame hover-claim boundary (D10): run before every DrawFrame; applies the
    /// clear-or-hold-or-grace rule from <see cref="VoicePacksPageModel.BeginHelpHoverFrame"/>.</summary>
    void BeginHelpHoverFrame();

    // Tuning
    void SetActionScope(string actionKey, SqueakActionScope? scope);
    void SetMoodTuning(SqueakMood mood, SqueakMoodFactor factor, float? value);
    void ToggleBaselinePreset(string presetDefName);
    void ToggleBaselineRace(string presetDefName, string raceDefName, bool selected);
    void ToggleBaselineXenotype(string presetDefName, string raceDefName, string xenotypeDefName, bool selected);
    void ImportBaselinePreset(string presetDefName);

    // Packs
    void ToggleVoicePack(SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey, bool enabled);
    void ForgetUnavailable(SqueakVoicePackScope scope, string raceDefName, string targetDefName);
}
