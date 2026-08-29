using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>心情→音色调制参数。既用于 CompProperties 默认层(XML),也用于 ModSettings override 层(序列化)。</summary>
public class SqueakMoodMod : IExposable
{
    public SqueakMood mood = SqueakMood.Neutral;
    public float pitchFactor = 1f;
    public float volumeFactor = 1f;
    public FloatRange pitchJitter = FloatRange.One;

    public void ExposeData()
    {
        Scribe_Values.Look(ref mood, "mood");
        Scribe_Values.Look(ref pitchFactor, "pitchFactor", 1f);
        Scribe_Values.Look(ref volumeFactor, "volumeFactor", 1f);
        Scribe_Values.Look(ref pitchJitter, "pitchJitter", FloatRange.One);
    }

    public SqueakMoodMod Clone() => new() { mood = mood, pitchFactor = pitchFactor, volumeFactor = volumeFactor, pitchJitter = pitchJitter };
}

public enum SqueakDistancePreset { Conservative, Balanced, Strong, Custom }

/// <summary>
/// 玩家配置。承载:
///  - voicePackMode:运行时 VoicePack 音源策略
///  - moodOverrides:心情调制 override(字段级覆盖 CompProperties 默认,用于换音频后补偿)
/// 所有业务设置实时发布到运行时；磁盘写入由 Mod 层防抖合并。
/// </summary>
public partial class UniversalSqueakerSettings : ModSettings
{
    private static readonly FloatRange FallbackBalancedDistanceRange = new(15f, 50f);

    // 0.2.3 产品决策:默认音源策略为 Fallback。Scribe 省略等于默认值的节点,
    // 因此"从未显式选择"的配置(无 voicePackMode 节点)天然落到 Fallback;显式 Off 仍写入节点并保留。
    public SqueakVoicePackMode voicePackMode = SqueakVoicePackMode.Fallback;
    public int voicePackSchemaVersion = CurrentVoicePackSchemaVersion;
    // v4 owns the race-aware selection/preset record identity migration.
    public int settingsSchemaVersion = CurrentSettingsSchemaVersion;
    public bool scaleCooldownWithTimeSpeed = true;
    public bool scaleFrequencyWithTalking = true;
    public bool scalePeriodicWithAudiblePopulation = true;
    public int globalMinIntervalTicks = 216;
    public bool localizeDebugActions = false;
    // S4 diagnostics foundation: player-facing camera indicator readout (no DevMode gating).
    public bool showCameraIndicator = false;
    public SqueakDevLoggingMode devLoggingMode = SqueakDevLoggingMode.Auto;
    public float globalCooldownMultiplier = 1f;
    public float globalVolumeFactor = 1f;
    public SqueakDistancePreset distancePreset = SqueakDistancePreset.Balanced;
    public FloatRange distanceRange = new(15f, 50f);
    public Dictionary<SqueakMood, SqueakMoodMod> moodOverrides = new();
    public List<VoicePackSelectionRecord> voicePackSelections = new();
    public List<XenotypePresetRecord> xenotypePresets = new();
    // S2 layered action tuning table (Global/Race/Xenotype).
    public List<ActionTuningRecord> actionTuning = new();
    // S5 layered mood tuning table (Global/Race/Xenotype); supersedes moodOverrides / preset mood rows.
    public List<MoodTuningRecord> moodTuning = new();
    // Action gate: non-built-in external actions fire only when true. Default false (closed).
    public bool allowExternalActions = false;
    public bool EffectiveDevLogging => SqueakLog.EffectiveDevLogging;
    public void SetDevLoggingMode(SqueakDevLoggingMode value)
    {
        devLoggingMode = Enum.IsDefined(typeof(SqueakDevLoggingMode), value) ? value : SqueakDevLoggingMode.Auto;
        ApplyDevLoggingModeToRuntime(true);
    }

    private void ApplyDevLoggingModeToRuntime(bool announceChange)
    {
        bool wasEnabled = SqueakLog.EffectiveDevLogging;
        SqueakLog.Configure(devLoggingMode);
        if (wasEnabled != SqueakLog.EffectiveDevLogging) SqueakLog.ResetSession();
        if (announceChange) SqueakLog.LoggingModeChanged(devLoggingMode, SqueakLog.EffectiveDevLogging);
    }

