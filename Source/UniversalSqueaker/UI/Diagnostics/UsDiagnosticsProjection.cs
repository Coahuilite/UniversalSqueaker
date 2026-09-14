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

/// <summary>
/// The evidence clock one rendered row actually uses. This is the provenance map
/// (modding_documents/team-mode/verify-dispatch/diag-provenance-map.md) as a type: a row is either a
/// CURRENT observation re-read from live pawn/runtime state at snapshot time, a PREVIOUS evaluation
/// remembered by the comp for a completed attempt, or a PREVIOUS dispatch. "Matching action identity"
/// is not freshness, so the audio rows that read the remembered evaluation are PreviousEvaluation
/// even when the remembered attempt belongs to the current action.
/// </summary>
public enum UsDiagBasis { CurrentObservation, PreviousEvaluation, PreviousDispatch, Unknown }

/// <summary>
/// Why a row has no Pass/Block verdict. <see cref="Stochastic"/> is the narrow production meaning of
/// the two always-Pending rows (probability, talking): the gate is a random roll the panel can read
/// the parameter for but never observe, so "pending" must not be flattened into "not reached".
/// <see cref="SourceMissing"/> is reserved for a row whose current source supplied no determination.
/// </summary>
public enum UsDiagPendingReason { None, Stochastic, SourceMissing }

/// <summary>Which of the in-window narrow views is showing. Wide mode shows both columns at once.</summary>
public enum UsDiagNavView { List, Detail }

/// <summary>
/// One rendered gate-chain row. Label, status and value are SEPARATE fields on purpose (09 §3.4): the
/// status cell carries the state word (and the attention marker, at most once), the value stays the
/// raw observation, and the basis says which clock the row's data comes from.
/// </summary>
public sealed class UsDiagGateLine
{
    public UsDiagGateLine(
        string name,
        string value,
        UsDiagGateState state,
        UsDiagGateGroup group,
        UsDiagBasis basis,
        string status,
        UsDiagPendingReason pendingReason = UsDiagPendingReason.None,
        string? naReasonKey = null)
    {
        Name = name; Value = value; State = state; Group = group; Basis = basis; Status = status;
        PendingReason = pendingReason; NaReasonKey = naReasonKey;
    }

    public string Name { get; }

    /// <summary>The raw observation. It NEVER carries the attention marker or a state word.</summary>
    public string Value { get; }

    public UsDiagGateState State { get; }

    public UsDiagGateGroup Group { get; }

    public UsDiagBasis Basis { get; }

    /// <summary>The status cell: the state word (Block carries the attention marker exactly once).</summary>
    public string Status { get; }

    public UsDiagPendingReason PendingReason { get; }

    /// <summary>Key of the explanation shown when the row is N/A (never "not reached").</summary>
    public string? NaReasonKey { get; }

    /// <summary>True when this row is a live observation and may feed the current-only summary.</summary>
    public bool IsCurrent => Basis == UsDiagBasis.CurrentObservation;
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
/// The current-only headline (09 §3.2 reading order item 2). It is computed from rows provably
/// current (<see cref="UsDiagGateLine.IsCurrent"/>) ALONE; a previous-event failure can never be
/// promoted into it. <see cref="Incomplete"/> is the honest answer when no current row exists at all,
/// and <see cref="Undetermined"/> names the current rows that carry no verdict (the stochastic gates).
/// </summary>
public sealed class UsDiagCurrentSummary
{
    public UsDiagCurrentSummary(string headline, string detail, bool hasCurrentBlock, bool incomplete, IReadOnlyList<string> undetermined)
    {
        Headline = headline; Detail = detail; HasCurrentBlock = hasCurrentBlock;
        Incomplete = incomplete; Undetermined = undetermined;
    }

    public string Headline { get; }

    /// <summary>Second line naming the undetermined current rows, or empty when there are none.</summary>
    public string Detail { get; }

    public bool HasCurrentBlock { get; }

    public bool Incomplete { get; }

