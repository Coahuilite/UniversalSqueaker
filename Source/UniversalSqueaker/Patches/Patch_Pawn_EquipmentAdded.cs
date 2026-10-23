using HarmonyLib;
using System.Reflection;
using Verse;

namespace UniversalSqueaker;

[HarmonyPatch]
public static class Patch_Pawn_EquipmentAdded
{
    private static bool Prepare() => UsPawnEventObservation.Prepare("us.core/EquipEvent");
    private static MethodBase TargetMethod() => UsPawnEventObservation.Equip!;
    private static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
    {
        Pawn pawn = __instance.pawn;
        if (pawn.Spawned && pawn.MapHeld == Find.CurrentMap && __instance.AllEquipmentListForReading.Contains(eq))
            pawn.GetComp<CompSqueaker>()?.Notify_Equip(eq);
    }
}
