using System;
using System.Collections.Generic;
using System.IO;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;
using Verse;
namespace UniversalSqueaker.SettingsMigrationTests;
/// <summary>Production settings/ExposeData/atomic-file and composition caller with explicit Scribe/game doubles.</summary>
internal static partial class Program
{
    private static int failures, checks;
    private static int Main(string[] args)
    {
        try
        {
            Console.WriteLine("ARGS=["+string.Join(",",args)+"]");
            if(args.Length==1 && args[0]=="--public-template") { PublicTemplate(); return failures==0?0:1; }
            if(args.Length==1 && args[0]=="--commit-contract") {
                OrdinaryEditsAreNotDiskCommits(); SourceSaveUsesOriginalModGeneration();
                Console.WriteLine("COMMIT-CONTRACT actual Mod/settings/atomic file with Scribe doubles PASS="+(checks-failures)+" FAIL="+failures);
                return failures==0?0:1;
            }
            if(args.Length!=0) throw new ArgumentException("Unknown settings test mode");
            AudioDomainsRejectWhitespace(); GlobalVolumeDefaultsAndClamps(); GlobalVolumeScribeRoundTrip();
            SetDistanceRangeClampsAndMarksCustom(); UiSwatchRoundTripRepairAndOmission(); IntroCollapsedScribeRoundTrip();
            NewFormatAndPreset(); BehaviorSourceConfigTransaction(); S4ResponseConfigAndReset(); DaCompleteActionConfig();
            DbEffectiveConfig(); DcSelectConfiguration(); SourceSaveUsesOriginalModGeneration(); CanonicalScopeAndPublication();
            Console.WriteLine("Production settings/ExposeData/atomic-file with Scribe doubles: PASS=" + (checks-failures) + " FAIL=" + failures);
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static GameSettings NewSettings() => new();
    private static void PublicTemplate()
    {
        var doc=new System.Xml.XmlDocument();doc.Load(Path.Combine(Environment.CurrentDirectory,"docs","examples","0.6","PlayerTemplate.xml"));
        var nodes=doc.SelectNodes("//actions/li")!;int count=0;
        foreach(System.Xml.XmlNode node in nodes)
        {
            var value=new PresetActionValues();value.LoadDataFromXmlCustom(node);count++;
            Check(value.ActionId==CoreActions.Work && value.Values.Volume.HasValue && value.Values.Scope==null,
                "public player template uses exact fullID and preserves sparse scope absence",ref failures);
            if(value.Values.Volume==.7)Check(value.Values.Participation["us.core/Age"]==false,"public template explicit Skip survives production value loader",ref failures);
            else Check(value.Values.Volume==0 && !value.Values.Participation.ContainsKey("us.core/Age"),"public child template preserves zero and participation absence",ref failures);
        }
        Check(count==2,"both public exact player-template values were consumed",ref failures);
        Console.WriteLine("PUBLIC-TEMPLATE production value loader PASS="+(checks-failures)+" FAIL="+failures);
    }
    private static void CanonicalScopeAndPublication()
    {
        Scenario("r2-canonical-scope-and-single-configuration-publication");
        var settings = NewSettings();
        var before = settings.CaptureBehaviorConfiguration();
        settings.ApplyToRuntime();
        var after = settings.CaptureBehaviorConfiguration();
        Check(after.Id == before.Id + 1 && ReferenceEquals(after, settings.CaptureBehaviorConfiguration()),
            "one ApplyToRuntime publishes one captured configuration revision, not two", ref failures);
        settings.SetActionTuningScope(CoreActions.Equip, "R1", "", BehaviorScope.AnyOccurrence);
        settings.SetActionTuningScope(CoreActions.Work, "R1", "", BehaviorScope.AnyOccurrence);
        Check(settings.ReadPlayerActionOverride(CoreActions.Equip, "R1", "").Scope == BehaviorScope.ActiveCommand
            && settings.ReadPlayerActionOverride(CoreActions.Work, "R1", "").Scope == BehaviorScope.AnyOccurrence,
            "settings normalize command-only Equip but preserve supported Any Work through canonical contracts", ref failures);
    }
    private static void Scenario(string name) => Console.WriteLine("Scenario " + name);
    private static void Check(bool value, string name, ref int errors)
    {
        checks++; Console.WriteLine((value ? "PASS " : "FAIL ") + name); if (!value) errors++;
    }
    private static void AudioDomainsRejectWhitespace()
    {
        Scenario("7-audio-domains-reject-whitespace");
        Check(!AudioDomains.TryCreate("   ", "Xeno", out _),
            "audio-domains: whitespace-only race rejected", ref failures);
        Check(!AudioDomains.TryCreate("RaceA", "   ", out _),
            "audio-domains: whitespace-only xeno rejected", ref failures);
        Check(AudioDomains.TryCreate("RaceA", "", out AudioDomain raceOnly) && raceOnly.IsRaceOnly,
            "audio-domains: empty xeno remains race-only", ref failures);
        Check(AudioDomains.TryCreate("RaceA", "XenoA", out AudioDomain xeno) && xeno.Xenotype?.DefName == "XenoA",
            "audio-domains: non-empty xeno creates xenotype domain", ref failures);
    }


    private static void GlobalVolumeDefaultsAndClamps()
    {
        Scenario("8-global-volume-default-and-clamp");
        GameSettings settings = NewSettings();
        Check(Math.Abs(settings.globalVolumeFactor - 1f) < 0.0001f,
            "global-volume: default is 1", ref failures);


        settings.SetGlobalVolume(-0.5f);
        Check(Math.Abs(settings.globalVolumeFactor) < 0.0001f
            && Math.Abs((float)settings.CaptureBehaviorConfiguration().SystemVolume) < 0.0001f,
            "global-volume: negative clamps to 0 and syncs runtime", ref failures);

        settings.SetGlobalVolume(1.5f);
        Check(Math.Abs(settings.globalVolumeFactor - 1f) < 0.0001f
            && Math.Abs((float)settings.CaptureBehaviorConfiguration().SystemVolume - 1f) < 0.0001f,
            "global-volume: >1 clamps to 1 and syncs runtime", ref failures);

        settings.SetGlobalVolume(0.37f);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f
            && Math.Abs((float)settings.CaptureBehaviorConfiguration().SystemVolume - 0.37f) < 0.0001f,
            "global-volume: valid value writes field and runtime", ref failures);

        settings.SetGlobalVolume(float.NaN);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f,
            "global-volume: NaN is rejected", ref failures);

        settings.SetGlobalVolume(float.PositiveInfinity);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f,
            "global-volume: Infinity is rejected", ref failures);

