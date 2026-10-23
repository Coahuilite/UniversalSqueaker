using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.OneShot;
using Verse;
using Verse.AI;
using Verse.Sound;

internal static partial class Program
{
    private static void DcProjectAudio()
    {
        UsOneShotRuntime.Initialize();
        var directory = new SqueakXenotypeCatalogSnapshot();
        var selected = new Dictionary<AudioDomain, HashSet<string>>();
        foreach (var pack in UsAuthorVoicePacks.Directory)
        {
            pack.TryGetPackKey(out string key);
            directory.RacePacks.Add(pack);
            selected[new AudioDomain(new RaceKey(pack.raceDefName), null)] = new HashSet<string> { key };
        }
        SqueakRuntimeResolver.Current.Registry = new SqueakPoolRegistry(SqueakKernelAdapter.BuildEntries(directory, selected), BuiltInFallbackTable.Empty);
    }
    private static void DcDamage(Pawn pawn, float value)
    {
#if US_FIXTURE_DC_MISSING_WOUNDED
        throw new InvalidOperationException("Physically missing native method");
#else
        pawn.PostApplyDamage(new DamageInfo(), value);
#endif
    }
    private static void DcNative(bool unselected, bool missing)
    {
        Console.WriteLine("MODE DC native: actual Harmony -> production notifications/facts/config/session/output; explicit platform doubles; unselected=" + unselected + " missing=" + missing);
        // The process fact supports AI and commanded work; explicitly choose Any for these coverage cases.
        // The inherited commandWork case below retains its own independent ActiveCommand setting.
        UniversalSqueakerMod.Settings.actionTuning.Add(new ActionTuningRecord { actionKey = "us.core/Work", hasScope = true, scope = SqueakActionScope.AnyOccurrence });
        DefDatabase<UsActionDef>.AllDefsListForReading.Clear();
        DefDatabase<UsProviderDef>.AllDefsListForReading.Clear();
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Clear();
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Clear();
        var race = new ThingDef { defName = "DC_RaceA", race = new object() };
        var otherRace = new ThingDef { defName = "DC_RaceB", race = new object() };
        foreach (var target in new[] { race, otherRace })
        {
            DefDatabase<ThingDef>.AllDefsListForReading.Add(target);
            var pack = new UsVoicePackDef { defName = "US_DC_" + target.defName, modContentPack = new ModContentPack { PackageId = "fixture.dc" } };
            pack.declaration.Race = target.defName;
            foreach (string action in BuiltInPawnContracts.CandidateOrder)
                pack.declaration.Entries.Add(new VoiceEntry { Key = action, Action = "us.core/" + action, Sound = "Ratkin1" });
            DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        }
        const string commandWork = "vendor/CommandWork";
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = commandWork, BaseAction = "us.core/Work", Priority = 100, Condition = new Condition("ref", "us.core/HasCurrentInjury") } });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading[0].declaration.Entries.Add(new VoiceEntry { Key = "CommandWork", Action = commandWork, Sound = "Ratkin1" });
        DcProjectAudio();
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        int serial = 0;
        Pawn Pawn(ThingDef? target = null)
        {
            var p = new Pawn { ThingID = "DC_Pawn" + ++serial, def = target ?? race, IsPlayerControlled = true,
                jobs = new Pawn_JobTracker(), pather = new Pawn_PathFollower(), mindState = new Pawn_MindState() };
            p.Comp = new CompSqueaker { Pawn = p };
            return p;
        }
        void Request(Pawn p, string action, string role)
        {
            var request = p.Comp!.LastCandidateRequest;
            Check(request != null && request.Action == "us.core/" + action && request.Subject == p.ThingID && request.SubjectRole == role
                && request.Domain == p.def.defName && request.Audio.SourceDomain.Race.DefName == p.def.defName
                && request.Audio.EntryKey.EndsWith("/" + action, StringComparison.Ordinal)
                && request.Audio.AudioId == "actual.provider/Ratkin1"
                && RuntimeSession(p).LastOutcome.Receipt?.Result == PlaybackResult.Dispatched,
                "DC actual request " + action + " subject/role/full key/entry/audio/supplying domain " + role);
        }
        void Event(Pawn p, string action, string role, Action invoke)
        {
            Find.TickManager.TicksGame += 100;
            int starts = SoundStarter.Played.Count;
            invoke();
            Request(p, action, role);
            Check(SoundStarter.Played.Count == starts + 1, "DC one actual dispatch for " + action);
            Console.WriteLine("DC " + RuntimeSession(p).DiagnosticText);
        }
        void State(Pawn p, string action, string role) => Event(p, action, role, () => p.Comp!.CompTick());
        var actor = Pawn();
        if (!missing)
        {
            // The candidate cannot be adopted merely because its methods were patched at startup.
            int starts = SoundStarter.Played.Count;
            new Verb_AbilityShoot { caster = actor }.Execute();
            new Verb_MeleeAttack { caster = actor, Result = false }.Execute();
            DcDamage(actor, 0);
            new Selector().Select(actor);
            var tracker = new Pawn_EquipmentTracker { pawn = actor };
            actor.CurJob = new Job { def = JobDefOf.Equip, playerForced = true };
            tracker.AddEquipment(new ThingWithComps());
            new MentalBreakWorker().TryStart(actor, "fixture", false);
            var command = new Command_Toggle { hotKey = KeyBindingDefOf.Command_ColonistDraft, tutorTag = "Draft", toggleAction = () => actor.Drafted = !actor.Drafted };
            actor.FixtureGizmos.Add(command); actor.GetGizmos().ToArray(); command.toggleAction!();
            Check(!UsOneShotRuntime.DevelopmentActive && !UsOneShotRuntime.IsControlled(actor) && SoundStarter.Played.Count == starts,
                "DC unactivated real source callbacks create no owner or output; support set is not an ordinary default");
            actor.CurJob = null;
        }
        UsOneShotRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1,
            unselected ? UsEquipQualification.Unselected : UsEquipQualification.PlayerCoreEquip));
        if (missing)
        {
            Check(UsPawnEventObservation.Wounded == null && !UsPawnEventObservation.Available("us.core/WoundedEvent"), "DC physically absent Wounded method detected");
            Check(!UsAttackObservation.MeleeReady && UsAttackObservation.Ranged.Length == 7, "DC physically absent melee marker disables paired observer, not ranged roots");
            Check(Harmony.GetPatchInfo(UsAttackObservation.Melee!) == null, "DC no half melee scope remains installed");
            Check(UsPawnFacts.Capture(actor, "us.core/Wounded")["us.core/WoundedEvent"] == Truth.Unknown, "DC direct notification cannot forge missing Wounded implementation");
            Event(actor, "Attack", "Attacker", () => new Verb_LaunchProjectile { caster = actor }.Execute());
            int starts = SoundStarter.Played.Count;
            Find.TickManager.TicksGame++;
            actor.Comp!.Notify_Wounded(0);
            Check(SoundStarter.Played.Count == starts && UsPawnEventObservation.Diagnostics.Any(x => x.Reason.Contains("Pawn.PostApplyDamage")), "DC missing path named, unrelated production Attack remains usable");
            return;
        }
        if (unselected)
        {
            actor.CurJob = new Job { def = JobDefOf.Equip, playerForced = true };
            new Pawn_EquipmentTracker { pawn = actor }.AddEquipment(new ThingWithComps());
            Check(RuntimeSession(actor).LastOutcome.Reason == "EquipQualificationRequired" && actor.Comp!.LastCandidateRequest == null,
                "DC unresolved official Equip qualification is not silently opened or hidden in the native fact");
            return;
        }
        Event(actor, "Select", "SelectedSubject", () => new Selector().Select(actor));
        foreach (float value in new[] { 0f, 4f })
        {
            Event(actor, "Wounded", "DamageRecipient", () => DcDamage(actor, value));
            Check(actor.DamageReturned && RuntimeSession(actor).DiagnosticText.Contains("PostApplyDamage:Returned,totalDamageDealt=" + value), "DC Wounded uses normal-return stage and preserves actual zero/positive meaning");
        }
        int beforeFailure = SoundStarter.Played.Count;
        actor.ThrowDamage = true; try { DcDamage(actor, 1); } catch (InvalidOperationException) { } actor.ThrowDamage = false;
        Check(SoundStarter.Played.Count == beforeFailure, "DC throwing damage body never publishes normal-return Wounded");
        foreach (Verb verb in new Verb[] { new Verb_LaunchProjectile(), new Verb_Shoot(), new Verb_ShootOneUse(), new Verb_LaunchProjectileStaticOneUse(),
            new Verb_AbilityShoot(), new Verb_ShootBeam(), new Verb_Spray(), new Verb_ArcSprayIncinerator(), new Verb_SpewFire(), new Verb_Ignite(), new Verb_PowerBeam(), new Verb_Bombardment() })
        {
            verb.caster = actor;
            Event(actor, "Attack", "Attacker", () => verb.Execute());
            Check(verb.Executions == 1, "DC actual mechanism executes once, including wrapper chain " + verb.GetType().Name);
            beforeFailure = SoundStarter.Played.Count; verb.Result = false; Find.TickManager.TicksGame++;
            verb.Execute(); Check(SoundStarter.Played.Count == beforeFailure, "DC no execution fact on supported mechanism early false " + verb.GetType().Name);
        }
        foreach (bool hit in new[] { false, true })
            Event(actor, "Attack", "Attacker", () => new Verb_MeleeAttack { caster = actor, Result = hit }.Execute());
        beforeFailure = SoundStarter.Played.Count;
        new Verb_MeleeAttack { caster = actor, Busy = true }.Execute();
        actor.Spawned = false; new Verb_MeleeAttack { caster = actor }.Execute(); actor.Spawned = true;
        try { new Verb_MeleeAttack { caster = actor, Throw = true }.Execute(); } catch (InvalidOperationException) { }
