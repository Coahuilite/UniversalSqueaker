using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;
using Verse;
using Verse.AI;
using RimWorld;
using HarmonyLib;
using Verse.Sound;

internal static partial class Program
{
    public sealed class DbResultProvider : IUsDefinitionProvider
    {
        public void Collect(ProviderBatch batch) => batch.AddResponse(new DbResultResponse());
    }
    private sealed class DbResultResponse : IUsResponseRule
    {
        public string Key => "example/ResultMetadata";
        public ResponseStage Stage => ResponseStage.Age;
        internal static IngestionResultFact? Seen;
        public ResponseContribution Evaluate(ResponseContext context)
        {
            Seen = context.Ingestion;
            return new ResponseContribution(1, 1);
        }
    }
    private static void DbEat()
    {
#if US_FIXTURE_EAT_MISSING_CHEW
        throw new InvalidOperationException("Use --db-eat-missing for this compiled fixture");
#else
        Console.WriteLine("MODE --db-eat: native-shape Harmony + production source/config/session/output, platform doubled");
        const string injured = "example/InjuredEat", after = "example/AfterIngestion", precise = "example/PreciseEat";
        DefDatabase<UsProviderDef>.AllDefsListForReading.Add(new UsProviderDef { providerClass = typeof(DbResultProvider) });
        var race = new ThingDef { defName = "DB_Ratkin", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(race);
        var pawn = new Pawn { ThingID = "DB_Recipient", def = race, jobs = new Pawn_JobTracker() };
        pawn.Comp = new CompSqueaker { Pawn = pawn };
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = injured, BaseAction = "us.core/Eat", Priority = 50, Condition = new Condition("ref", "us.core/HasCurrentInjury") } });
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = precise, BaseAction = "us.core/Eat", Priority = 51, Condition = new Condition("all", "", new Condition("ref", EatPolicy.Execution), new Condition("ref", "us.core/HasCurrentInjury")) } });
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = after, Priority = 60, Condition = new Condition("ref", EatPolicy.Result) } });
        var pack = new UsVoicePackDef { defName = "US_DB_Minimal", modContentPack = new ModContentPack { PackageId = "fixture.db" } };
        pack.declaration.Race = race.defName;
        const string maintainedEat = "fixture.db/MaintainedEat";
        DeclareMaintainedChild(pack, maintainedEat, CoreActions.Eat);
        foreach (string key in new[] { "us.core/Eat", injured, after, precise })
            pack.declaration.Entries.Add(new VoiceEntry { Key = key.Split('/')[1], Action = key, Sound = "Ratkin1" });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        SqueakerRuntime.Initialize();
        var directory = new SqueakXenotypeCatalogSnapshot();
        var selections = new Dictionary<AudioDomain, HashSet<string>>();
        foreach (var projected in VoicePackDirectory.Directory)
        {
            projected.TryGetPackKey(out string key);
            directory.RacePacks.Add(projected);
            selections[new AudioDomain(new RaceKey(projected.raceDefName), null)] = new HashSet<string> { key };
        }
        SqueakerRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1));
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        var settings = SqueakerMod.Settings;
        void Set(string action, BehaviorValues values)
            => Check(settings.TryChangeBehaviorOverride("Pawn", "", race.defName, "", action, values, () => true, out _), "DB player full ActionId publish " + action);
        Set(injured, new BehaviorValues { Enabled = false });
        Set(precise, new BehaviorValues { Enabled = false });
        Set(after, new BehaviorValues { HardIntervalTicks = 0, ResponseEnabled = new Dictionary<string, bool> { ["example/ResultMetadata"] = true } });
        void Advance() => Find.TickManager.TicksGame += 200;
        void Tick() { Advance(); SqueakerRuntime.TryTick(pawn); }
        Thing Food(bool nutrition, bool drug) => new() { def = new ThingDef { defName = "DBFood", ingestible = new object(), IsNutritionGivingIngestible = nutrition, IsDrug = drug } };
        var food = Food(true, false);
        JobDriver_Ingest Begin()
        {
            var job = new Job { def = JobDefOf.Ingest, targetA = new LocalTargetInfo { Thing = food } };
            var driver = new JobDriver_Ingest { pawn = pawn, job = job };
            pawn.CurJob = job; pawn.jobs!.curDriver = driver; driver.SetupToils();
            return driver;
        }
        var driver = Begin();
        var chew = Toils_Ingest.ChewIngestible(pawn, 1, TargetIndex.A); chew.actor = pawn;
        Truth Fact(string key) => UsPawnFacts.Capture(pawn, null)[key];
        Check(Fact(EatPolicy.Process) == Truth.True && Fact(EatPolicy.Execution) == Truth.False, "DB factory construction is not execution entry");
        Tick();
        var session = RuntimeSession(pawn);
        Check(session.LastOutcome.Action == "us.core/Eat" && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && session.DiagnosticText.Contains("sourceDomain=DB_Ratkin") && session.DiagnosticText.Contains("role=ActualIngester"),
            "DB minimal audio-only pack defaults Whole/Any/Any -> production dispatch with exact subject/domain");
        var xeno = new XenotypeDef { defName = "DB_LegalXeno" };
        Check(settings.TryChangeBehaviorOverride("Pawn", "", race.defName, xeno.defName, "us.core/Eat",
            new BehaviorValues { Phase = EatPhase.Execution }, () => true, out _), "DB exact Xeno player phase published");
        pawn.genes = new FixtureGenes { Xenotype = xeno }; Tick();
        Check(session.LastOutcome.Receipt == null, "DB canonical legal Xeno parameter is consumed by production session and refuses preparation");
        GameRuntime.Current.RejectChildContext = true; Tick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && session.DiagnosticText.Contains("sourceDomain=DB_Ratkin"),
            "DB resolver-rejected raw Xeno cannot leak precise parameters; same Race mapping remains accurate");
        GameRuntime.Current.RejectChildContext = false; pawn.genes = null;
        Set("us.core/Eat", new BehaviorValues { Phase = EatPhase.Execution });
        Tick();
        Check(session.LastOutcome.Receipt == null, "DB Execution parameter refuses preparation without broadening Whole");
        Toils_Ingest.FixtureInit = () => throw new InvalidOperationException("DB native init failed");
        var failed = Toils_Ingest.ChewIngestible(pawn, 1, TargetIndex.A); failed.actor = pawn; driver.FixtureToil = failed;
        Toils_Ingest.FixtureInit = null;
        try { failed.initAction!(); } catch (InvalidOperationException) { }
        Check(Fact(EatPolicy.Execution) == Truth.False, "DB throwing original init never becomes execution entry");
        driver.FixtureToil = chew; chew.initAction!();
        Check(Fact(EatPolicy.Execution) == Truth.True, "DB actual successful init in current Toil establishes execution");
        Tick(); Console.WriteLine("DB execution " + session.DiagnosticText);
        Check(session.LastOutcome.Action == "us.core/Eat" && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched, "DB actual execution permits same owner dispatch");
        int starts = SoundStarter.Played.Count; chew.Finish(); driver.FixtureToil = null; Tick();
        Check(SoundStarter.Played.Count == starts && Fact(EatPolicy.Execution) == Truth.False, "DB OneShot exit stops new starts, no stop-tail operation");
        for (int n = 0; n < 2; n++)
        for (int d = 0; d < 2; d++)
        {
            food = Food(n == 1, d == 1); driver.job.targetA = new LocalTargetInfo { Thing = food };
            var facts = new FactSnapshot(UsPawnFacts.Capture(pawn, null));
            foreach (EatNutritionScope nutrition in Enum.GetValues(typeof(EatNutritionScope)))
            foreach (EatDrugScope drug in Enum.GetValues(typeof(EatDrugScope)))
            {
                bool expected = (nutrition == EatNutritionScope.Any || (nutrition == EatNutritionScope.NutritionGiving) == (n == 1))
                    && (drug == EatDrugScope.Any || (drug == EatDrugScope.OnlyDrugs) == (d == 1));
                Check(EatConditions.Restriction(EatPhase.WholeProcess, nutrition, drug).Evaluate(facts) == (expected ? Truth.True : Truth.False),
                    "DB actual item nutrition x independent drug " + n + d + "/" + nutrition + "/" + drug);
            }
        }
        driver.job.targetB = new LocalTargetInfo { Thing = Food(false, false) };
        var alternate = Toils_Ingest.ChewIngestible(pawn, 1, TargetIndex.B); alternate.actor = pawn; driver.FixtureToil = alternate; alternate.initAction!();
        Check(Fact(EatPolicy.Nutrition) == Truth.False && Fact(EatPolicy.Drug) == Truth.False,
            "DB execution item follows actual factory target index rather than assuming A");
        alternate.Finish(); driver.FixtureToil = null;
        driver.job.targetA = new LocalTargetInfo { Thing = new Thing() };
        var unknown = new FactSnapshot(UsPawnFacts.Capture(pawn, null));
        Check(EatConditions.Restriction(EatPhase.WholeProcess, EatNutritionScope.Any, EatDrugScope.Any).Evaluate(unknown) == Truth.True
            && EatConditions.Restriction(EatPhase.WholeProcess, EatNutritionScope.NutritionGiving, EatDrugScope.Any).Evaluate(unknown) == Truth.Unknown,
            "DB dispenser/unavailable item has Unknown attributes; Any has no optional dependency");
        driver.job.targetA = new LocalTargetInfo { Thing = food };
        Check(Fact(EatPolicy.Drug) == Truth.True, "DB dispenser target replacement resamples actual food, never keeps dispenser fact");
        driver.FixtureToil = chew; chew.initAction!(); chew.Finish(); chew.initAction!();
        Check(Fact(EatPolicy.Execution) == Truth.True && session.DiagnosticText.Contains("phaseGeneration=1"), "DB Corpse same Job/Toil may reenter; diagnostics do not resample");
        Check(UsIngestionObservation.Describe(pawn).Contains("phaseGeneration=4"), "DB genuine execution entries advance observation generation");
        // Whole and Execution use the identical production sustained slot and notification path.
        var sound = DefDatabase<SoundDef>.AllDefsListForReading.First(x => x.defName == "Ratkin1");
        sound.sustain = true; MaintainSubject(pawn, true);
        Set(maintainedEat, new BehaviorValues { Phase = EatPhase.WholeProcess }); Tick();
        var whole = SoundStarter.Spawned.Last();
        Check(pawn.Comp.LastCandidateRequest?.Action == maintainedEat, "DB explicit declared child supplies sustained shape independently of the official OneShot");
        chew.Finish();
        Check(!whole.Ended, "DB Whole sustained survives each chewing exit while process remains true");
        driver.Cleanup(JobCondition.InterruptForced);
        Check(whole.Ended && Fact(EatPolicy.Process) == Truth.False, "DB interruption immediately ends Whole through existing owned slot, no completion event");
        driver = Begin(); chew = Toils_Ingest.ChewIngestible(pawn, 1, TargetIndex.A); chew.actor = pawn; driver.FixtureToil = chew; chew.initAction!();
        Set(maintainedEat, new BehaviorValues { Phase = EatPhase.Execution }); Tick();
        var execution = SoundStarter.Spawned.Last(); chew.Finish();
        Check(execution.Ended && Fact(EatPolicy.Process) == Truth.True, "DB Execution sustained ends on exact phase exit while Whole process remains");
        MaintainSubject(pawn, false); sound.sustain = false;
        Set(injured, new BehaviorValues { HardIntervalTicks = 7, Volume = .25 });
        Set("us.core/Eat", new BehaviorValues { Phase = EatPhase.WholeProcess });
        pawn.health!.hediffSet!.hediffs.Add(new Hediff_Injury { Severity = 1 }); Tick();
        Check(session.LastOutcome.Action == injured && session.DiagnosticText.Contains("localVolume=0.25")
            && session.State.ActionDeadline(injured) == Find.TickManager.TicksGame + 7,
            "DB InjuredEat full independent child config and direct shared AudioId, not parent parameter inheritance");
        pawn.health.hediffSet.hediffs.Clear();
        Set(precise, new BehaviorValues { Phase = EatPhase.WholeProcess });
        pawn.health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = 1 });
        Advance(); pawn.Comp.NotifyExternalByKey(precise, SqueakTriggerOrigin.External, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Receipt == null && session.LastOutcome.Reason == "NoTrueAction",
            "DB explicit child call with injury true still cannot loosen fixed execution by Whole parameter");
        pawn.health.hediffSet.hediffs.Clear();
        driver.Cleanup(JobCondition.Succeeded);
        Advance(); food.NutritionResult = 0; food.Ingested(pawn, 1);
        Check(session.LastOutcome.Action == after && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched
            && session.DiagnosticText.Contains("role=ActualIngester") && session.DiagnosticText.Contains("nutrition=0"),
            "DB successful zero-nutrition return -> independent AfterIngestion root after phase exit");
        Check(DbResultResponse.Seen?.Ingester == SqueakerNotifications.SubjectKey(pawn) && DbResultResponse.Seen.Item == food.ThingID
            && DbResultResponse.Seen.ItemDef == food.def.defName && DbResultResponse.Seen.Nutrition == 0,
            "DB author response consumes immutable actual ingester/item/zero result from production capture");
        Set(after, new BehaviorValues { Phase = EatPhase.Execution });
        Advance(); food.Ingested(pawn, 1);
        Check(session.LastOutcome.Receipt == null && session.LastOutcome.Reason == "EatParametersRequireIngestionContract",
            "DB invalid explicit active-phase parameter on independent result root is rejected, never silently ignored");
        Set(after, new BehaviorValues { HardIntervalTicks = 0 });
        starts = SoundStarter.Played.Count; Advance(); food.ThrowIngest = true;
        try { food.Ingested(pawn, 1); } catch (InvalidOperationException) { }
        Check(SoundStarter.Played.Count == starts, "DB ingestion exception never publishes result");
        food.ThrowIngest = false; food.IngestibleNow = false; Advance(); food.Ingested(pawn, 1);
        Check(SoundStarter.Played.Count == starts, "DB vanilla invalid-item guard zero return is not a successful zero ingestion");
        // Feeding uses actor's moving item target, recipient's owner; not feeder.CurJob as recipient evidence.
        var feeder = new Pawn { ThingID = "DB_Feeder", def = race, jobs = new Pawn_JobTracker() }; feeder.Comp = new CompSqueaker { Pawn = feeder };
        var feedJob = new Job { targetA = new LocalTargetInfo { Thing = Food(true, true) }, targetB = new LocalTargetInfo { Pawn = pawn } };
        var feed = new JobDriver_FoodFeedPatient { pawn = feeder, job = feedJob };
        feeder.CurJob = feedJob; feeder.jobs!.curDriver = feed; feed.SetupToils();
        var feedChew = Toils_Ingest.ChewIngestible(pawn, 1.5f, TargetIndex.A); feedChew.actor = feeder;
        feed.FixtureToil = feedChew; feedChew.initAction!();
        Check(Fact(EatPolicy.Execution) == Truth.True && UsPawnFacts.Capture(feeder, null)[EatPolicy.Process] == Truth.False,
            "DB actual feed Deliveree owns process/execution, feeder never masquerades as ingester");
        Check(Fact(EatPolicy.Nutrition) == Truth.True && Fact(EatPolicy.Drug) == Truth.True, "DB feed attributes follow actor's actual item target");
        feed.Cleanup(JobCondition.Incompletable);
        Check(Fact(EatPolicy.Execution) == Truth.False && Fact(EatPolicy.Result) == Truth.False, "DB feed cleanup closes observations without manufactured completion");
        settings.globalVolumeFactor = 0; // The following accepted row transaction publishes this captured system setting too.
        Set("us.core/Eat", new BehaviorValues { Enabled = true, Volume = 1 });
        driver = Begin(); Tick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && SoundStarter.Played.Last().source.volume == 0,
            "DB system G zero cannot be counteracted by domain enabled/volume; silent dispatch retains settlement meaning");
        Check(!session.DiagnosticText.Contains("result=ingester="), "DB next state decision does not retain prior event result payload in current diagnostics");
        starts = SoundStarter.Played.Count; long confirmed = session.State.ConfirmedTick ?? long.MinValue;
        settings.voicePackMode = SqueakVoicePackMode.Disabled; settings.PublishBehaviorConfiguration(); Tick();
        Check(SoundStarter.Played.Count == starts && (session.State.ConfirmedTick ?? long.MinValue) == confirmed,
            "DB system service disabled cannot be reopened by enabled=true domain");
        SqueakerRuntime.ResetSession();
        Check(UsIngestionObservation.Describe(pawn) == "", "DB session reset clears ephemeral observation records, no save payload");
