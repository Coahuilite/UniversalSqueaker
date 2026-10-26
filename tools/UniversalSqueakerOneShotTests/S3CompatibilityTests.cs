using UniversalSqueaker.Kernel;
using System;
using UniversalSqueaker.Authoring;
using System.Collections.Generic;
using UniversalSqueaker.Responses;

internal static partial class Program
{
    private static DecisionInput DeclaredNotificationInput(long tick)
    {
        const string key = "third.party/Finished";
        var definitions = new DefinitionCatalog(120, 120);
        var contract = new NotificationContract(key, 120);
        if (!definitions.DeclareNotification(contract)) throw new Exception("declaration rejected");
        return new DecisionInput
        {
            Subject = "p1", Domain = "Ratkin", Tick = tick, EventFact = contract.FactId,
            Actions = System.Array.FindAll(definitions.Actions, x => x.Key == key),
            Facts = definitions.Capture("p1", new Dictionary<string, Truth> { [contract.FactId] = Truth.True }),
            AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("external", "audio/External", key, "Ratkin", 1) })};
    }

    // r2 requires explicit declaration; preserve both orders, all four receipts and the original shared/action tick boundaries.
    private static void CompatibilityUsesSameOwnerAndOriginalClock()
    {
        var engine = new ResponseDecision();
        var policy = new StatePolicy(40, 20);
        foreach (bool externalFirst in new[] { false, true })
        {
            foreach (var receipt in (PlaybackResult[])Enum.GetValues(typeof(PlaybackResult)))
            {
                var state = new DecisionState(1000);
                var driver = new Driver { Result = receipt };
                var registered = Input(state: false);
                var external = DeclaredNotificationInput(1000);
                var first = engine.Run(state, policy, externalFirst ? external : registered, driver, driver);
                var second = engine.Run(state, policy, externalFirst ? registered : external, driver, driver);
                Check(first.Receipt?.Result == receipt && second.Reason == "TickOccupied" && driver.Calls == 1,
                    "S4 declared/official same-tick quota in both orders retains reservation for every receipt");
            }
        }
        var shared = new DecisionState(1000);
        var d = new Driver();
        engine.Run(shared, policy, DeclaredNotificationInput(1000), d, d);
        var nextRegistered = Input(1001, state: false);
        Check(engine.Run(shared, policy, nextRegistered, d, d).Reason == "HardDeadline" && d.Calls == 1,
            "S4 declared notification writes the same candidate hard protection as registered events");
        Check(engine.Run(shared, policy, DeclaredNotificationInput(1119), d, d).Reason == "HardDeadline" && d.Calls == 1,
            "S4 declared notification retains its action deadline without a separate Realtime clock");
        Check(engine.Run(shared, policy, DeclaredNotificationInput(1120), d, d).Receipt?.Result == PlaybackResult.Dispatched,
            "S4 exact public-string tick boundary re-enters one owner, no tick-to-seconds estimate");
        var reverse = new DecisionState(1000);
        var r = new Driver();
        var registeredFirst = Input(state: false);
        engine.Run(reverse, policy, registeredFirst, r, r);
        Check(engine.Run(reverse, policy, DeclaredNotificationInput(1001), r, r).Reason == "HardDeadline" && r.Calls == 1,
            "S4 registered shared hard protection covers valid declared notifications");
    }
}
