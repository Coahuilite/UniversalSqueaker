using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// Page-local UI ephemeral state. It is not persisted and is reset on settings session begin/end.
/// The view model is rebuilt from settings/catalog each frame; this state only remembers what the
/// user is looking at (selected domain, search text) and the baseline-preset import checkboxes.
/// Neither scroll nor the help hover claim lives here: both moved to the session — scroll positions
/// via <c>UiSession.ScrollPositions</c>, the hover claim via <c>UiSession.HoverClaim</c> (FL P3).
/// </summary>
public sealed class VoicePacksPageState
{
    public SqueakVoicePackScope SelectedScope = SqueakVoicePackScope.Race;
    public string SelectedRaceDefName = "";
    public string SelectedTargetName = "";
    public string SearchText = "";
    public readonly Dictionary<string, BaselinePresetSelection> BaselinePresets = new(StringComparer.Ordinal);

    // S5 调音编辑器（选项 ①）：当前编辑层（0=Global,1=Race,2=Xenotype）与层域身份。
    public int TuningLayer;
    public string TuningRaceDefName = "";
    public string TuningXenotypeDefName = "";
    public string ActiveTab = "Overview";
    public string ActiveSectionKey = "mode-row";

    /// <summary>
    /// Bottom help panel visibility (BH1). INDEPENDENT state on purpose: the engine's only
    /// binding-driven visibility switch is the <c>Tab</c> attribute compared against
    /// <c>UiBindings.ActiveTabKey</c>, and help visibility must never follow workspace switching.
    /// Defaults to false: the shipped window opens with the panel retracted above the footer, and
    /// expanding it neither widens nor moves the window - the body row just yields height.
    /// </summary>
    public bool HelpPanelOpen = false;
    public UiDomainFilter DomainFilter;
    public UiPackFilter PackFilter;

    // Packs 页 race/xenotype 并列筛选：空字符串 = All。
    public string RaceFilter = "";
    public string XenotypeFilter = "";

    // D4: each domain list carries its OWN search text (same predicate as the pack search -
    // UsChecklistFilter.QueryMatches: trimmed substring, OrdinalIgnoreCase). Empty = no narrowing.
    public string RaceSearchText = "";
    public string XenotypeSearchText = "";

    // VF1定稿: the tuning page's three internal areas (0 = action rules, 1 = mood tones,
    // 2 = native final fallback) and the editor selections / queries. Page state, never persisted.
    public int TuningArea;
    public string TuningSelectedAction = "";
    public string FallbackSelectedRace = "";
    public string FallbackSelectedEntryAction = "";
    public string FallbackSoundQuery = "";
    public string FallbackStatusKey = "";
    public string FallbackStatusArg = "";

    public string FallbackNewRaceQuery = "";

    public VoicePacksPageState()
    {
    }

    public void Reset()
    {
        SelectedScope = SqueakVoicePackScope.Race;
        SelectedRaceDefName = "";
        SelectedTargetName = "";
        SearchText = "";
        BaselinePresets.Clear();
        TuningLayer = 0;
        TuningRaceDefName = "";
        TuningXenotypeDefName = "";
        ActiveTab = "Overview";
        ActiveSectionKey = "mode-row";
        HelpPanelOpen = false;
        DomainFilter = default;
        PackFilter = default;
        RaceFilter = "";
        XenotypeFilter = "";
        RaceSearchText = "";
        XenotypeSearchText = "";
        TuningArea = 0;
        TuningSelectedAction = "";
        FallbackSelectedRace = "";
        FallbackSelectedEntryAction = "";
        FallbackSoundQuery = "";
        FallbackNewRaceQuery = "";
        FallbackStatusKey = "";
        FallbackStatusArg = "";
    }
}

/// <summary>Per-preset UI selection state (expanded flag + checked race/xenotype rows). Not persisted.</summary>
public sealed class BaselinePresetSelection
{
    public bool Expanded;
    public readonly HashSet<string> SelectedRaceDefNames = new(StringComparer.Ordinal);
    public readonly HashSet<string> SelectedXenotypeDomainKeys = new(StringComparer.Ordinal);
}
