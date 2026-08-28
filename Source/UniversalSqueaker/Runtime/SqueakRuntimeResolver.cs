using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker;

/// <summary>Immutable resolver: audio selection is independent of optional Xenotype behavior and mood deltas.
/// US is neutral: every race domain comes from the pawn's real raceDefName, never from a product seed.</summary>
public static class SqueakRuntimeResolver
{
    private static SqueakRuntimeSnapshot current = SqueakRuntimeSnapshot.GlobalOnly;
    private const float ContinuousDelaySeconds = .075f;
    private const float ContinuousMaxWaitSeconds = .150f;
    private static UniversalSqueakerSettings? pendingSettings;
    private static SqueakXenotypeCatalogSnapshot? pendingCatalog;
    private static long desiredRevision;
    private static long appliedRevision;
    private static bool pendingIsDiscrete;
    private static float continuousFirstChangeAt;
    private static float continuousDueAt;
    private static float lastContinuousPublishAt = float.NegativeInfinity;
    private static int mainThreadId;
    public static long ResolverRebuildCount { get; private set; }
    public static long RuntimeFlushCount { get; private set; }
    public static SqueakRuntimeSnapshot Current => Volatile.Read(ref current);

    /// <summary>Called by the mod constructor on Unity's main thread. BuildSnapshot reads mutable settings, so this resolver has one publisher.</summary>
    internal static void InitializeMainThread()
    {
        int caller = Thread.CurrentThread.ManagedThreadId;
        if (mainThreadId == 0) mainThreadId = caller;
        else Debug.Assert(mainThreadId == caller, "SqueakRuntimeResolver main thread changed.");
    }

    private static bool EnsureMainThread()
    {
        bool valid = mainThreadId != 0 && mainThreadId == Thread.CurrentThread.ManagedThreadId;
        Debug.Assert(valid, "SqueakRuntimeResolver mutation attempted off Unity's initialized main thread.");
        return valid;
    }

    /// <summary>Use for sliders/editor deltas that affect Xenotype resolver data. Trailing 75 ms, bounded at 150 ms.</summary>
    public static void NotifyContinuousResolverChange(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        if (!EnsureMainThread()) return;
        float now = Time.realtimeSinceStartup;
        pendingSettings = settings; pendingCatalog = catalog;
        if (desiredRevision == appliedRevision) continuousFirstChangeAt = now;
        desiredRevision++;
        continuousDueAt = Mathf.Max(lastContinuousPublishAt + ContinuousMaxWaitSeconds,
            Mathf.Min(now + ContinuousDelaySeconds, continuousFirstChangeAt + ContinuousMaxWaitSeconds));
    }

    /// <summary>Use for audio mode, selection, action scope, and catalog changes. It is effective before returning.</summary>
    public static void NotifyDiscreteResolverChange(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog)
    {
        if (!EnsureMainThread()) return;
        pendingSettings = settings; pendingCatalog = catalog;
        desiredRevision++;
        pendingIsDiscrete = true;
        continuousFirstChangeAt = Time.realtimeSinceStartup;
        continuousDueAt = continuousFirstChangeAt;
        FlushPendingRuntimeChanges(true);
    }

    public static void TickPendingRuntimeChanges()
    {
        if (!EnsureMainThread()) return;
        FlushPendingRuntimeChanges(false);
    }

    /// <summary>Flushes a due continuous edit, or all pending work when forced. Never writes settings.</summary>
    public static void FlushPendingRuntimeChanges(bool force = true)
    {
        if (!EnsureMainThread()) return;
        if (desiredRevision == appliedRevision || pendingSettings == null || pendingCatalog == null) return;
        float now = Time.realtimeSinceStartup;
        if (!force && !pendingIsDiscrete && now < continuousDueAt) return;
        long revision = desiredRevision;
        if (!TryPublish(pendingSettings, pendingCatalog, out _)) return;
        appliedRevision = revision;
        if (!pendingIsDiscrete) lastContinuousPublishAt = now;
        if (appliedRevision == desiredRevision)
        {
            pendingIsDiscrete = false;
            pendingSettings = null;
            pendingCatalog = null;
        }
    }

