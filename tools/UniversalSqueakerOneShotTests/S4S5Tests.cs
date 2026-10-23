using UniversalSqueaker.Kernel;
using System;
using System.Collections.Generic;
using UniversalSqueaker.Responses;

internal static partial class Program
{
    private static void S4RhythmAndResponses()
    {
        string firstReplay = S4S5Replay();
        Check(firstReplay == S4S5Replay(), "S4/S5 independent fixed facts/ticks/tagged-rolls/receipts/parameters/state double replay");
        Console.Write(firstReplay);
        try
        {
            S4AuthorResponses();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("S4 author response fixture failure: " + error.GetType().Name + ":" + error.Message);
            throw;
        }
        var state = new DecisionState(1000);
        var input = Input(); input.Activity = 2;
        var driver = new Driver();
        new ResponseDecision().Run(state, new StatePolicy(40, 20), input, driver, driver);
        Check(state.SharedDeadline == 1040 && state.ActionDeadline("us.core/Work") == 1120 && state.NextStateTick == 1050,
            "S4 A2 divides W20 once, never hard40/action120");
        var ratio = Input();
        ratio.Activity = ratio.StateWaitFactor = 1e308;
        var ratioState = new DecisionState(1000);
        var ratioDriver = new Driver();
        new ResponseDecision().Run(ratioState, new StatePolicy(40, 100), ratio, ratioDriver, ratioDriver);
        Check(ratioState.NextStateTick == 1140, "S4 finite neutral factor ratio does not overflow intermediate wait multiplication");
        input.Tick = 1041; input.Activity = .5;
        Check(new ResponseDecision().Run(state, new StatePolicy(40, 20), input, driver, driver).Reason == "NotDue" && state.NextStateTick == 1050,
            "S4 changing A does not redraw or rescale an already arranged wait");
        var eventInput = Input(1000, false);
        eventInput.Actions = new[] { new ActionPlan("us.core/Work", 1, new Condition("all"), 120, chance: .25) };
        eventInput.Activity = 4;
        eventInput.StateWaitFactor = 100;
        var saturated = new Driver();
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), eventInput, saturated, saturated).Receipt?.Result == PlaybackResult.Dispatched
            && !saturated.Purposes.Contains(RandomPurpose.Eligibility), "S4 ordinary event p_base*A saturates once; state-wait factor cannot add another event probability scaling");
        eventInput.Activity = .1;
        var low = new Driver();
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), eventInput, low, low).Reason == "ChanceRejected" && low.Calls == 0,
            "S4 event activity is probability, not a queued wait");

        var values = new Dictionary<string, double> { ["age.native"] = 1.2, ["age.nativeVolume"] = 1, ["age.override"] = 1.5,
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
        var age = new NativeAgeResponse();
        var pitchOnly = age.Evaluate(new ResponseContext("p1", new Dictionary<string, double>
            { ["age.native"] = 1.2, ["age.nativeVolume"] = .7, ["age.override"] = 1.5 }));
        var volumeOnly = age.Evaluate(new ResponseContext("p1", new Dictionary<string, double>
            { ["age.native"] = 1.2, ["age.nativeVolume"] = .7, ["age.overrideVolume"] = .5 }));
        Check(pitchOnly.Pitch == 1.5 && pitchOnly.Volume == .7 && volumeOnly.Pitch == 1.2 && volumeOnly.Volume == .5,
            "S4 explicit age override substitutes only corresponding axis, preserving other native axis");
        var infinite = new ModulationPlan { JitterMax = double.PositiveInfinity };
        var noRoll = new Driver();
        Check(!ResponseModulation.Compose(1, 1, infinite, new Dictionary<string, bool>(), noRoll).Valid && noRoll.Rolls == 0,
            "S4 nonfinite jitter boundary is rejected before consuming jitter roll");
        plan.EnabledRules = Array.Empty<string>(); plan.JitterMin = plan.JitterMax = 1;
        var disabled = ResponseModulation.Compose(2, 3, plan, new Dictionary<string, bool>(), new Driver());
        Check(disabled.Valid && disabled.Pitch == 2 && disabled.Volume == 3, "S4 present parameters/registered rules/Apply never enable a response");
        plan.EnabledRules = new[] { "us.core/Mood" }; plan.Context = new ResponseContext("p1", new Dictionary<string, double> { ["mood.level"] = .9 });
        Check(!ResponseModulation.Compose(1, 1, plan, new Dictionary<string, bool>(), new Driver()).Valid,
            "S4 enabled mood without explicit thresholds is rejected, not styled by hidden defaults");
        foreach (string key in new[] { "mood.level", "mood.low", "mood.high" })
        {
            var invalid = new Dictionary<string, double>(values) { [key] = double.NaN };
            plan.Context = new ResponseContext("p1", invalid);
            Check(!ResponseModulation.Compose(1, 1, plan, new Dictionary<string, bool>(), new Driver()).Valid,
                "S4 enabled mood rejects nonfinite fact " + key);
        }
        var activeInput = Input();
        activeInput.Actions = new[] { new ActionPlan("us.core/Work", 1, new Condition("all"), 120, sustained: true, requiresVocal: true) };
        activeInput.ActiveSustainedKey = "us.core/Work";
        activeInput.BlockedActions.Add("us.core/Work");
        var activeDriver = new Driver();
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), activeInput, activeDriver, activeDriver).Reason == "ActionOrScopeRejected",
            "S5 same-key active slot cannot bypass revoked source or permission");
        activeInput.BlockedActions.Clear();
        activeInput.HasVocalOrgans = false;
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), activeInput, activeDriver, activeDriver).Reason == "NoVocalOrgans"
            && activeDriver.Calls == 0 && activeDriver.Purposes.Count == 0,
            "S5 same-key active slot requires current vocal qualification without redrawing maintenance");
    }

    private sealed class Handle : ISustainedHandle
    {
        public bool Active { get; private set; } = true;
        public int Maintains, Ends;
        public string EndFault = "";
        public void Maintain() { Maintains++; }
        public void End()
        {
            Ends++;
            if (EndFault == "before") throw new InvalidOperationException("before native end");
            Active = false;
            if (EndFault == "after") throw new InvalidOperationException("after native end");
        }
    }
    private static PlaybackRequest SustainedRequest(string key, int priority, long tick = 1000)
        => new("p1", "Ratkin", key, new AudioCandidate("entry", "audio/Work", key, "Ratkin", 1), tick,
            sustained: true, priority: priority, condition: new Condition("ref", "state"));

    private static void S5SustainedOwner()
    {
        foreach (string phase in new[] { "before", "after" })
        {
            var failedSlot = new SustainedPlayback();
            var failedHandle = new Handle { EndFault = phase };
            failedSlot.Dispatch(SustainedRequest("us.core/Work", 10), true,
                () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched), failedHandle));
            int replacementCalls = 0;
            var result = failedSlot.Dispatch(SustainedRequest("us.core/Wounded", 20), true, () =>
            {
                replacementCalls++;
                return new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched), new Handle());
            });
            Check(result.Result == PlaybackResult.Rejected && replacementCalls == 0 && failedHandle.Ends == 1
                && failedSlot.LastEndReason.Contains(":EndFault:") && !failedSlot.Active,
                "S5 old End fault " + phase + " is separately recorded; new callback was not entered");
            failedSlot.Stop("lifecycle release");
            Check(failedHandle.Ends == 1, "S5 old End fault cannot be retried by later lifecycle release");
        }
        var slot = new SustainedPlayback();
        var handle = new Handle();
        Check(slot.Dispatch(SustainedRequest("us.core/Work", 10), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched), handle)).Result == PlaybackResult.Dispatched,
            "S5 valid new handle confirms sustained dispatch");
        int starts = 0;
        Check(slot.Dispatch(SustainedRequest("us.core/Work", 10, 1001), true, () => { starts++; return default; }).Result == PlaybackResult.AlreadyActive
            && starts == 0 && handle.Maintains == 1, "S5 same-key maintenance does not create a new instance");
        Check(slot.Dispatch(SustainedRequest("us.core/Call", 0), true, () => { starts++; return default; }).Result == PlaybackResult.Rejected
            && handle.Ends == 0, "S5 lower/equal request cannot end active slot");
        Check(slot.Dispatch(SustainedRequest("us.core/Wounded", 20), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Rejected, "prepared start rejected"))).Result == PlaybackResult.Rejected
            && handle.Ends == 1 && !slot.Active, "S5 higher priority ends old only after preparation boundary, rejected new start does not restore old");
        slot.Stop("again"); Check(handle.Ends == 1, "S5 owner never Ends an already-ended handle twice");
        var unknown = new Handle();
        slot.Dispatch(SustainedRequest("us.core/Work", 10), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched), unknown));
        Check(!slot.ReviewDue(1059) && slot.ReviewDue(1060), "S5 independent low-frequency active review, no per-tick contract polling");
        Check(slot.Review(1060, new FactSnapshot(new Dictionary<string, Truth>())) == "StateReviewUnknown" && unknown.Ends == 1,
            "S5 unknown active basis ends own slot rather than grace/retry");
        Check(!slot.ReviewDue(2000), "S5 no instance means no review work");
        Check(slot.Dispatch(SustainedRequest("us.core/Work", 10), true, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched))).Result == PlaybackResult.Indeterminate,
            "S5 no effective handle after possible startup side effect is indeterminate, never confirmed");
        Check(slot.Dispatch(SustainedRequest("us.core/Work", 10), false, () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched))).Result == PlaybackResult.Dispatched
            && !slot.Active, "S5 retained non-sustain material downgrade uses one prepared request and does not invent a handle");
    }
}
