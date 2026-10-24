using FerriteLib.UiKit.Kernel;
using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>Read-only projection of the VoicePack settings page. Built from settings + catalog each frame.</summary>
public sealed class VoicePacksViewState
{
    public SqueakVoicePackMode Mode { get; }
    public bool AllowEasterEggs { get; }
    public SqueakDistancePreset DistancePreset { get; }
    public bool ScaleFrequencyWithTalking { get; }

    public EatPhase EatPhase { get; }
    public EatNutritionScope EatNutritionScope { get; }
    public EatDrugScope EatDrugScope { get; }
    public bool? SelectRequirePlayerControlled { get; }
    public bool? SelectRequireAwake { get; }
    public bool? SelectRequireNotDowned { get; }
    public bool ShowCameraIndicator { get; }
    public SqueakDevLoggingMode DevLoggingMode { get; }
    public bool LocalizeDebugActions { get; }
    public float GlobalVolumeFactor { get; }
    public float DistanceRangeMin { get; }
    public float DistanceRangeMax { get; }
    public bool BiotechActive { get; }
    public string BannerText { get; }
    public IReadOnlyList<RaceLayerRowView> Races { get; }
    public IReadOnlyList<VoicePackDomainView> XenotypeDomains { get; }
    public VoicePackDomainView? SelectedDomain { get; }
    public IReadOnlyList<ActionScopeRowView> ActionScopes { get; }
    public IReadOnlyList<BaselinePresetView> BaselinePresets { get; }
    public int TuningLayer { get; }
    public string TuningRaceDefName { get; }
    public string TuningXenotypeDefName { get; }
    public IReadOnlyList<TuningDomainOptionView> TuningDomains { get; }
    public string BuildIdentity { get; }
    public string SaveStatus { get; }
    public bool IsDirty { get; }
    public IReadOnlyList<string> Authors { get; }
    public string RaceFilter { get; }
    public string XenotypeFilter { get; }
    public IReadOnlyList<FilterOptionView> RaceFilterOptions { get; }
    public IReadOnlyList<FilterOptionView> XenotypeFilterOptions { get; }

    /// <summary>US-PACK1 (§4.2): the ONE result body - every pack that survives the composed filter,
    /// aggregated into a card whose rows are the race/xenotype domains it provides. The card order and
    /// the row order are deterministic (Ordinal on the identity); expansion is the manual set union the
    /// query's xenotype-hit auto-expansion. Read-only projection; enable writes still ride the per-domain
    /// identity through ToggleVoicePack unchanged.</summary>
    public IReadOnlyList<PackCardView> PackCards { get; }

    /// <summary>The filter narrowed the result to zero cards while packs exist (empty-result note).</summary>
    public bool PackResultsEmpty { get; }

    /// <summary>The catalog carries no pack at all - the page's no-content sentence, distinct from an
    /// over-narrowed filter.</summary>
    public bool PackNoPacks { get; }

    /// <summary>VF1定稿: the tuning page's three internal areas (0 actions / 1 moods / 2 fallback) and the
    /// fallback editor's projection - races (maintainer ∪ player tables), the selected race's closed-17
    /// entry rows, query-filtered sound candidates (labelled with the CURRENT production
    /// availability, never the retired blanket verdict) and create candidates taken from the loaded
    /// pawn ThingDefs, not from the pack catalog. All read-only; writes ride the source's commands.</summary>
    public int TuningArea { get; }
    public IReadOnlyList<FallbackRaceView> FallbackRaces { get; }
    public string FallbackSelectedRace { get; }
    public IReadOnlyList<FallbackEntryView> FallbackEntries { get; }
    public IReadOnlyList<FilterOptionView> FallbackSoundOptions { get; }
    public IReadOnlyList<FilterOptionView> FallbackCandidateOptions { get; }
    public string FallbackStatusKey { get; }

