using System;
using System.Globalization;
using UnityEngine;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// Composite stepper slider: optional label, a − button, a horizontal slider, a numeric text
/// field, and a + button. Slider and field share the same <see cref="UiValueState"/>; −/+ step by
/// <c>Step</c>, clamp to Min/Max, and emit a neutral command named by <c>EmitName</c>.
/// </summary>
public sealed class StepperSliderWidget : IWidget
{
    public const string Kind = "input/stepper-slider";

    private const string LabelAttribute = "Label";
    private const string BindAttribute = "Bind";
    private const string CurrentAttribute = "Current";
    private const string MinAttribute = "Min";
    private const string MaxAttribute = "Max";
    private const string StepAttribute = "Step";
    private const string FormatAttribute = "Format";
    private const string EmitNameAttribute = "EmitName";
    private const string LiveAttribute = "Live";
    private const string HeightAttribute = "Height";
    private const string DefaultEmitName = "ValueChanged";
    private const string DefaultFormat = "0.##";
    private const float DefaultHeight = 28f;
    private const float LabelWidth = 80f;
    private const float ButtonWidth = 24f;
    private const float FieldWidth = 64f;
    private const float Gap = 6f;

    private UiElementSpec _spec = UiElementSpec.Empty;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        return ReadHeight();
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string label = Read(LabelAttribute);
        float min = ReadFloat(MinAttribute, 0f);
        float max = ReadFloat(MaxAttribute, 1f);
        float step = ReadFloat(StepAttribute, 1f);
        if (step <= 0f) step = 1f;
        string format = Read(FormatAttribute);
        if (format.Length == 0) format = DefaultFormat;
        string emitName = Read(EmitNameAttribute);
        if (emitName.Length == 0) emitName = DefaultEmitName;
        bool live = ReadBool(LiveAttribute, true);

        string scope = _spec.Id.Length > 0 ? _spec.Id : Kind;
        var id = new UiControlId(scope, "value");
        UiValueState state = UiValueStore.GetOrCreate(id);

        float current = ResolveCurrent(ctx, state, min, max);
        state.FloatValue = current;
        if (!state.Focused)
            state.EditText = UiInteract.FormatValue(current, format);

        float x = rect.x;
        float y = rect.y;
        float height = rect.height;

        if (label.Length > 0)
        {
            UiKitGui.Label(new Rect(x, y, LabelWidth, height), label, UiFont.Small, TextAnchor.MiddleLeft, Palette.TextPrimary);
            x += LabelWidth + Gap;
        }

        Rect minusRect = new(x, y, ButtonWidth, height);
        x += ButtonWidth + Gap;

        float remainingWidth = Math.Max(1f, rect.xMax - x - FieldWidth - Gap * 2f - ButtonWidth);
        Rect sliderRect = new(x, y, remainingWidth, height);
        x += remainingWidth + Gap;

        Rect fieldRect = new(x, y, FieldWidth, height);
        x += FieldWidth + Gap;

        Rect plusRect = new(x, y, ButtonWidth, height);

        DrawButton(minusRect, "−");
        DrawButton(plusRect, "+");

        float sliderValue = UiInteract.Slider(sliderRect, id, current, min, max, out bool sliderChanged);
        bool emittedSlider = false;
        if (sliderChanged)
        {
            state.EditText = UiInteract.FormatValue(sliderValue, format);
            emit(new UiCommand(emitName, sliderValue));
            emittedSlider = true;
        }

        UiInteract.NumberField(fieldRect, id, state.FloatValue, min, max, format, out bool committed);
        if (committed && !emittedSlider && (live || !state.Focused))
        {
            emit(new UiCommand(emitName, state.FloatValue));
        }

        UiInteract.Button(minusRect, UiLayer.TopAction, () =>
        {
            state.FloatValue = UiInteract.ClampValue(state.FloatValue - step, min, max);
            state.EditText = UiInteract.FormatValue(state.FloatValue, format);
            emit(new UiCommand(emitName, state.FloatValue));
        });

        UiInteract.Button(plusRect, UiLayer.TopAction, () =>
        {
            state.FloatValue = UiInteract.ClampValue(state.FloatValue + step, min, max);
            state.EditText = UiInteract.FormatValue(state.FloatValue, format);
            emit(new UiCommand(emitName, state.FloatValue));
        });
    }

    private float ResolveCurrent(WidgetContext ctx, UiValueState state, float min, float max)
    {
        if (_spec.TryGetAttribute(BindAttribute, out string key)
            && key.Length > 0
            && ctx.TryGetViewValue(key, out object? value)
            && TryConvertToFloat(value, out float bound))
        {
            return UiInteract.ClampValue(bound, min, max);
        }

        if (_spec.TryGetAttribute(CurrentAttribute, out string literal)
            && literal.Trim().Length > 0
            && !state.Focused
            && float.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out float current))
        {
            return UiInteract.ClampValue(current, min, max);
        }

        return state.FloatValue;
    }

    private static bool TryConvertToFloat(object? value, out float result)
    {
        switch (value)
        {
            case float f:
                result = f;
                return true;
            case double d:
                result = (float)d;
                return true;
            case int i:
                result = i;
                return true;
            case string s:
                return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            default:
                result = 0f;
                return false;
        }
    }

    private static void DrawButton(Rect rect, string text)
    {
        SurfaceFrame.Draw(rect, SurfaceFrame.SurfaceKind.Raised);
        UiKitGui.Label(rect, text, UiFont.Small, TextAnchor.MiddleCenter, Palette.TextPrimary);
    }

    private float ReadHeight()
    {
        if (_spec.TryGetAttribute(HeightAttribute, out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }

    private string Read(string name)
    {
        return _spec.TryGetAttribute(name, out string value) ? value : "";
    }

    private float ReadFloat(string name, float fallback)
    {
        return _spec.TryGetAttribute(name, out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    private bool ReadBool(string name, bool fallback)
    {
        if (!_spec.TryGetAttribute(name, out string raw)) return fallback;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.Ordinal);
    }
}
