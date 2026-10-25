using System;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Scroll-owned help content. Explicit pinned topics take precedence; unpinned consumers retain
/// the session's effective hover reference and fifteen-pass grace. Only the displayed text is
/// translated/measured, with the same width and font in Measure and Draw, never catalog maxima.
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
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, Kind, () => new UsHelpPanelWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Tab"), new UiAttributeDefinition("Hidden", UiAttributeKind.Boolean), new UiAttributeDefinition("TitleHidden", UiAttributeKind.Boolean))));
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<string>("help-section-ref", elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        float textWidth = Math.Max(1f, ctx.ViewWidth - Padding * 2f);
        var display = ResolveFrameDisplay(ctx, CurrentSection(ctx));
        float header = TitleHidden ? 0 : Math.Max(TitleMinHeight, Band(ctx, HeaderText(ctx, display), textWidth, UiFont.Small)) + TitleGap + ContentGap;
        return Padding * 2f + header + Band(ctx, display.LabelText, textWidth, UiFont.Small)
            + 2f + Band(ctx, display.BodyText, textWidth, UiFont.Tiny) + 4f;
    }

    private bool TitleHidden => spec.TryGetAttribute("TitleHidden", out string raw)
        && string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The one header-text outlet. The header is a Keyed format string wrapping the active title,
    /// so its length is data: Measure and Draw must both take it from here or the band gets
    /// sized for one string while another is drawn.
    /// </summary>
    private static string HeaderText(UiWidgetContext ctx, UsHelpPanelLogic.HelpPanelDisplay display)
    {
        return string.Format(UsKernelDraw.Keyed(ctx, HeaderFormatKey), display.TitleText);
    }

    private static float Band(UiWidgetContext ctx, string text, float textWidth, UiFont font)
    {
        return Math.Max(1f, ctx.Metrics.MeasureText(text, font, textWidth));
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        UiThemeDraw.Panel(ctx.Drawing, rect, ctx.Theme);

        HelpSection? section = CurrentSection(ctx);
        UsHelpPanelLogic.HelpPanelDisplay display = ResolveFrameDisplay(ctx, section);

        float x = rect.X + Padding;
        float y = rect.Y + Padding;
        float textWidth = Math.Max(1f, rect.Width - Padding * 2f);

        if (!TitleHidden)
        {
            string header = HeaderText(ctx, display);
            UiLayoutRect headerRect = new(x, y, textWidth, Math.Max(TitleMinHeight, Band(ctx, header, textWidth, UiFont.Small)));
            UiThemeDraw.SectionHeader(ctx.Drawing, headerRect, header, ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], UiFont.Small);
            y += headerRect.Height + TitleGap + ContentGap;
        }
        float labelHeight = Band(ctx, display.LabelText, textWidth, UiFont.Small);
        UsKernelDraw.Label(ctx.Drawing,
            new UiLayoutRect(x, y, textWidth, labelHeight),
            display.LabelText,
            ctx.Theme,
            ctx.Theme[UiPaletteRole.Foreground],
            UiFont.Small,
            UiTextAnchor.UpperLeft);
        y += labelHeight + 2f;
        UsKernelDraw.Label(ctx.Drawing,
            new UiLayoutRect(x, y, textWidth, Band(ctx, display.BodyText, textWidth, UiFont.Tiny)),
            display.BodyText,
            ctx.Theme,
            ctx.Theme[UiPaletteRole.SecondaryForeground],
            UiFont.Tiny,
            UiTextAnchor.UpperLeft);
    }

    /// <summary>The section this panel is currently falling back to (null when none is active).</summary>
    private static HelpSection? CurrentSection(UiWidgetContext ctx)
    {
        string sectionKey = ctx.Bindings.TryGet("help-section-ref", out string key) ? key : "";
        UsHelpCatalog.TryGetSection(sectionKey, out HelpSection section);
        return section;
    }

    /// <summary>Reads the session's effective help reference, including its 15-pass grace.
    /// CurrentHelpRef is a catalog reference, not a binding or a raw current-frame hit.</summary>
    private static UsHelpPanelLogic.HelpPanelDisplay ResolveFrameDisplay(UiWidgetContext ctx, HelpSection? section)
    {
        string current = ctx.Bindings.TryGet("pinned-help-ref", out string pinned) && pinned.Length > 0
            ? pinned : ctx.Session.CurrentHelpRef ?? "";
        return UsHelpPanelLogic.Resolve(section, current, TranslationSeam(ctx));
    }

    /// <summary>
    /// The seam handed to the Verse-free panel logic: catalog and panel strings are Keyed entry
    /// names, and Measure and Draw both resolve through it, so a measured height
    /// can never belong to a different string than the one drawn.
    /// </summary>
    private static Func<string, string> TranslationSeam(UiWidgetContext ctx)
    {
        return key => UsKernelDraw.Keyed(ctx, key);
    }
}
