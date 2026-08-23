using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalSqueaker;

/// <summary>
/// 0.3.1 波 3c：BabyFits 窄 hook（决策 §2.1.3 源码核验：MentalFitGenerator.TickInterval 最终成功调用
/// MentalStateHandler.TryStartMentalState(..., transitionSilently: true)）。
/// 语义：TryStartMentalState 成功 postfix，仅当 __result && stateDef 属于经 MentalFitDef 反向映射表验证的
/// BabyCry/Giggling 状态（MentalStateDef defName = Crying/Giggling）时通知新动作；
/// No-DLC（Biotech 关）→ DefDatabase&lt;MentalFitDef&gt; 为空 → 反向 map 空 → 永不通知；
/// 不扩大/替代 MentalBreakWorker.TryStart hook（Patch_MentalBreak 保持真实崩溃限定不动）。
/// </summary>
[HarmonyPatch]
public static class Patch_MentalFit
{
    private const string CryingStateDefName = "Crying";
    private const string GigglingStateDefName = "Giggling";

    private static readonly object reverseMapSync = new();
    private static Dictionary<MentalStateDef, MentalFitDef>? reverseMap;
    private static bool reverseMapBuilt;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // MentalStateHandler 在 1.6 位于 Verse.AI 命名空间（源文件 Verse/AI/MentalStateHandler.cs）。
        MethodBase? target = AccessTools.Method("Verse.AI.MentalStateHandler:TryStartMentalState");
        if (target != null) yield return target;
        else SqueakLog.HookMentalFitUnavailable();
    }

    private static void Postfix(MentalStateHandler __instance, MentalStateDef stateDef, bool __result)
    {
        if (!__result || stateDef == null) return;
        string defName = stateDef.defName;
        if (defName != CryingStateDefName && defName != GigglingStateDefName) return;
        if (!IsMentalFitState(stateDef)) return;
        // TryStartMentalState 成功时 curStateInt 已置为新状态；MentalState.pawn 是公开字段。
        Pawn? pawn = __instance.CurState?.pawn;
        if (pawn == null || !pawn.Spawned || pawn.MapHeld != Find.CurrentMap) return;
        pawn.GetComp<CompSqueaker>()?.Notify_MentalFit(defName == CryingStateDefName ? SqueakAction.Crying : SqueakAction.Giggling);
    }

    /// <summary>
    /// MentalFitDef 反向 map：DefDatabase 中每个 MentalFitDef.mentalState → fit（懒构建、线程安全、会话内缓存）。
    /// 只有被 MentalFitDef 引用的状态才算 BabyFits 通道；第三方以同名 defName 自定义状态而无 MentalFitDef
    /// 引用时不通知。Biotech 关时 DefDatabase 为空 → false（No-DLC 安全，不需要显式 Prepare=false）。
    /// </summary>
    private static bool IsMentalFitState(MentalStateDef stateDef)
    {
        if (!ModsConfig.BiotechActive) return false;
        lock (reverseMapSync)
        {
            if (!reverseMapBuilt)
            {
                Dictionary<MentalStateDef, MentalFitDef> map = new();
                foreach (MentalFitDef fit in DefDatabase<MentalFitDef>.AllDefs)
                {
                    if (fit?.mentalState != null) map[fit.mentalState] = fit;
                }
                reverseMap = map;
                reverseMapBuilt = true;
            }
        }
        return reverseMap!.ContainsKey(stateDef);
    }
}
