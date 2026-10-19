using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace UniversalSqueaker;

/// <summary>Last completed trigger attempt. This is runtime-only diagnostic state and is never Scribed.</summary>
public enum SqueakTriggerOutcome
{
    Disabled,
    ProbabilityRejected,
    ActionCooldown,
    GlobalCooldown,
    VocalOrgansSilent,
    TalkingRejected,
    NoSoundFallback,
    Dispatched,
    EligibilityRejected,
    PlaybackFailed,
    PeriodicStartupPending
}

/// <summary>Compact runtime-only trigger result retained only while diagnostics are enabled.</summary>
public readonly struct SqueakRecentOutcome
{
    public readonly SqueakTriggerOutcome Outcome;
    public readonly string Action;
    public readonly int Tick;
    public readonly float Realtime;
    public readonly bool CooldownConsumed;
    public readonly SoundDef? Sound;
    public readonly SqueakSoundSource SoundSource;
    public readonly string? PoolStableKey;
    /// <summary>Kernel chain tier that supplied this outcome's audio; null for pre-selection gates.</summary>
    public readonly UniversalSqueaker.Kernel.ChainTier? Tier;

    internal SqueakRecentOutcome(SqueakTriggerOutcome outcome, string action, int tick, float realtime,
        bool cooldownConsumed, SoundDef? sound, SqueakSoundSource soundSource, string? poolStableKey = null,
        UniversalSqueaker.Kernel.ChainTier? tier = null)
    {
        Outcome = outcome; Action = action; Tick = tick; Realtime = realtime;
        CooldownConsumed = cooldownConsumed; Sound = sound; SoundSource = soundSource;
        PoolStableKey = poolStableKey; Tier = tier;
    }
}

/// <summary>Read-only overlay input. Obtaining it never consumes random state or updates trigger state.</summary>
internal readonly struct SqueakDiagnosticSnapshot
{
    public readonly SqueakAction? CurrentTimingAction;
    public readonly bool CurrentActionEnabled;
    public readonly SqueakTriggerMode? CurrentTriggerMode;
    public readonly SqueakCooldownClock? CurrentCooldownClock;
    public readonly float CurrentActionIntervalMultiplier;
    public readonly SqueakTimingEvaluation Timing;
    public readonly SqueakTimingEvaluation BaseTiming;
    public readonly SqueakPeriodicPopulation.Snapshot Population;
    public readonly float MasterMultiplier;
    public readonly XenotypeDef? Xenotype;
    public readonly float XenotypeIntervalMultiplier;
    public readonly float TimeSpeedMultiplier;
    public readonly float EffectiveProbability;
    public readonly float BaseProbability;
    public readonly bool StartupPending;
    public readonly bool EffectiveTimingReady;
    public readonly SqueakVocalCapability VocalCapability;
    public readonly bool TalkingGateApplied;
    public readonly bool CurrentActionDeathExempt;
    public readonly SqueakRecentOutcome? LastEvaluation;
    public readonly SqueakRecentOutcome? LastSignificantOutcome;
    /// <summary>The last successful dispatch regardless of action or later failures - the panel's
    /// "last heard audio" outlet. Failures never overwrite it; ResetDiagnosticState clears it.</summary>
    public readonly SqueakRecentOutcome? LastDispatched;
    public readonly string KernelText;

    internal SqueakDiagnosticSnapshot(SqueakAction? currentTimingAction, bool currentActionEnabled,
        SqueakTriggerMode? currentTriggerMode, SqueakCooldownClock? currentCooldownClock,
        float currentActionIntervalMultiplier, SqueakTimingEvaluation timing, SqueakTimingEvaluation baseTiming, SqueakPeriodicPopulation.Snapshot population, float masterMultiplier,
        XenotypeDef? xenotype, float xenotypeIntervalMultiplier,
        float timeSpeedMultiplier, float effectiveProbability, float baseProbability, bool startupPending, bool effectiveTimingReady, SqueakVocalCapability vocalCapability,
        bool talkingGateApplied, bool currentActionDeathExempt,
        SqueakRecentOutcome? lastEvaluation, SqueakRecentOutcome? lastSignificantOutcome, SqueakRecentOutcome? lastDispatched = null,
        string kernelText = "")
    {
        CurrentTimingAction = currentTimingAction; CurrentActionEnabled = currentActionEnabled;
        CurrentTriggerMode = currentTriggerMode; CurrentCooldownClock = currentCooldownClock;
        CurrentActionIntervalMultiplier = currentActionIntervalMultiplier; Timing = timing; BaseTiming = baseTiming; Population = population;
        MasterMultiplier = masterMultiplier; Xenotype = xenotype; XenotypeIntervalMultiplier = xenotypeIntervalMultiplier;
        TimeSpeedMultiplier = timeSpeedMultiplier; EffectiveProbability = effectiveProbability; BaseProbability = baseProbability;
        StartupPending = startupPending; EffectiveTimingReady = effectiveTimingReady;
        VocalCapability = vocalCapability; TalkingGateApplied = talkingGateApplied;
        CurrentActionDeathExempt = currentActionDeathExempt;
        LastEvaluation = lastEvaluation; LastSignificantOutcome = lastSignificantOutcome; LastDispatched = lastDispatched;
        KernelText = kernelText;
    }
}

