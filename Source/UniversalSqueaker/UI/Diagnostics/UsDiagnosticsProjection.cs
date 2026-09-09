using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>Four-state gate coloring (round-9 ruling): NA is its own neutral state, never a disguised Pass.</summary>
public enum UsDiagGateState { Pass, Block, Pending, NA }

/// <summary>One rendered gate-chain row: localized name, localized value, semantic state.</summary>
public sealed class UsDiagGateLine
{
    public UsDiagGateLine(string name, string value, UsDiagGateState state)
    {
        Name = name; Value = value; State = state;
    }

    public string Name { get; }
    public string Value { get; }
    public UsDiagGateState State { get; }
}

/// <summary>Unknown = the pawn is not tracked (a search hit off-screen): honest blank, not a fake tone.</summary>
public enum UsDiagDotTone { Ready, Blocked, Pending, Unknown }

/// <summary>One summary row (the widget used three ways: list row, main monitor bar, lock-window bar).</summary>
public sealed class UsDiagRow
{
    public UsDiagRow(int pawnId, UsDiagDotTone tone, string pawnText, string actionText, string cooldownText, string audioText, bool ready, bool locked)
    {
        PawnId = pawnId; Tone = tone; PawnText = pawnText; ActionText = actionText;
        CooldownText = cooldownText; AudioText = audioText; Ready = ready; Locked = locked;
    }

    public int PawnId { get; }
    public UsDiagDotTone Tone { get; }
    public string PawnText { get; }
    public string ActionText { get; }
    public string CooldownText { get; }
    public string AudioText { get; }
    public bool Ready { get; }
    public bool Locked { get; }
}

/// <summary>The detail model of one pawn: header + current state + the 16-line gate chain.</summary>
public sealed class UsDiagDetail
{
    public UsDiagDetail(string pawnText, bool ready, bool locked, string actionText, string audioText, IReadOnlyList<UsDiagGateLine> gates)
    {
        PawnText = pawnText; Ready = ready; Locked = locked;
        ActionText = actionText; AudioText = audioText; Gates = gates;
    }

    public string PawnText { get; }
    public bool Ready { get; }
    public bool Locked { get; }
    public string ActionText { get; }
    public string AudioText { get; }
    public IReadOnlyList<UsDiagGateLine> Gates { get; }
}

/// <summary>
/// Primitive, Verse-free facts the gate chain needs. The production source extracts these from a
/// snapshot + pawn; the harness synthesizes them directly, which keeps every chain rule testable
/// without touching internals.
/// </summary>
public struct UsDiagGateFacts
{
    public UsDiagGateFacts()
    {
        ProbabilityValue = string.Empty;
        ActionCooldownValue = string.Empty;
        GlobalCooldownValue = string.Empty;
        TalkingValue = string.Empty;
        CurrentActionText = string.Empty;
        LastDispatchText = string.Empty;
    }
    // G0 Disabled bypass / G1 On map / G2 On screen.
    public bool ModeDisabled;
    public bool OnMap;
    public bool OnScreen;
    // G3 Plan (live only through its N/A state).
    public bool HasTimingAction;
    // G4 Identity (external plans only).
    public bool ExternalTriggerPlan;
    public bool PlayerControlled;
    public bool NotDowned;
    public bool Awake;
    // G6 Scope enabled / G7 Scope match.
    public bool ActionEnabled;
    public bool ScopeMatchApplicable;
    public bool ScopeMatchPass;
    // G8 Startup / G9 Probability / G10 Action cooldown.
    public bool StartupPending;
    public string ProbabilityValue = string.Empty;
    public bool ActionCooldownPass;
    public string ActionCooldownValue = string.Empty;
    // G11 Global cooldown (NA when not applicable).
    public bool GlobalApplicable;
    public bool GlobalPass;
    public string GlobalCooldownValue = string.Empty;
    // G12 Vocal organ / G13 Talking.
    public bool VocalPass;
    public string TalkingValue = string.Empty;
    // G14 Audio pool / G15 Playability / G16 Dispatch - all keyed off the current action's last evaluation.
    public bool EvaluationBelongsToCurrentAction;
    public bool AudioPoolBlocked;
    public bool EligibilityRejected;
    public bool Dispatched;
    public bool PlaybackFailed;
    // Section 1 values (already display strings; production formats via FormatDispatch).
    public string CurrentActionText = string.Empty;
    public string LastDispatchText = string.Empty;
    public bool HasLastDispatch;
}