    public VoicePacksViewState(
        SqueakVoicePackMode mode,
        bool allowEasterEggs,
        SqueakDistancePreset distancePreset,
        bool scaleFrequencyWithTalking,
        bool showCameraIndicator,
        SqueakDevLoggingMode devLoggingMode,
        bool localizeDebugActions,
        float globalVolumeFactor,
        float distanceRangeMin,
        float distanceRangeMax,
        bool biotechActive,
        string bannerText,
        IReadOnlyList<RaceLayerRowView> races,
        IReadOnlyList<VoicePackDomainView> xenotypeDomains,
        VoicePackDomainView? selectedDomain,
        IReadOnlyList<ActionScopeRowView> actionScopes,
        int tuningLayer,
        string tuningRaceDefName,
        string tuningXenotypeDefName,
        IReadOnlyList<TuningDomainOptionView> tuningDomains,
        IReadOnlyList<BaselinePresetView> baselinePresets,
        string buildIdentity,
        string saveStatus,
        bool isDirty,
        IReadOnlyList<string> authors,
        string raceFilter,
        string xenotypeFilter,
        IReadOnlyList<FilterOptionView> raceFilterOptions,
        IReadOnlyList<FilterOptionView> xenotypeFilterOptions,
        EatPhase eatPhase = EatPhase.WholeProcess,
        EatNutritionScope eatNutritionScope = EatNutritionScope.Any,
        EatDrugScope eatDrugScope = EatDrugScope.Any,
        IReadOnlyList<PackCardView>? packCards = null,
        bool packResultsEmpty = false,
        bool packNoPacks = false,
        int tuningArea = 0,
        IReadOnlyList<FallbackRaceView>? fallbackRaces = null,
        string fallbackSelectedRace = "",
        IReadOnlyList<FallbackEntryView>? fallbackEntries = null,
        IReadOnlyList<FilterOptionView>? fallbackSoundOptions = null,
        IReadOnlyList<FilterOptionView>? fallbackCandidateOptions = null,
        string fallbackStatusKey = "",
        UsRaceBoardView? raceBoard = null,
        bool? selectRequirePlayerControlled = null, bool? selectRequireAwake = null, bool? selectRequireNotDowned = null)
    {
        Mode = mode;
        AllowEasterEggs = allowEasterEggs;
        DistancePreset = distancePreset;
        ScaleFrequencyWithTalking = scaleFrequencyWithTalking;
        ShowCameraIndicator = showCameraIndicator;
        DevLoggingMode = devLoggingMode;
        LocalizeDebugActions = localizeDebugActions;
        GlobalVolumeFactor = globalVolumeFactor;
        DistanceRangeMin = distanceRangeMin;
        DistanceRangeMax = distanceRangeMax;
        BiotechActive = biotechActive;
        BannerText = bannerText ?? "";
        Races = races ?? Array.Empty<RaceLayerRowView>();
        XenotypeDomains = xenotypeDomains ?? Array.Empty<VoicePackDomainView>();
        SelectedDomain = selectedDomain;
        ActionScopes = actionScopes ?? Array.Empty<ActionScopeRowView>();
        TuningLayer = tuningLayer;
        TuningRaceDefName = tuningRaceDefName ?? "";
        TuningXenotypeDefName = tuningXenotypeDefName ?? "";
        TuningDomains = tuningDomains ?? Array.Empty<TuningDomainOptionView>();
        BaselinePresets = baselinePresets ?? Array.Empty<BaselinePresetView>();
        BuildIdentity = buildIdentity ?? "";
        SaveStatus = saveStatus ?? "";
        IsDirty = isDirty;
        Authors = authors ?? Array.Empty<string>();
        RaceFilter = raceFilter ?? "";
        XenotypeFilter = xenotypeFilter ?? "";
        RaceFilterOptions = raceFilterOptions ?? Array.Empty<FilterOptionView>();
        XenotypeFilterOptions = xenotypeFilterOptions ?? Array.Empty<FilterOptionView>();
        EatPhase = eatPhase;
        EatNutritionScope = eatNutritionScope; EatDrugScope = eatDrugScope;
        PackCards = packCards ?? Array.Empty<PackCardView>();
        PackResultsEmpty = packResultsEmpty;
        PackNoPacks = packNoPacks;
        TuningArea = tuningArea;
        FallbackRaces = fallbackRaces ?? Array.Empty<FallbackRaceView>();
        FallbackSelectedRace = fallbackSelectedRace ?? "";
        FallbackEntries = fallbackEntries ?? Array.Empty<FallbackEntryView>();
        FallbackSoundOptions = fallbackSoundOptions ?? Array.Empty<FilterOptionView>();
        FallbackCandidateOptions = fallbackCandidateOptions ?? Array.Empty<FilterOptionView>();
        FallbackStatusKey = fallbackStatusKey ?? "";
        RaceBoard = raceBoard ?? UsRaceBoardView.Empty;
        SelectRequirePlayerControlled = selectRequirePlayerControlled;
        SelectRequireAwake = selectRequireAwake;
        SelectRequireNotDowned = selectRequireNotDowned;
    }

