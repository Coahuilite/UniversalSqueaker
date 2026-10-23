using UniversalSqueaker.Kernel;
using System;
using System.Collections.Generic;
using System.Text;
using UniversalSqueaker.Responses;

internal static partial class Program
{
    private static string S4S5Replay()
    {
        var transcript = new StringBuilder();
        foreach (var result in (PlaybackResult[])Enum.GetValues(typeof(PlaybackResult)))
        {
            var input = Input();
            input.Activity = 2;
            input.Modulation = new ModulationPlan
            {
                EnabledRules = new[] { "us.core/Age", "us.core/Age" },
                Context = new ResponseContext("p1", new Dictionary<string, double>
                    { ["age.native"] = 1.2, ["age.nativeVolume"] = .8 }),
                JitterMin = .9, JitterMax = 1.1
            };
            var state = new DecisionState(1000);
            var driver = new Driver { Result = result };
            var engine = new ResponseDecision();
            var policy = new StatePolicy(40, 20);
            var decision = engine.Run(state, policy, input, driver, driver);
            transcript.Append("S4 t=1000 facts=Work A=2 nativeAge=1.2/.8 jitter=.9..1.1 chosen=").Append(decision.Action)
                .Append(" receipt=").Append(decision.Receipt?.Result).Append(" roll=").Append(string.Join(",", driver.Purposes))
                .Append(" local=").Append(driver.LastRequest?.Pitch).Append('/').Append(driver.LastRequest?.Audio.Volume)
                .Append(" calls=").Append(driver.Calls).Append(" confirmed=").Append(state.ConfirmedTick)
                .Append(" H=").Append(state.SharedDeadline).Append('/').Append(state.ActionDeadline("us.core/Work"))
                .Append(" W=").Append(state.NextStateTick).AppendLine();
            input.Activity = .5;
            input.Tick = 1001;
            int rolls = driver.Rolls;
            var next = engine.Run(state, policy, input, driver, driver);
            transcript.Append("S4 t=1001 A=.5 reason=").Append(next.Reason).Append(" extraRolls=").Append(driver.Rolls - rolls)
                .Append(" W=").Append(state.NextStateTick).AppendLine();
        }
        var slot = new SustainedPlayback();
        var old = new Handle();
        var start = slot.Dispatch(SustainedRequest("us.core/Work", 10), true,
            () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Dispatched), old));
        var same = slot.Dispatch(SustainedRequest("us.core/Work", 10, 1001), true, () => throw new Exception("must not start"));
        var lower = slot.Dispatch(SustainedRequest("us.core/Call", 0, 1002), true, () => throw new Exception("must not start"));
        var replace = slot.Dispatch(SustainedRequest("us.core/Wounded", 20, 1003), true,
            () => new SustainedStart(new PlaybackReceipt(PlaybackResult.Indeterminate, "NativeStartupUnconfirmed")));
        transcript.Append("S5 start=").Append(start.Result).Append(" same=").Append(same.Result).Append(" low=").Append(lower.Result)
            .Append(" replacement=").Append(replace.Result).Append(" oldEnds=").Append(old.Ends)
            .Append(" active=").Append(slot.Active).Append(" reason=").Append(slot.LastEndReason).AppendLine();
        return transcript.ToString();
    }
}
