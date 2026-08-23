using System;
using System.Collections.Generic;

namespace SqueakyRatkin.Kernel;

/// <summary>
/// 内置动作键的唯一权威（§5 波 1）。顺序是序列化 <c>SqueakAction</c> 的 append-only 顺序；
/// 0.3.1 波 3c 已 append Crying/Giggling（15/16），与产品枚举双向映射。
/// 内置 fallback 只能使用此清单内的键；外部动作键永远不进入内置表。
/// </summary>
public static class BuiltInActionKeys
{
    private static readonly string[] keys =
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
        "Crying",
        "Giggling",
    };

    private static readonly IReadOnlyList<string> readOnlyKeys = Array.AsReadOnly(keys);
    private static readonly IReadOnlyDictionary<string, int> indexes = BuildIndexes();

    public static IReadOnlyList<string> All => readOnlyKeys;

    public static bool Contains(string? key) => key != null && indexes.ContainsKey(key);

    public static bool TryGetIndex(string? key, out int index)
    {
        if (key != null && indexes.TryGetValue(key, out index)) return true;
        index = default;
        return false;
    }

    private static IReadOnlyDictionary<string, int> BuildIndexes()
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        for (int i = 0; i < keys.Length; i++) result.Add(keys[i], i);
        return result;
    }
}
