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
    public readonly Responses.SessionObservation? Response;
    public readonly SqueakVocalCapability VocalCapability;
    public readonly SqueakRecentOutcome? LastEvaluation, LastSignificantOutcome, LastDispatched;
    public readonly string KernelText;
    public readonly SqueakResponseEvidence? Evidence;
    internal SqueakDiagnosticSnapshot(Responses.SessionObservation? response, SqueakVocalCapability vocal,
        SqueakRecentOutcome? evaluation, SqueakRecentOutcome? significant, SqueakRecentOutcome? dispatched, string text,
        SqueakResponseEvidence? evidence = null)
    { Response = response; VocalCapability = vocal; LastEvaluation = evaluation; LastSignificantOutcome = significant; LastDispatched = dispatched; KernelText = text; Evidence = evidence; }
}
/// <summary>
/// 挂在带 Squeaker 组件的 pawn 上的自驱动发声组件。
/// 配置三层:CompProperties(XML默认) ← ModSettings(玩家override) ← 运行时。
/// 心情靠运行时 pitchFactor/volumeFactor 调制,每动作只需 1 个 SoundDef + 1 套中性音频。
/// </summary>
public partial class CompSqueaker : ThingComp
{
    // Eat occurrence granularity: cheap same-frame runtime statics published by the settings layer
    // (ApplyToRuntime / NotifyCheapRuntimeChanged). Defaults come from the pure rule, never a literal here.
    public static bool DiagnosticsEnabled;

    private static FloatRange activeDistanceRange = new(15f, 50f);

    private SqueakRecentOutcome? lastEvaluation;
    private SqueakRecentOutcome? lastSignificantOutcome;
    private SqueakRecentOutcome? lastDispatched;
    // Runtime-only membership in the audible-population diagnostic projection.
    private Map? registeredMap;



