using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// Page-local UI ephemeral state. It is not persisted and is reset on settings session begin/end.
/// The view model is rebuilt on published input/session revisions; this state remembers what the
/// user is looking at (selected domain, search text) and the baseline-preset import checkboxes.
/// Neither scroll nor the help hover claim lives here: both moved to the session — scroll positions
/// via <c>UiSession.ScrollPositions</c>, the hover claim via <c>UiSession.CurrentHelpRef</c> (FL P3).
/// </summary>
public sealed class VoicePacksPageState
{
    // Navigation and layout are per-window, never persisted or used as runtime policy.
    public string Page = "Voices";
    public string LastToolPage = "Behavior";
    public readonly List<string> PageHistory = new();
    public string PinnedHelpRef = "";
    public string PinnedHelpContext = "";
    public bool BrowseDefinitions;
    public bool FiltersOpen;
    public bool RestoreAllOpen;
    public bool WideLayout;
    public bool DetailOpen;
    public bool PackArrivalFromDirectory;
    public string SelectedPackKey = "";
    public int PackPage;
    public int PackTotal;
    public int PackSelected;
    public int OrphanSelectionCount;
    public string MissingSelections = "";
    public string SourceChangeDiagnostic = "";
    public string EnabledFilterIdentity = "";
    public readonly HashSet<string> EnabledFilterRows = new(StringComparer.Ordinal);
    internal IReadOnlyList<PackCardView> AllPackCards = Array.Empty<PackCardView>();
    public UsBoardBrowsePolicy.BoardGroup PackGroup = UsBoardBrowsePolicy.BoardGroup.PlayerRaces;
    public readonly UsBrowseListState PackRaceBrowse = new(), PackXenoBrowse = new(), PackAuthorBrowse = new();
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
    // UIR-002: the first-open teaching is ONE bool of page state. Default true (the teaching shows on
    // arrival), collapse/re-open is a single press, and it is NEVER persisted - a familiarised player is
    // not held hostage by a wizard, and a collapsed player can always find the explanation again.
    public bool IntroOpen = true;

    // OVT-002: the board browse state (session-only view state - never persisted, never a
    // sound/selection write). Default: the PLAYER RACES group, no chip, no search, xenotype
    // directory expanded. A default with no HAR loaded still shows vanilla humanlike + xenotypes;
    // it never degrades to an animal-first screen.
    public readonly UsBrowseListState DirectoryBrowse = new();
    public readonly UsBrowseListState TableBrowse = new(), CandidateBrowse = new(), SoundBrowse = new();
    public UsBrowseListView DirectoryList = UsBrowseListView.Empty, TableList = UsBrowseListView.Empty,
        CandidateList = UsBrowseListView.Empty, SoundList = UsBrowseListView.Empty;
    public string PendingFallbackRace = "";
    internal object? SoundDefinitions;
    internal IReadOnlyList<UsBrowseItem> CoreSounds = Array.Empty<UsBrowseItem>();
    public UsBoardBrowsePolicy.BoardGroup BoardGroup { get => DirectoryBrowse.Group; set => DirectoryBrowse.Group = value; }
    public UsBoardBrowsePolicy.BoardFilter BoardFilter = UsBoardBrowsePolicy.BoardFilter.None;
    public string BoardSearch { get => DirectoryBrowse.Query; set => DirectoryBrowse.Query = value; }
    public bool BoardXenoExpanded = true;
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
    /// Optional bottom sibling of the business body. Closed on a different page; its pinned topic
    /// and session-owned scroll survive closing/reopening the same topic within this window.
    /// </summary>
    public bool HelpPanelOpen = false;
    internal float HelpHeight = 128f;
    internal float HelpViewWidth, HelpViewHeight;
    internal long HelpLayoutRevision = -1;
    internal string HelpLayoutText = "";
    internal object? HelpSmallEnvironment, HelpTinyEnvironment;
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
    public string FallbackSoundQuery { get => SoundBrowse.Query; set { SoundBrowse.Query = value; SoundBrowse.Page = 0; } }
    public string FallbackStatusKey = "";
    public string FallbackStatusArg = "";

    public string FallbackNewRaceQuery { get => CandidateBrowse.Query; set { CandidateBrowse.Query = value; CandidateBrowse.Page = 0; } }

    public VoicePacksPageState()
    {
        TableBrowse.Group = UsBoardBrowsePolicy.BoardGroup.All;
    }

    public void Reset()
    {
        Page = "Voices"; LastToolPage = "Behavior"; PinnedHelpRef = ""; PinnedHelpContext = "";
        HelpHeight = 128f; HelpViewWidth = HelpViewHeight = 0; HelpLayoutRevision = -1; HelpLayoutText = "";
        PageHistory.Clear();
        BrowseDefinitions = false; FiltersOpen = false; RestoreAllOpen = false;
        DetailOpen = false; PackArrivalFromDirectory = false; SelectedPackKey = ""; PackPage = 0;
        PackTotal = 0; PackSelected = 0; OrphanSelectionCount = 0; MissingSelections = "";
        AllPackCards = Array.Empty<PackCardView>(); PackGroup = UsBoardBrowsePolicy.BoardGroup.PlayerRaces;
        PackRaceBrowse.Clear(); PackXenoBrowse.Clear(); PackAuthorBrowse.Clear();
        SourceChangeDiagnostic = ""; EnabledFilterIdentity = ""; EnabledFilterRows.Clear();
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
        IntroOpen = true;
        BoardGroup = UsBoardBrowsePolicy.BoardGroup.PlayerRaces;
        BoardFilter = UsBoardBrowsePolicy.BoardFilter.None;
        BoardSearch = "";
        BoardXenoExpanded = true;
        DirectoryBrowse.Clear(); TableBrowse.Clear(); CandidateBrowse.Clear(); SoundBrowse.Clear();
        TableBrowse.Group = UsBoardBrowsePolicy.BoardGroup.All;
        DirectoryList = TableList = CandidateList = SoundList = UsBrowseListView.Empty;
        PendingFallbackRace = ""; SoundDefinitions = null; CoreSounds = Array.Empty<UsBrowseItem>();
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