    /// <summary>UIR-002: the ALL-loaded-race board rows (read-only facts; the board never writes).
    /// Ordered by the def adapter (ordinal on defName); deliberately wider than the pack-declared
    /// race subset the filter dropdowns enumerate.</summary>
    public UsRaceBoardView RaceBoard { get; private set; } = UsRaceBoardView.Empty;
}

/// <summary>Simple display/value pair used by the pack-page filter dropdowns.</summary>
public readonly struct FilterOptionView
{
    public readonly string DisplayName;
    public readonly string Value;

    public FilterOptionView(string displayName, string value)
    {
        DisplayName = displayName ?? value ?? "";
        Value = value ?? "";
    }
}

/// <summary>One race domain row in the Race layer.</summary>
public readonly struct RaceLayerRowView
{
    public readonly string RaceDefName;
    public readonly string DisplayName;
    public readonly int EnabledCount;
    public readonly int CandidateCount;
    public readonly SqueakVoicePackDomainState State;

    public RaceLayerRowView(string raceDefName, string displayName, int enabledCount, int candidateCount, SqueakVoicePackDomainState state)
    {
        RaceDefName = raceDefName ?? "";
        DisplayName = displayName ?? raceDefName ?? "";
        EnabledCount = enabledCount;
        CandidateCount = candidateCount;
        State = state;
    }
}

/// <summary>One selectable VoicePack domain (Race or Xenotype). Components receive this as read-only state.</summary>
public readonly struct VoicePackDomainView
{
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    /// <summary>The race's translated label used when this domain is shown to the player; falls back
    /// to <see cref="RaceDefName"/> when empty so harness fixtures keep working unchanged.</summary>
    public readonly string RaceDisplay;
    public readonly string TargetDefName;
    public readonly string DisplayName;
    public readonly string SourceText;
    public readonly SqueakVoicePackDomainState State;
    public readonly bool IsDormant;
    public readonly bool IsTargetUnavailable;
    public readonly bool HasCanonicalConflict;
    public readonly int EnabledCount;
    public readonly int CandidateCount;
    public readonly int OrphanCount;
    public readonly IReadOnlyList<string> EnabledKeys;
    public readonly IReadOnlyList<VoicePackRowView> Packs;

    public VoicePackDomainView(
        SqueakVoicePackScope scope,
        string raceDefName,
        string targetDefName,
        string displayName,
        string sourceText,
        SqueakVoicePackDomainState state,
        bool isDormant,
        bool isTargetUnavailable,
        bool hasCanonicalConflict,
        int enabledCount,
        int candidateCount,
        int orphanCount,
        IReadOnlyList<string> enabledKeys,
        IReadOnlyList<VoicePackRowView> packs,
        string? raceDisplay = null)
    {
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
        DisplayName = displayName ?? targetDefName ?? "";
        SourceText = sourceText ?? "";
        State = state;
        IsDormant = isDormant;
        IsTargetUnavailable = isTargetUnavailable;
        HasCanonicalConflict = hasCanonicalConflict;
        EnabledCount = enabledCount;
        CandidateCount = candidateCount;
        OrphanCount = orphanCount;
        EnabledKeys = enabledKeys ?? Array.Empty<string>();
        Packs = packs ?? Array.Empty<VoicePackRowView>();
        RaceDisplay = raceDisplay == null || raceDisplay.Length == 0 ? RaceDefName : raceDisplay;
    }

    public string DomainIdentity => (Scope == SqueakVoicePackScope.Xenotype ? TargetDefName : RaceDefName) ?? "";
}

/// <summary>VF1: one race row in the final-fallback editor's race list.</summary>
public readonly struct FallbackRaceView
{
    public readonly string DefName;
    public readonly string Label;
    /// <summary>True when no shipped profile Def owns this race - it exists because a player made it.</summary>
    public readonly bool IsPlayerTable;
    /// <summary>How many of the closed action keys the table resolves to a sound.</summary>
    public readonly int SoundCount;

    public FallbackRaceView(string defName, string label, bool isPlayerTable, int soundCount)
    {
        DefName = defName ?? "";
        Label = label ?? defName ?? "";
        IsPlayerTable = isPlayerTable;
        SoundCount = soundCount;
    }
}

