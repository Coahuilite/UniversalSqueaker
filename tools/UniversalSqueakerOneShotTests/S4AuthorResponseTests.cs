using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.OneShot;

internal static partial class Program
{
    private sealed class ResponseProvider : IUsDefinitionProvider
    {
        internal bool Duplicate;
        public void Collect(ProviderBatch batch)
        {
            batch.AddFactSource(new ResponseFact());
            batch.AddResponse(new ExampleResponse());
            if (Duplicate)
            {
                batch.AddResponse(new ExampleResponse());
            }
        }
    }
    private sealed class ResponseFact : IUsFactSource
    {
        public string Key => "example/Ready";
        public Truth Capture(string subject) => Truth.True;
    }
    private sealed class ExampleResponse : IUsResponseRule
    {
        public string Key => "example/Response";
        public ResponseStage Stage => ResponseStage.Age;
        public ResponseContribution Evaluate(ResponseContext context)
            => new(context.Require("example.pitch"), context.Require("example.volume"));
    }

    private static void S4AuthorResponses()
    {
        var catalog = new AuthorCatalog(720, 216, true);
        Check(!catalog.AddProvider(new ResponseProvider { Duplicate = true }) && !catalog.HasFact("example/Ready")
            && catalog.Responses.Length == 2, "S4 provider response conflict rejects its complete fact-and-response batch");
        Check(catalog.AddProvider(new ResponseProvider()) && catalog.HasFact("example/Ready")
            && catalog.Responses.Length == 3, "S4 neutral fact-and-response provider commits through one ordinary author seam");
        var xml = new XmlDocument();
        xml.LoadXml("<declaration><key>example/Work</key><baseAction>us.core/Work</baseAction><condition><ref>us.core/WorkState</ref></condition>"
            + "<playback>Sustained</playback></declaration>");
        var action = new ActionDeclaration();
        action.LoadDataFromXmlCustom(xml.DocumentElement!);
        Check(catalog.AddAction(action) && catalog.Actions.Single(x => x.Key == action.Key).Sustained,
            "S5 normal action loader carries explicit sustained shape into the production plan");
        xml.LoadXml("<declaration><race>Ratkin</race><participation><li key='example/Response'>Skip</li></participation>"
            + "<actionParticipation><li action='example/Work'><li key='example/Response'>Apply</li></li></actionParticipation>"
            + "<entries><li><key>a</key><action>example/Work</action><sound>Work</sound><pitch>2</pitch></li>"
            + "<li><key>b</key><action>example/Work</action><sound>Work</sound>"
            + "<participation><li key='example/Response'>Skip</li></participation></li></entries></declaration>");
        var pack = new VoicePackDeclaration();
        pack.LoadDataFromXmlCustom(xml.DocumentElement!);
        Check(catalog.AddPack("example", "Pack", pack, _ => "audio/Work")
            && catalog.Audio[0].Pitch == 2 && catalog.Audio[0].Participation["example/Response"]
            && !catalog.Audio[1].Participation["example/Response"],
            "S4 normal pack/action/entry defaults fold once; entry calibration pitch remains independent");
        var otherPack = new VoicePackDeclaration { Race = "Ratkin" };
        otherPack.Participation["example/Response"] = false;
        otherPack.Entries.Add(new VoiceEntry { Key = "other", Action = "example/Work", Sound = "Work" });
        Check(catalog.AddPack("other", "Pack", otherPack, _ => "audio/Work")
            && !catalog.Audio[2].Participation["example/Response"],
            "S4 first pack's action participation cannot override a different pack referencing the same action");
        foreach (string malformed in new[]
        {
            "<participation extra='1'><li key='us.core/Age'>Apply</li></participation>",
            "<participation><li key='us.core/Age'>True</li></participation>",
            "<actionParticipation typo='1'><li action='us.core/Work'><li key='us.core/Age'>Apply</li></li></actionParticipation>",
            "<actionParticipation><li action='us.core/Work' extra='1'><li key='us.core/Age'>Apply</li></li></actionParticipation>",
            "<actionParticipation><li action='us.core/Work'/><li action='us.core/Work'/></actionParticipation>"
        })
        {
            xml.LoadXml("<declaration><race>Ratkin</race>" + malformed + "</declaration>");
            bool rejected = false;
            try
            {
                new VoicePackDeclaration().LoadDataFromXmlCustom(xml.DocumentElement!);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }
            Check(rejected, "S4 strict ordinary participation loader rejects malformed shape " + malformed);
        }

        xml.LoadXml("<declaration family='Pawn' target='Ratkin'><action key='Work' ageEnabled='true' moodEnabled='true'>"
            + "<response key='example/Response' enabled='true'/>"
            + "<parameter key='example.pitch' value='2'/><parameter key='example.volume' value='.5'/>"
            + "<parameter key='mood.low' value='.2'/><parameter key='mood.high' value='.8'/>"
            + "<parameter key='mood.high.pitch' value='.5'/></action></declaration>");
        var source = new BehaviorSourceDeclaration();
        source.LoadDataFromXmlCustom(xml.DocumentElement!);
        source.Source.Id = "example/Behavior";
        var directory = new BehaviorSourceDirectory(new[] { source.Source });
        var bindings = new BehaviorSourceState();
        Check(bindings.Bind(source.Source.Domain, source.Source.Id, directory), "S4 real source identity binds response parameters without enabling audio");
        string saved = bindings.ToXml();
        var restored = BehaviorSourceState.FromXml(saved);
        var effective = restored.Resolve(source.Source.Domain, "Work", directory, Array.Empty<BehaviorValues>(), _ => true, out bool paused);
        var facts = new Dictionary<string, double> { ["age.native"] = 1.2, ["age.nativeVolume"] = .5, ["mood.level"] = .9 };
        var plan = ResponseModulation.Resolve("p1", effective, facts, catalog.Responses);
        var result = ResponseModulation.Compose(2, 3, plan, catalog.Audio[0].Participation, new Driver());
        Check(!paused && result.Valid && Math.Abs(result.Pitch - 2.4) < .000001 && result.Volume == .75
            && restored.ToXml() == saved, "S4 real source parser/snapshot/runtime resolver composes native age then custom age and mood from one raw context");
        var entrySkip = ResponseModulation.Compose(1, 1, plan, catalog.Audio[1].Participation, new Driver());
        Check(entrySkip.Valid && Math.Abs(entrySkip.Pitch - .6) < .000001 && entrySkip.Volume == .5,
            "S4 enabled author behavior response cannot override the selected audio entry Skip");
        var explicitApply = new BehaviorValues();
        explicitApply.Participation["example/Response"] = true;
        var applied = restored.Resolve(source.Source.Domain, "Work", directory, new[] { explicitApply }, _ => true, out _);
        var applyPlan = ResponseModulation.Resolve("p1", applied, facts, catalog.Responses);
        var playerApply = ResponseModulation.Compose(1, 1, applyPlan, catalog.Audio[1].Participation, new Driver());
        Check(playerApply.Valid && Math.Abs(playerApply.Pitch - 1.2) < .000001 && playerApply.Volume == .25,
            "S4 real player Apply overrides entry Skip after author parameters and enablement resolve");
        var invalidSource = new XmlDocument();
        invalidSource.LoadXml("<declaration family='Pawn' target='Ratkin'><action key='Work'><response key='us.core/Age' enabled='true' participation='Apply'/></action></declaration>");
        bool rejectedParticipation = false;
        try
        {
            new BehaviorSourceDeclaration().LoadDataFromXmlCustom(invalidSource.DocumentElement!);
        }
        catch (ArgumentException)
        {
            rejectedParticipation = true;
        }
        Check(rejectedParticipation, "S4 author behavior source cannot declare a player participation override; field rejected explicitly");
        var player = new BehaviorValues { MoodEnabled = false };
        player.Participation["example/Response"] = false;
        effective = restored.Resolve(source.Source.Domain, "Work", directory, new[] { player }, _ => true, out _);
        plan = ResponseModulation.Resolve("p1", effective, facts, catalog.Responses);
        result = ResponseModulation.Compose(2, 3, plan, catalog.Audio[0].Participation, new Driver());
        Check(result.Valid && Math.Abs(result.Pitch - 2.4) < .000001 && result.Volume == 1.5,
            "S4 player explicit Skip wins entry Apply independently of player disabling only mood");
        plan.Rules = Array.Empty<IUsResponseRule>();
        Check(!ResponseModulation.Compose(1, 1, plan, new Dictionary<string, bool>(), new Driver()).Valid,
            "S4 missing enabled response implementation rejects, never replaces its contribution with identity");
        S4ResponseFileExample();
    }

