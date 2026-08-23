using System;

namespace SqueakyRatkin;

// 漏斗纯时序求值（0.3.1 波 4a 漏斗纯逻辑提取：SqueakTimingModel/SqueakTimingInput/SqueakTimingEvaluation
// → 纯文件 + harness 链接，决策 §5「漏斗纯逻辑提取」）。数学与 0.2.4 CompSqueaker 内嵌实现逐字节等价；
// 适配层在采样点把 ResolvedSqueakContext/RuntimeActionDelta 投影为两个标量乘数，本文件零 Verse 引用。

/// <summary>Pure timing inputs sampled once by a caller before evaluating a trigger.
/// Context/ActionDelta are projected to the two scalar multipliers the model reads.</summary>
public readonly struct SqueakTimingInput
{
    public readonly int NowTick;
    public readonly float NowRealtime;
    public readonly float TimeSpeedMultiplier;
    public readonly SqueakActionPlan Plan;
    public readonly float OverallIntervalMultiplier;
    public readonly float IntervalMultiplier;
    public readonly int LastActionTick;
    public readonly float LastActionRealtime;
    public readonly int LastGlobalTick;
    public readonly int GlobalBaseTicks;
    public readonly float MasterMultiplier;
    public readonly bool ScaleWithTimeSpeed;
    public readonly float PeriodicPopulationScale;

    public SqueakTimingInput(int nowTick, float nowRealtime, float timeSpeedMultiplier, SqueakActionPlan plan,
        float overallIntervalMultiplier, float intervalMultiplier, int lastActionTick, float lastActionRealtime,
        int lastGlobalTick, int globalBaseTicks, float masterMultiplier, bool scaleWithTimeSpeed, float periodicPopulationScale)
    {
        NowTick = nowTick; NowRealtime = nowRealtime; TimeSpeedMultiplier = timeSpeedMultiplier;
        Plan = plan; OverallIntervalMultiplier = overallIntervalMultiplier; IntervalMultiplier = intervalMultiplier;
        LastActionTick = lastActionTick; LastActionRealtime = lastActionRealtime; LastGlobalTick = lastGlobalTick;
        GlobalBaseTicks = globalBaseTicks;
        MasterMultiplier = masterMultiplier; ScaleWithTimeSpeed = scaleWithTimeSpeed; PeriodicPopulationScale = periodicPopulationScale;
    }
}

/// <summary>Pure, immutable result shared by the production gates and overlay diagnostics.</summary>
public readonly struct SqueakTimingEvaluation
{
    public readonly int? ActionIntervalTicks;
    public readonly float? ActionIntervalSeconds;
    public readonly int GlobalCooldownTicks;
    public readonly int? ActionRemainingTicks;
    public readonly float? ActionRemainingSeconds;
    public readonly int GlobalRemainingTicks;
    public readonly bool ActionReady;
    public readonly bool GlobalApplicable;
    public readonly bool GlobalReady;
    public readonly bool TimingReady;

    internal SqueakTimingEvaluation(int? actionIntervalTicks, float? actionIntervalSeconds, int globalCooldownTicks,
        int? actionRemainingTicks, float? actionRemainingSeconds, int globalRemainingTicks, bool actionReady,
        bool globalApplicable, bool globalReady)
    {
        ActionIntervalTicks = actionIntervalTicks; ActionIntervalSeconds = actionIntervalSeconds;
        GlobalCooldownTicks = globalCooldownTicks; ActionRemainingTicks = actionRemainingTicks;
        ActionRemainingSeconds = actionRemainingSeconds; GlobalRemainingTicks = globalRemainingTicks;
        ActionReady = actionReady; GlobalApplicable = globalApplicable; GlobalReady = globalReady;
        TimingReady = actionReady && (!globalApplicable || globalReady);
    }
}

/// <summary>Pure cooldown formula shared by production gates and later diagnostics.</summary>
public static class SqueakTimingModel
{
    private static SqueakTimingEvaluation Evaluate(SqueakTimingInput input, int actionTicks, float actionSeconds, int globalTicks)
    {
        bool realtime = input.Plan.CooldownClock == SqueakCooldownClock.Realtime;
        float actionRemainingSeconds = realtime
            ? Math.Max(0f, actionSeconds - (input.NowRealtime - input.LastActionRealtime))
            : 0f;
        int actionRemainingTicks = realtime
            ? 0
            : RemainingTicks(actionTicks, input.NowTick, input.LastActionTick);
        int globalRemainingTicks = RemainingTicks(globalTicks, input.NowTick, input.LastGlobalTick);
        bool globalApplicable = !input.Plan.IgnoreGlobalCooldown;
        return new SqueakTimingEvaluation(realtime ? null : actionTicks, realtime ? actionSeconds : null, globalTicks,
            realtime ? null : actionRemainingTicks, realtime ? actionRemainingSeconds : null, globalRemainingTicks,
            realtime ? actionRemainingSeconds <= 0f : actionRemainingTicks == 0, globalApplicable,
            globalRemainingTicks == 0);
    }

