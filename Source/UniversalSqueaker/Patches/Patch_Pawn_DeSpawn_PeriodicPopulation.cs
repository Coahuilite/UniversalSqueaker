using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

/// <summary>Keeps audible membership current for despawns and map transitions before the next Comp tick.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
internal static class Patch_Pawn_DeSpawn_PeriodicPopulation
{
    private static void Prefix(Pawn __instance)
    {
        __instance.TryGetComp<CompSqueaker>()?.NotifyPeriodicDespawn(__instance.MapHeld);
    }
}
