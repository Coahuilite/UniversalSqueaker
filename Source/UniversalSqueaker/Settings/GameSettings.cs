using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>Physical player settings. Concrete edits publish current behavior; the Mod save owner coalesces disk writes.</summary>
public partial class GameSettings : ModSettings
{
    private static readonly FloatRange FallbackBalancedDistanceRange = new(15f, 50f);

    // 0.2.3 产品决策:默认音源策略为 Fallback。Scribe 省略等于默认值的节点,
    // 因此"从未显式选择"的配置(无 voicePackMode 节点)天然落到 Fallback;显式 Off 仍写入节点并保留。
    public SqueakVoicePackMode voicePackMode = SqueakVoicePackMode.Fallback;
    public bool scaleFrequencyWithTalking = true;
    public bool localizeDebugActions = false;
    // Visibility only. Missing fields default on; an explicitly saved false is never repaired away.
    public bool showMainMenuEntry = true;
    public bool showOptionsEntry = true;
    public void SetMainMenuEntry(bool value)
    {
        if (showMainMenuEntry == value) return;
        showMainMenuEntry = value;
        QueuePersistence();
    }
    public void SetOptionsEntry(bool value)
    {
        if (showOptionsEntry == value) return;
        showOptionsEntry = value;
        QueuePersistence();
    }
    // S4 diagnostics foundation: player-facing camera indicator readout (no DevMode gating).
    public bool showCameraIndicator = false;
    public SqueakDevLoggingMode devLoggingMode = SqueakDevLoggingMode.Auto;
    // UIR-006 (WO-US-UI-REFACTOR-20261014): the selected official surface swatch. Add-only and
    // audio path reads it, so it publishes no runtime notification — persistence only. An unknown
    // hand-edited value is repaired at PostLoadInit and also fails soft on the read side.
    public string uiSwatchId = "core";
    public void SetUiSwatchId(string value)
    {
        string normalized = UsSwatchIds.IsKnown(value) ? value : UsSwatchIds.Default;
        if (string.Equals(uiSwatchId, normalized, StringComparison.Ordinal)) return;
        uiSwatchId = normalized;
        QueuePersistence();
    }
    // CORRECTIONS-R1 topic 3 (WO-US-UI-REFACTOR-20261014): the overview teaching's collapse state,
    // persisted as an explicit UI preference (default false = expanded, matching the first-open
    // doctrine). Like the swatch it is PURE APPEARANCE: add-only, default omitted at the Scribe
    // boundary, no audio-runtime notification anywhere - the setter queues persistence only - and the
    // global restore returns it through the same field table as every other setting.
    public bool uiIntroCollapsed = false;
    public void SetUiIntroCollapsed(bool value)
    {
        if (uiIntroCollapsed == value) return;
        uiIntroCollapsed = value;
        QueuePersistence();
    }
    public float globalVolumeFactor = 1f;
    public SqueakDistancePreset distancePreset = SqueakDistancePreset.Balanced;
    public FloatRange distanceRange = new(15f, 50f);
    public List<VoicePackSelectionRecord> voicePackSelections = new();
    // S2 layered action tuning table (Global/Race/Xenotype).
    public List<ActionTuningRecord> actionTuning = new();
    public bool EffectiveDevLogging => SqueakLog.EffectiveDevLogging;
    public void SetDevLoggingMode(SqueakDevLoggingMode value)
    {
        SqueakDevLoggingMode normalized = Enum.IsDefined(typeof(SqueakDevLoggingMode), value) ? value : SqueakDevLoggingMode.Auto;
        if (devLoggingMode == normalized) return;
        devLoggingMode = normalized;
        ApplyDevLoggingModeToRuntime(true);
        QueuePersistence();
    }

    private void ApplyDevLoggingModeToRuntime(bool announceChange)
    {
        bool wasEnabled = SqueakLog.EffectiveDevLogging;
        SqueakLog.Configure(devLoggingMode);
        if (wasEnabled != SqueakLog.EffectiveDevLogging) SqueakLog.ResetSession();
        if (announceChange) SqueakLog.LoggingModeChanged(devLoggingMode, SqueakLog.EffectiveDevLogging);
    }

    public void ApplyToRuntime()
    {
        GameRuntime.PublishConfiguration(this);
        Patch_DebugTabMenu_Actions.SetEnabled(localizeDebugActions);
        SqueakDebug.ShowCameraIndicator = showCameraIndicator;
        CompSqueaker.ApplyDistanceRange(ClampDistanceRange(distanceRange));
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
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        float clamped = Mathf.Clamp(value, 0f, 1f);
        if (System.Math.Abs(clamped - globalVolumeFactor) < 0.0001f) return;
        globalVolumeFactor = clamped;
        PublishBehaviorConfiguration();
        QueuePersistence();
    }



