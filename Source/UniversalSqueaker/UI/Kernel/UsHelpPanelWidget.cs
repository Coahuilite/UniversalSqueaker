using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US help panel content widget. It lives inside the XML "help-scroll" Scroll
/// container (the engine owns BeginScrollView), so this widget only measures and draws the catalog
/// content for the current section and emits typed hover/selection actions. All help content comes
/// from the existing <see cref="UsHelpCatalog"/> / <see cref="UsHelpPanelLogic"/> pure sources.
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
    private const float ContentLabelHeight = 22f;

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
        UsHelpPanelLogic.HelpPanelDisplay display = ResolveFrameDisplay(ctx, section);

        float listHeight = ItemHeight;
        if (section != null && section.Items.Count > 0)
        {
            listHeight = ItemHeight + (ItemHeight + ItemGap) * section.Items.Count;
        }

        float headerHeight = HeaderHeight(ctx, HeaderText(ctx, display), textWidth);
        float textHeight = Math.Max(1f, ctx.Metrics.MeasureText(display.Text, UiFont.Tiny, textWidth));
        return Padding * 2f + headerHeight + TitleGap + listHeight + ContentGap + ContentLabelHeight + 2f + textHeight + 4f;
    }

    /// <summary>
    /// The one header-text outlet. The header is a Keyed format string wrapping the active section
    /// title, so its length is data: Measure and Draw must both take it from here or the band gets
    /// sized for one string while another is drawn.
    /// </summary>
    private static string HeaderText(UiWidgetContext ctx, UsHelpPanelLogic.HelpPanelDisplay display)
    {
        return string.Format(UsKernelDraw.Keyed(ctx, HeaderFormatKey), display.Title);
    }

    /// <summary>Header band height: one line at minimum, more when the section title wraps.</summary>
    private static float HeaderHeight(UiWidgetContext ctx, string header, float textWidth)
    {
        return Math.Max(TitleMinHeight, ctx.Metrics.MeasureText(header, UiFont.Small, textWidth));
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

        // Keep the context header fixed at the top of the help surface; the index and body below
        // follow the active section, hover, or selection without changing the panel's hierarchy.
        string header = HeaderText(ctx, display);
        Rect headerRect = new(x, y, textWidth, HeaderHeight(ctx, header, textWidth));
        UiThemeDraw.SectionHeader(headerRect, header, ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small);
        UiThemeDraw.AccentRail(headerRect, ctx.Theme, true, 2f);
        y += headerRect.height + TitleGap;

        DrawItemRow(new Rect(x, y, textWidth, ItemHeight), UsKernelDraw.Keyed(ctx, UsHelpPanelLogic.OverviewLabel),
            selected: display.IsOverview, itemKey: null, ctx);
        y += ItemHeight;

        if (section != null)
        {
            foreach (HelpItem item in section.Items)
            {
                y += ItemGap;
                DrawItemRow(
                    new Rect(x, y, textWidth, ItemHeight),
                    item.Label,
                    selected: !display.IsOverview && string.Equals(display.ItemKey, item.Key, StringComparison.Ordinal),
                    itemKey: item.Key,
                    ctx);
                y += ItemHeight;
            }
        }

        y += ContentGap;
        UsKernelDraw.Label(
            new Rect(x, y, textWidth, ContentLabelHeight),
            display.Label,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            UiFont.Small,
            TextAnchor.UpperLeft);
        y += ContentLabelHeight + 2f;
        float textHeight = Math.Max(1f, ctx.Metrics.MeasureText(display.Text, UiFont.Tiny, textWidth));
        UsKernelDraw.Label(
            new Rect(x, y, textWidth, textHeight),
            display.Text,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            UiFont.Tiny,
            TextAnchor.UpperLeft);
    }

    private void DrawItemRow(Rect rect, string label, bool selected, string? itemKey, UiWidgetContext ctx)
    {
        bool hovered = Mouse.IsOver(rect);
        bool wasHovering = ctx.Bindings.TryGet("help-hover", out string currentHover)
            && string.Equals(currentHover, itemKey ?? "", StringComparison.Ordinal);

        if (hovered && !wasHovering)
        {
            // Hover changes the resolved help text, which changes the panel height; bump the
            // revision only when the display text actually changes so transient hover events do
            // not invalidate layout. Selection changes are bumped by the Host binding boundary.
            HelpSection? section = CurrentSection(ctx);
            string selection = ctx.Bindings.TryGet("help-selection", out string s) ? s : "";
            string before = UsHelpPanelLogic.Resolve(section, currentHover, selection, TranslationSeam(ctx)).Text;
            string after = UsHelpPanelLogic.Resolve(section, itemKey ?? "", selection, TranslationSeam(ctx)).Text;
            ctx.Bindings.Invoke("set-help-hover", itemKey ?? "");
            if (!string.Equals(after, before, StringComparison.Ordinal))
            {
                ctx.Session.BumpContentRevision();
            }
        }

        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered, selected);
        UsKernelDraw.Label(
            new Rect(rect.x + 6f, rect.y, Math.Max(1f, rect.width - 12f), rect.height),
            label,
            ctx.Theme,
            selected ? ctx.Theme.TextOnGold : ctx.Theme.TextPrimary,
            UiFont.Tiny,
            TextAnchor.MiddleLeft);

        string capturedKey = itemKey ?? "";
        if (UiNative.Button(rect))
        {
            // set-help-selection bumps the session revision through the Host binding boundary.
            ctx.Bindings.Invoke("set-help-selection", capturedKey);
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
    /// The seam handed to the Verse-free panel logic. That module may not reference Verse, so it
    /// returns Keyed entry NAMES for its own three strings; this is where they become display text.
    /// Measure, Draw and the hover text-diff all resolve through this one seam, so a measured height
    /// can never belong to a different string than the one drawn. Catalog text is deliberately not
    /// routed through it while the catalog is still plain English (see <see cref="UsHelpPanelLogic"/>).
    /// </summary>
    private static Func<string, string> TranslationSeam(UiWidgetContext ctx)
    {
        return key => UsKernelDraw.Keyed(ctx, key);
    }
}
