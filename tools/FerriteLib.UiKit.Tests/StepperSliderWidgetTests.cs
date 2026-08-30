using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>Phase 0-B: input/stepper-slider stepping, clamping, and slider/field sync.</summary>
internal static class StepperSliderWidgetTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyMeasure();
        VerifyMinusButtonSteps();
        VerifyPlusButtonClamps();
        VerifySliderSyncsFieldAndEmits();
        VerifyCustomWidthsDoNotBreakLayout();
        return failures;
    }

    private static void VerifyMeasure()
    {
        StepperSliderWidget widget = MakeWidget("stepper-measure", 0f, 100f, 2f, "0.##", 30f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        Check(Math.Abs(widget.Measure(ctx) - 30f) < 0.0001f, "stepper-slider Measure returns Height attribute");
    }

    private static void VerifyMinusButtonSteps()
    {
        ResetDebug();
        var commands = new List<UiCommand>();
        StepperSliderWidget widget = MakeWidget("stepper-minus", 0f, 10f, 2f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("stepper-minus", "value"));
        state.FloatValue = 4f;
        state.EditText = "4";

        UiInteract.BeginFrame();
        SetMouse(12f, 14f);
        UiInteract.DebugClick = true;
        UiInteract.SliderOverride = (_, _, _, _) => 4f;
        UiInteract.TextFieldOverride = (_, text) => text;
        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);
        UiInteract.ProcessEvents();
        UiInteract.EndFrame();

        Check(Math.Abs(state.FloatValue - 2f) < 0.0001f, "minus button steps value down by Step");
        Check(commands.Count == 1 && commands[0].Name == "ValueChanged"
            && Math.Abs((float)commands[0].Payload! - 2f) < 0.0001f,
            "minus button emits ValueChanged with stepped value");
    }

    private static void VerifyPlusButtonClamps()
    {
        ResetDebug();
        var commands = new List<UiCommand>();
        StepperSliderWidget widget = MakeWidget("stepper-plus", 0f, 10f, 2f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("stepper-plus", "value"));
        state.FloatValue = 9f;
        state.EditText = "9";

        UiInteract.BeginFrame();
        // Without a label, the plus button is the last 24px of a 300px-wide rect.
        SetMouse(288f, 14f);
        UiInteract.DebugClick = true;
        UiInteract.SliderOverride = (_, _, _, _) => 9f;
        UiInteract.TextFieldOverride = (_, text) => text;
        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);
        UiInteract.ProcessEvents();
        UiInteract.EndFrame();

        Check(Math.Abs(state.FloatValue - 10f) < 0.0001f, "plus button clamps at Max");
        Check(commands.Count == 1 && commands[0].Name == "ValueChanged"
            && Math.Abs((float)commands[0].Payload! - 10f) < 0.0001f,
            "plus clamp emits ValueChanged with clamped value");
    }

    private static void VerifySliderSyncsFieldAndEmits()
    {
        ResetDebug();
        var commands = new List<UiCommand>();
        StepperSliderWidget widget = MakeWidget("stepper-slider-sync", 0f, 100f, 5f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("stepper-slider-sync", "value"));
        state.FloatValue = 50f;
        state.EditText = "50";

        UiInteract.BeginFrame();
        UiInteract.SliderOverride = (_, _, _, _) => 65f;
        UiInteract.TextFieldOverride = (_, text) => text;
        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);
        UiInteract.EndFrame();

        Check(Math.Abs(state.FloatValue - 65f) < 0.0001f, "slider change syncs stepper FloatValue");
        Check(state.EditText == "65", "slider change syncs stepper EditText");
        Check(commands.Count == 1 && commands[0].Name == "ValueChanged"
            && Math.Abs((float)commands[0].Payload! - 65f) < 0.0001f,
            "slider change emits ValueChanged");
    }

    private static void VerifyCustomWidthsDoNotBreakLayout()
    {
        ResetDebug();
        var commands = new List<UiCommand>();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Min"] = "0",
            ["Max"] = "10",
            ["Step"] = "1",
            ["Format"] = "0.##",
            ["Height"] = "28",
            ["ButtonWidth"] = "16",
            ["FieldWidth"] = "36"
        };
        var spec = new UiElementSpec("stepper-compact", StepperSliderWidget.Kind, attributes);
        var widget = new StepperSliderWidget();
        widget.Configure(spec);
        var ctx = new WidgetContext("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("stepper-compact", "value"));
        state.FloatValue = 5f;
        state.EditText = "5";

        UiInteract.BeginFrame();
        UiInteract.SliderOverride = (_, _, _, _) => 5f;
        UiInteract.TextFieldOverride = (_, text) => text;
        bool threw = false;
        try
        {
            widget.Draw(new Rect(0f, 0f, 100f, 28f), ctx, commands.Add);
        }
        catch (Exception ex)
        {
            threw = true;
            Console.Error.WriteLine("  FAIL: compact stepper draw threw " + ex);
        }
        UiInteract.EndFrame();

        Check(!threw, "compact stepper-slider draws without throwing");
    }

    private static StepperSliderWidget MakeWidget(string id, float min, float max, float step, string format, float height)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Min"] = min.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Max"] = max.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Step"] = step.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Format"] = format,
            ["Height"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var spec = new UiElementSpec(id, StepperSliderWidget.Kind, attributes);
        var widget = new StepperSliderWidget();
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
