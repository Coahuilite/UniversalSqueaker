using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// The square ON/OFF toggle the maintainer asked for: a 34x18 track with a 14x14 knob that slides from the
/// left (off) to the right (on). It draws its own geometry because the declarative vocabulary cannot.
///
/// <para>
/// <b>MEASURED, this step's citation (2026-09-22).</b> Two negatives, each with the reason:
/// <list type="number">
/// <item><b>There is no purely FILLED declarative element.</b> `chrome/banner` is a TEXT band (it colours
/// text, it fills nothing); `chrome/rule` paints a HORIZONTAL hairline only (`RuleWidget.cs:61-80`); a
/// container's chrome is a whole filled band with four edges. So the track's shape has no declarative form.</item>
/// <item><b>No vocabulary lets geometry or position depend on a bound value.</b> The engine-wide
/// value-dependent attributes are `Visible`/`VisibleKey`/`Hidden`/`SelectedKey` - visibility and STATE,
/// never shape (`AtomVocabulary.EngineWideAttributes`). "The knob sits left when false and right when true"
/// is therefore not expressible, which is the whole of what this kind adds.</item>
/// </list>
/// Applies to the SHAPE only. The COLOUR half is (A) and uses the existing role path: the element declares
/// `SelectedKey` naming the same bool as `Bind`, so `AtomVocabulary.ResolveRole` answers the Active
/// treatment while it is on (`AtomVocabulary.cs:221-234`) - the accent-filled Selected surface with the
/// accent edge and `TextOnGold` ink - and the plain neutral treatment while it is off. This kind resolves
/// nothing itself: it paints its own material (MATERIAL, NOT ROLE below), so a re-tint or a scoped scheme
/// moves the toggle with everything else.
/// </para>
///
/// <para>
/// <b>NOT a library request.</b> "Geometry depends on a bound value" has exactly one consumer - this
/// toggle's appearance - so it is recorded as a candidate with this step as its citation, not filed against
/// the carrier.
/// </para>
///
/// <para>
/// <b>Where it sits.</b> The track starts at the band's own left edge: the enclosing row already pads and
/// spaces its control column, so an inset here would double it. The track is the full 34px only when the
/// manifest gives the band at least that much - the declared band is 36x30, which is what makes the control
/// the size the reference is. A narrower band shrinks the track rather than overflowing it.
/// <b>Correction, S6-3 follow-up (2026-09-22):</b> this paragraph claimed a 36x30 band while the manifest
/// declared 24x30, so the track rendered 24x18 with a 6px throw instead of the reference's 34x18 and 16px.
/// The seven bands are 36 wide now and the claim is true again; it had been a doc defect for exactly as long
/// as the manifest disagreed with it.
/// </para>
///
/// <para>
/// <b>What it owns</b> (the same three things the checkbox atom owns, which is why it is a kind and not a
/// composite): the two-state bool read-back, the hit rule (the WHOLE arranged band, and the click writes the
/// inverse of what it just read), and the geometry it draws inside that band. The disabled treatment is not
/// a branch here either: writability comes from the one funnel, and a read-only bool paints disabled and
/// refuses the pointer.
/// </para>
/// </summary>
public sealed class UsSquareToggleWidget : IUiWidget
{
    public const string Kind = "us/square-toggle";

    /// <summary>Track and knob geometry, taken from the reference the design cites (SR's toggle).</summary>
    public const float TrackWidth = 34f;
    public const float TrackHeight = 18f;
    public const float KnobSize = 14f;

    /// <summary>Knob origin inside the track: 2px from the top, 2px from each end.</summary>
    private const float KnobInset = 2f;

