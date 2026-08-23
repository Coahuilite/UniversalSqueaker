using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

/// <summary>Explicitly releases per-map membership when RimWorld tears a map down.</summary>
[HarmonyPatch(typeof(MapDeiniter), nameof(MapDeiniter.Deinit))]
internal static class Patch_Map_Deinit_PeriodicPopulation
{
    private static void Prefix(Map map) => SqueakPeriodicPopulation.RemoveMap(map);
}