        settings.SetGlobalVolume(0.37f);
        Check(Math.Abs(settings.globalVolumeFactor - 0.37f) < 0.0001f,
            "global-volume: same-value no-op keeps current", ref failures);
    }


    private static void GlobalVolumeScribeRoundTrip()
    {
        Scenario("9-global-volume-scribe-round-trip");
        GameSettings source = NewSettings();
        source.globalVolumeFactor = 0.42f;
        string path = Path.Combine(Path.GetTempPath(), "us-settings-global-volume-roundtrip-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            SafeSaver.Save(path, "Settings", () => source.ExposeData());
            GameSettings loaded = NewSettings();
            Scribe.loader.InitLoading(path);
            loaded.ExposeData();
            Scribe.loader.FinalizeLoading();
            Check(Math.Abs(loaded.globalVolumeFactor - 0.42f) < 0.0001f,
                "global-volume: Scribe round-trip preserves value", ref failures);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

// CORRECTIONS-R1 topic 5: the teaching collapse is a persisted UI preference (uiIntroCollapsed)
    // and NOTHING else - the same Scribe file, the same default-on-omission repair path the swatch
    // round-trip established. A missing key must restore OPEN (default false), never a stuck collapse.

    private static void SetDistanceRangeClampsAndMarksCustom()
    {
        Scenario("10-distance-range-clamp-custom");
        GameSettings settings = NewSettings();
        settings.distancePreset = SqueakDistancePreset.Balanced;
        settings.distanceRange = new FloatRange(15f, 50f);

        settings.SetDistanceRange(10f, 80f);
        Check(settings.distancePreset == SqueakDistancePreset.Custom,
            "distance-range: manual edit marks Custom", ref failures);
        Check(settings.distanceRange.min >= 15f - 0.0001f && settings.distanceRange.max <= 65f + 0.0001f,
            "distance-range: clamps into 15..65", ref failures);
        Check(settings.distanceRange.max >= settings.distanceRange.min + 5f - 0.0001f,
            "distance-range: enforces 5-unit minimum gap", ref failures);

        settings.SetDistanceRange(70f, 70f);
        Check(settings.distanceRange.min >= 15f - 0.0001f && settings.distanceRange.max <= 65f + 0.0001f,
            "distance-range: out-of-range endpoints clamp", ref failures);
        Check(settings.distanceRange.max >= settings.distanceRange.min + 5f - 0.0001f,
            "distance-range: equal endpoints enforce minimum gap", ref failures);

        settings.SetDistanceRange(20f, 22f);
        Check(settings.distanceRange.max >= settings.distanceRange.min + 5f - 0.0001f,
            "distance-range: close endpoints enforce minimum gap", ref failures);

        FloatRange beforeNan = settings.distanceRange;
        settings.SetDistanceRange(float.NaN, float.PositiveInfinity);
        Check(Math.Abs(settings.distanceRange.min - beforeNan.min) < 0.0001f
            && Math.Abs(settings.distanceRange.max - beforeNan.max) < 0.0001f,
            "distance-range: NaN/Infinity is rejected", ref failures);

        settings.distancePreset = SqueakDistancePreset.Custom;
        FloatRange beforeNoop = settings.distanceRange;
        settings.SetDistanceRange(beforeNoop.min, beforeNoop.max);
        Check(Math.Abs(settings.distanceRange.min - beforeNoop.min) < 0.0001f
            && Math.Abs(settings.distanceRange.max - beforeNoop.max) < 0.0001f,
            "distance-range: same Custom range is no-op", ref failures);
    }

    // The prepublication retirement removes the dedicated capability, not all responses by these Pawns.

    private static void UiSwatchRoundTripRepairAndOmission()
    {
        Scenario("16-ui-swatch-round-trip-repair-omission");
        GameSettings source = NewSettings();
        source.SetUiSwatchId("biotech");
        Check(source.uiSwatchId == "biotech", "ui-swatch: setter accepts a known id", ref failures);
        string path = Path.Combine(Path.GetTempPath(), "us-settings-swatch-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            SafeSaver.Save(path, "Settings", () => source.ExposeData());
            string xml = File.ReadAllText(path);
            Check(xml.Contains("appearanceSwatch"), "ui-swatch: a non-default value writes its node", ref failures);
            GameSettings loaded = NewSettings();
            Scribe.loader.InitLoading(path); loaded.ExposeData(); Scribe.loader.FinalizeLoading();
            Check(loaded.uiSwatchId == "biotech", "ui-swatch: Scribe round-trip preserves the value", ref failures);

            // Unknown hand-edited id -> PostLoadInit repairs to the shipped default.
            string bad = path + "-bad.xml";
            File.WriteAllText(bad, xml.Replace(">biotech<", ">neon<"));
            GameSettings repaired = NewSettings();
            Scribe.loader.InitLoading(bad);
            repaired.ExposeData();
            // The normalisation block is the PostLoadInit pass - run it exactly as the
            // game runs it after a settings load.
            Scribe.mode = LoadSaveMode.PostLoadInit;
            repaired.ExposeData();
            Scribe.loader.FinalizeLoading();
            Check(repaired.uiSwatchId == "core", "ui-swatch: unknown id repairs to core", ref failures);
            File.Delete(bad);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        // Default omits the node entirely (add-only zero-byte-delta rule).
        GameSettings clean = NewSettings();
        string cleanPath = Path.Combine(Path.GetTempPath(), "us-settings-swatch-clean-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            SafeSaver.Save(cleanPath, "Settings", () => clean.ExposeData());
            Check(!File.ReadAllText(cleanPath).Contains("appearanceSwatch"),
                "ui-swatch: the default writes no node", ref failures);
        }
        finally
        {
            if (File.Exists(cleanPath)) File.Delete(cleanPath);
        }
    }


    private static void IntroCollapsedScribeRoundTrip()
    {
        Scenario("10-intro-collapsed-scribe-round-trip");
        GameSettings source = NewSettings();
        source.SetUiIntroCollapsed(true);
        string path = Path.Combine(Path.GetTempPath(), "us-settings-intro-roundtrip-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            SafeSaver.Save(path, "Settings", () => source.ExposeData());
            GameSettings loaded = NewSettings();
            Scribe.loader.InitLoading(path);
            loaded.ExposeData();
            Scribe.loader.FinalizeLoading();
            Check(loaded.uiIntroCollapsed,
                "intro: the collapsed preference survives the Scribe round-trip", ref failures);

            // Omission repair: a file without the key re-reads to OPEN (default false), not garbage.
            GameSettings fresh = NewSettings();
            string bare = Path.Combine(Path.GetTempPath(), "us-settings-intro-bare-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(bare, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Settings />");
            GameSettings omitted = NewSettings();
            omitted.uiIntroCollapsed = true; // dirty pre-state must be repaired by the default
            Scribe.loader.InitLoading(bare);
            omitted.ExposeData();
            Scribe.loader.FinalizeLoading();
            Check(!omitted.uiIntroCollapsed,
                "intro: an omitted key restores the OPEN default (Look default false)", ref failures);
            if (File.Exists(bare)) File.Delete(bare);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }



}
