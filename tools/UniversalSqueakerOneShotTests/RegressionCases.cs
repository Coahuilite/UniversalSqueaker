using UniversalSqueaker.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Responses;
using UniversalSqueaker.Authoring;

internal static partial class Program
{
    private static void ExactTimingFixtures()
    {
        var engine = new ResponseDecision();
        var policy = new StatePolicy(40, 20);
        var t05 = new DecisionState(1000)
        {
            SharedDeadline = 1050
        };
        var blocked = new Driver();
        var outcome = engine.Run(t05, policy, Input(), blocked, blocked);
        Check(outcome.Reason == "HardDeadline", "T05 original due opportunity reaches hard gate");
        Check(t05.NextStateTick == 1060 && t05.SharedDeadline == 1050, "T05 original max(1000+60,1050)=1060");
        Check(t05.ConfirmedTick == null && t05.ActionDeadline("us.core/Work") == long.MinValue, "T05 no confirmation or action protection");
        Check(blocked.Calls == 0 && blocked.Rolls == 0, "T05 no backend or random");
        var fresh = engine.Run(t05, policy, Input(1060, true, false, false, true), blocked, blocked);
        Check(fresh.Action == "us.core/Call" && blocked.Calls == 1 && t05.ConfirmedTick == 1060,
            "T05 retry reads changed facts rather than replaying Work");
        CheckEventAfterRejectedState(engine, policy, 1010, 1050, 1060, 1130, "T06 original");
        CheckEventAfterRejectedState(engine, policy, 1050, 1090, 1090, 1170, "T06 delayed");
    }

    private static void CheckEventAfterRejectedState(ResponseDecision engine, StatePolicy policy, long eventTick, long shared, long next, long actionDeadline, string label)
    {
        var state = new DecisionState(1000);
        var rejected = new Driver
        {
            Result = PlaybackResult.Rejected
        };
        engine.Run(state, policy, Input(), rejected, rejected);
        Check(state.NextStateTick == 1060 && state.SharedDeadline == 0 && state.ConfirmedTick == null, label + " independent T02 starting state");
        var dispatched = new Driver();
        engine.Run(state, policy, Input(eventTick, false, false, true), dispatched, dispatched);
        Check(state.SharedDeadline == shared && state.ActionDeadline("us.core/Wounded") == actionDeadline, label + " hard deadlines");
        Check(state.ConfirmedTick == eventTick && state.ConfirmedAction == "us.core/Wounded", label + " confirmed dispatch");
        Check(state.NextStateTick == next && dispatched.Calls == 1 && dispatched.Rolls == 0, label + " max(existing opportunity,H), no appended W or new random");
        engine.Run(state, policy, Input(next - 1), dispatched, dispatched);
        Check(dispatched.Calls == 1 && state.NextStateTick == next, label + " not due leaves deadline unchanged");
    }

    private static void SameTickGateFailures()
    {
        var engine = new ResponseDecision();
        var policy = new StatePolicy(40, 20);
        var scopeState = new DecisionState(1000);
        var blocked = Input();
        blocked.BlockedActions.Add("us.core/Work");
        var driver = new Driver();
        engine.Run(scopeState, policy, blocked, driver, driver);
        var later = engine.Run(scopeState, policy, Input(1000, false, false, true), driver, driver);
        Check(later.Reason == "TickOccupied" && driver.Calls == 0 && driver.Rolls == 0, "scope failure after selection does not refund same-tick event");
        var hardState = new DecisionState(1000);
        hardState.Protect(Input().Actions.Single(x => x.Key == "us.core/Work"), new DecisionInput { Tick = 990 }, policy);
        // Isolate the Work hard gate: Wounded has no action deadline and shared is already elapsed.
        hardState.SharedDeadline = 0;
        var hard = engine.Run(hardState, policy, Input(), driver, driver);
        later = engine.Run(hardState, policy, Input(1000, false, false, true), driver, driver);
        Check(hard.Reason == "HardDeadline" && later.Reason == "TickOccupied" && driver.Calls == 0, "action hard failure after selection does not refund same-tick event");
        var notDue = new DecisionState(1060);
        engine.Run(notDue, policy, Input(), driver, driver);
        var eventResult = engine.Run(notDue, policy, Input(1000, false, false, true), driver, driver);
        Check(eventResult.Action == "us.core/Wounded" && driver.Calls == 1, "not-yet-due state opportunity does not reserve an attempt");
        bool chanceNamed = false;
        try
        {
            new ActionPlan("bad", 0, new Condition("all"), 1, chance: -1);
        }
        catch (ArgumentOutOfRangeException error)
        {
            chanceNamed = error.ParamName == "chance";
        }

        Check(chanceNamed, "invalid chance identifies chance parameter");
    }