/// <summary>
/// Pure projection of diagnostics content (round-9 contract): the 16-line gate chain (G5 was a
/// dead row - removed), the four-state rule (N/A is neutral white, never a green Pass), the G4
/// failure breakdown, the "remaining/effective-total" cooldown pair, and the four-tier audio
/// attribution. Every string arrives already translated through the injected seam, so this file
/// is Verse-free and harness-direct.
/// </summary>
public static class UsDiagnosticsProjection
{
    public const int RowsPerPage = 8;

    private static string K(Func<string, string> tr, string key) => tr(key);

    /// <summary>The chain in production order: 16 gates, G0-G4, G6-G16 (G5 deleted as a dead row).</summary>
    public static List<UsDiagGateLine> BuildGateChain(in UsDiagGateFacts f, Func<string, string> tr)
    {
        List<UsDiagGateLine> lines = new(16);

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Disabled"),
            f.ModeDisabled ? Pass(tr, false) : Pass(tr, true),
            f.ModeDisabled ? UsDiagGateState.Block : UsDiagGateState.Pass));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.OnMap"),
            Pass(tr, f.OnMap), f.OnMap ? UsDiagGateState.Pass : UsDiagGateState.Block));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.OnScreen"),
            Pass(tr, f.OnScreen), f.OnScreen ? UsDiagGateState.Pass : UsDiagGateState.Block));

        lines.Add(f.HasTimingAction
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Plan"), Pass(tr, true), UsDiagGateState.Pass)
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Plan"), NotApplicable(tr), UsDiagGateState.NA));

        lines.Add(BuildIdentity(f, tr));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeEnabled"),
            Pass(tr, f.ActionEnabled), f.ActionEnabled ? UsDiagGateState.Pass : UsDiagGateState.Block));

        lines.Add(!f.HasTimingAction || !f.ScopeMatchApplicable
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeMatch"), NotApplicable(tr), UsDiagGateState.NA)
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeMatch"),
                Pass(tr, f.ScopeMatchPass), f.ScopeMatchPass ? UsDiagGateState.Pass : UsDiagGateState.Block));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Startup"),
            Pass(tr, !f.StartupPending), f.StartupPending ? UsDiagGateState.Block : UsDiagGateState.Pass));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Probability"),
            WithFallback(f.ProbabilityValue), UsDiagGateState.Pending));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ActionCooldown"),
            WithFallback(f.ActionCooldownValue), f.ActionCooldownPass ? UsDiagGateState.Pass : UsDiagGateState.Block));

        lines.Add(!f.GlobalApplicable
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.GlobalCooldown"), NotApplicable(tr), UsDiagGateState.NA)
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.GlobalCooldown"),
                WithFallback(f.GlobalCooldownValue), f.GlobalPass ? UsDiagGateState.Pass : UsDiagGateState.Block));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.VocalOrgan"),
            Pass(tr, f.VocalPass), f.VocalPass ? UsDiagGateState.Pass : UsDiagGateState.Block));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Talking"),
            WithFallback(f.TalkingValue), UsDiagGateState.Pending));

        bool ev = f.EvaluationBelongsToCurrentAction;
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.AudioPool"),
            !ev ? NotApplicable(tr) : f.AudioPoolBlocked ? Pass(tr, false) : Pass(tr, true),
            !ev ? UsDiagGateState.NA : f.AudioPoolBlocked ? UsDiagGateState.Block : UsDiagGateState.Pass));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Playability"),
            !ev ? NotApplicable(tr) : f.EligibilityRejected ? Pass(tr, false) : Pass(tr, true),
            !ev ? UsDiagGateState.NA : f.EligibilityRejected ? UsDiagGateState.Block : UsDiagGateState.Pass));

        // G16 is pure tri-state (round-9): audio attribution has its single home in the Current state row.
        UsDiagGateState dispatchState = !ev ? UsDiagGateState.NA
            : f.Dispatched ? UsDiagGateState.Pass
            : f.PlaybackFailed ? UsDiagGateState.Block : UsDiagGateState.NA;
        string dispatchValue = !ev ? NotApplicable(tr)
            : f.Dispatched ? Pass(tr, true)
            : f.PlaybackFailed ? Pass(tr, false) : NotApplicable(tr);
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Dispatch"), dispatchValue, dispatchState));

        return lines;
    }

    private static UsDiagGateLine BuildIdentity(in UsDiagGateFacts f, Func<string, string> tr)
    {
        string name = K(tr, "US.Diagnostics.Gate.Identity");
        if (!f.ExternalTriggerPlan)
        {
            return new UsDiagGateLine(name, NotApplicable(tr), UsDiagGateState.NA);
        }

        if (f.PlayerControlled && f.NotDowned && f.Awake)
        {
            return new UsDiagGateLine(name, Pass(tr, true), UsDiagGateState.Pass);
        }

        // Blocked states name the failing condition(s) - the round-9 G4 enhancement.
        List<string> reasons = new(3);
        if (!f.PlayerControlled) reasons.Add(K(tr, "US.Diagnostics.Reason.NotPlayerControlled"));
        if (!f.NotDowned) reasons.Add(K(tr, "US.Diagnostics.Reason.Downed"));
        if (!f.Awake) reasons.Add(K(tr, "US.Diagnostics.Reason.Asleep"));
        return new UsDiagGateLine(name,
            K(tr, "US.Diagnostics.Value.Blocked") + ": " + string.Join(", ", reasons),
            UsDiagGateState.Block);
    }

    /// <summary>
    /// Four-tier audio attribution (round-9 ruling): the supplying chain tier IS the fallback state -
    /// direct hits read by their pack tier, the pack's own fallback group reads 包回退, built-in reads
    /// 原版 (the same folding the adapter and the usdiag protocol already apply).
    /// </summary>
    public static string FormatDispatch(string tierToken, string? poolKey, string? soundDefName, Func<string, string> tr)
    {
        string tier = TierLabel(tierToken, tr);
        string sound = string.IsNullOrEmpty(soundDefName) ? "?" : soundDefName!;
        return string.IsNullOrEmpty(poolKey)
            ? "[" + tier + "] : " + sound
            : "[" + tier + "·" + poolKey + "] : " + sound;
    }

    /// <summary>Tier machine tokens stay untranslated machine vocabulary; only the label is keyed.</summary>
    public static string TierLabel(string tierToken, Func<string, string> tr) => tierToken switch
    {
        "XenotypePack" => K(tr, "US.Diagnostics.Tier.XenotypePack"),
        "RacePack" => K(tr, "US.Diagnostics.Tier.RacePack"),
        "PackFallback" => K(tr, "US.Diagnostics.Tier.PackFallback"),
        "BuiltInFallback" => K(tr, "US.Diagnostics.Tier.Vanilla"),
        _ => K(tr, "US.Diagnostics.Tier.Vanilla"),
    };

    /// <summary>
    /// "remaining/total" in one unit (ticks or seconds per the s/t toggle). Null remaining = not
    /// applicable to that clock, rendered as the em-dash blank.
    /// </summary>
    public static string FormatCooldownPair(int? remainingTicks, float? remainingSeconds, int? totalTicks, float? totalSeconds, bool showSeconds)
    {
        if (showSeconds)
        {
            float? remaining = remainingSeconds
                ?? (remainingTicks.HasValue ? (float?)(remainingTicks.Value / 60f) : null);
            float? total = totalSeconds
                ?? (totalTicks.HasValue ? (float?)(totalTicks.Value / 60f) : null);
            return remaining == null
                ? "—"
                : remaining.Value.ToString("0.00") + "s"
                    + (total == null ? string.Empty : "/" + total.Value.ToString("0.00") + "s");
        }

        int? remainingT = remainingTicks
            ?? (remainingSeconds.HasValue ? (int?)Math.Round(remainingSeconds.Value * 60f) : null);
        int? totalT = totalTicks
            ?? (totalSeconds.HasValue ? (int?)Math.Round(totalSeconds.Value * 60f) : null);
        return remainingT == null
            ? "—"
            : remainingT.Value + "t" + (totalT == null ? string.Empty : "/" + totalT.Value + "t");
    }

    private static string Pass(Func<string, string> tr, bool pass)
        => tr(pass ? "US.Diagnostics.Value.Pass" : "US.Diagnostics.Value.Blocked");

    private static string NotApplicable(Func<string, string> tr) => tr("US.Diagnostics.Value.NotApplicable");

    private static string WithFallback(string value) => string.IsNullOrEmpty(value) ? "—" : value;

    /// <summary>Pure page math: clamped page index for a row count.</summary>
    public static int ClampPage(int page, int rowCount)
    {
        int pages = Math.Max(1, (rowCount + RowsPerPage - 1) / RowsPerPage);
        return page < 0 ? 0 : (page > pages - 1 ? pages - 1 : page);
    }

    public static int PageCount(int rowCount) => Math.Max(1, (rowCount + RowsPerPage - 1) / RowsPerPage);
}