    /// <summary>Builds and publishes exactly one immutable snapshot. Counters advance only after publication.</summary>
    private static bool TryPublish(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog, out SqueakRuntimeSnapshot published)
    {
        if (!EnsureMainThread()) { published = Current; return false; }
        Dictionary<string, LayerActionDelta> globalActionLayers = BuildGlobalActionLayers(settings);
        Dictionary<SqueakMood, LayerMoodDelta> globalMoodLayers = BuildGlobalMoodLayers(settings);
        try { published = BuildSnapshot(settings, catalog, globalActionLayers, globalMoodLayers); }
        catch (Exception ex) { SqueakLog.ResolverRebuildFailed(ex); published = BuildFallback(globalActionLayers, globalMoodLayers, settings); }
        Volatile.Write(ref current, published);
        ResolverRebuildCount++;
        RuntimeFlushCount++;
        return true;
    }

    /// <summary>S5 纯折叠：Global 层动作源数据（层 0 记录，同键字段级合并）。
    /// 外部动作键不进入本表（与 Pure 层契约一致；支持外部动作调音待生态出现后评估，YAGNI）。</summary>
    private static Dictionary<string, LayerActionDelta> BuildGlobalActionLayers(UniversalSqueakerSettings settings)
    {
        Dictionary<string, LayerActionDelta> layers = new(StringComparer.Ordinal);
        foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
        {
            if (record == null || record.IsValidLayer(out int layer) == false || layer != 0) continue;
            if (string.IsNullOrEmpty(record.actionKey)) continue;
            LayerActionDelta delta = FromActionRecord(record);
            layers[record.actionKey] = layers.TryGetValue(record.actionKey, out LayerActionDelta existing) ? existing.Merge(delta) : delta;
        }
        return layers;
    }

