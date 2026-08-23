using System;
using System.Collections.Generic;

namespace SqueakyRatkin.KernelCharacterization;

/// <summary>
/// Local mirror of the current 17 <c>SqueakActionDefinitions.AudioKey</c> values.
/// KernelCharacterization deliberately does not link that SR product metadata file; update this
/// mirror only in the coordinated action-ABI change that also expands the corpus.
/// 0.3.1 波 4a：Crying/Giggling 已 append（15/16），语料 17 动作矩阵同变更重建。
/// <see cref="BuiltInCount"/> = 15：只有内置表映射的项（Crying/Giggling 无内置 SoundDef = 默认静默，§4.7）。
/// </summary>
internal static class ActionAudioKeyMirror
{
    private static readonly string[] audioKeys =
    {
        "SR_Call",
        "SR_Eat",
        "SR_Sleep",
        "SR_Wounded",
        "SR_Select",
        "SR_Move",
        "SR_Social",
        "SR_Joy",
        "SR_Death",
        "SR_Draft",
        "SR_Undraft",
        "SR_Attack",
        "SR_Work",
        "SR_Equip",
        "SR_MentalBreak",
        "SR_Crying",
        "SR_Giggling",
    };

    public static IReadOnlyList<string> All => Array.AsReadOnly(audioKeys);

    /// <summary>17 = 全动作镜像（含 Crying/Giggling 的 AudioKey 元数据）。</summary>
    public static int Count => audioKeys.Length;

    /// <summary>15 = 有内置 SoundDef 映射的动作数；内置表只播种这些项。</summary>
    public static int BuiltInCount => audioKeys.Length - 2;

    public static string For(SqueakyRatkin.SqueakAction action)
    {
        int index = (int)action;
        if ((uint)index >= (uint)audioKeys.Length) throw new ArgumentOutOfRangeException(nameof(action));
        return audioKeys[index];
    }
}
