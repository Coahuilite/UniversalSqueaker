using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

[HarmonyPatch]
public static class Patch_Selector_Select
{
    private static MethodBase? TargetMethod()
    {
        // Selector.Select 实际签名: Select(object obj, bool playSound, bool forceDesignatorDeselect)
        // 第一参数是 object(不是 Thing),按重载链 fallback。
        return AccessTools.Method(typeof(Selector), "Select", new[] { typeof(object), typeof(bool), typeof(bool) })
            ?? AccessTools.Method(typeof(Selector), "Select", new[] { typeof(object), typeof(bool) })
            ?? AccessTools.Method(typeof(Selector), "Select", new[] { typeof(object) });
    }

    // 用位置注入 __0/__1 取值,避免与原方法参数名耦合(原方法参数名为 obj,不是 t)。
    // 0.3.2 定案:只认 playSound=true 的交互式点选。RimWorld 1.6 全部 playSound=false 调用点均为
    // 程序性后续选择(复活/区域创建/培养舱/基因提取/传送等自动选中),不属于玩家点击反馈。
    private static void Postfix(object __0, bool __1)
    {
        if (!__1 || __0 is not Pawn pawn)
        {
            return;
        }

        pawn.GetComp<CompSqueaker>()?.Notify_Select();
    }
}
