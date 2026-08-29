using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit;

/// <summary>
/// Public fallback guard for all UiKit consumers (built-in widgets and downstream mods).
/// Catches Measure/Draw failures, restores GUI state, runs a fallback, and logs once per
/// component per session through the UiKit log channel.
/// </summary>
public static class UiGuard
{
    private static readonly HashSet<string> LoggedThisSession = new(StringComparer.Ordinal);

    // Test seam: lets FerriteLib.UiKit.Tests capture warning text without a real game log.
    internal static Action<string>? LogWarningOverride;

    public static float MeasureOrFallback(
        Func<float> custom,
        float fallbackHeight,
        string componentId,
        string? logScope = null)
    {
        try
        {
            return custom();
        }
        catch (Exception ex)
        {
            LogOnce(componentId, logScope, ex);
            RestoreGuiState();
            return fallbackHeight;
        }
    }

    public static void DrawOrFallback(
        Rect rect,
        Action custom,
        Action<Rect> fallback,
        string componentId,
        string? logScope = null)
    {
        try
        {
            custom();
        }
        catch (Exception ex)
        {
            LogOnce(componentId, logScope, ex);
            RestoreGuiState();
            fallback?.Invoke(rect);
        }
    }

    /// <summary>
    /// Logs a page/component-level fallback through the same UiKit channel. Restores GUI state
    /// so a caller can safely draw a fallback page.
    /// </summary>
    public static void LogFallback(string componentId, string? logScope, Exception ex)
    {
        LogOnce(componentId, logScope, ex);
        RestoreGuiState();
    }

    public static void ResetSessionLog()
    {
        LoggedThisSession.Clear();
    }

    private static void LogOnce(string componentId, string? logScope, Exception ex)
    {
        string? resolvedScope = ResolveScope(componentId, logScope);
        string key = resolvedScope + "::" + (componentId ?? "");
        if (!LoggedThisSession.Add(key)) return;

        string scopeText = string.IsNullOrEmpty(resolvedScope) ? "" : " (" + resolvedScope + ")";
        string message = "[FerriteLib.UiKit] fallback triggered for component '" + componentId + "'" + scopeText + ": " + ex.Message;

        if (LogWarningOverride != null)
        {
            LogWarningOverride(message);
        }
        else
        {
            Log.Warning(message);
        }
    }

    private static string? ResolveScope(string componentId, string? logScope)
    {
        return string.IsNullOrEmpty(logScope) ? null : logScope;
    }

    private static void RestoreGuiState()
    {
        Text.Font = GameFont.Small;
        // Anchor is intentionally left to the fallback content so the guard does not depend on
        // UnityEngine.TextRenderingModule in minimal test stubs.
        GUI.color = Color.white;
    }
}