    /// <summary>S4-Vol: camera-height attenuation range. Clamped with the existing distance-range
    /// constraints; any manual edit switches the preset to Custom.</summary>
    internal void SetDistanceRange(float start, float end)
    {
        if (float.IsNaN(start) || float.IsInfinity(start)
            || float.IsNaN(end) || float.IsInfinity(end)) return;
        FloatRange clamped = ClampDistanceRange(new FloatRange(start, end));
        if (System.Math.Abs(clamped.min - distanceRange.min) < 0.0001f
            && System.Math.Abs(clamped.max - distanceRange.max) < 0.0001f
            && distancePreset == SqueakDistancePreset.Custom) return;
        distanceRange = clamped;
        distancePreset = SqueakDistancePreset.Custom;
        NotifyDistanceRuntimeChanged();
        QueuePersistence();
    }





    /// <summary>
    /// S4 diagnostics foundation: player-facing camera indicator toggle. Cheap: updates the runtime
    /// static directly (no resolver rebuild) and queues persistence.
    /// </summary>
    internal void SetTalkingScaling(bool value)
    {
        if (scaleFrequencyWithTalking == value) return;
        scaleFrequencyWithTalking = value; PublishBehaviorConfiguration(); QueuePersistence();
    }

    internal void SetCameraIndicator(bool value)
    {
        if (showCameraIndicator == value) return;
        showCameraIndicator = value;
        SqueakDebug.ShowCameraIndicator = value;
        QueuePersistence();
    }





    /// <summary>UI wiring: localize vanilla debug-menu action names. Applies through the Harmony
    /// patch's enable switch (which resets the debug action cache) and queues persistence.</summary>
    internal void SetLocalizeDebugActions(bool value)
    {
        if (localizeDebugActions == value) return;
        localizeDebugActions = value;
        Patch_DebugTabMenu_Actions.SetEnabled(value);
        QueuePersistence();
    }

    public void NotifyContinuousXenotypeRuntimeChanged() => GameRuntime.PublishConfiguration(this);
    public void NotifyDiscreteResolverRuntimeChanged() => GameRuntime.PublishConfiguration(this);
    public void QueuePersistence() => SqueakerMod.Instance?.QueueSettingsSave();


    internal void ApplySettingsRuntimeSideEffects(bool announceLoggingChange = true)
    {
        ApplyDevLoggingModeToRuntime(announceLoggingChange);
    }

    /// <summary>Change only the exact row's scope field; keep other fields and the template reset anchor.</summary>
    internal void SetActionTuningScope(string actionKey, string raceDefName, string xenotypeDefName, BehaviorScope? scope)
    {
        actionTuning ??= new List<ActionTuningRecord>();
        // 按 IsValidLayer 语义归一：raceDefName 空 + xenotypeDefName 非空 = 非法，直接忽略。
        bool hasRace = !string.IsNullOrEmpty(raceDefName);
        bool hasXeno = !string.IsNullOrEmpty(xenotypeDefName);
        if (!hasRace && hasXeno) return;
        if (!Authoring.AuthorXml.ActionId(actionKey)) return;

        // 身份比较统一走归一化后的本地量：helper 自身按 ?? "" 规则比较，调用点也不再让可空参数
        // 直接流进非空形参（否则 Nullable + TreatWarningsAsErrors 会以 CS8604 拒绝构建）。
        string actionRaceDefName = raceDefName ?? "";
        string actionXenotypeDefName = xenotypeDefName ?? "";

        // last-wins 取末个匹配行：写入落在这行，读回来的也是它。
        ActionTuningRecord? existing = null;
        foreach (ActionTuningRecord candidate in actionTuning)
        {
            if (SameActionTuningIdentity(candidate, actionKey, actionRaceDefName, actionXenotypeDefName))
            {
                existing = candidate;
            }
        }

        if (scope == null)
        {
            // 清除点名的那一个字段：组内**每一行**都要清（否则较早的重复行会继续贡献旧作用域而让清除
            // 变成空操作），然后只删「不再承载任何字段」的行。
            foreach (ActionTuningRecord row in actionTuning)
            {
                if (SameActionTuningIdentity(row, actionKey, actionRaceDefName, actionXenotypeDefName))
                {
                    row.hasScope = false;
                }
            }

            actionTuning.RemoveAll(c => SameActionTuningIdentity(c, actionKey, actionRaceDefName, actionXenotypeDefName)
                && !CarriesAnyActionTuningField(c));

            NotifyDiscreteResolverRuntimeChanged();
            QueuePersistence();
            return;
        }

        BehaviorScope effective = scope.Value;
        // 内置键按 SupportedScopes 归一（Draft/Undraft/Equip 仅支持 ActiveCommand），避免写入运行时永远不匹配的作用域。
        if (Kernel.BuiltInActionKeys.Contains(actionKey))
            effective = Authoring.BuiltInPawnContracts.NormalizeScope(actionKey, effective);

        ActionTuningRecord target;
        if (existing != null)
        {
            target = existing;
        }
        else
        {
            target = new ActionTuningRecord
            {
                actionKey = actionKey,
                raceDefName = actionRaceDefName,
                xenotypeDefName = actionXenotypeDefName,
            };
            actionTuning.Add(target);
        }

        target.hasScope = true;
        target.scope = effective;

        // 较早的重复行不得在同一个字段上竞争（否则 last-wins 会取决于列表顺序），但它们承载的**其它**
        // 字段全部保留；只有清空后什么都不剩的行才删除。
        foreach (ActionTuningRecord row in actionTuning)
        {
            if (ReferenceEquals(row, target)) continue;
            if (SameActionTuningIdentity(row, actionKey, actionRaceDefName, actionXenotypeDefName))
            {
                row.hasScope = false;
            }
        }

        actionTuning.RemoveAll(c => !ReferenceEquals(c, target)
            && SameActionTuningIdentity(c, actionKey, actionRaceDefName, actionXenotypeDefName)
            && !CarriesAnyActionTuningField(c));

        NotifyDiscreteResolverRuntimeChanged();
        QueuePersistence();
    }

