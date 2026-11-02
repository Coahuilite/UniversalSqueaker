using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>Approved explicit 1.6 execution mechanisms. Ordinary policy or an explicit Dev policy
/// activates observation; execution is not a hit or an arbitrary Verb-name match.</summary>
internal static class UsAttackObservation
{
    private static readonly List<FactDiagnostic> missing = new();
    internal static readonly MethodInfo? Melee = ResolveTarget("RimWorld.Verb_MeleeAttack", "TryCastShot");
    internal static readonly MethodInfo? MeleeExecuted = ResolveTarget("Verse.Pawn_DrawTracker", "Notify_MeleeAttackOn", typeof(Thing));
    internal static readonly MethodInfo[] Ranged = new[] {
        ResolveTarget("Verse.Verb_LaunchProjectile", "TryCastShot"),
        ResolveTarget("Verse.Verb_ShootBeam", "TryCastShot"),
        ResolveTarget("Verse.Verb_Spray", "TryCastShot"),
        ResolveTarget("Verse.Verb_SpewFire", "TryCastShot"),
        ResolveTarget("RimWorld.Verb_Ignite", "TryCastShot"),
        ResolveTarget("RimWorld.Verb_PowerBeam", "TryCastShot"),
        ResolveTarget("RimWorld.Verb_Bombardment", "TryCastShot") }.Where(x => x != null).Cast<MethodInfo>().ToArray();
    internal static bool MeleeReady => Melee != null && MeleeExecuted != null;
    internal static bool Ready => MeleeReady || Ranged.Length != 0;
    internal static IEnumerable<FactDiagnostic> Diagnostics => missing;
    private static bool reported;
    internal static bool Prepare(bool ready)
    {
        if (!reported)
        {
            reported = true;
            foreach (var item in missing) SqueakLog.CapabilityUnavailable("AttackExecution", item.Key, item.Reason);
        }
        return ready;
    }
    private static MethodInfo? ResolveTarget(string type, string member, params Type[] arguments)
    {
        Type? resolved = AccessTools.TypeByName(type);
        MethodInfo? method = resolved == null ? null : AccessTools.DeclaredMethod(resolved, member, arguments);
        if (method == null) missing.Add(new("us.core/AttackEvent", "AttackExecution", "MissingTarget:" + type + "." + member));
        return method;
    }
    internal sealed class Frame
    {
        internal readonly Pawn Pawn;
        internal readonly Frame? Previous;
        internal bool Executed;
        internal Frame(Pawn pawn, Frame? previous) { Pawn = pawn; Previous = previous; }
    }
    [ThreadStatic] private static Frame? current;
    internal static Frame? Begin(Verb verb)
    {
        if (!SqueakerRuntime.AttackObservationActive || verb.caster is not Pawn pawn) return null;
        return current = new Frame(pawn, current);
    }
    internal static void Mark(Pawn_DrawTracker drawer)
    {
        if (current != null && ReferenceEquals(current.Pawn.Drawer, drawer)) current.Executed = true;
    }
    internal static void Complete(Frame? frame, bool normalReturn)
    {
        if (frame == null) return;
        if (ReferenceEquals(current, frame)) current = frame.Previous;
        if (normalReturn && frame.Executed) Publish(frame.Pawn, "RimWorld.Verb_MeleeAttack:ExecutedMarker");
    }
    internal static void Publish(Pawn pawn, string mechanism)
    {
        if (SqueakerRuntime.AttackObservationActive && pawn.Spawned && pawn.MapHeld == Find.CurrentMap)
            pawn.GetComp<CompSqueaker>()?.Notify_Attack(mechanism);
    }
}
