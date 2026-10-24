using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;

internal static partial class Program
{
    private static void DcSelectSettings()
    {
        Console.WriteLine("MODE DC Select: three independent sparse player qualifications; actual Selector -> production owner/output");
        var race = new ThingDef { defName = "DC_SelectRace", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(race);
        var xeno = new XenotypeDef { defName = "DC_SelectXeno" };
        DefDatabase<XenotypeDef>.AllDefsListForReading.Add(xeno);
        var p = new Pawn { ThingID = "DC_Selected", def = race, IsPlayerControlled = true, jobs = new Pawn_JobTracker() };
        p.Comp = new CompSqueaker { Pawn = p, Vocal = false };
        var pack = new UsVoicePackDef { defName = "US_DC_Select", modContentPack = new ModContentPack { PackageId = "fixture.dc" } };
        pack.declaration.Race = race.defName;
        pack.declaration.Entries.Add(new VoiceEntry { Key = "accepted", Action = "us.core/Select", Sound = "Ratkin1" });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        var author = new UsBehaviorSourceDef { defName = "US_DC_SelectAuthor", modContentPack = pack.modContentPack };
        author.declaration.Sources.Add(new BehaviorSource { Domain = new BehaviorDomain("Pawn", "", race.defName),
            Contributions = new List<BehaviorContribution> { new() { Action = "us.core/Select", Values = new BehaviorValues {
                SelectRequirePlayerControlled = false, SelectRequireAwake = false, SelectRequireNotDowned = false } } } });
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Add(author);
        SqueakerRuntime.Initialize();
        var directory = new SqueakXenotypeCatalogSnapshot();
        var selections = new Dictionary<AudioDomain, HashSet<string>>();
        foreach (var audio in VoicePackDirectory.Directory) {
            audio.TryGetPackKey(out var key); directory.RacePacks.Add(audio);
            selections[new AudioDomain(new RaceKey(audio.raceDefName), null)] = new HashSet<string> { key };
        }
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        SqueakerRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1));
        var settings = SqueakerMod.Settings;
        var selector = new Selector(); var proxy = new object(); selector.Proxies.Add(proxy, p);
        bool Select(bool expected, string label) {
            selector.Selected.Clear(); Find.TickManager.TicksGame += 40; int count = SoundStarter.Played.Count;
            selector.Select(proxy);
            bool played = SoundStarter.Played.Count == count + 1;
            if (SqueakerRuntime.IsControlled(p)) Console.WriteLine("DC SELECT " + RuntimeSession(p).DiagnosticText);
            Check(played == expected, "DC Select " + label);
            return played;
        }
        void Field(string key, bool? value, string target = "", string child = "") => settings.SetSelectParameter(key, value, target, child);
        void Flags(bool player = true, bool awake = true, bool downed = false) {
            p.IsPlayerControlled = player; p.FixtureAwake = awake; p.Downed = downed; p.health ??= new FixtureHealth();
        }
        var defaults = settings.ReadSelectParameters();
        Check(defaults.SelectRequirePlayerControlled == true && defaults.SelectRequireAwake == true && defaults.SelectRequireNotDowned == true
            && settings.actionTuning.Count == 0, "DC Select absent fields use all three official true defaults without materializing overrides");
        Select(true, "default accepted final Pawn dispatches without a vocal gate");
        Check(RuntimeSession(p).DiagnosticText.Contains("requestedEntry=fixture.dc/US_DC_Select/accepted")
            && RuntimeSession(p).DiagnosticText.Contains("role=SelectedSubject"), "DC Select actual exact source entry and subject role");
        foreach (string key in new[] { "playerControlled", "awake", "notDowned" }) {
            Flags(key != "playerControlled", key != "awake", key == "notDowned");
            Select(false, key + " default rejects only its failed qualification");
            Field(key, false); Select(true, "explicit false relaxes only " + key);
            Flags(false, false, true); Select(false, "relaxing " + key + " does not cancel the other two qualifications");
            Field(key, null);
            Check(settings.ReadSelectParameterOverrides().PresentKeys().Count() == 0, "DC Select clear restores sparse absence " + key);
        }
        Field("playerControlled", false); Field("awake", false); Field("notDowned", false);
        Flags(false, false, true); p.health = null;
        Select(true, "all qualifications relaxed accepts actual feedback despite optional Awake/NotDowned Unknown");
        var owner = RuntimeSession(p);
        Check(UsPawnFacts.Capture(p, "us.core/Select")[SelectPolicy.NotDowned] == Truth.Unknown,
            "DC Select unavailable health is precise Unknown, not false or cached true");
        var view = settings.CaptureBehaviorConfiguration();
        var effective = view.Resolve(new BehaviorDomain("Pawn", "", race.defName), "us.core/Select", SelectPolicy.OfficialDefaults,
            SqueakerRuntime.BehaviorSources, key => key != SelectPolicy.NotDowned && key != SelectPolicy.Awake);
        Check(!effective.Paused && !effective.Dependencies.Contains(SelectPolicy.NotDowned) && !effective.Dependencies.Contains(SelectPolicy.Awake),
            "DC Select relaxed parameters do not require optional implementations");
        Field("notDowned", true);
        effective = settings.CaptureBehaviorConfiguration().Resolve(new BehaviorDomain("Pawn", "", race.defName), "us.core/Select", SelectPolicy.OfficialDefaults,
            SqueakerRuntime.BehaviorSources, key => key != SelectPolicy.NotDowned);
        Check(effective.Paused && effective.Failure.Contains(SelectPolicy.NotDowned), "DC Select selected optional dependency is named and isolated when implementation absent");
        Field("notDowned", false);
        int before = SoundStarter.Played.Count; long? confirmed = owner.State.ConfirmedTick;
        selector.Reject = true; Select(false, "relaxed settings cannot forge rejected upstream acceptance"); selector.Reject = false;
        selector.Selected.Clear(); selector.Select(proxy, false);
        Check(SoundStarter.Played.Count == before && owner.State.ConfirmedTick == confirmed,
            "DC Select relaxing all three cannot forge feedback or mutate confirmed owner state");
        Flags();
        Check(settings.TryChangeBehaviorSource(new BehaviorDomain("Pawn", "", race.defName), "fixture.dc/US_DC_SelectAuthor",
            SqueakerRuntime.BehaviorSources, _ => true, out _), "DC Select bind actual author source");
        Field("playerControlled", true, race.defName); Flags(false); Select(false, "Race player true beats profile false and author false");
        Field("playerControlled", false, race.defName, xeno.defName); p.genes = new FixtureGenes { Xenotype = xeno };
        Select(true, "legal Xeno player false overrides Race true using the same owner");
        Check(RuntimeSession(p) == owner && owner.DiagnosticText.Contains("selectRequirePlayerControlled=player:Pawn::DC_SelectRace:DC_SelectXeno"),
            "DC Select captured effective configuration reports exact child provenance without a second owner");
        GameRuntime.Current.RejectChildContext = true;
        Select(false, "resolver-rejected child cannot widen Race qualification"); GameRuntime.Current.RejectChildContext = false;
        p.genes = null;
        Field("playerControlled", null, race.defName); Select(true, "clearing Race field reveals explicit profile false");
        Field("playerControlled", null); Field("awake", null); Field("notDowned", null);
        Select(true, "author false remains after player fields are absent");
        Check(!settings.TryReplaceBehaviorOverride("NonPawn", "fixture", "object", "", "us.core/Select",
            new BehaviorValues { SelectRequireAwake = false }, () => true, out _), "DC Select Pawn qualification is not silently attached to NonPawn");
        Console.WriteLine("DC SELECT FINAL " + owner.DiagnosticText);
    }
}
