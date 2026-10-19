using System;
using System.Collections.Generic;
using System.Reflection;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.OneShot;
using Verse;
using Verse.AI;
using Verse.Sound;

internal static partial class Program
{
    private static UsControlledOneShotSession RuntimeSession(Pawn pawn)
    {
        var field = typeof(UsOneShotRuntime).GetField("sessions", BindingFlags.NonPublic | BindingFlags.Static)!;
        return ((Dictionary<Pawn, UsControlledOneShotSession>)field.GetValue(null)!)[pawn];
    }

    private static void S4ActualRuntimeCallers()
    {
        var pawn = new Pawn
        {
            IsPlayerControlled = true,
            CurJob = new Job { def = new JobDef { defName = "Wait" } },
            jobs = new Pawn_JobTracker { curDriver = new JobDriver() },
            pather = new Pawn_PathFollower(),
            mindState = new Pawn_MindState { IsIdle = true },
            needs = new Pawn_NeedsTracker { mood = new FixtureMood { CurLevelPercentage = .9f } }
        };
        var comp = new CompSqueaker { Pawn = pawn };
        pawn.Comp = comp;
        DefDatabase<ThingDef>.AllDefsListForReading.Add(pawn.def);
        var pack = new UsVoicePackDef { defName = "RuntimePack", modContentPack = new ModContentPack { PackageId = "fixture.author" } };
        pack.declaration.Race = pawn.def.defName;
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
        {
            pack.declaration.Entries.Add(new VoiceEntry { Key = key, Action = "us.core/" + key, Sound = "Ratkin1", Volume = 2, Pitch = 2 });
        }
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        UsOneShotRuntime.Initialize();
        int before = SoundStarter.Played.Count;
        comp.CompTick();
        comp.NotifyExternalByKey("Wounded", SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        Check(UsOneShotRuntime.PolicyStatus == "PolicySelectionRequired" && !UsOneShotRuntime.IsControlled(pawn)
            && SoundStarter.Played.Count == before && comp.MembershipUpdates == 2,
            "S4 real CompTick and public event callers reach the one temporary selection gate, zero sessions or dispatch before explicit selection");

        var values = UniversalSqueakerMod.Settings.Values;
        values.AgeEnabled = true;
        values.MoodEnabled = true;
        values.ResponseParameters["mood.low"] = .2;
        values.ResponseParameters["mood.high"] = .8;
        values.ResponseParameters["mood.high.pitch"] = .5;
        values.ResponseParameters["mood.high.volume"] = .5;
        pawn.ageTracker!.CurLifeStage.voxPitch = 1.5f;
        UniversalSqueakerMod.Settings.activity = 2;
        UsOneShotRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(100, 40, 120, 120, 0, 1, 1));
        comp.CompTick();
        var session = RuntimeSession(pawn);
        Console.WriteLine("S4 caller tick=" + Find.TickManager.TicksGame + " action=" + session.LastOutcome.Action
            + " reason=" + session.LastOutcome.Reason + " receipt=" + session.LastOutcome.Receipt?.Result
            + " count=" + SoundStarter.Played.Count + " next=" + session.State.NextStateTick);
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && SoundStarter.Played.Count == before + 1
            && session.State.NextStateTick == 1090 && session.State.SharedDeadline == 1040
            && Math.Abs(SoundStarter.Played[before].source.pitch - 1.5f) < .0001
            && Math.Abs(SoundStarter.Played[before].source.volume - .8f) < .0001,
            "S4 actual CompTick -> runtime -> selected ordinary author entry -> one response composition -> native-call boundary, A once and L/G separate");
        comp.NotifyExternalByKey("Wounded", SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Reason == "TickOccupied" && SoundStarter.Played.Count == before + 1,
            "S4 actual state then event same tick cannot create a second attempt owner");
        Find.TickManager.TicksGame = 1040;
        comp.NotifyExternalByKey("Wounded", SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && SoundStarter.Played.Count == before + 2,
            "S4 real event notification consumes explicit event candidate and existing owner after hard deadline");
        UniversalSqueakerMod.Settings.activity = .5;
        comp.CompTick();
        Check(ReferenceEquals(session, RuntimeSession(pawn)) && session.State.NextStateTick == 1090,
            "S4 settings activity change cannot recreate owner or redraw already scheduled wait");

