using UniversalSqueaker.Kernel;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Responses;

internal static partial class Program
{
    private static void DbEatAuthorExamples()
    {
        var minimal = new XmlDocument(); minimal.Load(Path.Combine(AppContext.BaseDirectory, "examples", "EatMinimal.xml"));
        var catalog = new DefinitionCatalog(0, 0, true);
        var audioInputs = new AudioInputBuilder(catalog);
        var audio = new VoicePackDeclaration();
        audio.LoadDataFromXmlCustom(minimal.SelectSingleNode("//declaration")!);
        Check(audio.Entries.Count == 1 && audio.Entries[0].Action == "us.core/Eat" && audio.Entries[0].Volume == null,
            "DB minimum XML has only target/identity/audio mapping, no behavior prerequisites");
        Check(audioInputs.AddPack("example", "US_ExampleEatMinimal", audio, sound => sound == "Click" ? "core/Click" : null),
            "DB real minimal public XML parser/catalog accepts Core resource reference");
        var extensions = new XmlDocument(); extensions.Load(Path.Combine(AppContext.BaseDirectory, "examples", "EatExtensions.xml"));
        foreach (XmlNode node in extensions.SelectNodes("//UniversalSqueaker.UsActionDef/declaration")!)
        {
            var action = new ActionDeclaration(); action.LoadDataFromXmlCustom(node);
            Check(catalog.AddAction(action), "DB public extension XML loads full fixed contract " + action.Key);
        }
        audio = new VoicePackDeclaration(); audio.LoadDataFromXmlCustom(extensions.SelectSingleNode("//UniversalSqueaker.UsVoicePackDef/declaration")!);
        Check(audioInputs.AddPack("example", "US_ExampleEatExtensions", audio, _ => "core/Click"), "DB both extensions reuse exact AudioId without parent mapping");
        var source = new BehaviorSourceDeclaration(); source.LoadDataFromXmlCustom(extensions.SelectSingleNode("//UniversalSqueaker.UsBehaviorSourceDef/declaration")!);
        source.Source.Id = "example/US_ExampleEatBehavior";
        var sources = new BehaviorSourceDirectory(new[] { source.Source });
        var state = new BehaviorSourceState();
        Check(state.Bind(source.Source.Domain, source.Source.Id, sources), "DB three-axis source XML binds under existing source authority");
        var settings = new BehaviorConfiguration(state.ToXml(), Array.Empty<BehaviorPlayerOverride>(), true, 1, 1);
        var child = settings.Resolve(source.Source.Domain, "example/InjuredEat", new BehaviorValues(), sources, catalog.HasFact);
        Check(!child.Paused && child.Read().Phase == EatPhase.Execution && child.Read().DrugScope == EatDrugScope.ExcludeDrugs
            && child.Read().NutritionScope == EatNutritionScope.NutritionGiving && child.Read().Volume == .5,
            "DB real XML uses exact child full config instead of inheriting parent knobs");
        var after = catalog.Actions.Single(x => x.Key == "example/AfterIngestion");
        var facts = catalog.Capture("p1", new Dictionary<string, Truth> { [EatPolicy.Process] = Truth.False, [EatPolicy.Execution] = Truth.False, [EatPolicy.Result] = Truth.True },
            null, new IngestionResultFact("p1", "food1", "Beer", 0));
        Check(after.Condition.Evaluate(facts) == Truth.True && !after.Condition.RequiredFacts().Contains(EatPolicy.Process),
            "DB AfterIngestion XML is independent result root, never requires ended process");
        var driver = new Driver();
        var input = new DecisionInput { Subject = "p1", SubjectRole = "ActualIngester", Domain = "Ratkin", Tick = 1000,
            EventFact = EatPolicy.Result,
            Actions = new[] { after }, AudioCatalog = new AudioCatalog(audioInputs.Entries), Facts = facts };
        Check(new ResponseDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, driver, driver).Receipt?.Result == PlaybackResult.Dispatched
            && driver.LastRequest?.Ingestion?.Item == "food1" && driver.LastRequest?.Ingestion?.Nutrition == 0,
            "DB loaded result extension propagates immutable ingester/item/zero result through actual request");
    }
}