    public static SqueakTimingEvaluation Evaluate(SqueakTimingInput input)
    {
        int configuredActionTicks = GetConfiguredActionTicks(input.Plan.MinIntervalTicks, input.MasterMultiplier,
            input.OverallIntervalMultiplier, input.IntervalMultiplier);
        float actionSeconds = input.Plan.CooldownClock == SqueakCooldownClock.Realtime
            ? configuredActionTicks / 60f
            : 0f;
        int actionTicks = input.Plan.CooldownClock == SqueakCooldownClock.Realtime
            ? 0
            : ApplyGameTickTimeSpeed(configuredActionTicks, input.ScaleWithTimeSpeed, input.TimeSpeedMultiplier);
        float populationScale = SanitizePopulationScale(input.PeriodicPopulationScale);
        actionTicks = ScaleTicks(actionTicks, populationScale);
        if (input.Plan.CooldownClock == SqueakCooldownClock.Realtime) actionSeconds *= populationScale;
        int globalTicks = ScaleTicks(GetGlobalCooldownTicks(input.GlobalBaseTicks, input.MasterMultiplier,
            input.ScaleWithTimeSpeed, input.TimeSpeedMultiplier), populationScale);
        return Evaluate(input, actionTicks, actionSeconds, globalTicks);
    }

    public static int GetActionIntervalTicks(
        int baseTicks,
        float masterMultiplier,
        float overallMultiplier,
        float actionMultiplier,
        bool scaleWithTimeSpeed,
        float timeSpeedMultiplier)
    {
        int configuredTicks = GetConfiguredActionTicks(baseTicks, masterMultiplier, overallMultiplier, actionMultiplier);
        return ApplyGameTickTimeSpeed(configuredTicks, scaleWithTimeSpeed, timeSpeedMultiplier);
    }

    public static float GetActionIntervalSeconds(
        int baseTicks,
        float masterMultiplier,
        float overallMultiplier,
        float actionMultiplier)
    {
        return GetConfiguredActionTicks(baseTicks, masterMultiplier, overallMultiplier, actionMultiplier) / 60f;
    }

    public static int GetGlobalCooldownTicks(
        int baseTicks,
        float masterMultiplier,
        bool scaleWithTimeSpeed,
        float timeSpeedMultiplier)
    {
        int configuredTicks = CeilingToTicks(MultiplyGlobalInterval(baseTicks, masterMultiplier));
        return ApplyGameTickTimeSpeed(configuredTicks, scaleWithTimeSpeed, timeSpeedMultiplier);
    }

    private static int GetConfiguredActionTicks(int baseTicks, float masterMultiplier, float overallMultiplier, float actionMultiplier)
    {
        float overall = SanitizeIntervalMultiplier(overallMultiplier);
        float action = SanitizeIntervalMultiplier(actionMultiplier);
        if (baseTicks <= 0 || overall == 0f || action == 0f)
        {
            return 0;
        }

        // Preserve the configured timing stages exactly: Xenotype scaling rounds first,
        // then the global master multiplier rounds that result.
        int legacyActionTicks = CeilingToTicks(baseTicks * (double)overall * action);
        float master = SanitizeIntervalMultiplier(masterMultiplier);
        return legacyActionTicks == 0 || master == 0f
            ? 0
            : CeilingToTicks(legacyActionTicks * (double)master);
    }

    private static double MultiplyGlobalInterval(int baseTicks, float masterMultiplier)
    {
        float master = SanitizeIntervalMultiplier(masterMultiplier);
        if (baseTicks <= 0 || master == 0f)
        {
            return 0d;
        }

        double value = baseTicks * (double)master;
        return double.IsInfinity(value) || value >= int.MaxValue ? int.MaxValue : value;
    }

    private static int ApplyGameTickTimeSpeed(int configuredTicks, bool scaleWithTimeSpeed, float timeSpeedMultiplier)
    {
        if (configuredTicks <= 0)
        {
            return 0;
        }

        double cooldown = configuredTicks;
        if (scaleWithTimeSpeed)
        {
            cooldown *= SanitizeTimeSpeedMultiplier(timeSpeedMultiplier);
        }

        return CeilingToTicks(cooldown);
    }

    private static int CeilingToTicks(double value) => value <= 0d
        ? 0
        : double.IsInfinity(value) || value >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(value);

    private static float SanitizeIntervalMultiplier(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, value);

    private static float SanitizeTimeSpeedMultiplier(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(1f, value);
    private static float SanitizePopulationScale(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(1f, value);
    private static int ScaleTicks(int ticks, float scale) => ticks <= 0 ? 0 : CeilingToTicks(ticks * (double)scale);

    private static int RemainingTicks(int interval, int now, int last)
    {
        if (interval <= 0 || (long)now - last >= interval)
        {
            return 0;
        }

        long remaining = (long)interval - ((long)now - last);
        return remaining >= int.MaxValue ? int.MaxValue : (int)Math.Max(0L, remaining);
    }
}
