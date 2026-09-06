using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Base for kernel-owned US section composites. It owns the three shared behaviors of every
/// settings section:
///  - Tab visibility: a <c>Tab</c> attribute (Basic/Tuning/Packs) hides the section (Measure = 0 and
///    no Draw) unless it matches the session "active-tab" value binding;
///  - card frame + title from <c>TitleKey</c>/<c>Title</c> + help-selection accent border;
///  - session-level fallback: Measure/Draw exceptions trip the session slot and draw a stable
///    fallback instead of corrupting the frame.
/// </summary>
public abstract class UsSectionWidgetBase : IUiWidget
{
    private UiElementSpec spec = UiElementSpec.Empty;

    /// <summary>Keyed format string of the tripped-section caption; <c>{0}</c> is the card title.</summary>
    private const string FallbackNoteKey = "US.Card.FallbackNote";

    string IUiWidget.Kind => Kind;

    public abstract string Kind { get; }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public abstract void Validate(IUiBindings bindings, string elementPath);

    public float Measure(UiWidgetContext ctx)
    {
        if (!IsVisible(ctx)) return 0f;
        return UiSessionGuard.MeasureOrFallback(
            ctx.Session,
            spec.Id,
            Kind,
            ctx.ElementPath,
            CardHeight(FallbackHeight(ctx)),
            () => CardHeight(MeasureBody(ctx)));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        if (!IsVisible(ctx)) return;

        string elementId = spec.Id;
        UiSessionGuard.DrawOrFallback(
            ctx.Session,
            elementId,
            Kind,
            ctx.ElementPath,
            rect,
            () => DrawBody(rect, ctx),
            fallback => DrawCard(fallback, ctx, body =>
                UsKernelDraw.Label(body, FallbackNote(ctx), ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny)));
    }

    /// <summary>True when the section's Tab attribute (if any) matches the active-tab binding.</summary>
    protected bool IsVisible(UiWidgetContext ctx)
    {
        if (!spec.TryGetAttribute("Tab", out string tab) || tab.Trim().Length == 0) return true;
        string active = ctx.Bindings.TryGet("active-tab", out string current) ? current : "";
        return string.Equals(tab.Trim(), active, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The one card-title outlet: a non-empty <c>TitleKey</c> attribute resolves through the Host
    /// translation seam, otherwise the literal <c>Title</c> attribute is used, otherwise the widget
    /// Kind. Measure and Draw both take the title from here, so a section never sizes one string and
    /// draws another.
    /// </summary>
    protected string SectionTitle(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("TitleKey", out string key) && key.Trim().Length > 0)
        {
            return ctx.Translation.Translate(key.Trim());
        }

        return spec.TryGetAttribute("Title", out string title) ? title : Kind;
    }

    /// <summary>
    /// Caption drawn instead of the body once the session guard has tripped this section. The title is
    /// injected into the keyed format string instead of concatenated, so a language that puts the note
    /// before the title may do so; the title itself still comes from <see cref="SectionTitle"/>, the
    /// single title outlet.
    /// </summary>
    private string FallbackNote(UiWidgetContext ctx)
    {
        return string.Format(UsKernelDraw.Keyed(ctx, FallbackNoteKey), SectionTitle(ctx));
    }

    /// <summary>
    /// Whether the help panel is currently explaining this section (drives the accent border).
    /// After the D2 ruling retired pinned selection, the border follows the live hover claim: a
    /// card gets the border while a claim resolving to one of its entries is held, so "what the
    /// panel is talking about" stays visible on the content column too.
    /// </summary>
    protected bool IsHelpSelected(UiWidgetContext ctx)
    {
        if (!spec.TryGetAttribute("HelpKey", out string helpKey) || helpKey.Trim().Length == 0) return false;
        string hover = ctx.Bindings.TryGet("help-hover", out string current) ? current : "";
        return hover.Length > 0 && hover.StartsWith(helpKey.Trim() + "/", StringComparison.Ordinal);
    }

    /// <summary>Fallback body height used when Measure throws.</summary>
    protected virtual float FallbackHeight(UiWidgetContext ctx)
    {
        return 64f;
    }

    /// <summary>
    /// Width of the card body that <see cref="DrawCard"/> hands to the content callback. Measure
    /// must use this same width (ViewWidth minus card side padding) so narrow-layout branches and
    /// text-wrap heights match between Measure and Draw.
    /// </summary>
    protected float BodyWidth(UiWidgetContext ctx)
    {
        return Math.Max(1f, ctx.ViewWidth - UsCardLayout.Padding * 2f);
    }

    /// <summary>Includes the same card chrome that DrawCard consumes.</summary>
    private static float CardHeight(float bodyHeight)
    {
        return UsCardLayout.MeasureBody(bodyHeight, titleHidden: false);
    }

    /// <summary>
    /// Measures the natural body height (card frame excluded) for the current context. Returns 0
    /// for sections whose dynamic content is empty.
    /// </summary>
    protected abstract float MeasureBody(UiWidgetContext ctx);

    /// <summary>Draws the section card; exceptions here are caught by the session guard.</summary>
    protected abstract void DrawBody(Rect rect, UiWidgetContext ctx);

    /// <summary>Draws the card frame around a body draw callback (title + help border).</summary>
    protected void DrawCard(Rect rect, UiWidgetContext ctx, Action<Rect> drawBody)
    {
        UsKernelDraw.DrawHelpFocusBorder(rect, ctx.Theme, IsHelpSelected(ctx));
        Rect bodyRect = UsKernelDraw.DrawCard(rect, SectionTitle(ctx), ctx.Theme);
        drawBody(bodyRect);
    }
}
