using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
public static class Patch_Pawn_PostApplyDamage
{
    private static void Postfix(Pawn __instance)
    {
        __instance.GetComp<CompSqueaker>()?.Notify_Wounded();
    }
}
