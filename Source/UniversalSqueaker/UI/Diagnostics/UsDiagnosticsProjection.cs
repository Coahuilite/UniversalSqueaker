using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>Four-state gate coloring (round-9 ruling): NA is its own neutral state, never a disguised Pass.</summary>
public enum UsDiagGateState { Pass, Block, Pending, NA }

/// <summary>
/// Which side of the chain a gate belongs to (09 §3.2's grouping): the game's own preconditions, the
/// rule layer's scope/timing decisions, and the audio layer's capability and dispatch. Presentation
/// only - the chain's ORDER stays the production evaluation order, because "first block" is defined on
/// that order and a regrouped list must not redefine it.
/// </summary>
public enum UsDiagGateGroup { Game, Rules, Audio }

/// <summary>One rendered gate-chain row: localized name, localized value, semantic state, group.</summary>
public sealed class UsDiagGateLine
{
    public UsDiagGateLine(string name, string value, UsDiagGateState state, UsDiagGateGroup group)
    {
        Name = name; Value = value; State = state; Group = group;
    }

    public string Name { get; }
    public string Value { get; }
    public UsDiagGateState State { get; }
    public UsDiagGateGroup Group { get; }
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

/// <summary>
/// One pawn's detail model: header, the verdict sentence and the 16-line grouped gate chain. The raw
/// strings the panel used to lead with moved to the optional raw section - the verdict and the chain
/// are what the reader needs first (09 §3.2: three answers within five seconds).
/// </summary>
public sealed class UsDiagDetail
{
    public UsDiagDetail(
        string pawnText,
        bool ready,
        bool locked,
        string actionText,
        string audioText,
        IReadOnlyList<UsDiagGateLine> gates,
        string verdict)
    {
        PawnText = pawnText; Ready = ready; Locked = locked;
        ActionText = actionText; AudioText = audioText; Gates = gates; Verdict = verdict;
    }

    public string PawnText { get; }
    public bool Ready { get; }
    public bool Locked { get; }
    public string ActionText { get; }
    public string AudioText { get; }
    public IReadOnlyList<UsDiagGateLine> Gates { get; }

    /// <summary>The one-sentence conclusion: will squeak / blocked at &lt;gate&gt; / not confirmed yet.</summary>
    public string Verdict { get; }
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
/// The collapsed bar's content, already through the translation seam: the three answers 09 §3.3
/// requires (what is this / is it on / what is it doing), plus the tone the activity dot carries.
/// Empty <see cref="Scale"/> and <see cref="Activity"/> mean "this segment has nothing to say", which
/// is different from "too narrow to say it" - the width ladder lives in <see cref="UsDiagnosticsProjection.LayoutBar"/>.
/// </summary>
public sealed class UsDiagBarModel
{
    public UsDiagBarModel(string identity, string switchText, string scale, string activity, UsDiagDotTone activityTone)
    {
        Identity = identity ?? string.Empty;
        SwitchText = switchText ?? string.Empty;
        Scale = scale ?? string.Empty;
        Activity = activity ?? string.Empty;
        ActivityTone = activityTone;
    }

    public string Identity { get; }
    public string SwitchText { get; }
    public string Scale { get; }
    public string Activity { get; }
    public UsDiagDotTone ActivityTone { get; }
}

/// <summary>
/// The facts the collapsed bar is built from. Everything is primitive or already-projected: the clock
/// arrives as a display string plus whole minutes so this file stays Verse-free and the harness can
/// drive "never" against "stopped" without a game.
/// </summary>
public struct UsDiagBarFacts
{
    public UsDiagBarFacts()
    {
        LockedPawnText = string.Empty;
        LastEventTimeText = string.Empty;
        PawnText = string.Empty;
        ActionText = string.Empty;
        AudioText = string.Empty;
    }

    /// <summary>The lock window's bar names its pinned pawn in the identity segment.</summary>
    public bool Locked;
    public string LockedPawnText;

    /// <summary>The tracking set's size, or 0 when the page has no list (lock window).</summary>
    public int TotalTargets;

    /// <summary>False before the first snapshot arrives: the switch reads "waiting for the first update".</summary>
    public bool HasMonitorRow;

    /// <summary>A paused game cannot produce events; saying so is more useful than looking stalled.</summary>
    public bool GamePaused;

    public string PawnText;
    public string ActionText;
    public string AudioText;
    public UsDiagDotTone MonitorTone;

