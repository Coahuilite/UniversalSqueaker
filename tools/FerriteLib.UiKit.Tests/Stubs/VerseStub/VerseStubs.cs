using System.Collections.Generic;
using UnityEngine;

namespace Verse;

/// <summary>Executable stub for the Verse IMGUI types the UiKit test paths touch at runtime.</summary>
public enum GameFont
{
    Tiny,
    Small,
    Medium
}

public static class Text
{
    public static GameFont Font { get; set; }

    public static TextAnchor Anchor { get; set; }
}

public static class Widgets
{
    // Recording hooks used by visual regression tests. The test assembly compiles against the
    // Krafs ref assembly, so these are only accessible through reflection at runtime.
    public static readonly List<Rect> DrawBoxSolidRects = new();
    public static readonly List<Color> DrawBoxSolidColors = new();

    public static void Label(Rect rect, string text)
    {
    }

    public static void DrawBoxSolid(Rect rect, Color color)
    {
        DrawBoxSolidRects.Add(rect);
        DrawBoxSolidColors.Add(color);
    }

    public static void ClearDrawBoxSolidCalls()
    {
        DrawBoxSolidRects.Clear();
        DrawBoxSolidColors.Clear();
    }

    public static bool ButtonInvisible(Rect rect)
    {
        return false;
    }

    public static float HorizontalSlider(
        Rect rect,
        float value,
        float min,
        float max,
        bool middleAlignment = false,
        string? label = null,
        string? leftAlignedLabel = null,
        string? rightAlignedLabel = null,
        float roundTo = -1f)
    {
        return value;
    }

    public static string TextField(Rect rect, string text)
    {
        return text ?? "";
    }
}

public static class Mouse
{
    public static bool IsOver(Rect rect)
    {
        return false;
    }
}

public static class Log
{
    public static void Warning(string message)
    {
    }
}
