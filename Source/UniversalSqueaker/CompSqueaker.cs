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

    internal SqueakRecentOutcome(SqueakTriggerOutcome outcome, string action, int tick, float realtime,
        bool cooldownConsumed, SoundDef? sound, SqueakSoundSource soundSource, string? poolStableKey = null)
    {
        Outcome = outcome; Action = action; Tick = tick; Realtime = realtime;
        CooldownConsumed = cooldownConsumed; Sound = sound; SoundSource = soundSource;
        PoolStableKey = poolStableKey;
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

    internal SqueakDiagnosticSnapshot(SqueakAction? currentTimingAction, bool currentActionEnabled,
        SqueakTriggerMode? currentTriggerMode, SqueakCooldownClock? currentCooldownClock,
        float currentActionIntervalMultiplier, SqueakTimingEvaluation timing, SqueakTimingEvaluation baseTiming, SqueakPeriodicPopulation.Snapshot population, float masterMultiplier,
        XenotypeDef? xenotype, float xenotypeIntervalMultiplier,
        float timeSpeedMultiplier, float effectiveProbability, float baseProbability, bool startupPending, bool effectiveTimingReady, SqueakVocalCapability vocalCapability,
        bool talkingGateApplied, bool currentActionDeathExempt,
        SqueakRecentOutcome? lastEvaluation, SqueakRecentOutcome? lastSignificantOutcome)
    {
        CurrentTimingAction = currentTimingAction; CurrentActionEnabled = currentActionEnabled;
        CurrentTriggerMode = currentTriggerMode; CurrentCooldownClock = currentCooldownClock;
        CurrentActionIntervalMultiplier = currentActionIntervalMultiplier; Timing = timing; BaseTiming = baseTiming; Population = population;
        MasterMultiplier = masterMultiplier; Xenotype = xenotype; XenotypeIntervalMultiplier = xenotypeIntervalMultiplier;
        TimeSpeedMultiplier = timeSpeedMultiplier; EffectiveProbability = effectiveProbability; BaseProbability = baseProbability;
        StartupPending = startupPending; EffectiveTimingReady = effectiveTimingReady;
        VocalCapability = vocalCapability; TalkingGateApplied = talkingGateApplied;
        CurrentActionDeathExempt = currentActionDeathExempt;
        LastEvaluation = lastEvaluation; LastSignificantOutcome = lastSignificantOutcome;
    }
}

public enum SqueakFinalPreviewStatus { NoEligibleSound, PawnOrMapUnavailable, IneligibleSound, Dispatched, Exception }

public enum SqueakPlaybackAttemptResult { NoEligibleSound, EligibilityRejected, Dispatched, Exception }

public readonly struct SqueakPlaybackAttempt
{
    public readonly SqueakPlaybackAttemptResult Result;
    public readonly SqueakSoundChoice Choice;
    internal SqueakPlaybackAttempt(SqueakPlaybackAttemptResult result, SqueakSoundChoice choice) { Result = result; Choice = choice; }
}

/// <summary>Read-only final-preview plan/result for the Dev audio browser.</summary>
public readonly struct SqueakFinalPreviewResult
{
    public readonly Pawn? Pawn;
    public readonly XenotypeDef? Xenotype;
    public readonly SqueakMood Mood;
    public readonly SoundDef? Sound;
    public readonly SqueakSoundSource Source;
    public readonly string? PoolStableKey;
    public readonly SqueakFinalPreviewStatus Status;
    public readonly SqueakSoundPlayability Playability;
    public readonly string Reason;
    internal SqueakFinalPreviewResult(Pawn? pawn, XenotypeDef? xenotype, SqueakMood mood, SqueakSoundChoice choice,
        SqueakFinalPreviewStatus status, SqueakSoundPlayability playability, string reason = "")
    {
        Pawn = pawn; Xenotype = xenotype; Mood = mood; Sound = choice.Sound; Source = choice.Source;
        PoolStableKey = choice.PoolStableKey; Status = status; Playability = playability;
        Reason = reason;
    }
}

/// <summary>单个动作的触发配置。</summary>
public class SqueakActionConfig
{
    public SqueakAction action = SqueakAction.Call;
    public SqueakTriggerMode mode = SqueakTriggerMode.RandomOneShot;
    public int minIntervalTicks = 300;
    public float probabilityPerCheck = 0.02f;
    public bool ignoreGlobalCooldown = false;
    public SqueakCooldownClock cooldownClock = SqueakCooldownClock.GameTicks;
}

/// <summary>
/// 挂在带 Squeaker 组件的 pawn 上的自驱动发声组件。
/// 配置三层:CompProperties(XML默认) ← ModSettings(玩家override) ← 运行时。
/// 心情靠运行时 pitchFactor/volumeFactor 调制,每动作只需 1 个 SoundDef + 1 套中性音频。
/// </summary>
public class CompSqueaker : ThingComp
{
    public static bool ScaleCooldownWithTimeSpeed = true;
    public static bool ScaleFrequencyWithTalking = true;
    public static bool ScalePeriodicWithAudiblePopulation = true;
    public static float GlobalCooldownMultiplier = 1f;
    public static int GlobalMinIntervalTicks = 216;
    public static bool DiagnosticsEnabled;

    private static readonly Dictionary<string, SoundDef?> SoundCacheMixed = new(StringComparer.Ordinal);
    private static readonly HashSet<string> MissingSoundWarnings = new(StringComparer.Ordinal);
    private static bool soundCacheInitialized;
    private static FloatRange activeDistanceRange = new(15f, 50f);

