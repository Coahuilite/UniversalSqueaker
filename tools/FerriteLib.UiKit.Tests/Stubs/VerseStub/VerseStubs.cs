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

/// <summary>
/// Minimal Verse persistence contract. Production settings records implement it, so the stub
/// must declare it for those types to load when the tuning/package widget paths run in the
/// harness.
/// </summary>
public interface IExposable
{
    void ExposeData();
}

/// <summary>
/// Minimal Verse float-pair value type (settings/tuning records reference it; only the type and
/// the min/max fields are needed for the widget paths to load).
/// </summary>
public struct FloatRange
{
    public float min;
    public float max;

    public FloatRange(float min, float max)
    {
        this.min = min;
        this.max = max;
    }
}

/// <summary>
/// Minimal Verse tagged string. The real RimWorld type carries rich-text tags and implicit
/// string conversions; the harness only needs the identity/string-conversion surface so the
/// real kernel translation seam can execute without a game language database.
/// </summary>
public struct TaggedString
{
    private readonly string rawText;

    public TaggedString(string rawText)
    {
        this.rawText = rawText ?? "";
    }

    public string RawText => rawText;

    public static implicit operator string(TaggedString taggedString)
    {
        return taggedString.rawText;
    }

    public static implicit operator TaggedString(string str)
    {
        return new TaggedString(str);
    }

    public override string ToString()
    {
        return rawText;
    }
}

/// <summary>Deterministic translation stub: keys pass through unchanged.</summary>
public static class Translator
{
    public static TaggedString Translate(this string key)
    {
        return new TaggedString(key ?? "");
    }
}

public static class Text
{
    public static GameFont Font { get; set; }

    public static TextAnchor Anchor { get; set; }

    // Deterministic stub so real widget Measure/Draw paths (e.g. ChromeBannerWidget, the US
    // kernel sections) can execute text-height layout without a real IMGUI text engine.
    public static float CalcHeight(string text, float width)
    {
        return 16f;
    }
}

public static class Widgets
{
    // Recording hooks used by visual regression tests. The test assembly compiles against the
    // Krafs ref assembly, so these are only accessible through reflection at runtime.
    public static readonly List<Rect> DrawBoxSolidRects = new();
    public static readonly List<Color> DrawBoxSolidColors = new();
    public static int ScrollViewDepth;
    public static int BeginScrollViewCalls;
    public static int EndScrollViewCalls;

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

    public static bool ButtonInvisible(Rect rect, bool doSound)
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

    public static void BeginScrollView(Rect outRect, ref Vector2 scrollPosition, Rect viewRect)
    {
        ScrollViewDepth++;
        BeginScrollViewCalls++;
    }

    public static void BeginScrollView(Rect outRect, ref Vector2 scrollPosition, Rect viewRect, bool showVerticalScrollbar)
    {
        ScrollViewDepth++;
        BeginScrollViewCalls++;
    }

    public static void EndScrollView()
    {
        if (ScrollViewDepth > 0) ScrollViewDepth--;
        EndScrollViewCalls++;
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
