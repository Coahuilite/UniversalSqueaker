using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// FerriteLib UiKit Phase A verification: registry fallback, manifest parsing safety,
/// and the two-pass layout engine's measure/draw/clamp behavior.
/// </summary>
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        try
        {
            RunAll();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("UNHANDLED: " + ex.GetType().FullName + " :: " + ex.Message);
        }

        if (failures == 0)
        {
            Console.WriteLine("ALL PASS");
            return 0;
        }

        Console.Error.WriteLine(failures + " test(s) failed.");
        return 1;
    }

    private static void RunAll()
    {
        Console.WriteLine("WidgetRegistry core fallback...");
        VerifyRegistryCoreFallback();

        Console.WriteLine("WidgetRegistry unknown kind...");
        VerifyRegistryUnknownKind();

        Console.WriteLine("LayoutManifest parse + unknown kind validation...");
        VerifyManifestParseAndUnknownKind();

        Console.WriteLine("LayoutManifest XXE and malformed XML rejection...");
        VerifyManifestRejectsUnsafeXml();

        Console.WriteLine("LayoutEngine fixed-height measure/draw consistency...");
        VerifyEngineFixedHeightRects();

        Console.WriteLine("LayoutEngine hidden elements...");
        VerifyEngineHiddenElements();

        Console.WriteLine("LayoutEngine scroll clamp...");
        VerifyEngineScrollClamp();
    }

    private static void VerifyRegistryCoreFallback()
    {
        WidgetRegistry.Clear();

        WidgetRegistry.Register(WidgetRegistry.CoreScope, "test/fallback",
            () => new RecordingWidget("test/fallback", 10f, new List<Rect>()));

        IWidget resolved = WidgetRegistry.Resolve("business-scope", "test/fallback");
        Check(resolved != null && resolved.Kind == "test/fallback",
            "core kind resolves through another scope");

        WidgetRegistry.Register("business-scope", "test/fallback",
            () => new RecordingWidget("exact-scope", 10f, new List<Rect>()));
        IWidget exact = WidgetRegistry.Resolve("business-scope", "test/fallback");
        Check(exact != null && exact.Kind == "exact-scope",
            "exact scope wins over core fallback");
    }

    private static void VerifyRegistryUnknownKind()
    {
        WidgetRegistry.Clear();

        try
        {
            WidgetRegistry.Resolve("some-scope", "missing/kind");
            Check(false, "unknown kind should throw");
        }
        catch (UnknownWidgetKindException ex)
        {
            Check(ex.Scope == "some-scope" && ex.Kind == "missing/kind",
                "UnknownWidgetKindException carries scope and kind");
        }
    }

    private static void VerifyManifestParseAndUnknownKind()
    {
        WidgetRegistry.Clear();
        WidgetRegistry.Register("test", "test/fixed",
            () => new RecordingWidget("test/fixed", 10f, new List<Rect>()));

        LayoutManifest manifest = LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"test\"><Widget id=\"a\" Kind=\"test/fixed\" Height=\"74\" /></UiPage>");

        CheckEqual("test", manifest.Source, "manifest source parsed");
        CheckEqual("1", manifest.SchemaVersion, "manifest schema version parsed");
        Check(manifest.Roots.Count == 1, "manifest root count");
        Check(manifest.Roots[0].Id == "a" && manifest.Roots[0].Kind == "test/fixed",
            "manifest root id and kind parsed");
        Check(manifest.Roots[0].TryGetAttribute("Height", out string height) && height == "74",
            "manifest attributes are readable case-insensitively");

        try
        {
            LayoutManifest.Parse(
                "<UiPage Schema=\"1\" Source=\"test\"><Widget id=\"b\" Kind=\"missing/kind\" /></UiPage>");
            Check(false, "manifest unknown kind should throw");
        }
        catch (UnknownWidgetKindException ex)
        {
            Check(ex.Scope == "test" && ex.Kind == "missing/kind",
                "manifest unknown kind exception carries scope and kind");
            Check(ex.Message.Contains("b"), "manifest unknown kind exception includes widget id");
        }
    }

    private static void VerifyManifestRejectsUnsafeXml()
    {
        WidgetRegistry.Clear();
        WidgetRegistry.Register("test", "test/fixed",
            () => new RecordingWidget("test/fixed", 10f, new List<Rect>()));

        string xxe = "<!DOCTYPE UiPage [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>"
            + "<UiPage Schema=\"1\" Source=\"test\"><Widget Kind=\"test/fixed\" Text=\"&xxe;\" /></UiPage>";

        CheckThrows(() => LayoutManifest.Parse(xxe),
            ex => ex is FormatException,
            "FormatException",
            "XXE DOCTYPE is rejected");

        CheckThrows(
            () => LayoutManifest.Parse("<UiPage Schema=\"1\" Source=\"test\"><Widget Kind=\"test/fixed\"></UiPage>"),
            ex => ex is FormatException,
            "FormatException",
            "malformed XML is rejected");
    }

    private static void VerifyEngineFixedHeightRects()
    {
        WidgetRegistry.Clear();
        var drawnRects = new List<Rect>();
        WidgetRegistry.Register("engine", "fixed",
            () => new RecordingWidget("fixed", 99f, drawnRects));

        LayoutManifest manifest = LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"engine\">"
            + "  <Widget id=\"a\" Kind=\"fixed\" Height=\"74\" />"
            + "  <Widget id=\"b\" Kind=\"fixed\" Height=\"40\" />"
            + "</UiPage>");

        var engine = new LayoutEngine(manifest);
        var ctx = new WidgetContext("engine", null, new StubMetrics(), new UiPageState());

        float contentHeight = engine.Measure(ctx, 400f);
        CheckEqual(114f, contentHeight, "engine fixed heights sum (74 + 40)");

        drawnRects.Clear();
        engine.Draw(new Rect(0f, 0f, 400f, 300f), ctx, _ => { });

        Check(drawnRects.Count == 2, "draw receives one rect per visible widget");
        Check(drawnRects.Count == 2 && drawnRects[0].height == 74f && drawnRects[1].height == 40f,
            "draw rect heights match measured heights");
        Check(drawnRects.Count == 2 && drawnRects[0].y == 0f && drawnRects[1].y == 74f,
            "draw rect y positions accumulate");
    }

    private static void VerifyEngineHiddenElements()
    {
        WidgetRegistry.Clear();
        var drawnRects = new List<Rect>();
        WidgetRegistry.Register("engine", "fixed",
            () => new RecordingWidget("fixed", 99f, drawnRects));

        LayoutManifest manifest = LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"engine\">"
            + "  <Widget id=\"a\" Kind=\"fixed\" Height=\"10\" />"
            + "  <Widget id=\"b\" Kind=\"fixed\" Height=\"20\" Hidden=\"true\" />"
            + "  <Widget id=\"c\" Kind=\"fixed\" Height=\"30\" Hidden=\"1\" />"
            + "  <Widget id=\"d\" Kind=\"fixed\" Height=\"40\" />"
            + "</UiPage>");

        var engine = new LayoutEngine(manifest);
        var ctx = new WidgetContext("engine", null, new StubMetrics(), new UiPageState());

        float contentHeight = engine.Measure(ctx, 400f);
        CheckEqual(50f, contentHeight, "hidden widgets are skipped in measure");

        drawnRects.Clear();
        engine.Draw(new Rect(0f, 0f, 400f, 300f), ctx, _ => { });

        Check(drawnRects.Count == 2, "hidden widgets are skipped in draw");
        Check(drawnRects.Count == 2 && drawnRects[0].height == 10f && drawnRects[1].height == 40f,
            "draw rect heights skip hidden elements");
    }

    private static void VerifyEngineScrollClamp()
    {
        WidgetRegistry.Clear();
        var drawnRects = new List<Rect>();
        WidgetRegistry.Register("engine", "fixed",
            () => new RecordingWidget("fixed", 99f, drawnRects));

        LayoutManifest smallManifest = LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"engine\"><Widget id=\"a\" Kind=\"fixed\" Height=\"40\" /></UiPage>");
        var smallEngine = new LayoutEngine(smallManifest);
        var smallCtx = new WidgetContext("engine", null, new StubMetrics(), new UiPageState());
        smallEngine.Measure(smallCtx, 400f);

        var state = new UiPageState { ScrollPosition = new Vector2(0f, 50f) };
        smallEngine.ClampScroll(state, 100f);
        CheckEqual(0f, state.ScrollPosition.y, "content smaller than view clamps to 0");

        LayoutManifest largeManifest = LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"engine\"><Widget id=\"a\" Kind=\"fixed\" Height=\"100\" /></UiPage>");
        var largeEngine = new LayoutEngine(largeManifest);
        var largeCtx = new WidgetContext("engine", null, new StubMetrics(), new UiPageState());
        largeEngine.Measure(largeCtx, 400f);

        state = new UiPageState { ScrollPosition = new Vector2(0f, 999f) };
        largeEngine.ClampScroll(state, 40f);
        CheckEqual(60f, state.ScrollPosition.y, "content larger than view clamps to max (100 - 40)");

        state = new UiPageState { ScrollPosition = new Vector2(0f, -5f) };
        largeEngine.ClampScroll(state, 40f);
        CheckEqual(0f, state.ScrollPosition.y, "negative scroll clamps to 0");
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

    private static void CheckEqual<T>(T expected, T actual, string name)
    {
        if (Equals(expected, actual))
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (expected '" + expected + "', got '" + actual + "')");
        }
    }

    private static void CheckThrows(Action action, Func<Exception, bool> isExpected, string expectedName, string name)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (isExpected(ex))
            {
                Console.WriteLine("  ok: " + name);
            }
            else
            {
                failures++;
                Console.Error.WriteLine("  FAIL: " + name + " (expected " + expectedName + ", got "
                    + ex.GetType().FullName + " :: " + ex.Message + ")");
            }
            return;
        }

        failures++;
        Console.Error.WriteLine("  FAIL: " + name + " (no exception thrown)");
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }
    }

    private sealed class RecordingWidget : IWidget
    {
        private readonly string kind;
        private readonly float measureHeight;
        private readonly List<Rect> drawnRects;

        public RecordingWidget(string kind, float measureHeight, List<Rect> drawnRects)
        {
            this.kind = kind;
            this.measureHeight = measureHeight;
            this.drawnRects = drawnRects;
        }

        public string Kind => kind;

        public void Configure(UiElementSpec spec)
        {
        }

        public float Measure(WidgetContext ctx)
        {
            return measureHeight;
        }

        public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
        {
            drawnRects.Add(rect);
        }
    }
}
