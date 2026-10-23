using System;
using System.Collections.Generic;
using System.IO;
using UniversalSqueaker;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;

internal static partial class Program
{
    private static void S4ResponseConfigAndReset()
    {
        Scenario("S4-response-Config-effective-layers-and-precise-Reset");
        var settings = NewSettings();
        settings.SetActivity(2.5);
        settings.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "us.core/Work",
            responses = new List<ResponseTuningValue>
            {
                new() { key = "us.core/Age", hasEnabled = true, enabled = true },
                new() { key = "us.core/Mood", hasEnabled = true, enabled = true }
            },
            responseParameters = new List<ResponseParameterValue> { new() { key = "mood.low", value = .2 }, new() { key = "mood.high", value = .8 } }
        });
        settings.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "us.core/Work", raceDefName = "FixtureRace",
            responses = new List<ResponseTuningValue> { new() { key = "us.core/Age", hasParticipation = true, apply = false } },
            responseParameters = new List<ResponseParameterValue> { new() { key = "age.override", value = 1.3 } }
        });
        settings.actionTuning.Add(new ActionTuningRecord
        {
            actionKey = "us.core/Work", raceDefName = "FixtureRace", xenotypeDefName = "FixtureXeno",
            responses = new List<ResponseTuningValue> { new() { key = "us.core/Mood", hasEnabled = true, enabled = false } }
        });
        var participation = new ResponseTuningValue { key = "us.core/Age", hasParticipation = true, apply = false };
        var calibration = new ResponseParameterValue { key = "age.override", value = 1.3 };
        settings.ReplaceResponseTuning("us.core/Work", "FixtureRace", "", new[] { participation }, new[] { calibration });
        calibration.value = 99;
        participation.apply = true;
        Check(settings.ResolveBehaviorSource(new BehaviorDomain("Pawn", "", "FixtureRace"), "us.core/Work",
                new BehaviorSourceDirectory(Array.Empty<BehaviorSource>()), _ => true, out _).ResponseParameters["age.override"] == 1.3,
            "S4 actual public response setter validates then deep-copies into the original persistence queue", ref failures);
        bool rejected = false;
        try
        {
            settings.ReplaceResponseTuning("us.core/Work", "FixtureRace", "", Array.Empty<ResponseTuningValue>(),
                new[] { new ResponseParameterValue { key = "mood.low", value = double.NaN } });
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected && settings.actionTuning[1].responseParameters[0].value == 1.3,
            "S4 invalid response setter input leaves original exact player record unchanged", ref failures);
        string folder = Path.Combine(Path.GetTempPath(), "us-s4-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "settings.xml");
            SafeSaver.Save(path, "Settings", () => settings.ExposeData());
            var loaded = NewSettings();
            Scribe.loader.InitLoading(path);
            loaded.ExposeData();
            Scribe.loader.FinalizeLoading();
            var domain = new BehaviorDomain("Pawn", "", "FixtureRace", "FixtureXeno");
            var empty = new BehaviorSourceDirectory(Array.Empty<BehaviorSource>());
            var effective = loaded.ResolveBehaviorSource(domain, "us.core/Work", empty, _ => true, out bool paused);
            Console.WriteLine("S4 config A=" + loaded.activity + " paused=" + paused + " age=" + effective.AgeEnabled
                + " mood=" + effective.MoodEnabled + " participation=" + effective.Participation.Count + " parameters=" + effective.ResponseParameters.Count);
            Check(loaded.activity == 2.5 && !paused && effective.AgeEnabled == true && effective.MoodEnabled == false
                && effective.Participation["us.core/Age"] == false && effective.ResponseParameters["age.override"] == 1.3
                && effective.ResponseParameters["mood.low"] == .2,
                "S4 actual Scribe reload and Settings resolver preserve positive A, separate enablement/participation and Global->Race->Xeno parameters", ref failures);
            var neighbor = loaded.ResolveBehaviorSource(new BehaviorDomain("Pawn", "", "Neighbor", "FixtureXeno"), "us.core/Work", empty, _ => true, out _);
            Check(neighbor.AgeEnabled == true && neighbor.MoodEnabled == true && neighbor.Participation.Count == 0
                && !neighbor.ResponseParameters.ContainsKey("age.override"), "S4 response player records do not cross exact race or xenotype domain", ref failures);
            loaded.SetActionTuningScope("us.core/Work", "FixtureRace", "", null);
            effective = loaded.ResolveBehaviorSource(domain, "us.core/Work", empty, _ => true, out _);
            Console.WriteLine("S4 reset participation=" + effective.Participation.Count + " mood=" + effective.MoodEnabled);
            Check(effective.Participation["us.core/Age"] == false, "S4 clearing unrelated scope cannot delete a response-only record", ref failures);
            loaded.ResetActionTuningRow("us.core/Work", "FixtureRace", "");
            effective = loaded.ResolveBehaviorSource(domain, "us.core/Work", empty, _ => true, out _);
            Check(effective.Participation.Count == 0 && !effective.ResponseParameters.ContainsKey("age.override")
                && effective.MoodEnabled == false && effective.ResponseParameters["mood.high"] == .8,
                "S4 exact action Reset clears local response fields, preserves parent/child player layers", ref failures);
            loaded.actionTuning[0].responseParameters.Add(new ResponseParameterValue { key = "mood.low", value = .1 });
            loaded.PublishBehaviorConfiguration();
            loaded.ResolveBehaviorSource(domain, "us.core/Work", empty, _ => true, out paused);
            Check(paused, "S4 duplicate response parameters are an explicit unreadable contribution, not last-wins style", ref failures);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