    public IReadOnlyList<string> Undetermined { get; }
}

/// <summary>
/// The previous-event band (09 §3.2 reading order item 3). Its facts are the two remembered slots'
/// own ticks; a line is composed only from what exists. When a tick is absent the band says recency is
/// unavailable rather than inventing an age of zero.
/// </summary>
public sealed class UsDiagPreviousBand
{
    public UsDiagPreviousBand(string heading, string evaluationLine, string dispatchLine)
    {
        Heading = heading; EvaluationLine = evaluationLine; DispatchLine = dispatchLine;
    }

    public string Heading { get; }

    /// <summary>Empty when no evaluation was ever recorded in this session.</summary>
    public string EvaluationLine { get; }

    /// <summary>Empty when nothing was ever dispatched in this session.</summary>
    public string DispatchLine { get; }

    public bool HasAny => EvaluationLine.Length > 0 || DispatchLine.Length > 0;
}

/// <summary>The two remembered slots' primitives, already extracted from the snapshot.</summary>
public struct UsDiagPreviousFacts
{
    public UsDiagPreviousFacts()
    {
        EvaluationOutcome = string.Empty;
        EvaluationAction = string.Empty;
        DispatchText = string.Empty;
    }

    public bool HasEvaluation;
    /// <summary>The machine outcome token - machine identifiers stay untranslated.</summary>
    public string EvaluationOutcome;
    /// <summary>The machine action key - machine identifiers stay untranslated.</summary>
    public string EvaluationAction;
    /// <summary>Game tick of that evaluation, or -1 when it is not known.</summary>
    public int EvaluationTick;

    public bool HasDispatch;
    public string DispatchText;
    public int DispatchTick;

    public int NowTick;
}

/// <summary>
/// One pawn's detail model: header, the current-only summary, the previous-event band, and the 16-line
/// grouped gate chain. The raw strings the panel used to lead with stay in the optional raw section.
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
        UsDiagCurrentSummary summary,
        UsDiagPreviousBand previous)
    {
        PawnText = pawnText; Ready = ready; Locked = locked;
        ActionText = actionText; AudioText = audioText; Gates = gates; Summary = summary; Previous = previous;
    }

    public string PawnText { get; }
    public bool Ready { get; }
    public bool Locked { get; }
    public string ActionText { get; }
    public string AudioText { get; }
    public IReadOnlyList<UsDiagGateLine> Gates { get; }

    /// <summary>What the CURRENT observations say, and nothing else.</summary>
    public UsDiagCurrentSummary Summary { get; }

    /// <summary>The remembered evidence, in its own band with its own recency.</summary>
    public UsDiagPreviousBand Previous { get; }
}

/// <summary>
/// Primitive, Verse-free facts the gate chain needs. The production source extracts these from a
/// snapshot + pawn; the harness synthesizes them directly, which keeps every chain rule testable
/// without touching internals. Field-by-field provenance: see the provenance map.
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
        Previous = new UsDiagPreviousFacts();
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
    /// <summary>The previous-event band's own primitives (ticks included).</summary>
    public UsDiagPreviousFacts Previous;
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
/// Pure projection of diagnostics content (round-9 contract, reworked by the 09 §3.2/§3.3 ruling):
/// the 16-line gate chain with a per-row time basis, the four-state rule (NA is neutral, never a green
/// Pass; Pending carries its reason), the current-only summary, the previous-event band, the collapsed
/// bar's three answers with their degradation ladder, and the responsive presentation decision. Every
/// string arrives already translated through the injected seam, so this file is Verse-free and
/// harness-direct: the narrow-width decision is a pure function of the measured coordinate space.
/// </summary>
public static class UsDiagnosticsProjection
{
    public const int RowsPerPage = 8;

    /// <summary>How long without an event turns the activity sentence from "last ..." into "N minutes without".</summary>
    public const int StaleAfterMinutes = 3;

    // ---- responsive presentation (the numbers the spec and the source must agree on) ---------------

    /// <summary>Shipped list column width (the search field + one page of rows).</summary>
    public const float ListColumnWidth = 232f;

    /// <summary>Shipped detail column width (the 40/60 value split without ellipsizing Chinese values).</summary>
    public const float DetailColumnWidth = 320f;

    /// <summary>The root Row's own Gap, in the spec and here.</summary>
    public const float PageGap = 8f;

