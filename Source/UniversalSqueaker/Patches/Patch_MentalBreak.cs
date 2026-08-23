using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

/// <summary>Core handler namespace varies across game assemblies; resolve it by its stable full type name.</summary>
[HarmonyPatch]
public static class Patch_MentalBreak
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // 0.2.4: hook 从 MentalStateHandler.TryStartMentalState(所有 mental state 的通用入口)收窄到
        // MentalBreakWorker.TryStart(游戏正式精神崩溃的唯⼀通道,MentalBreakDef 驱动)。
        // 修复:婴幼儿高心情的 Giggling / 低心情的 Crying 等非崩溃 mental state(Biotech BabyFits)
        // 不再被误报为精神崩溃。该语义对所有种族/所有非崩溃状态通用,不依赖年龄或 DLC 判断。
        MethodBase? target = AccessTools.Method("Verse.AI.MentalBreakWorker:TryStart")
            ?? AccessTools.Method("RimWorld.MentalBreakWorker:TryStart");
        if (target != null) yield return target;
        else SqueakLog.HookMentalBreakUnavailable();
    }

    private static void Postfix(Pawn pawn, bool __result)
    {
        if (!__result || pawn == null || !pawn.Spawned || pawn.MapHeld != Find.CurrentMap) return;
        pawn.GetComp<CompSqueaker>()?.Notify_MentalBreak();
    }
}
