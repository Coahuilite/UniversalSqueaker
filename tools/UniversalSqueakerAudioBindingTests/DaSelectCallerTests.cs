using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;
using Verse;
using RimWorld;
using HarmonyLib;
using Verse.Sound;

internal static partial class Program
{
    private static void DaActualSelect()
    {
        const string childId = "example/InjuredSelect";
        var pawn = new Pawn { ThingID = "Ratkin-A", def = new ThingDef { defName = "Ratkin", race = new object() },
            Downed = true, FixtureAwake = false, IsPlayerControlled = false };
        var other = new Pawn { ThingID = "Ratkin-B", def = pawn.def };
        pawn.Comp = new CompSqueaker { Pawn = pawn, Vocal = false };
        other.Comp = new CompSqueaker { Pawn = other };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(pawn.def);
        var kiiro = new ThingDef { defName = "Kiiro", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(kiiro);
        var action = new UsActionDef { declaration = new ActionDeclaration { Key = childId, BaseAction = "us.core/Select",
            Priority = 40, Condition = new Condition("ref", "us.core/HasCurrentInjury") } };
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(action);
        UsVoicePackDef Pack(string name, string race, string sound) {
            var pack = new UsVoicePackDef { defName = name, modContentPack = new ModContentPack { PackageId = "fixture.da" } };
            pack.declaration.Race = race;
            pack.declaration.Entries.Add(new VoiceEntry { Key = "ordinary", Action = "us.core/Select", Sound = sound });
            pack.declaration.Entries.Add(new VoiceEntry { Key = "injured", Action = childId, Sound = sound });
            DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
            return pack;
        }
        var ratkin = Pack("US_DaRatkin", "Ratkin", "Ratkin1");
        Pack("US_DaKiiro", "Kiiro", "Ratkin2");
        var xeno = new XenotypeDef { defName = "DA_Xeno" };
        DefDatabase<XenotypeDef>.AllDefsListForReading.Add(xeno);
        Pack("US_DaXeno", "Ratkin", "Ratkin3").declaration.Child = xeno.defName;
        Pack("US_DaInvalidXeno", "Ratkin", "Ratkin4").declaration.Child = "DA_UnloadedXeno";
        var domain = new BehaviorDomain("Pawn", "", "Ratkin");
        var source = new UsBehaviorSourceDef { defName = "US_DaSource", modContentPack = ratkin.modContentPack };
        source.declaration.Sources.Add(new BehaviorSource { Domain = domain, Contributions = new List<BehaviorContribution> {
            new() { Action = "us.core/Select", Values = new BehaviorValues { Enabled = true, HardIntervalTicks = 0 } },
            new() { Action = childId, Values = new BehaviorValues { Enabled = true, HardIntervalTicks = 50, Volume = .25 } }
        } });
        source.declaration.Sources.Add(new BehaviorSource { Domain = new BehaviorDomain("Pawn", "", "Ratkin", xeno.defName),
            Contributions = new List<BehaviorContribution> { new() { Action = "us.core/Select", Values = new BehaviorValues { Volume = .75 } } } });
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Add(source);
        SqueakerRuntime.Initialize();
        void PublishSelectedPacks() {
            var directory = new SqueakXenotypeCatalogSnapshot();
            var selections = new Dictionary<AudioDomain, HashSet<string>>();
            foreach (var pack in VoicePackDirectory.Directory) {
                pack.TryGetPackKey(out var key);
                var audioDomain = new AudioDomain(new RaceKey(pack.raceDefName),
                    pack.scope == SqueakVoicePackScope.Xenotype ? new XenotypeKey(pack.targetDefName) : (XenotypeKey?)null);
                selections[audioDomain] = new HashSet<string> { key };
                if (pack.scope == SqueakVoicePackScope.Race) directory.RacePacks.Add(pack);
                else directory.XenotypePacksByDefName[pack.targetDefName] = new[] { pack };
            }
        }
        PublishSelectedPacks();
        Check(!VoicePackDirectory.Directory.Any(x => x.defName == "US_DaInvalidXeno"),
            "DA unloaded Xeno declaration cannot become a legal child audio binding");
        string persisted = "";
        Check(SqueakerMod.Settings.TryChangeBehaviorSource(domain, "fixture.da/US_DaSource", SqueakerRuntime.BehaviorSources,
            xml => { persisted = xml; return true; }, out _) && persisted.Contains(childId),
            "DA actual settings source transaction persists full child identity before runtime publication");
        Check(SqueakerMod.Settings.TryChangeBehaviorSource(new BehaviorDomain("Pawn", "", "Ratkin", xeno.defName),
            "fixture.da/US_DaSource", SqueakerRuntime.BehaviorSources, _ => true, out _), "DA bind legal child author contribution independently");
        void RelaxSelect() => Check(SqueakerMod.Settings.TryReplaceBehaviorOverride("Pawn", "", "", "", "us.core/Select",
            new BehaviorValues { SelectRequirePlayerControlled = false, SelectRequireAwake = false, SelectRequireNotDowned = false },
            () => true, out _), "DA explicit player settings relax the three official qualifications without changing accepted-feedback evidence");
        RelaxSelect();
        SqueakerMod.Settings.actionTuning.Add(new ActionTuningRecord { actionKey = childId, raceDefName = "Ratkin",
            hasScope = true, scope = BehaviorScope.Disabled });
        SqueakerRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1));
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        var selector = new Selector();
        var proxy = new object(); selector.Proxies.Add(proxy, pawn);
        int starts = SoundStarter.Played.Count;
        selector.Reject = true; selector.Select(proxy); selector.Reject = false;
        Check(!SqueakerRuntime.IsControlled(pawn) && SoundStarter.Played.Count == starts,
            "DA method invocation rejected by upstream does not create a Select event or owner");
        selector.Select(proxy, false);
        Check(!SqueakerRuntime.IsControlled(pawn), "DA accepted membership without feedback intent is not a Select response");
        selector.Selected.Clear();
        selector.Select(proxy);
        var session = RuntimeSession(pawn);
        Check(session.LastOutcome.Action == "us.core/Select" && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && SoundStarter.Played.Count == starts + 1 && session.DiagnosticText.Contains("role=SelectedSubject")
            && session.DiagnosticText.Contains("subject=" + SqueakerNotifications.SubjectKey(pawn)) && session.DiagnosticText.Contains("nativeLabel=Ratkin-A") && session.DiagnosticText.Contains("requestedEntry=fixture.da/US_DaRatkin/ordinary"),
            "DA actual Harmony proxy selection -> committed final Pawn -> real settings -> same owner -> exact entry/request/receipt/diagnostic");
        selector.Select(proxy);
        Check(SoundStarter.Played.Count == starts + 1, "DA no upstream feedback produces no extra notification (no US dedup state)");
        selector.EmitFeedbackForExisting = true;
        Find.TickManager.TicksGame += 200; selector.Select(proxy);
        Check(SoundStarter.Played.Count == starts + 2,
            "DA if upstream accepts repeated feedback US adds no independent duplicate-selection rejection");
        selector.FailAfterFeedback = true;
        try { selector.Select(other); } catch (InvalidOperationException) { }
        Check(!SqueakerRuntime.IsControlled(other), "DA feedback before failed commit cannot select a different Pawn");
        selector.FailAfterFeedback = false; selector.Select(other);
        Check(RuntimeSession(other).State.ConfirmedAction == "us.core/Select" && SoundStarter.Played.Count == starts + 3,
            "DA exception cleanup releases nested observation and separate Pawn owners may act on the same tick");
        SqueakerMod.Settings.actionTuning.Clear();
        RelaxSelect();
        pawn.health!.hediffSet!.hediffs.Add(new Hediff_Injury { Severity = 1 });
        pawn.Comp.Vocal = true;
        Find.TickManager.TicksGame += 200; selector.Select(proxy);
        Check(session.LastOutcome.Action == childId && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && session.State.ActionDeadline(childId) == Find.TickManager.TicksGame + 50
            && session.State.ActionDeadline("us.core/Select") < Find.TickManager.TicksGame
            && session.DiagnosticText.Contains("requestedEntry=fixture.da/US_DaRatkin/injured")
            && session.DiagnosticText.Contains("localVolume=0.25") && !session.DiagnosticText.Contains("US_DaKiiro/"),
            "DA actual injury extension uses its full source settings/cooldown/direct shared AudioId and never Kiiro candidates");
        pawn.health.hediffSet.hediffs.Clear();
        Find.TickManager.TicksGame += 200; selector.Select(proxy);
        Check(session.LastOutcome.Action == "us.core/Select", "DA removing current injury leaves ordinary Select intact");
        pawn.health = null;
        Find.TickManager.TicksGame += 200; selector.Select(proxy);
        Check(session.LastOutcome.Action == "us.core/Select" && session.LastOutcome.Diagnostics.Any(x => x.Key == "us.core/HasCurrentInjury"),
            "DA current injury Unknown is not cached True and remains diagnostic while independent ordinary Select may respond");
        pawn.genes = new FixtureGenes { Xenotype = xeno };
        Find.TickManager.TicksGame += 200; selector.Select(proxy);
        Check(session.LastOutcome.Action == "us.core/Select" && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && session.DiagnosticText.Contains("sourceDomain=Ratkin+DA_Xeno")
            && session.DiagnosticText.Contains("requestedEntry=fixture.da/US_DaXeno/ordinary"),
            "DA actual legal Xeno context selects its own declared entry and preserves source subdomain");
        GameRuntime.Current.RejectChildContext = true;
        Find.TickManager.TicksGame += 200; selector.Select(proxy);
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && session.DiagnosticText.Contains("requestedEntry=fixture.da/US_DaRatkin/ordinary")
            && session.DiagnosticText.Contains("localVolume=1 "),
            "DA resolver-rejected raw genes cannot admit child audio or child behavior contributions");
        GameRuntime.Current.RejectChildContext = false;
        pawn.genes = null;
        pawn.health = new FixtureHealth(); pawn.health.hediffSet!.hediffs.Add(new Hediff_Injury { Severity = 1 });
        ratkin.declaration.Entries.RemoveAll(x => x.Action == childId);
        SqueakerRuntime.Initialize(); PublishSelectedPacks();
        Find.TickManager.TicksGame += 200; selector.Select(proxy);
        session = RuntimeSession(pawn);
        Check(session.LastOutcome.Action == childId && session.LastOutcome.Reason == "NoBoundAudio",
            "DA actual missing child audio cannot fall back to parent mapping even when parent shares the AudioId");
        Console.WriteLine("DA SELECT TRACE " + session.DiagnosticText);
    }
}
