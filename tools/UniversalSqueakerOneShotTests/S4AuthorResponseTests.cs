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
            + "<playback>Sustained</playback><participation><li key='example/Response'>Apply</li></participation></declaration>");
        var action = new ActionDeclaration();
        action.LoadDataFromXmlCustom(xml.DocumentElement!);
        Check(catalog.AddAction(action) && catalog.Actions.Single(x => x.Key == action.Key).Sustained,
            "S5 normal action loader carries explicit sustained shape into the production plan");
        xml.LoadXml("<declaration><race>Ratkin</race><participation><li key='example/Response'>Skip</li></participation>"
            + "<entries><li><key>a</key><action>example/Work</action><sound>Work</sound><pitch>2</pitch></li>"
            + "<li><key>b</key><action>example/Work</action><sound>Work</sound>"
            + "<participation><li key='example/Response'>Skip</li></participation></li></entries></declaration>");
        var pack = new VoicePackDeclaration();
        pack.LoadDataFromXmlCustom(xml.DocumentElement!);
        Check(catalog.AddPack("example", "Pack", pack, _ => "audio/Work")
            && catalog.Audio[0].Pitch == 2 && catalog.Audio[0].Participation["example/Response"]
            && !catalog.Audio[1].Participation["example/Response"],
            "S4 normal pack/action/entry defaults fold once; entry calibration pitch remains independent");

        xml.LoadXml("<declaration family='Pawn' target='Ratkin'><action key='Work' ageEnabled='true' moodEnabled='true'>"
            + "<response key='example/Response' enabled='true' participation='Apply'/>"
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
    }
}
