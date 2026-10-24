using System;
using System.Collections.Generic;
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
    private static void DbResultRouting()
    {
        Console.WriteLine("MODE --db-result-routing: production Thing.Ingested Harmony -> catalog/session/decision/output; platform doubled");
        const string eat = "us.core/Eat", after = "example/AfterIngestion", child = "example/InjuredAfterIngestion";
        DefDatabase<UsActionDef>.AllDefsListForReading.Clear();
        DefDatabase<UsProviderDef>.AllDefsListForReading.Clear();
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Clear();
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Clear();
        var race = new ThingDef { defName = "DB_ResultRoutingRace", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(race);
        var pack = new UsVoicePackDef { defName = "US_DB_OnlyEat", modContentPack = new ModContentPack { PackageId = "fixture.db.routing" } };
        pack.declaration.Race = race.defName;
        pack.declaration.Entries.Add(new VoiceEntry { Key = "Eat", Action = eat, Sound = "Ratkin1" });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        void Initialize()
        {
            SqueakerRuntime.Initialize();
            var directory = new SqueakXenotypeCatalogSnapshot();
            var selections = new Dictionary<AudioDomain, HashSet<string>>();
            foreach (var projected in VoicePackDirectory.Directory)
            {
                projected.TryGetPackKey(out string key);
                directory.RacePacks.Add(projected);
                selections[new AudioDomain(new RaceKey(projected.raceDefName), null)] = new HashSet<string> { key };
            }
        }
        (Pawn Pawn, Thing Food, JobDriver_Ingest Driver) Begin(string id, float nutrition)
        {
            var pawn = new Pawn { ThingID = id, def = race, jobs = new Pawn_JobTracker() };
            pawn.Comp = new CompSqueaker { Pawn = pawn };
            var food = new Thing { ThingID = id + "Food", NutritionResult = nutrition,
                def = new ThingDef { defName = "DB_RoutingFood", ingestible = new object(), IsNutritionGivingIngestible = true } };
            var job = new Job { def = JobDefOf.Ingest, targetA = new LocalTargetInfo { Thing = food } };
            var driver = new JobDriver_Ingest { pawn = pawn, job = job };
            pawn.CurJob = job; pawn.jobs.curDriver = driver; driver.SetupToils();
            return (pawn, food, driver);
        }
        Initialize();
        SqueakerRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 3, 4, 4, 0, 1, 1));
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        var catalog = (DefinitionCatalog)typeof(SqueakerRuntime).GetField("catalog", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Check(pack.declaration.Entries.Count == 1 && DefDatabase<UsActionDef>.AllDefsListForReading.Count == 0
            && !catalog.Actions.Any(x => x.Condition.RequiredFacts().Contains(EatPolicy.Result)),
            "DB routing baseline is truly audio-only Eat with no Result-consuming contract, not a disabled high-priority root");
        var observations = new List<(bool Pass, string Label)>();
        foreach (float nutrition in new[] { 0f, .5f })
        {
            Find.TickManager.TicksGame += 100;
            var (pawn, food, driver) = Begin("DB_OnlyEat_" + nutrition, nutrition);
            SqueakerRuntime.TryTick(pawn);
            var session = RuntimeSession(pawn);
            Check(session.LastOutcome.Action == eat && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched,
                "DB routing ordinary state Eat first dispatch nutrition=" + nutrition);
            long due = session.State.NextStateTick;
            long attempt = session.State.AttemptTick;
            string before = session.DiagnosticText;
            int starts = SoundStarter.Played.Count;
            Find.TickManager.TicksGame += 5; // hard deadlines elapsed, but the original soft state wait has not.
            float returned = food.Ingested(pawn, 1);
            Console.WriteLine("ONLY_EAT nutrition=" + returned + " tick=" + Find.TickManager.TicksGame + " originalDue=" + due
                + " extraStarts=" + (SoundStarter.Played.Count - starts) + " attemptBefore=" + attempt + " attemptAfter=" + session.State.AttemptTick
                + " before=[" + before + "] after=[" + session.DiagnosticText + "]");
            observations.Add((returned == nutrition && Find.TickManager.TicksGame < due
                && UsPawnFacts.Capture(pawn, null)[EatPolicy.Process] == Truth.True
                && ReferenceEquals(session, RuntimeSession(pawn)) && SoundStarter.Played.Count == starts
                && session.State.AttemptTick == attempt && session.DiagnosticText == before,
                "DB no Result consumer: normal return " + nutrition + " leaves playback, opportunity, attempts, cooldowns, rolls and diagnostics untouched"));
            SqueakerRuntime.TryTick(pawn);
            observations.Add((SoundStarter.Played.Count == starts && session.State.AttemptTick == attempt && session.DiagnosticText == before,
                "DB no Result consumer: state still waits for its original due tick nutrition=" + nutrition));
            starts = SoundStarter.Played.Count;
            Find.TickManager.TicksGame = (int)due;
            SqueakerRuntime.TryTick(pawn);
            observations.Add((SoundStarter.Played.Count == starts + 1 && session.State.ConfirmedTick == due && session.LastOutcome.Action == eat,
                "DB no Result consumer: original due state opportunity dispatches normally nutrition=" + nutrition));
            driver.Cleanup(JobCondition.Succeeded);

            var cold = Begin("DB_ColdOnlyEat_" + nutrition, nutrition);
            starts = SoundStarter.Played.Count;
            cold.Food.Ingested(cold.Pawn, 1);
            bool created = SqueakerRuntime.IsControlled(cold.Pawn);
            Console.WriteLine("COLD_ONLY_EAT nutrition=" + nutrition + " ownerCreated=" + created + " extraStarts=" + (SoundStarter.Played.Count - starts));
            observations.Add((!created && SoundStarter.Played.Count == starts,
                "DB no Result consumer: result alone does not allocate an owner or fabricate a response nutrition=" + nutrition));
            cold.Driver.Cleanup(JobCondition.Succeeded);
        }
        // Record both zero and positive observations before failing the pre-fix discriminator.
        foreach (var observation in observations) Check(observation.Pass, observation.Label);

        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = after, Priority = -100, Condition = new Condition("all", "", new Condition("ref", EatPolicy.Result)) } });
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = child, BaseAction = after, Priority = -90, Condition = new Condition("ref", "us.core/HasCurrentInjury") } });
        foreach (string action in new[] { after, child })
        {
            pack.declaration.Entries.Add(new VoiceEntry { Key = action.Split('/')[1], Action = action, Sound = "Ratkin1" });
            Check(SqueakerMod.Settings.TryReplaceBehaviorOverride("Pawn", "", race.defName, "", action,
                new BehaviorValues { HardIntervalTicks = 7 }, () => true, out _), "DB routing independent result interval " + action);
        }
        Initialize();
        Find.TickManager.TicksGame += 100;
        var active = Begin("DB_ExplicitResult", 0);
        SqueakerRuntime.TryTick(active.Pawn);
        var owner = RuntimeSession(active.Pawn);
        long stateDue = owner.State.NextStateTick, eatDeadline = owner.State.ActionDeadline(eat);
        Check(owner.LastOutcome.Action == eat, "DB result contracts are false on ordinary state opportunity");
        foreach (float nutrition in new[] { 0f, .5f })
        {
            Find.TickManager.TicksGame += nutrition == 0 ? 5 : 6;
            active.Food.NutritionResult = nutrition;
            int starts = SoundStarter.Played.Count;
            active.Food.Ingested(active.Pawn, 1);
            Console.WriteLine("EXPLICIT_RESULT nutrition=" + nutrition + " " + owner.DiagnosticText);
            Check(ReferenceEquals(owner, RuntimeSession(active.Pawn)) && owner.LastOutcome.Action == after
                && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && SoundStarter.Played.Count == starts + 1,
                "DB lower-priority independent Result root responds via same owner, without ordinary Eat stealing the event nutrition=" + nutrition);
            Check(owner.State.ActionDeadline(after) == Find.TickManager.TicksGame + 7 && owner.State.ActionDeadline(eat) == eatDeadline
                && owner.State.SharedDeadline == Find.TickManager.TicksGame + 3
                && owner.State.NextStateTick == stateDue && Find.TickManager.TicksGame < stateDue
                && owner.DiagnosticText.Contains("nutrition=" + nutrition),
                "DB result keeps its own interval/payload and existing shared settlement, never consumes Eat soft wait nutrition=" + nutrition);
            if (nutrition == 0)
            {
                Find.TickManager.TicksGame++;
                active.Food.Ingested(active.Pawn, 1);
                Check(SoundStarter.Played.Count == starts + 1 && owner.LastOutcome.Action == after && owner.LastOutcome.Reason == "HardDeadline",
                    "DB Result still obeys the same owner's hard deadline; no fallback to ordinary Eat");
            }
        }
        Find.TickManager.TicksGame += 4;
        active.Pawn.health!.hediffSet!.hediffs.Add(new Hediff_Injury { Severity = 1 });
        active.Food.Ingested(active.Pawn, 1);
        Check(ReferenceEquals(owner, RuntimeSession(active.Pawn)) && owner.LastOutcome.Action == child
            && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && owner.State.NextStateTick == stateDue,
            "DB inherited Result consumer is routed by compiled fixed condition, not only a direct ref or root name");
        Find.TickManager.TicksGame = (int)stateDue;
        SqueakerRuntime.TryTick(active.Pawn);
        Check(owner.LastOutcome.Action == eat && owner.State.ConfirmedTick == stateDue,
            "DB legitimate results leave the original ordinary Eat state opportunity usable");
        active.Driver.Cleanup(JobCondition.Succeeded);
        SqueakerRuntime.ResetSession();
    }
}
