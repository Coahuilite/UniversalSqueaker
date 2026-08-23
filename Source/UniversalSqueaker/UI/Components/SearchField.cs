using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Immediate-mode search text field. Mutates only the caller-owned search string.</summary>
public static class SearchField
{
    public static void Draw(Rect rect, ref string value, string hint)
    {
        Widgets.DrawBoxSolid(rect, new Color(.055f, .053f, .049f, .98f));
        SectionFrame.DrawBorder(rect);
        string controlName = "US_VoicePackSearch_" + hint.GetHashCode();
        GUI.SetNextControlName(controlName);
        string next = Widgets.TextField(rect, value ?? "");
        value = next ?? "";
        if (GUI.GetNameOfFocusedControl() == controlName)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UiPalette.Gold);
        }
        if (string.IsNullOrEmpty(value))
        {
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            GUI.color = new Color(.72f, .72f, .72f, .72f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect.ContractedBy(7f, 5f), hint);
            Text.Font = oldFont;
            GUI.color = oldColor;
        }
    }
}
