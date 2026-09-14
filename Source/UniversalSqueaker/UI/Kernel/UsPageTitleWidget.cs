using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Kernel-owned US page title: a large title line plus a caption line, resolved from the active
/// workspace through the host translation seam. Both lines are Keyed entries, never literals, so a
/// non-English game gets a translated heading instead of leftover English prose.
///
/// The title band also carries the discoverable Help toggle for the retractable right-side drawer:
/// a reserved control area at the right edge of the band, drawn with the existing selection-button
/// primitive and writing the Host's own "help-open" value binding. The toggle is NOT wired to
/// <c>UiBindings.ActiveTabKey</c> - help visibility is independent state. It explains itself through
/// the same single claim outlet every other control uses (<c>UsKernelDraw.HelpHover</c>), claiming the
/// catalog item <c>us/page-title/help-drawer</c>: the drawer control was the one interactive control
/// with no help entry (W2 audit, 2026-09-13), so hovering it now describes the drawer instead of
/// falling back to the section overview.
/// </summary>
public sealed class UsPageTitleWidget : IUiWidget
{
    public const string Kind = "us/page-title";

    /// <summary>Keyed label of the help-drawer toggle.</summary>
    private const string HelpToggleKey = "US.Help.Drawer.Toggle";

    /// <summary>The drawer's own boolean binding; never the active-tab key.</summary>
    private const string HelpOpenKey = "help-open";

    /// <summary>Catalog item the header toggle claims on hover (see UsHelpCatalog).</summary>
    private const string HelpDrawerHelpKey = "us/page-title/help-drawer";

    private const float TitleMinHeight = 30f;
    private const float CaptionHeight = 18f;
    private const float CaptionGap = 4f;
    private const float TextLeftInset = 2f;

    /// <summary>Gap between the reduced text band and the reserved toggle control.</summary>
    private const float ToggleGap = 8f;

    /// <summary>Floor of the reserved control area, so a short translation never shrinks the hit target.</summary>
    private const float ToggleMinWidth = 72f;

    /// <summary>Height of the reserved control area (the title band is at least 30).</summary>
    private const float ToggleHeight = 26f;

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
        bindings.ValidateValue<string>(UiBindings.ActiveTabKey, elementPath);
        // The toggle is this widget's one write: the binding must exist and be a bool, or the header
        // would ship a control that silently does nothing.
        bindings.ValidateValue<bool>(HelpOpenKey, elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        (string title, string caption) = ResolveHeading(ctx);
        string toggleLabel = UsKernelDraw.Keyed(ctx, HelpToggleKey);
        float textWidth = TextBandWidth(ctx.ViewWidth, ToggleWidth(ctx, toggleLabel));
        return TitleBand(textWidth, ctx, title) + CaptionGap + CaptionBand(textWidth, ctx, caption);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        (string title, string caption) = ResolveHeading(ctx);
        string toggleLabel = UsKernelDraw.Keyed(ctx, HelpToggleKey);
        float toggleWidth = ToggleWidth(ctx, toggleLabel);

        // Measure and Draw share this ONE reduced width. The fit audit compares the band Measure
        // allocated against the band Draw painted, so the reserved control area must shorten BOTH
        // title and caption by exactly the same amount, or the audit reports a clipped heading.
        float textWidth = TextBandWidth(rect.width, toggleWidth);
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

        bool open = !ctx.Bindings.TryGet(HelpOpenKey, out bool bound) || bound;
        Rect toggle = ToggleRect(rect, titleHeight, toggleWidth);
        // Claim the toggle's own catalog entry while the pointer is over it, through the one outlet
        // every control uses (session-owned claim; the panel resolves it).
        UsKernelDraw.HelpHover(toggle, ctx, HelpDrawerHelpKey);
        if (UsKernelDraw.SelectionButton(toggle, ctx, toggleLabel, ctx.Theme, open))
        {
            // Value-binding write (not the action): the setter routes through the business boundary
            // and advances the session revision, which is what re-arranges onto the other variant.
            ctx.Bindings.Set(HelpOpenKey, !open);
        }
    }

    /// <summary>The shared text band: the widget's width minus the left inset, the reserved control and the gap.</summary>
    private static float TextBandWidth(float fullWidth, float toggleWidth)
    {
        return Math.Max(1f, fullWidth - TextLeftInset - toggleWidth - ToggleGap);
    }

    /// <summary>
    /// Reserved control width: the measured label plus the selection button's own side insets, floored
    /// so a short translation still leaves a usable target. Measured at the drawn font.
    /// </summary>
    private static float ToggleWidth(UiWidgetContext ctx, string label)
    {
        float labelWidth = ctx.Metrics.MeasureWidth(label, UiFont.Tiny)
            + UsKernelDraw.SelectionButtonLabelInset * 2f;
        return Math.Max(ToggleMinWidth, labelWidth);
    }

    /// <summary>The reserved control rect: right-aligned in the band, vertically centered on the title line.</summary>
    private static Rect ToggleRect(Rect rect, float titleHeight, float toggleWidth)
    {
        float width = Math.Min(toggleWidth, Math.Max(1f, rect.width - TextLeftInset));
        float height = Math.Min(ToggleHeight, Math.Max(1f, rect.height));
        float top = rect.y + Math.Max(0f, (titleHeight - height) / 2f);
        return new Rect(rect.xMax - width, top, width, height);
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
        ctx.Bindings.TryGet(UiBindings.ActiveTabKey, out string activeTab);
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
