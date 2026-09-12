using System;
using System.Collections.Generic;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// Zero-Verse lane for the diagnostics contract: the pure session role model and the pure content
/// projection (16-line chain with a per-row time basis, four-state presentation, current-only summary,
/// previous-event band, remaining/total cooldowns, four-tier dispatch labels, page math and the
/// responsive presentation decision). Every assertion here pins a ruling that came out of a maintainer
/// audit, not an implementation detail.
/// </summary>
internal static class UsDiagnosticsLogicTests
{
    /// <summary>The attention marker the production source hands the projection (UsAttention.Marker).</summary>
    private const string Mark = "[!]";

    internal static void RunAll()
    {
        SessionModelRules();
        GateChainRules();
        ProvenanceRules();
        StatusCellRules();
        SummaryRules();
        PreviousBandRules();
        PresentationRules();
        CooldownPairRules();
        DispatchLabelRules();
        PageMathRules();
        CollapsedBarRules();
        BarDegradationRules();
        GroupRules();
        NumericColumnRules();
    }

    private sealed class Key : IEquatable<Key>
    {
        public Key(int id) { Id = id; }
        public int Id;
        public bool Equals(Key? other) => other != null && other.Id == Id;
        public override bool Equals(object? o) => Equals(o as Key);
        public override int GetHashCode() => Id;
        public override string ToString() => "K" + Id;
    }

    private static void SessionModelRules()
    {
        // Round-9: a pawn is tracked while it holds ANY role; released only when it holds none.
        var m = new SqueakDiagnosticsSessionModel<Key>();
        Key a = new(1), b = new(2), c = new(3);

        m.ReplaceViewport(new[] { a, b });
        Assert(m.IsTracked(a) && m.IsTracked(b) && !m.IsTracked(c), "viewport sweep defines tracking");
        Assert(m.ViewportOrder.Count == 2 && m.ViewportOrder[0].Id == 1, "viewport keeps driver order (stable paging)");

        m.SetSelected(b);
        Assert(m.IsTracked(b) && m.HasSelected, "selection is a role");

        m.Lock(c);
        Assert(m.IsTracked(c), "lock is a role: locked pawns stay tracked off-screen (the lock ruling)");

        m.ReplaceViewport(Array.Empty<Key>());
        Assert(!m.IsTracked(a) && m.IsTracked(b) && m.IsTracked(c),
            "leaving the viewport alone must NOT drop a selected or locked pawn (off-screen tracking)");

        m.SetSelected(null);
        Assert(!m.IsTracked(b), "releasing the last role releases the pawn (driver prunes)");

        m.Unlock(c);
        Assert(!m.IsTracked(c), "unlock with no other role releases the lock pawn");

        // Revision is the single clock: membership, selection switch, lock/unlock each bump.
        var clock = new SqueakDiagnosticsSessionModel<Key>();
        int r0 = clock.Revision;
        clock.ReplaceViewport(new[] { a });
        int r1 = clock.Revision;
        Assert(r1 > r0, "membership change bumps");
        clock.ReplaceViewport(new[] { a });
        Assert(clock.Revision == r1, "a same-set sweep must NOT bump (no churn per sweep)");
        clock.SetSelected(a);
        Assert(clock.Revision > r1, "selection switch bumps");
        clock.SetSelected(a);
        int same = clock.Revision;
        Assert(same > r1 && clock.Revision == same, "re-asserting the same selection must not bump");
        clock.BumpRevision();
        Assert(clock.Revision == same + 1, "snapshot update flows through the same clock");
        clock.Lock(b);
        clock.Unlock(b);
        Assert(clock.Revision == same + 3, "lock and each unlock bump once");

        // Keyed by value equality via the default comparer (the driver passes Reference-equal Pawns).
        var lk = new SqueakDiagnosticsSessionModel<string>();
        lk.Lock("x");
        Assert(lk.Lock("x") == false, "double lock is idempotent and does not bump");
        Assert(lk.Unlock("y") == false, "unlocking a never-locked key reports false");
    }

    private static string Tr(string key) => key; // machine identity: the lane asserts KEYS and STRUCTURE.

