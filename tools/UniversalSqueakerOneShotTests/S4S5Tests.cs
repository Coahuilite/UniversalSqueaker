using System;
using System.Collections.Generic;
using UniversalSqueaker.OneShot;

internal static partial class Program
{
    private static void S4RhythmAndResponses()
    {
        var state = new DecisionState(1000);
        var input = Input(); input.Activity = 2;
        var driver = new Driver();
        new OneShotDecision().Run(state, new StatePolicy(40, 20), input, driver, driver);
        Check(state.SharedDeadline == 1040 && state.ActionDeadline("us.core/Work") == 1120 && state.NextStateTick == 1050,
            "S4 A2 divides W20 once, never hard40/action120");
        input.Tick = 1041; input.Activity = .5;
        Check(new OneShotDecision().Run(state, new StatePolicy(40, 20), input, driver, driver).Reason == "NotDue" && state.NextStateTick == 1050,
            "S4 changing A does not redraw or rescale an already arranged wait");
        var eventInput = Input(1000, false);
        eventInput.Actions = new[] { new ActionPlan("us.core/Work", 1, new Condition("all"), 120, chance: .25) };
        eventInput.Activity = 4;
        var saturated = new Driver();
        Check(new OneShotDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), eventInput, saturated, saturated).Receipt?.Result == PlaybackResult.Dispatched
            && !saturated.Purposes.Contains(RandomPurpose.Eligibility), "S4 ordinary event p_base*A saturates once, no invented event/extra roll");
        eventInput.Activity = .1;
        var low = new Driver();
        Check(new OneShotDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), eventInput, low, low).Reason == "ChanceRejected" && low.Calls == 0,
            "S4 event activity is probability, not a queued wait");

        var values = new Dictionary<string, double> { ["age.native"] = 1.2, ["age.override"] = 1.5,
            ["mood.level"] = .9, ["mood.low"] = .3, ["mood.high"] = .7,
            ["mood.high.pitch"] = .8, ["mood.high.volume"] = .5 };
        var plan = new ModulationPlan { Context = new ResponseContext("p1", values), EnabledRules = new[] { "us.core/Age", "us.core/Mood", "us.core/Age" }, JitterMin = .9, JitterMax = 1.1 };
        var random = new Driver();
        var composed = ResponseModulation.Compose(2, 3, plan, new Dictionary<string, bool>(), random);
        Check(composed.Valid && Math.Abs(composed.Pitch - 2.208) < .000001 && composed.Volume == 1.5
            && random.Purposes.Count == 1 && random.Purposes[0] == RandomPurpose.ModulationJitter,
            "S4 calibration then explicit-age (not native twice) then mood then one jitter, duplicate rule reference once");
        var skip = ResponseModulation.Compose(2, 3, plan, new Dictionary<string, bool> { ["us.core/Age"] = false }, new Driver());
        Check(skip.Valid && Math.Abs(skip.Pitch - 1.472) < .000001, "S4 entry Skip age does not skip independent mood");
        plan.PlayerParticipation = new Dictionary<string, bool> { ["us.core/Age"] = true };
        var player = ResponseModulation.Compose(2, 3, plan, new Dictionary<string, bool> { ["us.core/Age"] = false }, new Driver());
        Check(player.Valid && player.Pitch == composed.Pitch, "S4 explicit player participation overrides entry Skip once");
        plan.EnabledRules = Array.Empty<string>(); plan.JitterMin = plan.JitterMax = 1;
        var disabled = ResponseModulation.Compose(2, 3, plan, new Dictionary<string, bool>(), new Driver());
        Check(disabled.Valid && disabled.Pitch == 2 && disabled.Volume == 3, "S4 present parameters/registered rules/Apply never enable a response");
        plan.EnabledRules = new[] { "us.core/Mood" }; plan.Context = new ResponseContext("p1", new Dictionary<string, double> { ["mood.level"] = .9 });
        Check(!ResponseModulation.Compose(1, 1, plan, new Dictionary<string, bool>(), new Driver()).Valid,
            "S4 enabled mood without explicit thresholds is rejected, not styled by hidden defaults");
    }

    private sealed class Handle : ISustainedHandle
    {
        public bool Active { get; private set; } = true;
        public int Maintains, Ends;
        public void Maintain() { Maintains++; }
        public void End() { Ends++; Active = false; }
    }
    private static PlaybackRequest SustainedRequest(string key, int priority, long tick = 1000)
        => new("p1", "Ratkin", key, new AudioCandidate("entry", "audio/Work", key, "Ratkin", 1), tick,
            sustained: true, priority: priority, condition: new Condition("ref", "state"));

    private static void S5SustainedOwner()
    {
        var slot = new SustainedPlayback();
        var handle = new Handle();
        Check(slot.Dispatch(SustainedRequest("Work", 10), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched), handle)).Result == PlaybackResult.Dispatched,
            "S5 valid new handle confirms sustained dispatch");
        int starts = 0;
        Check(slot.Dispatch(SustainedRequest("Work", 10, 1001), true, () => { starts++; return default; }).Result == PlaybackResult.AlreadyActive
            && starts == 0 && handle.Maintains == 1, "S5 same-key maintenance does not create a new instance");
        Check(slot.Dispatch(SustainedRequest("Call", 0), true, () => { starts++; return default; }).Result == PlaybackResult.Rejected
            && handle.Ends == 0, "S5 lower/equal request cannot end active slot");
        Check(slot.Dispatch(SustainedRequest("Wounded", 20), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Rejected, "prepared start rejected"))).Result == PlaybackResult.Rejected
            && handle.Ends == 1 && !slot.Active, "S5 higher priority ends old only after preparation boundary, rejected new start does not restore old");
        slot.Stop("again"); Check(handle.Ends == 1, "S5 owner never Ends an already-ended handle twice");
        var unknown = new Handle();
        slot.Dispatch(SustainedRequest("Work", 10), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched), unknown));
        Check(!slot.ReviewDue(1059) && slot.ReviewDue(1060), "S5 independent low-frequency active review, no per-tick contract polling");
        Check(slot.Review(1060, new FactSnapshot(new Dictionary<string, Truth>())) == "StateReviewUnknown" && unknown.Ends == 1,
            "S5 unknown active basis ends own slot rather than grace/retry");
        Check(!slot.ReviewDue(2000), "S5 no instance means no review work");
        Check(slot.Dispatch(SustainedRequest("Work", 10), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched))).Result == PlaybackResult.Indeterminate,
            "S5 no effective handle after possible startup side effect is indeterminate, never confirmed");
        Check(slot.Dispatch(SustainedRequest("Work", 10), false, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched))).Result == PlaybackResult.Dispatched
            && !slot.Active, "S5 retained non-sustain material downgrade uses one prepared request and does not invent a handle");
    }
}
