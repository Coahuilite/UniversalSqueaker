using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

// Patch only explicit mechanism roots. Shoot / ShootOneUse / AbilityShoot wrappers are
// covered when they actually call the observed launch implementation, never patched twice.
[HarmonyPatch]
public static class Patch_Verb_Attack
{
    private static bool Prepare() => UsAttackObservation.Prepare(UsAttackObservation.Ranged.Length != 0);
    private static IEnumerable<MethodBase> TargetMethods() => UsAttackObservation.Ranged;
    private static void Postfix(Verb __instance, bool __result, MethodBase __originalMethod)
    {
        if (__result && __instance.caster is Pawn pawn)
            UsAttackObservation.Publish(pawn, __originalMethod.DeclaringType?.FullName + "." + __originalMethod.Name + ":True");
    }
}

[HarmonyPatch]
public static class Patch_Melee_Attack
{
    private static bool Prepare() => UsAttackObservation.Prepare(UsAttackObservation.MeleeReady);
    private static MethodBase TargetMethod() => UsAttackObservation.Melee!;
    private static void Prefix(Verb __instance, out UsAttackObservation.Frame? __state) => __state = UsAttackObservation.Begin(__instance);
    private static void Postfix(UsAttackObservation.Frame? __state) => UsAttackObservation.Complete(__state, true);
    private static Exception? Finalizer(Exception? __exception, UsAttackObservation.Frame? __state)
    {
        if (__exception != null) UsAttackObservation.Complete(__state, false);
        return __exception;
    }
}

[HarmonyPatch]
public static class Patch_Melee_Attack_Executed
{
    private static bool Prepare() => UsAttackObservation.Prepare(UsAttackObservation.MeleeReady);
    private static MethodBase TargetMethod() => UsAttackObservation.MeleeExecuted!;
    private static void Prefix(Pawn_DrawTracker __instance) => UsAttackObservation.Mark(__instance);
}
