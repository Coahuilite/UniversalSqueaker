using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UniversalSqueaker;
using UniversalSqueaker.Responses;

internal static partial class Program
{
    private sealed class EatingDriver : JobDriver, IEatingDriver
    {
        public bool Nutrition;
        public bool GainingNutritionNow => Nutrition;
    }

    private static void S3PawnFactAdapter()
    {
        var pawn = new Pawn { CurJob = new Job { def = new JobDef { defName = "Wait" } },
            jobs = new Pawn_JobTracker { curDriver = new JobDriver() }, pather = new Pawn_PathFollower(),
            mindState = new Pawn_MindState { IsIdle = true } };
        Dictionary<string, Truth> Capture(string? key = null) => UsPawnFacts.Capture(pawn, key);
        Check(Capture()["us.core/CallState"] == Truth.True, "S3 actual capture: positive idle + ordinary Wait + stationary Call");
        pawn.CurJob.def!.defName = "Wait_Combat";
        Check(Capture()["us.core/CallState"] == Truth.False, "S3 actual capture: combat Wait does not become Call");
        pawn.CurJob.def.defName = "Wait_Wander"; pawn.pather.Moving = true;
        Check(Capture()["us.core/CallState"] == Truth.False, "S3 actual capture: moving wander cannot claim stationary Call");
        pawn.pather.Moving = false; pawn.mindState.IsIdle = false;
        Check(Capture()["us.core/CallState"] == Truth.False, "S3 actual capture: no positive idle is not a fallback Call");
        pawn.FixtureAwake = false;
        Check(Capture()["us.core/SleepState"] == Truth.False, "S3 actual capture: not Awake does not manufacture actual sleep");
        pawn.jobs.curDriver!.asleep = true;
        Check(Capture()["us.core/SleepState"] == Truth.True, "S3 actual capture: real driver asleep is Sleep");
        pawn.jobs.curDriver = null;
        Check(Capture()["us.core/SleepState"] == Truth.Unknown, "S3 actual capture: absent driver remains Unknown");
        pawn.CurJob.def = new JobDef { defName = "NotReallySocialChatVisit" };
        Check(Capture()["us.core/SocialState"] == Truth.False, "S3 actual capture: arbitrary substring is not social evidence");
        pawn.CurJob.def.joyKind = JoyKindDefOf.Social;
        Check(Capture()["us.core/SocialState"] == Truth.True && Capture()["us.core/JoyState"] == Truth.True, "S3 actual capture: published social joy kind supports actual overlapping facts");
        pawn.CurJob.def = JobDefOf.Ingest;
        pawn.CurJob.targetA = new LocalTargetInfo { Thing = new Thing { def = new ThingDef { IsDrug = true } } };
        var eating = new EatingDriver(); pawn.jobs.curDriver = eating;
        Check(Capture()[EatPolicy.Process] == Truth.True && Capture()[EatPolicy.Execution] == Truth.False,
            "DB self Ingest preparation is process, not execution even when nutrition driver claims true");
        eating.Nutrition = true;
        Check(Capture()[EatPolicy.Execution] == Truth.False, "DB IEatingDriver nutrition flag cannot manufacture Toil entry");        pawn.CurJob.playerForced = true; pawn.IsPlayerControlled = true;
        Check(Capture()["us.core/ActiveCommand"] == Truth.True, "S3 actual capture: work/move command provenance preserved independently of state");
        Check(Capture("us.core/Death")["us.core/DeathEvent"] == Truth.True && Capture()["us.core/DeathEvent"] == Truth.False,
            "S3 actual capture: notification is ephemeral and not replayed on later opportunities");
        ModsConfig.BiotechActive = false;
        Check(Capture()["us.core/BiotechAvailable"] == Truth.False, "S3 actual capture: no DLC fact stays false without deleting configuration");
        ModsConfig.BiotechActive = true;

    }    private static void S3NewAuthorDoesNotNeedLegacyTable()
    {
        var race = new ThingDef { defName = "S3BabyRace", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(race);
        var catalog = new SqueakXenotypeCatalogSnapshot();
        catalog.RaceDefNames.Add(race.defName);
        VoicePackCompAttach.Apply(catalog);
        Check(race.comps[0] is CompProperties_Squeaker && race.comps.Count == 1, "new native Comp attachment has no second action-plan authority");
        Check(UsControlledActionEligibility.Allows(true, BehaviorScope.AnyOccurrence, false, false), "explicit author contract can use an attached subject without old Comp action table");
        Check(!UsControlledActionEligibility.Allows(false, BehaviorScope.AnyOccurrence, false, false), "folded explicit disabled remains a hard gate");
        Check(!UsControlledActionEligibility.Allows(true, BehaviorScope.ActiveCommand, false, true), "registered author cannot bypass active-command scope");    }
}

