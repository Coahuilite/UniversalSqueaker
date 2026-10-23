using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private static void WorkCore(bool missing, bool careOnly)
    {
        Console.WriteLine("MODE Core Work --" + (missing ? "work-missing" : careOnly ? "work-care-lifetime" : "work-core") + ": actual production capture/config/owner/request; Job/driver/Toil/scheduler/audio doubles");
        using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("WorkCoreExpected")!);
        var cases = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('|')).ToArray();
        Check(cases.Length == 78 && cases.Select(x => x[0]).Distinct().Count() == 78, "audited Core inventory has 78 exact Job/driver expectations, not a production-table enumeration");
        DefDatabase<UsActionDef>.AllDefsListForReading.Clear(); DefDatabase<UsProviderDef>.AllDefsListForReading.Clear();
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Clear(); DefDatabase<UsVoicePackDef>.AllDefsListForReading.Clear();
        var race = new ThingDef { defName = "WorkCoreRace", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(race);
        var pack = new UsVoicePackDef { defName = "US_CoreWork", modContentPack = new ModContentPack { PackageId = "fixture.work" } };
        pack.declaration.Race = race.defName;
        const string maintainedWork = "fixture.work/MaintainedWork";
        DeclareMaintainedChild(pack, maintainedWork, CoreActions.Work);
        pack.declaration.Entries.Add(new VoiceEntry { Key = "Work", Action = "us.core/Work", Sound = "Ratkin1" });
        const string child = "vendor/FieldCare";
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = child, BaseAction = "us.core/Work", Priority = 100, Condition = new Condition("ref", "us.core/HasCurrentInjury") } });
        pack.declaration.Entries.Add(new VoiceEntry { Key = "Care", Action = child, Sound = "Ratkin1" });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        DcProjectAudio(); UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        int serial = 0;
        Pawn Actor(string[] row)
        {
            var p = new Pawn { ThingID = "WorkCore" + ++serial, def = race, IsPlayerControlled = true,
                jobs = new Pawn_JobTracker(), pather = new Pawn_PathFollower(), mindState = new Pawn_MindState() };
            var job = new Job { def = new JobDef { defName = row[0] }, playerForced = true,
                workGiverDef = row[2] == "" ? null : new WorkGiverDef { defName = row[2] } };
            var type = AccessTools.TypeByName(row[1]);
            var driver = type == null ? new JobDriver() : (JobDriver)Activator.CreateInstance(type)!;
            driver.pawn = p; driver.job = job;
            p.CurJob = job; p.jobs.curDriver = driver; p.Comp = new CompSqueaker { Pawn = p };
            return p;
        }
        Truth Fact(Pawn p) => UsPawnFacts.Capture(p, null)["us.core/WorkState"];
        void Current(Pawn p) => p.jobs!.curDriver!.FixtureToil = new Toil { actor = p };
        void Request(Pawn p, string action = "us.core/Work", string entry = "Work")
        {
            var q = p.Comp.LastCandidateRequest;
            Check(q?.Action == action && q.Subject == SqueakerNotifications.SubjectKey(p) && q.SubjectRole == "Worker" && q.Domain == race.defName
                && q.Audio.SourceDomain == AudioDomains.RaceOnly(race.defName) && q.Audio.EntryKey == "fixture.work/US_CoreWork/" + entry
                && q.Audio.AudioId == "actual.provider/Ratkin1" && RuntimeSession(p).LastOutcome.Receipt?.Result == PlaybackResult.Dispatched,
                "production request " + p.CurJob!.def!.defName + " exact full key/Worker/entry/AudioId/supplying domain");
        }
        var sow = cases.Single(x => x[0] == "Sow");
        void CareAndTail()
        {
            var recipient = new Pawn { ThingID = "FedOtherRace", def = new ThingDef { defName = "RecipientRace", race = new object() } };
            recipient.Comp = new CompSqueaker { Pawn = recipient };
            var feeder = Actor(cases.Single(x => x[0] == "FeedPatient"));
            var food = new Thing { def = new ThingDef { ingestible = new object(), IsNutritionGivingIngestible = true } };
            feeder.CurJob!.targetA = new LocalTargetInfo { Thing = food };
            feeder.CurJob.targetB = new LocalTargetInfo { Pawn = recipient };
            feeder.jobs!.curDriver!.SetupToils(); Current(feeder);
            Check(Fact(feeder) == Truth.True && Fact(recipient) == Truth.False
                && UsPawnFacts.Capture(feeder, null)[EatPolicy.Process] == Truth.False
                && UsPawnFacts.Capture(recipient, null)[EatPolicy.Process] == Truth.True,
                "actual FeedPatient setup separates working feeder from actual ingester in another Race");
            Find.TickManager.TicksGame += 100; feeder.Comp.CompTick(); Request(feeder);
            Check(!SqueakerRuntime.IsControlled(recipient) && feeder.Comp.LastCandidateRequest!.SubjectRole == "Worker",
                "care setup/request does not manufacture recipient owner or replace Worker with ActualIngester");
            feeder.jobs.curDriver.ended = true; feeder.jobs.curDriver.Cleanup(JobCondition.Succeeded);
            Check(Fact(feeder) == Truth.False && UsPawnFacts.Capture(recipient, null)[EatPolicy.Process] == Truth.False,
                "normal care exit clears both the live Work process and recipient ingestion observation");
            foreach (var condition in new[] { JobCondition.Succeeded, JobCondition.InterruptForced })
            {
                var p = Actor(sow); Current(p); Find.TickManager.TicksGame += 100; p.Comp.CompTick();
                var sample = SoundStarter.Played.Last();
                Check(sample.source.Playing, "new Sow OneShot is actually dispatched before " + condition);
                p.jobs!.curDriver!.ended = true; p.jobs.curDriver.Cleanup(condition);
                Find.TickManager.TicksGame += 60; p.Comp.CompTick();
                Check(sample.source.Playing && Fact(p) == Truth.False, "Work " + condition + " leaves dispatched OneShot natural tail instead of cutting it");
            }
        }
        var inactive = Actor(sow); Current(inactive); int starts = SoundStarter.Played.Count;
        inactive.Comp.CompTick();
        Check(!SqueakerRuntime.DevelopmentActive && !SqueakerRuntime.IsControlled(inactive) && SoundStarter.Played.Count == starts,
            "expanded Work coverage retains overall unselected Dev zero-owner/output gate");
        // Explicit fixture timing only; do not alter ordinary scope or built-in priority.
        SqueakerRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1));
        if (careOnly) { CareAndTail(); SqueakerRuntime.ResetSession(); return; }
        if (missing)
        {
            Check(AccessTools.TypeByName(sow[1]) == null, "Sow driver class is physically absent in this separate compilation");
            var p = Actor(sow); Current(p);
            Check(UsWorkObservation.Capture(p, out var why) == Truth.Unknown && why?.Reason == "MissingDriver:RimWorld.JobDriver_PlantSow",
                "missing one audited implementation is named Unknown, not false/true from other ready drivers");
            Find.TickManager.TicksGame += 100; p.Comp.CompTick();
            Check(SoundStarter.Played.Count == starts && p.Comp.LastCandidateRequest == null, "missing Sow implementation cannot dispatch Work or invent a fallback");
            var cutting = Actor(cases.Single(x => x[0] == "CutPlant")); Current(cutting);
            Find.TickManager.TicksGame += 100; cutting.Comp.CompTick(); Request(cutting);
            Check(UsWorkObservation.Ready && SoundStarter.Played.Count == starts + 1, "missing Sow isolates that process, unrelated CutPlant remains usable through the same owner implementation");
            return;
        }
        var legacy = new HashSet<string> { "Research", "Mine", "DoBill", "Repair", "Clean", "HaulToCell", "FinishFrame" };
        int tested = 0;
        foreach (var row in cases.Where(x => !legacy.Contains(x[0])))
        {
            var p = Actor(row); var driver = p.jobs!.curDriver!; var job = p.CurJob!;
            Check(Fact(p) == Truth.False, row[0] + " before current Toil is not yet a working process");
            driver.SetupToils(); Current(p);
            Check(Fact(p) == Truth.True, row[0] + " actual production capture accepts current preparation Toil");
            Find.TickManager.TicksGame += 100; starts = SoundStarter.Played.Count; p.Comp.CompTick(); Request(p);
            var owner = RuntimeSession(p);
            Check(SoundStarter.Played.Count == starts + 1, row[0] + " one state opportunity through the production caller, not one per category");
            driver.FixtureToil = new Toil { actor = p };
            Check(Fact(p) == Truth.True && ReferenceEquals(owner, RuntimeSession(p)), row[0] + " current execution Toil retains same process/owner");
            driver.job = new Job { def = job.def };
            Check(Fact(p) == Truth.Unknown, row[0] + " mismatched current Job is Unknown"); driver.job = job;
            driver.pawn = new Pawn(); Check(Fact(p) == Truth.Unknown, row[0] + " wrong driver Pawn is Unknown"); driver.pawn = p;
            p.jobs.curDriver = new JobDriver { pawn = p, job = job, FixtureToil = new Toil() };
            Check(Fact(p) == Truth.Unknown, row[0] + " wrong driver class cannot satisfy an audited Job name"); p.jobs.curDriver = driver;
            driver.ended = true; // Native Pawn_JobTracker's verified order: ended=true, Cleanup, ClearDriver.
            driver.Cleanup(JobCondition.Succeeded);
            Check(driver.FixtureToil != null && Fact(p) == Truth.False, row[0] + " normal exit is false even while old current Toil is still retained");
            driver.ended = false; driver.FixtureToil = null;
            Check(Fact(p) == Truth.False, row[0] + " no current Toil cannot retain previous true");
            p.CurJob = null; Check(Fact(p) == Truth.False, row[0] + " removed Job exits the process");
            tested++;
        }
        Check(tested == 71, "71 new Job/driver pairs each execute production capture/request/exit and mismatch cases; original seven evidence reused");
        foreach (var row in cases.Where(x => x[2] != ""))
        {
            var p = Actor(row); Current(p); p.CurJob!.workGiverDef = null;
            Check(UsWorkObservation.Capture(p, out var d) == Truth.Unknown && d?.Reason == "SpecificWorkGiverRequired:" + row[2], row[0] + " shared mechanism needs its exact audited work source");
            p.CurJob.workGiverDef = new WorkGiverDef { defName = "Unreviewed" };
            Check(Fact(p) == Truth.Unknown, row[0] + " merely having a workGiver label is insufficient");
        }
        foreach (string job in new[] { "Goto", "Wait", "LayDown", "UnknownWork" })
        {
            var p = Actor(sow); p.CurJob!.def = new JobDef { defName = job }; p.CurJob.workGiverDef = new WorkGiverDef { defName = "Train" }; Current(p);
            Check(Fact(p) == Truth.Unknown, job + " is not inferred from an unrelated job/WorkGiver/name fragment");
        }
        var overlap = Actor(sow); Current(overlap); overlap.pather!.Moving = true;
        Check(Fact(overlap) == Truth.True && UsPawnFacts.Capture(overlap, null)["us.core/MoveState"] == Truth.True, "preparation movement is genuinely Work plus Move");
        Find.TickManager.TicksGame += 100; starts = SoundStarter.Played.Count; overlap.Comp.CompTick();
        Check(RuntimeSession(overlap).LastOutcome.Action == "us.core/Move" && SoundStarter.Played.Count == starts,
            "unchanged Dev Move-before-Work selects Move; missing Move audio cannot fall through to Work");
        overlap.pather.Moving = false; Find.TickManager.TicksGame += 100; overlap.Comp.CompTick(); Request(overlap);
        var visit = Actor(cases.Single(x => x[0] == "VisitSickPawn")); Current(visit); visit.CurJob!.def!.joyKind = JoyKindDefOf.Social;
        Check(Fact(visit) == Truth.True && UsPawnFacts.Capture(visit, null)["us.core/SocialState"] == Truth.True,
            "actual Core VisitSickPawn joyKind Social overlaps Work without altering either fact");
        Find.TickManager.TicksGame += 100; visit.Comp.CompTick();
        Check(RuntimeSession(visit).LastOutcome.Action == "us.core/Social", "unchanged Dev Social-before-Work arbitration is visible, not hidden by raising Work priority");

        var doctor = Actor(cases.Single(x => x[0] == "TendPatient")); Current(doctor);
        var patient = new Pawn { ThingID = "PatientOtherDomain", def = new ThingDef { defName = "OtherRace", race = new object() } };
        patient.Comp = new CompSqueaker { Pawn = patient }; doctor.CurJob!.targetA = new LocalTargetInfo { Pawn = patient };
        Check(Fact(patient) == Truth.False && Fact(doctor) == Truth.True && !SqueakerRuntime.IsControlled(patient), "care recipient/animal target is not promoted to Worker or another owner");
        doctor.CurJob.playerForced = false; Find.TickManager.TicksGame += 100; starts = SoundStarter.Played.Count; doctor.Comp.CompTick();
        Check(SoundStarter.Played.Count == starts && doctor.Comp.LastCandidateRequest == null && Fact(doctor) == Truth.True,
            "automatic care remains a true Work fact but the unchanged ActiveCommand response qualification refuses");
        doctor.health!.hediffSet!.hediffs.Add(new Hediff_Injury { Severity = 1 });
        Check(SqueakerMod.Settings.TryChangeBehaviorOverride("Pawn", "", race.defName, "", child,
            new BehaviorValues { Scope = BehaviorScope.ActiveCommand, HardIntervalTicks = 9, Volume = .3 }, () => true, out _), "inherited full Work action uses production sparse settings writer");
        doctor.CurJob.playerForced = true; Find.TickManager.TicksGame += 100; doctor.Comp.CompTick(); Request(doctor, child, "Care");
        Check(doctor.Comp.LastCandidateRequest!.Audio.Volume == .3 && RuntimeSession(doctor).State.ActionDeadline(child) == Find.TickManager.TicksGame + 9,
            "new care process keeps child scope/config/entry and its independent complete-key protection");

        var sound = DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin1")!;
        sound.sustain = true;
        foreach (var condition in new[] { JobCondition.Succeeded, JobCondition.InterruptForced })
        {
            var p = Actor(sow); Current(p); MaintainSubject(p, true); Find.TickManager.TicksGame += 100; p.Comp.CompTick();
            var handle = SoundStarter.Spawned.Last(); Check(!handle.Ended, "new Sow process establishes its owned Sustained slot before " + condition);
            Check(p.Comp.LastCandidateRequest?.Action == maintainedWork, "new Sow sustained shape comes from its explicit declared child, not a Comp plan flag");
            p.jobs!.curDriver!.ended = true; p.jobs.curDriver.Cleanup(condition);
            Find.TickManager.TicksGame += 60; p.Comp.CompTick();
            Check(handle.Ended, "native ended/Cleanup order releases only the current Work Sustained on " + condition);
            MaintainSubject(p, false);
        }
        sound.sustain = false;
        CareAndTail();
        SqueakerRuntime.ResetSession();
    }
}