        var id = UsAudioBindings.KeyForName("Ratkin1")!;
        UniversalSqueakerMod.Settings.activity = 1;
        SqueakRuntimeResolver.Current.Registry = new SqueakPoolRegistry(new[]
        {
            new VoicePackEntry("fixture/external", AudioDomains.RaceOnly(pawn.def.defName), 1,
                new Dictionary<string, IReadOnlyList<ActionSoundSet>> { ["vendor/Unregistered"] = new[] { new ActionSoundSet(new[] { id }, null, 1) } })
        }, BuiltInFallbackTable.Empty);
        Find.TickManager.TicksGame = 1200;
        comp.NotifyExternalByKey("vendor/Unregistered", SqueakTriggerOrigin.External, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && session.LastOutcome.Action == "vendor/Unregistered",
            "S4 valid unregistered public string uses existing audio registry and the same new decision owner without registration prerequisite");
        comp.NotifyExternalByKey("Wounded", SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Reason == "TickOccupied", "S4 public string then built-in event same tick shares attempt reservation");
        comp.ExternalAllowed = false;
        Find.TickManager.TicksGame = 1300;
        comp.NotifyExternalByKey("vendor/Unregistered", SqueakTriggerOrigin.External, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Receipt == null && SoundStarter.Played.Count == before + 3,
            "S4 unregistered public string retains external permission under unified owner");

        S5ActualRuntimeSustained(pawn, comp, session);

        UniversalSqueakerMod.Settings.activity = 1;
        UniversalSqueakerMod.Settings.Values = new BehaviorValues();
        UsOneShotRuntime.ResetSession();
        SqueakRuntimeResolver.Current.Registry = SqueakPoolRegistry.Empty;
    }

    private static void S5ActualRuntimeSustained(Pawn pawn, CompSqueaker comp, UsControlledOneShotSession session)
    {
        var sound = DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin1")!;
        sound.sustain = true;
        comp.FixtureSustained = true;
        Find.TickManager.TicksGame = 2000;
        comp.CompTick();
        var original = SoundStarter.Spawned[0];
        long deadline = session.State.SharedDeadline;
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && !original.Ended,
            "S5 actual caller shape reaches prepared native sustained start and confirms effective handle");
        Find.TickManager.TicksGame = 2140;
        comp.CompTick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.AlreadyActive && SoundStarter.Spawned.Count == 1
            && session.State.SharedDeadline == deadline && session.LastRandomDraws.Count == 0,
            "S5 actual same-key maintenance neither redraws nor starts nor refreshes hard protection");
        SqueakSoundAvailabilityCache.RejectProductionInfo = true;
        Find.TickManager.TicksGame = 2200;
        comp.NotifyExternalByKey("Wounded", SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Rejected && !original.Ended && SoundStarter.Spawned.Count == 1,
            "S5 actual higher-priority preparation failure leaves old handle untouched");
        SqueakSoundAvailabilityCache.RejectProductionInfo = false;
        Find.TickManager.TicksGame = 2260;
        comp.NotifyExternalByKey("Wounded", SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        var replacement = SoundStarter.Spawned[1];
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && original.EndCalls == 1 && !replacement.Ended,
            "S5 actual prepared higher-priority request ends old once and confirms distinct new handle");
        Find.TickManager.TicksGame = 2330;
        comp.NotifyExternalByKey("Call", SqueakTriggerOrigin.External, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Rejected && !replacement.Ended && SoundStarter.Spawned.Count == 2,
            "S5 actual lower-priority request cannot interrupt owned sustained slot");
        comp.Allowed = false;
        Find.TickManager.TicksGame = 2400;
        comp.CompTick();
        Check(replacement.EndCalls == 1, "S5 actual independent review ends current handle after permission is revoked");
        comp.Allowed = true;
        SoundStarter.SpawnFailure = "null";
        Find.TickManager.TicksGame = 2500;
        comp.CompTick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Indeterminate && session.State.SharedDeadline == 2540
            && session.State.ConfirmedTick == 2260,
            "S5 actual unconfirmed native call protects hard deadline without reporting successful sustained start");
        comp.NotifyExternalByKey("Wounded", SqueakTriggerOrigin.Wounded, SqueakInvocationSource.StateEvent);
        Check(session.LastOutcome.Reason == "TickOccupied" && SoundStarter.Spawned.Count == 2,
            "S5 actual uncertain start cannot chain a second event attempt in the same tick");
        SoundStarter.SpawnFailure = "ended";
        Find.TickManager.TicksGame = 2620;
        comp.CompTick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Indeterminate && SoundStarter.Spawned[2].EndCalls == 1,
            "S5 actual nonnull but constructor-ended native handle is not confirmed or ended twice");
        SoundStarter.SpawnFailure = "";
        sound.sustain = false;
        comp.FixtureSustained = false;
    }
}
