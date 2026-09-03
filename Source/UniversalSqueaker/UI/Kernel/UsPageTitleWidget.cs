using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US page title: a large title line plus a caption line, resolved from the active
/// workspace through the host translation seam. Both lines are Keyed entries, never literals, so a
/// non-English game gets a translated heading instead of leftover English prose.
/// </summary>
public sealed class UsPageTitleWidget : IUiWidget
{
    public const string Kind = "us/page-title";

    private const float TitleMinHeight = 30f;
    private const float CaptionHeight = 18f;
    private const float CaptionGap = 4f;
    private const float TextLeftInset = 2f;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsPageTitleWidget(),
            new[] { "Id", "Kind", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>("active-tab", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        (string title, string caption) = ResolveHeading(ctx);
        return TitleBand(ctx, title) + CaptionGap + CaptionBand(ctx, caption);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        (string title, string caption) = ResolveHeading(ctx);
        float textWidth = Math.Max(1f, rect.width - TextLeftInset);
        float titleHeight = TitleBand(textWidth, ctx, title);
        float captionTop = rect.y + titleHeight + CaptionGap;
        float captionHeight = CaptionBand(textWidth, ctx, caption);

        UiThemeDraw.SectionBand(rect, ctx.Theme);
        UsKernelDraw.Label(
            new Rect(rect.x + TextLeftInset, rect.y, textWidth, titleHeight),
            title,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Medium,
            TextAnchor.MiddleLeft);
        UsKernelDraw.Label(
            new Rect(rect.x + TextLeftInset, captionTop, textWidth, captionHeight),
            caption,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.UpperLeft);
    }

    /// <summary>
    /// Caption band for the text as it is actually resolved for this frame. The caption is instructional
    /// prose and is the longest string on the page, so its height comes from the injected metrics rather
    /// than a constant: a constant here silently clips the second line on narrow layouts.
    /// Shared by Measure and Draw so the allocated band always equals the drawn band.
    /// </summary>
    private static float CaptionBand(UiWidgetContext ctx, string caption)
    {
        return CaptionBand(Math.Max(1f, ctx.ViewWidth - TextLeftInset), ctx, caption);
    }

    private static float CaptionBand(float textWidth, UiWidgetContext ctx, string caption)
    {
        return Math.Max(CaptionHeight, Math.Max(1f, ctx.Metrics.MeasureText(caption, UiFont.Tiny, textWidth)));
    }

    /// <summary>
    /// Title band, measured like the caption. The in-game font engine reports one medium line at 30px
    /// (ui.text.overflow, 2026-09-04), so the old 26px constant clipped every workspace title by one
    /// third of a line. Shared by Measure and Draw so the allocated band equals the drawn band.
    /// </summary>
    private static float TitleBand(UiWidgetContext ctx, string title)
    {
        return TitleBand(Math.Max(1f, ctx.ViewWidth - TextLeftInset), ctx, title);
    }

    private static float TitleBand(float textWidth, UiWidgetContext ctx, string title)
    {
        return Math.Max(TitleMinHeight, Math.Max(1f, ctx.Metrics.MeasureText(title, UiFont.Medium, textWidth)));
    }

    private static (string Title, string Caption) ResolveHeading(UiWidgetContext ctx)
    {
        ctx.Bindings.TryGet("active-tab", out string activeTab);
        (string titleKey, string captionKey) = HeadingKeyFor(activeTab);
        return (ctx.Translation.Translate(titleKey), ctx.Translation.Translate(captionKey));
    }

    /// <summary>
    /// One entry per workspace, keyed by the tab token the navigation widget emits. Tab tokens stay
    /// untranslated because they are also the persisted state value; only the display strings are Keyed.
    /// </summary>
    private static (string TitleKey, string CaptionKey) HeadingKeyFor(string? activeTab)
    {
        return activeTab switch
        {
            "Distance" => ("US.Page.Distance.Title", "US.Page.Distance.Caption"),
            "Packs" => ("US.Page.Packs.Title", "US.Page.Packs.Caption"),
            "Tuning" => ("US.Page.Tuning.Title", "US.Page.Tuning.Caption"),
            "Presets" => ("US.Page.Presets.Title", "US.Page.Presets.Caption"),
            _ => ("US.Page.Overview.Title", "US.Page.Overview.Caption")
        };
    }
}
