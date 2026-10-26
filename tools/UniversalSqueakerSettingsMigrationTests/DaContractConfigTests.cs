using System;
using System.Collections.Generic;
using System.IO;
using UniversalSqueaker;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;

internal static partial class Program
{
    private static void DaCompleteActionConfig()
    {
        Scenario("DA-full-action-source-Scribe-reset-and-service-family");
        const string ordinary = "us.core/Select", child = "example/InjuredSelect";
        var settings = NewSettings();
        var race = new BehaviorDomain("Pawn", "", "Ratkin");
        var armor = new BehaviorDomain("NonPawn", "armor", "A");
        var bench = new BehaviorDomain("NonPawn", "bench", "A");
        BehaviorSource Source(string id, BehaviorDomain domain, double volume) => new()
        {
            Id = id, Domain = domain, Contributions = new List<BehaviorContribution>
            {
                new() { Action = ordinary, Values = new BehaviorValues { Enabled = true, Volume = volume } },
                new() { Action = child, Values = new BehaviorValues { Enabled = true, Volume = .25, InteractionIntervalSeconds = 50 } }
            }
        };
        var sources = new[] { Source("author/Pawn", race, 0), Source("author/Armor", armor, 7), Source("author/Bench", bench, 9) };
        var directory = new BehaviorSourceDirectory(sources);
        foreach (var source in sources)
            Check(settings.TryChangeBehaviorSource(source.Domain, source.Id, directory, _ => true, out _),
                "DA bind exact source " + source.Id, ref failures);
        settings.SetActionTuningScope(ordinary, "", "", BehaviorScope.Disabled);
        settings.SetActionTuningScope(child, "Ratkin", "", BehaviorScope.AnyOccurrence);
        settings.ReplaceResponseTuning(child, "Ratkin", "",
            new[] { new ResponseTuningValue { key = "us.core/Age", hasEnabled = true, enabled = false } },
            new[] { new ResponseParameterValue { key = "age.overrideVolume", value = 0 } });
        string folder = Path.Combine(Path.GetTempPath(), "us-da-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "settings.xml");
            SafeSaver.Save(file, "Settings", () => settings.ExposeData());
            var loaded = NewSettings();
            Scribe.loader.InitLoading(file);
            loaded.ExposeData();
            Scribe.loader.FinalizeLoading();
            var missing = new BehaviorSourceDirectory(Array.Empty<BehaviorSource>());
            var plain = loaded.ResolveBehaviorSource(race, ordinary, missing, _ => true, out bool paused);
            var injured = loaded.ResolveBehaviorSource(race, child, missing, _ => true, out _);
            Check(!paused && plain.Enabled == true && plain.Scope == BehaviorScope.Disabled && plain.Volume == 0 && injured.Enabled == true
                && injured.Volume == .25 && injured.InteractionIntervalSeconds == 50 && injured.AgeEnabled == false
                && injured.ResponseParameters["age.overrideVolume"] == 0,
                "DA production settings/Scribe-double reload preserves separate full actions, author snapshot and player false/zero", ref failures);
            var configuration = loaded.CaptureBehaviorConfiguration();
            Check(!UniversalSqueaker.Responses.BehaviorContract.Authorized(configuration,
                    configuration.Resolve(race, ordinary, new BehaviorValues(), missing, _ => true), plain,
                    new UniversalSqueaker.Responses.ResponseFrame { Eligible = true })
                && UniversalSqueaker.Responses.BehaviorContract.Authorized(configuration,
                    configuration.Resolve(race, child, new BehaviorValues(), missing, _ => true), injured,
                    new UniversalSqueaker.Responses.ResponseFrame { Eligible = true }),
                "DA disabled scope blocks actual core authorization without synthesizing an explicit Enabled field or disabling the child", ref failures);
            foreach (var target in new[] { armor, bench })
            {
                var value = loaded.ResolveBehaviorSource(target, ordinary, missing, _ => true, out paused);
                Check(!paused && value.Enabled == true && value.Scope == null && value.Volume == (target.Namespace == "armor" ? 7 : 9),
                    "DA Pawn global behavior cannot override NonPawn source " + target.Namespace, ref failures);
            }
            string sourceBytes = loaded.behaviorSourcesXml;
            loaded.ResetActionTuningRow(child, "Ratkin", "");
            injured = loaded.ResolveBehaviorSource(race, child, missing, _ => true, out _);
            Check(injured.Volume == .25 && injured.ResponseParameters.Count == 0 && injured.AgeEnabled == null
                && loaded.behaviorSourcesXml == sourceBytes
                && loaded.ResolveBehaviorSource(race, ordinary, missing, _ => true, out _).Scope == BehaviorScope.Disabled,
                "DA exact child reset clears only its player fields; parent/source intent and snapshots survive", ref failures);
        }
        finally { Directory.Delete(folder, true); }
    }
}