    // Settings window session scaffolding. Opening/closing the window resets page-local UI state and
    // keeps the Mod shell flush-on-close lifecycle intact.
    internal void BeginSettingsSession() => UniversalSqueaker.UI.VoicePacksPage.BeginSession();
    internal void EndSettingsSession() => UniversalSqueaker.UI.VoicePacksPage.EndSession();
    internal void RequestXenotypeTabOnNextDraw() { }
    internal void ClearXenotypeTabRequest() { }

    public void DrawSettings(Rect inRect)
    {
        UniversalSqueaker.UI.VoicePacksPage.Draw(inRect);
    }

    public void ApplyToRuntime()
    {
        SqueakRuntimeResolver.NotifyDiscreteResolverChange(this, SqueakXenotypeCatalog.Current);
        Patch_DebugTabMenu_Actions.SetEnabled(localizeDebugActions);
        SqueakDebug.ShowCameraIndicator = showCameraIndicator;
        CompSqueaker.ScaleCooldownWithTimeSpeed = scaleCooldownWithTimeSpeed;
        CompSqueaker.ScaleFrequencyWithTalking = scaleFrequencyWithTalking;
        CompSqueaker.ScalePeriodicWithAudiblePopulation = scalePeriodicWithAudiblePopulation;
        CompSqueaker.GlobalCooldownMultiplier = Mathf.Clamp(globalCooldownMultiplier, 0f, 3f);
        CompSqueaker.GlobalMinIntervalTicks = Mathf.Max(1, globalMinIntervalTicks);
        CompSqueaker.GlobalVolumeFactor = Mathf.Clamp(globalVolumeFactor, 0f, 1f);
        CompSqueaker.ApplyDistanceRange(ClampDistanceRange(distanceRange));
    }

    /// <summary>Cheap controls are same-frame static runtime values and never rebuild the resolver.</summary>
    public void NotifyCheapRuntimeChanged()
    {
        CompSqueaker.ScaleCooldownWithTimeSpeed = scaleCooldownWithTimeSpeed;
        CompSqueaker.ScaleFrequencyWithTalking = scaleFrequencyWithTalking;
        CompSqueaker.ScalePeriodicWithAudiblePopulation = scalePeriodicWithAudiblePopulation;
        CompSqueaker.GlobalCooldownMultiplier = Mathf.Clamp(globalCooldownMultiplier, 0f, 3f);
        CompSqueaker.GlobalMinIntervalTicks = Mathf.Max(1, globalMinIntervalTicks);
        CompSqueaker.GlobalVolumeFactor = Mathf.Clamp(globalVolumeFactor, 0f, 1f);
    }

    public void NotifyDistanceRuntimeChanged() => CompSqueaker.ApplyDistanceRange(ClampDistanceRange(distanceRange));

    /// <summary>A3: switch the distance preset and republish the effective range (cheap, no resolver rebuild).</summary>
    internal void SetDistancePreset(SqueakDistancePreset preset)
    {
        if (preset == SqueakDistancePreset.Custom || !Enum.IsDefined(typeof(SqueakDistancePreset), preset)) return;
        if (preset == distancePreset) return;
        distancePreset = preset;
        distanceRange = GetDistancePresetRange(preset);
        NotifyDistanceRuntimeChanged();
        QueuePersistence();
    }

