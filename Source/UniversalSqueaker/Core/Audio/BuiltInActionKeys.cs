using System;
using System.Collections.Generic;

namespace UniversalSqueaker.Kernel;

/// <summary>
/// The fifteen official package keys, in the same order as <c>string</c>.
/// 内置 fallback 只能使用此清单内的键；外部动作键永远不进入内置表。
/// </summary>
public static class BuiltInActionKeys
{
    private static readonly string[] keys = new List<string>(CoreActions.All).ToArray();
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
