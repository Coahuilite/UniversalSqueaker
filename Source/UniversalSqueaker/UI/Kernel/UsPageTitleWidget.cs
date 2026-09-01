using System;
using UnityEngine;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US page title: a large title line plus a caption line, from TitleKey/Title and
/// CaptionKey/Caption attributes resolved through the host translation seam (keys win over
/// literals).
/// </summary>
public sealed class UsPageTitleWidget : IUiWidget
{
    public const string Kind = "us/page-title";

    private const float TitleHeight = 26f;
    private const float CaptionHeight = 18f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsPageTitleWidget(),
            new[] { "Id", "Kind", "Title", "TitleKey", "Caption", "CaptionKey", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>("active-tab", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        return TitleHeight + CaptionHeight + 4f;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        ctx.Bindings.TryGet("active-tab", out string activeTab);
        (string title, string caption) = HeadingFor(activeTab);
        UiThemeDraw.SectionBand(rect, ctx.Theme);
        UsKernelDraw.Label(
            new Rect(rect.x + 2f, rect.y, rect.width - 2f, TitleHeight),
            title,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Medium,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + 2f, rect.y + TitleHeight + 4f, rect.width - 2f, CaptionHeight),
            caption,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);
    }

    private static (string Title, string Caption) HeadingFor(string activeTab)
    {
        return activeTab switch
        {
            "Distance" => ("Distance attenuation", "Set one range, compare the curve, then choose a quick preset."),
            "Packs" => ("VoicePack selection", "Follow the flow: filter domains, choose a domain, then enable packs."),
            "Tuning" => ("Layered tuning", "Select the layer and domain first; then set action scope and mood values."),
            "Presets" => ("Baseline presets", "These importable baselines are supplied by installed Defs."),
            _ => ("VoicePack Routing", "Choose routing and playback behaviour for this save.")
        };
    }

    private string ReadText(UiWidgetContext ctx, string attribute)
    {
        if (spec.TryGetAttribute(attribute, out string key) && key.Trim().Length > 0)
        {
            return ctx.Translation.Translate(key.Trim());
        }

        string literalAttribute = string.Equals(attribute, "TitleKey", StringComparison.Ordinal) ? "Title" : "Caption";
        return spec.TryGetAttribute(literalAttribute, out string literal) ? literal : "";
    }
}
