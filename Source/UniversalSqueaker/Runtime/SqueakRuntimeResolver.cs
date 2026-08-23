using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;

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
        Dictionary<SqueakAction, RuntimeActionDelta> globalActions = BuildGlobalActions(settings);
        try { published = BuildSnapshot(settings, catalog, globalActions); }
        catch (Exception ex) { SqueakLog.ResolverRebuildFailed(ex); published = BuildFallback(globalActions, settings); }
        Volatile.Write(ref current, published);
        ResolverRebuildCount++;
        RuntimeFlushCount++;
        return true;
    }

    private static Dictionary<SqueakAction, RuntimeActionDelta> BuildGlobalActions(UniversalSqueakerSettings settings)
    {
        Dictionary<SqueakAction, RuntimeActionDelta> result = new();
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction))) result[action] = new RuntimeActionDelta(settings.GetActionGlobalScope(action), 1f, 1f);
        return result;
    }

    private static SqueakRuntimeSnapshot BuildSnapshot(UniversalSqueakerSettings settings, SqueakXenotypeCatalogSnapshot catalog, Dictionary<SqueakAction, RuntimeActionDelta> globalActions)
    {
        Dictionary<string, RuntimeBuilder> behavior = BuildBehavior(settings);
        Dictionary<UniversalSqueaker.Kernel.AudioDomain, HashSet<string>> selection = BuildSelections(settings.voicePackSelections);
        HashSet<SoundDef> known = SqueakKernelAdapter.CollectKnownSounds(catalog);
        List<UniversalSqueaker.Kernel.VoicePackEntry> entries = SqueakKernelAdapter.BuildEntries(catalog, selection);
        UniversalSqueaker.Kernel.SqueakPoolRegistry registry = new(entries, SqueakKernelAdapter.BuildBuiltIn());
        Dictionary<string, ResolvedSqueakContext> contexts = new(StringComparer.Ordinal);
        if (ModsConfig.BiotechActive)
        {
            HashSet<string> targets = new(catalog.XenotypePacksByDefName.Keys, StringComparer.Ordinal);
            foreach (VoicePackSelectionRecord record in settings.voicePackSelections ?? new List<VoicePackSelectionRecord>())
                if (record != null && record.scope == SqueakVoicePackScope.Xenotype && !string.IsNullOrEmpty(record.xenotypeDefName)) targets.Add(record.xenotypeDefName);
            foreach (string target in behavior.Keys) targets.Add(target);
            foreach (string target in targets)
            {
                catalog.XenotypeByDefName.TryGetValue(target, out XenotypeDef? xenotype);
                if (catalog.AmbiguousCanonicalDefNames.Contains(target)) xenotype = null;
                contexts.Add(target, BuildContext(xenotype, behavior.TryGetValue(target, out RuntimeBuilder? builder) ? builder : null, globalActions));
            }
        }
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            SoundDef? sound = DefDatabase<SoundDef>.GetNamedSilentFail(SqueakActionDefinitions.Get(action).AudioKey);
            if (sound != null) known.Add(sound);
        }
        return new SqueakRuntimeSnapshot(contexts, registry, known, NormalizeMode(settings.voicePackMode), globalActions, catalog.AmbiguousCanonicalDefNames, settings.AllowEasterEggSounds);
    }

    private static Dictionary<string, RuntimeBuilder> BuildBehavior(UniversalSqueakerSettings settings)
    {
        Dictionary<string, RuntimeBuilder> builders = new(StringComparer.Ordinal);
        foreach (XenotypePresetRecord record in settings.xenotypePresets ?? new List<XenotypePresetRecord>())
        {
            if (record == null || string.IsNullOrEmpty(record.xenotypeDefName)) continue;
            if (!builders.TryGetValue(record.xenotypeDefName, out RuntimeBuilder? builder)) { builder = new RuntimeBuilder(); builders.Add(record.xenotypeDefName, builder); }
            if (record.hasOverallIntervalMultiplier) builder.overallIntervalMultiplier = Sanitize(record.overallIntervalMultiplier);
            foreach (XenotypeMoodOverride mood in record.moodOverrides ?? new List<XenotypeMoodOverride>()) { if (mood == null) continue; RuntimeMoodBuilder b = builder.GetMood(mood.mood); if (mood.hasPitchFactor) b.SetPitch(mood.pitchFactor); if (mood.hasVolumeFactor) b.SetVolume(mood.volumeFactor); if (mood.hasPitchJitter) b.SetJitter(mood.pitchJitter); }
            foreach (XenotypeActionBehaviorOverride action in record.actionOverrides ?? new List<XenotypeActionBehaviorOverride>()) { if (action == null) continue; RuntimeActionBuilder b = builder.GetAction(action.action); if (action.hasEnabled) b.Enabled = action.enabled; if (action.hasIntervalMultiplier) b.IntervalMultiplier = Sanitize(action.intervalMultiplier); if (action.hasProbabilityMultiplier) b.ProbabilityMultiplier = Sanitize(action.probabilityMultiplier); }
        }
        return builders;
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

    private static ResolvedSqueakContext BuildContext(XenotypeDef? xenotype, RuntimeBuilder? builder, Dictionary<SqueakAction, RuntimeActionDelta> globals)
    {
        Dictionary<SqueakAction, RuntimeActionDelta> actions = new(globals);
        foreach (KeyValuePair<SqueakAction, RuntimeActionDelta> item in builder?.BuildActions() ?? new Dictionary<SqueakAction, RuntimeActionDelta>())
        {
            RuntimeActionDelta global = globals[item.Key]; actions[item.Key] = new RuntimeActionDelta(global.Enabled && item.Value.Enabled ? global.Scope : SqueakActionScope.Disabled, item.Value.IntervalMultiplier, item.Value.ProbabilityMultiplier);
        }
        return new ResolvedSqueakContext(xenotype, builder?.overallIntervalMultiplier ?? 1f, actions, builder?.BuildMoods());
    }

    private static SqueakRuntimeSnapshot BuildFallback(Dictionary<SqueakAction, RuntimeActionDelta> actions, UniversalSqueakerSettings settings)
    {
        try
        {
            HashSet<SoundDef> known = new();
            foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
            {
                SoundDef? sound = DefDatabase<SoundDef>.GetNamedSilentFail(SqueakActionDefinitions.Get(action).AudioKey);
                if (sound != null) known.Add(sound);
            }
            UniversalSqueaker.Kernel.SqueakPoolRegistry registry = new(
                Array.Empty<UniversalSqueaker.Kernel.VoicePackEntry>(),
                SqueakKernelAdapter.BuildBuiltIn());
            return new SqueakRuntimeSnapshot(new Dictionary<string, ResolvedSqueakContext>(), registry, known, SqueakVoicePackMode.Off, actions, null, settings.AllowEasterEggSounds);
        }
        catch { return SqueakRuntimeSnapshot.GlobalOnly; }
    }

    private static SqueakVoicePackMode NormalizeMode(SqueakVoicePackMode mode) => mode == SqueakVoicePackMode.Fallback || mode == SqueakVoicePackMode.Remix ? mode : SqueakVoicePackMode.Off;
    private static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, value);

    private sealed class RuntimeBuilder { public float overallIntervalMultiplier = 1f; private readonly Dictionary<SqueakAction, RuntimeActionBuilder> actions = new(); private readonly Dictionary<SqueakMood, RuntimeMoodBuilder> moods = new(); public RuntimeActionBuilder GetAction(SqueakAction a) { if (!actions.TryGetValue(a, out RuntimeActionBuilder? v)) { v = new RuntimeActionBuilder(); actions.Add(a, v); } return v; } public RuntimeMoodBuilder GetMood(SqueakMood m) { if (!moods.TryGetValue(m, out RuntimeMoodBuilder? v)) { v = new RuntimeMoodBuilder(); moods.Add(m, v); } return v; } public Dictionary<SqueakAction, RuntimeActionDelta> BuildActions() => actions.ToDictionary(x => x.Key, x => x.Value.Build()); public Dictionary<SqueakMood, RuntimeMoodDelta> BuildMoods() => moods.ToDictionary(x => x.Key, x => x.Value.Build()); }
    private sealed class RuntimeActionBuilder { public bool Enabled = true; public float IntervalMultiplier = 1f; public float ProbabilityMultiplier = 1f; public RuntimeActionDelta Build() => new(Enabled ? SqueakActionScope.AnyOccurrence : SqueakActionScope.Disabled, IntervalMultiplier, ProbabilityMultiplier); }
    private sealed class RuntimeMoodBuilder { private bool hp, hv, hj; private float p = 1f, v = 1f; private FloatRange j = FloatRange.One; public void SetPitch(float x) { hp = true; p = x; } public void SetVolume(float x) { hv = true; v = x; } public void SetJitter(FloatRange x) { hj = true; j = x; } public RuntimeMoodDelta Build() => new(hp, p, hv, v, hj, j); }
}