public enum SqueakPlaybackAttemptResult { NoEligibleSound, EligibilityRejected, Dispatched, Exception }

public readonly struct SqueakPlaybackAttempt
{
    public readonly SqueakPlaybackAttemptResult Result;
    public readonly SqueakSoundChoice Choice;
    internal SqueakPlaybackAttempt(SqueakPlaybackAttemptResult result, SqueakSoundChoice choice) { Result = result; Choice = choice; }
}

/// <summary>
/// 挂在带 Squeaker 组件的 pawn 上的自驱动发声组件。
/// 配置三层:CompProperties(XML默认) ← ModSettings(玩家override) ← 运行时。
/// 心情靠运行时 pitchFactor/volumeFactor 调制,每动作只需 1 个 SoundDef + 1 套中性音频。
/// </summary>
public partial class CompSqueaker : ThingComp
{
    public static bool ScaleCooldownWithTimeSpeed = true;
    public static bool ScaleFrequencyWithTalking = true;
    public static bool ScalePeriodicWithAudiblePopulation = true;
    public static float GlobalCooldownMultiplier = 1f;
    public static float GlobalVolumeFactor = 1f;
    public static int GlobalMinIntervalTicks = 216;
    // Eat occurrence granularity: cheap same-frame runtime statics published by the settings layer
    // (ApplyToRuntime / NotifyCheapRuntimeChanged). Defaults come from the pure rule, never a literal here.
    public static bool EatPrecisionEnabled = SqueakEatOccurrence.EatPrecisionDefault;
    public static bool EatPrecisionIncludeDrugs = SqueakEatOccurrence.EatPrecisionIncludeDrugsDefault;
    public static bool DiagnosticsEnabled;

    private static readonly Dictionary<string, SoundDef?> SoundCacheMixed = new(StringComparer.Ordinal);
    private static readonly HashSet<string> MissingSoundWarnings = new(StringComparer.Ordinal);
    private static bool soundCacheInitialized;
    private static FloatRange activeDistanceRange = new(15f, 50f);

    private readonly Dictionary<string, SqueakActionPlan> actionPlans = new(StringComparer.Ordinal);
    private SqueakRuntimeSnapshot? cachedRuntimeSnapshot;
    private XenotypeDef? cachedXenotype;
    private ResolvedSqueakContext cachedSqueakContext = ResolvedSqueakContext.GlobalOnly;
    private SqueakRecentOutcome? lastEvaluation;
    private SqueakRecentOutcome? lastSignificantOutcome;
    private SqueakRecentOutcome? lastDispatched;
    // Runtime-only, non-Scribe anchor. Every Periodic action phase remains rooted at this spawn tick.
    private Map? registeredMap;



    private Pawn Pawn => (Pawn)parent;
    internal Pawn RegisteredPawn => Pawn;
    private CompProperties_Squeaker Props => (CompProperties_Squeaker)props;

