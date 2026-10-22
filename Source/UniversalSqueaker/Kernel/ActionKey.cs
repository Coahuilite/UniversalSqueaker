using System;

namespace UniversalSqueaker.Kernel;

/// <summary>
/// 内核动作键边界映射：内置键以 <see cref="BuiltInActionKeys"/> 为唯一权威，
/// 枚举保留在 <c>UniversalSqueaker</c> 命名空间以维持设置/存档的序列化 ABI。
/// 17 键全部可双向解析。
/// </summary>
public static class ActionKey
{
    /// <summary>Translate only the closed legacy schema at its boundary; never strip an extension's tail.</summary>
    public static string DefinitionId(string key) => BuiltInActionKeys.Contains(key) ? "us.core/" + key : key;
    public static string SchemaKey(string key) => key.StartsWith("us.core/", StringComparison.Ordinal)
        && BuiltInActionKeys.Contains(key.Substring(8)) ? key.Substring(8) : key;

    private static readonly int SerializedActionCount = Enum.GetNames(typeof(SqueakAction)).Length;

    /// <summary>枚举 → 规范内置键。未知或尚未定义的枚举值返回 null。</summary>
    public static string? For(SqueakAction action)
    {
        int index = (int)action;
        return (uint)index < (uint)SerializedActionCount && index < BuiltInActionKeys.All.Count
            ? BuiltInActionKeys.All[index]
            : null;
    }

    /// <summary>已 append 的内置规范键 → 枚举值；保留但尚未 append 的键返回 false。</summary>
    public static bool TryParseBuiltIn(string key, out SqueakAction action)
    {
        if (BuiltInActionKeys.TryGetIndex(key, out int index) && (uint)index < (uint)SerializedActionCount)
        {
            action = (SqueakAction)index;
            return true;
        }
        action = default;
        return false;
    }
}
