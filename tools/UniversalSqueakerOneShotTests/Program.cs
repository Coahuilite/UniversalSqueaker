using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UniversalSqueaker.OneShot;
using UniversalSqueaker.Authoring;

internal static partial class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        checks++;
        if (!value)
        {
            throw new Exception(label);
        }
    }

    private sealed class Driver : IOneShotRandom, IOneShotBackend
    {
        public PlaybackResult Result = PlaybackResult.Dispatched;
        public int Calls, Rolls;
        public double Value = .1;
        public readonly List<RandomPurpose> Purposes = new();
        public PlaybackRequest? LastRequest;
        public bool FailRandom;
        public bool FailDispatch;
        public PlaybackReceipt Dispatch(PlaybackRequest request)
        {
            LastRequest = request;
            Calls++;
            if (FailDispatch)
            {
                throw new InvalidOperationException("backend fault");
            }
            return new PlaybackReceipt(Result, request.Audio.AudioId);
        }

        public double Next(RandomPurpose purpose)
        {
            Rolls++;
            Purposes.Add(purpose);
            if (FailRandom)
            {
                throw new Exception("random");
            }

            return Value;
        }
    }

    private static DecisionInput Input(long tick = 1000, bool state = true, bool work = true, bool wounded = false, bool call = true)
    {
        var catalog = new AuthorCatalog(120, 120);
        return new DecisionInput
        {
            Subject = "p1",
            Domain = "Ratkin",
            Tick = tick,
            StateOpportunity = state,
            Actions = catalog.Actions,
            Facts = new FactSnapshot(new Dictionary<string, Truth>
            {
                ["us.core/WorkState"] = work ? Truth.True : Truth.False,
                ["us.core/WoundedEvent"] = wounded ? Truth.True : Truth.False,
                ["us.core/CallState"] = call ? Truth.True : Truth.False
            }),
            Audio = new[]
            {
                new AudioCandidate("a", "pack/Work", "us.core/Work", "Ratkin", 1),
                new AudioCandidate("b", "pack/Wounded", "us.core/Wounded", "Ratkin", 1),
                new AudioCandidate("c", "pack/Call", "us.core/Call", "Ratkin", 1)
            }
        };
    }

    private static void Main()
    {
        ResultSettlement();
        RetryAndEventSequences();
        AudioIsolation();
        ThreeValuedConditions();
        BackendBoundaries();
        NonPawnIdentity();
        ScopeAndLegacyInvocation();
        RandomTranscript();
        ExactTimingFixtures();
        SameTickGateFailures();
        AuthorDiagnostics();
        DiagnosticsSurviveChosenAction();
        AuthorShapeAndRegistration();
        AuthorCases();
        FileExamples();
        Console.WriteLine("OneShot/author production source checks PASS: " + checks);
        S2Resources();
        S3ContractsAndSources();
        S4RhythmAndResponses();
        S5SustainedOwner();
        Console.WriteLine("OneShot + S2 production source checks PASS: " + checks);
    }

    private static void ResultSettlement()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        foreach (var result in new[]
        {
            PlaybackResult.Dispatched,
            PlaybackResult.Rejected,
            PlaybackResult.Indeterminate,
            PlaybackResult.AlreadyActive
        })
        {
            var state = new DecisionState(1000);
            var d = new Driver
            {
                Result = result
            };
            var outcome = engine.Run(state, policy, Input(), d, d);
            Check(outcome.Action == "us.core/Work" && d.Calls == 1, "T01-04 chosen Work once");
            Check(state.NextStateTick == 1060, "T01-04 future opportunity");
            bool protects = result == PlaybackResult.Dispatched || result == PlaybackResult.Indeterminate;
            Check(state.SharedDeadline == (protects ? 1040 : 0), "T01-04 shared settlement");
            Check(state.ActionDeadline("us.core/Work") == (protects ? 1120 : long.MinValue), "T01-04 action settlement");
            Check(state.ConfirmedTick == (result == PlaybackResult.Dispatched ? (long?)1000 : null), "T01-04 confirmation");
        }
    }

    private static void RetryAndEventSequences()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        var rejected = new Driver
        {
            Result = PlaybackResult.Rejected
        };
        var continuous = new DecisionState(1000);
        engine.Run(continuous, policy, Input(), rejected, rejected);
        engine.Run(continuous, policy, Input(1059), rejected, rejected);
        Check(rejected.Calls == 1, "T02 no early retry");
        var eventDriver = new Driver();
        engine.Run(continuous, policy, Input(1050, false, false, true), eventDriver, eventDriver);
        Check(continuous.ConfirmedTick == 1050 && continuous.ConfirmedAction == "us.core/Wounded", "T06 delayed confirmation");
        Check(continuous.ActionDeadline("us.core/Wounded") == 1170 && continuous.SharedDeadline == 1090, "T06 delayed deadlines");
        Check(continuous.NextStateTick == 1090 && eventDriver.Rolls == 0, "T06 no added W/no reroll");
        engine.Run(continuous, policy, Input(1060), eventDriver, eventDriver);
        Check(eventDriver.Calls == 1, "T06 postponed opportunity remains not due");
        var early = new DecisionState(1000);
        var success = new Driver();
        engine.Run(early, policy, Input(), success, success);
        var blocked = new Driver();
        engine.Run(early, policy, Input(1010, false, false, true), blocked, blocked);
        Check(blocked.Calls == 0 && blocked.Rolls == 0, "T07 event hard protection");
        var both = new DecisionState(1000);
        var twice = new Driver
        {
            Result = PlaybackResult.Rejected
        };
        var chosen = engine.Run(both, policy, Input(1000, true, true, true), twice, twice);
        Check(chosen.Action == "us.core/Wounded", "T08 Wounded priority");
        engine.Run(both, policy, Input(1000, false, true, false), twice, twice);
        Check(twice.Calls == 1, "T08 no failure refund");
        var change = new DecisionState(1000);
        var changed = new Driver
        {
            Result = PlaybackResult.Rejected
        };
        engine.Run(change, policy, Input(), changed, changed);
        var newResult = engine.Run(change, policy, Input(1060, true, false, false, true), changed, changed);
        Check(newResult.Action == "us.core/Call", "T09 fresh facts");
    }

    private static void AudioIsolation()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        var missing = Input();
        missing.Audio = missing.Audio.Where(x => x.Action != "us.core/Work").ToArray();
        var none = new Driver();
        var noSound = engine.Run(new DecisionState(1000), policy, missing, none, none);
        Check(noSound.Reason == "NoBoundAudio" && none.Calls == 0, "T10 no cross-action fallback");
        var p2 = Input();
        p2.Domain = "Kiiro";
        var isolated = new Driver();
        engine.Run(new DecisionState(1000), policy, p2, isolated, isolated);
        Check(isolated.Calls == 0, "T12 domain isolation");
        var second = new Driver();
        engine.Run(new DecisionState(1000), policy, Input(), second, second);
        Check(second.Calls == 1, "T12 no shared object state");
    }

    private static void ThreeValuedConditions()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        Truth[] values =
        {
            Truth.False,
            Truth.True,
            Truth.Unknown
        };
        foreach (var a in values)
        {
            foreach (var b in values)
            {
                var facts = new FactSnapshot(new Dictionary<string, Truth> { { "a", a }, { "b", b } });
                Truth[,] allTable =
                {
                    { Truth.False, Truth.False, Truth.False },
                    { Truth.False, Truth.True, Truth.Unknown },
                    { Truth.False, Truth.Unknown, Truth.Unknown }
                };
                Truth all = allTable[(int)a, (int)b];
                Truth[,] anyTable =
                {
                    { Truth.False, Truth.True, Truth.Unknown },
                    { Truth.True, Truth.True, Truth.True },
                    { Truth.Unknown, Truth.True, Truth.Unknown }
                };
                Truth any = anyTable[(int)a, (int)b];
                Check(new Condition("all", "", new Condition("ref", "a"), new Condition("ref", "b")).Evaluate(facts) == all, "T11 All");
                Check(new Condition("any", "", new Condition("ref", "a"), new Condition("ref", "b")).Evaluate(facts) == any, "T11 Any");
                Check(new Condition("not", "", new Condition("ref", "a")).Evaluate(facts) == new[] { Truth.True, Truth.False, Truth.Unknown }[(int)a], "T11 Not");
            }
        }
    }

    private static void BackendBoundaries()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        var bad = Input();
        bad.TalkingChance = .5;
        var invalid = new Driver
        {
            FailRandom = true
        };
        var badState = new DecisionState(1000);
        var randomFailure = engine.Run(badState, policy, bad, invalid, invalid);
        Check(randomFailure.Receipt?.Result == PlaybackResult.Rejected && badState.SharedDeadline == 0 && badState.NextStateTick == 1060, "preparation exception not indeterminate");
        Check(OneShotDispatch.Execute(() => false, () => throw new Exception()).Result == PlaybackResult.Rejected, "backend preflight");
        Check(OneShotDispatch.Execute(() => true, () => throw new Exception()).Result == PlaybackResult.Indeterminate, "backend uncertain side effect");
        Check(OneShotDispatch.Execute(() => true, () =>
        {
        }, () => throw new Exception()).Result == PlaybackResult.Dispatched, "diagnostic cannot revoke");
    }

    private static void NonPawnIdentity()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        var nonPawn = Input();
        nonPawn.Family = SubjectFamily.NonPawn;
        nonPawn.TargetNamespace = "armor";
        nonPawn.Domain = "Ratkin";
        var nonPawnDriver = new Driver();
        engine.Run(new DecisionState(1000), policy, nonPawn, nonPawnDriver, nonPawnDriver);
        Check(nonPawnDriver.Calls == 0, "NonPawn never borrows Pawn pool with same target text");
        nonPawn.Audio = new[]
        {
            new AudioCandidate("armor-a", "armor/Sound", "us.core/Work", "Ratkin", 1, 1, SubjectFamily.NonPawn, "armor")
        };
        engine.Run(new DecisionState(1000), policy, nonPawn, nonPawnDriver, nonPawnDriver);
        Check(nonPawnDriver.Calls == 1, "neutral NonPawn namespace/target distinct protocol");
        nonPawn.TargetNamespace = "building";
        engine.Run(new DecisionState(1000), policy, nonPawn, nonPawnDriver, nonPawnDriver);
        Check(nonPawnDriver.Calls == 1, "distinct NonPawn namespaces do not borrow");
    }

    private static void ScopeAndLegacyInvocation()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        bool oversized = false;
        try
        {
            new AudioCandidate("x", "p/a", "Work", "Ratkin", 1, 1e300);
        }
        catch (ArgumentException)
        {
            oversized = true;
        }

        Check(oversized, "finite native local gain required");
        var qualified = Input();
        qualified.BlockedActions.Add("us.core/Work");
        var scoped = new Driver();
        var scopeResult = engine.Run(new DecisionState(1000), policy, qualified, scoped, scoped);
        Check(scopeResult.Action == "us.core/Work" && scopeResult.Reason == "ActionOrScopeRejected" && scoped.Calls == 0, "scope rejection does not falsify facts or choose Call");
        var selected = new UniversalSqueaker.SqueakTriggerInvocation(UniversalSqueaker.SqueakTriggerOrigin.Wounded, UniversalSqueaker.SqueakInvocationSource.PlayerSelection);
        Check(selected.RequiresResponsivePawn && selected.IsPlayerInitiated && selected.SkipsRandomOneShotProbability, "legacy source metadata retained for controlled event bridge");
        var nativeEvent = new UniversalSqueaker.SqueakTriggerInvocation(UniversalSqueaker.SqueakTriggerOrigin.Wounded, UniversalSqueaker.SqueakInvocationSource.StateEvent);
        Check(!nativeEvent.RequiresResponsivePawn && !nativeEvent.IsPlayerInitiated, "native wounded event gains no selection/survival gate");
    }

    private static void RandomTranscript()
    {
        var engine = new OneShotDecision();
        var policy = new StatePolicy(40, 20);
        var randomInput = Input();
        randomInput.TalkingChance = .5;
        randomInput.Actions = randomInput.Actions.Select(x => x.Key == "us.core/Work" ? new ActionPlan(x.Key, x.Priority, x.Condition, x.Interval, chance: .5) : x).ToArray();
        randomInput.Audio = new[]
        {
            new AudioCandidate("a", "pack/first", "us.core/Work", "Ratkin", 1),
            new AudioCandidate("b", "pack/second", "us.core/Work", "Ratkin", 3)
        };
        var transcript = new Driver();
        engine.Run(new DecisionState(1000), policy, randomInput, transcript, transcript);
        Check(transcript.Purposes.SequenceEqual(new[] { RandomPurpose.Talking, RandomPurpose.Eligibility, RandomPurpose.Sound }), "ordered purpose transcript has no missing/extra draw");
        randomInput.TalkingChance = 1;
        randomInput.Actions = Input().Actions;
        var high = new Driver
        {
            Value = .9
        };
        var weighted = engine.Run(new DecisionState(1000), policy, randomInput, high, high);
        Check(weighted.Receipt?.Reason == "pack/second" && high.Rolls == 1, "fixed .9 selects weight-three entry using retained selector");
    }

    private static void FileExamples()
    {
        var catalog = new AuthorCatalog(120, 120);
        var ordinary = new XmlDocument();
        ordinary.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "examples", "AudioOnly.xml"));
        var pack = new VoicePackDeclaration();
        pack.LoadDataFromXmlCustom(ordinary.SelectSingleNode("Defs/UniversalSqueaker.UsVoicePackDef/declaration")!);
        Check(catalog.AddPack("example.audio", "US_Ratkin_WorkSlice", pack, s => "example.audio/" + s), "real ordinary XML field loaded and assembled");
        var extensions = new XmlDocument();
        extensions.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "examples", "Extensions.xml"));
        string providerType = extensions.SelectSingleNode("Defs/UniversalSqueaker.UsProviderDef/providerClass")!.InnerText;
        var type = typeof(AuthorCatalog).Assembly.GetType(providerType, true)!;
        Check(catalog.AddProvider((IUsDefinitionProvider)Activator.CreateInstance(type)!), "real explicit C# provider type instantiated from example");
        var action = new ActionDeclaration();
        action.LoadDataFromXmlCustom(extensions.SelectSingleNode("Defs/UniversalSqueaker.UsActionDef/declaration")!);
        Check(catalog.AddAction(action), "real XML combination assembled");
        var audio = new VoicePackDeclaration();
        audio.LoadDataFromXmlCustom(extensions.SelectSingleNode("Defs/UniversalSqueaker.UsVoicePackDef/declaration")!);
        Check(catalog.AddPack("example.ext", "US_ControlledExampleAudio", audio, s => "example.audio/" + s), "extension audio references assembled action");
        var input = Input();
        input.Actions = catalog.Actions;
        input.Audio = catalog.Audio;
        input.Facts = catalog.Capture("p1", new Dictionary<string, Truth> { { "us.core/WorkState", Truth.True } });
        var d = new Driver();
        var outcome = new OneShotDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), input, d, d);
        Check(outcome.Action == "example/OnDutyWork" && d.Calls == 1, "real file/provider/combination dispatch production core");
    }

    private static XmlNode Node(string text)
    {
        var doc = new XmlDocument();
        doc.LoadXml(text);
        return doc.DocumentElement!;
    }

    private sealed class Fact : IUsFactSource
    {
        public string Key => "example/OnDuty";

        public Truth Capture(string subject) => Truth.True;
    }

    private sealed class Provider : IUsDefinitionProvider
    {
        public bool Fail;
        public void Collect(ProviderBatch b)
        {
            b.AddFactSource(new Fact());
            if (Fail)
            {
                throw new Exception("partial");
            }
        }
    }

    private static void AuthorCases()
    {
        var catalog = new AuthorCatalog(120, 120);
        var pack = new VoicePackDeclaration();
        pack.LoadDataFromXmlCustom(Node("<declaration><race>Ratkin</race><entries><li><key>w</key><action>us.core/Work</action><sound>US_Work</sound><volume>0</volume></li><li><key>off</key><action>us.core/Work</action><sound>US_Off</sound><enabled>false</enabled></li></entries></declaration>"));
        Check(pack.FormatVersion == 1 && pack.Entries[0].Volume == 0 && pack.Entries[1].Enabled == false, "U3 omission/presence zero false");
        Check(catalog.AddPack("example.pack", "US_Pack", pack, x => "example.pack/" + x) && catalog.Audio.Length == 1 && catalog.Audio[0].Volume == 0, "ordinary audio-only assembled");
        Check(!catalog.AddPack("example.pack", "US_Pack", pack, x => x), "duplicate exact pack rejected");
        var action = new ActionDeclaration();
        action.LoadDataFromXmlCustom(Node("<declaration><formatVersion>1</formatVersion><key>example/OnDutyWork</key><baseAction>us.core/Work</baseAction><condition><all><ref>us.core/WorkState</ref><ref>example/OnDuty</ref></all></condition></declaration>"));
        Check(!catalog.AddAction(action), "missing required capability not removed");
        Check(!catalog.AddProvider(new Provider { Fail = true }), "provider failure no half batch");
        Check(!catalog.AddAction(action), "failed batch fact unavailable");
        Check(catalog.AddProvider(new Provider()), "single explicit provider accepted");
        Check(catalog.AddAction(action), "XML composition assembled with provider");
        var snapshot = catalog.Capture("p1", new Dictionary<string, Truth> { { "us.core/WorkState", Truth.True } });
        Check(catalog.Actions.Single(x => x.Key == "example/OnDutyWork").Condition.Evaluate(snapshot) == Truth.True, "real composed condition callable");
        var customPack = new VoicePackDeclaration();
        customPack.LoadDataFromXmlCustom(Node("<declaration><race>Ratkin</race><entries><li><key>a</key><action>example/OnDutyWork</action><sound>US_Work</sound></li></entries></declaration>"));
        Check(catalog.AddPack("example.pack", "US_Custom", customPack, x => "example.pack/" + x), "custom action audio loaded");
        var input = Input();
        input.Facts = snapshot;
        input.Actions = catalog.Actions;
        input.Audio = catalog.Audio;
        var driver = new Driver();
        var result = new OneShotDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), input, driver, driver);
        Check(result.Action == "example/OnDutyWork" && driver.Calls == 1, "author load enters production decision and dispatch");
        bool rejected = false;
        try
        {
            new VoicePackDeclaration().LoadDataFromXmlCustom(Node("<declaration><formatVersion>2</formatVersion><race>Ratkin</race></declaration>"));
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "unknown format rejected");
    }
}