    private static string? ActionKeyOf(SqueakAction action) => UniversalSqueaker.Kernel.ActionKey.For(action);

    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            string? key = UniversalSqueaker.Kernel.ActionKey.For(action);
            if (key == null) continue;
            actionPlans[key] = SqueakActionPlanFactory.Unconfigured(action);
        }
        foreach (SqueakActionConfig cfg in Props.actions)
        {
            if (cfg == null || !SqueakActionDefinitions.IsKnown(cfg.action)) continue;
            string? key = UniversalSqueaker.Kernel.ActionKey.For(cfg.action);
            if (key != null) actionPlans[key] = SqueakActionPlanFactory.FromLegacy(cfg);
        }
    }

    private SqueakAction? CurrentAction => PeriodicStateBinding.Probe(Pawn);

    public void Notify_Wounded() => NotifyExternal(SqueakAction.Wounded, VerseEventBinding.OriginFor(SqueakAction.Wounded), SqueakInvocationSource.StateEvent);
    public void Notify_Select() => NotifyExternal(SqueakAction.Select, VerseEventBinding.OriginFor(SqueakAction.Select), SqueakInvocationSource.PlayerSelection);
    public void Notify_Death() => NotifyExternal(SqueakAction.Death, VerseEventBinding.OriginFor(SqueakAction.Death), SqueakInvocationSource.StateEvent);
    public void Notify_Draft(bool drafted) => NotifyExternal(drafted ? SqueakAction.Draft : SqueakAction.Undraft, VerseEventBinding.OriginFor(drafted ? SqueakAction.Draft : SqueakAction.Undraft), SqueakInvocationSource.ActiveCommand);
    public void Notify_Attack() => NotifyExternal(SqueakAction.Attack, VerseEventBinding.OriginFor(SqueakAction.Attack), IsCurrentJobPlayerCommand() ? SqueakInvocationSource.ActiveCommand : SqueakInvocationSource.StateEvent);
    public void Notify_Equip()
    {
        // Equipment tracker notifications also cover AI, loading, and system equipment changes.
        // Equip is deliberately only the player's ordered Core Equip job, regardless of its scope.
        if (!IsCurrentEquipJobPlayerCommand()) return;
        NotifyExternal(SqueakAction.Equip, VerseEventBinding.OriginFor(SqueakAction.Equip), SqueakInvocationSource.ActiveCommand);
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        // Avoid resolver or population rebuild work here: batch spawning remains O(N).
        registeredMap = Pawn.Spawned ? Pawn.MapHeld : null;
        SqueakPeriodicPopulation.Register(this, registeredMap);
    }

    /// <summary>
    /// S4 diagnostics: the head mark is drawn from the IMGUI pass by
    /// <see cref="Patches.Patch_MapInterface_DiagnosticsMarks"/>, never from the thing render pass.
    /// <c>ThingComp.PostDraw</c> is reached through <c>Pawn.Draw &#8594; Comps_PostDraw()</c>
    /// (RimWorld <c>Verse/Pawn.cs:2754</c>) during thing rendering, and <c>GenMapUI.DrawText</c> goes
    /// through the game's IMGUI text path: the first real run logged "You can only call GUI functions
    /// from inside OnGUI" 8558 times there and no mark ever appeared.
    ///
    /// Returns true when the mark was drawn. This method performs no error handling of its own - the
    /// caller owns the phase it runs in and the once-per-failure report - so the draw stays a plain draw
    /// and a failure cannot be swallowed silently.
    /// </summary>
    internal bool TryDrawDiagnosticsMark()
    {
        if (!SqueakDiagnosticsOverlay.IsSessionActive || !Pawn.Spawned || Pawn.Destroyed || Pawn.MapHeld == null)
        {
            return false;
        }

        if (!SqueakDiagnosticsOverlay.TryGetMark(Pawn, out string mark, out Color color))
        {
            return false;
        }

        Vector2 position = new(Pawn.DrawPos.x, Pawn.DrawPos.z + 1.15f);
        // GenMapUI.DrawText is locked to Tiny font, so visibility comes from a 4-direction
        // black outline (same pattern as the overlay's DrawMark).
        const float edge = 0.05f;
        GenMapUI.DrawText(position + new Vector2(-edge, 0f), mark, Color.black);
        GenMapUI.DrawText(position + new Vector2(edge, 0f), mark, Color.black);
        GenMapUI.DrawText(position + new Vector2(0f, -edge), mark, Color.black);
        GenMapUI.DrawText(position + new Vector2(0f, edge), mark, Color.black);
        GenMapUI.DrawText(position, mark, color);
        return true;
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {

        UsOneShotRuntime.ReleasePawn(Pawn);
        SqueakPeriodicPopulation.Unregister(this, previousMap);
        registeredMap = null;
        base.PostDestroy(mode, previousMap);
    }

    internal void NotifyPeriodicDespawn(Map map)
    {
        UsOneShotRuntime.ReleasePawn(Pawn);
        SqueakPeriodicPopulation.Unregister(this, map);
        if (ReferenceEquals(registeredMap, map)) registeredMap = null;
    }
    public void Notify_MentalBreak() => NotifyExternal(SqueakAction.MentalBreak, VerseEventBinding.OriginFor(SqueakAction.MentalBreak), SqueakInvocationSource.StateEvent);

    /// <summary>0.3.1 波 3c：BabyFits 窄 hook 入口（Patch_MentalFit 已用 MentalFitDef 反向 map 验证状态）。</summary>
    public void Notify_MentalFit(SqueakAction action) => NotifyExternal(action,
        VerseEventBinding.OriginFor(action),
        SqueakInvocationSource.StateEvent);

    private void NotifyExternal(SqueakAction action, SqueakTriggerOrigin origin, SqueakInvocationSource source)
        => NotifyExternalByKey(UniversalSqueaker.Kernel.ActionKey.For(action) ?? action.ToString(), origin, source);

    /// <summary>外部动作（string 键）的触发入口：与内置动作走同一 NotifyExternal 通路。</summary>
/// <summary>0.3.2 身份门控（Verse 采样，harness out-of-scope）：玩家发起来源（点选/主动指令）要求
    /// 玩家可控 pawn（IsPlayerControlled 已含 Spawned、玩家派系、无精神崩溃、可控机甲/亚人语义）；
    /// 点选另要求可响应 = !Downed && Awake()。任何不满足都静默丢弃，不进入 TryTrigger。</summary>
    private bool IdentityGateAllows(SqueakTriggerInvocation invocation)
    {
        if (invocation.RequiresResponsivePawn && (Pawn.Downed || Pawn.health == null || !Pawn.Awake())) return false;
        if (invocation.IsPlayerInitiated && !Pawn.IsPlayerControlled) return false;
        return true;
    }

    private SqueakTriggerInvocation PeriodicInvocationFor(SqueakAction action)
    {
        // Work is sampled during CompTick like the other periodic actions, but its source is the
        // player's active forced work order when applicable. This lets ActiveCommand narrow only
        // that source while AnyOccurrence retains autonomous work occurrences.
        SqueakInvocationSource source = action == SqueakAction.Work && IsCurrentJobPlayerCommand()
            ? SqueakInvocationSource.ActiveCommand
            : SqueakInvocationSource.Periodic;
        return new SqueakTriggerInvocation(SqueakTriggerOrigin.Periodic, source);
    }

    private void SynchronizePeriodicMembership()
    {
        Map? map = Pawn.Spawned ? Pawn.MapHeld : null;
        if (ReferenceEquals(map, registeredMap)) return;
        SqueakPeriodicPopulation.Unregister(this, registeredMap);
        registeredMap = map;
        SqueakPeriodicPopulation.Register(this, registeredMap);
    }

    private bool IsActionAllowed(SqueakAction action) => IsActionAllowedByKey(UniversalSqueaker.Kernel.ActionKey.For(action));

    private bool IsActionAllowedByKey(string? actionKey)
    {
        if (string.IsNullOrEmpty(actionKey)) return false;
        if (UniversalSqueaker.Kernel.ActionKey.TryParseBuiltIn(actionKey!, out SqueakAction action))
            return SqueakActionDefinitions.IsEligible(action, UniversalSqueakerMod.Settings?.BabyActionsEnabled == true);
        return UniversalSqueakerMod.Settings?.allowExternalActions == true;
    }

    /// <summary>动作 plan 解析：先查静态 actionPlans（内置 17 键）；未命中且为非内置键时按需合成外部
    /// 默认 plan（动作门由 IsActionAllowedByKey 另行校验）。YAGNI：不保留 ActionEntry/TriggerBinding 半成品。</summary>
    private bool TryGetPlan(string actionKey, out SqueakActionPlan plan)
    {
        if (actionPlans.TryGetValue(actionKey, out plan)) return true;
        if (!UniversalSqueaker.Kernel.BuiltInActionKeys.Contains(actionKey))
        {
            plan = SqueakActionPlanFactory.External(actionKey);
            return true;
        }
        plan = default;
        return false;
    }

    private ResolvedSqueakContext GetRuntimeContext(out SqueakRuntimeSnapshot snapshot)
    {
        snapshot = SqueakRuntimeResolver.Current;
        XenotypeDef? xenotype = null;
        if (ModsConfig.BiotechActive)
        {
            xenotype = Pawn.genes?.Xenotype;
        }

        if (!ReferenceEquals(snapshot, cachedRuntimeSnapshot) || !ReferenceEquals(xenotype, cachedXenotype))
        {
            cachedRuntimeSnapshot = snapshot;
            cachedXenotype = xenotype;
            cachedSqueakContext = snapshot.ResolveContext(Pawn);
        }

        return cachedSqueakContext;
    }

    private void RecordOutcome(SqueakTriggerOutcome outcome, string actionKey, bool external, bool cooldownConsumed,
        SoundDef? sound, SqueakSoundSource source, float nowRealtime, string? poolStableKey = null,
        UniversalSqueaker.Kernel.ChainTier? tier = null)
    {
        if (DiagnosticsEnabled)
        {
            SqueakRecentOutcome evaluation = new(outcome, actionKey, Find.TickManager.TicksGame,
                nowRealtime, cooldownConsumed, sound, source, poolStableKey, tier);
            lastEvaluation = evaluation;
            if (outcome == SqueakTriggerOutcome.Dispatched)
            {
                // Round-9 contract: the "last heard audio" slot only ever accepts a dispatch;
                // later failures and other actions never clear or overwrite it.
                lastDispatched = evaluation;
            }
            if (external || IsSignificantOutcome(outcome))
            {
                lastSignificantOutcome = evaluation;
            }
        }
    }

    /// <summary>Clears runtime-only diagnostic history when a diagnostics manager changes sessions.</summary>
    public void ResetDiagnosticState()
    {
        lastEvaluation = null;
        lastSignificantOutcome = null;
        lastDispatched = null;
    }

    private static bool IsSignificantOutcome(SqueakTriggerOutcome outcome) => outcome != SqueakTriggerOutcome.ProbabilityRejected
        && outcome != SqueakTriggerOutcome.ActionCooldown && outcome != SqueakTriggerOutcome.GlobalCooldown
        && outcome != SqueakTriggerOutcome.PeriodicStartupPending;

    /// <summary>Samples diagnostic state without selecting audio, consuming Rand, changing timestamps, or touching the production context cache.</summary>
    internal SqueakDiagnosticSnapshot GetDiagnosticSnapshot()
    {
        return new SqueakDiagnosticSnapshot(null, false, null, null, 1f, default, default,
            SqueakPeriodicPopulation.GetSnapshot(), 1f, null, 1f, 1f, 0f, 0f, false, false,
            SampleVocalCapability(), ScaleFrequencyWithTalking, false,
            lastEvaluation, lastSignificantOutcome, lastDispatched, UsOneShotRuntime.DiagnosticText(Pawn));
    }

    internal void RecordKernelOutcome(OneShot.DecisionOutcome outcome, SoundDef? sound)
    {
        var mapped = outcome.Receipt?.Result == OneShot.PlaybackResult.Dispatched
            ? SqueakTriggerOutcome.Dispatched : SqueakTriggerOutcome.EligibilityRejected;
        RecordOutcome(mapped, outcome.Action, false,
            outcome.Receipt?.Result == OneShot.PlaybackResult.Dispatched || outcome.Receipt?.Result == OneShot.PlaybackResult.Indeterminate,
            sound, SqueakSoundSource.None, Time.realtimeSinceStartup);
    }
    /// <summary>Mirrors production's global and runtime scope gates without resolving audio or mutating trigger state.</summary>
    private static bool IsScopeEligible(SqueakAction action, RuntimeActionDelta delta, SqueakTriggerInvocation invocation)
    {
        // H1 fix: scope comes only from the layered context delta, not a separate global policy early-return.
        return delta.Enabled && (delta.Scope != SqueakActionScope.ActiveCommand || invocation.IsActiveCommand);
    }

    internal bool ControlledOneShotAllowed(string key, bool activeCommand = false, BehaviorValues? behavior = null, bool authorAction = false)
    {
        if (!TryGetPlan(key, out var plan)) return false;
        var context = GetRuntimeContext(out _);
        var delta = context.GetActionByKey(key);
        var scope = behavior?.Scope ?? delta.Scope;
        bool command = ((key == "Work" || key == "Move") && IsCurrentJobPlayerCommand()) || activeCommand;
        return UsControlledActionEligibility.Allows(plan.Configured, authorAction, IsActionAllowedByKey(key),
            behavior?.Enabled ?? delta.Enabled, scope, command, Pawn.IsPlayerControlled);
    }
    internal bool ControlledPlaybackSustained(string key)
    {
        return TryGetPlan(key, out var plan) && plan.Mode == SqueakTriggerMode.Sustained;
    }
    internal bool ControlledInvocationAllowed(SqueakTriggerInvocation invocation) => IdentityGateAllows(invocation);
    internal bool ControlledWorkActive() => CurrentAction == SqueakAction.Work;
    internal SqueakVocalCapability ControlledOneShotVocal() => SampleVocalCapability();
    private SqueakVocalCapability SampleVocalCapability() => new(GetVocalOrganEfficiency(),
        Pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Talking) ?? 1f);

    private float GetVocalOrganEfficiency()
    {
        if (Pawn.health?.hediffSet == null)
        {
            return 1f;
        }

        float source = PawnCapacityUtility.CalculateTagEfficiency(Pawn.health.hediffSet, BodyPartTagDefOf.TalkingSource);
        float pathway = PawnCapacityUtility.CalculateTagEfficiency(Pawn.health.hediffSet, BodyPartTagDefOf.TalkingPathway, 1f);
        float tongue = GetTagEfficiencyOrOneIfMissing(BodyPartTagDefOf.Tongue, 1f);
        return source * pathway * tongue;
    }

    private float GetTagEfficiencyOrOneIfMissing(BodyPartTagDef tag, float maxEfficiency)
    {
        return BodyHasPartTag(tag)
            ? PawnCapacityUtility.CalculateTagEfficiency(Pawn.health!.hediffSet, tag, maxEfficiency)
            : 1f;
    }

    private bool BodyHasPartTag(BodyPartTagDef tag)
    {
        List<BodyPartRecord>? parts = Pawn.RaceProps.body?.AllParts;
        if (parts == null)
        {
            return false;
        }

        foreach (BodyPartRecord part in parts)
        {
            if (part.def.tags != null && part.def.tags.Contains(tag))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureSoundCache()
    {
        if (soundCacheInitialized)
        {
            return;
        }

        foreach (SqueakAction a in Enum.GetValues(typeof(SqueakAction)))
        {
            string key = UniversalSqueaker.Kernel.ActionKey.For(a) ?? a.ToString();
            SoundCacheMixed[key] = DefDatabase<SoundDef>.GetNamedSilentFail(SqueakActionDefinitions.Get(a).AudioKey);
        }

        soundCacheInitialized = true;
    }

    public static void ApplyDistanceRange(FloatRange range)
    {
        activeDistanceRange = range;
        SqueakPeriodicPopulation.NotifyDistanceChanged();
        UsInstanceOutput.Distance = range;
    }

    /// <summary>Explicit overlay/diagnostic maintenance; snapshot readers remain side-effect free.</summary>
    internal static void MaintainPeriodicPopulationDiagnostics() => SqueakPeriodicPopulation.Maintain(activeDistanceRange);

    private bool IsCurrentJobPlayerCommand() => Pawn.CurJob?.playerForced == true;
    private bool IsCurrentEquipJobPlayerCommand() => Pawn.CurJob?.playerForced == true && Pawn.CurJob.def == JobDefOf.Equip;

}



