using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// Horizontal row of two or three selectable mode cards. Attributes: Title1..Title3,
/// Description1..Description3, Value1..Value3 (card targets), Current (literal value),
/// Bind (optional view key for Current), and EmitName (command name, default "SelectMode").
/// </summary>
public sealed class InputModeRowWidget : IWidget
{
    public const string Kind = "input/mode-row";

    private const string CurrentAttribute = "Current";
    private const string BindAttribute = "Bind";
    private const string EmitNameAttribute = "EmitName";
    private const string DefaultEmitName = "SelectMode";

    private const float CardGap = 6f;

    private UiElementSpec _spec = UiElementSpec.Empty;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        var cards = new List<ModeCardData>();
        for (int i = 1; ; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            if (!HasAnyCardAttribute(suffix)) break;

            cards.Add(new ModeCardData(
                Read("Title" + suffix),
                Read("Description" + suffix),
                Read("Value" + suffix)));
        }

        if (cards.Count == 0) return 64f;

        float rowWidth = Math.Max(1f, ctx.ViewWidth);
        float cardWidth = Math.Max(1f, (rowWidth - CardGap * (cards.Count - 1)) / cards.Count);
        float maxHeight = 64f;
        foreach (ModeCardData card in cards)
        {
            float textWidth = Math.Max(1f, cardWidth - 20f);
            float titleHeight = ctx.Metrics.MeasureText(card.Title, UiFont.Small, textWidth);
            float descHeight = ctx.Metrics.MeasureText(card.Description, UiFont.Tiny, textWidth);
            float cardHeight = 7f + Math.Max(24f, titleHeight) + 6f + descHeight + 8f;
            if (cardHeight > maxHeight) maxHeight = cardHeight;
        }

        return maxHeight;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string current = ResolveCurrent(ctx);
        string emitName = ReadEmitName();

        var cards = new List<ModeCardData>();
        for (int i = 1; ; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            if (!HasAnyCardAttribute(suffix)) break;

            cards.Add(new ModeCardData(
                Read("Title" + suffix),
                Read("Description" + suffix),
                Read("Value" + suffix)));
        }

        if (cards.Count == 0) return;

        float cardWidth = Math.Max(1f, (rect.width - CardGap * (cards.Count - 1)) / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            ModeCardData card = cards[i];
            float x = rect.x + i * (cardWidth + CardGap);
            var cardRect = new Rect(x, rect.y, cardWidth, rect.height);

            bool selected = string.Equals(current, card.Value, StringComparison.Ordinal);
            ModeCardRenderer.Draw(cardRect, selected, card.Title, card.Description,
                () => emit(new UiCommand(emitName, card.Value)));
        }
    }

    private bool HasAnyCardAttribute(string suffix)
    {
        return _spec.TryGetAttribute("Title" + suffix, out _)
            || _spec.TryGetAttribute("Description" + suffix, out _)
            || _spec.TryGetAttribute("Value" + suffix, out _);
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

    private sealed class ModeCardData
    {
        internal string Title;
        internal string Description;
        internal string Value;

        internal ModeCardData(string title, string description, string value)
        {
            Title = title;
            Description = description;
            Value = value;
        }
    }
}
