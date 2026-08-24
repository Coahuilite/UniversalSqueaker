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
}

public static class Widgets
{
    public static void Label(Rect rect, string text)
    {
    }

    public static void DrawBoxSolid(Rect rect, Color color)
    {
    }

    public static bool ButtonInvisible(Rect rect)
    {
        return false;
    }
}

public static class Mouse
{
    public static bool IsOver(Rect rect)
    {
        return false;
    }
}
