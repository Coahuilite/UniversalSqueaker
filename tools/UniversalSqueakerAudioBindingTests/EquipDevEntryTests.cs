#if US_DEV
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LudeonTK;
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
    private static void EquipDevEntry()
    {
        Console.WriteLine("MODE --equip-dev-entry: actual SelectCandidate -> production policy -> Harmony EquipmentAdded -> Notify_Equip -> same owner; menu/Verse/audio platform doubles");
        DefDatabase<UsActionDef>.AllDefsListForReading.Clear();
        DefDatabase<UsProviderDef>.AllDefsListForReading.Clear();
        DefDatabase<UsBehaviorSourceDef>.AllDefsListForReading.Clear();
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Clear();
        var race = new ThingDef { defName = "EquipMenuRace", race = new object() };
        DefDatabase<ThingDef>.AllDefsListForReading.Add(race);
        var pack = new UsVoicePackDef { defName = "US_Equip_Menu", modContentPack = new ModContentPack { PackageId = "fixture.equip" } };
        pack.declaration.Race = race.defName;
        pack.declaration.Entries.Add(new VoiceEntry { Key = "Equip", Action = "us.core/Equip", Sound = "Ratkin1" });
        DefDatabase<UsVoicePackDef>.AllDefsListForReading.Add(pack);
        DcProjectAudio();
        UsInstanceOutput.Initialize(new Harmony("us.s2.controlled.adapter.fixture"));
        int serial = 0;
        Pawn Actor()
        {
            var p = new Pawn { ThingID = "EquipMenuPawn" + ++serial, def = race, IsPlayerControlled = true,
                CurJob = new Job { def = JobDefOf.Equip, playerForced = true } };
            p.Comp = new CompSqueaker { Pawn = p };
            return p;
        }
        var method = typeof(ResponseDebugActions).GetMethod(nameof(ResponseDebugActions.SelectCandidate))!;
        var menu = method.GetCustomAttribute<DebugActionAttribute>()!;
        var select = (Action)Delegate.CreateDelegate(typeof(Action), method);
        Console.WriteLine("MENU actual callback=" + method.DeclaringType!.FullName + "." + method.Name + " label=" + menu.Name);
        Check(menu.Category == "Universal Squeaker" && menu.actionType == DebugActionType.Action
            && menu.allowedGameStates == AllowedGameStates.PlayingOnMap, "actual menu registration retains category/action/map eligibility");
        Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Pawn_EquipmentTracker), "Notify_EquipmentAdded")) != null,
            "actual EquipmentAdded method is patched, not replaced with a direct Notify_Equip call");
        Check(new UsDevelopmentPolicy(20, 40, 120, 120, 120, 1, 1).EquipQualification == UsEquipQualification.Unselected,
            "seven-argument policy still means unselected Equip qualification");

        var inactive = Actor();
        int starts = SoundStarter.Played.Count;
        new Pawn_EquipmentTracker { pawn = inactive }.AddEquipment(new ThingWithComps());
        Check(!SqueakerRuntime.DevelopmentActive && !SqueakerRuntime.IsControlled(inactive)
            && inactive.Comp.LastCandidateRequest == null && SoundStarter.Played.Count == starts,
            "unselected GLOBAL Dev gate creates zero owner/request/output on actual EquipmentAdded");
        Prefs.DevMode = false;
        bool refused = false;
        try { select(); } catch (InvalidOperationException e) { refused = e.Message.Contains("developer mode"); }
        finally { Prefs.DevMode = true; }
        Check(refused && !SqueakerRuntime.DevelopmentActive && !SqueakerRuntime.IsControlled(inactive),
            "actual menu callback cannot activate the candidate outside developer mode");

        select(); // The production menu owns the policy. No hand-constructed policy is selected here.
        Check(SqueakerRuntime.DevelopmentActive && SqueakerRuntime.PolicyStatus == "ExplicitDevelopmentCandidate",
            "actual SelectCandidate selects the process-only GLOBAL development policy");
        var actor = Actor();
        var tracker = new Pawn_EquipmentTracker { pawn = actor };
        var item = new ThingWithComps { ThingID = "EquipMenuItem" };
        Find.TickManager.TicksGame = 2000;
        tracker.AddEquipment(item);
        var owner = RuntimeSession(actor);
        Console.WriteLine("MENU Equip observation=" + owner.DiagnosticText);
        var request = actor.Comp.LastCandidateRequest;
        Check(request != null && request.Action == "us.core/Equip" && request.Subject == SqueakerNotifications.SubjectKey(actor)
            && request.SubjectRole == "EquipmentWearer" && request.Domain == race.defName
            && request.Audio.SourceDomain == AudioDomains.RaceOnly(race.defName)
            && request.Audio.EntryKey == "fixture.equip/US_Equip_Menu/Equip" && request.Audio.AudioId == "actual.provider/Ratkin1"
            && owner.LastOutcome.Receipt?.Result == PlaybackResult.Dispatched && SoundStarter.Played.Count == starts + 1,
            "actual menu policy dispatches the player's exact Core Equip entry/audio/domain through the production owner");
        Check(owner.DiagnosticText.Contains("Pawn_EquipmentTracker.Notify_EquipmentAdded:Returned,wearer=" + actor.ThingID + ",item=" + item.ThingID),
            "actual request retains returned phase and exact wearer/item evidence");
        Check(menu.Name.Contains("Dev") && menu.Name.Contains("Equip=PlayerCoreEquip (explicit Dev)"),
            "actual menu label names PlayerCoreEquip as an explicit Dev input, not a pending ordinary qualification");
        long? confirmed = owner.State.ConfirmedTick;
        long shared = owner.State.SharedDeadline, action = owner.State.ActionDeadline("us.core/Equip"), next = owner.State.NextStateTick;
        Check(confirmed == 2000 && shared == 2040 && action == 2120, "menu keeps the existing W20 H40 I120 protection inputs");
        select();
        Check(ReferenceEquals(owner, RuntimeSession(actor)) && owner.State.ConfirmedTick == confirmed
            && owner.State.SharedDeadline == shared && owner.State.ActionDeadline("us.core/Equip") == action && owner.State.NextStateTick == next,
            "repeat selection invokes the same menu fixture without replacing owner or clearing protection/opportunity");
        Find.TickManager.TicksGame = 2001;
        starts = SoundStarter.Played.Count;
        tracker.AddEquipment(new ThingWithComps());
        Check(SoundStarter.Played.Count == starts && owner.State.ConfirmedTick == confirmed
            && owner.State.SharedDeadline == shared && owner.State.ActionDeadline("us.core/Equip") == action,
            "repeat menu selection cannot reopen the still-protected Equip event");
        Find.TickManager.TicksGame = 2120;
        tracker.AddEquipment(new ThingWithComps());
        Check(ReferenceEquals(owner, RuntimeSession(actor)) && SoundStarter.Played.Count == starts + 1
            && owner.State.ConfirmedTick == 2120 && actor.Comp.LastCandidateRequest?.Action == "us.core/Equip",
            "same production owner accepts a fresh qualified Equip when the original hard deadline expires");

        void Reject(string label, Action<Pawn> change)
        {
            var denied = Actor(); change(denied);
            int before = SoundStarter.Played.Count;
            new Pawn_EquipmentTracker { pawn = denied }.AddEquipment(new ThingWithComps());
            var session = RuntimeSession(denied);
            Check(denied.Comp.LastCandidateRequest == null && SoundStarter.Played.Count == before
                && session.LastOutcome.Reason == "ExplicitPlayerCoreEquipQualification" && session.State.ConfirmedTick == null,
                "menu qualification does not widen " + label + " (fresh owner, no cooldown masking)");
        }
        Reject("AI automatic Equip", p => p.CurJob!.playerForced = false);
        Reject("non-player forced Core Equip", p => p.IsPlayerControlled = false);
        Reject("forced non-Core Equip", p => p.CurJob!.def = new JobDef { defName = "Equip" });
        Reject("missing current Job", p => p.CurJob = null);
        foreach (bool throwing in new[] { false, true })
        {
            var denied = Actor(); var broken = new Pawn_EquipmentTracker { pawn = denied, ThrowNotification = throwing };
            int before = SoundStarter.Played.Count; bool observedThrow = false;
            try {
                if (throwing) broken.AddEquipment(new ThingWithComps());
                else broken.Notify_EquipmentAdded(new ThingWithComps()); // No actual membership.
            } catch (InvalidOperationException e) { observedThrow = e.Message == "NativeEquipmentFailure"; }
            Check(observedThrow == throwing && !SqueakerRuntime.IsControlled(denied)
                && denied.Comp.LastCandidateRequest == null && SoundStarter.Played.Count == before,
                "actual " + (throwing ? "throwing EquipmentAdded" : "notification without equipment relation") + " publishes no normal-return event/owner");
        }
        Console.WriteLine("MENU Equip final=" + owner.DiagnosticText);
    }
}
#endif
