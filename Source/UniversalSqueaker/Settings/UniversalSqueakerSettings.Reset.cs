using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>Result of one restore operation. The return IS the publish contract:
/// <see cref="Applied"/> means the target was fully formed on the live instance and EXACTLY ONE
/// runtime publish plus EXACTLY ONE persistence request went through the existing funnel;
/// <see cref="NoChange"/> and <see cref="Rejected"/> mean ZERO writes, ZERO notifications,
/// ZERO persistence requests (repeated restore is value-idempotent).</summary>
public enum SqueakResetOutcome
{
    NoChange,
    Applied,
    Rejected,
}

/// <summary>US-RESET1: the plain visible settings areas, one per Overview/Distance UI binding
/// cluster (preparation §2/§6.4: distance area = host 397-441, basic toggles = 449-463,
/// timing = 470-508, logging = 515-539). The field set of each area is fixed in
/// <see cref="UniversalSqueakerSettings.ResetNormalArea"/>; no area was invented for framework
/// symmetry. Layout capture/outline flags are NOT here: they are host-session dev state,
/// not persisted settings fields.</summary>
public enum SqueakNormalResetArea
{
    Distance,
    Basics,
    Timing,
    Diagnostics,
}

/// <summary>
/// US-RESET1 生产恢复操作（后端切片）。
///
/// 目标形成规则（preparation §6.1/§6.4，reset1-backend-contract）：恢复目标只来自既有默认来源
/// —— 构造字段初始化（UniversalSqueakerSettings.cs:40-79）、<see cref="GetDistancePresetRange"/>
/// 与 <see cref="GetDefaultScaleFrequencyWithTalking"/>。全局恢复用一个瞬态
/// <c>new UniversalSqueakerSettings()</c> 快照作为只读默认来源（构造无运行时注册/写盘副作用），
/// 沿字段表复制进 live 实例后丢弃；UI 侧不得手抄第二套默认值。
///
/// 身份约束：所有操作都只改 live 实例自身的字段/列表内容，宿主持有的
/// <c>UniversalSqueakerMod.Settings</c> 引用在整个恢复后仍是同一实例。
///
/// 发布规则：先把目标完整形成，再走既有运行时发布/持久化漏斗（ApplyToRuntime /
/// NotifyDiscrete/ContinuousResolverRuntimeChanged / NotifyCheapRuntimeChanged /
/// NotifyDistanceRuntimeChanged / ApplyDevLoggingModeToRuntime + QueuePersistence），每次 Applied
/// 恰好一次持久化请求；没有一串 UI setter 造成的半恢复与多次发布，也没有任何全局可变注册表。
///
/// 边界（§6.2/§6.4/§6.6）：
///  - 全局恢复清普通设置、action/mood 覆盖、包选择、预设记录与来源锚点（含 xenotypePresets
///    域级整体间隔乘数——它是唯一拥有该乘数的恢复档）；保留当前 schema 标记、迁移诊断
///    （wasLoaded/migration pending·blocked/settingsLoadedFromFile）、每一独立 fallback store
///    文件与作者/Def 数据。恢复绝不重写 schema 版本，不伪装迁移边界，也不调用 store 的
///    RestoreDefault/DeletePlayerTable。
///  - 局部行/区域按精确层+域身份处理：非法身份（race 空而 xeno 非空）、空动作键、未知枚举区域
///    一律 Rejected 零副作用；目标域没有行只是 NoChange，绝不回落到 Global 语义。
///  - 局部恢复（行/区域）绝不枚举 xenotypePresets：域级整体乘数在局部保留。
///  - 恢复继承（SetActionTuningScope/SetMoodTuning 的 clear）、重置为预设（Reset*ToPreset）与
///    本文件的恢复默认是三类不同操作，互不混称。
///
/// UI 接线（确认窗、按钮、本地化、无效编辑整合关闭）由主 US 会话集成，不属于本切片；
/// helper 未接线前"确认取消=零写入"属集成验证项，本层只保证 API 层被调用才可能产生写入。
/// </summary>
public partial class UniversalSqueakerSettings
{
    /// <summary>全局恢复：沿 §6.2 字段表把 live 实例逐字段恢复到统一默认来源，然后恰好一次
    /// 运行时发布（dev logging + ApplyToRuntime 离散重建）与一次持久化请求。schema 标记、
    /// 迁移诊断、fallback store 与作者数据不在字段表内，因此保留。
    /// 返回值语义见 <see cref="SqueakResetOutcome"/>。</summary>
    internal SqueakResetOutcome ResetAllSettings()
    {
        // 只读默认来源：瞬态构造（构造字段初始化 = 业务默认单源），用完即弃，
        // 绝不成为宿主 Settings 引用、绝不注册进 ContentHandler。
        UniversalSqueakerSettings defaults = new();
        bool changed = false;

        if (voicePackMode != defaults.voicePackMode) { voicePackMode = defaults.voicePackMode; changed = true; }
        if (scaleCooldownWithTimeSpeed != defaults.scaleCooldownWithTimeSpeed) { scaleCooldownWithTimeSpeed = defaults.scaleCooldownWithTimeSpeed; changed = true; }
        if (scaleFrequencyWithTalking != defaults.scaleFrequencyWithTalking) { scaleFrequencyWithTalking = GetDefaultScaleFrequencyWithTalking(); changed = true; }
        if (scalePeriodicWithAudiblePopulation != defaults.scalePeriodicWithAudiblePopulation) { scalePeriodicWithAudiblePopulation = defaults.scalePeriodicWithAudiblePopulation; changed = true; }
        if (globalMinIntervalTicks != defaults.globalMinIntervalTicks) { globalMinIntervalTicks = defaults.globalMinIntervalTicks; changed = true; }
        if (localizeDebugActions != defaults.localizeDebugActions) { localizeDebugActions = defaults.localizeDebugActions; changed = true; }
        if (showCameraIndicator != defaults.showCameraIndicator) { showCameraIndicator = defaults.showCameraIndicator; changed = true; }
        if (devLoggingMode != defaults.devLoggingMode) { devLoggingMode = defaults.devLoggingMode; changed = true; }
        if (globalCooldownMultiplier != defaults.globalCooldownMultiplier) { globalCooldownMultiplier = defaults.globalCooldownMultiplier; changed = true; }
        if (globalVolumeFactor != defaults.globalVolumeFactor) { globalVolumeFactor = defaults.globalVolumeFactor; changed = true; }
        if (distancePreset != defaults.distancePreset) { distancePreset = defaults.distancePreset; changed = true; }
        if (!DistanceRangesEqual(distanceRange, defaults.distanceRange)) { distanceRange = defaults.distanceRange; changed = true; }
        if (allowEasterEggSounds != defaults.allowEasterEggSounds) { allowEasterEggSounds = defaults.allowEasterEggSounds; changed = true; }
        if (allowExternalActions != defaults.allowExternalActions) { allowExternalActions = defaults.allowExternalActions; changed = true; }
        if (allowBabyActions != defaults.allowBabyActions) { allowBabyActions = defaults.allowBabyActions; changed = true; }
        if (eatPrecisionEnabled != defaults.eatPrecisionEnabled) { eatPrecisionEnabled = defaults.eatPrecisionEnabled; changed = true; }
        if (eatPrecisionIncludeDrugs != defaults.eatPrecisionIncludeDrugs) { eatPrecisionIncludeDrugs = defaults.eatPrecisionIncludeDrugs; changed = true; }
#if US_EXPERIMENTAL
        // 构建变体诚实标注：该字段仅 Dev/EXP 构建存在，ship 构建的字段表天然不含它。
        if (experimentalKiiroCompat != defaults.experimentalKiiroCompat) { experimentalKiiroCompat = defaults.experimentalKiiroCompat; changed = true; }
#endif

        // 集合字段：默认 = 空。null 与空列表语义一致（读侧 ?? 兜底），因此「已是 null/空」不算
        // 变更、不重排列表身份；只有仍承载记录时才换成快照的空实例（与迁移事务的整表替换同形）。
        if (moodOverrides != null && moodOverrides.Count > 0) { moodOverrides = defaults.moodOverrides; changed = true; }
        if (voicePackSelections != null && voicePackSelections.Count > 0) { voicePackSelections = defaults.voicePackSelections; changed = true; }
        // §6.6：xenotypePresets 整列表清空（含 hasOverallIntervalMultiplier/overallIntervalMultiplier
        // 与行内 action/mood 覆盖）——全局是唯一拥有域级整体乘数的恢复档。
        if (xenotypePresets != null && xenotypePresets.Count > 0) { xenotypePresets = defaults.xenotypePresets; changed = true; }
        if (actionTuning != null && actionTuning.Count > 0) { actionTuning = defaults.actionTuning; changed = true; }
        if (moodTuning != null && moodTuning.Count > 0) { moodTuning = defaults.moodTuning; changed = true; }

        // 不在字段表内（保留，不许伪装）：settingsSchemaVersion / voicePackSchemaVersion（当前值自持，
        // 迁移边界不动）、wasLoaded 系诊断、migrationPersistencePending/Blocked、settingsLoadedFromFile。
        // 独立 fallback store（玩家表/声音映射）与作者 Def 完全不经过本方法。

        if (!changed) return SqueakResetOutcome.NoChange;

        // 目标已形成；沿既有漏斗发布一次、持久化请求一次。
        ApplySettingsRuntimeSideEffects(true);
        ApplyToRuntime();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    /// <summary>距离恢复（§4.3「恢复听取距离默认」）：distancePreset→Balanced、
    /// distanceRange→GetDistancePresetRange(Balanced)。明确保留 globalVolumeFactor、动作/心情
    /// 覆盖、包选择与 fallback。直接执行档（确认后调用；简单恢复不设二次确认）。
    /// 发布 = 既有距离通道 NotifyDistanceRuntimeChanged + 一次 QueuePersistence。</summary>
    internal SqueakResetOutcome ResetDistanceDefaults()
    {
        FloatRange target = GetDistancePresetRange(SqueakDistancePreset.Balanced);
        bool changed = distancePreset != SqueakDistancePreset.Balanced
            || !DistanceRangesEqual(distanceRange, target);
        if (!changed) return SqueakResetOutcome.NoChange;

        distancePreset = SqueakDistancePreset.Balanced;
        distanceRange = target;
        NotifyDistanceRuntimeChanged();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    /// <summary>恢复单条动作行（§6.3）：精确身份 (actionKey, race, xeno) 内**每一行**的三覆盖
    /// 字段 + 来源锚点 sourcePresetDefName 同置默认（清 hasScope/hasInterval/hasProbability、
    /// source=""），随后删除不再承载任何字段的行（清掉锚点后必删，恢复继承语义则**留住**锚点行
    /// ——两操作不同，见 SetActionTuningScope 的裁定注）。其它键/其它层/其它域全部保留；
    /// 不枚举 xenotypePresets（域级整体乘数保留）。
    /// 非法目标（空 actionKey、race 空而 xeno 非空）⇒ Rejected 零副作用；该身份没有行 ⇒
    /// NoChange，绝不回落 Global。发布 = 既有离散通道一次 + 持久化一次。</summary>
    internal SqueakResetOutcome ResetActionTuningRow(string actionKey, string raceDefName, string xenotypeDefName)
    {
        if (string.IsNullOrEmpty(actionKey)) return SqueakResetOutcome.Rejected;
        if (!IsValidTuningDomain(raceDefName, xenotypeDefName)) return SqueakResetOutcome.Rejected;

        string race = raceDefName ?? "";
        string xeno = xenotypeDefName ?? "";
        if (actionTuning == null || actionTuning.Count == 0) return SqueakResetOutcome.NoChange;

        bool changed = false;
        foreach (ActionTuningRecord row in actionTuning)
        {
            if (!SameActionTuningIdentity(row, actionKey, race, xeno)) continue;
            if (row.hasScope || row.hasIntervalMultiplier || row.hasProbabilityMultiplier
                || !string.IsNullOrEmpty(row.sourcePresetDefName)) changed = true;
            row.hasScope = false;
            row.hasIntervalMultiplier = false;
            row.hasProbabilityMultiplier = false;
            row.sourcePresetDefName = "";
        }

        // 清空后不再承载任何字段的行删除（含本就全默认的残行——结构性归一，同样计入变更）。
        if (actionTuning.RemoveAll(r => SameActionTuningIdentity(r, actionKey, race, xeno)
            && !CarriesAnyActionTuningField(r)) > 0) changed = true;

        if (!changed) return SqueakResetOutcome.NoChange;
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    /// <summary>恢复当前动作区域（§6.3/§6.4）：当前层+当前域身份 (race, xeno) 的**全部**动作行
    /// （三覆盖字段 + 锚点），规则与 <see cref="ResetActionTuningRow"/> 同形，只是身份换成域对
    /// （覆盖所有 actionKey）。上下其它层、其它种族/异种域、moodTuning、旧 moodOverrides、
    /// 包选择、xenotypePresets（含域级整体乘数，§6.6「局部不清」）全部保留。
    /// (race="", xeno="") 是合法的层 0 区域目标（只清 Global 层动作行），与全局恢复是不同操作。
    /// race 空而 xeno 非空 ⇒ Rejected 零副作用；域内无行 ⇒ NoChange，绝不回落 Global。</summary>
    internal SqueakResetOutcome ResetActionTuningArea(string raceDefName, string xenotypeDefName)
    {
        if (!IsValidTuningDomain(raceDefName, xenotypeDefName)) return SqueakResetOutcome.Rejected;
        if (actionTuning == null || actionTuning.Count == 0) return SqueakResetOutcome.NoChange;

        bool changed = ClearActionTuningDomainRows(raceDefName, xenotypeDefName);
        if (!changed) return SqueakResetOutcome.NoChange;
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    /// <summary>恢复当前心情区域（§6.3/§6.4）：当前层+当前域身份 (race, xeno) 的**全部**心情行
    /// （三因子字段 + 来源锚点，跨所有 mood）。规则与动作区域同形：清字段、只删不再承载任何
    /// 字段的行。其它层/域、动作行、包选择保留；旧全局 moodOverrides 现不参与运行时解析
    /// （S5 已迁移到层表），因此**无同步动作**（§6.4 行 5 的条件句）；xenotypePresets 域级
    /// 整体乘数局部不清（§6.6）。发布 = 既有连续通道一次 + 持久化一次。</summary>
    internal SqueakResetOutcome ResetMoodTuningArea(string raceDefName, string xenotypeDefName)
    {
        if (!IsValidTuningDomain(raceDefName, xenotypeDefName)) return SqueakResetOutcome.Rejected;
        if (moodTuning == null || moodTuning.Count == 0) return SqueakResetOutcome.NoChange;

        string race = raceDefName ?? "";
        string xeno = xenotypeDefName ?? "";
        bool changed = false;
        foreach (MoodTuningRecord row in moodTuning)
        {
            if (!SameMoodTuningDomain(row, race, xeno)) continue;
            if (row.hasPitchFactor || row.hasVolumeFactor || row.hasPitchJitter
                || !string.IsNullOrEmpty(row.sourcePresetDefName)) changed = true;
            row.hasPitchFactor = false;
            row.hasVolumeFactor = false;
            row.hasPitchJitter = false;
            row.sourcePresetDefName = "";
        }

        if (moodTuning.RemoveAll(r => SameMoodTuningDomain(r, race, xeno)
            && !CarriesAnyMoodTuningField(r)) > 0) changed = true;

        if (!changed) return SqueakResetOutcome.NoChange;
        NotifyContinuousXenotypeRuntimeChanged();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    /// <summary>普通可见区域恢复（§6.4 末行）：只动该可见区域直接对应的持久字段集合，
    /// 其它区域与 fallback 保留。字段集合来自当前 UI 绑定（报告逐项列出）：
    ///  - Distance（host 397-441）：globalVolumeFactor→1f、distancePreset→Balanced、
    ///    distanceRange→15–50。与 ResetDistanceDefaults 的区别就是这个区域**含**音量滑杆。
    ///  - Basics（host 449-463）：allowEasterEggSounds→false、scaleCooldownWithTimeSpeed→true、
    ///    scaleFrequencyWithTalking→true、scalePeriodicWithAudiblePopulation→true、
    ///    showCameraIndicator→false、allowBabyActions→false、eatPrecisionEnabled→false、
    ///    eatPrecisionIncludeDrugs→false。
    ///  - Timing（host 470-508）：globalMinIntervalTicks→216、globalCooldownMultiplier→1f。
    ///  - Diagnostics（host 515-539）：devLoggingMode→Auto、localizeDebugActions→false。
    /// 未知枚举值 ⇒ Rejected 零副作用。发布按各字段既有通道恰好一轮，持久化请求一次。</summary>
    internal SqueakResetOutcome ResetNormalArea(SqueakNormalResetArea area)
    {
        switch (area)
        {
            case SqueakNormalResetArea.Distance: return ResetDistanceVolumeArea();
            case SqueakNormalResetArea.Basics: return ResetBasicsArea();
            case SqueakNormalResetArea.Timing: return ResetTimingArea();
            case SqueakNormalResetArea.Diagnostics: return ResetDiagnosticsArea();
            default: return SqueakResetOutcome.Rejected;
        }
    }

    private SqueakResetOutcome ResetDistanceVolumeArea()
    {
        UniversalSqueakerSettings defaults = new();
        FloatRange targetRange = GetDistancePresetRange(SqueakDistancePreset.Balanced);
        bool volumeChanged = globalVolumeFactor != defaults.globalVolumeFactor;
        bool presetChanged = distancePreset != defaults.distancePreset;
        bool rangeChanged = !DistanceRangesEqual(distanceRange, targetRange);
        if (!volumeChanged && !presetChanged && !rangeChanged) return SqueakResetOutcome.NoChange;

        globalVolumeFactor = defaults.globalVolumeFactor;
        distancePreset = defaults.distancePreset;
        distanceRange = targetRange;

        ApplyGlobalVolumeStatic();
        NotifyDistanceRuntimeChanged();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    private SqueakResetOutcome ResetBasicsArea()
    {
        UniversalSqueakerSettings defaults = new();
        bool eggsChanged = allowEasterEggSounds != defaults.allowEasterEggSounds;
        bool cooldownChanged = scaleCooldownWithTimeSpeed != defaults.scaleCooldownWithTimeSpeed;
        bool talkingChanged = scaleFrequencyWithTalking != defaults.scaleFrequencyWithTalking;
        bool populationChanged = scalePeriodicWithAudiblePopulation != defaults.scalePeriodicWithAudiblePopulation;
        bool cameraChanged = showCameraIndicator != defaults.showCameraIndicator;
        bool babyChanged = allowBabyActions != defaults.allowBabyActions;
        bool eatChanged = eatPrecisionEnabled != defaults.eatPrecisionEnabled;
        bool drugsChanged = eatPrecisionIncludeDrugs != defaults.eatPrecisionIncludeDrugs;
        if (!eggsChanged && !cooldownChanged && !talkingChanged && !populationChanged
            && !cameraChanged && !babyChanged && !eatChanged && !drugsChanged) return SqueakResetOutcome.NoChange;

        allowEasterEggSounds = defaults.allowEasterEggSounds;
        scaleCooldownWithTimeSpeed = defaults.scaleCooldownWithTimeSpeed;
        scaleFrequencyWithTalking = GetDefaultScaleFrequencyWithTalking();
        scalePeriodicWithAudiblePopulation = defaults.scalePeriodicWithAudiblePopulation;
        showCameraIndicator = defaults.showCameraIndicator;
        allowBabyActions = defaults.allowBabyActions;
        eatPrecisionEnabled = defaults.eatPrecisionEnabled;
        // 父关强制子关（SetEatPrecision/PostLoadInit 同规则）：清父开关时子开关同步置默认，
        // "parent off + child on" 不会成为本恢复的落点。
        eatPrecisionIncludeDrugs = defaults.eatPrecisionIncludeDrugs;

        // 目标已形成；按各字段的既有通道一轮发布。
        if (cameraChanged) SqueakDebug.ShowCameraIndicator = showCameraIndicator;
        NotifyCheapRuntimeChanged();
        // 彩蛋开关是路由快照成员：变化走离散重建（SetAllowEasterEggSounds 同通道）。
        if (eggsChanged) NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    private SqueakResetOutcome ResetTimingArea()
    {
        UniversalSqueakerSettings defaults = new();
        bool intervalChanged = globalMinIntervalTicks != defaults.globalMinIntervalTicks;
        bool multiplierChanged = globalCooldownMultiplier != defaults.globalCooldownMultiplier;
        if (!intervalChanged && !multiplierChanged) return SqueakResetOutcome.NoChange;

        globalMinIntervalTicks = defaults.globalMinIntervalTicks;
        globalCooldownMultiplier = defaults.globalCooldownMultiplier;
        NotifyCheapRuntimeChanged();
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    private SqueakResetOutcome ResetDiagnosticsArea()
    {
        UniversalSqueakerSettings defaults = new();
        bool loggingChanged = devLoggingMode != defaults.devLoggingMode;
        bool localizeChanged = localizeDebugActions != defaults.localizeDebugActions;
        if (!loggingChanged && !localizeChanged) return SqueakResetOutcome.NoChange;

        devLoggingMode = defaults.devLoggingMode;
        localizeDebugActions = defaults.localizeDebugActions;

        if (loggingChanged) ApplyDevLoggingModeToRuntime(true);
        if (localizeChanged) Patch_DebugTabMenu_Actions.SetEnabled(localizeDebugActions);
        QueuePersistence();
        return SqueakResetOutcome.Applied;
    }

    /// <summary>局部层/域身份的合法性：镜像 IsValidLayer 归一——raceDefName 空 + xenotypeDefName
    /// 非空 = 非法目标，调用方（UI 集成层）拿到的必须是 Rejected，而不是任何更宽的动作。</summary>
    private static bool IsValidTuningDomain(string? raceDefName, string? xenotypeDefName)
    {
        bool hasRace = !string.IsNullOrEmpty(raceDefName);
        bool hasXeno = !string.IsNullOrEmpty(xenotypeDefName);
        return hasRace || !hasXeno;
    }

    private bool ClearActionTuningDomainRows(string? raceDefName, string? xenotypeDefName)
    {
        string race = raceDefName ?? "";
        string xeno = xenotypeDefName ?? "";
        bool changed = false;
        foreach (ActionTuningRecord row in actionTuning)
        {
            if (!SameActionTuningDomain(row, race, xeno)) continue;
            if (row.hasScope || row.hasIntervalMultiplier || row.hasProbabilityMultiplier
                || !string.IsNullOrEmpty(row.sourcePresetDefName)) changed = true;
            row.hasScope = false;
            row.hasIntervalMultiplier = false;
            row.hasProbabilityMultiplier = false;
            row.sourcePresetDefName = "";
        }

        if (actionTuning.RemoveAll(r => SameActionTuningDomain(r, race, xeno)
            && !CarriesAnyActionTuningField(r)) > 0) changed = true;
        return changed;
    }

    /// <summary>动作行的域身份比较（不带 actionKey）：域对 (race, xeno) 精确匹配，覆盖域内所有
    /// 键。与 <see cref="SameActionTuningIdentity"/> 同一 ?? "" Ordinal 规则。</summary>
    private static bool SameActionTuningDomain(ActionTuningRecord? record, string raceDefName, string xenotypeDefName)
    {
        return record != null
            && string.Equals(record.raceDefName ?? "", raceDefName ?? "", StringComparison.Ordinal)
            && string.Equals(record.xenotypeDefName ?? "", xenotypeDefName ?? "", StringComparison.Ordinal);
    }

    /// <summary>心情行的域身份比较（不带 mood）：域对 (race, xeno) 精确匹配，覆盖域内所有心情行。</summary>
    private static bool SameMoodTuningDomain(MoodTuningRecord? record, string raceDefName, string xenotypeDefName)
    {
        return record != null
            && string.Equals(record.raceDefName ?? "", raceDefName ?? "", StringComparison.Ordinal)
            && string.Equals(record.xenotypeDefName ?? "", xenotypeDefName ?? "", StringComparison.Ordinal);
    }

    private static bool DistanceRangesEqual(FloatRange a, FloatRange b)
        => a.min == b.min && a.max == b.max;
}
