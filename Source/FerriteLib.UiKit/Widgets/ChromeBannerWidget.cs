using System;
using System.Globalization;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// Framed banner with middle-left text. Attributes: Bind (view key), Text (fallback literal),
/// Font (Tiny/Small/Medium, default Tiny), Height (Auto or fixed; handled by the engine),
/// ColorHint (base/warning/success).
/// </summary>
public sealed class ChromeBannerWidget : IWidget
{
    public const string Kind = "chrome/banner";

    private const string BindAttribute = "Bind";
    private const string TextAttribute = "Text";
    private const string FontAttribute = "Font";
    private const string ColorHintAttribute = "ColorHint";

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
        if (text.Length == 0) return 0f;

        UiFont font = ReadFont();
        float contentWidth = Math.Max(1f, ctx.ViewWidth - 16f);
        return Math.Max(34f, ctx.Metrics.MeasureText(text, font, contentWidth) + 12f);
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string text = ResolveText(ctx);
        if (text.Length == 0) return;

        SurfaceFrame.SurfaceKind surface = ReadColorHint();
        SurfaceFrame.Draw(rect, surface);

        Color textColor = surface switch
        {
            SurfaceFrame.SurfaceKind.Warning => Palette.TextOnDanger,
            SurfaceFrame.SurfaceKind.Danger => Palette.TextOnDanger,
            SurfaceFrame.SurfaceKind.Success => Palette.TextOnGold,
            _ => Palette.TextSecondary
        };

        UiKitGui.Label(rect.ContractedBy(8f, 4f), text, ReadFont(), TextAnchor.MiddleLeft, textColor);
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
        return UiFont.Tiny;
    }

    private SurfaceFrame.SurfaceKind ReadColorHint()
    {
        if (_spec.TryGetAttribute(ColorHintAttribute, out string raw))
        {
            string value = raw.Trim();
            if (string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase)) return SurfaceFrame.SurfaceKind.Warning;
            if (string.Equals(value, "success", StringComparison.OrdinalIgnoreCase)) return SurfaceFrame.SurfaceKind.Success;
        }
        return SurfaceFrame.SurfaceKind.Base;
    }
}