public sealed class SqueakRuntimeSnapshot
{
    public static readonly SqueakRuntimeSnapshot GlobalOnly = new(new Dictionary<string, ResolvedSqueakContext>(), UniversalSqueaker.Kernel.SqueakPoolRegistry.Empty, new HashSet<SoundDef>(), SqueakVoicePackMode.Off, null, null, false);
    private readonly IReadOnlyDictionary<string, ResolvedSqueakContext> contexts; private readonly IReadOnlyDictionary<SqueakAction, RuntimeActionDelta> globalActions; private readonly ResolvedSqueakContext globalContext;
    public readonly SqueakVoicePackMode VoicePackMode; public readonly IReadOnlyCollection<SoundDef> KnownMapSoundDefs;
    public readonly UniversalSqueaker.Kernel.SqueakPoolRegistry Registry;
    public readonly bool AllowEggs;
    private readonly IReadOnlyCollection<string> ambiguousCanonicalNames;
    internal SqueakRuntimeSnapshot(Dictionary<string, ResolvedSqueakContext> contexts, UniversalSqueaker.Kernel.SqueakPoolRegistry registry, HashSet<SoundDef> known, SqueakVoicePackMode mode, Dictionary<SqueakAction, RuntimeActionDelta>? globals, IEnumerable<string>? ambiguousNames, bool allowEggs) { this.contexts = new ReadOnlyDictionary<string, ResolvedSqueakContext>(contexts); this.Registry = registry; globalActions = new ReadOnlyDictionary<SqueakAction, RuntimeActionDelta>(globals ?? new Dictionary<SqueakAction, RuntimeActionDelta>()); globalContext = new ResolvedSqueakContext(null, 1f, globals, null); VoicePackMode = mode; AllowEggs = allowEggs; KnownMapSoundDefs = new ReadOnlyCollection<SoundDef>(known.ToList()); ambiguousCanonicalNames = new ReadOnlyCollection<string>((ambiguousNames ?? Array.Empty<string>()).ToList()); }
    public ResolvedSqueakContext ResolveContext(Pawn pawn)
    {
        if (!ModsConfig.BiotechActive) return globalContext;
        XenotypeDef? xenotype = pawn?.genes?.Xenotype;
        string? defName = xenotype?.defName;
        if (string.IsNullOrEmpty(defName)) return globalContext;
        string exactDefName = defName!;
        if (!contexts.TryGetValue(exactDefName, out ResolvedSqueakContext? context)) return globalContext;
        if (ambiguousCanonicalNames.Contains(exactDefName)) return WarnAndFallback(exactDefName, "multiple loaded XenotypeDef instances");
        if (context.Xenotype != null && !ReferenceEquals(context.Xenotype, xenotype)) return WarnAndFallback(exactDefName, "runtime XenotypeDef differs from the unique canonical instance");
        return context;
    }
    private ResolvedSqueakContext WarnAndFallback(string defName, string reason)
    {
        SqueakLog.TargetRejected(defName, reason);
        return globalContext;
    }
    public SqueakActionScope GetGlobalScope(SqueakAction action) => globalActions.TryGetValue(action, out RuntimeActionDelta? value) ? value.Scope : SqueakActionDefinitions.Get(action).DefaultScope;
    public SqueakSoundChoice ChooseProductionSound(ResolvedSqueakContext context, SqueakAction action, Pawn pawn) => Choose(context, action, pawn, pawn.MapHeld, new TargetInfo(pawn), true);
    private SqueakSoundChoice Choose(ResolvedSqueakContext context, SqueakAction action, Pawn? pawn, Map? map, TargetInfo? target, bool production)
    {
        string? actionKey = UniversalSqueaker.Kernel.ActionKey.For(action);
        if (actionKey == null) return SqueakSoundChoice.None;
        // 中性路由：域身份 = pawn 真实 raceDefName。不可得时为空域，池空/无内置 profile = 无声，绝不跨种族。
        string raceDefName = pawn?.def?.defName ?? "";
        UniversalSqueaker.Kernel.AudioDomain domain = context.Xenotype != null
            ? new UniversalSqueaker.Kernel.AudioDomain(new UniversalSqueaker.Kernel.RaceKey(raceDefName), new UniversalSqueaker.Kernel.XenotypeKey(context.Xenotype.defName))
            : new UniversalSqueaker.Kernel.AudioDomain(new UniversalSqueaker.Kernel.RaceKey(raceDefName), null);
        UniversalSqueaker.Kernel.SelectionContext ctx = new(domain, actionKey, SqueakLifeStageResolver.Resolve(pawn), production, AllowEggs);
        UniversalSqueaker.Kernel.ChainResult result = Registry.Select(ctx, SqueakKernelAdapter.ToSelectionMode(VoicePackMode), SqueakKernelAdapter.GateFor(pawn, map, target), SqueakKernelAdapter.Rolls);
        return SqueakKernelAdapter.ToChoice(result);
    }
}