    private readonly Dictionary<string, SqueakActionPlan> actionPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> lastTriggerTick = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> lastTriggerRealTime = new(StringComparer.Ordinal);
    private readonly Dictionary<SqueakMood, SqueakMoodMod> moodModMap = new();
    private int lastAnyTriggerTick = int.MinValue / 2;
    private SqueakRuntimeSnapshot? cachedRuntimeSnapshot;
    private XenotypeDef? cachedXenotype;
    private ResolvedSqueakContext cachedSqueakContext = ResolvedSqueakContext.GlobalOnly;
    private SqueakRecentOutcome? lastEvaluation;
    private SqueakRecentOutcome? lastSignificantOutcome;
    // Runtime-only, non-Scribe anchor. Every Periodic action phase remains rooted at this spawn tick.
    private int startupAnchorTick;
    private bool startupAnchorRecorded;
    private readonly Dictionary<string, bool> periodicStartupPhaseMaterialized = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> periodicStartupReadyTicks = new(StringComparer.Ordinal);
    private Map? registeredMap;
    // S3：当前激活的 Sustainer 及其动作键（运行期状态，永远不 Scribe）。
    private Sustainer? activeSustainer;
    private string activeSustainerKey = "";

    private Pawn Pawn => (Pawn)parent;
    internal Pawn RegisteredPawn => Pawn;
    private CompProperties_Squeaker Props => (CompProperties_Squeaker)props;

    private static string? ActionKeyOf(SqueakAction action) => UniversalSqueaker.Kernel.ActionKey.For(action);

    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        lastAnyTriggerTick = int.MinValue / 2;
        foreach (SqueakAction action in Enum.GetValues(typeof(SqueakAction)))
        {
            string? key = UniversalSqueaker.Kernel.ActionKey.For(action);
            if (key == null) continue;
            actionPlans[key] = SqueakActionPlanFactory.Unconfigured(action);
            lastTriggerTick[key] = int.MinValue / 2;
            lastTriggerRealTime[key] = -1_000_000f;
        }
        foreach (SqueakActionConfig cfg in Props.actions)
        {
            if (cfg == null || !SqueakActionDefinitions.IsKnown(cfg.action)) continue;
            string? key = UniversalSqueaker.Kernel.ActionKey.For(cfg.action);
            if (key != null) actionPlans[key] = SqueakActionPlanFactory.FromLegacy(cfg);
        }