    /// <summary>The root Row's own Padding, on each side.</summary>
    public const float PagePadding = 8f;

    /// <summary>The inner width below which the master/detail split cannot hold both columns.</summary>
    public const float SplitInnerMinimum = ListColumnWidth + PageGap + DetailColumnWidth;

    /// <summary>
    /// The root Row's declared Breakpoint: <see cref="SplitInnerMinimum"/> plus 32 px of margin for the
    /// scrollbar reserve, rounding and the wrapped value column. The engine compares this against the
    /// Row's own INNER width, which is what the constant is stated in.
    /// </summary>
    public const float NarrowBreakpoint = SplitInnerMinimum + 32f;

    /// <summary>
    /// The panel's own presentation decision, in the window content width the shell hands
    /// <c>BeforeDraw</c>. It must agree with the engine's <c>Breakpoint</c> evaluation on the same frame,
    /// so it subtracts the same page padding the spec declares; a lane asserts the two agree at the
    /// boundary. The narrow view switch itself belongs to the navigation-body widget, not to an
    /// attribute: the carrier's creation-time contract allows <c>Tab</c> on widgets only.
    /// </summary>
    public static bool IsNarrowPresentation(float contentWidth)
        => contentWidth - PagePadding * 2f < NarrowBreakpoint;

    private static string K(Func<string, string> tr, string key) => tr(key);

