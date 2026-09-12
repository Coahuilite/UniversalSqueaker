using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace UniversalSqueaker;

/// <summary>
/// US's attention role - "a condition worth investigating" - as one hue behind one set of semantic
/// primitives. The ruling is `modding_documents/us-attention-color-ruling.md`: bright cyan
/// <c>#86E7D8</c> is US's attention foreground/marker, series gold stays the accent (where I am /
/// what is live) and the danger family stays reserved for destructive actions.
/// <para>
/// Why this type exists and is NOT a <see cref="UiTheme"/> token: FerriteLib's bag has no attention
/// slot, and the nearest name, <see cref="UiTheme.Warning"/>, is a compatibility redirect onto
/// <see cref="UiTheme.Danger"/> - assigning the cyan there would repaint every destructive control.
/// So the separation is carried US-side. <see cref="UsTheme"/> stays the single owner of the
/// surface table; this file is the single owner of the attention role.
/// </para>
/// <para>
/// Substrate rules from the ruling, applied by the draw helpers below:
/// <list type="bullet">
/// <item>a row rail is a thin cyan edge and the row fill stays neutral;</item>
/// <item>an attention badge is a cyan fill with dark ink <c>#0f1116</c> - light ink on this cyan is
/// about 1.20:1 and is never allowed;</item>
/// <item>an attention band defaults to no new tinted token: the existing neutral panel fill with a
/// cyan border;</item>
/// <item>a bare cyan dot is not a marker - <see cref="Marker"/> carries a shape so grayscale keeps
/// attention apart from the neutral effective-state dot.</item>
/// </list>
/// </para>
/// </summary>
public static class UsAttention
{
    /// <summary>The attention shape prefix. Primitive text, so no icon asset or font is needed.</summary>
    public const string Marker = "[!]";

    /// <summary>Bright cyan <c>#86E7D8</c>: the one attention foreground on US's dark planes.</summary>
    public static readonly Color Brush = Rgb(0x86, 0xE7, 0xD8);

    /// <summary>Dark ink <c>#0f1116</c> for text sitting ON <see cref="Brush"/>.</summary>
    public static readonly Color InkOnBrush = Rgb(0x0f, 0x11, 0x16);

    /// <summary>Neutral panel fill plus the cyan edge: the ruling's default treatment for an
    /// attention band, and deliberately not a new tinted background token.</summary>
    public static UiSurfaceStyle BandSurface(UiTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        return new UiSurfaceStyle(theme.Panel, Brush);
    }

    /// <summary>Thin edge width. Two pixels against the location rails' three: attention must not read as
    /// selection, and the width difference is what keeps the two apart even when hue is unavailable.</summary>
    public const float RailWidth = 2f;

    /// <summary>Thin cyan edge, drawn through the library's rail primitive.</summary>
    public static void Rail(Rect rect, UiTheme theme, float width = RailWidth)
    {
        UiThemeDraw.AccentRail(rect, theme, true, width, Brush);
    }

    /// <summary>Neutral fill with a cyan border, for a block summary or an unavailable-source band.</summary>
    public static void Band(Rect rect, UiTheme theme)
    {
        UiThemeDraw.Surface(rect, BandSurface(theme));
    }

    /// <summary>Cyan fill with dark ink - the only legal foreground on the attention fill.</summary>
    public static void Badge(Rect rect, string text, UiTheme theme, UiFont font = UiFont.Tiny)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        UiThemeDraw.Surface(rect, theme, Brush, Brush);
        UiThemeDraw.Label(
            new Rect(rect.x + 4f, rect.y, Mathf.Max(1f, rect.width - 8f), rect.height),
            text,
            theme,
            InkOnBrush,
            font,
            TextAnchor.MiddleCenter,
            singleLine: true);
    }

    private static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