    /// <summary>S4-Vol: global volume 0..1. Cheap runtime write, no resolver rebuild.</summary>
    internal void SetGlobalVolume(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 1f);
        globalVolumeFactor = clamped;
        CompSqueaker.GlobalVolumeFactor = clamped;
        QueuePersistence();
    }

    /// <summary>S4-Vol: camera-height attenuation range. Clamped with the existing distance-range
    /// constraints; any manual edit switches the preset to Custom.</summary>
    internal void SetDistanceRange(float start, float end)
    {
        FloatRange clamped = ClampDistanceRange(new FloatRange(start, end));
        distanceRange = clamped;
        distancePreset = SqueakDistancePreset.Custom;
        NotifyDistanceRuntimeChanged();
        QueuePersistence();
    }

    /// <summary>A4/A5: cheap global scaling toggles and cooldown multiplier (no resolver rebuild; runtime static values).</summary>
    internal void SetBasicTuning(string key, bool value)
    {
        switch (key)
        {
            case "ScaleCooldown": if (scaleCooldownWithTimeSpeed != value) { scaleCooldownWithTimeSpeed = value; NotifyCheapRuntimeChanged(); QueuePersistence(); } break;
            case "ScaleTalking": if (scaleFrequencyWithTalking != value) { scaleFrequencyWithTalking = value; NotifyCheapRuntimeChanged(); QueuePersistence(); } break;
            case "ScalePopulation": if (scalePeriodicWithAudiblePopulation != value) { scalePeriodicWithAudiblePopulation = value; NotifyCheapRuntimeChanged(); QueuePersistence(); } break;
            case "CameraIndicator": SetCameraIndicator(value); break;
        }
    }

    /// <summary>
    /// S4 diagnostics foundation: player-facing camera indicator toggle. Cheap: updates the runtime
    /// static directly (no resolver rebuild) and queues persistence.
    /// </summary>
    internal void SetCameraIndicator(bool value)
    {
        if (showCameraIndicator == value) return;
        showCameraIndicator = value;
        SqueakDebug.ShowCameraIndicator = value;
        QueuePersistence();
    }
    public void NotifyContinuousXenotypeRuntimeChanged() => SqueakRuntimeResolver.NotifyContinuousResolverChange(this, SqueakXenotypeCatalog.Current);
    public void NotifyDiscreteResolverRuntimeChanged() => SqueakRuntimeResolver.NotifyDiscreteResolverChange(this, SqueakXenotypeCatalog.Current);
    public void QueuePersistence() => UniversalSqueakerMod.Instance?.QueueSettingsSave();
    public void FlushPendingRuntimeForPreview() => SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);

    /// <summary>Consumes a PostLoadInit migration only from the main-thread startup callback.</summary>
    internal void QueuePendingMigrationPersistence()
    {
        if (!migrationPersistencePending || UniversalSqueakerMod.Instance == null) return;
        UniversalSqueakerMod.Instance.QueueSettingsSave();
        // Keep the pending flag if QueueSettingsSave ever throws, so the startup path remains recoverable.
        migrationPersistencePending = false;
    }

    internal void ApplySettingsRuntimeSideEffects(bool announceLoggingChange = true)
    {
        ApplyDevLoggingModeToRuntime(announceLoggingChange);
    }

    /// <summary>写一条分层作用域：Upsert 到 actionTuning（last-wins 按 (actionKey,raceDefName,xenotypeDefName)）。scope == null 表示清该层记录（移除）；走离散 resolver 重建 + 排队持久化。</summary>
    internal void SetActionTuningScope(string actionKey, string raceDefName, string xenotypeDefName, SqueakActionScope? scope)
    {
        actionTuning ??= new List<ActionTuningRecord>();
        // 按 IsValidLayer 语义归一：raceDefName 空 + xenotypeDefName 非空 = 非法，直接忽略。
        bool hasRace = !string.IsNullOrEmpty(raceDefName);
        bool hasXeno = !string.IsNullOrEmpty(xenotypeDefName);
        if (!hasRace && hasXeno) return;
        if (string.IsNullOrEmpty(actionKey)) return;

        ActionTuningRecord? existing = null;
        foreach (ActionTuningRecord candidate in actionTuning)
            if (candidate != null
                && string.Equals(candidate.actionKey, actionKey, StringComparison.Ordinal)
                && string.Equals(candidate.raceDefName ?? "", raceDefName ?? "", StringComparison.Ordinal)
                && string.Equals(candidate.xenotypeDefName ?? "", xenotypeDefName ?? "", StringComparison.Ordinal))
                existing = candidate;

        // 统一 last-wins：清除全部同身份行（含陈旧重复）后追加/不再追加，与 SetMoodTuning/UpsertMood 一致。
        actionTuning.RemoveAll(c => c != null
            && string.Equals(c.actionKey, actionKey, StringComparison.Ordinal)
            && string.Equals(c.raceDefName ?? "", raceDefName ?? "", StringComparison.Ordinal)
            && string.Equals(c.xenotypeDefName ?? "", xenotypeDefName ?? "", StringComparison.Ordinal));

        if (scope == null)
        {
            NotifyDiscreteResolverRuntimeChanged();
            QueuePersistence();
            return;
        }

        SqueakActionScope effective = scope.Value;
        // 内置键按 SupportedScopes 归一（Draft/Undraft/Equip 仅支持 ActiveCommand），避免写入运行时永远不匹配的作用域。
        if (UniversalSqueaker.Kernel.ActionKey.TryParseBuiltIn(actionKey, out SqueakAction builtInAction))
            effective = SqueakActionDefinitions.NormalizeScope(builtInAction, effective);
        actionTuning.Add(new ActionTuningRecord
        {
            actionKey = actionKey,
            raceDefName = raceDefName ?? "",
            xenotypeDefName = xenotypeDefName ?? "",
            sourcePresetDefName = existing?.sourcePresetDefName ?? "",
            hasScope = true,
            scope = effective,
        });
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
    }

    /// <summary>写一条分层心情调音：Upsert 到 moodTuning（last-wins 按 (mood,raceDefName,xenotypeDefName)）。
    /// factor ∈ {pitch, volume, jitter, clear}：字段级写入（hasX+值，其余因子继承不变）；clear 移除整行
    /// 记录（恢复继承）。走连续 resolver 重建（拖动期 75/150ms 合并）+ 排队持久化。</summary>
    internal void SetMoodTuning(SqueakMood mood, string raceDefName, string xenotypeDefName, string factor, float? value)
    {
        moodTuning ??= new List<MoodTuningRecord>();
        // 按 IsValidLayer 语义归一：raceDefName 空 + xenotypeDefName 非空 = 非法，直接忽略。
        bool hasRace = !string.IsNullOrEmpty(raceDefName);
        bool hasXeno = !string.IsNullOrEmpty(xenotypeDefName);
        if (!hasRace && hasXeno) return;

        // last-wins：扫描到末个匹配（与运行时同层 Merge 的列表序一致；SetActionTuningScope 同规则）。
        int index = -1;
        for (int i = 0; i < moodTuning.Count; i++)
        {
            MoodTuningRecord candidate = moodTuning[i];
            if (candidate != null
                && candidate.mood == mood
                && string.Equals(candidate.raceDefName ?? "", raceDefName ?? "", StringComparison.Ordinal)
                && string.Equals(candidate.xenotypeDefName ?? "", xenotypeDefName ?? "", StringComparison.Ordinal))
            {
                index = i;
            }
        }

        if (string.Equals(factor, "clear", StringComparison.Ordinal))
        {
            // 清全部匹配行（含陈旧重复）= 恢复继承。
            moodTuning.RemoveAll(c => c != null
                && c.mood == mood
                && string.Equals(c.raceDefName ?? "", raceDefName ?? "", StringComparison.Ordinal)
                && string.Equals(c.xenotypeDefName ?? "", xenotypeDefName ?? "", StringComparison.Ordinal));
            NotifyContinuousXenotypeRuntimeChanged();
            QueuePersistence();
            return;
        }

        bool validFactor = string.Equals(factor, "pitch", StringComparison.Ordinal)
            || string.Equals(factor, "volume", StringComparison.Ordinal)
            || string.Equals(factor, "jitter", StringComparison.Ordinal);
        if (!validFactor) return;
        if (value == null || float.IsNaN(value.Value) || float.IsInfinity(value.Value)) return;

        MoodTuningRecord record;
        if (index >= 0)
        {
            record = moodTuning[index];
        }
        else
        {
            record = new MoodTuningRecord { mood = mood, raceDefName = raceDefName ?? "", xenotypeDefName = xenotypeDefName ?? "" };
            moodTuning.Add(record);
        }

        // 字段级写入：只落被编辑的因子，其余 hasX 保持（继承语义不受影响）。
        if (string.Equals(factor, "pitch", StringComparison.Ordinal)) { record.hasPitchFactor = true; record.pitchFactor = Mathf.Clamp(value.Value, 0.5f, 2f); }
        else if (string.Equals(factor, "volume", StringComparison.Ordinal)) { record.hasVolumeFactor = true; record.volumeFactor = Mathf.Clamp(value.Value, 0.1f, 2f); }
        else if (string.Equals(factor, "jitter", StringComparison.Ordinal)) { float half = Mathf.Clamp(value.Value, 0f, 0.5f); record.hasPitchJitter = true; record.pitchJitter = new FloatRange(Math.Max(0.02f, 1f - half), 1f + half); }

        NotifyContinuousXenotypeRuntimeChanged();
        QueuePersistence();
    }

    /// <summary>增量导入调音预设：将选中 race/xeno 行写入 actionTuning 与 moodOverrides，然后离散重建 resolver 并排队持久化。</summary>
    internal BaselineImportResult ImportBaselinePreset(UniversalSqueakerTuningBaselineDef preset, BaselinePresetImporter.Selection selection)
    {
        BaselineImportResult result = BaselinePresetImporter.Import(preset, selection, this);
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
        return result;
    }

    /// <summary>0.3.1 波 3c 彩蛋开关读取（决策 §2.4：默认关，路由输入随快照）。</summary>
    public bool AllowEasterEggSounds => allowEasterEggSounds;

    /// <summary>彩蛋开关离散 setter：切换走离散 resolver 重建（快照 AllowEggs 生效于返回前），并排队持久化。</summary>
    internal void SetAllowEasterEggSounds(bool value)
    {
        if (allowEasterEggSounds == value) return;
        allowEasterEggSounds = value;
        QueuePersistence();
        NotifyDiscreteResolverRuntimeChanged();
    }

    /// <summary>Explicit future D2 refresh entry; intentionally does not evaluate notifications or normalize saved records.</summary>
    public void RefreshCatalogAndRuntime()
    {
        SqueakXenotypeCatalog.Refresh(this);
        NotifyDiscreteResolverRuntimeChanged();
    }

    /// <summary>Core mode commit: updates the persisted policy, publishes a discrete resolver rebuild, and queues save.</summary>
    internal void CommitVoicePackMode(SqueakVoicePackMode target)
    {
        if (target == voicePackMode) return;
        voicePackMode = target;
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
    }

    /// <summary>Compatibility bridge: race is implied by a single-race catalog or, for Race scope, by targetDefName.</summary>
    public void SetVoicePackSelection(SqueakVoicePackScope scope, string targetDefName, IEnumerable<string> enabledKeys)
    {
        SetVoicePackSelection(scope, ResolveLegacyRace(scope, targetDefName), targetDefName, enabledKeys);
    }

    /// <summary>Canonical last-wins selection write. Unknown keys remain persisted for future pack restoration.</summary>
    public void SetVoicePackSelection(SqueakVoicePackScope scope, string raceDefName, string targetDefName, IEnumerable<string> enabledKeys)
    {
        if (scope != SqueakVoicePackScope.Race && scope != SqueakVoicePackScope.Xenotype) return;
        string target = scope == SqueakVoicePackScope.Race ? "" : targetDefName ?? "";
        if (string.IsNullOrWhiteSpace(raceDefName)) return;
        if (scope == SqueakVoicePackScope.Xenotype && target.Length == 0) return;
        voicePackSelections.RemoveAll(x => VoicePackSelectionRecord.SameDomain(x, scope, raceDefName, target));
        List<string> keys = (enabledKeys ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        if (keys.Count > 0) voicePackSelections.Add(new VoicePackSelectionRecord
        {
            scope = scope,
            raceDefName = raceDefName,
            xenotypeDefName = target,
            enabledPackKeys = keys
        });
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
    }

    /// <summary>Compatibility bridge: race is implied by a single-race catalog or, for Race scope, by targetDefName.</summary>
    public void ForgetVoicePackSelection(SqueakVoicePackScope scope, string targetDefName)
    {
        ForgetVoicePackSelection(scope, ResolveLegacyRace(scope, targetDefName), targetDefName);
    }

    public void ForgetVoicePackSelection(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        string target = scope == SqueakVoicePackScope.Race ? "" : targetDefName ?? "";
        voicePackSelections.RemoveAll(x => VoicePackSelectionRecord.SameDomain(x, scope, raceDefName, target));
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
    }

    public void ForgetXenotypeTarget(string targetDefName)
    {
        if (string.IsNullOrEmpty(targetDefName)) return;
        xenotypePresets.RemoveAll(x => x != null && string.Equals(x.xenotypeDefName, targetDefName, StringComparison.Ordinal));
        voicePackSelections.RemoveAll(x => x != null && x.scope == SqueakVoicePackScope.Xenotype && string.Equals(x.xenotypeDefName, targetDefName, StringComparison.Ordinal));
        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
    }

    /// <summary>Compatibility bridge: race is implied by a single-race catalog or, for Race scope, by targetDefName.</summary>
    public SqueakVoicePackDomainStatus GetVoicePackSelectionStatus(SqueakVoicePackScope scope, string targetDefName)
    {
        return GetVoicePackSelectionStatus(scope, ResolveLegacyRace(scope, targetDefName), targetDefName);
    }

    public SqueakVoicePackDomainStatus GetVoicePackSelectionStatus(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        string target = scope == SqueakVoicePackScope.Race ? "" : targetDefName ?? "";
        VoicePackSelectionRecord? record = voicePackSelections.LastOrDefault(x => VoicePackSelectionRecord.SameDomain(x, scope, raceDefName, target));
        List<string> keys = new(record?.enabledPackKeys ?? new List<string>());
        SqueakXenotypeCatalogSnapshot catalog = SqueakXenotypeCatalog.Current;
        if (scope == SqueakVoicePackScope.Xenotype && !ModsConfig.BiotechActive) return new SqueakVoicePackDomainStatus(SqueakVoicePackDomainState.Dormant, keys);
        IEnumerable<SqueakVoicePackDef> domainPacks = catalog.GetVoicePackDomainPacks(scope, scope == SqueakVoicePackScope.Race ? raceDefName : target);
        // 双键域身份：Xenotype 状态必须按 (race, xeno) 过滤，避免同 xeno 跨 race 的包串扰。
        if (scope == SqueakVoicePackScope.Xenotype)
            domainPacks = domainPacks.Where(pack => string.Equals(pack.raceDefName, raceDefName, StringComparison.Ordinal));
        HashSet<string> domainKeys = new(StringComparer.Ordinal);
        foreach (SqueakVoicePackDef pack in domainPacks)
        {
            if (pack.TryGetPackKey(out string key)) domainKeys.Add(key);
        }
        if (scope == SqueakVoicePackScope.Xenotype && !catalog.XenotypeByDefName.ContainsKey(target)) return new SqueakVoicePackDomainStatus(SqueakVoicePackDomainState.TargetUnavailable, keys);
        foreach (string key in keys) if (!domainKeys.Contains(key)) return new SqueakVoicePackDomainStatus(SqueakVoicePackDomainState.Orphan, keys);
        return new SqueakVoicePackDomainStatus(SqueakVoicePackDomainState.Available, keys);
    }

    private string ResolveLegacyRace(SqueakVoicePackScope scope, string targetDefName)
    {
        IReadOnlyList<string> races = SqueakXenotypeCatalog.Current.RaceDefNames;
        if (scope == SqueakVoicePackScope.Race)
        {
            if (!string.IsNullOrEmpty(targetDefName)) return targetDefName!;
            return races.Count == 1 ? races[0] : "";
        }
        return races.Count == 1 ? races[0] : "";
    }

    private static FloatRange GetDistancePresetRange(SqueakDistancePreset preset)
    {
        // S1: distance presets are now US-global settings; the author comp face no longer carries them.
        return preset switch
        {
            SqueakDistancePreset.Conservative => new FloatRange(15f, 65f),
            SqueakDistancePreset.Strong => new FloatRange(15f, 40f),
            _ => FallbackBalancedDistanceRange,
        };
    }

    private static bool GetDefaultScaleFrequencyWithTalking()
    {
        // S1: talking-frequency scaling is now a US-global setting; no author comp face read-back.
        return true;
    }

    private static FloatRange ClampDistanceRange(FloatRange range)
    {
        float min = Mathf.Clamp(range.min, 15f, 60f);
        float max = Mathf.Clamp(range.max, 20f, 65f);
        if (max < min + 5f)
        {
            max = Mathf.Min(65f, min + 5f);
        }
        return new FloatRange(min, max);
    }
}