    /// <summary>Global 层解析结果（DefaultScope &lt; Global 折叠，17 内置键全量具体化）。</summary>
    private static Dictionary<string, RuntimeActionDelta> ResolveGlobalActions(Dictionary<string, LayerActionDelta> layers)
    {
        Dictionary<string, RuntimeActionDelta> result = new(StringComparer.Ordinal);
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            string key = UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString();
            LayerActionDelta? layer = layers.TryGetValue(key, out LayerActionDelta value) ? value : (LayerActionDelta?)null;
            result[key] = ToRuntime(ResolvedActionDelta.Resolve(SqueakActionDefinitions.Get(action).DefaultScope, layer, null, null));
        }
        return result;
    }

    private static LayerActionDelta FromActionRecord(ActionTuningRecord record) => new(
        record.scope, record.hasScope,
        record.hasIntervalMultiplier, Sanitize(record.intervalMultiplier),
        record.hasProbabilityMultiplier, Sanitize(record.probabilityMultiplier));

    private static LayerMoodDelta FromMoodRecord(MoodTuningRecord record)
    {
        // 防御性消毒（所有来源的单一入口：Scribe 旧档、预设导入、手编文件）：
        // 非有限值回退 1；jitter 反序交换（FloatRange.RandomInRange 需 min<=max）。
        float pitch = float.IsFinite(record.pitchFactor) ? record.pitchFactor : 1f;
        float volume = float.IsFinite(record.volumeFactor) ? record.volumeFactor : 1f;
        float jMin = record.pitchJitter.min;
        float jMax = record.pitchJitter.max;
        if (!float.IsFinite(jMin) || !float.IsFinite(jMax)) { jMin = 1f; jMax = 1f; }
        else if (jMin > jMax) { (jMin, jMax) = (jMax, jMin); }
        return new LayerMoodDelta(record.hasPitchFactor, pitch, record.hasVolumeFactor, volume, record.hasPitchJitter, jMin, jMax);
    }

    private static RuntimeActionDelta ToRuntime(ResolvedActionDelta delta) => new(delta.Scope, delta.IntervalMultiplier, delta.ProbabilityMultiplier);

    private static RuntimeMoodDelta ToRuntime(ResolvedMoodDelta delta) => new(delta.HasPitchFactor, delta.PitchFactor, delta.HasVolumeFactor, delta.VolumeFactor, delta.HasPitchJitter, new FloatRange(delta.JitterMin, delta.JitterMax));

    private static SqueakRuntimeSnapshot BuildSnapshot(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog, Dictionary<string, LayerActionDelta> globalActionLayers, Dictionary<SqueakMood, LayerMoodDelta> globalMoodLayers)
    {
        IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> behavior = BuildBehavior(settings);
        IReadOnlyDictionary<RaceKey, LayerBehaviorAggregate> raceBehavior = BuildRaceBehavior(settings);
        Dictionary<AudioDomain, HashSet<string>> selection = BuildSelections(settings.voicePackSelections);
        HashSet<SoundDef> known = SqueakKernelAdapter.CollectKnownSounds(catalog);
        List<VoicePackEntry> entries = SqueakKernelAdapter.BuildEntries(catalog, selection);
        SqueakPoolRegistry registry = new(entries, SqueakKernelAdapter.BuildBuiltIn());
        Dictionary<string, RuntimeActionDelta> globalActions = ResolveGlobalActions(globalActionLayers);
        Dictionary<SqueakMood, RuntimeMoodDelta> globalMoods = ResolveGlobalMoods(globalMoodLayers);
        Dictionary<AudioDomain, ResolvedSqueakContext> contexts = new();
        Dictionary<RaceKey, ResolvedSqueakContext> raceContexts = new();
        foreach (KeyValuePair<RaceKey, LayerBehaviorAggregate> raceEntry in raceBehavior)
            raceContexts.Add(raceEntry.Key, BuildContext(null, raceEntry.Value, null, globalActionLayers, globalMoodLayers, 1f));
        if (ModsConfig.BiotechActive)
        {
            foreach (AudioDomain domain in CollectXenoDomains(settings, catalog, behavior))
            {
                string xenoName = domain.Xenotype?.DefName ?? "";
                XenotypeDef? xenotype = null;
                if (!string.IsNullOrEmpty(xenoName) && catalog.XenotypeByDefName.TryGetValue(xenoName, out XenotypeDef? xd))
                    xenotype = xd;
                if (!string.IsNullOrEmpty(xenoName) && catalog.AmbiguousCanonicalDefNames.Contains(xenoName)) xenotype = null;
                raceBehavior.TryGetValue(domain.Race, out LayerBehaviorAggregate? raceBuilder);
                behavior.TryGetValue(domain, out LayerBehaviorAggregate? xenoBuilder);
                ResolvedSqueakContext xenoContext = BuildContext(xenotype, raceBuilder, xenoBuilder, globalActionLayers, globalMoodLayers, xenoBuilder?.OverallIntervalMultiplier ?? 1f);
                contexts.Add(domain, xenoContext);
            }
        }
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            SoundDef? sound = DefDatabase<SoundDef>.GetNamedSilentFail(SqueakActionDefinitions.Get(action).AudioKey);
            if (sound != null) known.Add(sound);
        }
        return new SqueakRuntimeSnapshot(contexts, raceContexts, registry, known, NormalizeMode(settings.voicePackMode), globalActions, globalMoods, catalog.AmbiguousCanonicalDefNames, settings.AllowEasterEggSounds);
    }

    /// <summary>收集所有应构建 xeno context 的 (race, xeno) 域：catalog 包声明、选择记录、已有调音域。</summary>
    private static IReadOnlyList<AudioDomain> CollectXenoDomains(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog, IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> behavior)
    {
        List<(string race, string? xeno)> sources = new();
        foreach (KeyValuePair<string, IReadOnlyList<SqueakVoicePackDef>> pair in catalog.XenotypePacksByDefName)
        {
            foreach (SqueakVoicePackDef pack in pair.Value)
            {
                if (pack == null || string.IsNullOrEmpty(pack.raceDefName) || string.IsNullOrEmpty(pack.targetDefName)) continue;
                sources.Add((pack.raceDefName, pack.targetDefName));
            }
        }
        foreach (VoicePackSelectionRecord record in settings.voicePackSelections ?? new List<VoicePackSelectionRecord>())
        {
            if (record == null || record.scope != SqueakVoicePackScope.Xenotype) continue;
            sources.Add((record.raceDefName ?? "", record.xenotypeDefName ?? ""));
        }
        foreach (AudioDomain domain in behavior.Keys)
        {
            if (domain.Xenotype != null) sources.Add((domain.Race.DefName, domain.Xenotype.Value.DefName));
        }
        return AudioDomains.Collect(sources);
    }

    /// <summary>S5 纯折叠：Global 层心情源数据（层 0 记录，同 mood 字段级合并）。</summary>
    private static Dictionary<SqueakMood, LayerMoodDelta> BuildGlobalMoodLayers(UniversalSqueakerSettings settings)
    {
        Dictionary<SqueakMood, LayerMoodDelta> layers = new();
        foreach (MoodTuningRecord record in settings.moodTuning ?? new List<MoodTuningRecord>())
        {
            if (record == null || record.IsValidLayer(out int layer) == false || layer != 0) continue;
            LayerMoodDelta delta = FromMoodRecord(record);
            layers[record.mood] = layers.TryGetValue(record.mood, out LayerMoodDelta existing) ? existing.Merge(delta) : delta;
        }
        return layers;
    }

    /// <summary>Global 层解析结果（仅含被显式决定的因子——GetMoodDelta 空语义保持：全默认 = 无 delta）。</summary>
    private static Dictionary<SqueakMood, RuntimeMoodDelta> ResolveGlobalMoods(Dictionary<SqueakMood, LayerMoodDelta> layers)
    {
        Dictionary<SqueakMood, RuntimeMoodDelta> result = new();
        foreach (SqueakMood mood in Enum.GetValues(typeof(SqueakMood)))
        {
            LayerMoodDelta? layer = layers.TryGetValue(mood, out LayerMoodDelta value) ? value : (LayerMoodDelta?)null;
            ResolvedMoodDelta resolved = ResolvedMoodDelta.Resolve(layer, null, null);
            if (!resolved.IsDefault) result[mood] = ToRuntime(resolved);
        }
        return result;
    }

    private static IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> BuildBehavior(UniversalSqueakerSettings settings)
    {
        List<(AudioDomain domain, string actionKey, LayerActionDelta delta)> actions = new();
        List<(AudioDomain domain, SqueakMood mood, LayerMoodDelta delta)> moods = new();
        List<(AudioDomain domain, float overallMultiplier)> multipliers = new();

        foreach (XenotypePresetRecord record in settings.xenotypePresets ?? new List<XenotypePresetRecord>())
        {
            if (record == null || string.IsNullOrEmpty(record.xenotypeDefName)) continue;
            if (!AudioDomains.TryCreate(record.raceDefName, record.xenotypeDefName, out AudioDomain domain)) continue;
            if (record.hasOverallIntervalMultiplier) multipliers.Add((domain, Sanitize(record.overallIntervalMultiplier)));
            // S5: XenotypePresetRecord.moodOverrides 已迁移至 moodTuning 层 2，本字段不再被运行时消费。
            foreach (XenotypeActionBehaviorOverride action in record.actionOverrides ?? new List<XenotypeActionBehaviorOverride>())
            {
                if (action == null) continue;
                string actionKey = ActionKey.For(action.action) ?? action.action.ToString();
                actions.Add((domain, actionKey, new LayerActionDelta(
                    action.hasEnabled ? (action.enabled ? SqueakActionScope.AnyOccurrence : SqueakActionScope.Disabled) : SqueakActionScope.AnyOccurrence,
                    action.hasEnabled,
                    action.hasIntervalMultiplier, Sanitize(action.intervalMultiplier),
                    action.hasProbabilityMultiplier, Sanitize(action.probabilityMultiplier))));
            }
        }
        // S2: actionTuning 的 Xenotype 层记录叠加（layer==2），字段级覆盖旧 actionOverrides 派生值。
        foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
        {
            if (record == null || record.IsValidLayer(out int layer) == false || layer != 2) continue;
            if (!AudioDomains.TryCreate(record.raceDefName, record.xenotypeDefName, out AudioDomain domain)) continue;
            if (!ActionKey.TryParseBuiltIn(record.actionKey, out _)) continue;
            actions.Add((domain, record.actionKey, FromActionRecord(record)));
        }
        // S5: moodTuning 的 Xenotype 层心情记录叠加（layer==2），字段级覆盖迁移后的预设派生值。
        foreach (MoodTuningRecord record in settings.moodTuning ?? new List<MoodTuningRecord>())
        {
            if (record == null || record.IsValidLayer(out int layer) == false || layer != 2) continue;
            if (!AudioDomains.TryCreate(record.raceDefName, record.xenotypeDefName, out AudioDomain domain)) continue;
            moods.Add((domain, record.mood, FromMoodRecord(record)));
        }
        return SqueakTuningAggregator.Aggregate(actions, moods, multipliers);
    }

    /// <summary>H3 + S5: aggregate actionTuning/moodTuning Race-layer records (layer==1) into per-race builders.</summary>
    private static IReadOnlyDictionary<RaceKey, LayerBehaviorAggregate> BuildRaceBehavior(UniversalSqueakerSettings settings)
    {
        List<(AudioDomain domain, string actionKey, LayerActionDelta delta)> actions = new();
        List<(AudioDomain domain, SqueakMood mood, LayerMoodDelta delta)> moods = new();
        foreach (ActionTuningRecord record in settings.actionTuning ?? new List<ActionTuningRecord>())
        {
            if (record == null || record.IsValidLayer(out int layer) == false || layer != 1) continue;
            if (string.IsNullOrEmpty(record.raceDefName)) continue;
            if (!ActionKey.TryParseBuiltIn(record.actionKey, out _)) continue;
            actions.Add((AudioDomains.RaceOnly(record.raceDefName), record.actionKey, FromActionRecord(record)));
        }
        // S5: moodTuning 的 Race 层心情记录聚合（layer==1）。
        foreach (MoodTuningRecord record in settings.moodTuning ?? new List<MoodTuningRecord>())
        {
            if (record == null || record.IsValidLayer(out int layer) == false || layer != 1) continue;
            if (string.IsNullOrEmpty(record.raceDefName)) continue;
            moods.Add((AudioDomains.RaceOnly(record.raceDefName), record.mood, FromMoodRecord(record)));
        }
        IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> table = SqueakTuningAggregator.Aggregate(actions, moods, Array.Empty<(AudioDomain, float)>());
        Dictionary<RaceKey, LayerBehaviorAggregate> result = new();
        foreach (KeyValuePair<AudioDomain, LayerBehaviorAggregate> pair in table)
            if (pair.Key.IsRaceOnly) result[pair.Key.Race] = pair.Value;
        return result;
    }

    /// <summary>记录 → AudioDomain 键的 last-wins 选择集。域身份来自记录自身 (raceDefName, xenotypeDefName)。</summary>
    private static Dictionary<UniversalSqueaker.Kernel.AudioDomain, HashSet<string>> BuildSelections(IEnumerable<VoicePackSelectionRecord> records)
    {
        Dictionary<UniversalSqueaker.Kernel.AudioDomain, HashSet<string>> result = new();
        foreach (VoicePackSelectionRecord record in records ?? Array.Empty<VoicePackSelectionRecord>())
        {
            if (record == null || (record.scope != SqueakVoicePackScope.Race && record.scope != SqueakVoicePackScope.Xenotype)) continue;
            string raceDefName = record.raceDefName ?? "";
            string xenotypeDefName = record.scope == SqueakVoicePackScope.Xenotype ? record.xenotypeDefName ?? "" : "";
            if (string.IsNullOrEmpty(raceDefName)) continue;
            if (record.scope == SqueakVoicePackScope.Xenotype && string.IsNullOrEmpty(xenotypeDefName)) continue;
            UniversalSqueaker.Kernel.AudioDomain domain = record.scope == SqueakVoicePackScope.Xenotype
                ? new UniversalSqueaker.Kernel.AudioDomain(new UniversalSqueaker.Kernel.RaceKey(raceDefName), new UniversalSqueaker.Kernel.XenotypeKey(xenotypeDefName))
                : new UniversalSqueaker.Kernel.AudioDomain(new UniversalSqueaker.Kernel.RaceKey(raceDefName), null);
            result[domain] = new HashSet<string>((record.enabledPackKeys ?? new List<string>()).Where(k => !string.IsNullOrEmpty(k)), StringComparer.Ordinal);
        }
        return result;
    }

    /// <summary>S5 纯折叠上下文构建：DefaultScope &lt; Global &lt; Race &lt; Xenotype（动作与心情同规则）。
    /// raceBuilder = Race 层源（null 时跳过）；xenoBuilder = Xenotype 层源（null 时跳过）。
    /// 心情仅收录被任何层显式决定的因子（GetMoodDelta 空语义保持：全默认 = 无 delta）。</summary>
    private static ResolvedSqueakContext BuildContext(
        XenotypeDef? xenotype,
        LayerBehaviorAggregate? raceBuilder,
        LayerBehaviorAggregate? xenoBuilder,
        Dictionary<string, LayerActionDelta> globalActionLayers,
        Dictionary<SqueakMood, LayerMoodDelta> globalMoodLayers,
        float overallIntervalMultiplier)
    {
        Dictionary<string, RuntimeActionDelta> actions = new(StringComparer.Ordinal);
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            string key = UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString();
            LayerActionDelta? global = globalActionLayers.TryGetValue(key, out LayerActionDelta g) ? g : (LayerActionDelta?)null;
            LayerActionDelta? race = raceBuilder != null && raceBuilder.Actions.TryGetValue(key, out LayerActionDelta r) ? r : (LayerActionDelta?)null;
            LayerActionDelta? xeno = xenoBuilder != null && xenoBuilder.Actions.TryGetValue(key, out LayerActionDelta x) ? x : (LayerActionDelta?)null;
            actions[key] = ToRuntime(ResolvedActionDelta.Resolve(SqueakActionDefinitions.Get(action).DefaultScope, global, race, xeno));
        }

        Dictionary<SqueakMood, RuntimeMoodDelta> moods = new();
        foreach (SqueakMood mood in Enum.GetValues(typeof(SqueakMood)))
        {
            LayerMoodDelta? global = globalMoodLayers.TryGetValue(mood, out LayerMoodDelta g) ? g : (LayerMoodDelta?)null;
            LayerMoodDelta? race = raceBuilder != null && raceBuilder.Moods.TryGetValue(mood, out LayerMoodDelta r) ? r : (LayerMoodDelta?)null;
            LayerMoodDelta? xeno = xenoBuilder != null && xenoBuilder.Moods.TryGetValue(mood, out LayerMoodDelta x) ? x : (LayerMoodDelta?)null;
            ResolvedMoodDelta resolved = ResolvedMoodDelta.Resolve(global, race, xeno);
            if (!resolved.IsDefault) moods[mood] = ToRuntime(resolved);
        }

        return new ResolvedSqueakContext(xenotype, overallIntervalMultiplier, actions, moods);
    }

    private static SqueakRuntimeSnapshot BuildFallback(Dictionary<string, LayerActionDelta> globalActionLayers, Dictionary<SqueakMood, LayerMoodDelta> globalMoodLayers, UniversalSqueakerSettings settings)
    {
        try
        {
            HashSet<SoundDef> known = new();
            foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
            {
                SoundDef? sound = DefDatabase<SoundDef>.GetNamedSilentFail(SqueakActionDefinitions.Get(action).AudioKey);
                if (sound != null) known.Add(sound);
            }
            SqueakPoolRegistry registry = new(
                Array.Empty<VoicePackEntry>(),
                SqueakKernelAdapter.BuildBuiltIn());
            // M1: 保留原模式——Disabled 真旁路不得被崩溃兜底改写（否则旁路 gate 失效，内置表可能发声）。
            return new SqueakRuntimeSnapshot(new Dictionary<AudioDomain, ResolvedSqueakContext>(), new Dictionary<RaceKey, ResolvedSqueakContext>(), registry, known, NormalizeMode(settings.voicePackMode), ResolveGlobalActions(globalActionLayers), ResolveGlobalMoods(globalMoodLayers), null, settings.AllowEasterEggSounds);
        }
        catch { return SqueakRuntimeSnapshot.GlobalOnly; }
    }

    private static SqueakVoicePackMode NormalizeMode(SqueakVoicePackMode mode) => mode == SqueakVoicePackMode.Fallback || mode == SqueakVoicePackMode.Remix || mode == SqueakVoicePackMode.Disabled ? mode : SqueakVoicePackMode.Vanilla;
    private static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, value);
}