    private static void S4ResponseFileExample()
    {
        var file = new XmlDocument();
        file.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "examples", "Responses.xml"));
        var action = new ActionDeclaration();
        action.LoadDataFromXmlCustom(file.SelectSingleNode("Defs/UniversalSqueaker.UsActionDef/declaration")!);
        var pack = new VoicePackDeclaration();
        pack.LoadDataFromXmlCustom(file.SelectSingleNode("Defs/UniversalSqueaker.UsVoicePackDef/declaration")!);
        var behavior = new BehaviorSourceDeclaration();
        behavior.LoadDataFromXmlCustom(file.SelectSingleNode("Defs/UniversalSqueaker.UsBehaviorSourceDef/declaration")!);
        var catalog = new AuthorCatalog(120, 120, true);
        Check(catalog.AddAction(action) && catalog.AddPack("example", "ResponseAudio", pack, name => "example/" + name),
            "S4 real Responses.xml ordinary loaders assemble exact action, pack-local participation and calibrated entries");
        var input = Input();
        input.Actions = catalog.Actions;
        input.Audio = catalog.Audio;
        var effective = behavior.Source.Contributions.Single(x => x.Action == "Work").Values;
        input.ActionModulation = new Dictionary<string, ModulationPlan>
        {
            [action.Key] = ResponseModulation.Resolve("p1", effective, new Dictionary<string, double>
                { ["age.native"] = 1.2, ["age.nativeVolume"] = 1, ["mood.level"] = .9 }, catalog.Responses)
        };
        var driver = new Driver();
        var outcome = new OneShotDecision().Run(new DecisionState(1000), new StatePolicy(40, 20), input, driver, driver);
        Check(outcome.Receipt?.Result == PlaybackResult.Dispatched && driver.LastRequest?.Sustained == true
            && driver.Purposes.Count(x => x == RandomPurpose.ModulationJitter) == 1,
            "S4 real author file reaches selected sustained-shaped production request with one composed response/jitter pass");
    }
}