    /// <summary>False means "no event has EVER arrived", which is not the same as "the last one is old".</summary>
    public bool HasLastEvent;
    public string LastEventTimeText;
    public int MinutesSinceLastEvent;
}

/// <summary>Which bar segments the width can hold. Identity and switch are contract, never layout.</summary>
public readonly struct UsDiagBarLayout
{
    public UsDiagBarLayout(bool showIdentity, bool showSwitch, bool showScale, bool showActivity)
    {
        ShowIdentity = showIdentity;
        ShowSwitch = showSwitch;
        ShowScale = showScale;
        ShowActivity = showActivity;
    }

    public bool ShowIdentity { get; }
    public bool ShowSwitch { get; }
    public bool ShowScale { get; }
    public bool ShowActivity { get; }
}

/// <summary>
/// Pure projection of diagnostics content (round-9 contract, extended by the 09 §3.2/§3.3 rework):
/// the 16-line gate chain (G5 was a dead row - removed), the four-state rule (N/A is neutral, never a
/// green Pass), the G4 failure breakdown, the "remaining/effective-total" cooldown pair, the four-tier
/// audio attribution, and now the two shapes the panel is actually read through - the collapsed bar's
/// three answers with their degradation ladder, and the verdict + grouped chain the expanded state
/// leads with. Every string arrives already translated through the injected seam, so this file is
/// Verse-free and harness-direct: the narrow-width ladder is a pure function of measured widths.
/// </summary>
public static class UsDiagnosticsProjection
{
    public const int RowsPerPage = 8;

    /// <summary>How long without an event turns the activity sentence from "last ..." into "N minutes without".</summary>
    public const int StaleAfterMinutes = 3;

    private static string K(Func<string, string> tr, string key) => tr(key);