public sealed class SqueakRuntimeSnapshot
{
    public static readonly SqueakRuntimeSnapshot GlobalOnly = new(new Dictionary<AudioDomain, ResolvedSqueakContext>(), SqueakPoolRegistry.Empty, new HashSet<SoundDef>(), SqueakVoicePackMode.Vanilla, null, null, false);
    private readonly IReadOnlyDictionary<AudioDomain, ResolvedSqueakContext> contexts;
    private readonly IReadOnlyDictionary<RaceKey, ResolvedSqueakContext> raceContexts;
    private readonly IReadOnlyDictionary<string, RuntimeActionDelta> globalActions;
    private readonly ResolvedSqueakContext globalContext;
    public readonly SqueakVoicePackMode VoicePackMode;
    public readonly IReadOnlyCollection<SoundDef> KnownMapSoundDefs;
    public readonly SqueakPoolRegistry Registry;
    public readonly bool AllowEggs;
    private readonly IReadOnlyCollection<string> ambiguousCanonicalNames;

    internal SqueakRuntimeSnapshot(Dictionary<AudioDomain, ResolvedSqueakContext> contexts, SqueakPoolRegistry registry, HashSet<SoundDef> known, SqueakVoicePackMode mode, Dictionary<string, RuntimeActionDelta>? globals, IEnumerable<string>? ambiguousNames, bool allowEggs)
        : this(contexts, new Dictionary<RaceKey, ResolvedSqueakContext>(), registry, known, mode, globals, null, ambiguousNames, allowEggs) { }

