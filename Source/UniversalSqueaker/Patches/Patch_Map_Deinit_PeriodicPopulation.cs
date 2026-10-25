using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

/// <summary>Releases map-owned responses and population membership before native teardown.</summary>
[HarmonyPatch(typeof(MapDeiniter), nameof(MapDeiniter.Deinit))]
internal static class Patch_Map_Deinit_PeriodicPopulation
{
    private static void Prefix(Map map)
    {
        SqueakerRuntime.ReleaseMap(map);
        SqueakPeriodicPopulation.RemoveMap(map);
    }
}
