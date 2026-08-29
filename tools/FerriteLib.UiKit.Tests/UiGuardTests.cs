using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>U0: public UiGuard fallback behavior, per-session dedupe, and component logging.</summary>
internal static class UiGuardTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyMeasureFallback();
        VerifyDrawFallback();
        VerifyLogOncePerSession();
        VerifyResetAllowsRelog();
        VerifyLogContainsComponentAndScope();
        return failures;
    }

    private static void VerifyMeasureFallback()
    {
        UiGuard.ResetSessionLog();
        UiGuard.LogWarningOverride = _ => { };

        float result = UiGuard.MeasureOrFallback(
            () => throw new InvalidOperationException("boom"),
            42f,
            "test/measure",
            "TestScope");

        CheckEqual(42f, result, "MeasureOrFallback returns fallback height on exception");
    }

    private static void VerifyDrawFallback()
    {
        UiGuard.ResetSessionLog();
        UiGuard.LogWarningOverride = _ => { };
        bool fallbackDrawn = false;

        UiGuard.DrawOrFallback(
            new Rect(0f, 0f, 10f, 10f),
            () => throw new InvalidOperationException("draw boom"),
            _ => fallbackDrawn = true,
            "test/draw",
            "TestScope");

        Check(fallbackDrawn, "DrawOrFallback invokes fallback on exception");
    }

    private static void VerifyLogOncePerSession()
    {
        UiGuard.ResetSessionLog();
        var messages = new List<string>();
        UiGuard.LogWarningOverride = messages.Add;

        UiGuard.MeasureOrFallback(
            () => throw new InvalidOperationException("once"),
            1f,
            "test/dedupe",
            "TestScope");
        UiGuard.MeasureOrFallback(
            () => throw new InvalidOperationException("twice"),
            1f,
            "test/dedupe",
            "TestScope");

        Check(messages.Count == 1, "same component/session logs only once");
    }

    private static void VerifyResetAllowsRelog()
    {
        UiGuard.ResetSessionLog();
        var messages = new List<string>();
        UiGuard.LogWarningOverride = messages.Add;

        UiGuard.MeasureOrFallback(() => throw new InvalidOperationException("a"), 1f, "test/reset", "TestScope");
        UiGuard.ResetSessionLog();
        UiGuard.MeasureOrFallback(() => throw new InvalidOperationException("b"), 1f, "test/reset", "TestScope");

        Check(messages.Count == 2, "ResetSessionLog allows the same component to log again");
    }

    private static void VerifyLogContainsComponentAndScope()
    {
        UiGuard.ResetSessionLog();
        string? captured = null;
        UiGuard.LogWarningOverride = message => captured = message;

        UiGuard.MeasureOrFallback(
            () => throw new InvalidOperationException("detail"),
            1f,
            "us/scope-tree",
            "UniversalSqueaker");

        Check(captured != null
            && captured.Contains("us/scope-tree")
            && captured.Contains("UniversalSqueaker")
            && captured.Contains("[FerriteLib.UiKit]"),
            "log message contains UiKit channel, component id, and scope");

        UiGuard.LogWarningOverride = _ => { };
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private static void CheckEqual<T>(T expected, T actual, string name)
    {
        if (Equals(expected, actual))
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (expected '" + expected + "', got '" + actual + "')");
        }
    }
}
