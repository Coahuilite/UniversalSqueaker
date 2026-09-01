using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>Greenfield chrome banner: a thin theme-aware text banner.</summary>
public sealed class ChromeBannerWidget : IUiWidget
{
    public const string Kind = "chrome/banner";

    private const float DefaultHeight = 22f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ChromeBannerWidget(),
            new[] { "Id", "Kind", "Bind", "Text", "Height", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            bindings.ValidateValue<string>(bindKey, elementPath);
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadHeight();
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string text = "";
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            ctx.Bindings.TryGet(bindKey, out text);
        }
        else if (spec.TryGetAttribute("Text", out string literal))
        {
            text = literal;
        }

        if (text.Length == 0) return;

        UiThemeDraw.Label(rect, text, ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
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