    /// <summary>
    /// The chain in production order: 16 gates, G0-G4, G6-G16 (G5 deleted as a dead row). Each gate
    /// carries the side of the chain it belongs to, the clock its data actually comes from, its status
    /// cell and (for N/A) the reason key.
    /// </summary>
    public static List<UsDiagGateLine> BuildGateChain(in UsDiagGateFacts f, Func<string, string> tr, string attentionMark = "")
    {
        List<UsDiagGateLine> lines = new(16);
        const UsDiagBasis now = UsDiagBasis.CurrentObservation;
        const UsDiagBasis prev = UsDiagBasis.PreviousEvaluation;

        UsDiagGateState disabled = f.ModeDisabled ? UsDiagGateState.Block : UsDiagGateState.Pass;
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Disabled"),
            YesNo(tr, !f.ModeDisabled), disabled, UsDiagGateGroup.Game, now,
            Status(disabled, attentionMark, tr)));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.OnMap"),
            YesNo(tr, f.OnMap), f.OnMap ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Game, now,
            Status(f.OnMap ? UsDiagGateState.Pass : UsDiagGateState.Block, attentionMark, tr)));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.OnScreen"),
            YesNo(tr, f.OnScreen), f.OnScreen ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Game, now,
            Status(f.OnScreen ? UsDiagGateState.Pass : UsDiagGateState.Block, attentionMark, tr)));

        lines.Add(f.HasTimingAction
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Plan"), YesNo(tr, true), UsDiagGateState.Pass,
                UsDiagGateGroup.Rules, now, Status(UsDiagGateState.Pass, attentionMark, tr))
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Plan"), Dash, UsDiagGateState.NA,
                UsDiagGateGroup.Rules, now, Status(UsDiagGateState.NA, attentionMark, tr),
                naReasonKey: "US.Diagnostics.Na.NoTimingAction"));

        lines.Add(BuildIdentity(f, tr, attentionMark));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeEnabled"),
            YesNo(tr, f.ActionEnabled), f.ActionEnabled ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Rules, now,
            Status(f.ActionEnabled ? UsDiagGateState.Pass : UsDiagGateState.Block, attentionMark, tr)));

        lines.Add(!f.HasTimingAction || !f.ScopeMatchApplicable
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeMatch"), Dash, UsDiagGateState.NA,
                UsDiagGateGroup.Rules, now, Status(UsDiagGateState.NA, attentionMark, tr),
                naReasonKey: "US.Diagnostics.Na.NoScopeMatch")
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ScopeMatch"),
                YesNo(tr, f.ScopeMatchPass), f.ScopeMatchPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
                UsDiagGateGroup.Rules, now,
                Status(f.ScopeMatchPass ? UsDiagGateState.Pass : UsDiagGateState.Block, attentionMark, tr)));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Startup"),
            YesNo(tr, !f.StartupPending), f.StartupPending ? UsDiagGateState.Block : UsDiagGateState.Pass,
            UsDiagGateGroup.Rules, now,
            Status(f.StartupPending ? UsDiagGateState.Block : UsDiagGateState.Pass, attentionMark, tr)));

        // G9 and G13 are the two stochastic gates: the panel reads their parameters but never the roll
        // of a future attempt, so they are Pending with the STOCHASTIC reason, never "not reached".
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Probability"),
            WithFallback(f.ProbabilityValue), UsDiagGateState.Pending, UsDiagGateGroup.Rules, now,
            Status(UsDiagGateState.Pending, attentionMark, tr), UsDiagPendingReason.Stochastic));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.ActionCooldown"),
            WithFallback(f.ActionCooldownValue), f.ActionCooldownPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Rules, now,
            Status(f.ActionCooldownPass ? UsDiagGateState.Pass : UsDiagGateState.Block, attentionMark, tr)));

        lines.Add(!f.GlobalApplicable
            ? new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.GlobalCooldown"), Dash, UsDiagGateState.NA,
                UsDiagGateGroup.Rules, now, Status(UsDiagGateState.NA, attentionMark, tr),
                naReasonKey: "US.Diagnostics.Na.GlobalIgnored")
            : new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.GlobalCooldown"),
                WithFallback(f.GlobalCooldownValue), f.GlobalPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
                UsDiagGateGroup.Rules, now,
                Status(f.GlobalPass ? UsDiagGateState.Pass : UsDiagGateState.Block, attentionMark, tr)));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.VocalOrgan"),
            YesNo(tr, f.VocalPass), f.VocalPass ? UsDiagGateState.Pass : UsDiagGateState.Block,
            UsDiagGateGroup.Audio, now,
            Status(f.VocalPass ? UsDiagGateState.Pass : UsDiagGateState.Block, attentionMark, tr)));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Talking"),
            WithFallback(f.TalkingValue), UsDiagGateState.Pending, UsDiagGateGroup.Audio, now,
            Status(UsDiagGateState.Pending, attentionMark, tr), UsDiagPendingReason.Stochastic));

        // G14-G16 read the REMEMBERED last evaluation of the current action. Matching action identity is
        // not proof of freshness, so their basis is PreviousEvaluation and they never feed the
        // current-only summary.
        bool ev = f.EvaluationBelongsToCurrentAction;
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.AudioPool"),
            !ev ? Dash : YesNo(tr, !f.AudioPoolBlocked),
            !ev ? UsDiagGateState.NA : f.AudioPoolBlocked ? UsDiagGateState.Block : UsDiagGateState.Pass,
            UsDiagGateGroup.Audio, prev,
            Status(!ev ? UsDiagGateState.NA : f.AudioPoolBlocked ? UsDiagGateState.Block : UsDiagGateState.Pass, attentionMark, tr),
            naReasonKey: !ev ? "US.Diagnostics.Na.NoEvaluationForAction" : null));

        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Playability"),
            !ev ? Dash : YesNo(tr, !f.EligibilityRejected),
            !ev ? UsDiagGateState.NA : f.EligibilityRejected ? UsDiagGateState.Block : UsDiagGateState.Pass,
            UsDiagGateGroup.Audio, prev,
            Status(!ev ? UsDiagGateState.NA : f.EligibilityRejected ? UsDiagGateState.Block : UsDiagGateState.Pass, attentionMark, tr),
            naReasonKey: !ev ? "US.Diagnostics.Na.NoEvaluationForAction" : null));

        // G16 is pure tri-state (round-9): audio attribution has its single home in the Current state row.
        UsDiagGateState dispatchState = !ev ? UsDiagGateState.NA
            : f.Dispatched ? UsDiagGateState.Pass
            : f.PlaybackFailed ? UsDiagGateState.Block : UsDiagGateState.NA;
        string dispatchValue = !ev || (!f.Dispatched && !f.PlaybackFailed) ? Dash : YesNo(tr, f.Dispatched);
        lines.Add(new UsDiagGateLine(K(tr, "US.Diagnostics.Gate.Dispatch"), dispatchValue, dispatchState,
            UsDiagGateGroup.Audio, prev, Status(dispatchState, attentionMark, tr),
            naReasonKey: dispatchState == UsDiagGateState.NA
                ? (!ev ? "US.Diagnostics.Na.NoEvaluationForAction" : "US.Diagnostics.Na.NoDispatchOutcome")
                : null));

        return lines;
    }

    /// <summary>
    /// The first gate that blocks, in production chain order, or -1 when nothing blocks. LEGACY meaning:
    /// over all 16 rows, whatever their time basis. It is NOT "where execution stopped" and it is not
    /// renamed; the current-only summary uses <see cref="FirstCurrentBlockIndex"/> instead.
    /// </summary>
    public static int FirstBlockedIndex(IReadOnlyList<UsDiagGateLine> gates)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i].State == UsDiagGateState.Block) return i;
        }

        return -1;
    }

    /// <summary>
    /// The first block among CURRENT observations only, or -1. This is the index the current-only
    /// summary is allowed to quote; remembered evidence cannot become a current blocker through it.
    /// </summary>
    public static int FirstCurrentBlockIndex(IReadOnlyList<UsDiagGateLine> gates)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i].IsCurrent && gates[i].State == UsDiagGateState.Block) return i;
        }

        return -1;
    }

    /// <summary>
    /// The current-only summary (09 §3.2 item 2). Only rows with <see cref="UsDiagGateLine.IsCurrent"/>
    /// are read; previous-event failures live in their own band. Three honest answers:
    /// a named current block, "no block found in current observations", or "current state incomplete"
    /// when no current row exists at all.
    /// </summary>
    public static UsDiagCurrentSummary BuildCurrentSummary(IReadOnlyList<UsDiagGateLine>? gates, Func<string, string> tr)
    {
        if (gates == null || gates.Count == 0) return Incomplete(tr, new List<string>());

        List<string> undetermined = new();
        bool incomplete = false;
        int first = -1;
        int currentRows = 0;
        for (int i = 0; i < gates.Count; i++)
        {
            UsDiagGateLine gate = gates[i];
            if (!gate.IsCurrent) continue;
            currentRows++;
            if (gate.State == UsDiagGateState.Block && first < 0) first = i;
            if (gate.State == UsDiagGateState.Pending)
            {
                undetermined.Add(gate.Name);
                if (gate.PendingReason == UsDiagPendingReason.SourceMissing) incomplete = true;
            }
        }

        if (currentRows == 0) return Incomplete(tr, undetermined);

        string detail = undetermined.Count > 0
            ? string.Format(K(tr, "US.Diagnostics.Summary.Undetermined"), string.Join(", ", undetermined))
            : string.Empty;

        if (first >= 0)
        {
            return new UsDiagCurrentSummary(
                string.Format(K(tr, "US.Diagnostics.Summary.CurrentBlock"), gates[first].Name),
                detail, true, incomplete, undetermined);
        }

        return incomplete
            ? new UsDiagCurrentSummary(K(tr, "US.Diagnostics.Summary.Incomplete"), detail, false, true, undetermined)
            : new UsDiagCurrentSummary(K(tr, "US.Diagnostics.Summary.NoCurrentBlock"), detail, false, false, undetermined);
    }

    private static UsDiagCurrentSummary Incomplete(Func<string, string> tr, List<string> undetermined)
        => new(K(tr, "US.Diagnostics.Summary.Incomplete"), string.Empty, false, true, undetermined);

    /// <summary>
    /// The previous-event band: the remembered evaluation and the remembered last dispatch, each with
    /// its OWN recency. A tick that exists produces a clock reading and an age; a missing one produces
    /// "recency unavailable" - no age of zero is ever invented for data that has no tick.
    /// </summary>
    public static UsDiagPreviousBand BuildPreviousBand(in UsDiagPreviousFacts f, Func<string, string> tr)
    {
        string heading = K(tr, "US.Diagnostics.Section.Previous");
        string evaluation = f.HasEvaluation
            ? string.Format(
                K(tr, "US.Diagnostics.Previous.Evaluation"),
                Text(f.EvaluationOutcome),
                Text(f.EvaluationAction),
                Recency(f.EvaluationTick, f.NowTick, tr))
            : f.HasDispatch ? K(tr, "US.Diagnostics.Previous.NoEvaluation") : string.Empty;

        string dispatch = f.HasDispatch
            ? string.Format(K(tr, "US.Diagnostics.Previous.Dispatch"), Text(f.DispatchText), Recency(f.DispatchTick, f.NowTick, tr))
            : f.HasEvaluation ? K(tr, "US.Diagnostics.Previous.NoDispatch") : string.Empty;

        // Nothing at all reads as one sentence instead of two absences.
        if (evaluation.Length == 0 && dispatch.Length == 0)
        {
            dispatch = K(tr, "US.Diagnostics.Previous.None");
        }

        return new UsDiagPreviousBand(heading, evaluation, dispatch);
    }

    /// <summary>
    /// One recency phrase. A negative or absent tick cannot be aged, so it reads "recency unavailable";
    /// a real tick of the same clock reads its clock time plus whole minutes since.
    /// </summary>
    public static string Recency(int eventTick, int nowTick, Func<string, string> tr)
    {
        if (eventTick < 0) return K(tr, "US.Diagnostics.Recency.Unavailable");
        int minutes = Math.Max(0, nowTick - eventTick) / 60;
        string clock = ClockText(eventTick);
        return minutes <= 0
            ? string.Format(K(tr, "US.Diagnostics.Recency.JustNow"), clock)
            : string.Format(K(tr, "US.Diagnostics.Recency.Ago"), clock, minutes);
    }

    /// <summary>Game clock for an event tick: 60 ticks a second, 2500 an hour, 60000 a day.</summary>
    public static string ClockText(int tick)
    {
        const long TicksPerDay = 60000L;
        const long TicksPerHour = 2500L;
        long dayTicks = ((long)tick % TicksPerDay + TicksPerDay) % TicksPerDay;
        long hourTicks = dayTicks % TicksPerHour;
        return (dayTicks / TicksPerHour).ToString("00") + ":"
            + (hourTicks * 60 / TicksPerHour).ToString("00") + ":"
            + (hourTicks / 60).ToString("00");
    }

    /// <summary>
    /// One row's status cell. Block carries the attention marker <b>exactly once</b>
    /// (<see cref="MarkOnce"/> is idempotent); Pass is the neutral pass word; Pending is the neutral
    /// ellipsis - never "not reached" and never a selection colour; N/A is the neutral N/A word.
    /// </summary>
    public static string Status(UsDiagGateState state, string attentionMark, Func<string, string> tr)
    {
        switch (state)
        {
            case UsDiagGateState.Block:
                return MarkOnce(K(tr, "US.Diagnostics.Gate.BlockMark"), attentionMark);
            case UsDiagGateState.Pass:
                return K(tr, "US.Diagnostics.Value.Pass");
            case UsDiagGateState.Pending:
                return K(tr, "US.Diagnostics.Value.Undetermined");
            default:
                return K(tr, "US.Diagnostics.Value.NotApplicable");
        }
    }

    /// <summary>
    /// Prefixes <paramref name="mark"/> at most once: re-applying the rule to an already-marked cell
    /// returns it untouched. This is the idempotence the round-9 defect fix landed (a doubled state
    /// word in game) and it now protects the status cell, which is the only place the marker may live.
    /// </summary>
    public static string MarkOnce(string text, string mark)
    {
        string body = text ?? string.Empty;
        if (string.IsNullOrEmpty(mark)) return body;
        return body.StartsWith(mark + " ", StringComparison.Ordinal) || body.Equals(mark, StringComparison.Ordinal)
            ? body
            : mark + " " + body;
    }

    /// <summary>The row's compact time-basis word (the full phrase is the band heading's job).</summary>
    public static string BasisText(UsDiagGateLine gate, Func<string, string> tr)
        => gate.Basis == UsDiagBasis.PreviousEvaluation || gate.Basis == UsDiagBasis.PreviousDispatch
            ? K(tr, "US.Diagnostics.Basis.Previous")
            : K(tr, "US.Diagnostics.Basis.Current");

    /// <summary>
    /// The row's reason line, or empty. An N/A always explains WHY it does not apply, and a Pending row
    /// always names its actual reason - neither is flattened into "not reached".
    /// </summary>
    public static string ReasonText(UsDiagGateLine gate, Func<string, string> tr)
    {
        string? reasonKey = gate.NaReasonKey;
        if (gate.State == UsDiagGateState.Pending && gate.PendingReason != UsDiagPendingReason.None)
        {
            reasonKey = gate.PendingReason == UsDiagPendingReason.Stochastic
                ? "US.Diagnostics.Pending.Stochastic"
                : "US.Diagnostics.Pending.SourceMissing";
        }

        return string.IsNullOrEmpty(reasonKey) ? string.Empty : K(tr, reasonKey!);
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

    private static UsDiagGateLine BuildIdentity(in UsDiagGateFacts f, Func<string, string> tr, string attentionMark)
    {
        string name = K(tr, "US.Diagnostics.Gate.Identity");
        if (!f.ExternalTriggerPlan)
        {
            return new UsDiagGateLine(name, Dash, UsDiagGateState.NA, UsDiagGateGroup.Game,
                UsDiagBasis.CurrentObservation, Status(UsDiagGateState.NA, attentionMark, tr),
                naReasonKey: "US.Diagnostics.Na.ExternalPlanOnly");
        }

        if (f.PlayerControlled && f.NotDowned && f.Awake)
        {
            return new UsDiagGateLine(name, YesNo(tr, true), UsDiagGateState.Pass, UsDiagGateGroup.Game,
                UsDiagBasis.CurrentObservation, Status(UsDiagGateState.Pass, attentionMark, tr));
        }

        // Blocked states name the failing condition(s) - the round-9 G4 enhancement. The state word
        // itself lives in the status cell, so the value is the breakdown alone.
        List<string> reasons = new(3);
        if (!f.PlayerControlled) reasons.Add(K(tr, "US.Diagnostics.Reason.NotPlayerControlled"));
        if (!f.NotDowned) reasons.Add(K(tr, "US.Diagnostics.Reason.Downed"));
        if (!f.Awake) reasons.Add(K(tr, "US.Diagnostics.Reason.Asleep"));
        return new UsDiagGateLine(name, string.Join(", ", reasons), UsDiagGateState.Block, UsDiagGateGroup.Game,
            UsDiagBasis.CurrentObservation, Status(UsDiagGateState.Block, attentionMark, tr));
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
                ? Dash
                : remaining.Value.ToString("0.00") + "s"
                    + (total == null ? string.Empty : "/" + total.Value.ToString("0.00") + "s");
        }

        int? remainingT = remainingTicks
            ?? (remainingSeconds.HasValue ? (int?)Math.Round(remainingSeconds.Value * 60f) : null);
        int? totalT = totalTicks
            ?? (totalSeconds.HasValue ? (int?)Math.Round(totalSeconds.Value * 60f) : null);
        return remainingT == null
            ? Dash
            : remainingT.Value + "t" + (totalT == null ? string.Empty : "/" + totalT.Value + "t");
    }

    private static string YesNo(Func<string, string> tr, bool value)
        => tr(value ? "US.Diagnostics.Value.Yes" : "US.Diagnostics.Value.No");

    /// <summary>The honest blank: a value that has no number, never a fake zero.</summary>
    public const string Dash = "—";

    private static string WithFallback(string value) => string.IsNullOrEmpty(value) ? Dash : value;

    private static string Text(string? value) => string.IsNullOrEmpty(value) ? "?" : value!;

    /// <summary>Pure page math: clamped page index for a row count.</summary>
    public static int ClampPage(int page, int rowCount)
    {
        int pages = Math.Max(1, (rowCount + RowsPerPage - 1) / RowsPerPage);
        return page < 0 ? 0 : (page > pages - 1 ? pages - 1 : page);
    }

    public static int PageCount(int rowCount) => Math.Max(1, (rowCount + RowsPerPage - 1) / RowsPerPage);
}