    /// <summary>The chain in production order: 16 gates, G0-G4, G6-G16 (G5 deleted as a dead row).
    /// Each gate carries the side of the chain it belongs to, for the grouped rendering.</summary>
    public static List<UsDiagGateLine> BuildGateChain(in UsDiagGateFacts f, Func<string, string> tr)
    {
        List<UsDiagGateLine> lines = new(16);

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Disabled"),
            f.ModeDisabled ? Pass(tr, false) : Pass(tr, true),
            f.ModeDisabled ? UsDiagGateState.Block : UsDiagGateState.Pass,
            UsDiagGateGroup.Game));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.OnMap"),
            Pass(tr, f.OnMap), f.OnMap ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Game));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.OnScreen"),
            Pass(tr, f.OnScreen), f.OnScreen ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Game));

        lines.Add(f.HasTimingAction
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Plan"), Pass(tr, true), UsDiagGateState.Pass, UsDiagGateGroup.Rules)
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Plan"), NotApplicable(tr), UsDiagGateState.NA, UsDiagGateGroup.Rules));

        lines.Add(BuildIdentity(f, tr));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeEnabled"),
            Pass(tr, f.ActionEnabled), f.ActionEnabled ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Rules));

        lines.Add(!f.HasTimingAction || !f.ScopeMatchApplicable
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeMatch"), NotApplicable(tr), UsDiagGateState.NA, UsDiagGateGroup.Rules)
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeMatch"),
                Pass(tr, f.ScopeMatchPass), f.ScopeMatchPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
                UsDiagGateGroup.Rules));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Startup"),
            Pass(tr, !f.StartupPending), f.StartupPending ? UsDiagGateState.Block : UsDiagGateState.Pass,
            UsDiagGateGroup.Rules));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Probability"),
            WithFallback(f.ProbabilityValue), UsDiagGateState.Pending, UsDiagGateGroup.Rules));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ActionCooldown"),
            WithFallback(f.ActionCooldownValue), f.ActionCooldownPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Rules));

        lines.Add(!f.GlobalApplicable
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.GlobalCooldown"), NotApplicable(tr), UsDiagGateState.NA, UsDiagGateGroup.Rules)
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.GlobalCooldown"),
                WithFallback(f.GlobalCooldownValue), f.GlobalPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
                UsDiagGateGroup.Rules));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.VocalOrgan"),
            Pass(tr, f.VocalPass), f.VocalPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Audio));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Talking"),
            WithFallback(f.TalkingValue), UsDiagGateState.Pending, UsDiagGateGroup.Audio));

        bool ev = f.EvaluationBelongsToCurrentAction;
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.AudioPool"),
            !ev ? NotApplicable(tr) : f.AudioPoolBlocked ? Pass(tr, false) : Pass(tr, true),
            !ev ? UsDiagGateState.NA : f.AudioPoolBlocked ? UsDiagGateState.Block : UsDiagGateState.Pass,
            UsDiagGateGroup.Audio));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Playability"),
            !ev ? NotApplicable(tr) : f.EligibilityRejected ? Pass(tr, false) : Pass(tr, true),
            !ev ? UsDiagGateState.NA : f.EligibilityRejected ? UsDiagGateState.Block : UsDiagGateState.Pass,
            UsDiagGateGroup.Audio));

        // G16 is pure tri-state (round-9): audio attribution has its single home in the Current state row.
        UsDiagGateState dispatchState = !ev ? UsDiagGateState.NA
            : f.Dispatched ? UsDiagGateState.Pass
            : f.PlaybackFailed ? UsDiagGateState.Block : UsDiagGateState.NA;
        string dispatchValue = !ev ? NotApplicable(tr)
            : f.Dispatched ? Pass(tr, true)
            : f.PlaybackFailed ? Pass(tr, false) : NotApplicable(tr);
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Dispatch"), dispatchValue, dispatchState, UsDiagGateGroup.Audio));

        return lines;
    }

    /// <summary>
    /// The first gate that blocks, in production chain order, or -1 when nothing blocks. The verdict
    /// sentence and the chain heading both quote this one number, so the panel cannot name two
    /// different blockers.
    /// </summary>
    public static int FirstBlockedIndex(IReadOnlyList<UsDiagGateLine> gates)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i].State == UsDiagGateState.Block) return i;
        }

        return -1;
    }

    /// <summary>The chain heading's tail: the first block's name, or the "nothing blocked" phrase.</summary>
    public static string FirstBlockedText(IReadOnlyList<UsDiagGateLine> gates, Func<string, string> tr)
    {
        int index = FirstBlockedIndex(gates);
        return index < 0
            ? K(tr, "US.Diagnostics.Gates.NoneBlocked")
            : string.Format(K(tr, "US.Diagnostics.Gates.FirstBlock"), gates[index].Name);
    }

    /// <summary>
    /// The verdict sentence: the panel's one-line answer. Ready wins; otherwise the first block names
    /// itself; when the chain neither passes nor blocks, the answer is honestly "not confirmed".
    /// </summary>
    public static string BuildVerdict(bool ready, IReadOnlyList<UsDiagGateLine> gates, Func<string, string> tr)
    {
        if (ready) return K(tr, "US.Diagnostics.Verdict.Play");
        int index = FirstBlockedIndex(gates);
        return index < 0
            ? K(tr, "US.Diagnostics.Verdict.Pending")
            : string.Format(K(tr, "US.Diagnostics.Verdict.Blocked"), gates[index].Name);
    }

    /// <summary>
    /// The three answers of 09 §3.3, in the order the bar shows them. A missing event is split into
    /// "never" and "stopped" on purpose: a recorder that has never heard anything is a different
    /// problem from one that heard something three minutes ago, and only the second is worth a
    /// "N minutes without a sound" sentence.
    /// </summary>
    public static UsDiagBarModel BuildBar(in UsDiagBarFacts f, Func<string, string> tr)
    {
        string identity = f.Locked
            ? string.Format(K(tr, "US.Diagnostics.Bar.IdentityLocked"), f.LockedPawnText)
            : K(tr, "US.Diagnostics.Bar.Identity");

        string switchText = !f.HasMonitorRow
            ? K(tr, "US.Diagnostics.Monitor.Empty")
            : f.GamePaused ? K(tr, "US.Diagnostics.Bar.Paused") : K(tr, "US.Diagnostics.Bar.Recording");

        string scale = f.TotalTargets > 0
            ? string.Format(K(tr, "US.Diagnostics.Bar.Scale"), f.TotalTargets)
            : string.Empty;

        string activity;
        UsDiagDotTone tone = f.HasLastEvent ? f.MonitorTone : UsDiagDotTone.Unknown;
        if (!f.HasLastEvent)
        {
            activity = K(tr, "US.Diagnostics.Bar.Activity.Never");
        }
        else if (f.MinutesSinceLastEvent >= StaleAfterMinutes)
        {
            activity = string.Format(K(tr, "US.Diagnostics.Bar.Activity.Stale"), f.MinutesSinceLastEvent, f.LastEventTimeText);
        }
        else
        {
            activity = string.Format(
                K(tr, "US.Diagnostics.Bar.Activity.Recent"),
                f.LastEventTimeText,
                ComposeSubject(f),
                f.AudioText ?? string.Empty);
        }

        return new UsDiagBarModel(identity, switchText, scale, activity, tone);
    }

    private static string ComposeSubject(in UsDiagBarFacts f)
    {
        string pawn = f.PawnText ?? string.Empty;
        string action = f.ActionText ?? string.Empty;
        if (pawn.Length == 0) return action;
        return action.Length == 0 ? pawn : pawn + "·" + action;
    }

    /// <summary>
    /// The width-driven degradation ladder (09 §3.3, rule 1): the activity sentence is dropped first,
    /// then the scale figure. The identity and the switch are NEVER omitted - a bar that lost its name
    /// or its on/off word is exactly the "meaningless when collapsed" defect this design exists to fix -
    /// so they are forced on even when the measured width cannot hold them, and the painter ellipsizes
    /// them instead of dropping them.
    /// </summary>
    public static UsDiagBarLayout LayoutBar(UsDiagBarModel bar, float availableWidth, float actionsWidth, float gap, Func<string, float> measure)
    {
        if (bar == null) throw new ArgumentNullException(nameof(bar));
        if (measure == null) throw new ArgumentNullException(nameof(measure));

        float identity = measure(bar.Identity);
        float toggle = measure(bar.SwitchText);
        float scale = bar.Scale.Length == 0 ? 0f : measure(bar.Scale);
        float activity = bar.Activity.Length == 0 ? 0f : measure(bar.Activity);

        // Identity + switch + the two actions are the irreducible bar; scale then activity are added
        // back only when the room exists.
        float core = identity + toggle + actionsWidth + gap * 3f;
        bool showScale = scale > 0f && core + scale <= availableWidth;
        bool showActivity = activity > 0f && core + activity + (showScale ? gap + scale : 0f) <= availableWidth;
        return new UsDiagBarLayout(true, true, showScale, showActivity);
    }

    /// <summary>
    /// The activity dot's fallback vocabulary (defect D7): when the font cannot draw the dot glyph the
    /// image is replaced by the state word, never by a blank box. Kept as a key here so the painter and
    /// the lane agree on the mapping.
    /// </summary>
    public static string DotFallbackKey(UsDiagDotTone tone) => tone switch
    {
        UsDiagDotTone.Ready => "US.Diagnostics.Dot.Ready",
        UsDiagDotTone.Blocked => "US.Diagnostics.Dot.Blocked",
        UsDiagDotTone.Pending => "US.Diagnostics.Dot.Pending",
        _ => "US.Diagnostics.Dot.Unknown",
    };

    /// <summary>
    /// Numeric-cell alignment (defect D6): the value columns must scan vertically. The kernel exposes no
    /// tabular-figure axis, so every cell in one column is padded to a single advance through the same
    /// metrics seam that draws it - the strongest alignment guarantee this backend can give, and the
    /// residual (proportional digit shapes) is stated rather than hidden. Values that already meet the
    /// target come back untouched.
    /// </summary>
    public static string PadNumeric(string value, float targetWidth, Func<string, float> measure)
    {
        if (string.IsNullOrEmpty(value) || measure == null || targetWidth <= 0f) return value ?? string.Empty;

        string padded = value;
        for (int i = 0; i < 24 && measure(padded) < targetWidth - 0.5f; i++)
        {
            padded = " " + padded;
        }

        return padded;
    }

    /// <summary>The widest advance in one numeric column, in the font that column is drawn with.</summary>
    public static float NumericColumnWidth(IReadOnlyList<string> values, Func<string, float> measure)
    {
        float widest = 0f;
        if (values == null || measure == null) return widest;
        for (int i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrEmpty(values[i])) continue;
            float width = measure(values[i]!);
            if (width > widest) widest = width;
        }

        return widest;
    }

    private static UsDiagGateLine BuildIdentity(in UsDiagGateFacts f, Func<string, string> tr)
    {
        string name = K(tr, "US.Diagnostics.Gate.Identity");
        if (!f.ExternalTriggerPlan)
        {
            return new UsDiagGateLine(name, NotApplicable(tr), UsDiagGateState.NA, UsDiagGateGroup.Game);
        }

        if (f.PlayerControlled && f.NotDowned && f.Awake)
        {
            return new UsDiagGateLine(name, Pass(tr, true), UsDiagGateState.Pass, UsDiagGateGroup.Game);
        }

        // Blocked states name the failing condition(s) - the round-9 G4 enhancement.
        List<string> reasons = new(3);
        if (!f.PlayerControlled) reasons.Add(K(tr, "US.Diagnostics.Reason.NotPlayerControlled"));
        if (!f.NotDowned) reasons.Add(K(tr, "US.Diagnostics.Reason.Downed"));
        if (!f.Awake) reasons.Add(K(tr, "US.Diagnostics.Reason.Asleep"));
        return new UsDiagGateLine(name,
            K(tr, "US.Diagnostics.Value.Blocked") + ": " + string.Join(", ", reasons),
            UsDiagGateState.Block,
            UsDiagGateGroup.Game);
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
