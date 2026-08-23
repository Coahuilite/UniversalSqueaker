using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded))]
public static class Patch_Pawn_EquipmentAdded
{
    private static void Postfix(Pawn_EquipmentTracker __instance)
    {
        Pawn pawn = __instance.pawn;
        if (pawn.Spawned && pawn.MapHeld == Find.CurrentMap) pawn.GetComp<CompSqueaker>()?.Notify_Equip();
    }
}
