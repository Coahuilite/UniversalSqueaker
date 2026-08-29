using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>B3: input/number-slider composite widget.</summary>
internal static class SliderNumberFieldTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyPureFunctions();
        VerifyMeasure();
        VerifyDrawEmitsSliderCommand();
        VerifyNumberFieldSyncsFloatValue();
        VerifyIllegalInputDoesNotBreakValue();
        VerifyUnchangedFieldDoesNotEmit();
        VerifyFocusedEditingNotClobberedBySlider();
        VerifyRegistryIntegration();
        return failures;
    }

    private static void VerifyPureFunctions()
    {
        Check(UiInteract.FormatValue(0.65f, "0%") == "65%", "FormatValue formats percent");
        Check(UiInteract.TryParseNumber("65", out float parsed) && Math.Abs(parsed - 65f) < 0.0001f,
            "ParseNumber parses valid number");
        Check(!UiInteract.TryParseNumber("abc", out _), "ParseNumber rejects invalid text");
        Check(Math.Abs(UiInteract.ClampValue(150f, 0f, 100f) - 100f) < 0.0001f,
            "ClampValue clamps above max");
    }

    private static void VerifyMeasure()
    {
        SliderNumberFieldWidget widget = MakeWidget("measure", 0f, 100f, "0.##", 42f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        Check(Math.Abs(widget.Measure(ctx) - 42f) < 0.0001f, "Measure returns Height attribute");
    }

    private static void VerifyDrawEmitsSliderCommand()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        var commands = new List<UiCommand>();
        SliderNumberFieldWidget widget = MakeWidget("slider-emit", 0f, 100f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("slider-emit", "value"));
        state.FloatValue = 50f;
        state.EditText = "50";

        UiInteract.SliderOverride = (_, _, _, _) => 65f;
        UiInteract.TextFieldOverride = (_, text) => text;

        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);

        Check(commands.Count == 1, "slider change emits one command");
        Check(commands.Count == 1 && commands[0].Name == "ValueChanged"
            && Math.Abs((float)commands[0].Payload! - 65f) < 0.0001f,
            "slider command carries new value");
        Check(state.EditText == "65", "slider change syncs EditText");
        UiInteract.EndFrame();
    }

    private static void VerifyNumberFieldSyncsFloatValue()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        var commands = new List<UiCommand>();
        SliderNumberFieldWidget widget = MakeWidget("field-sync", 0f, 100f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("field-sync", "value"));
        state.FloatValue = 50f;
        state.EditText = "50";

        UiInteract.SliderOverride = (_, _, _, _) => 50f;
        UiInteract.TextFieldOverride = (_, _) => "65";

        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);

        Check(Math.Abs(state.FloatValue - 65f) < 0.0001f, "number field legal input syncs FloatValue");
        Check(commands.Count == 1 && commands[0].Name == "ValueChanged"
            && Math.Abs((float)commands[0].Payload! - 65f) < 0.0001f,
            "number field legal input emits command");
        UiInteract.EndFrame();
    }

    private static void VerifyIllegalInputDoesNotBreakValue()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        var commands = new List<UiCommand>();
        SliderNumberFieldWidget widget = MakeWidget("field-illegal", 0f, 100f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("field-illegal", "value"));
        state.FloatValue = 50f;
        state.EditText = "50";

        UiInteract.SliderOverride = (_, _, _, _) => 50f;
        UiInteract.TextFieldOverride = (_, _) => "abc";

        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);

        Check(Math.Abs(state.FloatValue - 50f) < 0.0001f, "illegal input leaves FloatValue unchanged");
        Check(commands.Count == 0, "illegal input emits no command");
        UiInteract.EndFrame();
    }

    private static void VerifyUnchangedFieldDoesNotEmit()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        var commands = new List<UiCommand>();
        SliderNumberFieldWidget widget = MakeWidget("field-steady", 0f, 100f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("field-steady", "value"));
        state.FloatValue = 50f;
        state.EditText = "50";

        UiInteract.SliderOverride = (_, _, _, _) => 50f;
        UiInteract.TextFieldOverride = (_, text) => text;

        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);

        Check(commands.Count == 0, "unchanged unfocused field emits no command");
        UiInteract.EndFrame();
    }

    private static void VerifyFocusedEditingNotClobberedBySlider()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        var commands = new List<UiCommand>();
        SliderNumberFieldWidget widget = MakeWidget("field-edit", 0f, 100f, "0.##", 28f);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("field-edit", "value"));
        state.FloatValue = 50f;
        state.EditText = "abc";
        state.Focused = true;

        UiInteract.SliderOverride = (_, _, _, _) => 50f;
        UiInteract.TextFieldOverride = (_, text) => text;

        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx, commands.Add);

        Check(state.EditText == "abc", "focused edit text survives unchanged slider frame");
        Check(Math.Abs(state.FloatValue - 50f) < 0.0001f, "focused edit does not change FloatValue");
        Check(commands.Count == 0, "focused invalid edit emits no command");
        UiInteract.EndFrame();
    }

    private static void VerifyRegistryIntegration()
    {
        ResetDebug();
        WidgetRegistry.Clear();
        WidgetRegistry.Register(WidgetRegistry.CoreScope, SliderNumberFieldWidget.Kind,
            () => new SliderNumberFieldWidget());

        LayoutManifest manifest = LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"test\">"
            + "<Widget id=\"reg\" Kind=\"input/number-slider\" Min=\"0\" Max=\"100\" Height=\"28\" />"
            + "</UiPage>");
        LayoutEngine engine = new(manifest);
        WidgetContext ctx = new("test", null, new StubMetrics(), new UiPageState());

        UiInteract.SliderOverride = (_, _, _, _) => 50f;
        UiInteract.TextFieldOverride = (_, text) => text;

        bool threw = false;
        try
        {
            engine.Draw(new Rect(0f, 0f, 300f, 100f), ctx, _ => { });
        }
        catch (Exception ex)
        {
            threw = true;
            Console.Error.WriteLine("  FAIL: registry integration threw " + ex);
        }

        Check(!threw, "LayoutEngine draws input/number-slider without exception");
    }

    private static SliderNumberFieldWidget MakeWidget(string id, float min, float max, string format, float height)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Min"] = min.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Max"] = max.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Format"] = format,
            ["Height"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var spec = new UiElementSpec(id, SliderNumberFieldWidget.Kind, attributes);
        var widget = new SliderNumberFieldWidget();
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
