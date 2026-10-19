using System;
using UniversalSqueaker.OneShot;

internal static partial class Program
{
    private static DecisionInput PublicStringInput(long tick)
    {
        const string key = "third.party/unregistered";
        return new DecisionInput
        {
            Subject = "p1", Domain = "Ratkin", Tick = tick,
            Actions = new[] { new ActionPlan(key, 0, new Condition("all"), 120) },
            Audio = new[] { new AudioCandidate("external", "audio/External", key, "Ratkin", 1) }
        };
    }

    // S4 explicitly retires S3's old Realtime/probability adaptation, not the public string ability.
    private static void CompatibilityUsesSameOwnerAndOriginalClock()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        foreach (bool externalFirst in new[] { false, true })
        {
            foreach (var receipt in (PlaybackResult[])Enum.GetValues(typeof(PlaybackResult)))
            {
                var state = new DecisionState(1000);
                var driver = new Driver { Result = receipt };
                var registered = Input();
                registered.StateOpportunity = false;
                var external = PublicStringInput(1000);
                var first = engine.Run(state, policy, externalFirst ? external : registered, driver, driver);
                var second = engine.Run(state, policy, externalFirst ? registered : external, driver, driver);
                Check(first.Receipt?.Result == receipt && second.Reason == "TickOccupied" && driver.Calls == 1,
                    "S4 public/17 same-tick quota in both orders retains reservation for every receipt");
            }
        }
        var shared = new DecisionState(1000);
        var d = new Driver();
        engine.Run(shared, policy, PublicStringInput(1000), d, d);
        var nextRegistered = Input(1001);
        nextRegistered.StateOpportunity = false;
        Check(engine.Run(shared, policy, nextRegistered, d, d).Reason == "HardDeadline" && d.Calls == 1,
            "S4 public string writes the same candidate hard protection as registered events");
        Check(engine.Run(shared, policy, PublicStringInput(1119), d, d).Reason == "HardDeadline" && d.Calls == 1,
            "S4 public string retains its action deadline without a separate Realtime clock");
        Check(engine.Run(shared, policy, PublicStringInput(1120), d, d).Receipt?.Result == PlaybackResult.Dispatched,
            "S4 exact public-string tick boundary re-enters one owner, no tick-to-seconds estimate");
        var reverse = new DecisionState(1000);
        var r = new Driver();
        var registeredFirst = Input();
        registeredFirst.StateOpportunity = false;
        engine.Run(reverse, policy, registeredFirst, r, r);
        Check(engine.Run(reverse, policy, PublicStringInput(1001), r, r).Reason == "HardDeadline" && r.Calls == 1,
            "S4 registered shared hard protection covers valid public strings");
    }
}
