using UnityEngine;
using Verse;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>Small shared drawing helpers for the US Ferrite widgets.</summary>
internal static class UsWidgetDrawing
{
    public static void DrawTitle(Rect rect, string text)
    {
        UiText.DrawTitle(rect, text);
    }

    public static void DrawSectionHeader(Rect rect, string text)
    {
        UiText.DrawSection(rect, text);
    }
}
