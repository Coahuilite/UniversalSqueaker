using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// Neutral fallback guard for built-in widgets. Restores GUI state and logs once per widget id per session.
/// </summary>
internal static class FerriteGuard
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
            if (LoggedThisSession.Add(widgetId))
            {
                Log.Warning("[FerriteLib.UiKit] widget fallback '" + widgetId + "': " + ex.Message);
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
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
            if (LoggedThisSession.Add(widgetId))
            {
                Log.Warning("[FerriteLib.UiKit] widget fallback '" + widgetId + "': " + ex.Message);
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            vanillaFallback?.Invoke(rect);
        }
    }
}
