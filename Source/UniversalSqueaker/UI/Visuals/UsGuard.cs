using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Per-widget fallback guard. Catches custom draw/measure failures, restores GUI state, and runs a
/// vanilla fallback. Each widget id is logged only once per settings session.
/// </summary>
internal static class UsGuard
{
    private static readonly HashSet<string> LoggedThisSession = new(StringComparer.Ordinal);

    public static void ResetSessionLog()
    {
        LoggedThisSession.Clear();
    }

    public static float MeasureOrFallback(Func<float> custom, float fallbackHeight, string widgetId)
    {
        try
        {
            return custom();
        }
        catch (Exception ex)
        {
            LogOnce(widgetId, ex);
            RestoreGuiState();
            return fallbackHeight;
        }
    }

    public static void DrawOrFallback(Rect rect, Action custom, Action<Rect> vanillaFallback, string widgetId)
    {
        try
        {
            custom();
        }
        catch (Exception ex)
        {
            LogOnce(widgetId, ex);
            RestoreGuiState();
            vanillaFallback?.Invoke(rect);
        }
    }

    private static void RestoreGuiState()
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }

    private static void LogOnce(string widgetId, Exception ex)
    {
        if (LoggedThisSession.Add(widgetId))
        {
            Log.Warning("[UniversalSqueaker] UI widget fallback '" + widgetId + "': " + ex.Message);
        }
    }
}