    internal SqueakRuntimeSnapshot(Dictionary<AudioDomain, ResolvedSqueakContext> contexts, Dictionary<RaceKey, ResolvedSqueakContext> raceContexts, SqueakPoolRegistry registry, HashSet<SoundDef> known, SqueakVoicePackMode mode, Dictionary<string, RuntimeActionDelta>? globals, Dictionary<SqueakMood, RuntimeMoodDelta>? globalMoods, IEnumerable<string>? ambiguousNames, bool allowEggs)
    {
        this.contexts = new ReadOnlyDictionary<AudioDomain, ResolvedSqueakContext>(contexts);
        this.raceContexts = new ReadOnlyDictionary<RaceKey, ResolvedSqueakContext>(raceContexts ?? new Dictionary<RaceKey, ResolvedSqueakContext>());
        Registry = registry;
        globalActions = new ReadOnlyDictionary<string, RuntimeActionDelta>(globals ?? new Dictionary<string, RuntimeActionDelta>());
        globalContext = new ResolvedSqueakContext(null, 1f, globals, globalMoods);
        VoicePackMode = mode;
        AllowEggs = allowEggs;
        KnownMapSoundDefs = new ReadOnlyCollection<SoundDef>(known.ToList());
        ambiguousCanonicalNames = new ReadOnlyCollection<string>((ambiguousNames ?? Array.Empty<string>()).ToList());
    }