#endif
    }

    private static void DbEatMissing()
    {
        Console.WriteLine("MODE --db-eat-missing: physically absent ChewIngestible target");
        Check(UsIngestionObservation.Chew == null && !UsIngestionObservation.ExecutionReady, "DB physically missing execution factory is detected");
        Check(UsIngestionObservation.ProcessReady && UsIngestionObservation.Ingested != null && UsSelectObservation.Ready, "DB unrelated process/result/Select capabilities remain ready");
        var owner = Harmony.GetPatchInfo(UsIngestionObservation.Setup!);
        Check(owner != null && owner.Postfixes.Count > 0, "DB PatchAll completes with process observer installed, no global catch");
        var pawn = new Pawn { CurJob = new Job { def = JobDefOf.Ingest }, jobs = new Pawn_JobTracker { curDriver = new JobDriver() } };
        var capture = UsPawnFacts.Capture(pawn, null);
        Check(capture[EatPolicy.Process] == Truth.True && capture[EatPolicy.Execution] == Truth.Unknown, "DB Whole self-process survives missing precision; precision is Unknown");
        var view = new BehaviorConfiguration("", Array.Empty<BehaviorPlayerOverride>(), true, 1, 1);
        var domain = new BehaviorDomain("Pawn", "", "Ratkin");
        var directory = new BehaviorSourceDirectory(Array.Empty<BehaviorSource>());
        Check(!view.Resolve(domain, "us.core/Eat", new BehaviorValues { Phase = EatPhase.WholeProcess }, directory, UsIngestionObservation.Available).Paused,
            "DB Whole with Any does not require missing execution implementation");
        var precise = view.Resolve(domain, "example/Precise", new BehaviorValues { Phase = EatPhase.Execution }, directory, UsIngestionObservation.Available);
        Check(precise.Paused && precise.Failure.Contains(EatPolicy.Execution), "DB selected precision pauses without widening");
        Check(UsIngestionObservation.Diagnostics.Any(x => x.Key == EatPolicy.Execution && x.Reason.Contains("ChewIngestible")),
            "DB missing capability has exact target diagnostic");
    }
}
