using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>Phase 0-B: chart/line parsing, drawing, and control-point dragging.</summary>
internal static class LineChartWidgetTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyParsePoints();
        VerifyDrawDoesNotThrow();
        VerifyDragControlPointEmits();
        VerifyEditablePointsRestrictsDrag();
        return failures;
    }

    private static void VerifyParsePoints()
    {
        List<Vector2> points = LineChartWidget.ParsePoints("0,0;0.25,0.8;0.5,0.5;0.75,0.9;1,0");

        Check(points.Count == 5, "Points literal parses five control points");
        Check(points.Count == 5 && Math.Abs(points[0].x) < 0.0001f && Math.Abs(points[0].y) < 0.0001f,
            "first point is 0,0");
        Check(points.Count == 5 && Math.Abs(points[1].x - 0.25f) < 0.0001f
            && Math.Abs(points[1].y - 0.8f) < 0.0001f,
            "second point parses normalized coordinates");
        Check(points.Count == 5 && Math.Abs(points[4].x - 1f) < 0.0001f && Math.Abs(points[4].y) < 0.0001f,
            "last point is 1,0");
    }

    private static void VerifyDrawDoesNotThrow()
    {
        ResetDebug();
        LineChartWidget widget = MakeWidget("chart-draw", "0,0;0.25,0.8;0.5,0.5;0.75,0.9;1,0", true, 120f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());

        UiInteract.BeginFrame();
        bool threw = false;
        try
        {
            widget.Draw(new Rect(0f, 0f, 200f, 120f), ctx, _ => { });
        }
        catch (Exception ex)
        {
            threw = true;
            Console.Error.WriteLine("  FAIL: chart draw threw " + ex);
        }

        Check(!threw, "chart/line draws without throwing");
        UiInteract.EndFrame();
    }

    private static void VerifyDragControlPointEmits()
    {
        ResetDebug();
        var commands = new List<UiCommand>();
        LineChartWidget widget = MakeWidget("chart-drag", "0,0;0.5,0.5;1,1", true, 120f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("chart-drag", "chart"));

        // Plot rect is (8,8,184,104). Point 1 (0.5,0.5) pixel is (100,60).
        UiInteract.BeginFrame();
        SetMouse(100f, 60f);
        UiInteract.DebugMouseDown = true;
        widget.Draw(new Rect(0f, 0f, 200f, 120f), ctx, commands.Add);

        Check(state.Dragging && state.Cursor == 1, "pointer down near a control point starts dragging it");

        SetMouse(150f, 20f);
        UiInteract.DebugMouseDown = false;
        UiInteract.DebugMouseDrag = true;
        widget.Draw(new Rect(0f, 0f, 200f, 120f), ctx, commands.Add);

        UiInteract.DebugMouseDrag = false;
        UiInteract.DebugMouseUp = true;
        widget.Draw(new Rect(0f, 0f, 200f, 120f), ctx, commands.Add);
        UiInteract.EndFrame();

        Check(!state.Dragging, "pointer up ends control-point dragging");
        Check(commands.Count == 1, "dragging a control point emits one command");
        Check(commands.Count == 1 && commands[0].Name == "PointChanged"
            && commands[0].Payload is LineChartPointChange change
            && change.Index == 1
            && Math.Abs(change.Point.x - 0.77173913f) < 0.001f
            && Math.Abs(change.Point.y - 0.88461538f) < 0.001f,
            "chart command payload contains index and normalized point");
    }

    private static void VerifyEditablePointsRestrictsDrag()
    {
        ResetDebug();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Points"] = "0,0;0.5,0.5;1,1",
            ["Editable"] = "true",
            ["EditablePoints"] = "1",
            ["Height"] = "120"
        };
        var spec = new UiElementSpec("chart-editable-points", LineChartWidget.Kind, attributes);
        var widget = new LineChartWidget();
        widget.Configure(spec);
        var ctx = new WidgetContext("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("chart-editable-points", "chart"));

        // Point 0 is at pixel (8,112). It is not in EditablePoints, so it must not start a drag.
        UiInteract.BeginFrame();
        SetMouse(8f, 112f);
        UiInteract.DebugMouseDown = true;
        widget.Draw(new Rect(0f, 0f, 200f, 120f), ctx, _ => { });
        Check(!state.Dragging, "non-editable point does not start drag");
        UiInteract.EndFrame();

        ResetDebug();
        // Point 1 is at pixel (100,60) and is editable.
        UiInteract.BeginFrame();
        SetMouse(100f, 60f);
        UiInteract.DebugMouseDown = true;
        widget.Draw(new Rect(0f, 0f, 200f, 120f), ctx, _ => { });
        Check(state.Dragging && state.Cursor == 1, "editable point starts drag");
        UiInteract.EndFrame();
    }

    private static LineChartWidget MakeWidget(string id, string points, bool editable, float height)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Points"] = points,
            ["Editable"] = editable ? "true" : "false",
            ["Height"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var spec = new UiElementSpec(id, LineChartWidget.Kind, attributes);
        var widget = new LineChartWidget();
        widget.Configure(spec);
        return widget;
    }

    private static void ResetDebug()
    {
        UiInteract.DebugMousePositionEnabled = false;
        UiInteract.DebugMousePosition = default;
        UiInteract.DebugClick = false;
        UiInteract.DebugMouseDown = false;
        UiInteract.DebugMouseDrag = false;
        UiInteract.DebugMouseUp = false;
        UiInteract.DebugEnter = false;
        UiInteract.DebugFocusLost = false;
        UiInteract.SliderOverride = null;
        UiInteract.TextFieldOverride = null;
        UiValueStore.ResetFrame();
    }

    private static void SetMouse(float x, float y)
    {
        UiInteract.DebugMousePositionEnabled = true;
        UiInteract.DebugMousePosition = new Vector2(x, y);
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
}
