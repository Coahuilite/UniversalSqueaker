using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>Phase 0-B: nested Block/Section/Column parsing, measurement, drawing, and hit ids.</summary>
internal static class ContainerLayoutTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyNestedParsing();
        VerifyNestedMeasure();
        VerifyNestedDrawOrder();
        VerifyHiddenSubtree();
        VerifyTryGetElementY();
        VerifyParserRejectsNestedWidgetContent();
        VerifyParserRejectsUnknownNestedElement();
        return failures;
    }

    private static void VerifyNestedParsing()
    {
        WidgetRegistry.Clear();
        WidgetRegistry.Register("ct", "fixed", () => new RecordingWidget("fixed", 10f, new List<Rect>()));

        LayoutManifest manifest = ParseNestedManifest();

        Check(manifest.Roots.Count == 1, "nested manifest has one root");
        UiElementSpec block = manifest.Roots[0];
        Check(block.Kind == "Block" && block.Id == "block", "Block id and kind parsed");
        Check(block.Children.Count == 2, "Block has two children");

        UiElementSpec section = block.Children[1];
        Check(section.Kind == "Section" && section.Id == "section", "Section nested under Block parsed");
        Check(section.Children.Count == 2, "Section has two children");

        UiElementSpec column = section.Children[1];
        Check(column.Kind == "Column" && column.Id == "columns", "Column nested under Section parsed");
        Check(column.Children.Count == 2, "Column has two children");
        Check(column.Children[0].Id == "left" && column.Children[1].Id == "right", "Column child ids parsed");
    }

    private static void VerifyNestedMeasure()
    {
        WidgetRegistry.Clear();
        var drawn = new List<Rect>();
        WidgetRegistry.Register("ct", "fixed", () => new RecordingWidget("fixed", 10f, drawn));

        LayoutManifest manifest = ParseNestedManifest();
        LayoutEngine engine = new(manifest);
        WidgetContext ctx = new("ct", null, new StubMetrics(), new UiPageState());

        float height = engine.Measure(ctx, 400f);

        // Block: pad 4 + title 22 + [a 10 + gap 2 + Section(title 22 + b 20 + Column max(30,40)=40)] + pad 4.
        CheckEqual(124f, height, "nested container measure accumulates heights, titles, padding, and gaps");
    }

    private static void VerifyNestedDrawOrder()
    {
        WidgetRegistry.Clear();
        var drawn = new List<Rect>();
        WidgetRegistry.Register("ct", "fixed", () => new RecordingWidget("fixed", 10f, drawn));

        LayoutManifest manifest = ParseNestedManifest();
        LayoutEngine engine = new(manifest);
        WidgetContext ctx = new("ct", null, new StubMetrics(), new UiPageState());
        engine.Measure(ctx, 400f);

        drawn.Clear();
        engine.Draw(new Rect(0f, 0f, 400f, 300f), ctx, _ => { });

        Check(drawn.Count == 4, "nested draw visits every visible leaf exactly once");
        Check(drawn.Count == 4 && drawn[0].height == 10f, "first draw is Block child a");
        Check(drawn.Count == 4 && drawn[1].height == 20f, "second draw is Section child b");
        Check(drawn.Count == 4 && drawn[2].height == 30f, "third draw is Column left child");
        Check(drawn.Count == 4 && drawn[3].height == 40f, "fourth draw is Column right child");
    }

    private static void VerifyHiddenSubtree()
    {
        WidgetRegistry.Clear();
        var drawn = new List<Rect>();
        WidgetRegistry.Register("ct", "fixed", () => new RecordingWidget("fixed", 10f, drawn));

        string xml = "<UiPage Schema=\"1\" Source=\"ct\">"
            + "<Block Id=\"visible\" Padding=\"2\" Gap=\"1\">"
            + "  <Widget Id=\"a\" Kind=\"fixed\" Height=\"10\" />"
            + "  <Section Id=\"hidden-section\" Title=\"Hidden\" Hidden=\"true\">"
            + "    <Widget Id=\"hidden-a\" Kind=\"fixed\" Height=\"50\" />"
            + "    <Widget Id=\"hidden-b\" Kind=\"fixed\" Height=\"60\" />"
            + "  </Section>"
            + "  <Widget Id=\"b\" Kind=\"fixed\" Height=\"20\" />"
            + "</Block>"
            + "</UiPage>";

        LayoutManifest manifest = LayoutManifest.Parse(xml);
        LayoutEngine engine = new(manifest);
        WidgetContext ctx = new("ct", null, new StubMetrics(), new UiPageState());

        float height = engine.Measure(ctx, 400f);
        CheckEqual(2f + 2f + 10f + 1f + 20f, height, "hidden container subtree contributes no height");
        Check(!engine.TryGetElementY("hidden-a", out _), "hidden child is not in the measured element map");

        drawn.Clear();
        engine.Draw(new Rect(0f, 0f, 400f, 100f), ctx, _ => { });
        Check(drawn.Count == 2, "hidden subtree children are not drawn");
    }

    private static void VerifyTryGetElementY()
    {
        WidgetRegistry.Clear();
        WidgetRegistry.Register("ct", "fixed", () => new RecordingWidget("fixed", 10f, new List<Rect>()));

        LayoutManifest manifest = ParseNestedManifest();
        LayoutEngine engine = new(manifest);
        WidgetContext ctx = new("ct", null, new StubMetrics(), new UiPageState());
        engine.Measure(ctx, 400f);

        Check(engine.TryGetElementY("block", out float blockY) && blockY == 0f, "root Block y found");
        Check(engine.TryGetElementY("section", out float sectionY) && sectionY > blockY, "nested Section y found");
        Check(engine.TryGetElementY("right", out float rightY) && rightY > sectionY, "deep Column child y found");
        Check(!engine.TryGetElementY("missing", out _), "missing id returns false");
    }

    private static void VerifyParserRejectsNestedWidgetContent()
    {
        WidgetRegistry.Clear();
        WidgetRegistry.Register("ct", "fixed", () => new RecordingWidget("fixed", 10f, new List<Rect>()));

        CheckThrows(
            () => LayoutManifest.Parse(
                "<UiPage Schema=\"1\" Source=\"ct\"><Widget Kind=\"fixed\"><Widget Kind=\"fixed\" /></Widget></UiPage>"),
            "nested element inside Widget is rejected");
    }

    private static void VerifyParserRejectsUnknownNestedElement()
    {
        WidgetRegistry.Clear();
        WidgetRegistry.Register("ct", "fixed", () => new RecordingWidget("fixed", 10f, new List<Rect>()));

        CheckThrows(
            () => LayoutManifest.Parse(
                "<UiPage Schema=\"1\" Source=\"ct\"><Block><Unknown /></Block></UiPage>"),
            "unknown element inside a container is rejected");
    }

    private static LayoutManifest ParseNestedManifest()
    {
        return LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"ct\">"
            + "<Block Id=\"block\" Title=\"Block\" Padding=\"4\" Gap=\"2\">"
            + "  <Widget Id=\"a\" Kind=\"fixed\" Height=\"10\" />"
            + "  <Section Id=\"section\" Title=\"Section\">"
            + "    <Widget Id=\"b\" Kind=\"fixed\" Height=\"20\" />"
            + "    <Column Id=\"columns\" Gap=\"3\">"
            + "      <Widget Id=\"left\" Kind=\"fixed\" Height=\"30\" />"
            + "      <Widget Id=\"right\" Kind=\"fixed\" Height=\"40\" />"
            + "    </Column>"
            + "  </Section>"
            + "</Block>"
            + "</UiPage>");
    }

    private static void CheckThrows(Action action, string name)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (ex is FormatException || ex is UnknownWidgetKindException)
            {
                Console.WriteLine("  ok: " + name);
                return;
            }

            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (unexpected " + ex.GetType().FullName + ": " + ex.Message + ")");
            return;
        }

        failures++;
        Console.Error.WriteLine("  FAIL: " + name + " (no exception thrown)");
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

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return 24f;
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
