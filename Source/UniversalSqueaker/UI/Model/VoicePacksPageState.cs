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
    // US-ESC1 (§4.1): the "user has cancelled" marks. The projections auto-select a first item on FIRST
    // entry (the accepted initial default); an Escape that returns from a selection must NOT be undone by
    // the next BuildView, so the cancel is stored as an explicit state, not by blanking the fields alone.
    // A new selection or a workspace switch re-establishes the branch and clears the mark. Never persisted.
    public bool DomainSelectionCanceled;
    // US-ESC1 review1 (PM observation 1): the tuning branch's CONTEXT is its own return layer, expressed
    // as business state rather than inferred from a non-zero layer/area - a user operating on the default
    // Global layer must also be able to leave the context before the window closes. Active from the moment
    // the branch is entered or touched; false ONLY after the cancel answered. Never persisted.
    public bool TuningContextActive = true;
    public bool FallbackTableCanceled;
    public string SearchText = "";
    // US-PACK1 (§4.2): the MANUAL card-expansion set (pack key = card identity). It is the user's own
    // browsing state and the ONLY thing cancel-pack-results collapses. The query's xenotype-hit
    // auto-expansion is never stored here - it is derived from SearchText per projection, so clearing
    // the query restores exactly this manual set. Page state, never persisted, cleared on Reset.
    public readonly HashSet<string> PackCardsExpanded = new(StringComparer.Ordinal);
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

    // Packs 页统一筛选区（US-PACK1）：作者/种族/异种下拉与状态旗标共同缩小唯一的包卡片结果区。
    // D4 的两条独立列表搜索随并列浏览卡退役：一个关键词字段（SearchText）现在承担全部文本命中。
    public string RaceFilter = "";
    public string XenotypeFilter = "";

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
        PackCardsExpanded.Clear();
        TuningArea = 0;
        TuningSelectedAction = "";
        FallbackSelectedRace = "";
        FallbackSelectedEntryAction = "";
        FallbackSoundQuery = "";
        FallbackNewRaceQuery = "";
        FallbackStatusKey = "";
        FallbackStatusArg = "";
        DomainSelectionCanceled = false;
        FallbackTableCanceled = false;
        TuningContextActive = true;
    }
}

/// <summary>Per-preset UI selection state (expanded flag + checked race/xenotype rows). Not persisted.</summary>
public sealed class BaselinePresetSelection
{
    public bool Expanded;
    public readonly HashSet<string> SelectedRaceDefNames = new(StringComparer.Ordinal);
    public readonly HashSet<string> SelectedXenotypeDomainKeys = new(StringComparer.Ordinal);
}