        foreach (SqueakMoodMod mod in Props.moodMods)
        {
            moodModMap[mod.mood] = mod;
        }
    }

    private SqueakMood CurrentMood
    {
        get
        {
            if (Pawn.InMentalState)
            {
                return SqueakMood.Break;
            }

            Need_Mood? mood = Pawn.needs?.mood;
            if (mood == null)
            {
                return SqueakMood.Neutral;
            }

            float p = mood.CurLevelPercentage;
            if (p > 0.65f)
            {
                return SqueakMood.Good;
            }

            return p < 0.35f ? SqueakMood.Bad : SqueakMood.Neutral;
        }
    }

    private SqueakAction? CurrentAction => PeriodicStateBinding.Probe(Pawn);

    public override void CompTick()
    {
        SynchronizePeriodicMembership();
        SqueakAction? action = CurrentAction;
        // Disabled = 真旁路：gate 置于任何发声状态维护之前（与 NotifyExternalByKey 头部 gate 对仗，
        // 治本而非在分支补 End）。已激活的 Sustainer 不再被 Maintain，由原版未维护生命周期自动 End
        // （约 1–2 秒尾音）；activeSustainer 字段持有已结束引用，重新启用后首个 MaintainSustainer 自清。
        if (SqueakRuntimeResolver.Current.VoicePackMode == SqueakVoicePackMode.Disabled)
        {
            if (action != null)
            {
                SqueakLog.AudioDisabled(UniversalSqueaker.Kernel.ActionKey.For(action.Value) ?? action.Value.ToString());
            }
            return;
        }
        // S3 每帧维护：先于任何早退执行，确保 pawn 离开地图/离屏/持续状态消失时 sustainer 被 End。
        MaintainSustainer(action);

        if (!Pawn.Spawned || Pawn.MapHeld == null || Pawn.MapHeld != Find.CurrentMap)
        {
            return;
        }

        if (!Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(Pawn.Position))
        {
            return;
        }

        if (action == null) return;
        string? actionKey = ActionKeyOf(action.Value);
        if (actionKey == null || !TryGetPlan(actionKey, out SqueakActionPlan plan)) return;
        if (!plan.Configured) return;

        switch (plan.Mode)
        {
            case SqueakTriggerMode.EachTime:
                TryTrigger(plan, PeriodicInvocationFor(action.Value));
                break;
            case SqueakTriggerMode.RandomOneShot:
                TryTrigger(plan, PeriodicInvocationFor(action.Value));
                break;
            case SqueakTriggerMode.External:
                break;
            // S3：Sustained 模式在周期状态下持续发声；状态消失（Probe 不再返回该动作）→ End。
            // Blocker 修复：首次/缺失时走 TryTrigger（完整 gate/cooldown/outcome），已有 sustainer 时只由
            // MaintainSustainer 维持，不再每 tick 直接 TryPlaySustained 绕过触发门。
            case SqueakTriggerMode.Sustained:
                if (activeSustainer != null && !activeSustainer.Ended && activeSustainerKey == actionKey) break;
                TryTrigger(plan, PeriodicInvocationFor(action.Value));
                break;
        }
    }

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
        if (!startupAnchorRecorded)
        {
            startupAnchorTick = Find.TickManager.TicksGame;
            startupAnchorRecorded = true;
        }
    }

    /// <summary>
    /// S4 diagnostics: draws the head mark through the public ThingComp draw hook instead of a
    /// MapInterface reflection hook (US red line). Naturally follows the pawn; the actual text
    /// draw is a 4-direction black outline plus the main color above the pawn's slot.
    /// </summary>
    public override void PostDraw()
    {
        try
        {
            PostDrawCore();
        }
        catch (Exception ex)
        {
            // Diagnostics must fail closed: a modded pawn/draw exception never breaks the game frame.
            Log.Warning("[UniversalSqueaker] Diagnostics draw failed for " + Pawn.LabelShort + ": " + SqueakLogText.SanitizeExceptionMessage(ex.Message));
        }
    }

    private void PostDrawCore()
    {
        if (SqueakDiagnosticsOverlay.Mode == SqueakDiagnosticsMode.Off || !Pawn.Spawned || Pawn.Destroyed || Pawn.MapHeld == null)
        {
            return;
        }

        if (!SqueakDiagnosticsOverlay.TryGetMark(Pawn, out string mark, out Color color))
        {
            return;
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
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        EndActiveSustainer();
        SqueakPeriodicPopulation.Unregister(this, previousMap);
        registeredMap = null;
        base.PostDestroy(mode, previousMap);
    }

    private void EndActiveSustainer()
    {
        if (activeSustainer != null)
        {
            if (!activeSustainer.Ended) activeSustainer.End();
            activeSustainer = null;
            activeSustainerKey = "";
        }
    }
    internal void NotifyPeriodicDespawn(Map map)
    {
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
    public void NotifyExternalByKey(string actionKey, SqueakTriggerOrigin origin, SqueakInvocationSource source)
    {
        SynchronizePeriodicMembership();
        if (SqueakRuntimeResolver.Current.VoicePackMode == SqueakVoicePackMode.Disabled)
        {
            SqueakLog.AudioDisabled(actionKey);
            return;
        }
        if (!Pawn.Spawned || Pawn.MapHeld != Find.CurrentMap)
        {
            return;
        }

        if (!Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(Pawn.Position))
        {
            return;
        }

        if (!TryGetPlan(actionKey, out SqueakActionPlan plan)) return;
        if (!plan.Configured) return;

        SqueakTriggerInvocation invocation = new(origin, source);
        if (!IdentityGateAllows(invocation)) return; // fail-closed silent: no cooldowns, no outcome, no log.

        TryTrigger(plan, invocation);
    }

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
        bool isBuiltIn = UniversalSqueaker.Kernel.BuiltInActionKeys.Contains(actionKey);
        return isBuiltIn || UniversalSqueakerMod.Settings?.allowExternalActions == true;
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

    private void TryTrigger(SqueakActionPlan plan, SqueakTriggerInvocation invocation)
    {
        string actionKey = plan.ActionKey;
        // Action gate (Goal A): a non-built-in action requires allowExternalActions before it can fire.
        if (!IsActionAllowedByKey(actionKey)) return;
        // H1 fix: the global early-return is gone. Action scope now resolves from the layered context
        // (Xeno > Race > Global > Default), so a global disable no longer suppresses a xenotype enable.
        int now = 0;
        float nowRealtime = 0f;
        bool hasAttemptRealtime = false;
        bool hasAttemptTick = false;
        try
        {
            nowRealtime = Time.realtimeSinceStartup;
            hasAttemptRealtime = true;
            ResolvedSqueakContext context = GetRuntimeContext(out SqueakRuntimeSnapshot snapshot);
            RuntimeActionDelta actionDelta = context.GetActionByKey(actionKey);
            if (!actionDelta.Enabled)
            {
                RecordOutcome(SqueakTriggerOutcome.Disabled, actionKey, invocation.IsExternal, false, null, SqueakSoundSource.None, nowRealtime);
                return;
            }
            if (actionDelta.Scope == SqueakActionScope.ActiveCommand && !invocation.IsActiveCommand) return;

            now = Find.TickManager.TicksGame;
            hasAttemptTick = true;
            // The first Periodic production caller refreshes one shared snapshot for this tick,
            // including when player-facing scaling is off: startup materialization still uses the
            // same bounded shared maintenance path.
            SqueakPeriodicPopulation.Snapshot periodicPopulation = invocation.Origin == SqueakTriggerOrigin.Periodic
                ? SqueakPeriodicPopulation.Maintain(activeDistanceRange) : SqueakPeriodicPopulation.GetSnapshot();
            float periodicScale = invocation.Origin == SqueakTriggerOrigin.Periodic && ScalePeriodicWithAudiblePopulation
                ? SanitizePeriodicPopulationScale(periodicPopulation.Scale) : 1f;
            SqueakTimingEvaluation timing = EvaluateTiming(plan, context, actionDelta, now, nowRealtime,
                Find.TickManager.TickRateMultiplier, periodicScale);
            if (invocation.Origin == SqueakTriggerOrigin.Periodic && IsPeriodicStartupPending(actionKey, timing, now))
            {
                RecordOutcome(SqueakTriggerOutcome.PeriodicStartupPending, actionKey, false, false, null, SqueakSoundSource.None, nowRealtime);
                return;
            }

            if (!invocation.SkipsRandomOneShotProbability && plan.Mode == SqueakTriggerMode.RandomOneShot)
            {
                float probability = Mathf.Clamp01(plan.ProbabilityPerCheck * actionDelta.ProbabilityMultiplier) / periodicScale;
                bool passed = Rand.Value < probability;
                if (!passed)
                {
                    RecordOutcome(SqueakTriggerOutcome.ProbabilityRejected, actionKey, false, false, null, SqueakSoundSource.None, nowRealtime);
                    return;
                }
            }

            if (!timing.ActionReady)
            {
                RecordOutcome(SqueakTriggerOutcome.ActionCooldown, actionKey, invocation.IsExternal, false, null, SqueakSoundSource.None, nowRealtime);
                return;
            }

            if (timing.GlobalApplicable && !timing.GlobalReady)
            {
                RecordOutcome(SqueakTriggerOutcome.GlobalCooldown, actionKey, invocation.IsExternal, false, null, SqueakSoundSource.None, nowRealtime);
                return;
            }

            SqueakVocalCapability capability = SampleVocalCapability();
            bool applyTalkingGate = ScaleFrequencyWithTalking && plan.Definition.VocalGatePolicy == SqueakVocalGatePolicy.ApplyTalkingGate;
            float roll = capability.RequiresTalkingRoll(applyTalkingGate) ? Rand.Value : 0f;
            SqueakVocalGateDecision vocalDecision = capability.Decide(applyTalkingGate, roll);
            if (vocalDecision != SqueakVocalGateDecision.Allowed)
            {
                ConsumeAttemptCooldowns(actionKey, now, nowRealtime);
                RecordOutcome(vocalDecision == SqueakVocalGateDecision.VocalOrgansSilent
                    ? SqueakTriggerOutcome.VocalOrgansSilent : SqueakTriggerOutcome.TalkingRejected,
                    actionKey, invocation.IsExternal, true, null, SqueakSoundSource.None, nowRealtime);
                return;
            }

            SqueakPlaybackAttempt attempt = plan.Mode == SqueakTriggerMode.Sustained
                ? TryPlaySustained(actionKey, CurrentMood, context, snapshot)
                : PlayOneShot(actionKey, CurrentMood, context, snapshot);
            ConsumeAttemptCooldowns(actionKey, now, nowRealtime);
            SqueakTriggerOutcome outcome = attempt.Result switch
            {
                SqueakPlaybackAttemptResult.NoEligibleSound => SqueakTriggerOutcome.NoSoundFallback,
                SqueakPlaybackAttemptResult.EligibilityRejected => SqueakTriggerOutcome.EligibilityRejected,
                SqueakPlaybackAttemptResult.Dispatched => SqueakTriggerOutcome.Dispatched,
                _ => SqueakTriggerOutcome.PlaybackFailed,
            };
            RecordOutcome(outcome, actionKey, invocation.IsExternal, true, attempt.Choice.Sound, attempt.Choice.Source, nowRealtime, attempt.Choice.PoolStableKey);
        }
        catch (Exception ex)
        {
            SqueakLog.TriggerAttemptFailed(actionKey, ex);
            try
            {
                if (!hasAttemptRealtime)
                {
                    nowRealtime = Time.realtimeSinceStartup;
                }
                if (!hasAttemptTick) now = Find.TickManager.TicksGame;
                ConsumeAttemptCooldowns(actionKey, now, nowRealtime);
                RecordOutcome(SqueakTriggerOutcome.PlaybackFailed, actionKey, invocation.IsExternal, true, null, SqueakSoundSource.None, nowRealtime);
            }
            catch { }
        }
    }

    private SqueakTimingEvaluation EvaluateTiming(SqueakActionPlan plan, ResolvedSqueakContext context,
        RuntimeActionDelta actionDelta, int nowTick, float nowRealtime, float timeSpeedMultiplier, float periodicScale = 1f)
    {
        // 0.3.1 波 4a：SqueakTimingModel 已提取为纯逻辑（Kernel/SqueakTimingModel.cs）；
        // 适配层在此把 Context/ActionDelta 投影为模型读取的两个标量乘数。
        // 冷却键一律用 plan.ActionKey：内置 plan 的 ActionKey 即规范内置键；外部合成 plan 的
        // .Action 是 Call 哨兵，若用 ActionKeyOf(.Action) 会让所有外部动作错误共享 Call 冷却槽。
        return SqueakTimingModel.Evaluate(new SqueakTimingInput(nowTick, nowRealtime, timeSpeedMultiplier, plan,
            context.OverallIntervalMultiplier, actionDelta.IntervalMultiplier,
            lastTriggerTick.GetValueOrDefault(plan.ActionKey), lastTriggerRealTime.GetValueOrDefault(plan.ActionKey), lastAnyTriggerTick,
            GlobalMinIntervalTicks, GlobalCooldownMultiplier, ScaleCooldownWithTimeSpeed, periodicScale));
    }

    private bool IsPeriodicStartupPending(string actionKey, SqueakTimingEvaluation timing, int now)
    {
        if (!startupAnchorRecorded) return false;
        if (!periodicStartupPhaseMaterialized.GetValueOrDefault(actionKey)) MaterializePeriodicStartupPhase(actionKey, timing, Find.TickManager.TickRateMultiplier);
        return now < periodicStartupReadyTicks.GetValueOrDefault(actionKey);
    }

    private void MaterializePeriodicStartupPhase(string actionKey, SqueakTimingEvaluation timing, float tickRateMultiplier)
    {
        if (periodicStartupPhaseMaterialized.GetValueOrDefault(actionKey) || !startupAnchorRecorded) return;
        periodicStartupPhaseMaterialized[actionKey] = true;
        periodicStartupReadyTicks[actionKey] = CalculatePeriodicStartupReadyTick(actionKey, timing, tickRateMultiplier);
    }

    private int CalculatePeriodicStartupReadyTick(string actionKey, SqueakTimingEvaluation timing, float tickRateMultiplier)
    {
        if (!startupAnchorRecorded) return int.MinValue;
        int actionTicks = timing.ActionIntervalTicks.GetValueOrDefault();
        if (timing.ActionIntervalSeconds.HasValue)
        {
            actionTicks = Mathf.CeilToInt(timing.ActionIntervalSeconds.Value * 60f * SafeTickRateMultiplier(tickRateMultiplier));
        }
        int governingTicks = timing.GlobalApplicable ? Math.Max(actionTicks, timing.GlobalCooldownTicks) : actionTicks;
        return governingTicks > 0
            ? startupAnchorTick + 1 + (int)(StablePawnActionPhase(Pawn.ThingID, actionKey) % (uint)governingTicks)
            : startupAnchorTick;
    }

    private static float SafeTickRateMultiplier(float value) => float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : value;
    private static float SanitizePeriodicPopulationScale(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 1f ? 1f : value;

    private static uint StablePawnActionPhase(string identity, string actionKey)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char c in identity ?? string.Empty) { hash ^= c; hash *= 16777619u; }
            foreach (char c in actionKey ?? string.Empty) { hash ^= c; hash *= 16777619u; }
            return hash;
        }
    }
    /// <summary>Projects the current LifeStageDef's data-owned voice axis; missing stage is identity.</summary>
    private UniversalSqueaker.Kernel.ModulationAxis ResolveAgeModulation()
    {
        LifeStageDef? stage = Pawn.ageTracker?.CurLifeStage;
        return stage == null
            ? UniversalSqueaker.Kernel.ModulationAxis.Identity
            : new UniversalSqueaker.Kernel.ModulationAxis(true, stage.voxPitch, true, stage.voxVolume, false, (1f, 1f));
    }


    /// <summary>心情调制合并:CompProperties 默认 → 分层 context delta（Global < Race < Xenotype，
    /// 快照层已合并——S5 起不再实时读 settings.moodOverrides，H3 完成），最后合成年龄调制。</summary>
    private SqueakMoodMod ResolveMoodMod(SqueakMood mood, ResolvedSqueakContext context)
    {
        SqueakMoodMod mod = new() { mood = mood };
        if (moodModMap.TryGetValue(mood, out SqueakMoodMod? def))
        {
            mod = def.Clone();
        }

        RuntimeMoodDelta? delta = context.GetMoodDelta(mood);
        if (delta != null)
        {
            if (delta.HasPitchFactor) mod.pitchFactor = delta.PitchFactor;
            if (delta.HasVolumeFactor) mod.volumeFactor = delta.VolumeFactor;
            if (delta.HasPitchJitter) mod.pitchJitter = delta.PitchJitter;
        }

        UniversalSqueaker.Kernel.ModulationAxis moodAxis = new(true, mod.pitchFactor, true, mod.volumeFactor, true,
            (mod.pitchJitter.min, mod.pitchJitter.max));
        UniversalSqueaker.Kernel.ModulationAxis composed = UniversalSqueaker.Kernel.Modulation.ComposeModulation(moodAxis, ResolveAgeModulation());
        mod.pitchFactor = composed.Pitch;
        mod.volumeFactor = composed.Volume;
        mod.pitchJitter = new FloatRange(composed.Jitter.Min, composed.Jitter.Max);
        return mod;
    }

    private SqueakPlaybackAttempt PlayOneShot(string actionKey, SqueakMood mood, ResolvedSqueakContext context, SqueakRuntimeSnapshot snapshot)
    {
        SqueakSoundChoice choice = SqueakSoundChoice.None;
        SoundDef? def = null;
        try
        {
            choice = snapshot.ChooseProductionSoundByKey(context, actionKey, Pawn);
            def = choice.Sound;
        if (def == null)
        {
            if (MissingSoundWarnings.Add(actionKey))
            {
                SqueakLog.AudioNoSound(actionKey);
            }
            return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.NoEligibleSound, choice);
        }

            SqueakMoodMod mod = ResolveMoodMod(mood, context);
            if (!SqueakSoundAvailabilityCache.TryCreateProductionInfo(def, Pawn, out SoundInfo info, out _))
            {
                return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.EligibilityRejected, choice);
            }
            info.pitchFactor = mod.pitchFactor * mod.pitchJitter.RandomInRange;
            info.volumeFactor = mod.volumeFactor;
            def.PlayOneShot(info);
            SqueakDebug.NotifySqueakByKey(Pawn, actionKey, mood, choice);
            return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.Dispatched, choice);
        }
        catch (Exception ex)
        {
            string soundKey = def?.defName ?? actionKey;
            SqueakLog.AudioDispatchFailed(actionKey, soundKey, ex);
            return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.Exception, choice);
        }
    }

    /// <summary>CompTick 每帧维护当前 Sustainer：持续状态仍在（当前 probe 动作键与 sustainer 键一致）
    /// 且 pawn 在当前地图、未摧毁、可视范围内则 Maintain()，否则 End() 并置空。
    /// Sustainer 生命周期完全收敛在本组件（不强制走 TriggerBinding）。
    /// 原版自身会结束长期未 Maintain 的 sustainer，此处同步清理自终结的残留状态。</summary>
    private void MaintainSustainer(SqueakAction? currentAction)
    {
        Sustainer? sustainer = activeSustainer;
        if (sustainer == null) return;
        if (sustainer.Ended)
        {
            activeSustainer = null;
            activeSustainerKey = "";
            return;
        }
        string? currentKey = currentAction == null ? null : ActionKeyOf(currentAction.Value);
        bool modeSustained = currentKey != null && TryGetPlan(currentKey, out SqueakActionPlan currentPlan)
            && currentPlan.Mode == SqueakTriggerMode.Sustained;
        bool valid = modeSustained && currentKey == activeSustainerKey
            && Pawn.Spawned && !Pawn.Destroyed && Pawn.MapHeld != null
            && Pawn.MapHeld == Find.CurrentMap
            && Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(Pawn.Position);
        if (valid)
        {
            sustainer.Maintain();
        }
        else
        {
            sustainer.End();
            activeSustainer = null;
            activeSustainerKey = "";
        }
    }

    /// <summary>Sustained 模式的发声入口：选中音为 sustained SoundDef → TrySpawnSustainer 存入
    /// activeSustainer（按 pawn 位置、PerTick 维护）；非 sustain → 优雅降级为一次性 PlayOneShot。</summary>
    private SqueakPlaybackAttempt TryPlaySustained(string actionKey, SqueakMood mood, ResolvedSqueakContext context, SqueakRuntimeSnapshot snapshot)
    {
        SqueakSoundChoice choice = SqueakSoundChoice.None;
        SoundDef? def = null;
        try
        {
            // 外部/周期重复触发保护：同键已激活则直接视为已派发；异键旧 sustainer 先 End，避免重叠发声。
            if (activeSustainer != null && !activeSustainer.Ended)
            {
                if (activeSustainerKey == actionKey)
                    return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.Dispatched, SqueakSoundChoice.None);
                activeSustainer.End();
                activeSustainer = null;
                activeSustainerKey = "";
            }

            choice = snapshot.ChooseProductionSoundByKey(context, actionKey, Pawn);
            def = choice.Sound;
            if (def == null)
            {
                if (MissingSoundWarnings.Add(actionKey))
                {
                    SqueakLog.AudioNoSound(actionKey);
                }
                return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.NoEligibleSound, choice);
            }

            SqueakMoodMod mod = ResolveMoodMod(mood, context);
            if (def.sustain)
            {
                if (!SqueakSoundAvailabilityCache.TryCreateProductionInfo(def, Pawn, out _, out _))
                {
                    return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.EligibilityRejected, choice);
                }
                SoundInfo info = SoundInfo.InMap(new TargetInfo(Pawn), MaintenanceType.PerTick);
                info.pitchFactor = mod.pitchFactor * mod.pitchJitter.RandomInRange;
                info.volumeFactor = mod.volumeFactor;
                Sustainer? sustainer = def.TrySpawnSustainer(info);
                if (sustainer == null)
                {
                    return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.EligibilityRejected, choice);
                }
                activeSustainer = sustainer;
                activeSustainerKey = actionKey;
                SqueakDebug.NotifySqueakByKey(Pawn, actionKey, mood, choice);
                return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.Dispatched, choice);
            }

            // 非 sustain 音在 Sustained 模式下优雅降级为一次性播放；仍由 TryTrigger 的冷却/概率门控，
            // 不会在无 activeSustainer 时每 tick 无界重放。
            return PlayOneShot(actionKey, mood, context, snapshot);
        }
        catch (Exception ex)
        {
            string soundKey = def?.defName ?? actionKey;
            SqueakLog.AudioDispatchFailed(actionKey, soundKey, ex);
            return new SqueakPlaybackAttempt(SqueakPlaybackAttemptResult.Exception, choice);
        }
    }

    /// <summary>
    /// Resolves and plays a production-equivalent final one-shot without touching trigger gates, cooldowns, diagnostics,
    /// motes, or persistent state. Random state is restored even when resolver selection needs a random draw.
    /// </summary>
    public SqueakFinalPreviewResult PreviewFinal(SqueakAction action, SqueakMood? moodOverride = null) => PreviewFinal(action, moodOverride, SqueakSettingsGameContext.Capture());

    /// <summary>Settings preview boundary: callers may pass their once-per-frame context so no menu UI path reaches map services.</summary>
    public SqueakFinalPreviewResult PreviewFinal(SqueakAction action, SqueakMood? moodOverride, SqueakSettingsGameContext gameContext)
    {
        SqueakSoundChoice choice = SqueakSoundChoice.None;
        SqueakMood mood = SqueakMood.Neutral;
        Rand.PushState();
        try
        {
            SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);
            if (!gameContext.IsPawnOnCurrentMap(Pawn) || Pawn.Dead)
            {
                return new SqueakFinalPreviewResult(Pawn, null, mood, choice, SqueakFinalPreviewStatus.PawnOrMapUnavailable, SqueakSoundPlayability.MapRequired, "pawn_or_map_unavailable");
            }

            mood = moodOverride ?? CurrentMood;
            SqueakRuntimeSnapshot snapshot = SqueakRuntimeResolver.Current;
            ResolvedSqueakContext context = snapshot.ResolveContext(Pawn);
            choice = snapshot.ChooseProductionSound(context, action, Pawn);
            if (choice.IsNone)
            {
                return new SqueakFinalPreviewResult(Pawn, context.Xenotype, mood, choice, SqueakFinalPreviewStatus.NoEligibleSound, SqueakSoundPlayability.NoAudio, "resolver_no_eligible_sound");
            }
            SqueakSoundPlayability playability = SqueakSoundAvailabilityCache.GetProductionPlayability(choice.Sound, Pawn);
            if (playability != SqueakSoundPlayability.Playable)
            {
                return new SqueakFinalPreviewResult(Pawn, context.Xenotype, mood, choice, SqueakFinalPreviewStatus.IneligibleSound, playability, "sound_not_playable_" + playability);
            }

            SqueakMoodMod mod = ResolveMoodMod(mood, context);
            if (!SqueakSoundAvailabilityCache.TryCreateProductionInfo(choice.Sound, Pawn, out SoundInfo info, out playability))
            {
                return new SqueakFinalPreviewResult(Pawn, context.Xenotype, mood, choice, SqueakFinalPreviewStatus.IneligibleSound, playability, "sound_info_failed_" + playability);
            }
            info.pitchFactor = mod.pitchFactor * mod.pitchJitter.RandomInRange;
            info.volumeFactor = mod.volumeFactor;
            choice.Sound!.PlayOneShot(info);
            return new SqueakFinalPreviewResult(Pawn, context.Xenotype, mood, choice, SqueakFinalPreviewStatus.Dispatched, playability, "dispatched");
        }
        catch { return new SqueakFinalPreviewResult(Pawn, null, mood, choice, SqueakFinalPreviewStatus.Exception, SqueakSoundPlayability.Failed, "exception"); }
        finally { Rand.PopState(); }
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

    private void ConsumeAttemptCooldowns(string actionKey, int nowTick, float nowRealtime)
    {
        lastTriggerTick[actionKey] = nowTick;
        lastTriggerRealTime[actionKey] = nowRealtime;
        lastAnyTriggerTick = nowTick;
    }

    private void RecordOutcome(SqueakTriggerOutcome outcome, string actionKey, bool external, bool cooldownConsumed,
        SoundDef? sound, SqueakSoundSource source, float nowRealtime, string? poolStableKey = null)
    {
        if (DiagnosticsEnabled)
        {
            SqueakRecentOutcome evaluation = new(outcome, actionKey, Find.TickManager.TicksGame,
                nowRealtime, cooldownConsumed, sound, source, poolStableKey);
            lastEvaluation = evaluation;
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
    }

    private static bool IsSignificantOutcome(SqueakTriggerOutcome outcome) => outcome != SqueakTriggerOutcome.ProbabilityRejected
        && outcome != SqueakTriggerOutcome.ActionCooldown && outcome != SqueakTriggerOutcome.GlobalCooldown
        && outcome != SqueakTriggerOutcome.PeriodicStartupPending;

    /// <summary>Samples diagnostic state without selecting audio, consuming Rand, changing timestamps, or touching the production context cache.</summary>
    internal SqueakDiagnosticSnapshot GetDiagnosticSnapshot()
    {
        SqueakAction? action = CurrentAction;
        int nowTick = Find.TickManager.TicksGame;
        float nowRealtime = Time.realtimeSinceStartup;
        float timeSpeed = Find.TickManager.TickRateMultiplier;
        ResolvedSqueakContext context = SqueakRuntimeResolver.Current.ResolveContext(Pawn);
        if (action == null)
        {
            return new SqueakDiagnosticSnapshot(action, false, null, null, 1f, default, default, SqueakPeriodicPopulation.GetSnapshot(), GlobalCooldownMultiplier,
                context.Xenotype, context.OverallIntervalMultiplier, timeSpeed, 0f, 0f, false, false, SampleVocalCapability(), false, false,
                lastEvaluation, lastSignificantOutcome);
        }

        string? key = ActionKeyOf(action.Value);
        if (key == null || !actionPlans.TryGetValue(key, out SqueakActionPlan plan))
        {
            return new SqueakDiagnosticSnapshot(action, false, null, null, 1f, default, default, SqueakPeriodicPopulation.GetSnapshot(), GlobalCooldownMultiplier, context.Xenotype, context.OverallIntervalMultiplier, timeSpeed, 0f, 0f, false, false, SampleVocalCapability(), false, false, lastEvaluation, lastSignificantOutcome);
        }
        if (!plan.Configured)
        {
            return new SqueakDiagnosticSnapshot(action, false, null, null, 1f, default, default, SqueakPeriodicPopulation.GetSnapshot(), GlobalCooldownMultiplier,
                context.Xenotype, context.OverallIntervalMultiplier, timeSpeed, 0f, 0f, false, false, SampleVocalCapability(), false, false,
                lastEvaluation, lastSignificantOutcome);
        }

        RuntimeActionDelta delta = context.GetAction(action.Value);
        SqueakTriggerInvocation invocation = PeriodicInvocationFor(action.Value);
        bool actionEnabled = IsScopeEligible(action.Value, delta, invocation);
        SqueakPeriodicPopulation.Snapshot population = SqueakPeriodicPopulation.GetSnapshot();
        float periodicScale = ScalePeriodicWithAudiblePopulation && (plan.Mode == SqueakTriggerMode.EachTime || plan.Mode == SqueakTriggerMode.RandomOneShot)
            ? SanitizePeriodicPopulationScale(population.Scale) : 1f;
        SqueakTimingEvaluation timing = EvaluateTiming(plan, context, delta, nowTick, nowRealtime, timeSpeed, periodicScale);
        SqueakTimingEvaluation baseTiming = EvaluateTiming(plan, context, delta, nowTick, nowRealtime, timeSpeed, 1f);
        float baseProbability = plan.Mode == SqueakTriggerMode.RandomOneShot
            ? Mathf.Clamp01(plan.ProbabilityPerCheck * delta.ProbabilityMultiplier) : 1f;
        float probability = baseProbability / periodicScale;
        bool isPeriodic = plan.Mode == SqueakTriggerMode.EachTime || plan.Mode == SqueakTriggerMode.RandomOneShot;
        string? dk = ActionKeyOf(action.Value);
        int startupReadyTick = periodicStartupPhaseMaterialized.GetValueOrDefault(dk ?? "")
            ? periodicStartupReadyTicks.GetValueOrDefault(dk ?? "")
            : CalculatePeriodicStartupReadyTick(dk ?? "", timing, timeSpeed);
        bool startupPending = isPeriodic && startupAnchorRecorded && nowTick < startupReadyTick;
        return new SqueakDiagnosticSnapshot(action, actionEnabled, plan.Mode, plan.CooldownClock, delta.IntervalMultiplier,
            timing, baseTiming, population, GlobalCooldownMultiplier, context.Xenotype, context.OverallIntervalMultiplier, timeSpeed, probability, baseProbability, startupPending, timing.TimingReady && !startupPending,
            SampleVocalCapability(), ScaleFrequencyWithTalking && plan.Definition.VocalGatePolicy == SqueakVocalGatePolicy.ApplyTalkingGate,
            plan.Definition.VocalGatePolicy == SqueakVocalGatePolicy.ExemptTalkingGate, lastEvaluation, lastSignificantOutcome);
    }

    /// <summary>Mirrors production's global and runtime scope gates without resolving audio or mutating trigger state.</summary>
    private static bool IsScopeEligible(SqueakAction action, RuntimeActionDelta delta, SqueakTriggerInvocation invocation)
    {
        // H1 fix: scope comes only from the layered context delta, not a separate global policy early-return.
        return delta.Enabled && (delta.Scope != SqueakActionScope.ActiveCommand || invocation.IsActiveCommand);
    }

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
        EnsureSoundCache();
        HashSet<SoundDef> defs = new(SqueakRuntimeResolver.Current.KnownMapSoundDefs);
        foreach (SoundDef? def in SoundCacheMixed.Values)
        {
            if (def != null) defs.Add(def);
        }

        foreach (SoundDef def in defs)
        {
            ApplyDistanceRange(def, activeDistanceRange);
        }
    }

    /// <summary>Explicit overlay/diagnostic maintenance; snapshot readers remain side-effect free.</summary>
    internal static void MaintainPeriodicPopulationDiagnostics() => SqueakPeriodicPopulation.Maintain(activeDistanceRange);

    private static void ApplyDistanceRange(SoundDef? def, FloatRange range)
    {
        if (def?.subSounds == null)
        {
            return;
        }

        foreach (SubSoundDef subSound in def.subSounds)
        {
            if (!subSound.onCamera)
            {
                subSound.distRange = range;
            }
        }
    }

    private bool IsCurrentJobPlayerCommand() => Pawn.CurJob?.playerForced == true;
    private bool IsCurrentEquipJobPlayerCommand() => Pawn.CurJob?.playerForced == true && Pawn.CurJob.def == JobDefOf.Equip;

}