    public ResolvedSqueakContext ResolveContext(Pawn pawn)
    {
        // 双键：域身份 = (pawn race, pawn xenotype)。规则由 Pure SqueakContextSelector 决定，绝不跨 race。
        string raceDefName = pawn?.def?.defName ?? "";
        bool hasRaceContext = !string.IsNullOrEmpty(raceDefName) && raceContexts.TryGetValue(new RaceKey(raceDefName), out _);
        ResolvedSqueakContext raceContext = hasRaceContext ? raceContexts[new RaceKey(raceDefName)] : globalContext;
        if (!ModsConfig.BiotechActive) return raceContext;
        XenotypeDef? xenotype = pawn?.genes?.Xenotype;
        string? defName = xenotype?.defName;
        ContextSelection selection = SqueakContextSelector.Select(raceDefName, defName, contexts.Keys, ambiguousCanonicalNames, hasRaceContext);
        if (selection.Kind != ContextSelectionKind.Xeno || selection.XenoDomain == null)
            return selection.Kind == ContextSelectionKind.Race ? raceContext : globalContext;
        ResolvedSqueakContext context = contexts[selection.XenoDomain.Value];
        if (context.Xenotype != null && !ReferenceEquals(context.Xenotype, xenotype))
            return WarnAndFallback(defName ?? "", "runtime XenotypeDef differs from the unique canonical instance");
        return context;
    }

