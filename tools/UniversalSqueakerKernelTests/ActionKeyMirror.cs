using System;
using System.Collections.Generic;
using UniversalSqueaker;

namespace UniversalSqueaker.KernelTests;

/// <summary>
/// 15 动作名镜像（= enum 名序 = BuiltInActionKeys 序）。
/// BuiltInCount = 15：只有内置 fallback 映射的动作数。
/// 声音键是测试数据：内核不携带任何产品声音键；产品种子由适配层从数据文件注入。
/// </summary>
internal static class ActionKeyMirror
{
    private static readonly string[] actionNames =
    {
        "Call",
        "Eat",
        "Sleep",
        "Wounded",
        "Select",
        "Move",
        "Social",
        "Joy",
        "Death",
        "Draft",
        "Undraft",
        "Attack",
        "Work",
        "Equip",
        "MentalBreak",
    };

    public static IReadOnlyList<string> All => Array.AsReadOnly(actionNames);

    /// <summary>15 = 全动作镜像。</summary>
    public static int Count => actionNames.Length;

    /// <summary>15 = 有内置 SoundDef 映射的动作数；内置表只播种这些项。</summary>
    public static int BuiltInCount => actionNames.Length;

    public static string For(SqueakAction action)
    {
        int index = (int)action;
        if ((uint)index >= (uint)actionNames.Length) throw new ArgumentOutOfRangeException(nameof(action));
        return actionNames[index];
    }

    /// <summary>内置 fallback 的中性测试声音键（仅测试数据）。</summary>
    public static string BuiltInFallbackKey(SqueakAction action)
    {
        if ((int)action >= BuiltInCount) throw new ArgumentOutOfRangeException(nameof(action));
        return "US_Fallback_" + For(action);
    }
}
