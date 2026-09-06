using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Page-local UI ephemeral state. It is not persisted and is reset on settings session begin/end.
/// The view model is rebuilt from settings/catalog each frame; this state only remembers what the
/// user is looking at (selected domain, search text, help, scroll) and the baseline-preset import
/// checkboxes.
/// </summary>
public sealed class VoicePacksPageState
{
    public SqueakVoicePackScope SelectedScope = SqueakVoicePackScope.Race;
    public string SelectedRaceDefName = "";
    public string SelectedTargetName = "";
    public string SearchText = "";
    public string HelpHoverKey = "";

    // D10 grace: the last claim that actually landed, the frames left to hold it after the
    // pointer stopped claiming, and the frame counter + claim stamp that distinguish "a widget
    // claimed during last frame's draw" from "BeginHelpHoverFrame restored the held value". Owned
    // by VoicePacksPageModel; the help panel keeps explaining the previous control during transit
    // gaps instead of flashing the section overview (maintainer ruling 2026-09-06).
    public string HelpHoverHeld = "";
    public int HelpHoverGraceLeft;
    public int HelpHoverFrame;
    public int HelpHoverClaimStamp;
    public Vector2 ScrollPosition;
    public Vector2 HelpScrollPosition;
    public readonly Dictionary<string, BaselinePresetSelection> BaselinePresets = new(StringComparer.Ordinal);

    // S5 调音编辑器（选项 ①）：当前编辑层（0=Global,1=Race,2=Xenotype）与层域身份。
    public int TuningLayer;
    public string TuningRaceDefName = "";
    public string TuningXenotypeDefName = "";
    public string ActiveTab = "Overview";
    public string ActiveSectionKey = "mode-row";
    public string ScrollTargetKey = "";
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
        HelpHoverKey = "";
        HelpHoverHeld = "";
        HelpHoverGraceLeft = 0;
        HelpHoverFrame = 0;
        HelpHoverClaimStamp = 0;
        ScrollPosition = Vector2.zero;
        HelpScrollPosition = Vector2.zero;
        BaselinePresets.Clear();
        TuningLayer = 0;
        TuningRaceDefName = "";
        TuningXenotypeDefName = "";
        ActiveTab = "Overview";
        ActiveSectionKey = "mode-row";
        ScrollTargetKey = "";
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