/// <summary>VF1: one action row inside the fallback table editor. State vocabulary is the kernel's:
/// 0 = unset (no entry), 1 = player override, 2 = maintainer data, 3 = entry present but the sound
/// is loaded-empty/failed (the NORMAL missing-sound state - short note, never an error).</summary>
public readonly struct FallbackEntryView
{
    public const int Unset = 0, PlayerOverride = 1, Maintainer = 2, NoSound = 3;

    public readonly string ActionKey;
    public readonly string ActionLabel;
    public readonly string SoundDefName;
    public readonly string SoundLabel;
    public readonly int State;

    public FallbackEntryView(string actionKey, string actionLabel, string soundDefName, string soundLabel, int state)
    {
        ActionKey = actionKey ?? "";
        ActionLabel = actionLabel ?? actionKey ?? "";
        SoundDefName = soundDefName ?? "";
        SoundLabel = soundLabel ?? "";
        State = state;
    }
}
/// <summary>S5 分层：动作 × 当前层的作用域行。行携带本层记录与有效作用域。
/// Carries the built-in <see cref="string"/> so the widget can skip scope states the action
/// does not support (e.g. Draft/Undraft/Equip are ActiveCommand-only).</summary>
public readonly struct ActionScopeRowView
{
    public readonly string ActionKey;
    public readonly string DisplayName;
    /// <summary>SA1.2 分组（SR 正本成员表，非 scope 推导）：玩家可主动触发（PlayerTriggered）
    /// 或系统/事件触发（SystemOrEvent）。纯呈现归属，不影响 typed 值与资格判定。</summary>
    public readonly ActionScopeGroup Group;
    /// <summary>本层记录的作用域（HasOwnScope=false 时无意义）。</summary>
    public readonly BehaviorScope Scope;
    public readonly string Action;
    /// <summary>S5 分层：本层是否存在显式记录（false = 继承底层）。</summary>
    public readonly bool HasOwnScope;
    /// <summary>S5 分层：有效作用域（DefaultScope &lt; Global &lt; Race &lt; Xenotype，字段级 last-wins）。</summary>
    public readonly BehaviorScope EffectiveScope;
    public readonly string Explanation;

    // PRE1/VF1定稿 A1: the interval and probability multipliers ride the SAME fold the runtime uses
    // (field-level last-wins over Default < Global < Race < Xenotype). Before this projection the
    // multipliers were fully live in data and runtime but invisible in the UI - the maintainer's own
    // comment in GameSettings.cs named that fact. Own* = this layer's record (null =
    // inherit); Effective* = the folded answer; the source layers say WHERE the effective value came
    // from (0 Global / 1 Race / 2 Xenotype / -1 default).
    /// <summary>Reset-to-preset anchor state, same vocabulary as the mood rows: whether an anchor
    /// exists, whether it resolves to an entry for THIS action, and the target's display label.</summary>
    public readonly bool HasPresetAnchor;
    public readonly bool PresetResetReady;
    public readonly string ResetPresetTarget;

    public ActionScopeRowView(
        string actionKey, string displayName, ActionScopeGroup group, BehaviorScope scope, string action,
        bool hasOwnScope = false, BehaviorScope effectiveScope = default,
        bool hasPresetAnchor = false, bool presetResetReady = false, string resetPresetTarget = "", string explanation = "")
    {
        ActionKey = actionKey ?? "";
        DisplayName = displayName ?? actionKey ?? "";
        Group = group;
        Scope = scope;
        Action = action;
        HasOwnScope = hasOwnScope;
        // 无条件保存有效值：本层有记录时，更高层（Race/Xeno）的覆盖也必须展示（折叠只认层优先级，与列表顺序无关）。
        EffectiveScope = effectiveScope;
        Explanation = explanation;
        HasPresetAnchor = hasPresetAnchor;
        PresetResetReady = presetResetReady;
        ResetPresetTarget = resetPresetTarget ?? "";
    }
}

/// <summary>One VoicePack row inside a domain checklist.</summary>
public readonly struct VoicePackRowView
{
    public readonly string Key;
    public readonly string Label;
    public readonly string ModName;
    public readonly string Author;
    public readonly string DefName;
    public readonly string Coverage;
    public readonly string SearchText;
    public readonly bool IsSelected;

    public VoicePackRowView(
        string key,
        string label,
        string modName,
        string author,
        string defName,
        string coverage,
        string searchText,
        bool isSelected)
    {
        Key = key ?? "";
        Label = label ?? defName ?? key ?? "";
        ModName = modName ?? "";
        Author = author ?? "";
        DefName = defName ?? "";
        Coverage = coverage ?? "";
        SearchText = searchText ?? "";
        IsSelected = isSelected;
    }
}

