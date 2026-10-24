using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// US-owned row-state surface: the FILL and the 3px left rail a SELECTED domain row must show, painted
/// from the row's own bool, plus - since SA1.7 and only where the element declares <c>Hover="true"</c> -
/// the pointer's hover plate on the navigation's own ladder. Draw only, no hit: it takes no input,
/// claims no press and declares no hit area, so it cannot steal a click from the row's hit band.
///
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
    /// The selected row's fill: the palette's DECLARED Selected role (OVT-004, PM selection-surface
    /// rule). The old bright-accent alpha fill behind body text is RETIRED - its arithmetic could
    /// not carry 4.5:1 body contrast on the authored grounds - and every table answers with its own
    /// authored Selected surface, the same role UsKernelDraw.RowSurface already paints for selected
    /// rows: one declared answer, no second derived fill, no threshold moved. Named ONCE so the
    /// widget and the palette lanes cannot disagree about what is painted.
    /// </summary>
    public static UiColour SelectedFill(UiTheme theme)
    {
        return theme[UiPaletteRole.SelectedSurface];
    }

    /// <summary>
    /// The anchor's ink. NOT the accent, and the reason is MEASURED: a rail drawn from the same colour
    /// source as its ground reads at contrast 1.00 by construction and is invisible whatever its width.
    /// Since OVT-004 the fill is the declared Selected surface (not a gold alpha tint), and the anchor
    /// rides the palette's own primary ink - the two are different tokens by design, and the theme lane
    /// measures the pair on the real draw.
    /// </summary>
    public static UiColour SelectedRail(UiTheme theme)
    {
        return theme[UiPaletteRole.Foreground];
    }

    /// <summary>Source-over alpha compositing. ONE implementation of this rule on the product side: a
    /// contrast reading is about PERCEIVED pixels, and the lane must not carry a second copy of the
    /// arithmetic (this session already paid twice for one convention living in two places).</summary>
    public static UiColour Composite(UiColour foreground, UiColour background)
    {
        float a = Mathf.Clamp01(foreground.Alpha);
        return new UiColour(
            foreground.Red * a + background.Red * (1f - a),
            foreground.Green * a + background.Green * (1f - a),
            foreground.Blue * a + background.Blue * (1f - a),
            1f);
    }

    /// <summary>
    /// The PERCEIVED fill. The declared Selected role is OPAQUE, so the source-over composite is
    /// the identity - the formula stays (the recorder stores what the widget WROTE, the player
    /// sees what IMGUI blends, and contrast is a property of the second one; keeping the ONE
    /// composite implementation honest is what lets the lane compare against the real draw).
    /// Under the retired accent-alpha fill this composite had to thread a 0.014-wide luminance
    /// window; under the authored surface the contrast is a property of the table itself, verified
    /// per table by the palette-contrast lane over these exact perceived colours.
    /// </summary>
    public static UiColour PaintedFill(UiTheme theme)
    {
        return Composite(SelectedFill(theme), theme[UiPaletteRole.ControlSurface]);
    }

    /// <summary>The perceived rail: opaque, so compositing is the identity.</summary>
    public static UiColour PaintedRail(UiTheme theme)
    {
        return SelectedRail(theme);
    }

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, Kind, () => new UsSelectionSurfaceWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Bind", binding: UiBindingRole.Value), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("Tab"), new UiAttributeDefinition("Hidden", UiAttributeKind.Boolean), new UiAttributeDefinition("Hover", UiAttributeKind.Boolean))));
    }

    public void Configure(UiElementSpec value)
    {
        spec = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        // The SELECTED state is a bound bool; the SA1.7 HOVER plate needs no binding (it reads the
        // pointer), so a surface that declares only Hover="true" validates nothing. A Bind, when
        // present, must still resolve as a bool.
        if (spec.TryGetAttribute("Bind", out string bind) && bind.Length > 0)
        {
            bindings.ValidateValue<bool>(bind, elementPath);
        }
    }

    /// <summary>Contributes nothing to the row content reference: the manifest declares
    /// Height="MatchContent" and this element must not influence that reference (the text column is the
    /// reference), so the overlay takes the text height and this surface follows it.</summary>
    public float Measure(UiWidgetContext ctx)
    {
        return 0f;
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;
        // TryGetBool, as a CONVENTION and NOT as a fix. An earlier revision of this comment claimed the read
        // failed because 'selected' is a READ-ONLY registration; that is wrong, and the correction is worth
        // keeping: BindReadOnly and BindValue land in the same values table and TryGet<T> reads that same
        // table, so read-only and writable behave identically there (audit read the chain: BindReadOnly ->
        // AddValue -> values.Add). What TryGetBool adds is only "answer false when the key is bound to
        // ANOTHER type". The convention is kept because it is what the carrier's own SelectedBind resolution
        // and DeclarativePacksLaneTests use, so every reader of this key fails soft the same way.
        //
        // The ORIGINAL CAUSE IS NOT REPRODUCED. The edit that introduced TryGetBool shipped in the same batch
        // as the cross-space filter fix, and the failure ("painted row rails: 0") persisted until THAT fix
        // landed - so this change was never isolated and explains nothing. If the question is reopened, the
        // most likely cause is that the key did not resolve at all at that moment (the item-scoped
        // qualification of the template's Bind attribute), which is a thing to reproduce, not to assume.
        bool selected = ctx.Bindings.TryGetBool(BindingKey(), out bool bound) && bound;
        if (selected)
        {
            // The SHIPPED selected-row treatment (OVT-004 re-cut): plane FIRST, rails LAST. The fill
            // is the palette's DECLARED Selected surface (the old gold alpha tint is retired); the
            // anchor rail rides SelectedRail (the palette's primary ink). The thresholds are unchanged
            // and measured on the REAL recorded draw by the theme lane: body ink vs the ground under
            // it >= 4.5, the drawn identity marks vs their adjacent ground >= 3.0. The Selected-vs-
            // Raised plane difference is deliberately NOT gated (PM ruling: <3 there is legal - the
            // identity rides the marks, not the background delta).
            UiThemeDraw.Surface(ctx.Drawing, rect, ctx.Theme, SelectedFill(ctx.Theme), ctx.Theme[UiPaletteRole.Border]);
            UiThemeDraw.AccentRail(ctx.Drawing, rect, ctx.Theme, true, RailWidth, SelectedRail(ctx.Theme));
            return;
        }

        // SA1.7 (user: the voice-pack list lacks the hover highlight the navigation cards already have):
        // an OPT-IN hover plate - the SAME ladder the nav card paints on hover (HoverSurface.Fill with
        // the BorderStrong structural edge), so a row under the pointer reads the same everywhere in the
        // window. Declared per element with Hover="true"; a surface that does not declare it paints
        // nothing when not selected, exactly as before. Still DRAW-ONLY: hover is READ, never taken - the
        // row's own hit band stays the only click channel, so browse-vs-enable semantics are untouched.
        if (spec.TryGetAttribute("Hover", out string hover)
                && string.Equals(hover, "true", StringComparison.OrdinalIgnoreCase)
                && ctx.Controls.IsMouseOver(rect))
        {
            UiThemeDraw.Surface(ctx.Drawing, rect, ctx.Theme, ctx.Theme.HoverSurface.Fill, ctx.Theme[UiPaletteRole.StrongBorder]);
        }
    }

    private string BindingKey()
    {
        return spec.TryGetAttribute("Bind", out string bind) && bind.Length > 0 ? bind : spec.Id;
    }
}