    /// <summary>
    /// The sentences are FORMAT keys whose placeholders live in the translated value (the language files
    /// hold them), so this stub mirrors that shape - otherwise the lane would assert against a bare key
    /// and prove nothing about substitution.
    /// </summary>
    private static string Fmt(string key) => key switch
    {
        "US.Diagnostics.Bar.Scale" => "{0} targets",
        "US.Diagnostics.Bar.IdentityLocked" => "Sound log (locked: {0})",
        "US.Diagnostics.Bar.Activity.Recent" => "Last {0} {1} {2}",
        "US.Diagnostics.Bar.Activity.Stale" => "{0} min without a sound, last {1}",
        "US.Diagnostics.Summary.CurrentBlock" => "Current known block: {0}",
        "US.Diagnostics.Summary.Undetermined" => "Undetermined here: {0}",
        "US.Diagnostics.Group.CurrentFormat" => "{0} · current observations",
        "US.Diagnostics.Group.PreviousFormat" => "{0} · previous evaluation",
        "US.Diagnostics.Previous.Evaluation" => "Previous evaluation: {0} · {1} · {2}",
        "US.Diagnostics.Previous.Dispatch" => "Last dispatch: {0} · {1}",
        "US.Diagnostics.Recency.JustNow" => "at {0}, less than a minute ago",
        "US.Diagnostics.Recency.Ago" => "at {0}, {1} min ago",
        _ => key,
    };

    private static UsDiagGateFacts NormalFacts() => new()
    {
        HasTimingAction = true,
        OnMap = true,
        OnScreen = true,
        ActionEnabled = true,
        ActionCooldownPass = true,
        ActionCooldownValue = "120/600t",
        GlobalApplicable = true,
        GlobalPass = true,
        GlobalCooldownValue = "0/216t",
        VocalPass = true,
        ProbabilityValue = "0.02 / 0.03",
        TalkingValue = "0.11",
        CurrentActionText = "Work",
        HasLastDispatch = true,
        LastDispatchText = "[RacePack·k] : s",
    };

    private static void GateChainRules()
    {
        var gates = UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr, Mark);
        Assert(gates.Count == 16, "the chain is 16 lines after the G5 dead-row deletion, got " + gates.Count);
        foreach (UsDiagGateLine g in gates) Assert(g.Name.StartsWith("US.Diagnostics.Gate."), "gate names are keyed, got " + g.Name);

        // Four-state rule: NA is its own state and the neutral outlet key.
        var noAction = NormalFacts();
        noAction.HasTimingAction = false;
        var na = UsDiagnosticsProjection.BuildGateChain(noAction, Tr, Mark);
        Assert(na[3].State == UsDiagGateState.NA && na[3].Value == UsDiagnosticsProjection.Dash,
            "G3 Plan renders N/A as the fourth state with an honest dash value, never a green Pass");
        Assert(na[3].NaReasonKey != null, "and N/A carries WHY it does not apply instead of 'not reached'");
        Assert(na[6].State == UsDiagGateState.NA, "G7 Scope match is N/A without an action");
        Assert(na[15].State == UsDiagGateState.NA, "G16 without evaluation is N/A");

        var notGlobal = NormalFacts();
        notGlobal.GlobalApplicable = false;
        Assert(UsDiagnosticsProjection.BuildGateChain(notGlobal, Tr, Mark)[10].State == UsDiagGateState.NA,
            "G11 Global cooldown is N/A (not a fake Pass) when the action ignores it");