/// <summary>
/// US-PACK1: one domain row INSIDE a pack card - the (scope, race, xenotype) identity the pack provides,
/// with that identity's own enable answer for THIS pack. Two rows of one card are two domains of one pack;
/// their switches write two independent domain selections, which is what keeps the cross-domain
/// non-crosstalk contract visible on screen (§4.2: no cross-domain global pack switch is added).
/// <c>AutoTrigger</c> marks the row whose XENOTYPE domain the active conditions name - the keyword
/// matched its DOMAIN half, or the xenotype dropdown is set and this row survived it (review-1: the
/// dropdown alone must expose the matching content too). That mark is what auto-expands the owning
/// card without touching the manual expansion set (§1/§4.2 ruling).
/// </summary>
public readonly struct PackCardDomainRowView
{
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    public readonly string TargetDefName;
    /// <summary>Translated domain name shown on the row (race label, or the xenotype label).</summary>
    public readonly string DisplayName;
    /// <summary>True when this row's domain currently has the pack enabled.</summary>
    public readonly bool IsEnabled;
    /// <summary>True when the active xenotype condition (keyword domain hit or the xenotype dropdown)
    /// names this row's domain - the per-row auto-expand trigger, never a manual-set write.</summary>
    public readonly bool AutoTrigger;

    public PackCardDomainRowView(
        SqueakVoicePackScope scope, string raceDefName, string targetDefName, string displayName,
        bool isEnabled, bool autoTrigger)
    {
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
        DisplayName = displayName ?? "";
        IsEnabled = isEnabled;
        AutoTrigger = autoTrigger;
    }

    /// <summary>The row's Repeat identity: the same '|'-joined shape the browse rows used, extended with
    /// the pack key - one flat key per CARD-ROW pair, so a pack appearing in two domains is two distinct
    /// item scopes and their switches can never share state.</summary>
    public string RowKey(string packKey)
        => Scope == SqueakVoicePackScope.Xenotype
            ? packKey + "|" + RaceDefName + "|" + TargetDefName
            : packKey + "|" + RaceDefName;
}

/// <summary>
/// US-PACK1: one pack card. The header half is the pack identity (label, DefName, Mod, author, coverage);
/// the body is its domain rows. <see cref="Expanded"/> is the ANSWER the screen draws - the manual set's
/// membership OR a query hit on one of its rows - while <see cref="ManualExpanded"/> stays the user's own
/// gesture, so clearing the keyword provably restores exactly what the player opened.
/// </summary>
public readonly struct PackCardView
{
    public readonly string Key;
    public readonly string Label;
    public readonly string DefName;
    public readonly string ModName;
    public readonly string Author;
    public readonly string Coverage;
    /// <summary>True when the player opened this card (the manual set's membership).</summary>
    public readonly bool ManualExpanded;
    /// <summary>True when the card is open because the keyword hit one of its domain rows.</summary>
    public readonly bool AutoExpanded;
    /// <summary>Manual OR auto - the single answer the body's visibility reads.</summary>
    public readonly bool Expanded;
    /// <summary>The domains that survived the composed filter for this pack (source order).</summary>
    public readonly IReadOnlyList<PackCardDomainRowView> Rows;

    public PackCardView(
        string key, string label, string defName, string modName, string author, string coverage,
        bool manualExpanded, bool autoExpanded, IReadOnlyList<PackCardDomainRowView> rows)
    {
        Key = key ?? "";
        Label = label ?? defName ?? key ?? "";
        DefName = defName ?? "";
        ModName = modName ?? "";
        Author = author ?? "";
        Coverage = coverage ?? "";
        ManualExpanded = manualExpanded;
        AutoExpanded = autoExpanded;
        Expanded = manualExpanded || autoExpanded;
        Rows = rows ?? Array.Empty<PackCardDomainRowView>();
    }

    /// <summary>At least one of the pack's domains currently enables it (the header's enable badge).</summary>
    public bool AnyDomainEnabled
    {
        get
        {
            for (int i = 0; i < Rows.Count; i++) if (Rows[i].IsEnabled) return true;
            return false;
        }
    }
}