    private sealed class SampleFact : IUsFactSource
    {
        private readonly string key;
        public int Reads;
        public int Captures;
        public bool FailKey;
        public bool ChangeKey;
        public bool FailCapture;
        public SampleFact(string key)
        {
            this.key = key;
        }

        public string Key
        {
            get
            {
                Reads++;
                if (FailKey)
                {
                    throw new InvalidOperationException("key failure");
                }

                if (ChangeKey && Reads > 1)
                {
                    throw new InvalidOperationException("key read twice");
                }

                return key;
            }
        }

        public Truth Capture(string subject)
        {
            Captures++;
            if (FailCapture)
            {
                throw new InvalidOperationException("capture failure");
            }

            return Truth.True;
        }
    }

    private sealed class FactProvider : IUsDefinitionProvider
    {
        private readonly IUsFactSource[] facts;
        public FactProvider(params IUsFactSource[] facts)
        {
            this.facts = facts;
        }

        public void Collect(ProviderBatch batch)
        {
            foreach (var fact in facts)
            {
                batch.AddFactSource(fact);
            }
        }
    }

    private static void AuthorDiagnostics()
    {
        var catalog = new DefinitionCatalog(120, 120);
        var audioInputs = new AudioInputBuilder(catalog);
        var failed = new SampleFact("example/Failed")
        {
            FailCapture = true,
            ChangeKey = true
        };
        Check(catalog.AddProvider(new FactProvider(failed)), "faulting runtime fact registered once");
        var snapshot = catalog.Capture("p1", new Dictionary<string, Truth>());
        var input = Input();
        input.Facts = snapshot;
        input.Actions = new[]
        {
            new ActionPlan("example/Test", 1, new Condition("all", "", new Condition("ref", "example/Failed"), new Condition("ref", "example/Missing")), 120)
        };
        var driver = new Driver();
        var result = new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), input, driver, driver);
        Check(result.Reason == "UnknownFacts" && result.Diagnostics.Count == 2, "D1 unknown outcome retains both fact identities");
        Check(result.Diagnostics.Any(x => x.Key == "example/Failed" && x.Provider.Contains("FactProvider") && x.Reason.Contains("capture failure")), "D1 captured provider exception identity and reason");
        Check(result.Diagnostics.Any(x => x.Key == "example/Missing" && x.Reason == "MissingFact"), "D1 missing key diagnostic");
        Check(failed.Reads == 1 && failed.Captures == 1 && driver.Rolls == 0 && driver.Calls == 0, "D1 diagnostics never reread provider key, resample or draw");
    }

    private static void DiagnosticsSurviveChosenAction()
    {
        var catalog = new DefinitionCatalog(120, 120);
        var audioInputs = new AudioInputBuilder(catalog);
        var failed = new SampleFact("example/HighPriority") { FailCapture = true };
        Check(catalog.AddProvider(new FactProvider(failed)), "chosen-path diagnostic provider registered");
        var snapshot = catalog.Capture("p1", new Dictionary<string, Truth>
        {
            ["us.core/WorkState"] = Truth.True
        });
        foreach (string scenario in new[]
        {
            "Dispatched", "Rejected", "Scope", "HardDeadline", "NoVocal",
            "Talking", "Eligibility", "NoAudio", "PreparationFault", "BackendFault"
        })
        {
            var input = Input();
            input.Facts = snapshot;
            input.Actions = new[]
            {
                new ActionPlan("example/Higher", 30, new Condition("ref", "example/HighPriority"), 120),
                new ActionPlan("us.core/Work", 20, new Condition("ref", "us.core/WorkState"), 120,
                    chance: scenario == "Eligibility" ? 0 : 1, skipTalking: false, requiresVocal: true)
            };
            var state = new DecisionState(1000);
            var driver = new Driver
            {
                Result = scenario == "Rejected" ? PlaybackResult.Rejected : PlaybackResult.Dispatched,
                FailRandom = scenario == "PreparationFault",
                FailDispatch = scenario == "BackendFault"
            };
            switch (scenario)
            {
                case "Scope":
                    input.BlockedActions.Add("us.core/Work");
                    break;
                case "HardDeadline":
                    state.SharedDeadline = 1050;
                    break;
                case "NoVocal":
                    input.HasVocalOrgans = false;
                    break;
                case "Talking":
                    input.TalkingChance = 0;
                    break;
                case "PreparationFault":
                    input.TalkingChance = .5;
                    break;
                case "NoAudio":
                    input.AudioCatalog = new AudioCatalog(Array.Empty<AudioCandidate>());
                    break;
            }
            var outcome = new ResponseDecision().Run(state, new StatePolicy(40, 20), input, driver, driver);
            Check(outcome.Action == "us.core/Work", scenario + ": lower independent True action remains selected");
            Check(outcome.Diagnostics.Count == 1
                && outcome.Diagnostics[0].Key == "example/HighPriority"
                && outcome.Diagnostics[0].Provider.Contains("FactProvider")
                && outcome.Diagnostics[0].Reason.Contains("capture failure"),
                scenario + ": selected result retains higher Unknown cause");
            bool entersBackend = scenario == "Dispatched" || scenario == "Rejected" || scenario == "BackendFault";
            Check(driver.Calls == (entersBackend ? 1 : 0), scenario + ": dispatch boundary unchanged");
            Check(driver.Rolls == (scenario == "PreparationFault" ? 1 : 0),
                scenario + ": diagnostic forwarding adds no random draw");
            if (scenario == "Dispatched")
            {
                Check(outcome.Receipt?.Result == PlaybackResult.Dispatched && state.ConfirmedTick == 1000,
                    "higher Unknown does not prevent legitimate lower dispatch");
            }
            if (scenario == "Rejected")
            {
                Check(outcome.Receipt?.Result == PlaybackResult.Rejected && state.ConfirmedTick == null,
                    "lower backend rejection still carries original Unknown diagnosis");
            }
        }
        Check(failed.Reads == 1 && failed.Captures == 1,
            "all selected return paths use captured diagnostics without resampling");
    }
    private static void AuthorShapeAndRegistration()
    {
        string header = "<declaration><key>example/A</key><baseAction>us.core/Work</baseAction>";
        foreach (string condition in new[]
        {
            "<condition><ref>a</ref><ref>b</ref></condition>",
            "<condition><ref><ref>a</ref></ref></condition>",
            "<condition><all>ignored<ref>a</ref></all></condition>",
            "<condition><unknown/></condition>",
            "<condition><not><ref>a</ref><ref>b</ref></not></condition>",
            "<condition><ref unexpected='yes'>a</ref></condition>"
        }

        )
        {
            ExpectInvalidXml(() => new ActionDeclaration().LoadDataFromXmlCustom(Node(header + condition + "</declaration>")), "condition rejects malformed expression: " + condition);
        }

        ExpectInvalidXml(() => new ActionDeclaration().LoadDataFromXmlCustom(Node(header + "<key>example/B</key><condition><ref>a</ref></condition></declaration>")), "duplicate declaration field rejected");
        ExpectInvalidXml(() => new VoicePackDeclaration().LoadDataFromXmlCustom(Node("<declaration><race>Ratkin</race><entries><unexpected/></entries></declaration>")), "unknown entries element rejected");
        ExpectInvalidXml(() => new VoicePackDeclaration().LoadDataFromXmlCustom(Node("<declaration><race>Ratkin</race><unknown/></declaration>")), "unknown pack field rejected");
        var catalog = new DefinitionCatalog(120, 120);
        var audioInputs = new AudioInputBuilder(catalog);
        var retained = new SampleFact("example/Retained");
        Check(catalog.AddProvider(new FactProvider(retained)), "preexisting provider accepted");
        var first = new SampleFact("example/First");
        var invalid = new SampleFact("example/Invalid")
        {
            FailKey = true
        };
        Check(!catalog.AddProvider(new FactProvider(first, invalid)), "later registration key fault rejects full batch");
        var snapshot = catalog.Capture("p1", new Dictionary<string, Truth>());
        Check(snapshot.Read("example/Retained") == Truth.True && snapshot.Read("example/First") == Truth.Unknown && first.Captures == 0, "failed registration preserves old catalog without half batch");
        var changing = new SampleFact("example/Stable")
        {
            ChangeKey = true
        };
        Check(catalog.AddProvider(new FactProvider(changing)), "registered key captured only once");
        snapshot = catalog.Capture("p1", new Dictionary<string, Truth>());
        Check(snapshot.Read("example/Stable") == Truth.True && changing.Reads == 1, "capture uses registered identity");
        var baseCondition = new Condition("ref", "us.core/WorkState");
        var parent = new ActionDeclaration
        {
            Key = "example/Parent",
            BaseAction = "us.core/Work",
            Condition = baseCondition
        };
        var child = new ActionDeclaration
        {
            Key = "example/Child",
            BaseAction = "example/Parent",
            Condition = baseCondition
        };
        Check(!catalog.AddAction(child), "S1 custom base rejected before parent");
        Check(catalog.AddAction(parent) && catalog.AddAction(child), "DA legal custom parent accepted after its dependency is registered");
    }

    private static void ExpectInvalidXml(Action load, string label)
    {
        bool rejected = false;
        try
        {
            load();
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, label);
    }
}
