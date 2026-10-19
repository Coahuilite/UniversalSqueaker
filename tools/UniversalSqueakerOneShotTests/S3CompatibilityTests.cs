using System;
using UniversalSqueaker;
using UniversalSqueaker.OneShot;

internal static partial class Program
{
    private static DecisionInput CompatibilityInput(long tick, float realtime, SqueakCooldownClock clock = SqueakCooldownClock.Realtime)
    {
        const string key = "third.party/unregistered";
        var metadata = new SqueakActionDefinition(SqueakAction.Call, key, key, "", SqueakVocalGatePolicy.ApplyTalkingGate,
            SqueakActionScopeSupport.AnyOccurrence, SqueakActionScope.AnyOccurrence);
        return new DecisionInput { Subject = "p1", Domain = "Ratkin", Tick = tick, Realtime = realtime,
            Actions = new[] { new ActionPlan(key, 0, new Condition("all"), 0) },
            Audio = new[] { new AudioCandidate("legacy", "audio/Legacy", key, "Ratkin", 1) },
            CompatibilityTiming = new LegacyTimingParameters { Plan = new SqueakActionPlan(metadata, true, SqueakTriggerMode.RandomOneShot,
                120, .02f, false, clock), MasterMultiplier = 2, GlobalBaseTicks = 20, ScaleWithTimeSpeed = true, SpeedMultiplier = 3 } };
    }

    private static void CompatibilityUsesSameOwnerAndOriginalClock()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        foreach (bool compatibilityFirst in new[] { false, true })
        {
            foreach (var receipt in (PlaybackResult[])Enum.GetValues(typeof(PlaybackResult)))
            {
                var state = new DecisionState(1000);
                var driver = new Driver { Result = receipt };
                var modern = Input(); modern.StateOpportunity = false; modern.Realtime = 100;
                var legacy = CompatibilityInput(1000, 100);
                var first = engine.Run(state, policy, compatibilityFirst ? legacy : modern, driver, driver);
                var second = engine.Run(state, policy, compatibilityFirst ? modern : legacy, driver, driver);
                Check(first.Receipt?.Result == receipt && second.Reason == "TickOccupied" && driver.Calls == 1,
                    "mixed legacy/17 same-tick quota in both orders, all receipts including rejection retain slot");
            }
        }
        var shared = new DecisionState(1000);
        var d = new Driver();
        engine.Run(shared, policy, CompatibilityInput(1000, 100), d, d);
        var nextModern = Input(1040); nextModern.StateOpportunity = false;
        Check(engine.Run(shared, policy, nextModern, d, d).Reason == "HardDeadline" && d.Calls == 1,
            "legacy computed shared 20*2*3 protects modern until 1120, not fixture H40");
        var nextLegacy = CompatibilityInput(2000, 100.1f);
        int rolls = d.Rolls;
        Check(engine.Run(shared, policy, nextLegacy, d, d).Reason == "LegacyClockDeadline" && d.Calls == 1 && d.Rolls == rolls,
            "large tick jump with only .1 seconds real time cannot satisfy realtime action interval, zero extra rolls");
        nextLegacy = CompatibilityInput(2001, 104);
        Check(engine.Run(shared, policy, nextLegacy, d, d).Receipt?.Result == PlaybackResult.Dispatched,
            "exact realtime boundary 120 ticks/60 * master2 is four real seconds, unchanged old formula");
        var paused = new DecisionState(1000);
        var p = new Driver();
        engine.Run(paused, policy, CompatibilityInput(1000, 100), p, p);
        var pausedInput = CompatibilityInput(1000, 999);
        Check(engine.Run(paused, policy, pausedInput, p, p).Reason == "TickOccupied" && p.Calls == 1,
            "elapsed realtime during pause does not defeat same game tick quota");
        var modernShared = new DecisionState(1000);
        var m = new Driver();
        var initialModern = Input(); initialModern.StateOpportunity = false; initialModern.Realtime = 100;
        engine.Run(modernShared, policy, initialModern, m, m);
        Check(engine.Run(modernShared, policy, CompatibilityInput(1001, 101), m, m).Reason == "HardDeadline" && m.Calls == 1,
            "modern shared hard protection applies to old unregistered string too");
        var seeded = new DecisionState(1000);
        seeded.SeedLegacy(0, new[] { new System.Collections.Generic.KeyValuePair<string, LegacyClockRecord>("third.party/unregistered", new LegacyClockRecord(0, 99)) });
        var seedDriver = new Driver();
        Check(engine.Run(seeded, policy, CompatibilityInput(1000, 100), seedDriver, seedDriver).Reason == "LegacyClockDeadline" && seedDriver.Calls == 0,
            "original last realtime record projects into same decision owner at takeover");
    }
}