    private ResolvedSqueakContext WarnAndFallback(string defName, string reason)
    {
        SqueakLog.TargetRejected(defName, reason);
        return globalContext;
    }

    public SqueakSoundChoice ChooseProductionSound(ResolvedSqueakContext context, SqueakAction action, Pawn pawn) => Choose(context, action, pawn, pawn.MapHeld, new TargetInfo(pawn), true);
    public SqueakSoundChoice ChooseProductionSoundByKey(ResolvedSqueakContext context, string actionKey, Pawn pawn) => ChooseByKey(context, actionKey, pawn, pawn.MapHeld, new TargetInfo(pawn), true);
    private SqueakSoundChoice Choose(ResolvedSqueakContext context, SqueakAction action, Pawn? pawn, Map? map, TargetInfo? target, bool production)
        => ChooseByKey(context, ActionKey.For(action) ?? "", pawn, map, target, production);

    private SqueakSoundChoice ChooseByKey(ResolvedSqueakContext context, string actionKey, Pawn? pawn, Map? map, TargetInfo? target, bool production)
    {
        if (string.IsNullOrEmpty(actionKey)) return SqueakSoundChoice.None;
        // 中性路由：域身份 = pawn 真实 raceDefName。不可得时为空域，池空/无内置 profile = 无声，绝不跨种族。
        string raceDefName = pawn?.def?.defName ?? "";
        AudioDomain domain = context.Xenotype != null
            ? new AudioDomain(new RaceKey(raceDefName), new XenotypeKey(context.Xenotype.defName))
            : new AudioDomain(new RaceKey(raceDefName), null);
        SelectionContext ctx = new(domain, actionKey, SqueakLifeStageResolver.Resolve(pawn), production, AllowEggs);
        ChainResult result = Registry.Select(ctx, SqueakKernelAdapter.ToSelectionMode(VoicePackMode), SqueakKernelAdapter.GateFor(pawn, map, target), SqueakKernelAdapter.Rolls);
        return SqueakKernelAdapter.ToChoice(result);
    }
}

