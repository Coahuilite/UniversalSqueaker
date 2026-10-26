using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;
internal static partial class Program
{
    private static void NewFormatAndPreset()
    {
        Scenario("r2-new-physical-handle-sparse-player-templates-no-old-reader");
        var settings = NewSettings();
        string folder = Path.Combine(Path.GetTempPath(), "us-r2-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "settings.xml");
        void Save(GameSettings value) => SafeSaver.Save(file, "SettingsBlock", () => Scribe_Deep.Look(ref value, "ModSettings"));
        GameSettings Read()
        {
            GameSettings value = null!; Scribe.loader.InitLoading(file); Scribe_Deep.Look(ref value, "ModSettings");
            Scribe.loader.FinalizeLoading(); return value;
        }
        try
        {
            Save(settings);
            string fresh = File.ReadAllText(file);
            Check(fresh.Contains("<responseConfigurationFormat>1</responseConfigurationFormat>")
                && typeof(GameSettings).Name == "GameSettings" && typeof(SqueakerMod).Name == "SqueakerMod",
                "new forced format marker and different framework physical handle", ref failures);
            File.WriteAllText(file, fresh.Replace("<responseConfigurationFormat>1</responseConfigurationFormat>", ""));
            var old = Read();
            Check(old.IsConfigurationUnreadable && !old.CaptureBehaviorConfiguration().SystemEnabled,
                "missing new marker is unreadable, not an old-reader/default migration", ref failures);
            Check(!old.TryReplaceBehaviorOverride("Pawn", "", "R", "", CoreActions.Work, new BehaviorValues(), () => true, out _),
                "unreadable format cannot be silently overwritten by an atomic edit", ref failures);
            Check(!settings.TryReplaceBehaviorOverride("Pawn", "", "R", "", "Work", new BehaviorValues(), () => true, out _),
                "short action spelling is not another internal identity", ref failures);

            var doc = new XmlDocument();
            doc.LoadXml("<li key='us.core/Work' enabled='false' volume='0' simulationIntervalTicks='0'><parameter key='mood.low' value='0'/><participation key='us.core/Age' apply='false'/></li>");
            var parsed = new PresetActionValues(); parsed.LoadDataFromXmlCustom(doc.DocumentElement!);
            Check(parsed.ActionId == CoreActions.Work && parsed.Values.Enabled == false && parsed.Values.Volume == 0
                && parsed.Values.SimulationIntervalTicks == 0 && parsed.Values.Scope == null && !parsed.Values.Participation["us.core/Age"],
                "template XML uses the current source value grammar with false/zero/absence/participation", ref failures);
            var preset = new UsBehaviorPresetDef { defName = "TemplateA", races = new List<PresetRaceEntry>
            {
                new() { raceDefName = "R1", actions = new() { parsed }, xenotypes = new()
                    { new() { xenotypeDefName = "SharedX", actions = new() { new PresetActionValues(CoreActions.Work, new BehaviorValues { Pitch = 2 }) } } } },
                new() { raceDefName = "R2", actions = new() { new PresetActionValues(CoreActions.Eat, new BehaviorValues { DrugScope = EatDrugScope.OnlyDrugs }) },
                    xenotypes = new() { new() { xenotypeDefName = "SharedX", actions = new() { new PresetActionValues(CoreActions.Work, new BehaviorValues { Volume = 9 }) } } } }
            } };
            var domain = new BehaviorDomain("Pawn", "", "R1");
            var source = new BehaviorSource { Id = "author/Source", Domain = domain, Contributions = new()
                { new() { Action = CoreActions.Work, Values = new BehaviorValues { Volume = .8 } } } };
            var directory = new BehaviorSourceDirectory(new[] { source });
            Check(settings.TryChangeBehaviorSource(domain, source.Id, directory, _ => true, out _), "bind source independently before player-template import", ref failures);
            string authorBytes = settings.behaviorSourcesXml;
            settings.ReplacePlayerValues(CoreActions.Work, "R1", "SharedX", new BehaviorValues { TalkingEnabled = false, Pitch = 3 }, "");
            var selection = new BehaviorPresetImporter.Selection();
            selection.XenotypeDomainKeys.Add(BehaviorPresetImporter.XenotypeDomainKey("R1", "SharedX"));
            var imported = settings.ImportBaselinePreset(preset, selection);
            var own = settings.ReadPlayerActionOverride(CoreActions.Work, "R1", "SharedX");
            Check(imported.ActionsImported == 1 && own.Enabled == false && own.Volume == 0 && own.SimulationIntervalTicks == 0
                && own.Pitch == 2 && own.TalkingEnabled == false && own.Scope == null,
                "explicit incremental import overlays only template-present fields over the exact player's current row", ref failures);
            Check(settings.behaviorSourcesXml == authorBytes && settings.ReadPlayerActionOverride(CoreActions.Work, "R2", "SharedX").Volume == null
                && settings.ReadPlayerActionOverride(CoreActions.Work, "R1", "").Volume == null,
                "same-named xenotypes do not cross race; player templates never replace/bind author sources", ref failures);
            settings.ImportBaselinePreset(preset, selection);
            Check(settings.actionTuning.Count == 1 && settings.actionTuning.Single().sourcePresetDefName == preset.defName,
                "repeat import is idempotent and stores a reset target, not a provenance claim", ref failures);
            settings.SetActionTuningScope(CoreActions.Work, "R1", "SharedX", BehaviorScope.Disabled);
            settings.SetActionTuningScope(CoreActions.Work, "R1", "SharedX", null);
            Check(settings.actionTuning.Single().sourcePresetDefName == preset.defName && settings.ReadPlayerActionOverride(CoreActions.Work, "R1", "SharedX").Volume == 0,
                "clear one scope field preserves template anchor and other sparse response fields", ref failures);
            settings.ReplacePlayerValues(CoreActions.Work, "R1", "SharedX", new BehaviorValues { Volume = 7, TalkingEnabled = true }, preset.defName);
            Check(settings.ResetActionTuningToPreset(CoreActions.Work, "R1", "SharedX", preset)
                && settings.ReadPlayerActionOverride(CoreActions.Work, "R1", "SharedX").Volume == 0
                && settings.ReadPlayerActionOverride(CoreActions.Work, "R1", "SharedX").TalkingEnabled == true,
                "reset-to-template re-applies current template fields while preserving unrelated player fields", ref failures);
            Save(settings); var restored = Read();
            Check(restored.actionTuning.Single().sourcePresetDefName == preset.defName
                && restored.ReadPlayerActionOverride(CoreActions.Work, "R1", "SharedX").Volume == 0
                && !restored.ReadPlayerActionOverride(CoreActions.Work, "R1", "SharedX").Participation["us.core/Age"]
                && restored.behaviorSourcesXml == authorBytes,
                "production settings/ExposeData with Scribe doubles restores sparse template and original author identity", ref failures);
            var invalid = new PresetActionValues(CoreActions.Work, new BehaviorValues { Pitch = 1 }); invalid.Values.Pitch = double.NaN;
            preset.races[0].xenotypes[0].actions.Add(invalid);
            var previous = settings.actionTuning;
            bool rejected = false; try { settings.ImportBaselinePreset(preset, selection); } catch (ArgumentException) { rejected = true; }
            Check(rejected && ReferenceEquals(previous, settings.actionTuning), "invalid template leaves entire original player candidate and publication unchanged", ref failures);
            Check(!GameSettings.TryFindActionBaseline(preset, CoreActions.Work, "R1", "SharedX", out _),
                "invalid current template is an unavailable UI reset target, not a thrown read-model exception", ref failures);
            Check(restored.ResetActionTuningRow(CoreActions.Work, "R1", "SharedX") == SqueakResetOutcome.Applied
                && restored.actionTuning.Count == 0 && restored.behaviorSourcesXml == authorBytes,
                "reset-default differs from clear and reset-template: removes only exact player row and reset anchor", ref failures);
            Check(new DefinitionCatalog(120, 120, true).Actions.Length == 15
                && typeof(GameSettings).GetField("allowBabyActions") == null && typeof(GameSettings).GetField("moodTuning") == null,
                "fifteen official packages; retired dedicated response and unused mood-table storage stay absent", ref failures);
        }
        finally { Directory.Delete(folder, true); }
    }
}