    /// <summary>Gap between the track's right edge and the label.</summary>
    private const float LabelGap = 6f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UsKernelWidgetRegistrar.Scope,
            Kind,
            () => new UsSquareToggleWidget(),
            new[] { "Id", "Kind", "Bind", "ActionBind", "Label", "LabelKey", "Height", "Tab", "Hidden", "SelectedKey", "Tone", "Emphasis" },
            new[] { "Label", "LabelKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string key = ReadBindKey();
        if (key.Length == 0)
        {
            throw new InvalidOperationException(
                "UsSquareToggleWidget at '" + elementPath + "' requires a Bind naming the bool value binding it"
                + " shows; a toggle with no binding could never be read or written.");
        }

        bindings.ValidateValue<bool>(key, elementPath);

        string actionKey = ReadActionKey();
        if (actionKey.Length > 0)
        {
            bindings.ValidateCommand(actionKey, elementPath);
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        // One band per row, the theme's row height unless the manifest declares one. The label is single
        // line, so its text never moves the band - the same contract the checkbox atom has.
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return ctx.Theme.Geometry.RowHeight;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string key = ReadBindKey();
        bool state = ctx.Bindings.TryGetBool(key, out bool bound) && bound;

        // Writability through the public read side: a read-only bool paints the disabled treatment and
        // refuses the click. The command funnel's own verdict arrives on the node.
        bool? writable = ctx.Bindings.IsWritable(key) ? true : (bool?)null;
        if (ctx.Node != null && ctx.Node.IsDisabled) writable = false;

        // The interaction ladder is the atoms' own: armed beats hover.
        bool armed = UiNative.IsMouseDownOver(rect);
        bool hovered = !armed && UiNative.IsMouseOver(rect);

        // MATERIAL, not role. This control paints its own body from theme VALUES rather than asking the
        // role table what an on/off surface looks like, and that is a measured ruling rather than a
        // preference: the cards it lives in are the flat scope, which deliberately sets SelectedBorder equal
        // to Selected and RaisedBorder equal to Raised (that equality IS "a flat surface paints no box"), so
        // the role table's OFF treatment paints the track in exactly the colour of the plane behind it -
        // measured #191612 on #191612, i.e. the control disappears. A control that must be VISIBLE on a flat
        // plane cannot take its material from the role of the plane it sits on. The values are still the
        // theme's own (no literal, no new token), so a re-tint moves the toggle with everything else.
        UiSurfaceStyle track = Material(ctx.Theme, state, armed, hovered, writable);
        Color knobInk = KnobInk(ctx.Theme, state, writable);

        Paint(rect, track, knobInk, state, ctx);

        // The hit rule: the whole arranged band, exactly like the checkbox atom. Writability only gates the
        // write - a read-only toggle still draws, it simply refuses.
        if (writable == false) return;

        if (UiNative.Button(rect, ctx))
        {
            ctx.Bindings.Set<bool>(key, !state);
            string actionKey = ReadActionKey();
            if (actionKey.Length > 0)
            {
                ctx.Bindings.Invoke(actionKey);
            }
        }
    }

    /// <summary>
    /// The track's material for one state, from theme values only:
    /// <list type="bullet">
    /// <item><b>off</b>: the raised plane with the shared border edge - the "recessed track" the reference
    /// draws, and visibly NOT the flat card face because the edge is `Border`, which the flat scope
    /// deliberately does not alias to the fill.</item>
    /// <item><b>on</b>: the accent at a quarter alpha, edged in the SAME neutral 'Border' the off state
    /// uses. Measured against the reference (2026-09-22): its outline is one constant stroke in BOTH states
    /// and the state is told by the fill and the knob, so an accent edge here made the ON state say the same
    /// thing three times (fill, edge, knob) where the reference says it twice.</item>
    /// </list>
    /// Hover and armed restate the same pair with the hover step / the full accent, so the control's own
    /// state stays readable while the pointer moves over it. A disabled element keeps the off material: the
    /// refusal is the pointer's, and the value is still what the player sees.
    /// </summary>
    public static UiSurfaceStyle Material(UiTheme theme, bool state, bool armed, bool hovered, bool? writable)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        if (writable == false) return new UiSurfaceStyle(theme.Raised, theme.Border);
        if (state)
        {
            Color fill = armed ? theme.AccentGold : theme.AccentWith(AccentAlpha);
            return new UiSurfaceStyle(fill, theme.Border);
        }

        if (hovered)
        {
            return new UiSurfaceStyle(theme.HoverSurface.Fill, theme.BorderStrong);
        }

        return new UiSurfaceStyle(theme.Raised, theme.Border);
    }

