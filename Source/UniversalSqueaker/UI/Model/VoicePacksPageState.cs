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
    public UiDomainFilter DomainFilter;
    public UiPackFilter PackFilter;

    // Packs 页 race/xenotype 并列筛选：空字符串 = All。
    public string RaceFilter = "";
    public string XenotypeFilter = "";

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
        DomainFilter = default;
        PackFilter = default;
        RaceFilter = "";
        XenotypeFilter = "";
    }
}

/// <summary>Per-preset UI selection state (expanded flag + checked race/xenotype rows). Not persisted.</summary>
public sealed class BaselinePresetSelection
{
    public bool Expanded;
    public readonly HashSet<string> SelectedRaceDefNames = new(StringComparer.Ordinal);
    public readonly HashSet<string> SelectedXenotypeDomainKeys = new(StringComparer.Ordinal);
}