public enum SqueakSoundSource { None, XenotypePack, RacePack, Vanilla }
public readonly struct SqueakSoundChoice { public static readonly SqueakSoundChoice None = default; public readonly SoundDef? Sound; public readonly SqueakSoundSource Source; public readonly string? PoolStableKey; public readonly bool IsEgg; public bool IsNone => Sound == null || Source == SqueakSoundSource.None; internal SqueakSoundChoice(SoundDef? sound, SqueakSoundSource source, string? packKey, bool isEgg = false) { Sound = sound; Source = source; PoolStableKey = packKey; IsEgg = isEgg; } }
public sealed class ResolvedSqueakContext { public static readonly ResolvedSqueakContext GlobalOnly = new(null, 1f, null, null); public readonly XenotypeDef? Xenotype; public readonly float OverallIntervalMultiplier; private readonly IReadOnlyDictionary<SqueakAction, RuntimeActionDelta> actions; private readonly IReadOnlyDictionary<SqueakMood, RuntimeMoodDelta> moods; internal ResolvedSqueakContext(XenotypeDef? x, float interval, Dictionary<SqueakAction, RuntimeActionDelta>? a, Dictionary<SqueakMood, RuntimeMoodDelta>? m) { Xenotype = x; OverallIntervalMultiplier = interval; actions = new ReadOnlyDictionary<SqueakAction, RuntimeActionDelta>(a ?? new Dictionary<SqueakAction, RuntimeActionDelta>()); moods = new ReadOnlyDictionary<SqueakMood, RuntimeMoodDelta>(m ?? new Dictionary<SqueakMood, RuntimeMoodDelta>()); } public RuntimeActionDelta GetAction(SqueakAction a) => actions.TryGetValue(a, out RuntimeActionDelta? v) ? v : RuntimeActionDelta.Default; public bool TryGetMood(SqueakMood m, out RuntimeMoodDelta d) => moods.TryGetValue(m, out d!); public RuntimeMoodDelta? GetMoodDelta(SqueakMood m) => moods.TryGetValue(m, out RuntimeMoodDelta? v) ? v : null; }
public sealed class RuntimeActionDelta { public static readonly RuntimeActionDelta Default = new(); public SqueakActionScope Scope { get; } public bool Enabled => Scope != SqueakActionScope.Disabled; public float IntervalMultiplier { get; } public float ProbabilityMultiplier { get; } internal RuntimeActionDelta(SqueakActionScope scope = SqueakActionScope.AnyOccurrence, float intervalMultiplier = 1f, float probabilityMultiplier = 1f) { Scope = scope; IntervalMultiplier = intervalMultiplier; ProbabilityMultiplier = probabilityMultiplier; } }
public sealed class RuntimeMoodDelta { public bool HasPitchFactor { get; } public float PitchFactor { get; } public bool HasVolumeFactor { get; } public float VolumeFactor { get; } public bool HasPitchJitter { get; } public FloatRange PitchJitter { get; } internal RuntimeMoodDelta(bool hp, float p, bool hv, float v, bool hj, FloatRange j) { HasPitchFactor = hp; PitchFactor = p; HasVolumeFactor = hv; VolumeFactor = v; HasPitchJitter = hj; PitchJitter = j; } }
