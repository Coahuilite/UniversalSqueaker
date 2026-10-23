using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.OneShot;

internal static partial class Program
{
    private static void DcAuthorExamples()
    {
        var catalog = new AuthorCatalog(0, 0, true);
        XmlDocument Load(string file) { var d = new XmlDocument(); d.Load(Path.Combine(AppContext.BaseDirectory, "examples", file)); return d; }
        var document = Load("OfficialFifteen.xml");
        var pack = new VoicePackDeclaration(); pack.LoadDataFromXmlCustom(document.SelectSingleNode("//declaration")!);
        Check(pack.Entries.Count == 15 && pack.Entries.Select(x => x.Action).OrderBy(x => x).SequenceEqual(catalog.Actions.Select(x => x.Key).OrderBy(x => x)),
            "DC public XML maps all and only fifteen official full ActionIds");
        Check(catalog.AddPack("example", "US_ExampleFifteen", pack, name => name == "Click" ? "core/Click" : null), "DC public fifteen-pack XML passes production loader/catalog");
        foreach (var action in catalog.Actions) {
            var driver = new Driver();
            var values = action.Condition.RequiredFacts().ToDictionary(x => x, _ => Truth.True);
            var input = new DecisionInput { Subject = "p1", Domain = "Ratkin", SubjectRole = "SourceSubject", Tick = 1000,
                HasVocalOrgans = true, TalkingChance = 1, Facts = new FactSnapshot(values), Actions = new[] { action }, Audio = catalog.Audio };
            var result = new OneShotDecision().Run(new DecisionState(1000), new StatePolicy(0, 20), input, driver, driver);
            Check(result.Receipt?.Result == PlaybackResult.Dispatched && driver.LastRequest?.Action == action.Key
                && driver.LastRequest.SubjectRole == action.SubjectRole && driver.LastRequest.Audio.AudioId == "core/Click",
                "DC loaded public mapping carries exact contract role and direct AudioId " + action.Key);
        }
        document = Load("SelectQualifications.xml");
        foreach (XmlNode node in document.SelectNodes("//UniversalSqueaker.UsActionDef/declaration")!) {
            var action = new ActionDeclaration(); action.LoadDataFromXmlCustom(node);
            Check(catalog.AddAction(action), "DC public explicit qualification child/independent root registers " + action.Key);
        }
        var source = new BehaviorSourceDeclaration(); source.LoadDataFromXmlCustom(document.SelectSingleNode("//UniversalSqueaker.UsBehaviorSourceDef/declaration")!);
        source.Source.Id = "example/US_ExampleSelectQualifications";
        var directory = new BehaviorSourceDirectory(new[] { source.Source }); var state = new BehaviorSourceState();
        Check(state.Bind(source.Source.Domain, source.Source.Id, directory), "DC public Select qualification source binds explicitly");
        var snapshot = new BehaviorConfigurationSnapshot(state.ToXml(), Array.Empty<BehaviorPlayerOverride>(), true, 1, 1);
        var child = snapshot.Resolve(source.Source.Domain, "example/QualifiedSelected", new BehaviorValues(), directory, catalog.HasFact);
        var independent = snapshot.Resolve(source.Source.Domain, "example/SelectionWitness", new BehaviorValues(), directory, catalog.HasFact);
        Check(!child.Paused && child.Read().SelectRequirePlayerControlled == true && child.Read().SelectRequireAwake == false
            && child.Read().SelectRequireNotDowned == false && !SelectPolicy.HasParameters(independent.Read()),
            "DC child opts into its own sparse qualifications; generic root does not inherit official Pawn defaults");
        var fixedChild = catalog.Actions.Single(x => x.Key == "example/QualifiedSelected").Condition;
        var condition = new Condition("all", "", fixedChild, SelectConditions.Restriction(child.Read()));
        var facts = new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.True, ["us.core/HasCurrentInjury"] = Truth.True,
            [SelectPolicy.PlayerControlled] = Truth.True, [SelectPolicy.Awake] = Truth.Unknown, [SelectPolicy.NotDowned] = Truth.Unknown };
        Check(condition.Evaluate(new FactSnapshot(facts)) == Truth.True, "DC relaxed optional qualifications do not read unknown facts as required");
        facts["us.core/SelectEvent"] = Truth.False;
        Check(condition.Evaluate(new FactSnapshot(facts)) == Truth.False, "DC parameter settings cannot remove inherited accepted-feedback condition");
        facts["us.core/SelectEvent"] = Truth.True; facts[SelectPolicy.PlayerControlled] = Truth.Unknown;
        Check(condition.Evaluate(new FactSnapshot(facts)) == Truth.Unknown, "DC explicitly required player qualification keeps precise Unknown");
    }
}
