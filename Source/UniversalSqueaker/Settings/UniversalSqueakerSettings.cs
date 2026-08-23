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
    // 保留的 Scribe 标记：US 是数据驱动产品，没有内置 race 种子；该字段仅为旧 Scribe 形状兼容而保留。
    public bool voicePackDefaultSeeded;
    // v4 owns the race-aware selection/preset record identity migration.
    public int settingsSchemaVersion = CurrentSettingsSchemaVersion;
    public bool scaleCooldownWithTimeSpeed = true;
    public bool scaleFrequencyWithTalking = true;
    public bool scalePeriodicWithAudiblePopulation = true;
    public bool localizeDebugActions = false;
    public bool developerToolsEnabled = false;
    public SqueakDevLoggingMode devLoggingMode = SqueakDevLoggingMode.Auto;
    public float globalCooldownMultiplier = 1f;
    public SqueakDistancePreset distancePreset = SqueakDistancePreset.Balanced;
    public FloatRange distanceRange = new(15f, 50f);
    public Dictionary<SqueakMood, SqueakMoodMod> moodOverrides = new();
    public List<VoicePackSelectionRecord> voicePackSelections = new();
    public List<XenotypePresetRecord> xenotypePresets = new();
    // Canonical persisted list keeps old saves (with no records) enabled by default.
    public List<GlobalActionEnabledRecord> globalActionEnabled = new();
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

    // Settings window session scaffolding. Phase 3 replaces the placeholder draw with the componentized
    // minimal UI; these hooks exist now so the Mod shell and its close/save lifecycle compile and run.
    internal void BeginSettingsSession() { }
    internal void EndSettingsSession() { }
    internal void RequestXenotypeTabOnNextDraw() { }
    internal void ClearXenotypeTabRequest() { }

    public void DrawSettings(Rect inRect)
    {
        Widgets.Label(inRect, SqueakLabels.SettingsCategory);
    }

    public void ApplyToRuntime()
    {
        // Publish this independent gate before resolver rebuilding. A disabled production action must not
        // need resolver/catalog/context access merely to decide that it is silent.
        SqueakGlobalActionPolicy.Publish(this);
        SqueakRuntimeResolver.NotifyDiscreteResolverChange(this, SqueakXenotypeCatalog.Current);
        CompSqueaker.ScaleCooldownWithTimeSpeed = scaleCooldownWithTimeSpeed;
        CompSqueaker.ScaleFrequencyWithTalking = scaleFrequencyWithTalking;
        CompSqueaker.ScalePeriodicWithAudiblePopulation = scalePeriodicWithAudiblePopulation;
        CompSqueaker.GlobalCooldownMultiplier = Mathf.Clamp(globalCooldownMultiplier, 0f, 3f);
        CompSqueaker.ApplyDistanceRange(ClampDistanceRange(distanceRange));
    }

    /// <summary>Cheap controls are same-frame static runtime values and never rebuild the resolver.</summary>
    public void NotifyCheapRuntimeChanged()
    {
        CompSqueaker.ScaleCooldownWithTimeSpeed = scaleCooldownWithTimeSpeed;
        CompSqueaker.ScaleFrequencyWithTalking = scaleFrequencyWithTalking;
        CompSqueaker.ScalePeriodicWithAudiblePopulation = scalePeriodicWithAudiblePopulation;
        CompSqueaker.GlobalCooldownMultiplier = Mathf.Clamp(globalCooldownMultiplier, 0f, 3f);
    }

    public void NotifyDistanceRuntimeChanged() => CompSqueaker.ApplyDistanceRange(ClampDistanceRange(distanceRange));
    /// <summary>Global mood is read directly by CompSqueaker during playback; no resolver rebuild is needed.</summary>
    public void NotifyGlobalMoodRuntimeChanged() { }
    public void NotifyContinuousXenotypeRuntimeChanged() => SqueakRuntimeResolver.NotifyContinuousResolverChange(this, SqueakXenotypeCatalog.Current);
    public void NotifyDiscreteResolverRuntimeChanged() => SqueakRuntimeResolver.NotifyDiscreteResolverChange(this, SqueakXenotypeCatalog.Current);
    public void QueuePersistence() => UniversalSqueakerMod.Instance?.QueueSettingsSave();
    public void FlushPendingRuntimeForPreview() => SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);

    /// <summary>
    /// Kept for call-site compatibility. US has no product race seed; the legacy single-target overload
    /// resolves the race from the catalog when there is exactly one admitted race. Multi-race callers must
    /// use the race-aware overload.
    /// </summary>
    internal void EnsureBuiltInRaceDefault()
    {
        // A failed v3/v1 transaction must remain untouched until its next startup retry.
        if (settingsSchemaVersion < CurrentSettingsSchemaVersion || voicePackSchemaVersion < CurrentVoicePackSchemaVersion) return;
        if (voicePackDefaultSeeded) return;
        voicePackDefaultSeeded = true;
        // US is data-driven and has no built-in race seed. The catalog derives all race domains from
        // loaded VoicePack declarations; an empty source starts with no seeded selection.
    }

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

    public bool IsActionGloballyEnabled(SqueakAction action)
    {
        return GetActionGlobalScope(action) != SqueakActionScope.Disabled;
    }

    public SqueakActionScope GetActionGlobalScope(SqueakAction action)
    {
        SqueakActionScope scope = SqueakActionDefinitions.Get(action).DefaultScope;
        foreach (GlobalActionEnabledRecord record in globalActionEnabled ?? new List<GlobalActionEnabledRecord>())
            if (record != null && record.action == action) scope = SqueakActionDefinitions.NormalizeScope(action, record.scope);
        return scope;
    }

    internal void SetActionGloballyEnabled(SqueakAction action, bool enabled)
    {
        SetActionGlobalScope(action, enabled ? SqueakActionDefinitions.Get(action).DefaultScope : SqueakActionScope.Disabled);
    }

    internal void SetActionGlobalScope(SqueakAction action, SqueakActionScope scope)
    {
        scope = SqueakActionDefinitions.NormalizeScope(action, scope);
        GlobalActionEnabledRecord? record = null;
        foreach (GlobalActionEnabledRecord candidate in globalActionEnabled)
            if (candidate != null && candidate.action == action) record = candidate;
        if (record == null) globalActionEnabled.Add(new GlobalActionEnabledRecord { action = action, enabled = scope != SqueakActionScope.Disabled, scope = scope, scopeWasLoaded = true });
        else { record.enabled = scope != SqueakActionScope.Disabled; record.scope = scope; record.scopeWasLoaded = true; }
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

    // 数据驱动:mood/action 列表从所有挂 CompProperties_Squeaker 的 ThingDef 读(XML actions/moodMods)。
    // XML 加配置自动出现在运行时默认值,无需改 C# 数组。DefDatabase 加载后不变,首次访问懒加载缓存。
    internal static IEnumerable<CompProperties_Squeaker> ConfiguredSqueakers()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def.comps == null)
            {
                continue;
            }

            foreach (CompProperties cp in def.comps)
            {
                if (cp is CompProperties_Squeaker sq)
                {
                    yield return sq;
                }
            }
        }
    }

    private static FloatRange GetDistancePresetRange(SqueakDistancePreset preset)
    {
        foreach (CompProperties_Squeaker sq in ConfiguredSqueakers())
        {
            foreach (SqueakDistancePresetConfig cfg in sq.distancePresets)
            {
                if (cfg.preset == preset)
                {
                    return cfg.range;
                }
            }
        }

        return preset switch
        {
            SqueakDistancePreset.Conservative => new FloatRange(15f, 65f),
            SqueakDistancePreset.Strong => new FloatRange(15f, 40f),
            _ => FallbackBalancedDistanceRange,
        };
    }

    private static bool GetDefaultScaleFrequencyWithTalking()
    {
        foreach (CompProperties_Squeaker sq in ConfiguredSqueakers())
        {
            return sq.scaleFrequencyWithTalking;
        }

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
