using System;
using FerriteLib.UiKit;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>B2: value control ids, state store, Slider and NumberField primitives.</summary>
internal static class ValueControlTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyControlIdEquality();
        VerifyValueStoreIdentity();
        VerifySliderDragUpdatesState();
        VerifyNumberFieldLegalInput();
        VerifyNumberFieldIllegalInput();
        VerifyNumberFieldClamp();
        VerifyNumberFieldFocusLossFallback();
        VerifyNumberFieldUsesPassedValueWhenUnfocused();
        VerifyResetFrame();
        return failures;
    }

    private static void VerifyControlIdEquality()
    {
        var a = new UiControlId("scope", "name");
        var b = new UiControlId("scope", "name");
        var c = new UiControlId("scope", "other");
        var d = new UiControlId("other", "name");

        Check(a.Equals(b), "same scope/name are equal");
        Check(a == b, "operator == for equal ids");
        Check(a != c, "operator != for different ids");
        Check(a.GetHashCode() == b.GetHashCode(), "equal ids have equal hash codes");
        Check(!a.Equals(c) && !a.Equals(d), "different ids are not equal");
    }

    private static void VerifyValueStoreIdentity()
    {
        var id = new UiControlId("store", "one");
        var other = new UiControlId("store", "two");

        UiValueState first = UiValueStore.GetOrCreate(id);
        UiValueState second = UiValueStore.GetOrCreate(id);
        UiValueState different = UiValueStore.GetOrCreate(other);

        Check(ReferenceEquals(first, second), "same id returns same instance");
        Check(!ReferenceEquals(first, different), "different id returns different instance");
    }

    private static void VerifySliderDragUpdatesState()
    {
        ResetDebug();
        var id = new UiControlId("slider", "drag");
        UiValueState state = UiValueStore.GetOrCreate(id);
        Rect rect = new(0f, 0f, 200f, 20f);
        SetMouse(100f, 10f);

        UiInteract.BeginFrame();
        UiInteract.SliderOverride = (_, _, _, _) => 55f;
        UiInteract.DebugMouseDown = true;
        float first = UiInteract.Slider(rect, id, 50f, 0f, 100f, out bool changedFirst);
        Check(Math.Abs(first - 55f) < 0.0001f && changedFirst, "slider mouse down returns dragged value and changed");
        Check(Math.Abs(state.FloatValue - 55f) < 0.0001f, "slider mouse down updates FloatValue");
        Check(state.EditText == "55", "slider updates EditText");
        Check(state.Dragging, "slider mouse down marks Dragging");

        UiInteract.DebugMouseDown = false;
        UiInteract.DebugMouseDrag = true;
        UiInteract.SliderOverride = (_, _, _, _) => 65f;
        UiInteract.Slider(rect, id, 55f, 0f, 100f, out _);
        Check(Math.Abs(state.FloatValue - 65f) < 0.0001f && state.EditText == "65", "slider drag updates value and text");
        Check(state.Dragging, "slider drag keeps Dragging");

        UiInteract.DebugMouseDrag = false;
        UiInteract.DebugMouseUp = true;
        UiInteract.SliderOverride = (_, _, _, _) => 65f;
        UiInteract.Slider(rect, id, 65f, 0f, 100f, out bool changedUp);
        Check(!changedUp, "slider mouse up with same value reports no change");
        Check(!state.Dragging, "slider mouse up clears Dragging");
        UiInteract.EndFrame();
    }

    private static void VerifyNumberFieldLegalInput()
    {
        ResetDebug();
        var id = new UiControlId("field", "legal");
        UiValueState state = UiValueStore.GetOrCreate(id);
        state.FloatValue = 50f;
        state.EditText = "50";
        state.Focused = true;
        Rect rect = new(0f, 0f, 80f, 20f);
        SetMouse(10f, 10f);

        UiInteract.BeginFrame();
        UiInteract.TextFieldOverride = (_, _) => "65";
        string display = UiInteract.NumberField(rect, id, 50f, 0f, 100f, "0.##", out bool committed);
        Check(committed, "legal number field input commits");
        Check(Math.Abs(state.FloatValue - 65f) < 0.0001f, "legal input updates FloatValue");
        Check(state.EditText == "65", "legal input keeps EditText");
        Check(display == "65", "focused legal input displays EditText");
        UiInteract.EndFrame();
    }

    private static void VerifyNumberFieldIllegalInput()
    {
        ResetDebug();
        var id = new UiControlId("field", "illegal");
        UiValueState state = UiValueStore.GetOrCreate(id);
        state.FloatValue = 50f;
        state.EditText = "50";
        state.Focused = true;
        Rect rect = new(0f, 0f, 80f, 20f);
        SetMouse(10f, 10f);

        UiInteract.BeginFrame();
        UiInteract.TextFieldOverride = (_, _) => "abc";
        string display = UiInteract.NumberField(rect, id, 50f, 0f, 100f, "0.##", out bool committed);
        Check(!committed, "illegal number field input does not commit");
        Check(Math.Abs(state.FloatValue - 50f) < 0.0001f, "illegal input leaves FloatValue unchanged");
        Check(state.EditText == "abc", "illegal input keeps edit text");
        Check(display == "abc", "focused illegal input displays raw text");
        UiInteract.EndFrame();
    }

    private static void VerifyNumberFieldClamp()
    {
        ResetDebug();
        var id = new UiControlId("field", "clamp");
        UiValueState state = UiValueStore.GetOrCreate(id);
        state.FloatValue = 50f;
        state.EditText = "50";
        state.Focused = true;
        Rect rect = new(0f, 0f, 80f, 20f);
        SetMouse(10f, 10f);

        UiInteract.BeginFrame();
        UiInteract.TextFieldOverride = (_, _) => "150";
        UiInteract.NumberField(rect, id, 50f, 0f, 100f, "0.##", out bool committed);
        Check(committed, "clamped legal input commits");
        Check(Math.Abs(state.FloatValue - 100f) < 0.0001f, "input above max clamps to max");
        UiInteract.EndFrame();
    }

    private static void VerifyNumberFieldFocusLossFallback()
    {
        ResetDebug();
        var id = new UiControlId("field", "focus");
        UiValueState state = UiValueStore.GetOrCreate(id);
        state.FloatValue = 50f;
        state.EditText = "abc";
        state.Focused = true;
        Rect rect = new(0f, 0f, 80f, 20f);
        SetMouse(10f, 10f);

        UiInteract.BeginFrame();
        UiInteract.TextFieldOverride = (_, _) => "abc";
        UiInteract.DebugFocusLost = true;
        string display = UiInteract.NumberField(rect, id, 50f, 0f, 100f, "0.##", out _);
        Check(!state.Focused, "focus loss clears Focused");
        Check(state.EditText == "50", "invalid text reverts to formatted value on focus loss");
        Check(display == "50", "unfocused field displays formatted value");
        UiInteract.EndFrame();
    }

    private static void VerifyNumberFieldUsesPassedValueWhenUnfocused()
    {
        ResetDebug();
        var id = new UiControlId("field", "fresh");
        UiValueState state = UiValueStore.GetOrCreate(id);
        Rect rect = new(0f, 0f, 80f, 20f);
        SetMouse(10f, 10f);

        UiInteract.BeginFrame();
        UiInteract.TextFieldOverride = (_, text) => text;
        string display = UiInteract.NumberField(rect, id, 50f, 0f, 100f, "0.##", out _);
        Check(Math.Abs(state.FloatValue - 50f) < 0.0001f, "unfocused NumberField adopts passed value");
        Check(display == "50", "unfocused NumberField displays passed value");
        UiInteract.EndFrame();
    }

    private static void VerifyResetFrame()
    {
        var id = new UiControlId("store", "reset");
        UiValueState state = UiValueStore.GetOrCreate(id);
        state.FloatValue = 42f;
        state.EditText = "42";
        state.Dragging = true;
        state.Focused = true;
        state.Cursor = 3;

        UiValueStore.ResetFrame();

        Check(!state.Dragging && !state.Focused && state.Cursor == 0,
            "ResetFrame clears transient frame state");
        Check(Math.Abs(state.FloatValue - 42f) < 0.0001f && state.EditText == "42",
            "ResetFrame preserves committed value state");
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
}