#if !US_FIXTURE_DC_MISSING_MELEE_MARKER
        actor.Drawer.Notify_MeleeAttackOn(new Thing());
#endif
        Check(SoundStarter.Played.Count == beforeFailure, "DC melee busy/unspawned/exception are not executed misses; scope clears and stray marker cannot publish");
        foreach (Verb verb in new Verb[] { new Verb_BeatFire(), new Verb_Jump(), new Verb_CastAbility(), new Verb_CastAbilityJump(), new Verb_CastTargetEffect(),
            new Verb_DeployBroadshield(), new Verb_DeployToxPack(), new Verb_DeployDeadlifePack(), new Verb_FirefoamPop(), new Verb_SmokePop(), new Verb_Spawn(), new Verb_MechCluster() })
        {
            verb.caster = actor; beforeFailure = SoundStarter.Played.Count; verb.Execute();
            Check(SoundStarter.Played.Count == beforeFailure, "DC utility/generic mechanism does not acquire Attack from bool/name " + verb.GetType().Name);
        }
        beforeFailure = SoundStarter.Played.Count; new Verb_LaunchProjectile { caster = new Thing() }.Execute();
        Check(SoundStarter.Played.Count == beforeFailure, "DC NonPawn caster is not silently assigned a Pawn host/operator");

        actor.CurJob = new Job { def = JobDefOf.Equip, playerForced = true };
        var equipment = new Pawn_EquipmentTracker { pawn = actor };
        var item = new ThingWithComps { ThingID = "DC_Equipment" };
        Event(actor, "Equip", "EquipmentWearer", () => equipment.AddEquipment(item));
        Check(RuntimeSession(actor).DiagnosticText.Contains("wearer=" + actor.ThingID + ",item=" + item.ThingID), "DC Equip retains actual wearer/item and returned phase");
        beforeFailure = SoundStarter.Played.Count; equipment.Notify_EquipmentAdded(new ThingWithComps());
        Check(SoundStarter.Played.Count == beforeFailure, "DC notification without actual current equipment relation is insufficient");
        Find.TickManager.TicksGame++; actor.CurJob.playerForced = false; equipment.AddEquipment(new ThingWithComps());
        Check(RuntimeSession(actor).LastOutcome.Reason == "ExplicitPlayerCoreEquipQualification" && SoundStarter.Played.Count == beforeFailure,
            "DC actual AI equipment fact is distinct from explicit official Dev response qualification");
        actor.CurJob = null; actor.Drafted = false;
        var toggle = new Command_Toggle { hotKey = KeyBindingDefOf.Command_ColonistDraft, tutorTag = "Draft", toggleAction = () => actor.Drafted = !actor.Drafted };
        actor.FixtureGizmos.Clear(); actor.FixtureGizmos.Add(toggle); actor.GetGizmos().ToArray(); actor.GetGizmos().ToArray();
        Event(actor, "Draft", "DraftedPawn", () => toggle.toggleAction!());
        Event(actor, "Undraft", "UndraftedPawn", () => toggle.toggleAction!());
        beforeFailure = SoundStarter.Played.Count; actor.Drafted = true; actor.Drafted = false;
        var noChange = new Command_Toggle { hotKey = KeyBindingDefOf.Command_ColonistDraft, tutorTag = "Draft", toggleAction = () => { } };
        actor.FixtureGizmos.Clear(); actor.FixtureGizmos.Add(noChange); actor.GetGizmos().ToArray(); noChange.toggleAction!();
        Check(SoundStarter.Played.Count == beforeFailure, "DC setters and genuine no-change player command manufacture neither Draft nor Undraft");
        Event(actor, "MentalBreak", "BreakSubject", () => new MentalBreakWorker().TryStart(actor, "fixture", false));
        foreach (MentalBreakWorker source in new MentalBreakWorker[] { new MentalBreakWorker_Catatonic(), new MentalBreakWorker_RunWild(),
            new MentalBreakWorker_WildDecree(), new MentalBreakWorker_HumanityBreak() })
        {
            Event(actor, "MentalBreak", "BreakSubject", () => source.TryStart(actor, "fixture", false));
            Check(RuntimeSession(actor).DiagnosticText.Contains(source is MentalBreakWorker_HumanityBreak
                ? "MentalBreak:Verse.AI.MentalBreakWorker.TryStart:True" : source.GetType().FullName + ".TryStart:True"),
                "DC positive break mechanism retains its actual start source, wrapper not duplicated " + source.GetType().Name);
        }
        beforeFailure = SoundStarter.Played.Count; new MentalBreakWorker { Result = false }.TryStart(actor, "fixture", true);
        try { new MentalBreakWorker { Throw = true }.TryStart(actor, "fixture", true); } catch (InvalidOperationException) { }
        Check(SoundStarter.Played.Count == beforeFailure, "DC failed/throwing positive MentalBreak source does not publish startup");

        var idle = Pawn(); idle.CurJob = new Job { def = new JobDef { defName = "Wait" } }; idle.mindState!.IsIdle = true;
        State(idle, "Call", "IdlePawn"); idle.CurJob.def.defName = "Wait_Combat";
        beforeFailure = SoundStarter.Played.Count; Find.TickManager.TicksGame += 100; idle.Comp!.CompTick();
        Check(SoundStarter.Played.Count == beforeFailure && idle.Comp.LastCandidateRequest == null, "DC positive Call never falls back from unknown/combat job");
        idle.CurJob.def.defName = "Wait_Wander"; State(idle, "Call", "IdlePawn");
        var mover = Pawn(); mover.pather!.Moving = true; State(mover, "Move", "SelfMover");
        var sleeper = Pawn(); sleeper.jobs!.curDriver = new JobDriver { asleep = true }; State(sleeper, "Sleep", "Sleeper");
        var food = new Thing { def = new ThingDef { ingestible = new object() } };
        var feeder = Pawn(); var feedJob = new Job { targetA = new LocalTargetInfo { Thing = food }, targetB = new LocalTargetInfo { Pawn = sleeper } };
        var feed = new JobDriver_FoodFeedPatient { pawn = feeder, job = feedJob }; feeder.CurJob = feedJob; feeder.jobs!.curDriver = feed; feed.SetupToils();
        Check(UsPawnFacts.Capture(sleeper, null)[EatPolicy.Process] == Truth.True, "DC overlap is real feed observation, not a fake fact dictionary");
        State(sleeper, "Sleep", "Sleeper");
        Check(sleeper.Comp!.LastCandidateRequest!.SubjectRole != "ActualIngester", "DC an incidental Eat fact cannot overwrite selected Sleep role");
        sleeper.jobs.curDriver.asleep = false; State(sleeper, "Eat", "ActualIngester"); feed.Cleanup(JobCondition.Succeeded);
        var social = Pawn(); social.CurJob = new Job { def = new JobDef { defName = "StandAndBeSociallyActive" } }; State(social, "Social", "SocialParticipant");
        social.CurJob.def.defName = "UnknownSocialChatJob";
        Check(UsPawnFacts.Capture(social, null)["us.core/SocialState"] == Truth.False, "DC names resembling social mechanisms are not semantic evidence");
        social.CurJob.def.joyKind = JoyKindDefOf.Social;
        Check(UsPawnFacts.Capture(social, null)["us.core/JoyState"] == Truth.True && UsPawnFacts.Capture(social, null)["us.core/SocialState"] == Truth.True,
            "DC Social and Joy genuinely overlap rather than falsifying one fact");
        State(social, "Social", "SocialParticipant"); social.CurJob.def.joyKind = new JoyKindDef(); State(social, "Joy", "JoyParticipant");
        foreach (var pair in new (string Job, JobDriver Driver)[] { ("Research", new JobDriver_Research()), ("Mine", new JobDriver_Mine()), ("DoBill", new JobDriver_DoBill()),
            ("Repair", new JobDriver_Repair()), ("Clean", new JobDriver_CleanFilth()), ("HaulToCell", new JobDriver_HaulToCell()), ("FinishFrame", new JobDriver_ConstructFinishFrame()) })
        {
            var worker = Pawn(); var job = new Job { def = new JobDef { defName = pair.Job } };
            worker.CurJob = job; worker.jobs!.curDriver = pair.Driver; pair.Driver.pawn = worker; pair.Driver.job = job;
            Check(UsPawnFacts.Capture(worker, null)["us.core/WorkState"] == Truth.False, "DC Work no active Toil is false " + pair.Job);
            pair.Driver.FixtureToil = new Toil(); State(worker, "Work", "Worker");
            pair.Driver.job = new Job();
            var captured = UsPawnFacts.Capture(worker, null, out var diagnostics);
            Check(captured["us.core/WorkState"] == Truth.Unknown && diagnostics.Any(x => x.Reason.Contains("Mismatch")), "DC Work current Job/driver mismatch is named Unknown " + pair.Job);
        }
        var labelled = Pawn(); labelled.CurJob = new Job { def = new JobDef { defName = "UnknownWork" }, workGiverDef = new object() };
        Check(UsPawnFacts.Capture(labelled, null)["us.core/WorkState"] == Truth.Unknown, "DC workGiver tag alone cannot grant Work");

        // A full child identity must receive actual state command provenance, not only built-in tail names.
        var commanded = Pawn(); var researchJob = new Job { def = new JobDef { defName = "Research" } };
        var researcher = new JobDriver_Research { pawn = commanded, job = researchJob, FixtureToil = new Toil() };
        commanded.CurJob = researchJob; commanded.jobs!.curDriver = researcher;
        commanded.health!.hediffSet!.hediffs.Add(new Hediff_Injury { Severity = 1 });
        Check(UniversalSqueakerMod.Settings.TryChangeBehaviorOverride("Pawn", "", race.defName, "", commandWork,
            new BehaviorValues { Scope = SqueakActionScope.ActiveCommand, HardIntervalTicks = 9, Volume = .3 }, () => true, out _),
            "DC full child settings use production sparse writer");
        beforeFailure = SoundStarter.Played.Count; Find.TickManager.TicksGame += 100; commanded.Comp!.CompTick();
        Check(SoundStarter.Played.Count == beforeFailure && RuntimeSession(commanded).LastOutcome.Action == commandWork,
            "DC automatic Work process remains true but explicit child command scope refuses without fallback");
        researchJob.playerForced = true; Find.TickManager.TicksGame += 100; commanded.Comp.CompTick();
        var childRequest = commanded.Comp.LastCandidateRequest;
        Check(childRequest?.Action == commandWork && childRequest.SubjectRole == "Worker" && childRequest.Audio.Volume == .3
            && childRequest.Audio.EntryKey.EndsWith("/CommandWork", StringComparison.Ordinal)
            && RuntimeSession(commanded).State.ActionDeadline(commandWork) == Find.TickManager.TicksGame + 9,
            "DC child keeps actual command/Worker role, independent full config/entry/deadline and direct AudioId reuse");

        var victim = Pawn(otherRace);
        Check(UniversalSqueakerMod.Settings.TryChangeBehaviorOverride("Pawn", "", otherRace.defName, "", "us.core/Wounded",
            new BehaviorValues { HardIntervalTicks = 7, Volume = .25 }, () => true, out _), "DC victim has independent accurate-domain effective settings");
        beforeFailure = SoundStarter.Played.Count; Find.TickManager.TicksGame += 100;
        new Verb_LaunchProjectile { caster = actor, OnExecution = () => DcDamage(victim, 0) }.Execute();
        Request(actor, "Attack", "Attacker"); Request(victim, "Wounded", "DamageRecipient");
        Check(SoundStarter.Played.Count == beforeFailure + 2 && !ReferenceEquals(RuntimeSession(actor), RuntimeSession(victim))
            && victim.Comp!.LastCandidateRequest!.Audio.Volume == .25
            && RuntimeSession(victim).State.ActionDeadline("us.core/Wounded") == Find.TickManager.TicksGame + 7,
            "DC real nested attacker/victim callbacks use different owners/domains/config/protection even with the same AudioId");

        var sustainSound = DefDatabase<SoundDef>.AllDefsListForReading.First(x => x.defName == "Ratkin1");
        sustainSound.sustain = true; mover.Comp!.FixtureSustained = true;
        Find.TickManager.TicksGame += 100; mover.Comp.CompTick(); var movingHandle = SoundStarter.Spawned.Last();
        mover.pather!.Moving = false; Find.TickManager.TicksGame += 60; mover.Comp.CompTick();
        Check(movingHandle.Ended, "DC Move Sustained ends at current-state review, without cutting an already dispatched OneShot");
        mover.pather.Moving = true; Find.TickManager.TicksGame += 100; mover.Comp.CompTick();
        var invalidParameterHandle = SoundStarter.Spawned.Last();
        Check(!invalidParameterHandle.Ended, "DC Move has a live owned slot before changing its effective configuration");
        Check(UniversalSqueakerMod.Settings.TryChangeBehaviorOverride("Pawn", "", race.defName, "", "us.core/Move",
            new BehaviorValues { SelectRequireAwake = false }, () => true, out _), "DC stores the explicit Pawn parameter contribution before contract applicability is checked");
        RuntimeSession(mover).Maintain(force: true);
        Check(invalidParameterHandle.Ended, "DC Select-only fields on a non-selection contract invalidate its current Sustained authorization, not only its next start");
        actor.Comp!.FixtureSustained = true;
        Find.TickManager.TicksGame += 100; DcDamage(actor, 0); var woundedHandle = SoundStarter.Spawned.Last();
        Find.TickManager.TicksGame += 60; actor.Comp.CompTick();
        Check(woundedHandle.Ended, "DC transient Wounded event does not latch into a current wound/Sustained lifetime");
        mover.Comp.FixtureSustained = false; actor.Comp.FixtureSustained = false; sustainSound.sustain = false;
        var dying = Pawn();
        Event(dying, "Death", "DyingSubject", () => dying.Kill(null));
        Check(dying.KillEntered && !dying.Spawned && RuntimeSession(dying).DiagnosticText.Contains("Pawn.Kill:Entering"), "DC Death request was made before Kill body/despawn, not a death-complete receipt");
        dying.Comp!.CompTick(); Check(!UsOneShotRuntime.IsControlled(dying), "DC unavailable dying subject releases its existing owner at the lifecycle entry");
        UsOneShotRuntime.ResetSession();
    }
}
