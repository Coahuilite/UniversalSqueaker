using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit;

/// <summary>
/// Per-frame interaction registry and deferred input dispatcher.
///
/// Widgets register their interactive rects during Draw; <see cref="ProcessEvents"/> runs after
/// all widgets have drawn and resolves the single highest-priority target. Native controls that
/// handle their own input (sliders, text fields, help buttons) should call <see cref="Protect"/>
/// first so low-priority row buttons cannot steal their events.
/// </summary>
public static class UiInteract
{
    private sealed class RegisteredButton
    {
        internal Rect Rect;
        internal UiLayer Layer;
        internal Action Callback = null!;
    }

    private static readonly List<Rect> ProtectedRects = new();
    private static readonly List<RegisteredButton> Buttons = new();
    private static bool frameActive;

    // Test seams. They are internal and only used by the FerriteLib.UiKit.Tests assembly.
    internal static bool DebugMousePositionEnabled;
    internal static Vector2 DebugMousePosition;
    internal static bool DebugClick;
    internal static bool DebugMouseDown;
    internal static bool DebugMouseDrag;
    internal static bool DebugMouseUp;
    internal static bool DebugEnter;
    internal static bool DebugFocusLost;
    internal static Func<Rect, float, float, float, float>? SliderOverride;
    internal static Func<Rect, string, string>? TextFieldOverride;

    /// <summary>Starts a new interaction frame and clears the previous frame's registrations.</summary>
    public static void BeginFrame()
    {
        ProtectedRects.Clear();
        Buttons.Clear();
        frameActive = true;
    }

    /// <summary>Ends the current frame and clears all registrations. Safe to call multiple times.</summary>
    public static void EndFrame()
    {
        ProtectedRects.Clear();
        Buttons.Clear();
        frameActive = false;
    }

    /// <summary>
    /// Registers a rect whose native input handling must be preserved. While the mouse is inside any
    /// protected rect, <see cref="ProcessEvents"/> will not trigger registered buttons and will not
    /// consume the event.
    /// </summary>
    public static void Protect(Rect rect)
    {
        if (!frameActive) return;
        ProtectedRects.Add(rect);
    }

    /// <summary>Registers a click target. The callback is invoked later by <see cref="ProcessEvents"/>.</summary>
    public static void Button(Rect rect, UiLayer layer, Action callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        if (!frameActive) return;
        Buttons.Add(new RegisteredButton { Rect = rect, Layer = layer, Callback = callback });
    }

    /// <summary>Registers a large content row button. Equivalent to <see cref="Button"/> at Content layer.</summary>
    public static void Row(Rect rect, Action callback)
    {
        Button(rect, UiLayer.Content, callback);
    }

    /// <summary>
    /// Dispatches the current frame's input. Protected rects win; otherwise the highest layer and,
    /// within that layer, the most recently registered button that contains the mouse is invoked.
    /// </summary>
    public static void ProcessEvents()
    {
        if (!frameActive) return;

        if (IsMouseOverAnyProtected()) return;

        if (!IsClickEvent()) return;

        RegisteredButton? target = FindTarget();
        if (target == null) return;

        target.Callback();
        ConsumeEvent();
    }

    /// <summary>
    /// Slider primitive. Registers the rect as protected, reads the native slider value, writes it
    /// into the shared <see cref="UiValueState"/>, and reports whether the value changed.
    /// </summary>
    public static float Slider(Rect rect, UiControlId id, float value, float min, float max, out bool changed)
    {
        Protect(rect);
        UiValueState state = UiValueStore.GetOrCreate(id);
        float newValue = NativeHorizontalSlider(rect, value, min, max);
        newValue = ClampValue(newValue, min, max);
        changed = Math.Abs(newValue - value) > 0.0001f;
        state.FloatValue = newValue;
        if (changed || !state.Focused)
            state.EditText = FormatValue(newValue, "0.##");
        if (IsMouseDownOver(rect) || IsMouseDragOver(rect)) state.Dragging = true;
        if (IsMouseUpOver(rect)) state.Dragging = false;
        return newValue;
    }

