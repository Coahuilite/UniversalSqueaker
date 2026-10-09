using System;
using System.Globalization;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// US-owned section header: a 3px gold left rail, an optional geometric marker and the keyed title. It
/// is what US's declarative cards use in place of the carrier's `section/header`.
///
/// <para>
/// <b>Why a US kind, stated as it actually is.</b> Two reasons, and only one of them is a library defect:
/// <list type="number">
/// <item><b>The rail has no declarative shape.</b> `chrome/rule` paints a horizontal hairline only, and a
/// container's chrome is a whole filled band, so "a 3px strip down this element's left edge" cannot be
/// declared by anything in the vocabulary.</item>
/// <item><b>The carrier's bottom rule WAS un-declarable.</b> `SectionHeaderWidget` used to paint it
/// unconditionally; that was filed as a library defect and FIXED on the carrier side (FL `876750a`,
/// `Chrome="none"`), so a declarative header can now have no rule. This kind therefore does not exist to
/// work around it any more - if the rail is ever given up, the manifest can go back to declaring
/// `section/header Chrome="none"` and delete this file. What keeps it here today is the rail, which is
/// real, player-visible, and asked for.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Ask for the accent by name. This was measured twice, and the first answer was wrong because of an
/// explicit argument, not because of a helper.</b> `UiTheme.SelectedSurface` is
/// `(Selected, SelectedBorder ?? AccentGold)` (`UiTheme.cs:219-224`) and `SelectedBorder` IS assignable
/// from a style document (`UiStyleDocument.cs:516` -> `UiStyleResolver.cs:272`) - so when the flat scope
/// `us-flat-panel` sets it EQUAL to `Selected` (that equality is how a flat surface paints no box), the
/// `?? AccentGold` fallback is ALIASED AWAY and the border reads as the fill. Measured: the first rail was
/// passed `theme.SelectedSurface.Border` explicitly and came out `#3A311F`.
/// <see cref="UiThemeDraw.AccentRail"/> itself is not the trap - its own fallback is
/// `color ?? theme.AccentGold` (`UiThemeDraw.cs:150`), it never reads `SelectedSurface`, and calling it
/// WITHOUT a colour is safe. The lesson is the calling convention: **when you want the accent, name the
/// accent** - `theme.AccentGold`, the series colour the navigation rail uses and which is deliberately not
/// re-tinted.
/// </para>
///
/// <para>
/// Nothing in the style table resolves a gold INK, though: the only gold text path is `TextOnGold` on a
/// gold fill (the button's own pairing). The marker is therefore painted in `TextPrimary` because that is
/// the honest colour available - a gold ink role with no fill under it is a vocabulary addition with no
/// forced consumer yet, so it is recorded as a backlog candidate (citation: this step) rather than invented
/// here.
/// </para>
///
/// <para>
/// <b>Not a hit lane.</b> This header takes no input: it claims no click, it declares no hit area, and the
/// S4-2 hit seam is not involved. What it can be held to is geometry and existence, and that is what
/// `UsSectionHeaderLaneTests` asserts.
/// </para>
/// </summary>
public sealed class UsSectionHeaderWidget : IUiWidget
{
    public const string Kind = "us/section-header";

    /// <summary>The band the title line is drawn in when nothing declares a height.</summary>
    private const float DefaultHeight = 24f;

    /// <summary>
    /// Rail width. Three pixels is the series' location-rail width, deliberately the same number the
    /// navigation card's own rail uses (`UsNavWidget`): one rule, one width, so a rail means "this is
    /// the current thing" everywhere in the window. The attention rail is 2px precisely so it cannot be
    /// confused with either (`UsAttention.RailWidth`).
    /// </summary>
    public const float RailWidth = 3f;

    /// <summary>
    /// Left inset of the title, in the theme's own `Geometry.Padding`. The carrier's header drew its
    /// label from the band's own `rect.x`, and the card that arranges this element already pads that
    /// rect - so keeping this number identical is what makes the swap pixel-neutral for the title
    /// baseline, and the reason the card's height does not move.
    /// </summary>
    private const float TitleInset = 0f;

    /// <summary>Gap between the marker glyph and the title. Zero when no marker is drawn.</summary>
    private const float MarkerGap = 4f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsSectionHeaderWidget(),
            new[] { "Id", "Kind", "Title", "TitleKey", "HelpKey", "Tab", "Hidden", "Height" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        // A header carries a title, never a value: there is no binding to require. A missing TitleKey is a
        // legal (if empty) header rather than a creation-time failure - the carrier's header behaves the
        // same way, and refusing it here would be a contract this element never had.
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadHeight(ctx);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiTheme theme = ctx.Theme;

        // The rail is the section's whole edge. Painted from the theme's own accent token rather than a
        // literal, so a lane can assert the identity instead of a number. NOT SelectedSurface.Border: the
        // flat scope sets that equal to Selected (that is how a flat surface paints no box), so it is not
        // gold inside these cards - measured, recorded in the class comment.
        UiThemeDraw.AccentRail(rect, theme, true, RailWidth, theme.AccentGold);

        float left = rect.x + TitleInset;
        string title = ResolveTitle(ctx);
        float markerWidth = MarkerWidth(ctx);
        if (markerWidth > 0f)
        {
            UsKernelDraw.Label(
                new Rect(left, rect.y, markerWidth, rect.height),
                Marker,
                ctx,
                theme.TextPrimary,
                UiFont.Small,
                TextAnchor.MiddleLeft,
                singleLine: true);
            left += markerWidth + MarkerGap;
        }

        float textWidth = Math.Max(1f, rect.xMax - left);
        UsKernelDraw.Label(
            new Rect(left, rect.y, textWidth, rect.height),
            title,
            ctx,
            theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft);
    }

    /// <summary>
    /// The marker is a geometric glyph, and whether the font can draw it is asked through the same
    /// measurement seam that draws it - never the backend directly (`UsDiagGlyphs`' D7 rule). A font that
    /// cannot draw it gets NO marker rather than an empty box: the rail already carries the section, so the
    /// degradation is "less decoration", not "broken glyph".
    /// </summary>
    public const string Marker = "▶";

    /// <summary>The measured width of the marker band, or 0 when this font cannot draw it.</summary>
    private static float MarkerWidth(UiWidgetContext ctx)
    {
        if (!UsDiagGlyphs.Supports(Marker, ctx.Metrics, UiFont.Small)) return 0f;
        return Math.Max(1f, ctx.Metrics.MeasureWidth(Marker, UiFont.Small));
    }

    private string ResolveTitle(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("TitleKey", out string key) && key.Trim().Length > 0)
        {
            return ctx.Translation.Translate(key.Trim());
        }

        return spec.TryGetAttribute("Title", out string literal) ? literal : "";
    }

    private float ReadHeight(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }
}
