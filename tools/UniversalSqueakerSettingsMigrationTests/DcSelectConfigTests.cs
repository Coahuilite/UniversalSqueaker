using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using UniversalSqueaker;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;

internal static partial class Program
{
    private static void DcSelectConfiguration()
    {
        Scenario("DC-Select-production-settings-ExposeData-atomic-file-with-Scribe-double");
        var settings = NewSettings();
        var domain = new BehaviorDomain("Pawn", "", "SelectRace", "LegalXeno");
        var doc = new XmlDocument();
        doc.LoadXml("<declaration family='Pawn' namespace='' target='SelectRace'><action key='us.core/Select' selectRequirePlayerControlled='false' selectRequireAwake='true' selectRequireNotDowned='false'/></declaration>");
        var author = BehaviorSourceState.ReadSource(doc.DocumentElement!, domain.Parent, "author/select");
        var directory = new BehaviorSourceDirectory(new[] { author });
        var source = new BehaviorSourceState();
        Check(source.Bind(domain.Parent, author.Id, directory), "DC Select parses independent author boolean fields", ref failures);
        settings.behaviorSourcesXml = source.ToXml();
        var restoredSource = BehaviorSourceState.FromXml(settings.behaviorSourcesXml);
        Check(restoredSource.Read(domain.Parent).Snapshot!.Contributions.Single().Values.SelectRequirePlayerControlled == false
            && restoredSource.ToXml() == source.ToXml(), "DC Select author snapshot preserves explicit false and sparse keys", ref failures);
        string folder = Path.Combine(Path.GetTempPath(), "dc-select-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "settings.xml");
        void Serialize(string file) => SafeSaver.Save(file, "SettingsBlock", () => { var value = settings; Scribe_Deep.Look(ref value, "ModSettings"); });
        bool Save() => settings.TryWriteBehaviorCandidate(path, settings.behaviorSourcesXml, Serialize, out _);
        GameSettings Reload() {
            GameSettings value = null!; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref value, "ModSettings"); Scribe.loader.FinalizeLoading(); return value;
        }
        try
        {
            Check(settings.TryReplaceBehaviorOverride("Pawn", "", "", "", "us.core/Select",
                new BehaviorValues { SelectRequirePlayerControlled = true, SelectRequireAwake = false }, Save, out _),
                "DC Select persist sparse Pawn service fields, including false", ref failures);
            Check(settings.TryReplaceBehaviorOverride("Pawn", "", "SelectRace", "", "us.core/Select",
                new BehaviorValues { SelectRequireAwake = true }, Save, out _) && settings.TryReplaceBehaviorOverride("Pawn", "", "SelectRace", "LegalXeno", "us.core/Select",
                new BehaviorValues { SelectRequireNotDowned = true }, Save, out _), "DC Select persist independent Race and Xeno fields", ref failures);
            var loaded = Reload();
            var effective = loaded.CaptureBehaviorConfiguration().Resolve(domain, "us.core/Select", SelectPolicy.OfficialDefaults, directory, _ => true);
            Check(!effective.Paused && effective.Read().SelectRequirePlayerControlled == true && effective.Read().SelectRequireAwake == true
                && effective.Read().SelectRequireNotDowned == true && effective.Origins["selectRequirePlayerControlled"] == "player:Pawn::profile"
                && effective.Origins["selectRequireAwake"] == "player:Pawn::SelectRace:"
                && effective.Origins["selectRequireNotDowned"] == "player:Pawn::SelectRace:LegalXeno",
                "DC Select production settings/ExposeData/atomic file with Scribe double restores three layers and exact provenance", ref failures);
            Check(!loaded.actionTuning.Single(x => x.xenotypeDefName == "LegalXeno").hasSelectRequireAwake
                && loaded.actionTuning.Single(x => x.raceDefName == "").hasSelectRequireAwake
                && !loaded.actionTuning.Single(x => x.raceDefName == "").selectRequireAwake,
                "DC Select omission and explicit false remain different after reload", ref failures);
            string previous = File.ReadAllText(path); var original = settings.actionTuning;
            Check(!settings.TryReplaceBehaviorOverride("Pawn", "", "SelectRace", "LegalXeno", "us.core/Select",
                new BehaviorValues { SelectRequireNotDowned = false }, () => settings.TryWriteBehaviorCandidate(path, settings.behaviorSourcesXml,
                    _ => throw new IOException("fixture serialization failure"), out _), out _)
                && ReferenceEquals(settings.actionTuning, original) && File.ReadAllText(path) == previous,
                "DC Select failed serialization restores prior live rows and committed settings file", ref failures);
            Check(settings.ResetNormalArea(SqueakNormalResetArea.Basics) == SqueakResetOutcome.Applied
                && !settings.ReadSelectParameterOverrides().SelectRequirePlayerControlled.HasValue
                && !settings.ReadSelectParameterOverrides().SelectRequireAwake.HasValue
                && settings.ReadSelectParameterOverrides("SelectRace").SelectRequireAwake == true
                && settings.ReadSelectParameterOverrides("SelectRace", "LegalXeno").SelectRequireNotDowned == true,
                "DC Select Basics clears only service presence, preserves exact Race/Xeno", ref failures);
            Check(settings.ResetActionTuningRow("us.core/Select", "SelectRace", "LegalXeno") == SqueakResetOutcome.Applied
                && !settings.ReadSelectParameterOverrides("SelectRace", "LegalXeno").SelectRequireNotDowned.HasValue
                && settings.behaviorSourcesXml == source.ToXml(), "DC Select exact row Reset clears player field but preserves author snapshot", ref failures);
            settings.SetSelectParameter("playerControlled", false); settings.SetSelectParameter("awake", false); settings.SetSelectParameter("notDowned", false);
            settings.SetSelectParameter("awake", null);
            Check(settings.ReadSelectParameterOverrides().SelectRequirePlayerControlled == false
                && !settings.ReadSelectParameterOverrides().SelectRequireAwake.HasValue && settings.ReadSelectParameterOverrides().SelectRequireNotDowned == false,
                "DC Select setting Inherit clears only its own field", ref failures);
            Check(!settings.TryReplaceBehaviorOverride("NonPawn", "vendor", "relay", "", "us.core/Select",
                new BehaviorValues { SelectRequireAwake = true }, Save, out _), "DC Select Pawn parameters reject NonPawn rather than impose a hidden gate", ref failures);
            var unrelated = settings.CaptureBehaviorConfiguration().Resolve(domain, "vendor/IndependentSelect", new BehaviorValues(), directory, _ => true);
            Check(!unrelated.Paused && !SelectPolicy.HasParameters(unrelated.Read()), "DC Select parent parameters do not leak into independent/child full ActionId", ref failures);
            var relaxed = new BehaviorConfiguration("", new[] { new BehaviorPlayerOverride("Pawn", "", "", "", "us.core/Select",
                new BehaviorValues { SelectRequirePlayerControlled = false, SelectRequireAwake = false, SelectRequireNotDowned = false }) }, true, 1, 1);
            Check(!relaxed.Resolve(domain, "us.core/Select", SelectPolicy.OfficialDefaults, directory, _ => false).Paused,
                "DC Select unselected optional qualifications add no implementation dependency", ref failures);
            var fresh = NewSettings().ReadSelectParameters();
            Check(fresh.SelectRequirePlayerControlled == true && fresh.SelectRequireAwake == true && fresh.SelectRequireNotDowned == true,
                "DC Select missing configuration has all three true defaults", ref failures);
        }
        finally {
            if (!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture cleanup escaped its isolated TEMP");
            Directory.Delete(folder, true);
        }
    }
}