        // G4 identity: N/A off external plans; Pass on all-true; Blocked names the failing conditions.
        var ext = NormalFacts();
        Assert(UsDiagnosticsProjection.BuildGateChain(ext, Tr, Mark)[4].State == UsDiagGateState.NA,
            "G4 is N/A for non-external trigger plans");
        ext.ExternalTriggerPlan = true;
        ext.PlayerControlled = ext.NotDowned = ext.Awake = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(ext, Tr, Mark)[4].State == UsDiagGateState.Pass,
            "G4 passes a valid external pawn");
        ext.NotDowned = false;
        ext.Awake = false;
        var blocked = UsDiagnosticsProjection.BuildGateChain(ext, Tr, Mark)[4];
        Assert(blocked.State == UsDiagGateState.Block
                && blocked.Value == "US.Diagnostics.Reason.Downed" + ", " + "US.Diagnostics.Reason.Asleep",
            "G4 Blocked breaks down the failing conditions in the VALUE, with the state word in the status cell, got '"
            + blocked.Value + "' / '" + blocked.Status + "'");

        // G16 is pure tri-state: a dispatch reads Pass, never the audio string (attribution single-pointed).
        var dispatched = NormalFacts();
        dispatched.EvaluationBelongsToCurrentAction = true;
        dispatched.Dispatched = true;
        var g16 = UsDiagnosticsProjection.BuildGateChain(dispatched, Tr, Mark)[15];
        Assert(g16.State == UsDiagGateState.Pass && g16.Value == "US.Diagnostics.Value.Yes",
            "G16 keeps tri-state only; the pack:sound string belongs to the Current state row alone");
        var failed = NormalFacts();
        failed.EvaluationBelongsToCurrentAction = true;
        failed.PlaybackFailed = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(failed, Tr, Mark)[15].State == UsDiagGateState.Block,
            "G16 blocks a playback failure");
        var staleEv = NormalFacts();
        staleEv.EvaluationBelongsToCurrentAction = false;
        staleEv.Dispatched = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(staleEv, Tr, Mark)[15].State == UsDiagGateState.NA,
            "an evaluation from ANOTHER action must not light this chain (stale impersonation guard)");

        // G0 disabled bypass is a Block with the bypass truth; probability/talking stay Pending.
        var disabled = NormalFacts();
        disabled.ModeDisabled = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(disabled, Tr, Mark)[0].State == UsDiagGateState.Block, "G0 blocks when Disabled");
        Assert(UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr, Mark)[8].State == UsDiagGateState.Pending, "G9 probability is Pending (stochastic)");
        Assert(UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr, Mark)[12].State == UsDiagGateState.Pending, "G13 talking is Pending (stochastic)");
    }

    /// <summary>
    /// The provenance map as an executable contract: rows 0-12 are CURRENT observations, rows 13-15 read
    /// the remembered evaluation. A mutation that marks an audio row current (or a live row previous)
    /// must fail here, because the current-only summary is built on this split.
    /// </summary>
    private static void ProvenanceRules()
    {
        var gates = UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr, Mark);
        for (int i = 0; i <= 12; i++)
        {
            Assert(gates[i].IsCurrent, "row " + i + " (" + gates[i].Name + ") is a current observation");
        }

        for (int i = 13; i <= 15; i++)
        {
            Assert(!gates[i].IsCurrent && gates[i].Basis == UsDiagBasis.PreviousEvaluation,
                "row " + i + " (" + gates[i].Name + ") is previous-event evidence, got " + gates[i].Basis);
        }

        Assert(gates[8].PendingReason == UsDiagPendingReason.Stochastic && gates[12].PendingReason == UsDiagPendingReason.Stochastic,
            "the two always-Pending rows name the STOCHASTIC reason, not a generic one");
        Assert(UsDiagnosticsProjection.ReasonText(gates[8], Tr) == "US.Diagnostics.Pending.Stochastic",
            "and that reason is what the row shows");
    }

    /// <summary>
    /// One status mark per condition, and the raw value never carries it. The round-9 defect was a
    /// doubled state word; the round-ruling adds the second half - the mark moved OUT of the value.
    /// </summary>
    private static void StatusCellRules()
    {
        Assert(CountOccurrences(UsDiagnosticsProjection.MarkOnce("US.Diagnostics.Gate.BlockMark", Mark), Mark) == 1,
            "positive control: a marked status cell counts once");
        string once = UsDiagnosticsProjection.MarkOnce("US.Diagnostics.Value.Pass", Mark);
        Assert(UsDiagnosticsProjection.MarkOnce(once, Mark) == once,
            "the marking rule is idempotent: re-rendering cannot double the marker (the in-game defect)");

        // The all-false fixture blocks in five places (map/screen/scope/cooldown/vocal organ), so the
        // sweep below is not a one-row special case.
        var gates = UsDiagnosticsProjection.BuildGateChain(new UsDiagGateFacts(), Tr, Mark);
        int blockedRows = 0;
        for (int i = 0; i < gates.Count; i++)
        {
            UsDiagGateLine gate = gates[i];
            int marks = CountOccurrences(gate.Status, Mark);
            Assert(marks <= 1, "row " + i + " status carries the marker at most once, got '" + gate.Status + "'");
            Assert(gate.State == UsDiagGateState.Block ? marks == 1 : marks == 0,
                "a blocked row carries the marker exactly once, every other state none (row " + i + ")");
            Assert(CountOccurrences(gate.Value, Mark) == 0,
                "the raw VALUE never carries the marker (row " + i + ", value '" + gate.Value + "')");
            if (gate.State == UsDiagGateState.Block) blockedRows++;
        }

        Assert(blockedRows > 1, "the control chain has several blocked rows, so the sweep above is not a one-row special case");

        // Pending is neutral and never "not reached"; N/A is neutral and carries its reason.
        var pending = gates[8];
        Assert(pending.Status == "US.Diagnostics.Value.Undetermined",
            "a Pending row reads the neutral ellipsis word, got '" + pending.Status + "'");
        Assert(!pending.Status.Contains("NotReached") && !pending.Status.Contains("not reached"),
            "and it never claims the condition was not reached");
        var na = UsDiagnosticsProjection.BuildGateChain(new UsDiagGateFacts(), Tr, Mark)[3];
        Assert(na.Status == "US.Diagnostics.Value.NotApplicable",
            "an N/A row reads the neutral not-applicable word, got '" + na.Status + "'");
        Assert(na.Status != "US.Diagnostics.Value.Pass", "and never the pass word");
        Assert(UsDiagnosticsProjection.ReasonText(na, Tr) == "US.Diagnostics.Na.NoTimingAction",
            "an N/A row always explains itself");
        Assert(UsDiagnosticsProjection.ReasonText(gates[0], Tr).Length == 0,
            "a determined row carries no reason line");
        Assert(UsDiagnosticsProjection.BasisText(gates[0], Tr) == "US.Diagnostics.Basis.Current"
                && UsDiagnosticsProjection.BasisText(gates[13], Tr) == "US.Diagnostics.Basis.Previous",
            "each row names its own time basis");
    }

    /// <summary>
    /// The current-only summary may quote CURRENT rows alone. The adversarial case is the ruling's
    /// "current data absent + previous audio failure": the remembered failure must stay in its own band.
    /// </summary>
    private static void SummaryRules()
    {
        // (1) A current block wins the headline, even when a previous row blocks later in the chain.
        var both = NormalFacts();
        both.StartupPending = true;
        both.EvaluationBelongsToCurrentAction = true;
        both.EligibilityRejected = true;
        var gates = UsDiagnosticsProjection.BuildGateChain(both, Tr, Mark);
        UsDiagCurrentSummary summary = UsDiagnosticsProjection.BuildCurrentSummary(gates, Fmt);
        Assert(summary.HasCurrentBlock, "a current block is a current block");
        Assert(summary.Headline.Contains(gates[7].Name) && !summary.Headline.Contains(gates[14].Name),
            "the headline names the CURRENT block, not the remembered one, got '" + summary.Headline + "'");
        Assert(UsDiagnosticsProjection.FirstCurrentBlockIndex(gates) == 7, "and the current-only index finds it");

        // (2) The adversarial case: no current block at all, a previous failure blocks.
        var prevOnly = NormalFacts();
        prevOnly.EvaluationBelongsToCurrentAction = true;
        prevOnly.EligibilityRejected = true;
        var prevGates = UsDiagnosticsProjection.BuildGateChain(prevOnly, Tr, Mark);
        Assert(prevGates[14].State == UsDiagGateState.Block, "the previous evaluation really does block in this fixture");
        UsDiagCurrentSummary prevSummary = UsDiagnosticsProjection.BuildCurrentSummary(prevGates, Fmt);
        Assert(!prevSummary.HasCurrentBlock, "a remembered failure is never promoted into the current block summary");
        Assert(prevSummary.Headline == "US.Diagnostics.Summary.NoCurrentBlock",
            "the honest headline is 'no block found in current observations', got '" + prevSummary.Headline + "'");
        Assert(UsDiagnosticsProjection.FirstCurrentBlockIndex(prevGates) == -1,
            "while the current-only index reports none");
        Assert(UsDiagnosticsProjection.FirstBlockedIndex(prevGates) == 14,
            "and the LEGACY index still reports the first block over all 16 rows - its meaning is unchanged");

        // (3) The always-Pending rows are named as undetermined, not silently dropped.
        Assert(prevSummary.Undetermined.Count == 2 && prevSummary.Detail.Contains(prevGates[8].Name) && prevSummary.Detail.Contains(prevGates[12].Name),
            "the two stochastic rows are named in the summary's undetermined line, got '" + prevSummary.Detail + "'");
        Assert(!prevSummary.Incomplete, "an inherently undetermined gate is not an incomplete state");

        // (4) No rows at all = an incomplete state, never a silent all-clear.
        UsDiagCurrentSummary none = UsDiagnosticsProjection.BuildCurrentSummary(new List<UsDiagGateLine>(), Tr);
        Assert(none.Incomplete && none.Headline == "US.Diagnostics.Summary.Incomplete",
            "no current observations reads as incomplete, got '" + none.Headline + "'");
    }

    /// <summary>The previous-event band owns its own recency, and never invents an age for a missing tick.</summary>
    private static void PreviousBandRules()
    {
        var facts = new UsDiagPreviousFacts
        {
            HasEvaluation = true,
            EvaluationOutcome = "ProbabilityRejected",
            EvaluationAction = "work",
            EvaluationTick = 120000,
            NowTick = 120180,
        };
        UsDiagPreviousBand band = UsDiagnosticsProjection.BuildPreviousBand(facts, Fmt);
        Assert(band.HasAny, "a recorded evaluation fills the band");
        Assert(band.EvaluationLine.Contains("ProbabilityRejected") && band.EvaluationLine.Contains("work"),
            "the machine tokens stay untranslated in the band, got '" + band.EvaluationLine + "'");
        Assert(band.EvaluationLine.Contains("3 min ago"),
            "a real tick produces a real age, got '" + band.EvaluationLine + "'");
        Assert(band.DispatchLine == "US.Diagnostics.Previous.NoDispatch",
            "an evaluation without a dispatch says so instead of showing a blank");
        Assert(UsDiagnosticsProjection.BuildPreviousBand(new UsDiagPreviousFacts(), Fmt).DispatchLine == "US.Diagnostics.Previous.None",
            "nothing at all reads as one sentence about the whole band");

        var noTicks = new UsDiagPreviousFacts { HasEvaluation = true, EvaluationOutcome = "Disabled", EvaluationAction = "call", EvaluationTick = -1, NowTick = 500 };
        Assert(UsDiagnosticsProjection.BuildPreviousBand(noTicks, Fmt).EvaluationLine.Contains("US.Diagnostics.Recency.Unavailable"),
            "an event with no tick says recency is unavailable instead of manufacturing an age");
        Assert(UsDiagnosticsProjection.Recency(-1, 10, Fmt) == "US.Diagnostics.Recency.Unavailable",
            "and the recency helper agrees");
        Assert(UsDiagnosticsProjection.ClockText(0) == "00:00:00", "the game clock formats from a tick, got " + UsDiagnosticsProjection.ClockText(0));
    }

    /// <summary>
    /// The responsive decision (09 §3.5): the split needs both fixed columns plus the gap, and the
    /// declared Breakpoint keeps a margin above that demonstrable minimum. The spec and the source both
    /// read these constants, so a threshold below the real minimum dies here instead of in game.
    /// </summary>
    private static void PresentationRules()
    {
        Assert(UsDiagnosticsProjection.SplitInnerMinimum == UsDiagnosticsProjection.ListColumnWidth
                + UsDiagnosticsProjection.PageGap + UsDiagnosticsProjection.DetailColumnWidth,
            "the split minimum is the two columns plus the gap, measured not guessed");
        Assert(UsDiagnosticsProjection.NarrowBreakpoint > UsDiagnosticsProjection.SplitInnerMinimum,
            "the declared breakpoint keeps a margin above the minimum, got " + UsDiagnosticsProjection.NarrowBreakpoint);

        float exactlyWide = UsDiagnosticsProjection.NarrowBreakpoint + UsDiagnosticsProjection.PagePadding * 2f;
        Assert(!UsDiagnosticsProjection.IsNarrowPresentation(exactlyWide),
            "at exactly the breakpoint the split is still used, got " + exactlyWide);
        Assert(UsDiagnosticsProjection.IsNarrowPresentation(exactlyWide - 0.5f),
            "half a pixel below it the navigation presentation takes over");
        Assert(!UsDiagnosticsProjection.IsNarrowPresentation(100000f),
            "and a wide page is never dragged into the navigation presentation");
    }

    private static void CooldownPairRules()
    {
        string tick = UsDiagnosticsProjection.FormatCooldownPair(300, null, 900, null, false);
        Assert(tick == "300t/900t", "remaining/total in ticks (round-9 timer ruling), got " + tick);
        string sec = UsDiagnosticsProjection.FormatCooldownPair(null, 5f, null, 15f, true);
        Assert(sec == "5.00s/15.00s", "remaining/total in seconds, got " + sec);
        string realtimeAsTicks = UsDiagnosticsProjection.FormatCooldownPair(null, 2.5f, null, 7.5f, false);
        Assert(realtimeAsTicks == "150t/450t", "realtime cooldown converts to ticks when t-mode shows, got " + realtimeAsTicks);
        Assert(UsDiagnosticsProjection.FormatCooldownPair(null, null, null, null, false) == UsDiagnosticsProjection.Dash,
            "nothing applicable renders the honest em-dash, never a fake 0");
    }

    private static void DispatchLabelRules()
    {
        // Four-tier attribution (round-9): the supplying tier IS the state; the pack key rides along.
        string xeno = UsDiagnosticsProjection.FormatDispatch("XenotypePack", "craftsmen-xeno", "Squeak_Happy_03", Tr);
        Assert(xeno == "[US.Diagnostics.Tier.XenotypePack·craftsmen-xeno] : Squeak_Happy_03", "xenotype direct hit reads by pack key, got " + xeno);
        string race = UsDiagnosticsProjection.FormatDispatch("RacePack", "militia-race", "Squeak_Work_01", Tr);
        Assert(race == "[US.Diagnostics.Tier.RacePack·militia-race] : Squeak_Work_01", "cross-tier fallback to race reads [Race pack·key] (the maintainer's ladder rung 2)");
        string packFb = UsDiagnosticsProjection.FormatDispatch("PackFallback", "militia-race", "Squeak_Mumble_02", Tr);
        Assert(packFb == "[US.Diagnostics.Tier.PackFallback·militia-race] : Squeak_Mumble_02",
            "in-pack fallback keeps the supplying pack key (rung 3, SqueakPoolRegistry evidence)");
        string vanilla = UsDiagnosticsProjection.FormatDispatch("BuiltInFallback", null, "Squeak_Call_04", Tr);
        Assert(vanilla == "[US.Diagnostics.Tier.Vanilla] : Squeak_Call_04", "built-in reads [Vanilla] with no key (rung 4, adapter folding made visible)");
        Assert(UsDiagnosticsProjection.FormatDispatch("RacePack", "k", null, Tr).EndsWith(" : ?"),
            "a dispatch without a resolved sound stays honest with '?'");
    }

    private static void PageMathRules()
    {
        Assert(UsDiagnosticsProjection.RowsPerPage == 8, "8 rows per page (round-9 ruling)");
        Assert(UsDiagnosticsProjection.PageCount(0) == 1 && UsDiagnosticsProjection.PageCount(8) == 1
            && UsDiagnosticsProjection.PageCount(9) == 2 && UsDiagnosticsProjection.PageCount(38) == 5,
            "page count is ceiling division with a floor of one");
        Assert(UsDiagnosticsProjection.ClampPage(99, 9) == 1 && UsDiagnosticsProjection.ClampPage(-5, 9) == 0
            && UsDiagnosticsProjection.ClampPage(4, 0) == 0, "page clamps into range for any count");
    }

    private static UsDiagBarFacts BarFacts(bool hasEvent, string time, int minutesAgo) => new()
    {
        Locked = false,
        TotalTargets = 8,
        HasMonitorRow = true,
        GamePaused = false,
        PawnText = "格里姆",
        ActionText = "打招呼",
        AudioText = "已播",
        MonitorTone = UsDiagDotTone.Ready,
        HasLastEvent = hasEvent,
        LastEventTimeText = time,
        MinutesSinceLastEvent = minutesAgo,
    };

    private static void CollapsedBarRules()
    {
        UsDiagBarModel recent = UsDiagnosticsProjection.BuildBar(BarFacts(true, "12:04:09", 0), Fmt);
        Assert(recent.Identity.Length > 0, "the bar answers 'what is this' (identity segment is never empty)");
        Assert(recent.SwitchText.Length > 0, "the bar answers 'is it on' (switch segment is never empty)");
        Assert(recent.Activity.Length > 0, "the bar answers 'what is it doing' (activity segment is never empty)");
        Assert(recent.Scale.Contains("8"), "the scale figure carries the tracking count, got " + recent.Scale);
        Assert(recent.Activity.Contains("12:04:09") && recent.Activity.Contains("格里姆"),
            "the recent sentence carries the event time and the subject, got " + recent.Activity);

        UsDiagBarModel never = UsDiagnosticsProjection.BuildBar(BarFacts(false, string.Empty, 0), Fmt);
        UsDiagBarModel stale = UsDiagnosticsProjection.BuildBar(BarFacts(true, "12:01:00", 3), Fmt);
        Assert(never.Activity != stale.Activity && never.Activity != recent.Activity && stale.Activity != recent.Activity,
            "'never received' and 'was receiving, now stopped' must read differently (09 §3.3's new projection)");
        Assert(never.Activity == "US.Diagnostics.Bar.Activity.Never",
            "a recorder that never heard anything says so, got " + never.Activity);
        Assert(stale.Activity.Contains("3"), "the stopped sentence carries how long it has been quiet, got " + stale.Activity);
        Assert(stale.Activity.Contains("12:01:00"), "and still names the last event time");

        var lockedFacts = BarFacts(true, "12:04:09", 0);
        lockedFacts.Locked = true;
        lockedFacts.LockedPawnText = "Violet";
        UsDiagBarModel lockedBar = UsDiagnosticsProjection.BuildBar(lockedFacts, Fmt);
        Assert(lockedBar.Identity.Contains("Violet"), "the lock window's bar identifies its pinned pawn, got " + lockedBar.Identity);

        var waiting = BarFacts(false, string.Empty, 0);
        waiting.HasMonitorRow = false;
        Assert(UsDiagnosticsProjection.BuildBar(waiting, Tr).SwitchText == "US.Diagnostics.Monitor.Empty",
            "no snapshot yet reads as the waiting state, never as 'recording'");
        var paused = BarFacts(true, "12:04:09", 0);
        paused.GamePaused = true;
        Assert(UsDiagnosticsProjection.BuildBar(paused, Tr).SwitchText == "US.Diagnostics.Bar.Paused",
            "a paused game says so instead of looking stalled");

        var noScale = BarFacts(true, "12:04:09", 0);
        noScale.TotalTargets = 0;
        Assert(UsDiagnosticsProjection.BuildBar(noScale, Tr).Scale.Length == 0,
            "a page without a list has no scale figure to show");
    }

    /// <summary>09 §3.3 rule 1's ladder: activity goes first, then the scale, never the identity or the switch.</summary>
    private static void BarDegradationRules()
    {
        UsDiagBarModel bar = UsDiagnosticsProjection.BuildBar(BarFacts(true, "12:04:09", 0), Tr);
        Func<string, float> measure = text => text.Length * 10f;

        const float actions = 40f;
        const float gap = 6f;
        float core = measure(bar.Identity) + measure(bar.SwitchText) + actions + gap * 3f;
        float withScale = core + measure(bar.Scale);
        float withEverything = withScale + gap + measure(bar.Activity);

        UsDiagBarLayout wide = UsDiagnosticsProjection.LayoutBar(bar, withEverything + 1f, actions, gap, measure);
        Assert(wide.ShowIdentity && wide.ShowSwitch && wide.ShowScale && wide.ShowActivity,
            "a wide bar answers all three questions");

        // Drop the activity first: it is the only segment that can go while the bar still explains itself.
        UsDiagBarLayout medium = UsDiagnosticsProjection.LayoutBar(bar, withScale + 1f, actions, gap, measure);
        Assert(!medium.ShowActivity && medium.ShowScale && medium.ShowIdentity && medium.ShowSwitch,
            "the activity sentence is the first thing narrow widths drop");

        // Then the scale figure.
        UsDiagBarLayout narrow = UsDiagnosticsProjection.LayoutBar(bar, core + 1f, actions, gap, measure);
        Assert(!narrow.ShowActivity && !narrow.ShowScale && narrow.ShowIdentity && narrow.ShowSwitch,
            "the scale figure goes second");

        // And even when NOTHING fits, the identity and the switch are still contract (09 §3.3 rule 1):
        // this is the fallback a mutation must be able to remove.
        UsDiagBarLayout impossible = UsDiagnosticsProjection.LayoutBar(bar, 1f, 40f, 6f, measure);
        Assert(impossible.ShowIdentity && impossible.ShowSwitch,
            "identity and switch are NEVER omitted, not even when the measured width cannot hold them");
    }

    private static void GroupRules()
    {
        var normal = UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr, Mark);
        int game = 0, rules = 0, audio = 0;
        foreach (UsDiagGateLine gate in normal)
        {
            if (gate.Group == UsDiagGateGroup.Game) game++;
            else if (gate.Group == UsDiagGateGroup.Rules) rules++;
            else audio++;
        }

        Assert(game == 4 && rules == 7 && audio == 5,
            "the 16 gates split 4/7/5 across game/rules/audio, got " + game + "/" + rules + "/" + audio);
        Assert(normal[4].Group == UsDiagGateGroup.Game && normal[3].Group == UsDiagGateGroup.Rules
                && normal[14].Group == UsDiagGateGroup.Audio,
            "the identity gate is a game-side precondition while the plan gate is rules-side");

        // The audio group is the one that mixes clocks, which is why the bands are (group, basis) pairs.
        int audioCurrent = 0, audioPrevious = 0;
        foreach (UsDiagGateLine gate in normal)
        {
            if (gate.Group != UsDiagGateGroup.Audio) continue;
            if (gate.IsCurrent) audioCurrent++;
            else audioPrevious++;
        }

        Assert(audioCurrent == 2 && audioPrevious == 3,
            "audio splits into 2 current + 3 previous rows, got " + audioCurrent + "/" + audioPrevious);
        Assert(UsDiagnosticsProjection.FirstBlockedIndex(normal) == -1, "an all-clear chain reports no block");
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = 0;
        while (token.Length > 0 && index < text.Length)
        {
            int found = text.IndexOf(token, index, StringComparison.Ordinal);
            if (found < 0) break;
            count++;
            index = found + token.Length;
        }

        return count;
    }

    private static void NumericColumnRules()
    {
        Func<string, float> measure = text => text.Length * 7f;
        string[] column = { "120/600t", "0/216t", "12/12:00" };
        float widest = UsDiagnosticsProjection.NumericColumnWidth(column, measure);
        Assert(Math.Abs(widest - 8 * 7f) < 0.001f, "the column advance is the widest cell, got " + widest);

        string padded = UsDiagnosticsProjection.PadNumeric("0/216t", widest, measure);
        Assert(measure(padded) >= widest - 0.5f && padded.EndsWith("0/216t"),
            "a short cell is padded up to the column advance without touching its digits, got '" + padded + "'");
        Assert(UsDiagnosticsProjection.PadNumeric("120/600t", widest, measure) == "120/600t",
            "a cell that already meets the advance is returned untouched");
        Assert(UsDiagnosticsProjection.PadNumeric("", widest, measure) == string.Empty,
            "an empty cell stays empty (honest blank, never padded into a value)");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("UsDiagnosticsLogic: " + message);
    }
}
