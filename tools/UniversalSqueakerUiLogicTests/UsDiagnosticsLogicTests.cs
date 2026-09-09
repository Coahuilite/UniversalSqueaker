using System;
using System.Collections.Generic;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// Zero-Verse lane for the round-9 diagnostics contract: the pure session role model and the
/// pure content projection (16-line chain, four-state NA, G4 breakdown, remaining/total
/// cooldowns, four-tier dispatch labels, page math). Every assertion here pins a ruling that
/// came out of the maintainer audit, not an implementation detail.
/// </summary>
internal static class UsDiagnosticsLogicTests
{
    internal static void RunAll()
    {
        SessionModelRules();
        GateChainRules();
        CooldownPairRules();
        DispatchLabelRules();
        PageMathRules();
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
        var gates = UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr);
        Assert(gates.Count == 16, "the chain is 16 lines after the G5 dead-row deletion, got " + gates.Count);
        foreach (UsDiagGateLine g in gates) Assert(g.Name.StartsWith("US.Diagnostics.Gate."), "gate names are keyed, got " + g.Name);

        // Four-state rule: NA is its own state and the neutral outlet key.
        var noAction = NormalFacts();
        noAction.HasTimingAction = false;
        var na = UsDiagnosticsProjection.BuildGateChain(noAction, Tr);
        Assert(na[3].State == UsDiagGateState.NA && na[3].Value == "US.Diagnostics.Value.NotApplicable",
            "G3 Plan renders N/A as the fourth state, never a green Pass (round-9 NA ruling)");
        Assert(na[6].State == UsDiagGateState.NA, "G7 Scope match is N/A without an action");
        Assert(na[15].State == UsDiagGateState.NA, "G16 without evaluation is N/A");

        var notGlobal = NormalFacts();
        notGlobal.GlobalApplicable = false;
        Assert(UsDiagnosticsProjection.BuildGateChain(notGlobal, Tr)[10].State == UsDiagGateState.NA,
            "G11 Global cooldown is N/A (not a fake Pass) when the action ignores it");

        // G4 identity: N/A off external plans; Pass on all-true; Blocked names the failing conditions.
        var ext = NormalFacts();
        Assert(UsDiagnosticsProjection.BuildGateChain(ext, Tr)[4].State == UsDiagGateState.NA,
            "G4 is N/A for non-external trigger plans");
        ext.ExternalTriggerPlan = true;
        ext.PlayerControlled = ext.NotDowned = ext.Awake = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(ext, Tr)[4].State == UsDiagGateState.Pass,
            "G4 passes a valid external pawn");
        ext.NotDowned = false;
        ext.Awake = false;
        var blocked = UsDiagnosticsProjection.BuildGateChain(ext, Tr)[4];
        Assert(blocked.State == UsDiagGateState.Block
                && blocked.Value == "US.Diagnostics.Value.Blocked: " + "US.Diagnostics.Reason.Downed" + ", " + "US.Diagnostics.Reason.Asleep",
            "G4 Blocked must break down the failing conditions (round-9 enhancement), got '" + blocked.Value + "'");

        // G16 is pure tri-state: a dispatch reads Pass, never the audio string (attribution single-pointed).
        var dispatched = NormalFacts();
        dispatched.EvaluationBelongsToCurrentAction = true;
        dispatched.Dispatched = true;
        var g16 = UsDiagnosticsProjection.BuildGateChain(dispatched, Tr)[15];
        Assert(g16.State == UsDiagGateState.Pass && g16.Value == "US.Diagnostics.Value.Pass",
            "G16 keeps tri-state only; the pack:sound string belongs to the Current state row alone");
        var failed = NormalFacts();
        failed.EvaluationBelongsToCurrentAction = true;
        failed.PlaybackFailed = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(failed, Tr)[15].State == UsDiagGateState.Block,
            "G16 blocks a playback failure");
        var staleEv = NormalFacts();
        staleEv.EvaluationBelongsToCurrentAction = false;
        staleEv.Dispatched = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(staleEv, Tr)[15].State == UsDiagGateState.NA,
            "an evaluation from ANOTHER action must not light this chain (stale impersonation guard)");

        // G0 disabled bypass is a Block with the bypass truth; probability/talking stay Pending (blue).
        var disabled = NormalFacts();
        disabled.ModeDisabled = true;
        Assert(UsDiagnosticsProjection.BuildGateChain(disabled, Tr)[0].State == UsDiagGateState.Block, "G0 blocks when Disabled");
        Assert(UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr)[8].State == UsDiagGateState.Pending, "G9 probability is Pending (stochastic)");
        Assert(UsDiagnosticsProjection.BuildGateChain(NormalFacts(), Tr)[12].State == UsDiagGateState.Pending, "G13 talking is Pending (stochastic)");
    }

    private static void CooldownPairRules()
    {
        string tick = UsDiagnosticsProjection.FormatCooldownPair(300, null, 900, null, false);
        Assert(tick == "300t/900t", "remaining/total in ticks (round-9 timer ruling), got " + tick);
        string sec = UsDiagnosticsProjection.FormatCooldownPair(null, 5f, null, 15f, true);
        Assert(sec == "5.00s/15.00s", "remaining/total in seconds, got " + sec);
        string realtimeAsTicks = UsDiagnosticsProjection.FormatCooldownPair(null, 2.5f, null, 7.5f, false);
        Assert(realtimeAsTicks == "150t/450t", "realtime cooldown converts to ticks when t-mode shows, got " + realtimeAsTicks);
        Assert(UsDiagnosticsProjection.FormatCooldownPair(null, null, null, null, false) == "—",
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

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("UsDiagnosticsLogic: " + message);
    }
}
