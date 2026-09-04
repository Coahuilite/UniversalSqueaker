using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US help panel content widget (C+A model). It lives inside the XML "help-scroll"
/// Scroll container (the engine owns BeginScrollView), so this widget only measures and draws the
/// catalog content. The displayed entry is the hovered control's claim (resolved across the whole
/// catalog through <see cref="UsHelpPanelLogic.Resolve"/>), falling back to the active section's
/// overview; the index rows themselves claim hover through the same channel. All text bands are
/// hover-invariant catalog maxima, so switching entries never changes the panel height and never
/// invalidates layout. All content comes from <see cref="UsHelpCatalog"/> /
/// <see cref="UsHelpPanelLogic"/> pure sources, stored as Keyed entry names.
/// </summary>
public sealed class UsHelpPanelWidget : IUiWidget
{
    public const string Kind = "us/help-panel";

    private const float Padding = 8f;
    private const float TitleMinHeight = 20f;
    private const float TitleGap = 6f;
    private const float ItemHeight = 20f;
    private const float ItemGap = 2f;
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
        bindings.ValidateValue<string>("help-hover", elementPath);
        bindings.ValidateValue<string>("help-selection", elementPath);
        bindings.ValidateAction<string>("set-help-hover", elementPath);
        bindings.ValidateAction<string>("set-help-selection", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        float textWidth = Math.Max(1f, ctx.ViewWidth - Padding * 2f);
        HelpSection? section = CurrentSection(ctx);

        float listHeight = ItemHeight;
        if (section != null && section.Items.Count > 0)
        {
            listHeight = ItemHeight + (ItemHeight + ItemGap) * section.Items.Count;
        }

        // Hover-invariant bands: any catalog entry can appear in the header/label/body at any time
        // (hover resolves across the whole catalog), so the panel sizes against the tallest
        // candidate, never the currently displayed string. Switching help text on hover therefore
        // never changes the panel height and never invalidates layout.
        float headerHeight = MaxHeaderBand(ctx, textWidth);
        float labelHeight = MaxLabelBand(ctx, textWidth);
        float textHeight = MaxBodyBand(ctx, textWidth);
        return Padding * 2f + headerHeight + TitleGap + listHeight + ContentGap + labelHeight + 2f + textHeight + 4f;
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
        float x = rect.x + Padding;
        float textWidth = Math.Max(1f, rect.width - Padding * 2f);
        float headerBand = MaxHeaderBand(ctx, textWidth);
        float labelBand = MaxLabelBand(ctx, textWidth);
        float bodyBand = MaxBodyBand(ctx, textWidth);

        // Index rows are laid out once; the claim pass and the draw pass share the rects so the
        // hovered row and the displayed body can never disagree about which row owns which rect.
        var rows = new List<(Rect Row, string? Key)>(1 + (section?.Items.Count ?? 0));
        float rowY = rect.y + Padding + headerBand + TitleGap;
        rows.Add((new Rect(x, rowY, textWidth, ItemHeight), null));
        rowY += ItemHeight;
        if (section != null)
        {
            foreach (HelpItem item in section.Items)
            {
                rowY += ItemGap;
                rows.Add((new Rect(x, rowY, textWidth, ItemHeight), item.Key));
                rowY += ItemHeight;
            }
        }

        // Claim pass first: the panel's own rows claim hover BEFORE the display is resolved, so
        // hovering the index updates the body in the same pass (the window clears the claim at the
        // start of every frame, so resolving first would never see it). Mid-column controls claimed
        // earlier in this Draw order already; the panel draws last.
        foreach ((Rect rowRect, string? key) in rows)
        {
            UsKernelDraw.HelpHover(rowRect, ctx, key ?? UsHelpPanelLogic.OverviewHoverKey);
        }

        UsHelpPanelLogic.HelpPanelDisplay display = ResolveFrameDisplay(ctx, section);

        // Keep the context header fixed at the top of the help surface; the index and body below
        // follow the active section, hover, or selection without changing the panel's hierarchy.
        // All three bands are the same hover-invariant maxima Measure allocated.
        string header = HeaderText(ctx, display);
        Rect headerRect = new(x, rect.y + Padding, textWidth, headerBand);
        UiThemeDraw.SectionHeader(headerRect, header, ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small);
        UiThemeDraw.AccentRail(headerRect, ctx.Theme, true, 2f);

        for (int i = 0; i < rows.Count; i++)
        {
            string? key = rows[i].Key;
            string label = i == 0
                ? UsKernelDraw.Keyed(ctx, UsHelpPanelLogic.OverviewLabel)
                : UsKernelDraw.Keyed(ctx, section!.Items[i - 1].Label);
            bool selected = i == 0
                ? display.IsOverview
                : !display.IsOverview && string.Equals(display.ItemKey, key, StringComparison.Ordinal);
            DrawItemRow(rows[i].Row, label, selected, Mouse.IsOver(rows[i].Row), key, ctx);
        }

        float y = rowY + ContentGap;
        UsKernelDraw.Label(
            new Rect(x, y, textWidth, labelBand),
            display.Label,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.UpperLeft);
        y += labelBand + 2f;
        UsKernelDraw.Label(
            new Rect(x, y, textWidth, bodyBand),
            display.Text,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.UpperLeft);
    }

    private static void DrawItemRow(Rect rect, string label, bool selected, bool hovering, string? itemKey, UiWidgetContext ctx)
    {
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovering, selected);
        UsKernelDraw.Label(
            new Rect(rect.x + 6f, rect.y, Math.Max(1f, rect.width - 12f), rect.height),
            label,
            ctx.Theme,
            selected ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        if (UiNative.Button(rect))
        {
            // set-help-selection bumps the session revision through the Host binding boundary.
            ctx.Bindings.Invoke("set-help-selection", itemKey ?? "");
        }
    }

    /// <summary>The section this panel is currently explaining (null when nothing is selected).</summary>
    private static HelpSection? CurrentSection(UiWidgetContext ctx)
    {
        string sectionKey = ctx.Bindings.TryGet("help-section-key", out string key) ? key : "";
        UsHelpCatalog.TryGetSection(sectionKey, out HelpSection section);
        return section;
    }

    /// <summary>Resolves the display for the frame's current hover/selection bindings.</summary>
    private static UsHelpPanelLogic.HelpPanelDisplay ResolveFrameDisplay(UiWidgetContext ctx, HelpSection? section)
    {
        string hover = ctx.Bindings.TryGet("help-hover", out string h) ? h : "";
        string selection = ctx.Bindings.TryGet("help-selection", out string s) ? s : "";
        return UsHelpPanelLogic.Resolve(section, hover, selection, TranslationSeam(ctx));
    }

    /// <summary>
    /// The seam handed to the Verse-free panel logic. That module may not reference Verse, so
    /// everything - its own three strings and every catalog title/label/body - arrives as a Keyed
    /// entry NAME; this is where they become display text. Measure, Draw and the band maxima all
    /// resolve through this one seam, so a measured height can never belong to a different string
    /// than the one drawn.
    /// </summary>
    private static Func<string, string> TranslationSeam(UiWidgetContext ctx)
    {
        return key => UsKernelDraw.Keyed(ctx, key);
    }
}
