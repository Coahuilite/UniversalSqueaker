using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;
using Verse;
using Verse.AI;
using Verse.Sound;

internal static partial class Program
{
    public sealed class RuntimeContractProvider : IUsDefinitionProvider
    {
        public void Collect(ProviderBatch batch) => batch.AddFactSource(new RuntimeContractFact());
    }
    private sealed class RuntimeContractFact : IUsFactSource
    {
        internal static bool Ready;
        public string Key => "fixture/Allowed";
        public Truth Capture(string subject) => Ready ? Truth.True : Truth.False;
    }
    private static PawnResponseAdapter RuntimeSession(Pawn pawn)
    {
        var field = typeof(SqueakerRuntime).GetField("sessions", BindingFlags.NonPublic | BindingFlags.Static)!;
        return ((Dictionary<int, PawnResponseAdapter>)field.GetValue(null)!)[pawn.thingIDNumber];
    }


    private const string MaintainedCall = "fixture.runtime/MaintainedCall", MaintainedWound = "fixture.runtime/MaintainedWound";
    private static void S4ActualRuntimeCallers()
    {
        Console.WriteLine("MODE normal/r2 caller: actual declared contracts, production published settings, one owner and native output double");
        var settings = SqueakerMod.Settings;
        settings.actionTuning.Clear(); settings.voicePackSelections.Clear(); settings.behaviorSourcesXml = "";
        settings.activity = 2; settings.globalVolumeFactor = .8f; settings.voicePackMode = SqueakVoicePackMode.Fallback;
        var pawn = new Pawn { IsPlayerControlled = true, CurJob = new Job { def = new JobDef { defName = "Wait" } },
            jobs = new Pawn_JobTracker { curDriver = new JobDriver() }, pather = new Pawn_PathFollower(),
            mindState = new Pawn_MindState { IsIdle = true }, needs = new Pawn_NeedsTracker { mood = new FixtureMood { CurLevelPercentage = .9f } } };
        var comp = new CompSqueaker { Pawn = pawn }; pawn.Comp = comp;
        DefDatabase<ThingDef>.AllDefsListForReading.Add(pawn.def);
        var declared = new NotificationContract("fixture.runtime/Finished", 120);
        SqueakerNotifications.Declare(declared);
        var pack = new UsVoicePackDef { defName = "RuntimePack", modContentPack = new ModContentPack { PackageId = "fixture.author" } };
        pack.declaration.Race = pawn.def.defName;
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
            pack.declaration.Entries.Add(new VoiceEntry { Key = key, Action = key, Sound = "Ratkin1", Volume = 2, Pitch = 2 });
        pack.declaration.Entries.Add(new VoiceEntry { Key = "notification", Action = declared.ActionId, Sound = "Ratkin1" });
        DeclareMaintainedChild(pack, MaintainedCall, CoreActions.Call, 100);
        DeclareMaintainedChild(pack, MaintainedWound, CoreActions.Wounded, 200);
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        DefDatabase<UsProviderDef>.AllDefsListForReading.Add(new UsProviderDef { providerClass = typeof(RuntimeContractProvider) });
        DefDatabase<UsActionDef>.AllDefsListForReading.Add(new UsActionDef { declaration = new ActionDeclaration {
            Key = "vendor/Working", BaseAction = CoreActions.Work, Condition = new Condition("ref", "fixture/Allowed") } });
        pack.declaration.Entries.Add(new VoiceEntry { Key = "registered", Action = "vendor/Working", Sound = "Ratkin1" });
        var behaviorDomain = new BehaviorDomain("Pawn", "", pawn.def.defName);
        var behaviorDef = new UsBehaviorSourceDef { defName = "Source", modContentPack = pack.modContentPack };
        behaviorDef.declaration.Sources.Add(new BehaviorSource { Domain = behaviorDomain });
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Add(behaviorDef);
        SqueakerRuntime.Initialize();
        Check(SqueakerRuntime.Definitions.HasAction(declared.ActionId), "ordinary public notification declared once before subject owners exist");
        var directoryPack = VoicePackDirectory.Directory.Single(x => x.defName == pack.defName);
        Check(directoryPack.TryGetPackKey(out string selectionKey) && selectionKey == "fixture.author:RuntimePack"
            && directoryPack.scope == SqueakVoicePackScope.Race && directoryPack.raceDefName == pawn.def.defName,
            "ordinary audio metadata retains author package:Def selection identity and exact domain");
        var domain = AudioDomains.RaceOnly(pawn.def.defName);
        var selections = new Dictionary<AudioDomain, HashSet<string>> { [domain] = new() { selectionKey } };
        var directory = new SqueakXenotypeCatalogSnapshot(); directory.RacePacks.Add(directoryPack);
        ProjectSelection(directory, selections);
        int before = SoundStarter.Played.Count;
        comp.CompTick(); comp.Notify_Wounded();
        Check(!SqueakerRuntime.IsControlled(pawn) && SoundStarter.Played.Count == before && comp.MembershipUpdates == 2,
            "actual CompTick/event callers retain unselected process gate, zero owner/dispatch");
        foreach (string key in BuiltInPawnContracts.CandidateOrder)
            settings.actionTuning.Add(new ActionTuningRecord { actionKey = key,
                responses = new() { new() { key = "us.core/Age", hasEnabled = true, enabled = true }, new() { key = "us.core/Mood", hasEnabled = true, enabled = true } },
                responseParameters = new() { new() { key = "mood.low", value = .2 }, new() { key = "mood.high", value = .8 },
                    new() { key = "mood.high.pitch", value = .5 }, new() { key = "mood.high.volume", value = .5 } } });
        settings.PublishBehaviorConfiguration(); pawn.ageTracker!.CurLifeStage.voxPitch = 1.5f;
        SqueakerRuntime.SelectDevelopmentPolicy(new UsDevelopmentPolicy(100, 40, 120, 120, 0, 1, 1));
        comp.CompTick(); var session = RuntimeSession(pawn);
        Console.WriteLine("R2 CALLER " + session.DiagnosticText + " count=" + SoundStarter.Played.Count + " before=" + before
            + " native=" + SoundStarter.Played.Last().source.pitch + "/" + SoundStarter.Played.Last().source.volume);
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && SoundStarter.Played.Count == before + 1
            && session.State.NextStateTick == 1090 && session.State.SharedDeadline == 1040
            && Math.Abs(SoundStarter.Played[before].source.pitch - 1.5f) < .0001
            && Math.Abs(SoundStarter.Played[before].source.volume - .8f * .8f) < .0001,
            "actual caller consumes published responses/A once; native mapping/context 0.8 x local L1 x current G0.8");
        comp.Notify_Wounded(); Check(session.LastOutcome.Reason == "TickOccupied", "actual state then event reserves original owner same tick");
        Find.TickManager.TicksGame = 1040; comp.Notify_Wounded();
        Check(SoundStarter.Played.Count == before + 2, "real Wounded event uses the existing owner after shared boundary");
        long scheduled = session.State.NextStateTick; settings.SetActivity(.5); comp.CompTick();
        Check(ReferenceEquals(session, RuntimeSession(pawn)) && session.State.NextStateTick == scheduled,
            "accepted activity edit does not recreate owner or redraw an existing wait");
        settings.SetActivity(1); Find.TickManager.TicksGame = 1200;
        int starts = SoundStarter.Played.Count; long confirmed = session.State.ConfirmedTick!.Value;
        Check(!SqueakerNotifications.TryProcess(pawn, "vendor/Unregistered") && SoundStarter.Played.Count == starts && session.State.ConfirmedTick == confirmed,
            "unknown public string cannot synthesize plan/audio/attempt or replace the owner");
        Check(SqueakerNotifications.TryProcess(pawn, declared) && session.LastOutcome.Action == declared.ActionId
            && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && SoundStarter.Played.Count == starts + 1,
            "explicit C# declaration then public notify uses the same original owner and bound resource");
        comp.Notify_Wounded(); Check(session.LastOutcome.Reason == "TickOccupied", "declared notification then native event shares same tick reservation");
        Check(settings.TryReplaceBehaviorOverride("Pawn", "", pawn.def.defName, "", declared.ActionId,
            new BehaviorValues { Enabled = false }, () => true, out _), "explicit current player permission for the declared action");
        Find.TickManager.TicksGame = 1340; SqueakerNotifications.TryProcess(pawn, declared);
        Check(SoundStarter.Played.Count == starts + 1 && session.LastOutcome.Receipt == null,
            "declaration cannot bypass current player permission");
        RuntimeContractFact.Ready = false; Find.TickManager.TicksGame = 1400;
        SqueakerNotifications.TryProcess(pawn, "vendor/Working");
        Check(session.LastOutcome.Receipt == null, "registered author call cannot bypass its false added condition");
        RuntimeContractFact.Ready = true; Find.TickManager.TicksGame = 1460;
        SqueakerNotifications.TryProcess(pawn, "vendor/Working");
        Check(session.LastOutcome.Receipt == null, "registered author call retains inherited false Work even after its extra fact becomes true");
        S5ActualRuntimeSustained(pawn, comp, session);
        starts = SoundStarter.Played.Count;
        ProjectSelection(directory, new Dictionary<AudioDomain, HashSet<string>>());
        Find.TickManager.TicksGame = 3000; comp.Notify_Wounded();
        Check(session.LastOutcome.Receipt == null && SoundStarter.Played.Count == starts
            && SqueakerRuntime.BehaviorSources.Find(behaviorDomain, "fixture.author/Source") != null,
            "exact current pack filter removes audio without unloading independent behavior definitions");
        ProjectSelection(directory, selections); Find.TickManager.TicksGame = 3120; comp.Notify_Wounded();
        Check(SoundStarter.Played.Count == starts + 1, "reenabled pack resumes on the same owner and canonical audio catalog");
        settings.voicePackMode = SqueakVoicePackMode.Disabled; settings.PublishBehaviorConfiguration();
        Find.TickManager.TicksGame = 3300; comp.Notify_Wounded();
        Check(SoundStarter.Played.Count == starts + 1, "disabled system cannot be reopened by author/domain enabled settings");
        settings.voicePackMode = SqueakVoicePackMode.Fallback; settings.PublishBehaviorConfiguration();
        var material = DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin1")!; material.sustain = true; MaintainSubject(pawn, true);
        Find.TickManager.TicksGame = 3500; comp.Notify_Wounded(); var mapHandle = SoundStarter.Spawned.Last();
        var map = pawn.MapHeld; pawn.MapHeld = new Map(); Find.TickManager.TicksGame = 3501; comp.CompTick();
        Check(mapHandle.EndCalls == 1, "actual current-map loss releases owned sustained once");
        pawn.MapHeld = map; Find.TickManager.TicksGame = 3640; comp.Notify_Wounded(); var destroyed = SoundStarter.Spawned.Last();
        pawn.Destroyed = true; comp.CompTick();
        Check(destroyed.EndCalls == 1 && !SqueakerRuntime.IsControlled(pawn), "destroyed caller releases handle and sole session");
        pawn.Destroyed = false; S5ActualEndFaults(pawn, comp);
        MaintainSubject(pawn, false); material.sustain = false; settings.actionTuning.Clear(); settings.PublishBehaviorConfiguration();
        SqueakerRuntime.ResetSession();
    }
    private static void S5ActualRuntimeSustained(Pawn pawn, CompSqueaker comp, PawnResponseAdapter session)
    {
        var sound = DefDatabase<SoundDef>.GetNamedSilentFail("Ratkin1")!; sound.sustain = true; MaintainSubject(pawn, true);
        int baseline = SoundStarter.Spawned.Count;
        Find.TickManager.TicksGame = 2000; comp.CompTick(); var original = SoundStarter.Spawned.Last();
        long deadline = session.State.SharedDeadline;
        Check(session.LastOutcome.Action == MaintainedCall && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && !original.Ended,
            "explicit declared sustained child starts through actual caller/native boundary");
        Find.TickManager.TicksGame = 2140; comp.CompTick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.AlreadyActive && SoundStarter.Spawned.Count == baseline + 1
            && session.State.SharedDeadline == deadline && session.LastRandomDraws.Count == 0,
            "same-key maintenance neither redraws/starts nor refreshes hard deadline");
        SqueakSoundAvailabilityCache.RejectProductionInfo = true; Find.TickManager.TicksGame = 2200; comp.Notify_Wounded();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Rejected && !original.Ended && SoundStarter.Spawned.Count == baseline + 1,
            "higher-priority preparation failure leaves old owned handle untouched");
        SqueakSoundAvailabilityCache.RejectProductionInfo = false; Find.TickManager.TicksGame = 2260; comp.Notify_Wounded();
        var replacement = SoundStarter.Spawned.Last();
        Check(session.LastOutcome.Action == MaintainedWound && session.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && original.EndCalls == 1 && !replacement.Ended,
            "prepared higher-priority contract ends old once and confirms a distinct handle");
        Find.TickManager.TicksGame = 2330; SqueakerRuntime.TryObservedEvent(pawn, CoreActions.Call, SqueakTriggerOrigin.External, SqueakInvocationSource.StateEvent, "FixtureCurrentCall");
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Rejected && !replacement.Ended && SoundStarter.Spawned.Count == baseline + 2,
            "lower-priority contract cannot interrupt active sustained output");
        Find.TickManager.TicksGame = 2400; session.Maintain(force: true);
        Check(replacement.EndCalls == 1 && session.State.ConfirmedTick == 2260 && SoundStarter.Spawned.Count == baseline + 2,
            "maintenance-only current fact review ends transient Wounded without inventing another response opportunity");
        SoundStarter.SpawnFailure = "null"; Find.TickManager.TicksGame = 2500; comp.CompTick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Indeterminate && session.State.SharedDeadline == 2540 && session.State.ConfirmedTick == 2260,
            "unconfirmed entered native call protects hard boundary without confirming a handle");
        comp.Notify_Wounded(); Check(session.LastOutcome.Reason == "TickOccupied", "uncertain start cannot chain second attempt same tick");
        SoundStarter.SpawnFailure = "ended"; Find.TickManager.TicksGame = 2620; comp.CompTick();
        Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Indeterminate && SoundStarter.Spawned.Last().EndCalls == 1,
            "constructor-ended handle is unconfirmed and never ended twice");
        SoundStarter.SpawnFailure = ""; MaintainSubject(pawn, false); sound.sustain = false;
        UsInstanceOutput.Initialize(new Harmony("fixture.required-output-owner-missing"));
        int starts = SoundStarter.Played.Count; long protectedUntil = session.State.SharedDeadline;
        Find.TickManager.TicksGame = 2800; comp.Notify_Wounded();
        Check(!UsInstanceOutput.Available && session.LastOutcome.Receipt?.Result == PlaybackResult.Rejected
            && SoundStarter.Played.Count == starts && session.State.SharedDeadline == protectedUntil,
            "missing required output patch rejects before side effects or new protection");
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
    }
    private static void S5ActualEndFaults(Pawn pawn, CompSqueaker comp)
    {
        foreach (string phase in new[] { "before", "after" })
        {
            SqueakerRuntime.ResetSession(); Find.TickManager.TicksGame = 4000; comp.CompTick();
            var session = RuntimeSession(pawn); var old = SoundStarter.Spawned.Last(); old.EndFailure = phase;
            int starts = SoundStarter.Spawned.Count; long shared = session.State.SharedDeadline;
            Find.TickManager.TicksGame = 4140; comp.Notify_Wounded();
            Check(session.LastOutcome.Receipt?.Result == PlaybackResult.Rejected && session.LastOutcome.Reason.Contains("NewStartNotEntered:")
                && session.DiagnosticText.Contains(":EndFault:") && SoundStarter.Spawned.Count == starts && old.EndCalls == 1
                && session.State.SharedDeadline == shared && session.State.ActionDeadline(MaintainedWound) == long.MinValue && session.State.ConfirmedTick == 4000,
                "old End fault " + phase + " reports uncertainty, zero new start and zero new hard protection");
            SqueakerRuntime.ResetSession(); Check(old.EndCalls == 1, "failed old End is not repeated during lifecycle cleanup");
        }
        Find.TickManager.TicksGame = 5000; comp.CompTick(); var failing = SoundStarter.Spawned.Last(); failing.EndFailure = "before";
        var other = new Pawn { ThingID = "Pawn-EndCleanupControl", IsPlayerControlled = true, needs = pawn.needs,
            CurJob = new Job { def = new JobDef { defName = "Wait" } }, jobs = new Pawn_JobTracker { curDriver = new JobDriver() },
            pather = new Pawn_PathFollower(), mindState = new Pawn_MindState { IsIdle = true } };
        var otherComp = new CompSqueaker { Pawn = other }; other.Comp = otherComp; MaintainSubject(other, true); otherComp.CompTick();
        var healthy = SoundStarter.Spawned.Last(); SqueakerRuntime.ResetSession();
        Check(!ReferenceEquals(failing, healthy) && failing.EndCalls == 1 && healthy.EndCalls == 1 && !SqueakerRuntime.IsControlled(pawn)
            && !SqueakerRuntime.IsControlled(other) && Log.Lines.Exists(x => x.Contains("owned sustained end uncertain")),
            "one lifecycle End fault is reported without preventing another subject's owned release");
        MaintainSubject(other, false);
    }
}
