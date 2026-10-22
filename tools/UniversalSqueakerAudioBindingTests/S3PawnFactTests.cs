using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UniversalSqueaker;
using UniversalSqueaker.OneShot;

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
        var properties = (CompProperties_Squeaker)race.comps[0];
        Check(properties.actions.Count == 15, "S3 actual auto attachment retains legacy 15-plan defaults rather than changing daily policy");
        foreach (var action in new[] { SqueakAction.Crying, SqueakAction.Giggling })
        {
            bool configured = properties.actions.Exists(x => x.action == action);
            bool permitted = SqueakActionEligibility.IsEligible(action, true);
            Check(!configured && UsControlledActionEligibility.Allows(configured, true, permitted, true, SqueakActionScope.AnyOccurrence, false, false),
                "S3 new author " + action + " is usable with automatic Comp without author writing legacy table");
            Check(!UsControlledActionEligibility.Allows(configured, true, SqueakActionEligibility.IsEligible(action, false), true, SqueakActionScope.AnyOccurrence, false, false),
                "S3 baby player permission off still rejects " + action);
            Check(!UsControlledActionEligibility.Allows(configured, true, permitted, false, SqueakActionScope.AnyOccurrence, false, false),
                "S3 explicit source/player disabled still rejects " + action);
            Check(!UsControlledActionEligibility.Allows(configured, false, permitted, true, SqueakActionScope.AnyOccurrence, false, false),
                "S3 retained legacy-only caller does not acquire new registration prerequisite or missing-plan admission " + action);
        }
        Check(!UsControlledActionEligibility.Allows(true, false, false, true, SqueakActionScope.AnyOccurrence, false, false),
            "S3 unregistered string allowExternal false is still a hard permission");
        Check(!UsControlledActionEligibility.Allows(true, true, true, true, SqueakActionScope.ActiveCommand, false, true),
            "S3 new author cannot bypass active-command scope");
    }
}