    /// <summary>
    /// Number field primitive. Registers the rect as protected, reads native text input, keeps the
    /// user's edit text while focused, and commits valid clamped numbers into the shared value state.
    /// </summary>
    public static string NumberField(
        Rect rect,
        UiControlId id,
        float value,
        float min,
        float max,
        string format,
        out bool committed)
    {
        Protect(rect);
        UiValueState state = UiValueStore.GetOrCreate(id);

        if (!state.Focused)
        {
            state.FloatValue = value;
            state.EditText = FormatValue(value, format);
        }

        if (IsMouseDownOver(rect))
            state.Focused = true;

        string displayText = state.Focused ? state.EditText : FormatValue(state.FloatValue, format);
        string text = NativeTextField(rect, displayText);
        float previousValue = state.FloatValue;
        committed = false;

        bool parsed = TryParseNumber(text, out float parsedValue);
        if (parsed)
        {
            parsedValue = ClampValue(parsedValue, min, max);
            bool textChanged = !string.Equals(text, displayText, StringComparison.Ordinal);
            bool valueChanged = Math.Abs(parsedValue - previousValue) > 0.0001f;
            state.FloatValue = parsedValue;
            state.EditText = text;
            committed = textChanged || valueChanged;
        }
        else
        {
            state.EditText = text;
        }

        if (state.Focused && (IsEnterPressed() || IsFocusLost(rect)))
        {
            if (!parsed)
            {
                state.EditText = FormatValue(state.FloatValue, format);
            }
            else
            {
                state.EditText = FormatValue(state.FloatValue, format);
            }

            state.Focused = false;
        }

        return state.Focused ? state.EditText : FormatValue(state.FloatValue, format);
    }

    internal static bool TryParseNumber(string text, out float value)
    {
        if (text != null && text.Trim().Length > 0
            && float.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0f;
        return false;
    }

    internal static float ClampValue(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return min;
        if (max < min) return min;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    internal static string FormatValue(float value, string format)
    {
        if (string.IsNullOrEmpty(format)) format = "0.##";
        return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static RegisteredButton? FindTarget()
    {
        RegisteredButton? best = null;
        int bestIndex = -1;
        for (int i = 0; i < Buttons.Count; i++)
        {
            RegisteredButton candidate = Buttons[i];
            if (!IsMouseOver(candidate.Rect)) continue;

            if (best == null
                || candidate.Layer > best.Layer
                || (candidate.Layer == best.Layer && i > bestIndex))
            {
                best = candidate;
                bestIndex = i;
            }
        }

        return best;
    }

    private static bool IsMouseOverAnyProtected()
    {
        foreach (Rect rect in ProtectedRects)
        {
            if (IsMouseOver(rect)) return true;
        }

        return false;
    }

    private static bool IsMouseOver(Rect rect)
    {
        if (DebugMousePositionEnabled)
        {
            return DebugMousePosition.x >= rect.x
                && DebugMousePosition.x <= rect.xMax
                && DebugMousePosition.y >= rect.y
                && DebugMousePosition.y <= rect.yMax;
        }

        return Mouse.IsOver(rect);
    }

    private static bool IsMouseDownOver(Rect rect)
    {
        if (DebugMousePositionEnabled)
        {
            return DebugMouseDown && IsMouseOver(rect);
        }

        Event? current = Event.current;
        return current != null
            && current.type == EventType.MouseDown
            && current.button == 0
            && Mouse.IsOver(rect);
    }

    private static bool IsMouseDragOver(Rect rect)
    {
        if (DebugMousePositionEnabled)
        {
            return DebugMouseDrag && IsMouseOver(rect);
        }

        Event? current = Event.current;
        return current != null
            && current.type == EventType.MouseDrag
            && current.button == 0
            && Mouse.IsOver(rect);
    }

    private static bool IsMouseUpOver(Rect rect)
    {
        if (DebugMousePositionEnabled)
        {
            return DebugMouseUp && IsMouseOver(rect);
        }

        Event? current = Event.current;
        return current != null
            && current.type == EventType.MouseUp
            && current.button == 0
            && Mouse.IsOver(rect);
    }

    private static bool IsClickEvent()
    {
        if (DebugClick) return true;

        Event? current = Event.current;
        return current != null
            && current.type == EventType.MouseUp
            && current.button == 0;
    }

    private static bool IsEnterPressed()
    {
        if (DebugEnter) return true;

        Event? current = Event.current;
        return current != null
            && current.type == EventType.KeyDown
            && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter);
    }

    private static bool IsFocusLost(Rect rect)
    {
        if (DebugFocusLost) return true;

        Event? current = Event.current;
        return current != null
            && current.type == EventType.MouseDown
            && current.button == 0
            && !Mouse.IsOver(rect);
    }

    private static void ConsumeEvent()
    {
        if (DebugClick)
        {
            DebugClick = false;
            return;
        }

        Event.current?.Use();
    }

    private static float NativeHorizontalSlider(Rect rect, float value, float min, float max)
    {
        if (SliderOverride != null) return SliderOverride(rect, value, min, max);
        return Verse.Widgets.HorizontalSlider(rect, value, min, max, middleAlignment: true);
    }

    private static string NativeTextField(Rect rect, string text)
    {
        if (TextFieldOverride != null) return TextFieldOverride(rect, text);
        return Verse.Widgets.TextField(rect, text) ?? "";
    }
}
