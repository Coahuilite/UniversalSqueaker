using UnityEngine;

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
    private int cachedFrame = -1;

    public UsKernelSettingsSource(UniversalSqueakerSettings settings)
    {
        this.settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        state = new VoicePacksPageState();
    }

    public VoicePacksPageState ViewState => state;

    public VoicePacksViewState BuildView()
    {
        int frame = Time.frameCount;
        if (cachedView == null || frame != cachedFrame)
        {
            cachedView = VoicePacksPageModel.BuildView(settings, SqueakXenotypeCatalog.Current, state);
            cachedFrame = frame;
        }

        return cachedView;
    }

    public string SectionHelpKey(string sectionKey)
    {
        return VoicePacksPageModel.SectionHelpKeyOf(sectionKey);
    }

    public string BuildIdentity => UniversalSqueakerMod.Instance != null ? UniversalSqueakerMod.BuildIdentity() : "unknown";

    public string SaveStatus => UniversalSqueakerMod.Instance?.SaveState.ToString() ?? "Unknown";

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

    public void SetActiveTab(string tab)
    {
        VoicePacksPageModel.SetActiveTab(state, tab);
    }

    public void ScrollToSection(string sectionKey)
    {
        VoicePacksPageModel.ScrollToSection(state, sectionKey);
    }

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

    public void SetHelpHover(string key)
    {
        VoicePacksPageModel.SetHelpHover(state, key);
    }

    public void SetHelpSelection(string key)
    {
        VoicePacksPageModel.SetHelpSelection(state, key);
    }

    public void SetActionScope(string actionKey, SqueakActionScope? scope)
    {
        VoicePacksPageModel.SetActionScope(settings, state, actionKey, scope);
    }

    public void SetMoodTuning(SqueakMood mood, SqueakMoodFactor factor, float? value)
    {
        VoicePacksPageModel.SetMoodTuning(settings, state, mood, factor, value);
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
