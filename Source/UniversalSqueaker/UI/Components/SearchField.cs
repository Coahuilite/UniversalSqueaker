using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>Immediate-mode search text field. Mutates only the caller-owned search string.</summary>
public static class SearchField
{
    public static void Draw(Rect rect, ref string value, string hint)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiInteract.Protect(rect);

        string current = value ?? "";
        UiGuard.DrawOrFallback(
            rect,
            () => { current = DrawCore(rect, current, hint); },
            fallback => { current = Widgets.TextField(fallback, current) ?? ""; },
            "us/search-field");
        value = current;
    }

    private static string DrawCore(Rect rect, string value, string hint)
    {
        UsSurface.DrawSurface(rect, UsSurface.SurfaceKind.Base);
        string controlName = "US_VoicePackSearch_" + hint.GetHashCode();
        GUI.SetNextControlName(controlName);
        string next = Widgets.TextField(rect, value ?? "");
        if (GUI.GetNameOfFocusedControl() == controlName)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UsVisualTokens.AccentGold);
        }
        if (string.IsNullOrEmpty(value))
        {
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            GUI.color = UsVisualTokens.TextSecondary;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect.ContractedBy(7f, 5f), hint);
            Text.Font = oldFont;
            GUI.color = oldColor;
        }
        return next ?? "";
    }
}