    private Pawn Pawn => (Pawn)parent;
    internal Pawn RegisteredPawn => Pawn;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        // Avoid resolver or population rebuild work here: batch spawning remains O(N).
        registeredMap = Pawn.Spawned ? Pawn.MapHeld : null;
        SqueakPeriodicPopulation.Register(this, registeredMap);
    }

    /// <summary>
    /// S4 diagnostics: the head mark is drawn from the IMGUI pass by
    /// <see cref="Patch_MapInterface_DiagnosticsMarks"/>, never from the thing render pass.
    /// <c>ThingComp.PostDraw</c> is reached through <c>Pawn.Draw &#8594; Comps_PostDraw()</c>
    /// (RimWorld <c>Verse/Pawn.cs:2754</c>) during thing rendering, and <c>GenMapUI.DrawText</c> goes
    /// through the game's IMGUI text path: the first real run logged "You can only call GUI functions
    /// from inside OnGUI" 8558 times there and no mark ever appeared.
    ///
    /// The cached glyph/colour now delegate to the shared neutral screen-mark widget through the
    /// session-owned thin projection adapter. Returns true when the mark was drawn.
    /// This method performs no error handling of its own - the
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

        return SqueakDiagnosticsOverlay.DrawCachedMark(Pawn, mark, color);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {

        SqueakerRuntime.ReleasePawn(Pawn);
        SqueakDiagnosticsOverlay.NotifyTargetUnavailable(Pawn);
        SqueakPeriodicPopulation.Unregister(this, previousMap);
        registeredMap = null;
        base.PostDestroy(mode, previousMap);
    }

    internal void NotifyPeriodicDespawn(Map map)
    {
        SqueakerRuntime.ReleasePawn(Pawn);
        SqueakDiagnosticsOverlay.NotifyTargetUnavailable(Pawn);
        SqueakPeriodicPopulation.Unregister(this, map);
        if (ReferenceEquals(registeredMap, map)) registeredMap = null;
    }
    /// <summary>主动命令保持玩家控制权来源门；选择事实与官方Pawn回应资格在各自明确合同处处理。</summary>
    private bool IdentityGateAllows(SqueakTriggerInvocation invocation)
        => !invocation.IsActiveCommand || Pawn.IsPlayerControlled; // Explicit player-command provenance, not a Select qualification.

    private void SynchronizePeriodicMembership()
    {
        Map? map = Pawn.Spawned ? Pawn.MapHeld : null;
        if (ReferenceEquals(map, registeredMap)) return;
        SqueakPeriodicPopulation.Unregister(this, registeredMap);
        registeredMap = map;
        SqueakPeriodicPopulation.Register(this, registeredMap);
    }


    private void RecordOutcome(SqueakTriggerOutcome outcome, string actionKey, bool external, bool cooldownConsumed,
        SoundDef? sound, SqueakSoundSource source, float nowRealtime, string? poolStableKey = null,
        UniversalSqueaker.Kernel.ChainTier? tier = null)
    {
        if (DiagnosticsEnabled || SqueakDiagnosticsOverlay.IsTracked(Pawn))
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
        var evidence = SqueakerRuntime.DiagnosticEvidence(Pawn);
        return new SqueakDiagnosticSnapshot(evidence.Observation, default, lastEvaluation, lastSignificantOutcome, lastDispatched, "", evidence);
    }

    private int responseDiagnosticAttempts;
    private double responseDiagnosticUntil;
    private long responseDiagnosticSuppressed, responseDiagnosticSuppressedTotal, responseDiagnosticFailures;
    private SqueakDiagnosticLogResult lastDiagnosticLogResult;
    internal bool WantsDetailedDiagnostics => DiagnosticsEnabled || SqueakDiagnosticsOverlay.IsRecording(Pawn)
        || SqueakLog.ShouldEmitDev && (Time.realtimeSinceStartup >= responseDiagnosticUntil || responseDiagnosticAttempts < 64);
    internal string DiagnosticLogStatus => "mode=" + SqueakLog.Mode + " effective=" + SqueakLog.ShouldEmitDev
        + " window_attempted=" + responseDiagnosticAttempts + " window_until_seconds=" + responseDiagnosticUntil.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
        + " rate_suppressed=" + responseDiagnosticSuppressed + " total_suppressed=" + responseDiagnosticSuppressedTotal
        + " output_failures=" + responseDiagnosticFailures + " last_output=" + lastDiagnosticLogResult;
    internal void RecordKernelOutcome(Responses.DecisionOutcome outcome, SoundDef? sound, Responses.PlaybackRequest? request)
    {
        var mapped = outcome.Receipt?.Result == Responses.PlaybackResult.Dispatched
            ? SqueakTriggerOutcome.Dispatched : SqueakTriggerOutcome.EligibilityRejected;
        var source = request?.Audio.SourceTier switch
        {
            UniversalSqueaker.Kernel.ChainTier.XenotypePack => SqueakSoundSource.XenotypePack,
            UniversalSqueaker.Kernel.ChainTier.RacePack => SqueakSoundSource.RacePack,
            UniversalSqueaker.Kernel.ChainTier.PackFallback => request!.Audio.SourceDomain.Xenotype.HasValue
                ? SqueakSoundSource.XenotypePack : SqueakSoundSource.RacePack,
            UniversalSqueaker.Kernel.ChainTier.BuiltInFallback => SqueakSoundSource.Vanilla,
            _ => SqueakSoundSource.None
        };
        RecordOutcome(mapped, outcome.Action, false,
            outcome.Receipt?.Result == Responses.PlaybackResult.Dispatched || outcome.Receipt?.Result == Responses.PlaybackResult.Indeterminate,
            sound, source, Time.realtimeSinceStartup, request?.Audio.PackKey, request?.Audio.SourceTier);
        SqueakDiagnosticsOverlay.RecordSettled(Pawn);
        if (!SqueakLog.ShouldEmitDev)
        {
            responseDiagnosticAttempts = 0; responseDiagnosticUntil = 0; responseDiagnosticSuppressed = 0;
            lastDiagnosticLogResult = SqueakDiagnosticLogResult.ModeOff;
            return;
        }
        double now = Time.realtimeSinceStartup;
        long previousSuppressed = 0;
        if (now >= responseDiagnosticUntil)
        {
            previousSuppressed = responseDiagnosticSuppressed;
            responseDiagnosticAttempts = 0; responseDiagnosticSuppressed = 0; responseDiagnosticUntil = now + 60;
        }
        if (responseDiagnosticAttempts >= 64)
        { responseDiagnosticSuppressed++; responseDiagnosticSuppressedTotal++; return; }
        responseDiagnosticAttempts++;
        lastDiagnosticLogResult = SqueakerRuntime.LogResponse(Pawn, responseDiagnosticAttempts == 64, previousSuppressed, responseDiagnosticUntil);
        if (lastDiagnosticLogResult is SqueakDiagnosticLogResult.FormatFailed or SqueakDiagnosticLogResult.SinkFailed) responseDiagnosticFailures++;
    }
    internal bool ControlledInvocationAllowed(SqueakTriggerInvocation invocation) => IdentityGateAllows(invocation);
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

    public static void ApplyDistanceRange(FloatRange range)
    {
        activeDistanceRange = range;
        SqueakPeriodicPopulation.NotifyDistanceChanged();
        UsInstanceOutput.Distance = range;
    }

    /// <summary>Explicit overlay/diagnostic maintenance; snapshot readers remain side-effect free.</summary>
    internal static void MaintainPeriodicPopulationDiagnostics() => SqueakPeriodicPopulation.Maintain(activeDistanceRange);

    // One shared bounded-cadence audible snapshot; never a new per-Pawn map scan. A paused
    // game has no state ticks, so normalize its zero tick rate to the neutral speed.
    internal static double StateRhythmFactor() => SqueakPeriodicPopulation.Maintain(activeDistanceRange).Scale
        * Math.Max(1d, Find.TickManager.TickRateMultiplier);


}



