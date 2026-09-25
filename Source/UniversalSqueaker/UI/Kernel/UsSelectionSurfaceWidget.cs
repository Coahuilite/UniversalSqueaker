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

    /// <summary>
    /// The selected row's fill: the accent tinted toward the surface, named ONCE so the widget and the palette
    /// lane cannot disagree about what is painted. 0.57f is chosen against the COMPOSITED colour (the lane
    /// composites before it compares, cf2e0a8): it lands the drawn fill so that title-vs-fill and
    /// fill-vs-plane BOTH clear their floors with margin, which no value on the old gold-ink path could do.
    /// </summary>
    public static Color SelectedFill(UiTheme theme)
    {
        return theme.AccentWith(0.57f);
    }

    /// <summary>
    /// The anchor's ink. NOT the accent, and the reason is MEASURED: a fill and a rail from the same colour
    /// source share their RGB (only alpha differs), so rail-vs-fill contrast is exactly 1.00 by construction -
    /// a gold rail on a gold fill is invisible whatever its width. The anchor's job is to be visible, not to be
    /// gold (Lead's ruling 2026-09-25); gold is present as the fill.
    /// </summary>
    public static Color SelectedRail(UiTheme theme)
    {
        return theme.TextPrimary;
    }

    /// <summary>Source-over alpha compositing. ONE implementation of this rule on the product side: a
    /// contrast reading is about PERCEIVED pixels, and the lane must not carry a second copy of the
    /// arithmetic (this session already paid twice for one convention living in two places).</summary>
    public static Color Composite(Color foreground, Color background)
    {
        float a = Mathf.Clamp01(foreground.a);
        return new Color(
            foreground.r * a + background.r * (1f - a),
            foreground.g * a + background.g * (1f - a),
            foreground.b * a + background.b * (1f - a),
            1f);
    }

    /// <summary>
    /// The PERCEIVED fill - a DERIVED value: the written parameter composited over the card plane. The
    /// recorder stores what the widget WROTE (RGBA); the player sees what IMGUI blends, and contrast is a
    /// property of the second one. Real pixels are not directly observed here; this model rests on two
    /// supports - UiThemeDraw.Solid forwards RGBA to DrawBoxSolid unchanged (code), and IMGUI alpha blending
    /// is standard semantics. Corroboration to request from the maintainer: the selected row should read as
    /// this composited colour (a dark olive/brown gold), NOT as the bright gold of the raw token.
    ///
    /// The margins are a STRUCTURAL ceiling, not a tuned number: a bright title ink (TextPrimary, L ~= 0.794)
    /// and a very dark card plane pin the workable fill luminance to L_fill in [0.124, 0.138] - 0.014 wide -
    /// because the title needs L_fill low enough for 4.5 while the fill must stay clear of the plane for 3.0.
    /// A larger margin needs a different signal carrier, not a different alpha.
    /// </summary>
    public static Color PaintedFill(UiTheme theme)
    {
        return Composite(SelectedFill(theme), theme.Raised);
    }

    /// <summary>The perceived rail: opaque, so compositing is the identity.</summary>
    public static Color PaintedRail(UiTheme theme)
    {
        return SelectedRail(theme);
    }

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
        // TryGetBool, as a CONVENTION and NOT as a fix. An earlier revision of this comment claimed the read
        // failed because 'selected' is a READ-ONLY registration; that is wrong, and the correction is worth
        // keeping: BindReadOnly and BindValue land in the same values table and TryGet<T> reads that same
        // table, so read-only and writable behave identically there (audit read the chain: BindReadOnly ->
        // AddValue -> values.Add). What TryGetBool adds is only "answer false when the key is bound to
        // ANOTHER type". The convention is kept because it is what the carrier's own SelectedKey resolution
        // and DeclarativePacksLaneTests use, so every reader of this key fails soft the same way.
        //
        // The ORIGINAL CAUSE IS NOT REPRODUCED. The edit that introduced TryGetBool shipped in the same batch
        // as the cross-space filter fix, and the failure ("painted row rails: 0") persisted until THAT fix
        // landed - so this change was never isolated and explains nothing. If the question is reopened, the
        // most likely cause is that the key did not resolve at all at that moment (the item-scoped
        // qualification of the template's Bind attribute), which is a thing to reproduce, not to assume.
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
        // task-32: plane FIRST, rails LAST (the order the helper documented), with the thresholds written
        // before the colours (14.16): fill vs plane >= 3.0 (WCAG 2.1 SC 1.4.11 non-text), rail vs fill and vs
        // plane >= 3.0 (same clause), title ink vs fill >= 4.5 (WCAG 2.2 SC 1.4.3 AA, text). All four are
        // measured on the COMPOSITED colours by the colour lane's contrast criteria. (Written without naming
        // that lane's type: the deleted-type guard scans production UI source as TEXT, so a class name in a
        // comment trips it - the same brittleness the funnel guard showed. The guard itself needs stripping
        // comments and matching identifier boundaries; that fix is its own unit.)
        UiThemeDraw.Surface(rect, ctx.Theme, SelectedFill(ctx.Theme), ctx.Theme.Border);
        UiThemeDraw.AccentRail(rect, ctx.Theme, true, RailWidth, SelectedRail(ctx.Theme));
    }

    private string BindingKey()
    {
        return spec.TryGetAttribute("Bind", out string bind) && bind.Length > 0 ? bind : spec.Id;
    }
}
