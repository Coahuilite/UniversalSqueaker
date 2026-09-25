using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// US-owned row-state surface: the FILL and the 3px left rail a SELECTED domain row must show, painted
/// from the row own bool. Draw only, no hit: it takes no input, claims no hover and declares no hit area,
/// so it cannot steal a press from the row hit band.
///
/// <para>
/// WHY A KIND, measured before it was written:
/// <list type="number">
/// <item><b>The RAIL has no declarative shape.</b> chrome/rule paints a HORIZONTAL hairline only and a
/// container chrome is a whole filled band, so "a 3px strip down this element left edge" is not
/// expressible - the same boundary us/section-header recorded for the section rail.</item>
/// <item><b>The FILL has no declarative carrier on this row.</b> text/wrapped paints no surface, and the
/// row only surface-capable element is the hit button, whose Chrome="none" is exactly what makes it
/// appearance-less (G3). Letting the HIT paint itself was tried in S4-2 and REFUSED because it MOVED the
/// hit band geometry at 320px (DeclarativePacksLaneTests reddened); a sibling that paints and does not hit
/// keeps the band exactly where it was.</item>
/// </list>
/// </para>
///
/// <para>
/// The colours are the NAVIGATION own selected family: StatusTreatment(Active) for the fill and
/// AccentGold for the rail, so a rail means "this is the current thing" with ONE width and ONE accent
/// everywhere in the window (the nav card rail and the section header rail are the same 3px and the same
/// accent). Naming the accent explicitly is deliberate: the flat scope aliases SelectedSurface ??
/// AccentGold away, which is how the first section rail came out #3A311F (measured, kept in that widget).
/// </para>
///
/// <para>
/// The rail is drawn HERE because the carrier cannot do per-edge strokes yet (FL task-11: chrome accepts
/// only default or none, all four sides or nothing). When per-edge strokes land, this kind is the thing to
/// retire: the fill can become a container state and the rail a one-sided edge.
/// </para>
/// </summary>
public sealed class UsSelectionSurfaceWidget : IUiWidget
{
    public const string Kind = "us/selection-surface";

    /// <summary>The same 3px the navigation rail and the section-header rail use: one rule, one width.</summary>
    public const float RailWidth = 3f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsSelectionSurfaceWidget(),
            new[] { "Id", "Kind", "Bind", "Height", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec value)
    {
        spec = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        bindings.ValidateValue<bool>(BindingKey(), elementPath);
    }

    /// <summary>Contributes nothing to the row content reference: the manifest declares
    /// Height="MatchContent" and this element must not influence that reference (the text column is the
    /// reference), so the overlay takes the text height and this surface follows it.</summary>
    public float Measure(UiWidgetContext ctx)
    {
        return 0f;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        // TryGetBool, not TryGet<bool>: the row's 'selected' is a READ-ONLY registration, and the fail-soft
        // bool query is the one the carrier's own SelectedKey resolution uses (DeclarativePacksLaneTests reads
        // the same key the same way). Measured: with TryGet<bool> the surface was arranged with the row's exact
        // rect - 524x68.67 at (12,518) - and painted NOTHING, because the read answered false.
        if (!ctx.Bindings.TryGetBool(BindingKey(), out bool selected) || !selected) return;

        // The SHIPPED selected-row treatment, reused rather than re-invented: RowSurface paints the whole
        // plane first (theme.Selected for the selected rail state) and the rails LAST, so the plane's own
        // edge cannot paint over the rail. Painting the two by hand in the other order is the one way to get
        // this visibly wrong, which is why the helper is called instead of its two primitives.
        //
        // The rail is RowRail.Selected's ink (theme.TextSecondary), not the accent: the accent rail means
        // "the current object" - the navigation card's active tab - and the two facts are deliberately kept
        // apart (UsKernelDraw.RowSurface's own contract). Hover is passed false: T21 is the SELECTED state;
        // the row's hover treatment is a separate item and nothing is claimed for it here.
        UsKernelDraw.RowSurface(rect, ctx.Theme, hovered: false, selected: true);
    }

    private string BindingKey()
    {
        return spec.TryGetAttribute("Bind", out string bind) && bind.Length > 0 ? bind : spec.Id;
    }
}