/// <summary>One tuning-baseline preset projected for the import UI.</summary>
public sealed class BaselinePresetView
{
    public readonly string DefName;
    public readonly string Label;
    public readonly string Description;
    public readonly bool Expanded;
    public readonly IReadOnlyList<BaselineRaceView> Races;
    public readonly int SelectedRaceCount;
    public readonly int SelectedXenotypeCount;

    public BaselinePresetView(string defName, string label, string description, bool expanded,
        IReadOnlyList<BaselineRaceView> races, int selectedRaceCount, int selectedXenotypeCount)
    {
        DefName = defName ?? "";
        Label = label ?? defName ?? "";
        Description = description ?? "";
        Expanded = expanded;
        Races = races ?? Array.Empty<BaselineRaceView>();
        SelectedRaceCount = selectedRaceCount;
        SelectedXenotypeCount = selectedXenotypeCount;
    }
}

/// <summary>One race row inside a baseline preset tree.</summary>
public sealed class BaselineRaceView
{
    public readonly string RaceDefName;
    public readonly string DisplayName;
    public readonly bool Selected;
    public readonly int ActionCount;
    public readonly int FieldCount;
    public readonly IReadOnlyList<BaselineXenotypeView> Xenotypes;

    public BaselineRaceView(string raceDefName, string displayName, bool selected, int actionCount, int fieldCount, IReadOnlyList<BaselineXenotypeView> xenotypes)
    {
        RaceDefName = raceDefName ?? "";
        DisplayName = displayName ?? raceDefName ?? "";
        Selected = selected;
        ActionCount = actionCount;
        FieldCount = fieldCount;
        Xenotypes = xenotypes ?? Array.Empty<BaselineXenotypeView>();
    }
}

/// <summary>One xenotype row inside a baseline preset race block.</summary>
public sealed class BaselineXenotypeView
{
    public readonly string XenotypeDefName;
    public readonly string DisplayName;
    public readonly bool InheritFromRace;
    public readonly bool Selected;
    public readonly int ActionCount;
    public readonly int FieldCount;

    /// <summary>
    /// The xenotype's native icon, or null when there is none to show. This is the R4-B adapter boundary:
    /// the Verse-side model resolves it (<c>XenotypeDef.Icon</c>) and hands it over as a VALUE, so the
    /// Kernel widget never performs a resource lookup. Null is a first-class answer - an absent
    /// definition, no Biotech and a missing texture all land here - and the drawing side must fall back
    /// rather than paint a substitute glyph.
    /// </summary>
    public readonly UiImage Image;

    public BaselineXenotypeView(string xenotypeDefName, string displayName, bool inheritFromRace, bool selected, int actionCount, int fieldCount, UiImage image = default)
    {
        XenotypeDefName = xenotypeDefName ?? "";
        DisplayName = displayName ?? xenotypeDefName ?? "";
        InheritFromRace = inheritFromRace;
        Selected = selected;
        ActionCount = actionCount;
        FieldCount = fieldCount;
        Image = image;
    }
}

/// <summary>S5 调音层域选项：Race 层 = 单 race；Xenotype 层 = (race, xenotype) 域。</summary>
public readonly struct TuningDomainOptionView
{
    public readonly string RaceDefName;
    public readonly string TargetDefName;
    public readonly string DisplayName;

    public TuningDomainOptionView(string raceDefName, string displayName, string targetDefName = "")
    {
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
        DisplayName = displayName ?? raceDefName ?? targetDefName ?? "";
    }
}

/// <summary>S5 分层心情编辑器行：本层**末行**记录（Own，列表序最后一个匹配行）+ 有效值（编辑器显示/滑块起点）。
/// <para><b>F-Q：<c>Own != null</c> 不等于「本层有覆盖」。</b>clear 之后带来源的心情行仍留在清单里
/// （F-P，来源是「重置为预设」的锚点），此时 Own 指向一行三旗标全 false 的记录。读 Own 的<b>值</b>前必须先看
/// <c>hasPitchFactor</c>/<c>hasVolumeFactor</c>/<c>hasPitchJitter</c> 或 <c>sourcePresetDefName</c>；
/// 现状消费者 <c>UsScopeTreeWidget</c> 已按 <c>hasXFactor == true</c> 门控，null 只表示本层没有行。</para></summary>
