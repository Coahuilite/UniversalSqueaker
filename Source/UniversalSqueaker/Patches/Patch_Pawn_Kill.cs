using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_Pawn_Kill
{
    // Prefix: 在 Kill 主体(DeSpawnOrDeselect / SetDead)前触发。
    // 此时 pawn 仍 spawned,position 有效,SoundInfo.InMap 的 distRange 衰减正常。
    // 被击杀和流血而亡都统一走 Pawn.Kill,一个 hook 覆盖两种死亡。
    private static void Prefix(Pawn __instance)
    {
        __instance.GetComp<CompSqueaker>()?.Notify_Death();
    }
}
