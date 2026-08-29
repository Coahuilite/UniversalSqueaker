using System;
using System.Globalization;
using UnityEngine;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// One selectable mode card. Attributes: Title, Description, Current (literal value),
/// Bind (optional view key for Current), Target (value emitted on click), and EmitName
/// (command name, default "SelectMode").
/// </summary>
public sealed class InputModeCardWidget : IWidget
{
    public const string Kind = "input/mode-card";

    private const string TitleAttribute = "Title";
    private const string DescriptionAttribute = "Description";
    private const string CurrentAttribute = "Current";
    private const string BindAttribute = "Bind";
    private const string TargetAttribute = "Target";
    private const string EmitNameAttribute = "EmitName";
    private const string DefaultEmitName = "SelectMode";

    private UiElementSpec _spec = UiElementSpec.Empty;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        string title = Read(TitleAttribute);
        string description = Read(DescriptionAttribute);
        float textWidth = Math.Max(1f, ctx.ViewWidth - 20f);
        float titleHeight = ctx.Metrics.MeasureText(title, UiFont.Small, textWidth);
        float descHeight = ctx.Metrics.MeasureText(description, UiFont.Tiny, textWidth);
        return Math.Max(64f, 7f + Math.Max(24f, titleHeight) + 6f + descHeight + 8f);
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        FerriteGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind);
    }

    private void DrawCore(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        string title = Read(TitleAttribute);
        string description = Read(DescriptionAttribute);
        string current = ResolveCurrent(ctx);
        string target = Read(TargetAttribute);
        string emitName = ReadEmitName();

        bool selected = string.Equals(current, target, StringComparison.Ordinal);
        ModeCardRenderer.Draw(rect, selected, title, description, () => emit(new UiCommand(emitName, target)));
    }

    private void DrawVanilla(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        string title = Read(TitleAttribute);
        string current = ResolveCurrent(ctx);
        string target = Read(TargetAttribute);
        string emitName = ReadEmitName();
        bool selected = string.Equals(current, target, StringComparison.Ordinal);
        if (VerseWidgets.ButtonText(rect, (selected ? "● " : "") + title))
        {
            emit(new UiCommand(emitName, target));
        }
    }

    private string ResolveCurrent(WidgetContext ctx)
    {
        if (_spec.TryGetAttribute(BindAttribute, out string key)
            && key.Length > 0
            && ctx.TryGetViewValue(key, out object? value)
            && value != null)
        {
            return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        return Read(CurrentAttribute);
    }

    private string Read(string name)
    {
        return _spec.TryGetAttribute(name, out string value) ? value : "";
    }

    private string ReadEmitName()
    {
        string emitName = Read(EmitNameAttribute);
        return emitName.Length > 0 ? emitName : DefaultEmitName;
    }
}
