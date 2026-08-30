using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// Self-drawn dropdown/select control. Clicking the field opens an option list; selecting an
/// option emits a neutral command named by <c>EmitName</c> (default "ValueChanged").
/// Options are parsed from the <c>Options</c> attribute (comma/semicolon separated) or from
/// individual <c>Option1..OptionN</c> attributes. Optional <c>Values</c> supplies submitted values.
/// </summary>
public sealed class DropdownWidget : IWidget
{
    public const string Kind = "input/dropdown";

    private const string LabelAttribute = "Label";
    private const string BindAttribute = "Bind";
    private const string CurrentAttribute = "Current";
    private const string OptionsAttribute = "Options";
    private const string OptionsBindAttribute = "OptionsBind";
    private const string ValuesAttribute = "Values";
    private const string EmitNameAttribute = "EmitName";
    private const string HeightAttribute = "Height";
    private const string DefaultEmitName = "ValueChanged";
    private const float DefaultHeight = 28f;
    private const float LabelWidth = 80f;
    private const float OptionHeight = 24f;
    private const float TextPadding = 6f;

    internal readonly struct DropdownOption
    {
        internal readonly string Text;
        internal readonly string Value;

        internal DropdownOption(string text, string value)
        {
            Text = text;
            Value = value;
        }
    }

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
        string emitName = Read(EmitNameAttribute);
        if (emitName.Length == 0) emitName = DefaultEmitName;

        IReadOnlyList<DropdownOption> options = BuildOptions(_spec, ctx);
        if (options.Count == 0) return;

        string scope = _spec.Id.Length > 0 ? _spec.Id : Kind;
        var id = new UiControlId(scope, "dropdown");
        UiValueState state = UiValueStore.GetOrCreate(id);

        string current = ResolveCurrent(ctx, state);
        string display = FindDisplayText(options, current);

        float labelWidth = label.Length > 0 ? LabelWidth : 0f;
        float fieldWidth = Math.Max(1f, rect.width - labelWidth);
        Rect labelRect = new(rect.x, rect.y, labelWidth, rect.height);
        Rect fieldRect = new(rect.x + labelWidth, rect.y, fieldWidth, rect.height);

        if (labelWidth > 0f)
        {
            UiKitGui.Label(labelRect, label, UiFont.Small, TextAnchor.MiddleLeft, Palette.TextPrimary);
        }

        DrawField(fieldRect, display, state.Open);

        int currentIndex = FindOptionIndex(options, current);
        if (currentIndex >= 0)
        {
            DrawOptionText(fieldRect, options[currentIndex].Text);
        }
        else if (display.Length > 0)
        {
            DrawOptionText(fieldRect, display);
        }

        UiInteract.Button(fieldRect, UiLayer.Content, () => state.Open = !state.Open);