    private static bool CarriesAnyActionTuningField(ActionTuningRecord? record) => record != null
        && (record.CarriesBehaviorFields || record.hasScope || record.responses.Count > 0 || record.responseParameters.Count > 0 || record.sourcePresetDefName != "");
    private static bool SameActionTuningIdentity(ActionTuningRecord? row, string action, string race, string xeno) => row != null
        && row.serviceFamily == "Pawn" && row.actionKey == action && row.raceDefName == race && row.xenotypeDefName == xeno;

    internal static bool TryFindActionBaseline(UsBehaviorPresetDef? preset, string action, string race, string xeno, out BehaviorValues? values)
    {
        values = null;
        if (preset == null) return false;
        try
        {
            if (!BehaviorPresetImporter.ValuesFor(preset, race, xeno).TryGetValue(action, out var found)) return false;
            values = found.Copy(); return true;
        }
        catch (ArgumentException) { return false; } // Invalid current template is an unavailable reset target, not a window failure.
    }
    internal bool ResetActionTuningToPreset(string action, string race, string xeno, UsBehaviorPresetDef? preset)
    {
        if (!IsValidTuningDomain(race, xeno) || preset == null) return false;
        var anchor = actionTuning.LastOrDefault(x => SameActionTuningIdentity(x, action, race, xeno))?.sourcePresetDefName;
        if (anchor != preset.defName || !TryFindActionBaseline(preset, action, race, xeno, out var template)) return false;
        var current = ReadPlayerActionOverride(action, race, xeno);
        current.Overlay(template!);
        ReplacePlayerValues(action, race, xeno, current, anchor!);
        PublishBehaviorConfiguration(); QueuePersistence(); return true;
    }
    internal PresetImportResult ImportBaselinePreset(UsBehaviorPresetDef preset, BehaviorPresetImporter.Selection selection)
    {
        var result = BehaviorPresetImporter.Import(preset, selection, this);
        if (result.ActionsImported > 0) { PublishBehaviorConfiguration(); QueuePersistence(); }
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
        VoicePackSelectionRecord? record = (voicePackSelections ?? new List<VoicePackSelectionRecord>())
            .LastOrDefault(x => VoicePackSelectionRecord.SameDomain(x, scope, raceDefName, target));
        List<string> keys = new(record?.enabledPackKeys ?? new List<string>());
        SqueakXenotypeCatalogSnapshot catalog = SqueakXenotypeCatalog.Current;
        if (scope == SqueakVoicePackScope.Xenotype && !ModsConfig.BiotechActive) return new SqueakVoicePackDomainStatus(SqueakVoicePackDomainState.Dormant, keys);
        IEnumerable<VoicePackInfo> domainPacks = catalog.GetVoicePackDomainPacks(scope, scope == SqueakVoicePackScope.Race ? raceDefName : target);
        // 双键域身份：Xenotype 状态必须按 (race, xeno) 过滤，避免同 xeno 跨 race 的包串扰。
        if (scope == SqueakVoicePackScope.Xenotype)
            domainPacks = domainPacks.Where(pack => string.Equals(pack.raceDefName, raceDefName, StringComparison.Ordinal));
        HashSet<string> domainKeys = new(StringComparer.Ordinal);
        foreach (VoicePackInfo pack in domainPacks)
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



    private static FloatRange ClampDistanceRange(FloatRange range)
    {
        float min = float.IsNaN(range.min) || float.IsInfinity(range.min) ? 15f : Mathf.Clamp(range.min, 15f, 60f);
        float max = float.IsNaN(range.max) || float.IsInfinity(range.max) ? 50f : Mathf.Clamp(range.max, 20f, 65f);
        if (max < min + 5f)
        {
            max = Mathf.Min(65f, min + 5f);
        }
        return new FloatRange(min, max);
    }
}
