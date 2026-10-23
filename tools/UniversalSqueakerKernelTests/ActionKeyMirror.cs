using System;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;
namespace UniversalSqueaker.KernelTests;
/// <summary>Test-only translation of the frozen corpus vocabulary. No product short-key reader.</summary>
internal static class ActionKeyMirror
{
    public static IReadOnlyList<string> All => CoreActions.All;
    public static int Count => All.Count;
    public static int BuiltInCount => Count;
    public static string For(string action)
    {
        if (!BuiltInActionKeys.Contains(action)) throw new ArgumentOutOfRangeException(nameof(action));
        return action.Substring("us.core/".Length); // Historical synthetic sound labels remain byte-identical.
    }
    public static string FrozenId(string label)
    {
        if (BuiltInActionKeys.Contains("us.core/" + label)) return "us.core/" + label;
        return label == "Crying" || label == "Giggling" ? "fixture.history/" + label : label;
    }
    public static string BuiltInFallbackKey(string action) => "US_Fallback_" + For(action);
}