        if (state.Open)
        {
            UiInteract.Button(new Rect(-100000f, -100000f, 200000f, 200000f), UiLayer.Background,
                () => state.Open = false);

            float listTop = rect.yMax;
            var listRect = new Rect(rect.x, listTop, rect.width, options.Count * OptionHeight);
            SurfaceFrame.Draw(listRect, SurfaceFrame.SurfaceKind.Panel);

            for (int i = 0; i < options.Count; i++)
            {
                Rect rowRect = new(rect.x, listTop + i * OptionHeight, rect.width, OptionHeight);
                bool selected = string.Equals(options[i].Value, current, StringComparison.Ordinal);

                if (selected)
                {
                    VerseWidgets.DrawBoxSolid(rowRect, Palette.Selected);
                }
                else
                {
                    VerseWidgets.DrawBoxSolid(rowRect, Palette.Raised);
                }

                SurfaceFrame.DrawBorder(rowRect);
                UiKitGui.Label(
                    new Rect(rowRect.x + TextPadding, rowRect.y, rowRect.width - TextPadding * 2f, rowRect.height),
                    options[i].Text,
                    UiFont.Small,
                    TextAnchor.MiddleLeft,
                    selected ? Palette.AccentGold : Palette.TextPrimary);

                int captured = i;
                UiInteract.Button(rowRect, UiLayer.TopAction, () =>
                {
                    state.Open = false;
                    state.StringValue = options[captured].Value;
                    emit(new UiCommand(emitName, options[captured].Value));
                });
            }
        }
    }

    private static IReadOnlyList<DropdownOption> BuildOptions(UiElementSpec spec, WidgetContext ctx)
    {
        if (spec.TryGetAttribute(OptionsBindAttribute, out string key)
            && key.Length > 0
            && ctx.TryGetViewValue(key, out object? value)
            && value != null)
        {
            var dynamic = new List<DropdownOption>();
            switch (value)
            {
                case IEnumerable<KeyValuePair<string, string>> pairs:
                    foreach (KeyValuePair<string, string> pair in pairs)
                        dynamic.Add(new DropdownOption(pair.Key, pair.Value));
                    break;
                case IEnumerable<string> strings:
                    foreach (string item in strings)
                        dynamic.Add(new DropdownOption(item, item));
                    break;
                case IEnumerable<object> objects:
                    foreach (object item in objects)
                    {
                        if (item is string text)
                            dynamic.Add(new DropdownOption(text, text));
                        else if (item is KeyValuePair<string, string> pair)
                            dynamic.Add(new DropdownOption(pair.Key, pair.Value));
                    }
                    break;
            }

            if (dynamic.Count > 0) return dynamic;
        }

        return ParseOptions(spec);
    }

    internal static IReadOnlyList<DropdownOption> ParseOptions(UiElementSpec spec)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        var options = new List<string>();
        if (spec.TryGetAttribute(OptionsAttribute, out string optionsRaw) && optionsRaw.Trim().Length > 0)
        {
            options.AddRange(SplitList(optionsRaw));
        }
        else
        {
            var indexed = new List<KeyValuePair<int, string>>();
            foreach (KeyValuePair<string, string> pair in spec.Attributes)
            {
                if (!pair.Key.StartsWith("Option", StringComparison.Ordinal) || pair.Key.Length <= "Option".Length)
                    continue;

                if (int.TryParse(pair.Key.Substring("Option".Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out int index))
                {
                    indexed.Add(new KeyValuePair<int, string>(index, pair.Value));
                }
            }

            indexed.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (KeyValuePair<int, string> pair in indexed)
                options.Add(pair.Value);
        }

        var values = new List<string>();
        if (spec.TryGetAttribute(ValuesAttribute, out string valuesRaw) && valuesRaw.Trim().Length > 0)
        {
            values.AddRange(SplitList(valuesRaw));
        }
        else if (options.Count > 0 && !HasOptionsAttribute(spec))
        {
            var indexedValues = new List<KeyValuePair<int, string>>();
            foreach (KeyValuePair<string, string> pair in spec.Attributes)
            {
                if (!pair.Key.StartsWith("Value", StringComparison.Ordinal) || pair.Key.Length <= "Value".Length)
                    continue;

                if (int.TryParse(pair.Key.Substring("Value".Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out int index))
                {
                    indexedValues.Add(new KeyValuePair<int, string>(index, pair.Value));
                }
            }

            indexedValues.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (KeyValuePair<int, string> pair in indexedValues)
                values.Add(pair.Value);
        }

        if (values.Count == 0)
        {
            values.AddRange(options);
        }
        else if (values.Count != options.Count)
        {
            throw new FormatException(
                $"Dropdown id=\"{spec.Id}\" has {options.Count} option(s) but {values.Count} value(s); Options and Values must match.");
        }

        var result = new List<DropdownOption>(options.Count);
        for (int i = 0; i < options.Count; i++)
            result.Add(new DropdownOption(options[i], values[i]));
        return result;
    }

    private static bool HasOptionsAttribute(UiElementSpec spec)
    {
        return spec.TryGetAttribute(OptionsAttribute, out string raw) && raw.Trim().Length > 0;
    }

    private static List<string> SplitList(string raw)
    {
        var result = new List<string>();
        string[] parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.None);
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0) result.Add(trimmed);
        }

        return result;
    }

    private static string FindDisplayText(IReadOnlyList<DropdownOption> options, string current)
    {
        foreach (DropdownOption option in options)
        {
            if (string.Equals(option.Value, current, StringComparison.Ordinal)
                || string.Equals(option.Text, current, StringComparison.Ordinal))
            {
                return option.Text;
            }
        }

        return current;
    }

    private static int FindOptionIndex(IReadOnlyList<DropdownOption> options, string current)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, current, StringComparison.Ordinal)
                || string.Equals(options[i].Text, current, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private string ResolveCurrent(WidgetContext ctx, UiValueState state)
    {
        if (_spec.TryGetAttribute(BindAttribute, out string key)
            && key.Length > 0
            && ctx.TryGetViewValue(key, out object? value)
            && value != null)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        if (state.StringValue != null) return state.StringValue;

        return _spec.TryGetAttribute(CurrentAttribute, out string literal) ? literal : "";
    }

    private static void DrawField(Rect rect, string display, bool open)
    {
        SurfaceFrame.Draw(rect, open ? SurfaceFrame.SurfaceKind.Selected : SurfaceFrame.SurfaceKind.Raised);

        // Draw a simple neutral caret in the right edge.
        float caretX = rect.xMax - 12f;
        float caretY = rect.y + rect.height * 0.5f;
        VerseWidgets.DrawBoxSolid(new Rect(caretX, caretY - 1f, 8f, 1f), Palette.TextSecondary);
        VerseWidgets.DrawBoxSolid(new Rect(caretX, caretY, 8f, 1f), Palette.TextSecondary);
        VerseWidgets.DrawBoxSolid(new Rect(caretX, caretY + 1f, 8f, 1f), Palette.TextSecondary);

        if (display.Length == 0) return;
        DrawOptionText(rect, display);
    }

    private static void DrawOptionText(Rect rect, string text)
    {
        UiKitGui.Label(
            new Rect(rect.x + TextPadding, rect.y, rect.width - TextPadding * 2f, rect.height),
            text,
            UiFont.Small,
            TextAnchor.MiddleLeft,
            Palette.TextPrimary);
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
}
