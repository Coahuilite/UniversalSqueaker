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
    public string SelectedTargetName = "";
    public string SearchText = "";
    public bool HelpOpen;
    public Vector2 ScrollPosition;
    public readonly Dictionary<string, BaselinePresetSelection> BaselinePresets = new(StringComparer.Ordinal);

    // S5 调音编辑器（选项 ①）：当前编辑层（0=Global,1=Race,2=Xenotype）与层域身份。
    public int TuningLayer;
    public string TuningRaceDefName = "";
    public string TuningXenotypeDefName = "";

    public VoicePacksPageState()
    {
    }

    public void Reset()
    {
        SelectedScope = SqueakVoicePackScope.Race;
        SelectedTargetName = "";
        SearchText = "";
        HelpOpen = false;
        ScrollPosition = Vector2.zero;
        BaselinePresets.Clear();
        TuningLayer = 0;
        TuningRaceDefName = "";
        TuningXenotypeDefName = "";
    }
}

/// <summary>Per-preset UI selection state (expanded flag + checked race/xenotype rows). Not persisted.</summary>
public sealed class BaselinePresetSelection
{
    public bool Expanded;
    public readonly HashSet<string> SelectedRaceDefNames = new(StringComparer.Ordinal);
    public readonly HashSet<string> SelectedXenotypeDefNames = new(StringComparer.Ordinal);
}
