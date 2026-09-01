using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>Greenfield empty state placeholder.</summary>
public sealed class EmptyStateWidget : IUiWidget
{
    public const string Kind = "state/empty";

    private const float DefaultHeight = 48f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new EmptyStateWidget(),
            new[] { "Id", "Kind", "Text", "TextKey", "Height", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        // No required binding.
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadHeight();
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string text = spec.TryGetAttribute("TextKey", out string key) && key.Length > 0
            ? ctx.Translation.Translate(key)
            : spec.TryGetAttribute("Text", out string literal) ? literal : "";

        UiThemeDraw.Label(rect, text, ctx.Theme, ctx.Theme.TextSecondary, UiFont.Small, TextAnchor.MiddleCenter);
    }

    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }
}
