using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US help panel content widget (C+A model; D2 ruling: no persistent index list).
/// It lives inside the XML "help-scroll" Scroll container (the engine owns BeginScrollView), so
/// this widget only measures and draws help content: the hovered control's entry when a claim is
/// live, otherwise the active section's overview (the big-level fallback). Claims come from the
/// mid-column controls through <see cref="UsKernelDraw.HelpHover"/>; the panel owns no interactive
/// rows - with hover supplying context, a list of options disconnected from what is shown read as
/// broken (maintainer ruling, 2026-09-05) - so pinned selection retired with the list. Text bands
/// are hover-invariant catalog maxima, so switching entries never changes the panel height and
/// never invalidates layout. Content comes from <see cref="UsHelpCatalog"/> /
/// <see cref="UsHelpPanelLogic"/> pure sources, stored as Keyed entry names.
/// </summary>
public sealed class UsHelpPanelWidget : IUiWidget
{
    public const string Kind = "us/help-panel";

    private const float Padding = 8f;
    private const float TitleMinHeight = 20f;
    private const float TitleGap = 6f;
    private const float ContentGap = 6f;

    /// <summary>Keyed format string of the panel header; <c>{0}</c> is the active section title.</summary>
    private const string HeaderFormatKey = "US.Help.Header";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsHelpPanelWidget(),
            new[] { "Id", "Kind", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<string>("help-section-key", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        float textWidth = Math.Max(1f, ctx.ViewWidth - Padding * 2f);

        // Hover-invariant bands: any catalog entry can appear in the header/label/body at any
        // time (hover resolves across the whole catalog), so the panel sizes against the tallest
        // candidate, never the currently displayed string. Switching help text on hover therefore
        // never changes the panel height and never invalidates layout.
        float headerHeight = MaxHeaderBand(ctx, textWidth);
        float labelHeight = MaxLabelBand(ctx, textWidth);
        float textHeight = MaxBodyBand(ctx, textWidth);
        return Padding * 2f + headerHeight + TitleGap + ContentGap + labelHeight + 2f + textHeight + 4f;
    }

    /// <summary>
    /// The one header-text outlet. The header is a Keyed format string wrapping the active title,
    /// so its length is data: Measure and Draw must both take it from here or the band gets
    /// sized for one string while another is drawn.
    /// </summary>
    private static string HeaderText(UiWidgetContext ctx, UsHelpPanelLogic.HelpPanelDisplay display)
    {
        return string.Format(UsKernelDraw.Keyed(ctx, HeaderFormatKey), display.Title);
    }

    /// <summary>
    /// Header band measured against every title the header can ever show (hover is global). The
    /// candidates are the catalog's section title keys resolved through the same seam the panel
    /// draws from, and the header renders in Small, so both font and resolution match the draw.
    /// </summary>
    private static float MaxHeaderBand(UiWidgetContext ctx, float textWidth)
    {
        string format = UsKernelDraw.Keyed(ctx, HeaderFormatKey);
        float max = Band(ctx, string.Format(format, UsKernelDraw.Keyed(ctx, UsHelpPanelLogic.EmptyTitle)), textWidth, UiFont.Small);
        foreach (string titleKey in UsHelpCatalog.AllSectionTitles())
        {
            max = Math.Max(max, Band(ctx, string.Format(format, UsKernelDraw.Keyed(ctx, titleKey)), textWidth, UiFont.Small));
        }

        return Math.Max(TitleMinHeight, max);
    }

    /// <summary>Label band (Small, as drawn) against the overview label, the empty title and every resolved item label.</summary>
    private static float MaxLabelBand(UiWidgetContext ctx, float textWidth)
    {
        float max = Band(ctx, UsKernelDraw.Keyed(ctx, UsHelpPanelLogic.OverviewLabel), textWidth, UiFont.Small);
        max = Math.Max(max, Band(ctx, UsKernelDraw.Keyed(ctx, UsHelpPanelLogic.EmptyTitle), textWidth, UiFont.Small));
        foreach (string labelKey in UsHelpCatalog.AllItemLabels())
        {
            max = Math.Max(max, Band(ctx, UsKernelDraw.Keyed(ctx, labelKey), textWidth, UiFont.Small));
        }

        return max;
    }

    /// <summary>
    /// Body band (Tiny, as drawn) against every resolved string the body can ever show: the empty
    /// text, EVERY section overview (overviews run longer than any item body) and every item body.
    /// </summary>
    private static float MaxBodyBand(UiWidgetContext ctx, float textWidth)
    {
        float max = Band(ctx, UsKernelDraw.Keyed(ctx, UsHelpPanelLogic.EmptyText), textWidth, UiFont.Tiny);
        foreach (string overviewKey in UsHelpCatalog.AllSectionOverviews())
        {
            max = Math.Max(max, Band(ctx, UsKernelDraw.Keyed(ctx, overviewKey), textWidth, UiFont.Tiny));
        }

        foreach (string textKey in UsHelpCatalog.AllItemTexts())
        {
            max = Math.Max(max, Band(ctx, UsKernelDraw.Keyed(ctx, textKey), textWidth, UiFont.Tiny));
        }

        return max;
    }

    private static float Band(UiWidgetContext ctx, string text, float textWidth, UiFont font)
    {
        return Math.Max(1f, ctx.Metrics.MeasureText(text, font, textWidth));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiThemeDraw.Panel(rect, ctx.Theme);

        HelpSection? section = CurrentSection(ctx);
        UsHelpPanelLogic.HelpPanelDisplay display = ResolveFrameDisplay(ctx, section);

        float x = rect.x + Padding;
        float y = rect.y + Padding;
        float textWidth = Math.Max(1f, rect.width - Padding * 2f);

        // Header, then the displayed entry's label and body. All three bands are the same
        // hover-invariant maxima Measure allocated, so the content below cannot shift.
        string header = HeaderText(ctx, display);
        Rect headerRect = new(x, y, textWidth, MaxHeaderBand(ctx, textWidth));
        UiThemeDraw.SectionHeader(headerRect, header, ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small);
        UiThemeDraw.AccentRail(headerRect, ctx.Theme, true, 2f);
        y += headerRect.height + TitleGap;

        y += ContentGap;
        UsKernelDraw.Label(
            new Rect(x, y, textWidth, MaxLabelBand(ctx, textWidth)),
            display.Label,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.UpperLeft);
        y += MaxLabelBand(ctx, textWidth) + 2f;
        UsKernelDraw.Label(
            new Rect(x, y, textWidth, MaxBodyBand(ctx, textWidth)),
            display.Text,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.UpperLeft);
    }

    /// <summary>The section this panel is currently falling back to (null when none is active).</summary>
    private static HelpSection? CurrentSection(UiWidgetContext ctx)
    {
        string sectionKey = ctx.Bindings.TryGet("help-section-key", out string key) ? key : "";
        UsHelpCatalog.TryGetSection(sectionKey, out HelpSection section);
        return section;
    }

    /// <summary>Resolves the display for the frame's current hover claim (session-owned since FL P3;
    /// the claim is not a binding, so there is nothing to validate here beyond the section fallback).</summary>
    private static UsHelpPanelLogic.HelpPanelDisplay ResolveFrameDisplay(UiWidgetContext ctx, HelpSection? section)
    {
        return UsHelpPanelLogic.Resolve(section, ctx.Session.HoverClaim ?? "", TranslationSeam(ctx));
    }

    /// <summary>
    /// The seam handed to the Verse-free panel logic: catalog and panel strings are Keyed entry
    /// names, and Measure, Draw and the band maxima all resolve through it, so a measured height
    /// can never belong to a different string than the one drawn.
    /// </summary>
    private static Func<string, string> TranslationSeam(UiWidgetContext ctx)
    {
        return key => UsKernelDraw.Keyed(ctx, key);
    }
}
