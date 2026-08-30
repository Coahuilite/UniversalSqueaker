using System;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// G4 visual regression tests for the neutral UiKit selectable button helper.
/// The Verse stub records every DrawBoxSolid call; these tests assert that the selected
/// state emits the reference bottom/left accent bar using a Palette token.
/// </summary>
internal static class SelectionButtonTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifySelectedBottomAccent();
        VerifySelectedLeftAccent();
        VerifyNonSelectedHasNoAccent();
        VerifyDangerSelectedUsesDangerAccent();
        VerifyDrawFieldOpenShowsAccent();
        return failures;
    }

    private static void VerifySelectedBottomAccent()
    {
        ResetDrawCalls();
        var rect = new Rect(10f, 20f, 120f, 28f);
        SelectionButton.Draw(rect, "Layer", true, accent: SelectionButton.SelectionAccent.Bottom, hovered: false);

        IReadOnlyList<DrawCall> calls = SnapshotDrawCalls();
        Rect? accent = FindBottomAccent(calls, Palette.AccentGold);
        Check(accent.HasValue, "selected bottom accent bar is drawn");
        Check(accent.HasValue && Near(accent.Value.y, rect.yMax - SelectionButton.BottomAccentHeight)
            && Near(accent.Value.height, SelectionButton.BottomAccentHeight),
            "bottom accent bar sits at rect.yMax - 3f with 3f height");
    }

    private static void VerifySelectedLeftAccent()
    {
        ResetDrawCalls();
        var rect = new Rect(10f, 20f, 120f, 28f);
        SelectionButton.Draw(rect, "Layer", true, accent: SelectionButton.SelectionAccent.Left, hovered: false);

        IReadOnlyList<DrawCall> calls = SnapshotDrawCalls();
        Rect? accent = FindLeftAccent(calls, Palette.AccentGold);
        Check(accent.HasValue, "selected left accent bar is drawn");
        Check(accent.HasValue && Near(accent.Value.x, rect.x + 1f)
            && Near(accent.Value.width, SelectionButton.LeftAccentWidth)
            && Near(accent.Value.height, rect.height - 2f),
            "left accent bar sits at x+1 with 4f width and full height");
    }

    private static void VerifyNonSelectedHasNoAccent()
    {
        ResetDrawCalls();
        var rect = new Rect(0f, 0f, 100f, 24f);
        SelectionButton.Draw(rect, "Off", false, accent: SelectionButton.SelectionAccent.Bottom, hovered: false);

        IReadOnlyList<DrawCall> calls = SnapshotDrawCalls();
        Check(FindBottomAccent(calls, Palette.AccentGold) == null,
            "non-selected button does not draw a bottom gold accent");
        Check(FindLeftAccent(calls, Palette.AccentGold) == null,
            "non-selected button does not draw a left gold accent");
    }

    private static void VerifyDangerSelectedUsesDangerAccent()
    {
        ResetDrawCalls();
        var rect = new Rect(0f, 0f, 100f, 24f);
        SelectionButton.Draw(rect, "Forget", true, danger: true, hovered: false);

        IReadOnlyList<DrawCall> calls = SnapshotDrawCalls();
        Rect? accent = FindBottomAccent(calls, Palette.Danger);
        Check(accent.HasValue, "danger selected button draws a bottom danger accent bar");
        Check(FindBottomAccent(calls, Palette.AccentGold) == null,
            "danger selected button does not draw a gold accent bar");
    }

    private static void VerifyDrawFieldOpenShowsAccent()
    {
        ResetDrawCalls();
        var rect = new Rect(0f, 0f, 160f, 24f);
        SelectionButton.DrawField(rect, "Any", true, hovered: false);

        IReadOnlyList<DrawCall> calls = SnapshotDrawCalls();
        Check(FindBottomAccent(calls, Palette.AccentGold).HasValue,
            "open dropdown trigger field draws a bottom gold accent");
    }

    private static Rect? FindBottomAccent(IReadOnlyList<DrawCall> calls, Color color)
    {
        foreach (DrawCall call in calls)
        {
            if (!NearColor(call.Color, color)) continue;
            if (Near(call.Rect.height, SelectionButton.BottomAccentHeight)
                && Near(call.Rect.width, call.Rect.width) // width may vary; height is the signature
                && call.Rect.y > call.Rect.yMax - SelectionButton.BottomAccentHeight - 0.01f)
            {
                // The bottom border is only 1px; the accent is exactly 3px tall and touches yMax.
                if (Near(call.Rect.yMax, call.Rect.y + SelectionButton.BottomAccentHeight))
                {
                    return call.Rect;
                }
            }
        }

        return null;
    }

    private static Rect? FindLeftAccent(IReadOnlyList<DrawCall> calls, Color color)
    {
        foreach (DrawCall call in calls)
        {
            if (!NearColor(call.Color, color)) continue;
            if (Near(call.Rect.width, SelectionButton.LeftAccentWidth)
                && call.Rect.height > 1f
                && call.Rect.x > call.Rect.xMax - SelectionButton.LeftAccentWidth - 0.01f)
            {
                // Left border is 1px wide; the accent is 4px wide and touches the left edge.
                if (Near(call.Rect.xMax, call.Rect.x + SelectionButton.LeftAccentWidth))
                {
                    return call.Rect;
                }
            }
        }

        return null;
    }

    private static void ResetDrawCalls()
    {
        MethodInfo clear = typeof(Verse.Widgets).GetMethod("ClearDrawBoxSolidCalls",
            BindingFlags.Public | BindingFlags.Static);
        Check(clear != null, "Verse stub exposes ClearDrawBoxSolidCalls");
        clear?.Invoke(null, null);
    }

    private static IReadOnlyList<DrawCall> SnapshotDrawCalls()
    {
        var calls = new List<DrawCall>();
        FieldInfo rectsField = typeof(Verse.Widgets).GetField("DrawBoxSolidRects",
            BindingFlags.Public | BindingFlags.Static);
        FieldInfo colorsField = typeof(Verse.Widgets).GetField("DrawBoxSolidColors",
            BindingFlags.Public | BindingFlags.Static);
        Check(rectsField != null && colorsField != null, "Verse stub exposes draw call lists");

        if (rectsField == null || colorsField == null) return calls;

        var rects = (System.Collections.IList)rectsField.GetValue(null)!;
        var colors = (System.Collections.IList)colorsField.GetValue(null)!;
        int count = Math.Min(rects.Count, colors.Count);
        for (int i = 0; i < count; i++)
        {
            calls.Add(new DrawCall((Rect)rects[i]!, (Color)colors[i]!));
        }

        return calls;
    }

    private static bool Near(float a, float b)
    {
        return Math.Abs(a - b) < 0.01f;
    }

    private static bool NearColor(Color a, Color b)
    {
        return Near(a.r, b.r) && Near(a.g, b.g) && Near(a.b, b.b) && Near(a.a, b.a);
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

    private readonly struct DrawCall
    {
        internal readonly Rect Rect;
        internal readonly Color Color;

        internal DrawCall(Rect rect, Color color)
        {
            Rect = rect;
            Color = color;
        }
    }
}
