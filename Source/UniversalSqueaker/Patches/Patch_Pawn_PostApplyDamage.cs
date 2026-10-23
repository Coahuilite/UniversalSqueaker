using HarmonyLib;
using System.Reflection;
using Verse;

namespace UniversalSqueaker;

[HarmonyPatch]
public static class Patch_Pawn_PostApplyDamage
{
    private static bool Prepare() => UsPawnEventObservation.Prepare("us.core/WoundedEvent");
    private static MethodBase TargetMethod() => UsPawnEventObservation.Wounded!;
    private static void Postfix(Pawn __instance, float totalDamageDealt)
    {
        __instance.GetComp<CompSqueaker>()?.Notify_Wounded(totalDamageDealt);
    }
}
