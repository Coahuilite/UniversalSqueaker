using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using VerseWidgets = Verse.Widgets;

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
    private const string HelpKeysAttribute = "HelpKeys";
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
        int columns = CardColumns(rowWidth, cards.Count);
        int rows = (cards.Count + columns - 1) / columns;
        float cardWidth = Math.Max(1f, (rowWidth - CardGap * (columns - 1)) / columns);
        float maxHeight = 64f;
        foreach (ModeCardData card in cards)
        {
            float textWidth = Math.Max(1f, cardWidth - 20f);
            float titleHeight = ctx.Metrics.MeasureText(card.Title, UiFont.Small, textWidth);
            float descHeight = ctx.Metrics.MeasureText(card.Description, UiFont.Tiny, textWidth);
            float cardHeight = 7f + Math.Max(24f, titleHeight) + 6f + descHeight + 8f;
            if (cardHeight > maxHeight) maxHeight = cardHeight;
        }

        return rows * maxHeight + (rows - 1) * CardGap;
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiGuard.DrawOrFallback(
            rect,
            () => DrawCore(rect, ctx, emit),
            fallback => DrawVanilla(fallback, ctx, emit),
            Kind,
            "FerriteLib");
    }

    private void DrawCore(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
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

        int columns = CardColumns(rect.width, cards.Count);
        int rows = (cards.Count + columns - 1) / columns;
        float cardWidth = Math.Max(1f, (rect.width - CardGap * (columns - 1)) / columns);
        float cardHeight = (rect.height - CardGap * (rows - 1)) / rows;

        string[] helpKeys = ReadHelpKeys();
        for (int i = 0; i < cards.Count; i++)
        {
            ModeCardData card = cards[i];
            int row = i / columns;
            int col = i % columns;
            float x = rect.x + col * (cardWidth + CardGap);
            float y = rect.y + row * (cardHeight + CardGap);
            var cardRect = new Rect(x, y, cardWidth, cardHeight);

            string helpKey = i < helpKeys.Length ? helpKeys[i] : "";
            if (!string.IsNullOrEmpty(helpKey) && Mouse.IsOver(cardRect))
            {
                ctx.State.HelpHoverKey = helpKey;
            }

            bool selected = string.Equals(current, card.Value, StringComparison.Ordinal);
            ModeCardRenderer.Draw(cardRect, selected, card.Title, card.Description,
                () => emit(new UiCommand(emitName, card.Value)));

            if (!string.IsNullOrEmpty(helpKey)
                && (string.Equals(ctx.State.HelpHoverKey, helpKey, StringComparison.Ordinal)
                    || string.Equals(ctx.State.HelpSelectionKey, helpKey, StringComparison.Ordinal)))
            {
                SurfaceFrame.DrawBorder(cardRect, Palette.AccentGold);
            }
        }
    }

    private void DrawVanilla(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
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

        int columns = CardColumns(rect.width, cards.Count);
        int rows = (cards.Count + columns - 1) / columns;
        float cardWidth = Math.Max(1f, (rect.width - CardGap * (columns - 1)) / columns);
        float cardHeight = (rect.height - CardGap * (rows - 1)) / rows;
        for (int i = 0; i < cards.Count; i++)
        {
            ModeCardData card = cards[i];
            int row = i / columns;
            int col = i % columns;
            Rect cardRect = new(rect.x + col * (cardWidth + CardGap), rect.y + row * (cardHeight + CardGap), cardWidth, cardHeight);
            bool selected = string.Equals(current, card.Value, StringComparison.Ordinal);
            if (VerseWidgets.ButtonText(cardRect, (selected ? "● " : "") + card.Title))
            {
                emit(new UiCommand(emitName, card.Value));
            }
        }
    }

    private static int CardColumns(float width, int count)
    {
        int columns = Math.Max(1, count);
        if (columns > 1 && (width - CardGap * (columns - 1)) / columns < 140f)
        {
            columns = 2;
        }
        if (columns > 1 && (width - CardGap * (columns - 1)) / columns < 100f)
        {
            columns = 1;
        }
        return Math.Max(1, Math.Min(columns, count));
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

    private string[] ReadHelpKeys()
    {
        if (!_spec.TryGetAttribute(HelpKeysAttribute, out string value)
            || string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
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
