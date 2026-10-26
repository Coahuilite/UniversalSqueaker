using UniversalSqueaker.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Responses;

internal static partial class Program
{
    private static void S3ContractsAndSources()
    {
        AllBuiltInContractsUseSameDecision();
        CompatibilityUsesSameOwnerAndOriginalClock();
        SparseSourceAuthorityAndRecovery();
        SourceDeletionAndStrictShape();
        IncompatibleSnapshotAndTickOverflow();
        FinalNativeParametersRejectUnrepresentableProducts();
        S3FileExampleIsConsumed();
        string first = S3Replay();
        string second = S3Replay();
        Check(first == second, "S3 full facts/ticks/purpose/result transcript double replay");
        Console.Write(first);
    }

    private static void AllBuiltInContractsUseSameDecision()
    {
        var catalog = new DefinitionCatalog(120, 120, fullDevCandidate: true);
        var audioInputs = new AudioInputBuilder(catalog);
        Check(catalog.Actions.Length == 15, "all fifteen official wrappers present only in expanded Dev selection");
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
        {
            var facts = BuiltInPawnContracts.CandidateOrder.ToDictionary(BuiltInPawnContracts.FactKey,
                x => x == key ? Truth.True : Truth.False);
            foreach (var result in (PlaybackResult[])Enum.GetValues(typeof(PlaybackResult)))
            {
                var driver = new Driver { Result = result };
                var input = new DecisionInput { Subject = "p1", Domain = "Ratkin", Tick = 1000,
                    StateOpportunity = !BuiltInPawnContracts.IsEvent(key), Actions = catalog.Actions,
                    EventFact = BuiltInPawnContracts.IsEvent(key) ? BuiltInPawnContracts.FactKey(key) : "",
                    Interaction = UsResponsePolicy.IsOfficialInteraction(key)
                        ? new InteractionOpportunity(BuiltInPawnContracts.FactKey(key), 1, 100) : null,
                    Facts = catalog.Capture("p1", facts), TalkingChance = key == "us.core/Death" ? 0 : 1,
                    AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("entry", "core/" + key, key, "Ratkin", 1) })};
                var state = new DecisionState(1000);
                var outcome = new ResponseDecision().Run(state, new StatePolicy(40, 20), input, driver, driver);
                Check(outcome.Action == key && outcome.Receipt?.Result == result && driver.Calls == 1,
                    key + " uses real decision and each backend receipt, Death skips only Talking");
                Check(driver.Purposes.Count == 0 && state.AttemptTick == (input.Interaction.HasValue ? long.MinValue : 1000), key + " one candidate consumes no unnecessary roll and reserves only its opportunity channel");
                Check((state.ConfirmedTick == 1000) == (result == PlaybackResult.Dispatched), key + " confirmed receipt separation");
                input.HasVocalOrgans = false;
                var noOrgans = new Driver { Result = result };
                var qualification = new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), input, noOrgans, noOrgans);
                Check(key == "us.core/Select" ? qualification.Receipt?.Result == result && noOrgans.Calls == 1
                    : qualification.Reason == "NoVocalOrgans" && noOrgans.Calls == 0, key + " explicit applicable vocal qualification");
            }
        }
        var unknown = new DecisionInput { Subject = "p1", Domain = "Ratkin", Tick = 1000, StateOpportunity = true,
            Actions = catalog.Actions, Facts = new FactSnapshot(new Dictionary<string, Truth>()) };
        var empty = new Driver();
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), unknown, empty, empty).Reason == "UnknownFacts"
            && empty.Calls == 0 && empty.Rolls == 0, "unknown full capture never fabricates Call");
    }

    private static BehaviorSource Source(string id, BehaviorDomain domain, string action, BehaviorValues values, params string[] requires)
        => new() { Id = id, Domain = domain, Contributions = new List<BehaviorContribution>
            { new() { Action = action, Values = values, RequiredFacts = requires } } };

    private static void SparseSourceAuthorityAndRecovery()
    {
        var race = new BehaviorDomain("Pawn", "", "Ratkin");
        var child = new BehaviorDomain("Pawn", "", "Ratkin", "Xeno");
        var neighbor = new BehaviorDomain("Pawn", "", "Kiiro");
        var a = Source("author.a/Behavior", race, "us.core/Work", new BehaviorValues { Enabled = false, TalkingEnabled = false, SimulationIntervalTicks = 0, Volume = 0 }, "extension/Required");
        var b = Source("author.b/Behavior", race, "us.core/Work", new BehaviorValues { Enabled = true, Volume = 9 });
        var xeno = Source("author.x/Behavior", child, "us.core/Work", new BehaviorValues { Enabled = true, Volume = 3, Pitch = 2 });
        var other = Source("author.n/Behavior", neighbor, "us.core/Work", new BehaviorValues { Enabled = false });
        var directory = new BehaviorSourceDirectory(new[] { a, b, xeno, other });
        var state = new BehaviorSourceState();
        Check(state.Read(race).Intent == BehaviorSourceIntent.Unconfigured && !state.AdoptUniqueDevCandidate(race, directory), "complete conflicting candidate set does not auto choose");
        Check(state.Bind(race, a.Id, directory) && state.Bind(child, xeno.Id, directory) && state.Bind(neighbor, other.Id, directory), "explicit exact-domain bindings");
        var inherited = new BehaviorDomain("Pawn", "", "Ratkin", "Inherited");
        state.Forget(inherited);
        Check(!state.AdoptUniqueDevCandidate(inherited, directory), "explicit inheritance never adopts a new local source");
        string saved = state.ToXml();
        Check(saved.Contains("enabled=\"False\"") && saved.Contains("simulationIntervalTicks=\"0\"") && !saved.Contains("tick") && !saved.Contains("player"), "author-only snapshot contains false and zero existence, no runtime or player state");
        state = BehaviorSourceState.FromXml(saved);
        var missing = new BehaviorSourceDirectory(new[] { b, other });
        var value = state.Resolve(race, "us.core/Work", missing, Array.Empty<BehaviorValues>(), _ => true, out bool paused);
        Check(value.Enabled == false && value.TalkingEnabled == false && value.SimulationIntervalTicks == 0 && value.Volume == 0 && !paused,
            "A absent with B present preserves A false/zero snapshot, no takeover");
        value = state.Resolve(child, "us.core/Work", missing, new[] { new BehaviorValues { Enabled = false, Volume = 7 } }, _ => true, out paused);
        Check(value.Enabled == false && value.Volume == 7 && value.Pitch == 2, "all author parent/child fold before player parent: sparse field permissions");
        var returned = a.Copy();
        returned.Contributions[0].Values.Volume = 2;
        var reconnected = new BehaviorSourceDirectory(new[] { returned, b, xeno, other });
        state.Refresh(reconnected);
        value = state.Resolve(race, "us.core/Work", reconnected, Array.Empty<BehaviorValues>(), _ => false, out paused);
        Check(paused && value.Volume == 2 && state.Read(race).SourceId == a.Id, "same identity reconnect updates author snapshot but missing implementation still pauses dependency");
        var bad = returned.Copy();
        bad.Contributions[0].Values.Volume = double.NaN;
        bad.Contributions.Add(new BehaviorContribution { Action = "us.core/Select", Values = new BehaviorValues { Volume = 4 } });
        var badDirectory = new BehaviorSourceDirectory(new[] { bad, b, other });
        state.Refresh(badDirectory);
        value = state.Resolve(race, "us.core/Work", badDirectory, Array.Empty<BehaviorValues>(), _ => true, out paused);
        Check(value.Volume == 2 && !paused, "bad update cannot replace last valid author Work snapshot");
        value = state.Resolve(race, "us.core/Select", badDirectory, Array.Empty<BehaviorValues>(), _ => true, out paused);
        Check(value.Volume == 4 && !paused, "bad Work update does not suspend valid Select contribution");
        state.Forget(race);
        value = state.Resolve(race, "us.core/Work", reconnected, new[] { new BehaviorValues { Volume = 7 } }, _ => true, out paused);
        Check(state.Read(race).Intent == BehaviorSourceIntent.Inherit && state.Read(race).Snapshot == null && value.Volume == 7
            && state.Read(neighbor).SourceId == other.Id && state.Read(child).SourceId == xeno.Id, "single-domain forget preserves neighbor, child and player data");
        var unique = new BehaviorSourceState();
        Check(unique.AdoptUniqueDevCandidate(race, new BehaviorSourceDirectory(new[] { a, a })), "explicit Dev adoption deduplicates repeated same source reference");
        Check(!unique.Bind(race, b.Id, new BehaviorSourceDirectory(new[] { a })), "failed switch preserves old binding");
        Check(unique.Read(race).SourceId == a.Id, "switch failure has no mutation");
        var bench = new BehaviorDomain("NonPawn", "bench", "A");
        Check(!bench.Equals(new BehaviorDomain("NonPawn", "armor", "A")), "nonpawn source identities preserve explicit namespace");
    }

    private static void S3FileExampleIsConsumed()
    {
        var document = new XmlDocument();
        document.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "examples", "BehaviorSource.xml"));
        var source = new BehaviorSourceDeclaration();
        source.LoadDataFromXmlCustom(document.SelectSingleNode("Defs/UniversalSqueaker.UsBehaviorSourceDef/declaration")!);
        source.Source.Id = "example/US_ExampleBehavior";
        var catalog = new DefinitionCatalog(120, 120, fullDevCandidate: true);
        var audioInputs = new AudioInputBuilder(catalog);
        catalog.AddBehaviorSource(source.Source);
        var binding = new BehaviorSourceState();
        Check(binding.Bind(source.Source.Domain, source.Source.Id, catalog.BehaviorSources), "real source XML parser publishes into same author catalog");
        var resolved = binding.Resolve(source.Source.Domain, "us.core/Work", catalog.BehaviorSources, Array.Empty<BehaviorValues>(), catalog.HasFact, out bool paused);
        Check(resolved.Enabled == false && resolved.Volume == 0 && !paused, "loaded source XML is actually resolved with presence and catalog requirements");
        var action = new ActionDeclaration();
        action.LoadDataFromXmlCustom(document.SelectSingleNode("Defs/UniversalSqueaker.UsActionDef/declaration")!);
        Check(catalog.AddAction(action), "real Eat composition references registered built-in facts");
        var audio = new VoicePackDeclaration();
        audio.LoadDataFromXmlCustom(document.SelectSingleNode("Defs/UniversalSqueaker.UsVoicePackDef/declaration")!);
        Check(audioInputs.AddPack("example", "US_ExampleDrugAudio", audio, _ => "audio/Eat"), "real composite audio loaded");
        var input = new DecisionInput { Subject = "p1", Domain = "Ratkin", Tick = 1000, StateOpportunity = true, Actions = catalog.Actions, AudioCatalog = new AudioCatalog(audioInputs.Entries),
            Facts = catalog.Capture("p1", new Dictionary<string, Truth> { ["us.core/IngestionProcess"] = Truth.True, ["us.core/IngestibleDrug"] = Truth.True, ["us.core/IngestionExecution"] = Truth.True }) };
        var driver = new Driver();
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), input, driver, driver).Action == "example/DrugChewing"
            && driver.Calls == 1, "real expanded XML fact composition dispatches same production core");
        var multi = new BehaviorSourceDeclaration();
        multi.LoadDataFromXmlCustom(Node("<declaration><target family='Pawn' target='Ratkin'><action key='us.core/Work' volume='0'/></target><target family='Pawn' target='Ratkin' child='Xeno'><action key='us.core/Work' enabled='false'/></target></declaration>"));
        foreach (var target in multi.Sources)
        {
            target.Id = "example/OneDef";
            catalog.AddBehaviorSource(target);
        }
        Check(multi.Sources.Count == 2 && catalog.BehaviorSources.Candidates(new BehaviorDomain("Pawn", "", "Ratkin", "Xeno")).Single().Id == "example/OneDef",
            "one real source Def can declare separate exact targets without merging their identity or contributions");
    }

    private static void SourceDeletionAndStrictShape()
    {
        var domain = new BehaviorDomain("Pawn", "", "Ratkin");
        var a = Source("a/Def", domain, "us.core/Work", new BehaviorValues { Volume = 0 });
        var state = new BehaviorSourceState();
        state.Bind(domain, a.Id, new BehaviorSourceDirectory(new[] { a }));
        var removed = a.Copy();
        removed.Contributions.Clear();
        var current = new BehaviorSourceDirectory(new[] { removed });
        var value = state.Resolve(domain, "us.core/Work", current, Array.Empty<BehaviorValues>(), _ => true, out bool paused);
        Check(value.Volume == null && !paused, "valid author update deliberately removing Work does not resurrect old snapshot before refresh");
        state.Refresh(current);
        Check(state.Read(domain).Snapshot!.Contributions.Count == 0, "committed refresh removes old contribution rather than retaining effective parent value");
        foreach (string xml in new[]
        {
            "<behaviorSources format='1' typo='x' />",
            "<behaviorSources format='1'><wrong family='Pawn' target='Ratkin' intent='Inherit' /></behaviorSources>",
            "<behaviorSources format='1'><binding family='Pawn' target='Ratkin' intent='Bound' source='a/Def'><snapshot/><snapshot/></binding></behaviorSources>",
            "<behaviorSources format='1'><binding family='Pawn' target='Ratkin' intent='Inherit' typo='x'/></behaviorSources>"
        })
        {
            bool rejected = false;
            try { BehaviorSourceState.FromXml(xml); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "source Config parser rejects unknown/duplicate shape without silently deleting meaning");
        }
        foreach (string xml in new[]
        {
            "<declaration family='Pawn' target='Ratkin' typo='x'/>",
            "<declaration family='Pawn' target='Ratkin'><wrong key='us.core/Work'/></declaration>",
            "<declaration family='Pawn' target='Ratkin'><action key='us.core/Work' typo='false'/></declaration>",
            "<declaration family='Pawn' target='Ratkin'><action key='us.core/Work'><enabled>false</enabled></action></declaration>"
        })
        {
            bool rejected = false;
            try { new BehaviorSourceDeclaration().LoadDataFromXmlCustom(Node(xml)); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "author parser rejects misspelled field or extra element");
        }
        foreach (var args in new[] { new[] { "Pawn", "alias", "Ratkin", "" }, new[] { "Pawn", "", "Ratkin", " Xeno" }, new[] { "NonPawn", " ns", "A", "" } })
        {
            bool rejected = false;
            try { new BehaviorDomain(args[0], args[1], args[2], args[3]); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "source identities reject alternate Pawn namespace and whitespace spellings");
        }
    }
    private static void IncompatibleSnapshotAndTickOverflow()
    {
        var domain = new BehaviorDomain("Pawn", "", "Ratkin");
        var a = Source("a/Def", domain, "us.core/Work", new BehaviorValues { Enabled = false });
        var b = Source("b/Def", domain, "us.core/Work", new BehaviorValues { Enabled = true });
        var state = new BehaviorSourceState();
        state.Bind(domain, a.Id, new BehaviorSourceDirectory(new[] { a }));
        string incompatible = state.ToXml().Replace("semantics=\"1\"", "semantics=\"2\"");
        state = BehaviorSourceState.FromXml(incompatible);
        var missing = new BehaviorSourceDirectory(new[] { b });
        var reasons = new List<string>();
        state.Resolve(domain, "us.core/Work", missing, Array.Empty<BehaviorValues>(), _ => true, out bool paused, reasons.Add);
        Check(paused && state.Read(domain).SourceId == a.Id && state.ToXml() == incompatible,
            "Bound A absent with incompatible snapshot suspends dependent Work, preserves original and never adopts B");
        Check(reasons.Single() == "IncompatibleAuthorSnapshot:a/Def:us.core/Work", "incompatible snapshot records an actionable source/action reason");
        state.Resolve(domain, "us.core/Select", missing, Array.Empty<BehaviorValues>(), _ => true, out paused);
        Check(!paused, "incompatible Work snapshot does not suspend unrelated Select");
        var returned = new BehaviorSourceDirectory(new[] { a, b });
        state.Resolve(domain, "us.core/Work", returned, Array.Empty<BehaviorValues>(), _ => true, out paused);
        Check(!paused, "same valid A identity resumes live contribution despite old incompatible snapshot");
        state.Refresh(returned);
        Check(state.Read(domain).Snapshot!.Semantics == 1, "successful refresh replaces incompatible snapshot with current valid author record");
        foreach (var receipt in new[] { PlaybackResult.Dispatched, PlaybackResult.Indeterminate })
        {
            var input = Input(1000);
            input.Actions = new[] { new ActionPlan("us.core/Work", 20, new Condition("ref", "us.core/WorkState"), long.MaxValue) };
            var driver = new Driver { Result = receipt };
            var decisionState = new DecisionState(1000);
            new ResponseDecision().Run(decisionState, new StatePolicy(long.MaxValue, long.MaxValue), input, driver, driver);
            Check(decisionState.ActionDeadline("us.core/Work") == long.MaxValue && decisionState.SharedDeadline == long.MaxValue
                && decisionState.NextStateTick == long.MaxValue,
                "legal long.MaxValue intervals/shared/wait saturate deadlines rather than wrapping on " + receipt);
            input.Tick = 1001;
            Check(new ResponseDecision().Run(decisionState, new StatePolicy(40, 20), input, driver, driver).Reason == "NotDue" && driver.Calls == 1,
                "overflow-safe protection cannot be bypassed at next tick");
        }
        var retryInput = Input(long.MaxValue - 1, true, false, false, false);
        var retryState = new DecisionState(long.MaxValue - 1);
        var noAudio = new Driver();
        new ResponseDecision().Run(retryState, new StatePolicy(40, 20), retryInput, noAudio, noAudio);
        Check(retryState.NextStateTick == long.MaxValue, "retry advancement saturates near maximum tick too");
    }
    private static void FinalNativeParametersRejectUnrepresentableProducts()
    {
        var domain = new BehaviorDomain("Pawn", "", "Ratkin");
        var source = Source("a/Def", domain, "us.core/Work", new BehaviorValues { Volume = float.MaxValue });
        var directory = new BehaviorSourceDirectory(new[] { source });
        var binding = new BehaviorSourceState();
        binding.Bind(domain, source.Id, directory);
        var values = binding.Resolve(domain, "us.core/Work", directory, Array.Empty<BehaviorValues>(), _ => true, out _);
        var input = Input();
        input.Actions = new[] { new ActionPlan("us.core/Work", 20, new Condition("ref", "us.core/WorkState"), 120, volume: values.Volume!.Value) };
        input.AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("entry", "audio/Work", "us.core/Work", "Ratkin", 1, 2) });
        var driver = new Driver();
        var state = new DecisionState(1000);
        var outcome = new ResponseDecision().Run(state, new StatePolicy(40, 20), input, driver, driver);
        Check(outcome.Reason == "InvalidLocalParameters" && driver.Calls == 0 && state.SharedDeadline == 0,
            "legal source float.MaxValue times legal entry 2 rejects before backend/native without changing configuration");
        int nativeCalls = 0;
        var rejected = OneShotDispatch.Execute(() => OneShotOutput.TryPrepare(float.MaxValue, 1, 2, out _, out _), () => nativeCalls++);
        Check(rejected.Result == PlaybackResult.Rejected && nativeCalls == 0,
            "legal local maximum times global factor 2 rejects before native side effects, not native clamping");
        Check(OneShotOutput.TryPrepare(float.MaxValue, 1, 1, out float volume, out float pitch) && volume == float.MaxValue && pitch == 1,
            "exact float boundary remains valid, no speculative smaller S4 range");
        Check(OneShotOutput.TryPrepare(0, 1, 0, out volume, out pitch) && volume == 0,
            "explicit zero volume remains valid and is not confused with missing value");
        Check(!OneShotOutput.TryPrepare(1, double.NaN, 1, out _, out _) && !OneShotOutput.TryPrepare(1, double.Epsilon, 1, out _, out _),
            "invalid or positive-but-unrepresentable final pitch is rejected before native dispatch");
    }
    private static string S3Replay()
    {
        var result = new StringBuilder();
        var catalog = new DefinitionCatalog(120, 120, fullDevCandidate: true);
        var audioInputs = new AudioInputBuilder(catalog);
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
        {
            foreach (var receipt in (PlaybackResult[])Enum.GetValues(typeof(PlaybackResult)))
            {
                var driver = new Driver { Result = receipt };
                var input = new DecisionInput { Subject = "p1", Domain = "Ratkin", Tick = 1000,
                    StateOpportunity = !BuiltInPawnContracts.IsEvent(key),
                    EventFact = BuiltInPawnContracts.IsEvent(key) ? BuiltInPawnContracts.FactKey(key) : "",
                    Interaction = UsResponsePolicy.IsOfficialInteraction(key)
                        ? new InteractionOpportunity(BuiltInPawnContracts.FactKey(key), 1, 100) : null,
                    TalkingChance = .8, Actions = catalog.Actions,
                    Facts = catalog.Capture("p1", BuiltInPawnContracts.CandidateOrder.ToDictionary(BuiltInPawnContracts.FactKey, x => x == key ? Truth.True : Truth.False)),
                    AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("a", "audio/first", key, "Ratkin", 1), new AudioCandidate("b", "audio/second", key, "Ratkin", 3) })};
                var state = new DecisionState(1000);
                var outcome = new ResponseDecision().Run(state, new StatePolicy(40, 20), input, driver, driver);
                var expected = key == "us.core/Death" || key == "us.core/Select" ? new[] { RandomPurpose.Sound } : new[] { RandomPurpose.Talking, RandomPurpose.Sound };
                Check(driver.Purposes.SequenceEqual(expected), "S3 labelled roll sequence " + key + " " + receipt);
                result.AppendLine("S3 tick=1000 facts=" + key + " chosen=" + outcome.Action + " receipt=" + receipt + " roll=" + string.Join(",", driver.Purposes)
                    + " calls=" + driver.Calls + " confirmed=" + state.ConfirmedTick + " shared=" + state.SharedDeadline + " next=" + state.NextStateTick);
            }
        }
        return result.ToString();
    }
}
