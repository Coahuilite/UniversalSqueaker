using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;

internal static partial class Program
{
    private static void DaContractIdentity()
    {
        var leaf = new Condition("ref", "us.core/SelectEvent");
        var alias = new[] { leaf };
        var tree = new Condition("all", "", alias);
        alias[0] = new Condition("ref", "missing");
        var facts = new FactSnapshot(new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.True });
        Check(tree.Evaluate(facts) == Truth.True, "DA condition copies constructor array aliases");
        bool immutable = false;
        try { ((IList<Condition>)tree.Children)[0] = alias[0]; } catch (NotSupportedException) { immutable = true; }
        Check(immutable, "DA public child view cannot mutate a registered subtree");
        var catalog = new DefinitionCatalog(120, 120, true);
        var audioInputs = new AudioInputBuilder(catalog);
        var root = new ActionDeclaration { Key = "vendor/IndependentSelect", Condition = leaf, Priority = 40 };
        var child = new ActionDeclaration { Key = "vendor/InjuredSelect", BaseAction = root.Key,
            Condition = new Condition("ref", "us.core/Responsive"), Priority = 50 };
        var grand = new ActionDeclaration { Key = "vendor/Grandchild", BaseAction = child.Key,
            Condition = new Condition("ref", "us.core/PlayerControlled"), Priority = 60 };
        Check(catalog.AddActions(new[] { grand, child, root }) == 3, "DA independent root and out-of-order multilevel graph resolve together");
        root.Condition = new Condition("ref", "absent");
        facts = new FactSnapshot(new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.True,
            ["us.core/Responsive"] = Truth.False, ["us.core/PlayerControlled"] = Truth.True });
        Check(catalog.Actions.Single(x => x.Key == root.Key).Condition.Evaluate(facts) == Truth.True
            && catalog.Actions.Single(x => x.Key == child.Key).Condition.Evaluate(facts) == Truth.False,
            "DA changing author declarations or child conditions cannot rewrite the original root");
        Check(catalog.Actions.Single(x => x.Key == "us.core/Select").Condition.Evaluate(facts) == Truth.True,
            "DA ordinary Select remains independent of narrowed extensions");
        Check(catalog.AddActions(new[] {
            new ActionDeclaration { Key = "bad/A", BaseAction = "bad/B" },
            new ActionDeclaration { Key = "bad/B", BaseAction = "bad/A" },
            new ActionDeclaration { Key = "bad/Missing", BaseAction = "absent/Parent" },
            new ActionDeclaration { Key = "bad/Duplicate" }, new ActionDeclaration { Key = "bad/Duplicate" },
            new ActionDeclaration { Key = "bad/Fact", Condition = new Condition("ref", "absent/Fact") }
        }) == 0 && !catalog.Actions.Any(x => x.Key.StartsWith("bad/", StringComparison.Ordinal)),
            "DA cycles, duplicate identities, unavailable parent and missing implementation remain isolated");
        var sourceDomain = new BehaviorDomain("Pawn", "", "Ratkin");
        var source = new BehaviorSource { Id = "vendor/Settings", Domain = sourceDomain,
            Contributions = new List<BehaviorContribution> {
                new() { Action = root.Key, Values = new BehaviorValues { Enabled = false } },
                new() { Action = child.Key, Values = new BehaviorValues { Enabled = true, Volume = 0 } }
            } };
        var directory = new BehaviorSourceDirectory(new[] { source });
        var binding = new BehaviorSourceState();
        Check(binding.Bind(sourceDomain, source.Id, directory), "DA exact author source bound");
        binding = BehaviorSourceState.FromXml(binding.ToXml());
        Check(binding.Resolve(sourceDomain, root.Key, directory, Array.Empty<BehaviorValues>(), catalog.HasFact, out _).Enabled == false
            && binding.Resolve(sourceDomain, child.Key, directory, Array.Empty<BehaviorValues>(), catalog.HasFact, out _).Volume == 0,
            "DA source snapshot keeps distinct complete parent/child action identity and explicit zero");
        foreach (var target in new[] { new BehaviorDomain("Pawn", "", "Kiiro"), new BehaviorDomain("NonPawn", "armor", "Ratkin"), new BehaviorDomain("NonPawn", "bench", "Ratkin") })
            Check(binding.Resolve(target, child.Key, directory, Array.Empty<BehaviorValues>(), catalog.HasFact, out _).Volume == null,
                "DA exact behavior domain cannot borrow a different subject family/namespace/target");

        var input = new DecisionInput { Subject = "p1", SubjectRole = "SelectedSubject", Domain = "Ratkin", Tick = 1000,
            HasVocalOrgans = false, TalkingChance = 0, Facts = facts,
            Actions = catalog.Actions.Where(x => x.Key == "us.core/Select").ToArray(),
            AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("ordinary", "audio/shared", "us.core/Select", "Ratkin", 1) })};
        var driver = new Driver();
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, driver, driver).Receipt?.Result == PlaybackResult.Dispatched,
            "DA generic Select does not inject Pawn vocal/talking qualifications");
        input.Actions = catalog.Actions.Where(x => x.Key == child.Key).ToArray();
        input.AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("child", "audio/shared", child.Key, "Ratkin", 1) });
        driver = new Driver();
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, driver, driver).Receipt == null,
            "DA narrowed child cannot borrow true parent facts");
        input.Facts = new FactSnapshot(new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.True, ["us.core/Responsive"] = Truth.True });
        var state = new DecisionState(1000);
        var outcome = new ResponseDecision().Run(state, new StatePolicy(0, 20), input, driver, driver);
        Check(outcome.Action == child.Key && driver.LastRequest!.Action == child.Key && driver.LastRequest.Audio.EntryKey == "child"
            && driver.LastRequest.SubjectRole == "SelectedSubject" && state.ConfirmedAction == child.Key,
            "DA child keeps complete identity through selection/request/receipt/settlement with direct shared AudioId");
        input.AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("ordinary", "audio/shared", "us.core/Select", "Ratkin", 1) });
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, new Driver(), new Driver()).Reason == "NoBoundAudio",
            "DA missing child mapping never creates implicit parent-audio fallback");
        DaSourceAttribution();
        DaDeclaredDomains();
        DcRetiredNamesRemainAvailableToIndependentAuthors();
        DcAuthorExamples();
        Console.WriteLine("DA contract identity, graph and source attribution checks passed");
    }

    private static void DcRetiredNamesRemainAvailableToIndependentAuthors()
    {
        var catalog = new DefinitionCatalog(120, 120, true);
        var audioInputs = new AudioInputBuilder(catalog);
        Check(catalog.Actions.Length == 15 && catalog.Actions.All(x => x.SubjectRole != ""),
            "DC official inventory is exactly fifteen with explicit selected-contract roles");
        foreach (string name in new[] { "Crying", "Giggling" })
        {
            string key = "independent/" + name;
            Check(catalog.AddAction(new ActionDeclaration { Key = key, Condition = new Condition("ref", "us.core/SelectEvent") }),
                "DC retirement is not a global third-party contract-name blacklist " + key);
            var input = new DecisionInput { Subject = "p1", SubjectRole = "DeclaredSubject", Domain = "Ratkin", Tick = 1000,
                Facts = new FactSnapshot(new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.True }),
                Actions = catalog.Actions.Where(x => x.Key == key).ToArray(),
                AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("independent-entry", "audio/direct", key, "Ratkin", 1) })};
            var driver = new Driver();
            var result = new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, driver, driver);
            Check(result.Receipt?.Result == PlaybackResult.Dispatched && driver.LastRequest?.Action == key
                && driver.LastRequest.SubjectRole == "DeclaredSubject", "DC independent positive contract still dispatches with its own key/subject " + key);
        }
    }

    private static void DaDeclaredDomains()
    {
        var catalog = new DefinitionCatalog(120, 120, true);
        var audioInputs = new AudioInputBuilder(catalog);
        const string action = "vendor/GenericSelect";
        Check(catalog.AddAction(new ActionDeclaration { Key = action, Condition = new Condition("ref", "us.core/SelectEvent") }),
            "DA independent generic root registers without a Pawn parent");
        foreach (var identity in new[] { "Ratkin", "Kiiro", "armor", "bench" })
        {
            bool pawn = identity == "Ratkin" || identity == "Kiiro";
            var declaration = new VoicePackDeclaration { Family = pawn ? "Pawn" : "NonPawn",
                Namespace = pawn ? "" : identity, Target = pawn ? identity : "A" };
            declaration.Entries.Add(new VoiceEntry { Key = "entry", Action = action, Sound = "shared" });
            Check(audioInputs.AddPack("domains", identity, declaration, _ => "audio/shared"), "DA declared exact audio domain " + identity);
        }
        foreach (var identity in new[] { "Ratkin", "Kiiro", "armor", "bench" })
        {
            bool pawn = identity == "Ratkin" || identity == "Kiiro";
            var input = new DecisionInput { Subject = identity + "1", SubjectRole = "SelectedSubject", Tick = 1000,
                Family = pawn ? SubjectFamily.PawnRace : SubjectFamily.NonPawn, TargetNamespace = pawn ? "" : identity,
                Domain = pawn ? identity : "A", HasVocalOrgans = false, TalkingChance = 0,
                Facts = new FactSnapshot(new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.True }),
                Actions = catalog.Actions.Where(x => x.Key == action).ToArray(), AudioCatalog = new AudioCatalog(audioInputs.Entries)};
            var d = new Driver();
            var result = new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, d, d);
            Check(result.Receipt?.Result == PlaybackResult.Dispatched && d.LastRequest!.Audio.EntryKey == "domains/" + identity + "/entry"
                && d.LastRequest.Family == input.Family && d.LastRequest.TargetNamespace == input.TargetNamespace,
                "DA registered root and pack reach the same production decision without cross-domain candidates " + identity);
        }
    }

    private static void DaSourceAttribution()
    {
        var race = AudioDomains.RaceOnly("Ratkin");
        var xeno = new AudioDomain(new RaceKey("Ratkin"), new XenotypeKey("X"));
        var vanilla = new BuiltInFallbackTable(new[] { new FallbackProfile(new RaceKey("Ratkin"), 1,
            new Dictionary<string, string> { ["us.core/Select"] = "audio/shared" }) });
        VoicePackEntry Pack(string key, AudioDomain domain, bool fallback = false) => new(key, domain, 1,
            fallback ? new Dictionary<string, IReadOnlyList<ActionSoundSet>>() : new Dictionary<string, IReadOnlyList<ActionSoundSet>> {
                ["us.core/Select"] = new[] { new ActionSoundSet(new[] { "audio/shared" }, null, 1) } },
            fallback ? new Dictionary<string, string> { ["us.core/Select"] = "audio/shared" } : null);
        var entries = new[] { Pack("race", race), Pack("xeno", xeno), Pack("fallback", xeno, true) };
        foreach (var mode in new[] { SelectionMode.Fallback, SelectionMode.Remix, SelectionMode.Off })
        {
            int count = mode == SelectionMode.Remix ? 4 : 1;
            for (int i = 0; i < count; i++)
            {
                var input = new DecisionInput { Subject = "p1", SubjectRole = "SelectedSubject", Domain = "Ratkin", ChildDomain = "X", Tick = 1000,
                    Facts = new FactSnapshot(new Dictionary<string, Truth> { ["accepted"] = Truth.True }),
                    Actions = new[] { new ActionPlan("us.core/Select", 1, new Condition("ref", "accepted"), 120, skipTalking: true, requiresVocal: false) },
                    AudioContext = new SelectionContext(xeno, "wrong/action", AgeBucket.Adult, true),
                    AudioCatalog = new AudioCatalog(Array.Empty<AudioCandidate>(), vanilla, entries), SourceMode = mode };
                var d = new Driver { Value = (i + .1) / count };
                var result = new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, d, d);
                var a = d.LastRequest!.Audio;
                var expectedTier = mode == SelectionMode.Off ? ChainTier.BuiltInFallback : (ChainTier)new[] { 0, 1, 2, 3 }[i];
                Check(result.Receipt?.Result == PlaybackResult.Dispatched && a.SourceTier == expectedTier
                    && a.EntryKey != "fallback/us.core/Select" && a.EntryKey.Contains("us.core/Select")
                    && a.SourceDomain == (expectedTier == ChainTier.XenotypePack || expectedTier == ChainTier.PackFallback ? xeno : race)
                    && a.Action == "us.core/Select" && d.LastRequest.ChildDomain == "X",
                    "DA all normal/fallback/final tiers retain actual entry/source domain and complete action " + mode + "/" + i);
            }
        }
        foreach (string ns in new[] { "armor", "bench" })
        {
            var domain = AudioDomain.NonPawn(ns, "A");
            var input = new DecisionInput { Subject = ns + "1", SubjectRole = "SelectedSubject", Family = SubjectFamily.NonPawn,
                TargetNamespace = ns, Domain = "A", Tick = 1000, HasVocalOrgans = false,
                Actions = new[] { new ActionPlan("vendor/Select", 1, new Condition("all"), 0, skipTalking: true, requiresVocal: false) },
                AudioCatalog = new AudioCatalog(Array.Empty<AudioCandidate>(), BuiltInFallbackTable.Empty, new[] { new VoicePackEntry(ns, domain, 1,
                    new Dictionary<string, IReadOnlyList<ActionSoundSet>> { ["vendor/Select"] = new[] { new ActionSoundSet(new[] { "audio/shared" }, null, 1) } }) }),
                AudioContext = new SelectionContext(domain, "vendor/Select", AgeBucket.Adult, true) };
            var d = new Driver();
            new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, d, d);
            Check(d.LastRequest!.Family == SubjectFamily.NonPawn && d.LastRequest.TargetNamespace == ns
                && d.LastRequest.Audio.SourceDomain == domain && d.LastRequest.Audio.SourceTier == ChainTier.ObjectPack,
                "DA NonPawn old-pool branch retains actual family/namespace and object source " + ns);
            input.TargetNamespace = ns == "armor" ? "bench" : "armor";
            d = new Driver();
            Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, d, d).Reason == "InvalidIdentity" && d.Calls == 0,
                "DA mismatched caller audio context is rejected before selection");
        }
    }
}
