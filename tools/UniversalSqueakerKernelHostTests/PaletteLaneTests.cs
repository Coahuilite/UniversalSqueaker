using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The palette guard (S6-3 step 3). Two properties, both of them about what a RE-TINT can silently break:
///
/// <list type="number">
/// <item><b>The flat scope stays flat.</b> "No box" is expressed by a surface's border token being the SAME
/// colour as its fill, so a re-tint that updates one and forgets the other brings every card outline back -
/// with nothing to report it. Every pair is asserted explicitly, on the resolved scoped theme, and a
/// one-sided change reddens.</item>
/// <item><b>Ink stays legible on its own surface.</b> A warm dark palette is exactly the case where a
/// secondary note turns to mud on a raised surface, so the two inks are checked against the surfaces they
/// are actually drawn on with a WCAG relative-luminance ratio. This is a regression GUARD with a number -
/// it does not claim the colours are tasteful.</item>
/// </list>
///
/// <para>NOT CLAIMED: the real screen. The stub renders no pixels, so the deltas live in the S6 report as
/// real-screen items.</para>
/// </summary>
internal static class PaletteLaneTests
{
    private const string FlatScheme = "us-flat-panel";

    /// <summary>Every (fill, edge) pair the flat scope must keep equal. A re-tint that moves one side of any
    /// of these re-draws a box.</summary>
    private static readonly (string Name, Func<UiTheme, Color> Fill, Func<UiTheme, Color> Edge)[] FlatPairs =
    {
        ("Raised/RaisedBorder", t => t.Raised, t => t.RaisedSurface.Border),
        ("Hover/HoverBorder", t => t.Hover, t => t.HoverSurface.Border),
        ("Selected/SelectedBorder", t => t.Selected, t => t.SelectedSurface.Border),
        ("WorkspacePlane/Raised", t => t.WorkspacePlane, t => t.Raised),
    };

    /// <summary>Minimum contrast for body ink on the surface it is painted on. 3.0 is a floor for a
    /// distinguishable ink, not a target: the shipped values sit far above it, so this reddens on a palette
    /// that turns a note to mud rather than on one that is merely unfashionable.</summary>
    private const double MinInkContrast = 3.0;

    public static int RunAll()
    {
        Step("the flat pairs are still equal after a re-tint", TheFlatPairsSurviveARetint);
        Step("ink stays legible on its own surface", InkStaysLegible);
        Console.WriteLine("PaletteLaneTests ALL PASS");
        return 0;
    }

    private static void TheFlatPairsSurviveARetint()
    {
        UiTheme page = UsTheme.Surface();
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        UiTheme flat = new UiStyleResolver(page, host.Manifest.Styles)
            .ThemeFor(new[] { new UiStyleDeclaration(FlatScheme) });

        foreach ((string name, Func<UiTheme, Color> fill, Func<UiTheme, Color> edge) in FlatPairs)
        {
            Assert(SameColor(fill(flat), edge(flat)),
                "the flat scope must keep '" + name + "' equal, or that surface paints a 1px box again: fill "
                + Hex(fill(flat)) + " vs edge " + Hex(edge(flat)) + ". A re-tint must move BOTH sides.");
        }

        // Control: on the page level the same pair is deliberately NOT equal (cards are boxed there), so the
        // assertion above is measuring the scope rather than the palette's accidental sameness.
        Assert(!SameColor(page.Raised, page.RaisedSurface.Border),
            "control: the page level must stay boxed, got raised " + Hex(page.Raised) + " == border "
            + Hex(page.RaisedSurface.Border));

        Console.WriteLine("[palette-flat] raised=" + Hex(flat.Raised) + " hover=" + Hex(flat.Hover)
            + " selected=" + Hex(flat.Selected) + " plane=" + Hex(flat.WorkspacePlane));
    }

    private static void InkStaysLegible()
    {
        UiTheme theme = UsTheme.Surface();
        // The four inks against the surfaces they are actually painted on.
        (string Name, Color Ink, Color Surface)[] cases =
        {
            ("primary on panel", theme.TextPrimary, theme.Panel),
            ("primary on raised", theme.TextPrimary, theme.Raised),
            ("secondary on panel", theme.TextSecondary, theme.Panel),
            ("secondary on raised", theme.TextSecondary, theme.Raised),
            ("active ink on selected", theme.TextOnGold, theme.Selected),
        };

        foreach ((string name, Color ink, Color surface) in cases)
        {
            double ratio = Contrast(ink, surface);
            Console.WriteLine("[palette-ink] " + name + " ink=" + Hex(ink) + " surface=" + Hex(surface)
                + " contrast=" + ratio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            Assert(ratio >= MinInkContrast,
                name + ": the ink must stay distinguishable from its own surface - contrast "
                + ratio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " is below the "
                + MinInkContrast.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                + " floor (ink " + Hex(ink) + " on " + Hex(surface) + ")");
        }
    }

    /// <summary>WCAG relative-luminance contrast ratio. Plain arithmetic on the channel values - the wheel
    /// is not reinvented, it is quoted.</summary>
    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a);
        double lb = Luminance(b);
        double high = Math.Max(la, lb);
        double low = Math.Min(la, lb);
        return (high + 0.05) / (low + 0.05);
    }

    private static double Luminance(Color color)
    {
        return 0.2126 * Channel(color.r) + 0.7152 * Channel(color.g) + 0.0722 * Channel(color.b);
    }

    private static double Channel(float value)
    {
        double c = Mathf.Clamp01(value);
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static bool SameColor(Color a, Color b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private static string Hex(Color color)
    {
        return "#" + Ch(color.r) + Ch(color.g) + Ch(color.b) + Ch(color.a);
    }

    private static string Ch(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 255f).ToString("X2");
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("PaletteLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