public enum SqueakSoundSource { None, XenotypePack, RacePack, Vanilla }
public readonly struct SqueakSoundChoice { public static readonly SqueakSoundChoice None = default; public readonly SoundDef? Sound; public readonly SqueakSoundSource Source; public readonly string? PoolStableKey; public readonly bool IsEgg; public bool IsNone => Sound == null || Source == SqueakSoundSource.None; internal SqueakSoundChoice(SoundDef? sound, SqueakSoundSource source, string? packKey, bool isEgg = false) { Sound = sound; Source = source; PoolStableKey = packKey; IsEgg = isEgg; } }
public sealed class ResolvedSqueakContext { public static readonly ResolvedSqueakContext GlobalOnly = new(null, 1f, null, null); public readonly XenotypeDef? Xenotype; public readonly float OverallIntervalMultiplier; private readonly IReadOnlyDictionary<string, RuntimeActionDelta> actions; private readonly IReadOnlyDictionary<SqueakMood, RuntimeMoodDelta> moods; internal ResolvedSqueakContext(XenotypeDef? x, float interval, Dictionary<string, RuntimeActionDelta>? a, Dictionary<SqueakMood, RuntimeMoodDelta>? m) { Xenotype = x; OverallIntervalMultiplier = interval; actions = new ReadOnlyDictionary<string, RuntimeActionDelta>(a ?? new Dictionary<string, RuntimeActionDelta>()); moods = new ReadOnlyDictionary<SqueakMood, RuntimeMoodDelta>(m ?? new Dictionary<SqueakMood, RuntimeMoodDelta>()); } public RuntimeActionDelta GetActionByKey(string key) => actions.TryGetValue(key, out RuntimeActionDelta? v) ? v : RuntimeActionDelta.Default; public RuntimeActionDelta GetAction(SqueakAction a) => GetActionByKey(UniversalSqueaker.Kernel.ActionKey.For(a) ?? a.ToString()); public bool TryGetMood(SqueakMood m, out RuntimeMoodDelta d) => moods.TryGetValue(m, out d!); public RuntimeMoodDelta? GetMoodDelta(SqueakMood m) => moods.TryGetValue(m, out RuntimeMoodDelta? v) ? v : null; internal Dictionary<string, RuntimeActionDelta> ActionSnapshot() => new(actions); internal Dictionary<SqueakMood, RuntimeMoodDelta> MoodSnapshot() => new(moods); }
public sealed class RuntimeActionDelta { public static readonly RuntimeActionDelta Default = new(); public SqueakActionScope Scope { get; } public bool Enabled => Scope != SqueakActionScope.Disabled; public float IntervalMultiplier { get; } public float ProbabilityMultiplier { get; } internal RuntimeActionDelta(SqueakActionScope scope = SqueakActionScope.AnyOccurrence, float intervalMultiplier = 1f, float probabilityMultiplier = 1f) { Scope = scope; IntervalMultiplier = intervalMultiplier; ProbabilityMultiplier = probabilityMultiplier; } }
public sealed class RuntimeMoodDelta { public bool HasPitchFactor { get; } public float PitchFactor { get; } public bool HasVolumeFactor { get; } public float VolumeFactor { get; } public bool HasPitchJitter { get; } public FloatRange PitchJitter { get; } internal RuntimeMoodDelta(bool hp, float p, bool hv, float v, bool hj, FloatRange j) { HasPitchFactor = hp; PitchFactor = p; HasVolumeFactor = hv; VolumeFactor = v; HasPitchJitter = hj; PitchJitter = j; } }