public class CompProperties_Squeaker : CompProperties
{
    public List<SqueakActionConfig> actions = new();
    public List<SqueakMoodMod> moodMods = new();

    public CompProperties_Squeaker()
    {
        compClass = typeof(CompSqueaker);
    }

    /// <summary>
    /// Programmatic default used by the canonical auto-attach path. It mirrors the shipped 216-tick
    /// baseline and the 15 production-action plan of the example packs; no race or sound-key literals.
    /// </summary>
    public static CompProperties_Squeaker CreateDefault()
    {
        return new CompProperties_Squeaker
        {
            actions = new List<SqueakActionConfig>
            {
                new() { action = SqueakAction.Eat, mode = SqueakTriggerMode.EachTime, minIntervalTicks = 144 },
                new() { action = SqueakAction.Call, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 864, probabilityPerCheck = 0.012f },
                new() { action = SqueakAction.Move, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 504, probabilityPerCheck = 0.012f },
                new() { action = SqueakAction.Sleep, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 1080, probabilityPerCheck = 0.008f },
                new() { action = SqueakAction.Social, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 504, probabilityPerCheck = 0.016f },
                new() { action = SqueakAction.Joy, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 504, probabilityPerCheck = 0.016f },
                new() { action = SqueakAction.Wounded, mode = SqueakTriggerMode.External, minIntervalTicks = 216 },
                new() { action = SqueakAction.Select, mode = SqueakTriggerMode.External, minIntervalTicks = 18, ignoreGlobalCooldown = true, cooldownClock = SqueakCooldownClock.Realtime },
                new() { action = SqueakAction.Death, mode = SqueakTriggerMode.External, minIntervalTicks = 0, ignoreGlobalCooldown = true },
                new() { action = SqueakAction.Draft, mode = SqueakTriggerMode.External, minIntervalTicks = 36, ignoreGlobalCooldown = true, cooldownClock = SqueakCooldownClock.Realtime },
                new() { action = SqueakAction.Undraft, mode = SqueakTriggerMode.External, minIntervalTicks = 36, ignoreGlobalCooldown = true, cooldownClock = SqueakCooldownClock.Realtime },
                new() { action = SqueakAction.Attack, mode = SqueakTriggerMode.External, minIntervalTicks = 216 },
                new() { action = SqueakAction.Work, mode = SqueakTriggerMode.RandomOneShot, minIntervalTicks = 720, probabilityPerCheck = 0.012f },
                new() { action = SqueakAction.Equip, mode = SqueakTriggerMode.External, minIntervalTicks = 216 },
                new() { action = SqueakAction.MentalBreak, mode = SqueakTriggerMode.External, minIntervalTicks = 0, ignoreGlobalCooldown = true },
            },
            moodMods = new List<SqueakMoodMod>
            {
                new() { mood = SqueakMood.Good, pitchFactor = 1.2f, pitchJitter = new FloatRange(0.97f, 1.03f), volumeFactor = 1.3f },
                new() { mood = SqueakMood.Neutral, pitchFactor = 1.0f, pitchJitter = new FloatRange(0.97f, 1.03f), volumeFactor = 1.0f },
                new() { mood = SqueakMood.Bad, pitchFactor = 0.8f, pitchJitter = new FloatRange(0.97f, 1.03f), volumeFactor = 0.7f },
                new() { mood = SqueakMood.Break, pitchFactor = 1.1f, pitchJitter = new FloatRange(0.6f, 1.5f), volumeFactor = 1.5f },
            },
        };
    }
}
