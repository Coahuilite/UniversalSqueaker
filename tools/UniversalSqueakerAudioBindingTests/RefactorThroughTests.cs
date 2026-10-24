using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;
using Verse;
using Verse.AI;
using Verse.Sound;

internal static partial class Program
{
    private static void RefactorThrough()
    {
        Console.WriteLine("MODE --refactor-through: actual Harmony hooks → production declaration/config/facts/neutral owner/audio → native output double/receipt");
        var custom = new NotificationContract("external.worker/Finished", 120, 40);
        SqueakerNotifications.Declare(custom);
        var restricted = new NotificationContract("external.worker/NeedsInjury", restriction: new Condition("ref", "us.core/HasCurrentInjury"));
        var noAudio = new NotificationContract("external.worker/WithoutAudio");
        var refusedOutput = new NotificationContract("external.worker/OutputRefused");
        SqueakerNotifications.Declare(restricted); SqueakerNotifications.Declare(noAudio); SqueakerNotifications.Declare(refusedOutput);
        var race = new ThingDef { defName = "RefactorRace", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(race);
        var pack = new UsVoicePackDef { defName = "US_Through", modContentPack = new ModContentPack { PackageId = "fixture.through" } };
        pack.declaration.Race = race.defName;
        foreach (var key in new[] { CoreActions.Select, CoreActions.Eat, CoreActions.Attack, custom.ActionId, restricted.ActionId, refusedOutput.ActionId })
            pack.declaration.Entries.Add(new VoiceEntry { Key = key, Action = key, Sound = "Ratkin1" });
        const string sustained = "external.worker/Moving";
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = sustained, BaseAction = CoreActions.Move, Priority = 100, Sustained = true, Condition = new Condition("all") } });
        pack.declaration.Entries.Add(new VoiceEntry { Key = "moving", Action = sustained, Sound = "Ratkin2" });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        SqueakerRuntime.Initialize();
        var domain = AudioDomains.RaceOnly(race.defName);
        ProjectSelection(new SqueakXenotypeCatalogSnapshot(), new Dictionary<AudioDomain, HashSet<string>> { [domain] = new() { "fixture.through:US_Through" } });
        var pawn = new Pawn { ThingID = "Through1", def = race, IsPlayerControlled = true,
            jobs = new Pawn_JobTracker(), pather = new Pawn_PathFollower(), mindState = new Pawn_MindState() };
        pawn.Comp = new CompSqueaker { Pawn = pawn, Vocal = false };
        int before = SoundStarter.Played.Count;
        new Selector().Select(pawn);
        Check(!SqueakerRuntime.IsControlled(pawn) && SoundStarter.Played.Count == before, "unselected Dev gate still allocates no subject owner");
        SqueakerRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1));
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        Check(UsInstanceOutput.Available, "actual native output patch owner/readiness is the one installed fixture Harmony identity");
        Find.TickManager.TicksGame += 1;
        new Selector().Select(pawn);
        var owner = RuntimeSession(pawn);
        Console.WriteLine("SELECT TRACE " + owner.DiagnosticText + " issues=" + string.Join(";", SqueakerRuntime.Issues));
        Check(owner.LastOutcome.Action == CoreActions.Select && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && SoundStarter.Played.Count == before + 1, "actual accepted Select reaches same neutral owner without hidden vocal gate");
        pawn.Comp.Vocal = true;
        Find.TickManager.TicksGame += 1;
        var melee = new Verb_MeleeAttack { caster = pawn, Result = false };
        melee.Execute();
        Check(owner.LastOutcome.Action == CoreActions.Attack && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && SoundStarter.Played.Count == before + 2, "actual melee executed-false marker dispatches Attack, not a hit claim");
        Find.TickManager.TicksGame += 1;
        melee.Busy = true; melee.Execute();
        Check(SoundStarter.Played.Count == before + 2, "native busy false early return has no execution marker or extra Attack");
        var food = new Thing { def = new ThingDef { ingestible = new object(), IsNutritionGivingIngestible = true } };
        var job = new Job { def = JobDefOf.Ingest, targetA = new LocalTargetInfo { Thing = food } };
        var driver = new JobDriver_Ingest { pawn = pawn, job = job };
        pawn.CurJob = job; pawn.jobs!.curDriver = driver;
        driver.SetupToils(); driver.FixtureToil = new Toil { actor = pawn };
        Find.TickManager.TicksGame += 1; pawn.Comp.CompTick();
        Check(owner.LastOutcome.Action == CoreActions.Eat && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && SoundStarter.Played.Count == before + 3, "actual ingestion process uses settled Whole/Any/Any in neutral session");
        long wait = owner.State.NextStateTick, attempt = owner.State.AttemptTick;
        food.NutritionResult = 0; food.Ingested(pawn, 1);
        food.NutritionResult = .5f; food.Ingested(pawn, 1);
        Check(owner.State.NextStateTick == wait && owner.State.AttemptTick == attempt && SoundStarter.Played.Count == before + 3,
            "normal zero/positive ingestion results with no Result consumer do not create ordinary Eat opportunities");
        Find.TickManager.TicksGame += 1;
        Check(SqueakerNotifications.TryProcess(pawn, custom), "declared external C# notification enters production gateway");
        var request = pawn.Comp.LastCandidateRequest;
        Check(ReferenceEquals(owner, RuntimeSession(pawn)) && request?.Action == custom.ActionId && request.Subject == SqueakerNotifications.SubjectKey(pawn)
            && request.Audio.EntryKey == "fixture.through/US_Through/" + custom.ActionId
            && request.Audio.AudioId == "actual.provider/Ratkin1" && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched,
            "C# author notification settles original subject owner with exact ActionId/entry/AudioId/receipt");
        long deadline = owner.State.ActionDeadline(custom.ActionId);
        Find.TickManager.TicksGame += 1; SqueakerNotifications.TryProcess(pawn, custom);
        Check(owner.LastOutcome.Reason == "HardDeadline" && owner.State.ActionDeadline(custom.ActionId) == deadline && SoundStarter.Played.Count == before + 4,
            "explicit notification shares original hard owner, not a parallel event cooldown");
        Find.TickManager.TicksGame++;
        Check(SqueakerNotifications.TryProcess(pawn, restricted) && owner.LastOutcome.Receipt == null
            && SoundStarter.Played.Count == before + 4,
            "processed notification is true when the actual injury qualification prevents playback");
        Console.WriteLine("NOTIFICATION qualification " + owner.DiagnosticText);
        Find.TickManager.TicksGame++;
        Check(SqueakerNotifications.TryProcess(pawn, noAudio) && owner.LastOutcome.Reason == "NoBoundAudio"
            && owner.LastOutcome.Receipt == null && SoundStarter.Played.Count == before + 4,
            "processed notification is true with no mapped audio, not a playback claim");
        Find.TickManager.TicksGame++;
        SqueakSoundAvailabilityCache.RejectProductionInfo = true;
        try
        {
            Check(SqueakerNotifications.TryProcess(pawn, refusedOutput)
                && owner.LastOutcome.Receipt?.Result == PlaybackResult.Rejected && SoundStarter.Played.Count == before + 4,
                "processed notification stays true while actual native output preflight returns Rejected");
            Console.WriteLine("NOTIFICATION rejected output " + owner.DiagnosticText);
        }
        finally { SqueakSoundAvailabilityCache.RejectProductionInfo = false; }
        var audio = SqueakerRuntime.Audio;
        var configuration = SqueakerMod.Settings.CaptureBehaviorConfiguration();
        Check(ReferenceEquals(configuration, SqueakerMod.Settings.CaptureBehaviorConfiguration()), "repeated opportunities/readers capture published configuration reference without reparsing XML");
        SqueakerMod.Settings.SetActivity(2);
        Check(!ReferenceEquals(configuration, SqueakerMod.Settings.CaptureBehaviorConfiguration()) && ReferenceEquals(audio, SqueakerRuntime.Audio)
            && owner.State.NextStateTick == wait && owner.State.ActionDeadline(custom.ActionId) == deadline,
            "configuration edit publishes only behavior revision, retaining audio and scheduled waits/deadlines");
        Check(!SqueakerNotifications.TryProcess(pawn, "unknown/Undeclared") && !SqueakerNotifications.TryProcess(pawn, CoreActions.Select)
            && SoundStarter.Played.Count == before + 4, "unknown full string is rejected rather than guessed into a default Call plan");
        pawn.def = new ThingDef { defName = "ChangedRace", race = new object() };
        pawn.ThingID = "ChangedRace_" + pawn.thingIDNumber;
        Find.TickManager.TicksGame++;
        SqueakerNotifications.TryProcess(pawn, custom);
        Check(ReferenceEquals(owner, RuntimeSession(pawn)) && owner.LastOutcome.Reason == "HardDeadline"
            && owner.State.ActionDeadline(custom.ActionId) == deadline && SoundStarter.Played.Count == before + 4,
            "actual adapter uses stable numeric subject identity across changed race/display ThingID, retaining the same owner/deadline");
        pawn.def = race;
        pawn.CurJob = null;
        pawn.pather!.Moving = true;
        UsAudioBindings.Resolve("actual.provider/Ratkin2")!.sustain = true;
        Find.TickManager.TicksGame = (int)owner.State.NextStateTick;
        pawn.Comp.CompTick();
        var native = SoundStarter.Spawned.Last();
        Check(owner.LastOutcome.Action == sustained && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && !native.Ended,
            "explicit maintained contract starts one native sustainer through original neutral owner");
        Check(SqueakerMod.Settings.TryReplaceBehaviorOverride("Pawn", "", race.defName, "", sustained, new BehaviorValues { Enabled = false }, () => true, out _),
            "current configuration can close sustained contract independently");
        Find.TickManager.TicksGame++;
        pawn.Comp.CompTick();
        Check(native.Ended && native.EndCalls == 1 && owner.DiagnosticText.Contains("CurrentAuthorizationUnavailable"),
            "changed configuration forces current authorization review before sixty-tick fact cadence, ending owned sustained audio once");
        Console.WriteLine("r2 THROUGH TRACE " + owner.DiagnosticText);
    }
}
