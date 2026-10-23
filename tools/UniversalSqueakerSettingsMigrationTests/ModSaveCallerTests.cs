using System;
using System.IO;
using System.Collections.Generic;
using UniversalSqueaker;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;

internal static partial class Program
{
    private static void SourceSaveUsesOriginalModGeneration()
    {
        Scenario("S3-real-Mod-caller-mixed-generation-stage-and-framework-flush");
        string originalDirectory = GenFilePaths.ConfigFolderPath;
        string folder = Path.Combine(Path.GetTempPath(), "us-s3-mod-caller-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        GenFilePaths.ConfigFolderPath = folder;
        var settings = NewSettings();
        SqueakerMod.Settings = settings;
        var mod = new SqueakerMod(new ModContentPack { FolderName = "US-Fixture" });
        var domain = new BehaviorDomain("Pawn", "", "Ratkin");
        var a = new BehaviorSource { Id = "author.a/Def", Domain = domain, Contributions = new List<BehaviorContribution>
            { new() { Action = "us.core/Work", Values = new BehaviorValues { Enabled = false } } } };
        var b = a.Copy(); b.Id = "author.b/Def";
        SqueakerRuntime.BehaviorSources = new BehaviorSourceDirectory(new[] { a, b });
        string path = Path.Combine(folder, "Mod_US-Fixture_SqueakerMod.xml");
        Mod.BaseWrites = 0;
        try
        {
            settings.SetGlobalVolume(.4f); // Actual Settings setter -> original Mod queue, not a manual counter seed.
            Check(mod.IsSettingsDirty && mod.SaveQueueRequestCount == 1, "caller: ordinary production setter queues original generation", ref failures);
            Check(mod.ChangeBehaviorSource(domain, a.Id, out string failure) && failure == "", "caller: actual Mod source operation stages and commits entire SettingsBlock", ref failures);
            long writes = mod.PhysicalSaveCount;
            Check(writes == 1 && !mod.IsSettingsDirty && mod.SaveState == SettingsSaveState.Saved,
                "caller: source commit confirms included ordinary generation and final save state", ref failures);
            mod.WriteSettings();
            Check(mod.PhysicalSaveCount == writes && Mod.BaseWrites == 0,
                "caller: framework/close forced flush does not duplicate generation already serialized by source commit", ref failures);
            Check(File.ReadAllText(path).Contains("0.4") && File.ReadAllText(path).Contains("author.a/Def"),
                "caller: same actual staged file contains both ordinary edit and bound source", ref failures);

            settings.SetGlobalVolume(.5f);
            string beforeFile = File.ReadAllText(path);
            string beforeSource = settings.behaviorSourcesXml;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Check(!mod.ChangeBehaviorSource(domain, b.Id, out _) && mod.IsSettingsDirty
                    && mod.PhysicalSaveCount == writes && settings.behaviorSourcesXml == beforeSource,
                    "caller: failed atomic replacement does not confirm ordinary generation or source binding", ref failures);
            }
            Check(File.ReadAllText(path) == beforeFile, "caller: failed source commit preserves original target bytes", ref failures);
            mod.WriteSettings();
            Check(mod.PhysicalSaveCount == writes + 1 && Mod.BaseWrites == 1 && !mod.IsSettingsDirty,
                "caller: original pending generation remains writable after source failure", ref failures);

            settings.SetGlobalVolume(.6f);
            Mod.FailWrite = true;
            mod.WriteSettings();
            Mod.FailWrite = false;
            Check(mod.SaveState == SettingsSaveState.Failed && mod.IsSettingsDirty,
                "caller: original serializer failure retains its ordinary generation", ref failures);
            Check(mod.ChangeBehaviorSource(domain, b.Id, out _) && !mod.IsSettingsDirty
                && mod.SaveState == SettingsSaveState.Saved,
                "caller: successful source serialization consumes covered Failed generation and clears stale Failed state", ref failures);

            settings.SetGlobalVolume(.65f);
            ScribeSaver.BeforeFinalize = () => settings.SetGlobalVolume(.7f);
            Check(mod.ChangeBehaviorSource(domain, a.Id, out _) && mod.IsSettingsDirty
                && mod.SaveState == SettingsSaveState.Saving,
                "caller: source commit only acknowledges captured generation, preserves edit queued during serialization", ref failures);
            long beforeLaterFlush = mod.PhysicalSaveCount;
            mod.WriteSettings();
            var saved = new System.Xml.XmlDocument();
            saved.Load(path);
            float savedVolume = float.Parse(saved.SelectSingleNode("SettingsBlock/ModSettings/outputVolume")!.InnerText,
                System.Globalization.CultureInfo.InvariantCulture);
            Check(mod.PhysicalSaveCount == beforeLaterFlush + 1 && !mod.IsSettingsDirty && Math.Abs(savedVolume - .7f) < .0001f,
                "caller: reentrant later generation is saved once by original framework queue", ref failures);

            GameRuntime.FailPublish = true;
            Check(mod.ChangeBehaviorSource(domain, b.Id, out string publishFailure)
                && publishFailure == "Committed:RuntimePublishFailed:InvalidOperationException"
                && BehaviorSourceState.FromXml(settings.behaviorSourcesXml).Read(domain).SourceId == b.Id
                && File.ReadAllText(path).Contains("author.b/Def"),
                "caller: post-commit runtime failure reports committed truth and keeps committed memory/file", ref failures);
            GameRuntime.FailPublish = false;
            long committedWrites = mod.PhysicalSaveCount;
            mod.WriteSettings();
            Check(mod.PhysicalSaveCount == committedWrites && !mod.IsSettingsDirty,
                "caller: publish failure does not cause successful disk transaction to repeat", ref failures);

            settings.SetGlobalVolume(.25f);
            Check(mod.TryChangeBehaviorOverride("Pawn", "", "Ratkin", "", "us.core/Eat",
                new BehaviorValues { Enabled = false, Volume = 0, HardIntervalTicks = 0, Phase = EatPhase.Execution,
                    NutritionScope = EatNutritionScope.NonNutritionGiving, DrugScope = EatDrugScope.OnlyDrugs }, out _)
                && !mod.IsSettingsDirty && mod.PhysicalSaveCount == committedWrites + 1,
                "DB actual Mod player operation commits new row and original pending generation once", ref failures);
            string playerFile = File.ReadAllText(path);
            Check(playerFile.Contains("Execution") && playerFile.Contains("NonNutritionGiving") && playerFile.Contains("OnlyDrugs")
                && playerFile.Contains("author.b/Def"), "DB actual complete Settings serialization includes Eat axes and source intent together", ref failures);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Check(!mod.TryChangeBehaviorOverride("Pawn", "", "Ratkin", "", "us.core/Eat",
                    new BehaviorValues { Phase = EatPhase.WholeProcess }, out _)
                    && settings.actionTuning.Find(x => x.actionKey == "us.core/Eat")!.eatPhase == EatPhase.Execution,
                    "DB actual Mod failed replacement preserves previous player row and does not publish new view", ref failures);
            }
            Check(File.ReadAllText(path) == playerFile, "DB actual Mod player failure preserves committed Config copy", ref failures);
        }
        finally
        {
            ScribeSaver.BeforeFinalize = null;
            GameRuntime.FailPublish = false;
            Mod.FailWrite = false;

            GenFilePaths.ConfigFolderPath = originalDirectory;
            Directory.Delete(folder, true);
        }
    }
}
