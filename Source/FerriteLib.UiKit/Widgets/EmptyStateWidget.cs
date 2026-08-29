using System;
using System.Globalization;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// Centered empty/error state inside a framed surface. Attributes: Text/Bind and Font (default Small).
/// </summary>
public sealed class EmptyStateWidget : IWidget
{
    public const string Kind = "chrome/empty-state";

    private const string BindAttribute = "Bind";
    private const string TextAttribute = "Text";
    private const string FontAttribute = "Font";

    private UiElementSpec _spec = UiElementSpec.Empty;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        string text = ResolveText(ctx);
        if (text.Length == 0) return 40f;

        UiFont font = ReadFont();
        float contentWidth = Math.Max(1f, ctx.ViewWidth - 32f);
        return Math.Max(40f, ctx.Metrics.MeasureText(text, font, contentWidth) + 16f);
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (rect.width <= 1f || rect.height <= 1f) return;

        SurfaceFrame.Draw(rect);

        string text = ResolveText(ctx);
        if (text.Length == 0) return;

        UiKitGui.Label(rect.ContractedBy(16f), text, ReadFont(), TextAnchor.MiddleCenter, Palette.TextSecondary);
    }

    private string ResolveText(WidgetContext ctx)
    {
        if (_spec.TryGetAttribute(BindAttribute, out string key)
            && key.Length > 0
            && ctx.TryGetViewValue(key, out object? value)
            && value != null)
        {
            return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        return _spec.TryGetAttribute(TextAttribute, out string literal) ? literal : "";
    }

    private UiFont ReadFont()
    {
        if (_spec.TryGetAttribute(FontAttribute, out string raw)
            && Enum.TryParse(raw.Trim(), true, out UiFont font))
        {
            return font;
        }
        return UiFont.Small;
    }
}
