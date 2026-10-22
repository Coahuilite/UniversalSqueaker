using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UniversalSqueaker;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;
internal static partial class Program
{
    private static void DbEffectiveConfig()
    {
        Scenario("DB-production-settings-ExposeData-atomic-file-with-Scribe-double-roundtrip");
        var settings = NewSettings();
        var race = new BehaviorDomain("Pawn", "", "Ratkin");
        var child = new BehaviorDomain("Pawn", "", "Ratkin", "LegalXeno");
        var other = new BehaviorDomain("Pawn", "", "Kiiro");
        var n1 = new BehaviorDomain("NonPawn", "example.one", "Relay");
        var n2 = new BehaviorDomain("NonPawn", "example.two", "Relay");
        var author = new BehaviorSource { Id = "author/A", Domain = race, Contributions = new() {
            new() { Action = "us.core/Eat", Values = new BehaviorValues { Phase = EatPhase.Execution, Volume = .7, Enabled = true } } } };
        var xeno = author.Copy(); xeno.Domain = child; xeno.Contributions[0].Values.Volume = .9;
        var directory = new BehaviorSourceDirectory(new[] { author, xeno });
        var defaults = new BehaviorValues { Phase = EatPhase.WholeProcess, NutritionScope = EatNutritionScope.Any, DrugScope = EatDrugScope.Any, Volume = 1, Enabled = true };
        string folder = Path.Combine(Path.GetTempPath(), "db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "settings.xml");
        void Serialize(string stage) => SafeSaver.Save(stage, "SettingsBlock", () => { var value = settings; Scribe_Deep.Look(ref value, "ModSettings"); });
        bool Save() => settings.TryWriteBehaviorCandidate(path, settings.behaviorSourcesXml, Serialize, out _);
        UniversalSqueakerSettings Reload()
        {
            UniversalSqueakerSettings result = null!;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref result, "ModSettings"); Scribe.loader.FinalizeLoading();
            return result;
        }
        try
        {
            Check(settings.TryChangeBehaviorSource(race, author.Id, directory, xml => settings.TryWriteBehaviorCandidate(path, xml, Serialize, out _), out _)
                && settings.TryChangeBehaviorSource(child, xeno.Id, directory, xml => settings.TryWriteBehaviorCandidate(path, xml, Serialize, out _), out _),
                "DB parent and legal child author source persisted before player layers", ref failures);
            Check(settings.TryChangeBehaviorOverride("Pawn", "", "", "", "us.core/Eat",
                new BehaviorValues { Enabled = false, Volume = 0, HardIntervalTicks = 0, Phase = EatPhase.WholeProcess, NutritionScope = EatNutritionScope.Any, DrugScope = EatDrugScope.Any },
                Save, out _), "DB player Pawn service profile commits false/zero/independent Eat defaults", ref failures);
            var captured = settings.CaptureBehaviorConfiguration();
            var effective = captured.Resolve(child, "us.core/Eat", defaults, directory, _ => true);
            Check(!effective.Paused && effective.Read().Enabled == false && effective.Read().Volume == 0 && effective.Read().HardIntervalTicks == 0
                && effective.Read().Phase == EatPhase.WholeProcess && effective.Origins["volume"] == "player:Pawn::profile" && effective.Dependencies.Count == 0,
                "DB all authors precede player Pawn; Any/Whole adds no optional capability requirement", ref failures);
            Check(settings.TryChangeBehaviorOverride("Pawn", "", race.Target, "", "us.core/Eat", new BehaviorValues { Volume = .4 }, Save, out _)
                && settings.TryChangeBehaviorOverride("Pawn", "", child.Target, child.Child, "us.core/Eat", new BehaviorValues {
                    Phase = EatPhase.Execution, NutritionScope = EatNutritionScope.NonNutritionGiving, DrugScope = EatDrugScope.OnlyDrugs }, Save, out _),
                "DB sparse Race and legal Xeno save independently", ref failures);
            var next = Reload().CaptureBehaviorConfiguration();
            var resolved = next.Resolve(child, "us.core/Eat", defaults, directory, _ => true);
            Check(next.Id != captured.Id && resolved.Read().Volume == .4 && resolved.Read().Enabled == false
                && resolved.Read().Phase == EatPhase.Execution && resolved.Dependencies.SequenceEqual(new[] { EatPolicy.Execution, EatPolicy.Drug, EatPolicy.Nutrition }.OrderBy(x => x, StringComparer.Ordinal)),
                "DB production ExposeData + atomic file + Scribe double restore every field/presence and selected dependencies", ref failures);
            Check(captured.Resolve(child, "us.core/Eat", defaults, directory, _ => true).Read().Volume == 0,
                "DB old decision view remains immutable across publication", ref failures);
            var leak = resolved.Read(); leak.Volume = 7;
            Check(resolved.Read().Volume == .4, "DB accessors cannot mutate captured effective values", ref failures);
            Check(next.Resolve(child, "us.core/Eat", defaults, directory, key => key != EatPolicy.Nutrition).Paused
                && !next.Resolve(race, "us.core/Eat", defaults, directory, _ => false).Paused,
                "DB missing selected nutrition pauses precise child only, unselected optional branch stays usable", ref failures);
            Check(next.Resolve(other, "us.core/Eat", defaults, directory, _ => true).Read().Volume == 0,
                "DB Kiiro receives only player Pawn service profile, never Ratkin author/Race/Xeno", ref failures);
            string file = File.ReadAllText(path);
            var before = settings.CaptureBehaviorConfiguration();
            Check(!settings.TryChangeBehaviorOverride("Pawn", "", race.Target, "", "us.core/Eat", new BehaviorValues { Volume = .8 },
                () => settings.TryWriteBehaviorCandidate(path, settings.behaviorSourcesXml, stage => { File.WriteAllText(stage, "partial"); throw new IOException("DB injected serialization"); }, out _),
                out _), "DB injected actual atomic serialization failure rejects candidate", ref failures);
            Check(File.ReadAllText(path) == file && settings.CaptureBehaviorConfiguration().Resolve(race, "us.core/Eat", defaults, directory, _ => true).Read().Volume
                == before.Resolve(race, "us.core/Eat", defaults, directory, _ => true).Read().Volume,
                "DB failed player transaction preserves previous disk and in-memory effective configuration", ref failures);
            Check(settings.TryChangeBehaviorOverride("NonPawn", n1.Namespace, "", "", "example/Signal", new BehaviorValues { Enabled = false, Volume = 0 }, Save, out _)
                && settings.TryChangeBehaviorOverride("NonPawn", n2.Namespace, n2.Target, "", "example/Signal", new BehaviorValues { Enabled = true, Volume = .8 }, Save, out _),
                "DB two NonPawn namespaces have own profile and precise target rows", ref failures);
            var nonPawn = Reload().CaptureBehaviorConfiguration();
            Check(nonPawn.Resolve(n1, "example/Signal", new BehaviorValues(), directory, _ => true).Read().Volume == 0
                && nonPawn.Resolve(n2, "example/Signal", new BehaviorValues(), directory, _ => true).Read().Volume == .8
                && nonPawn.Resolve(n1, "us.core/Eat", new BehaviorValues(), directory, _ => true).Read().Phase == null,
                "DB same NonPawn target string never crosses namespace or acquires Pawn Eat fields", ref failures);
            Check(!settings.TryChangeBehaviorOverride("NonPawn", n1.Namespace, n1.Target, "", "example/Signal",
                new BehaviorValues { Phase = EatPhase.Execution }, Save, out _), "DB explicit Pawn-only NonPawn contribution rejected, not silently omitted", ref failures);
            settings.voicePackMode = SqueakVoicePackMode.Disabled; settings.globalVolumeFactor = 0;
            var off = settings.CaptureBehaviorConfiguration();
            Check(!off.SystemEnabled && off.SystemVolume == 0, "DB system enabled/G remain outside overridable domain fold", ref failures);
            settings.actionTuning.Add(new ActionTuningRecord { actionKey = "example/Bad", raceDefName = race.Target, hasEatPhase = true, eatPhase = (EatPhase)99 });
            Check(settings.CaptureBehaviorConfiguration().Resolve(race, "example/Bad", defaults, directory, _ => true).Paused,
                "DB illegal explicit enum retained as paused invalid input, never omission/default", ref failures);
            Check(settings.CaptureBehaviorConfiguration().Resolve(race, "example/Unrelated", new BehaviorValues(), directory, _ => true).Paused == false,
                "DB bad action does not pause independent configuration", ref failures);
            settings.ResetActionTuningRow("Eat", child.Target, child.Child);
            Check(settings.CaptureBehaviorConfiguration().Resolve(child, "us.core/Eat", defaults, directory, _ => true).Read().Phase == EatPhase.WholeProcess,
                "DB legacy visible Eat row reset clears canonical full-ActionId Xeno fields, preserving author/player-parent fold", ref failures);
            settings.actionTuning.Add(new ActionTuningRecord { actionKey = "example/Legacy", raceDefName = race.Target,
                hasProbabilityMultiplier = true, probabilityMultiplier = .5f, sourcePresetDefName = "OriginalPreset" });
            Check(settings.TryChangeBehaviorOverride("Pawn", "", race.Target, "", "example/Legacy", new BehaviorValues { Volume = .2 }, Save, out _)
                && settings.actionTuning.Any(x => x.actionKey == "example/Legacy" && x.hasProbabilityMultiplier && x.probabilityMultiplier == .5f && x.sourcePresetDefName == "OriginalPreset"),
                "DB new behavior transaction preserves numeric/provenance fields still consumed by retained callers", ref failures);
            settings.ResetActionTuningArea("", "");
            Check(settings.actionTuning.Count(x => x.serviceFamily == "NonPawn") == 2,
                "DB Pawn profile reset cannot remove either NonPawn namespace's own rows", ref failures);
        }
        finally { Directory.Delete(folder, true); }
    }
}
