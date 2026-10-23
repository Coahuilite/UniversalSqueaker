using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

/// <summary>A positive successful MentalBreakWorker start, never an arbitrary mental state.</summary>
[HarmonyPatch]
public static class Patch_MentalBreak
{
    private static bool Prepare() => UsPawnEventObservation.Prepare("us.core/MentalBreakEvent");
    private static IEnumerable<MethodBase> TargetMethods() => UsPawnEventObservation.MentalBreakTargets;
    private static void Postfix(Pawn pawn, bool __result, MethodBase __originalMethod)
    {
        if (__result && pawn != null && pawn.Spawned && pawn.MapHeld == Find.CurrentMap)
            pawn.GetComp<CompSqueaker>()?.Notify_MentalBreak(__originalMethod.DeclaringType?.FullName + "." + __originalMethod.Name);
    }
}