    /// <summary>
    /// The knob's ink for one state, and the state's own signal: off it is the theme's light neutral over the
    /// dark track; on it is the ACCENT ITSELF at full alpha over the accent-washed fill. That is the
    /// reference's own contrast split, measured 2026-09-22 - its ON knob is the saturated gold, and the gold
    /// lives on the small moving part rather than on the whole perimeter. It stays distinguishable from the
    /// fill it sits in because that fill is the accent at a QUARTER alpha (asserted, not assumed:
    /// `UsSquareToggleLaneTests`).
    /// </summary>
    public static Color KnobInk(UiTheme theme, bool state, bool? writable)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        if (writable == false) return theme.TextDisabled;
        return state ? theme.AccentGold : theme.TextSecondary;
    }

    /// <summary>Alpha of the accent that fills the track while the toggle is on.</summary>
    public const float AccentAlpha = 0.25f;

    /// <summary>
    /// The track and the knob, in the row's own space. Public and pure so a lane asserts the SAME arithmetic
    /// the draw uses - the geometry is the half of this kind that has no declarative equivalent, and a lane
    /// that recomputed it would be measuring its own copy.
    /// </summary>
    public static void Geometry(Rect row, bool state, out Rect track, out Rect knob)
    {
        float side = Math.Min(TrackHeight, Math.Max(1f, row.height));
        track = new Rect(
            row.x,
            row.y + (row.height - side) * 0.5f,
            Math.Min(TrackWidth, Math.Max(1f, row.width)),
            side);

        float knobSize = Math.Min(KnobSize, Math.Max(1f, track.height - KnobInset * 2f));
        float knobY = track.y + KnobInset;
        float knobX = state
            ? track.xMax - KnobInset - knobSize
            : track.x + KnobInset;
        knob = new Rect(knobX, knobY, knobSize, knobSize);
    }

    private void Paint(Rect rect, UiSurfaceStyle track, Color knobInk, bool state, UiWidgetContext ctx)
    {
        UiTheme theme = ctx.Theme;
        Geometry(rect, state, out Rect trackRect, out Rect knob);

        // One paint outlet for the whole control: the track is the material above (fill + its edge) and the
        // knob is that state's ink. Nothing here is a literal, so a re-tint moves the toggle with everything
        // else.
        UiThemeDraw.Surface(trackRect, track, theme.Geometry.Hairline);
        UiThemeDraw.Solid(knob, knobInk);

        string label = ResolveLabel(ctx);
        if (label.Length == 0) return;
        float labelX = trackRect.xMax + LabelGap;
        float labelWidth = rect.xMax - labelX;
        if (labelWidth <= 1f) return;

        // The label is the page's text, not the control's material: it takes the theme's primary ink (or the
        // disabled ink), so a row's caption does not turn gold because its switch is on.
        UsKernelDraw.Label(
            new Rect(labelX, rect.y, labelWidth, rect.height),
            label,
            ctx,
            theme.TextPrimary,
            theme.DefaultFont,
            TextAnchor.MiddleLeft,
            singleLine: true);
    }

    /// <summary>The label through the one keyed-text outlet, or the literal, or nothing.</summary>
    private string ResolveLabel(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("LabelKey", out string key) && key.Trim().Length > 0)
        {
            return UsKernelDraw.Keyed(ctx, key.Trim());
        }

        return spec.TryGetAttribute("Label", out string literal) ? literal : "";
    }

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bind) && bind.Trim().Length > 0) return bind.Trim();
        return (spec.Id ?? "").Trim();
    }

    private string ReadActionKey()
    {
        return spec.TryGetAttribute("ActionBind", out string action) ? action.Trim() : "";
    }
}
