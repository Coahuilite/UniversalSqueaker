using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>Phase 0-B: input/dropdown parsing and selection interaction.</summary>
internal static class DropdownWidgetTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyParseOptionsAndValues();
        VerifyParseOptionNAttributes();
        VerifySelectionEmitsCommand();
        VerifyDynamicOptionsFromViewState();
        return failures;
    }

    private static void VerifyParseOptionsAndValues()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Options"] = "Alpha, Beta;Gamma",
            ["Values"] = "a,b,c"
        };
        var spec = new UiElementSpec("parse", DropdownWidget.Kind, attributes);

        IReadOnlyList<DropdownWidget.DropdownOption> options = DropdownWidget.ParseOptions(spec);

        Check(options.Count == 3, "Options attribute splits on comma and semicolon");
        Check(options.Count == 3 && options[0].Text == "Alpha" && options[1].Text == "Beta" && options[2].Text == "Gamma",
            "option texts are trimmed in order");
        Check(options.Count == 3 && options[0].Value == "a" && options[1].Value == "b" && options[2].Value == "c",
            "Values attribute maps to submitted values");
    }

    private static void VerifyParseOptionNAttributes()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Option2"] = "Second",
            ["Option1"] = "First",
            ["Option3"] = "Third",
            ["Value2"] = "2",
            ["Value1"] = "1",
            ["Value3"] = "3"
        };
        var spec = new UiElementSpec("parse-n", DropdownWidget.Kind, attributes);

        IReadOnlyList<DropdownWidget.DropdownOption> options = DropdownWidget.ParseOptions(spec);

        Check(options.Count == 3, "OptionN attributes are collected");
        Check(options.Count == 3 && options[0].Text == "First" && options[1].Text == "Second" && options[2].Text == "Third",
            "OptionN attributes are sorted by numeric suffix");
        Check(options.Count == 3 && options[0].Value == "1" && options[2].Value == "3",
            "ValueN attributes pair with OptionN attributes");
    }

    private static void VerifySelectionEmitsCommand()
    {
        ResetDebug();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Options"] = "Alpha, Beta, Gamma",
            ["Values"] = "a,b,c",
            ["EmitName"] = "Picked"
        };
        var spec = new UiElementSpec("dropdown-select", DropdownWidget.Kind, attributes);
        var widget = new DropdownWidget();
        widget.Configure(spec);

        var commands = new List<UiCommand>();
        var ctx = new WidgetContext("test", null, new StubMetrics(), new UiPageState());
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("dropdown-select", "dropdown"));
        state.Open = false;
        state.StringValue = null;

        UiInteract.BeginFrame();
        SetMouse(100f, 14f);
        UiInteract.DebugClick = true;
        widget.Draw(new Rect(0f, 0f, 200f, 28f), ctx, commands.Add);
        UiInteract.ProcessEvents();
        UiInteract.EndFrame();

        Check(state.Open, "clicking the dropdown field opens the option list");

        UiInteract.BeginFrame();
        // Option rows start at rect.yMax; second option is at 28 + 24 = 52..76.
        SetMouse(100f, 52f + 12f);
        UiInteract.DebugClick = true;
        widget.Draw(new Rect(0f, 0f, 200f, 28f), ctx, commands.Add);
        UiInteract.ProcessEvents();
        UiInteract.EndFrame();

        Check(!state.Open, "selecting an option closes the dropdown");
        Check(commands.Count == 1, "selecting an option emits exactly one command");
        Check(commands.Count == 1 && commands[0].Name == "Picked"
            && commands[0].Payload is string payload && payload == "b",
            "dropdown command carries the selected submitted value");
    }

    private static void VerifyDynamicOptionsFromViewState()
    {
        ResetDebug();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bind"] = "Current",
            ["OptionsBind"] = "Options",
            ["EmitName"] = "Picked"
        };
        var spec = new UiElementSpec("dropdown-dynamic", DropdownWidget.Kind, attributes);
        var widget = new DropdownWidget();
        widget.Configure(spec);

        var options = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Alpha", "a"),
            new KeyValuePair<string, string>("Beta", "b")
        };
        var view = new Dictionary<string, object?>
        {
            ["Current"] = "a",
            ["Options"] = options
        };
        var ctx = new WidgetContext("test", view, new StubMetrics(), new UiPageState());
        var commands = new List<UiCommand>();
        UiValueState state = UiValueStore.GetOrCreate(new UiControlId("dropdown-dynamic", "dropdown"));
        state.Open = false;
        state.StringValue = null;

        UiInteract.BeginFrame();
        SetMouse(100f, 14f);
        UiInteract.DebugClick = true;
        widget.Draw(new Rect(0f, 0f, 200f, 28f), ctx, commands.Add);
        UiInteract.ProcessEvents();
        UiInteract.EndFrame();

        Check(state.Open, "dynamic dropdown opens from OptionsBind view state");

        UiInteract.BeginFrame();
        SetMouse(100f, 52f + 12f);
        UiInteract.DebugClick = true;
        widget.Draw(new Rect(0f, 0f, 200f, 28f), ctx, commands.Add);
        UiInteract.ProcessEvents();
        UiInteract.EndFrame();

        Check(!state.Open, "dynamic dropdown selection closes");
        Check(commands.Count == 1 && commands[0].Name == "Picked"
            && commands[0].Payload is string payload && payload == "b",
            "dynamic dropdown emits selected value");
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
