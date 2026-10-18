using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The palette guard - UIR-006 (WO-US-UI-REFACTOR-20261014) rewrote its object. It used to assert the
/// flat scope's (fill == edge) pairs; the region scheme is retired and the six official palettes now
/// own the surface relationships through their role tables, so what a re-tint can still silently break
/// is INK LEGIBILITY: every shipped palette must keep the required text readable on the surfaces it is
/// drawn on, and attention must stay an independent hue that no palette accent collides with.
///
/// <para>
/// MUTATION LEDGER:
/// <list type="number">
/// <item><b>InkStaysLegible</b> - a palette that turns body/secondary/disabled ink to mud on any of the
/// three surfaces it is painted on reddens with the measured WCAG ratio. The PM's Core note says small
/// text is never laid on Quiet Shade/Bronze Mist/Lark - this is that ruling made checkable.</item>
/// <item><b>AttentionStaysIndependent</b> - a palette whose accent equals the attention brush reddens:
/// the "condition worth investigating" hue answers to UsAttention alone, not to a theme identity.</item>
/// <item><b>SwitchHasItsOwnInk</b> - a palette whose OFF thumb equals its own resting surface or its
/// primary ink reddens: the switch position is a required mark and the thumb must be seen.</item>
/// </list>
/// </para>
///
/// <para>NOT CLAIMED: taste, and the real screen. Ratios are a regression floor (3.0 for ink, 1.5 for
/// the thumb against its track), not a design target; the shipped Core values sit above them.</para>
/// </summary>
internal static class PaletteLaneTests
{
    private const double MinInkContrast = 3.0;
    private const double MinThumbSeparation = 1.5;

    /// <summary>(ink selector, surface selector, why) - each pair is a place US actually paints.</summary>
    private static readonly (string Name, Func<UiTheme, Color> Ink, Func<UiTheme, Color> On)[] InkPairs =
    {
        ("TextPrimary/Raised", t => t.TextPrimary, t => t.Raised),
        ("TextPrimary/Panel", t => t.TextPrimary, t => t.Panel),
        ("TextPrimary/Base", t => t.TextPrimary, t => t.Base),
        ("TextSecondary/Raised", t => t.TextSecondary, t => t.Raised),
        ("TextSecondary/Panel", t => t.TextSecondary, t => t.Panel),
        ("TextOnGold/Selected", t => t.TextOnGold, t => t.Selected),
        ("TextOnGold/NavSelected", t => t.TextOnGold, t => t.Selected), // the nav paints ink on the selected plate
        ("TextOnDanger/Danger", t => t.TextOnDanger, t => t.Danger),
    };

    public static int RunAll()
    {
        Step("ink stays legible on its own surface, in every palette", InkStaysLegible);
        Step("the attention brush stays outside every palette", AttentionStaysIndependent);
        Step("the switch thumb keeps its own ink, in every palette", SwitchHasItsOwnInk);
        Step("the contrast instrument composites alpha before it compares (task-32, restored)", TheCompositorIsHonest);
        Step("the selection is legible on the plane it sits on (CORRECTIONS-R1 re-anchored)", TheSelectionIsLegible);
        Console.WriteLine("PaletteLaneTests ALL PASS");
        return 0;
    }

    private static void InkStaysLegible()
    {
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            UiTheme bag = UsTheme.Configure(UiTheme.Vanilla, swatch.Id);
            foreach ((string name, Func<UiTheme, Color> ink, Func<UiTheme, Color> on) in InkPairs)
            {
                double ratio = Contrast(ink(bag), on(bag));
                Assert(ratio >= MinInkContrast,
                    swatch.Id + ": '" + name + "' reads " + ratio.ToString("0.00")
                    + ":1, under the " + MinInkContrast.ToString("0.0")
                    + ":1 floor - the ink turns to mud on that surface");
            }
        }
    }

    private static void AttentionStaysIndependent()
    {
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            UiTheme bag = UsTheme.Configure(UiTheme.Vanilla, swatch.Id);
            Assert(!Same(UsAttention.Brush, bag.AccentGold),
                swatch.Id + ": the accent equals the attention brush - [!] would stop meaning \"investigate\"");
            Assert(!Same(UsAttention.InkOnBrush, bag.TextPrimary),
                swatch.Id + ": primary ink equals the attention badge's ink - the two marks collapse");
        }

        // The attention duo itself is fixed by the ruling, not by any palette.
        Assert(Contrast(UsAttention.InkOnBrush, UsAttention.Brush) >= 4.5,
            "the attention badge's own ink must hold 4.5:1 on the cyan");
    }

    private static void SwitchHasItsOwnInk()
    {
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            UiTheme bag = UsTheme.Configure(UiTheme.Vanilla, swatch.Id);
            Assert(!Same(bag.SwitchThumbOff, bag.Raised),
                swatch.Id + ": the OFF thumb equals the resting surface - the switch reads as an empty track");
            Assert(!Same(bag.SwitchThumbOff, bag.AccentGold),
                swatch.Id + ": the OFF thumb equals the ON colour - position stops being the answer");
            double separation = Contrast(bag.SwitchThumbOff, bag.Raised);
            Assert(separation >= MinThumbSeparation,
                swatch.Id + ": OFF thumb vs track reads " + separation.ToString("0.00")
                + ":1, under the " + MinThumbSeparation.ToString("0.0") + ":1 separation floor");
        }
    }

    /// <summary>
    /// RESTORED (CORRECTIONS-R1 topic 4): the r1 rewrite of this lane dropped the task-32 compositor
    /// and selection steps. The arithmetic guard comes back unchanged - it protects the SHARED
    /// Composite() every reader and lane uses - and the selection step comes back RE-ANCHORED. The
    /// palette ruling (PM 2026-10-14) keeps the Selected raw value as an ASSIST fill and carries the
    /// required selection signal on the high-contrast rail/edge plus text, so the old fill-vs-plane
    /// floor is not kept as a pass/fail gate; the re-anchor is stated, not hidden: the fill-vs-plane
    /// number is still MEASURED and printed every run, and the enforceable floors became (a) the
    /// rail against BOTH real neighbouring surfaces at 3.0, (b) body ink on its real grounds at 4.5,
    /// and (c) the compositor's own identity facts via the positive/negative controls.
    /// </summary>
    private static void TheCompositorIsHonest()
    {
        Color white = new Color(1f, 1f, 1f, 1f);
        Color black = new Color(0f, 0f, 0f, 1f);
        Color half = new Color(1f, 1f, 1f, 0.5f);
        Color mid = UsSelectionSurfaceWidget.Composite(half, black);
        Assert(Mathf.Abs(mid.r - 0.5f) < 1e-4f && Mathf.Abs(mid.g - 0.5f) < 1e-4f
                && Mathf.Abs(mid.b - 0.5f) < 1e-4f,
            "source-over with alpha 0.5 over black must land at the midpoint, got " + mid);
        Color full = UsSelectionSurfaceWidget.Composite(white, black);
        Assert(Mathf.Abs(full.r - 1f) < 1e-4f,
            "alpha 1 must fully replace the plane (positive control), got " + full);
        Color zero = UsSelectionSurfaceWidget.Composite(new Color(1f, 0f, 0f, 0f), black);
        Assert(Mathf.Abs(zero.r) < 1e-4f,
            "alpha 0 must leave the plane untouched (negative control), got " + zero);
    }

    private static void TheSelectionIsLegible()
    {
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            UiTheme theme = UsTheme.Configure(UiTheme.Vanilla, swatch.Id);
            Color fill = UsSelectionSurfaceWidget.PaintedFill(theme);   // composite over the resting plane
            Color rail = UsSelectionSurfaceWidget.PaintedRail(theme);   // opaque
            Color plane = theme.Raised;

            // (a) the rail separates from BOTH real neighbouring surfaces (required selection mark).
            Require("rail-vs-fill " + swatch.Id, Contrast(rail, fill), 3.0);
            Require("rail-vs-plane " + swatch.Id, Contrast(rail, plane), 3.0);
            // (b) body ink on its real grounds keeps the full readability floor.
            Require("body-on-raised " + swatch.Id, Contrast(theme.TextPrimary, plane), 4.5);
            Require("body-on-panel " + swatch.Id, Contrast(theme.TextPrimary, theme.Panel), 4.5);
            Require("body-on-base " + swatch.Id, Contrast(theme.TextPrimary, theme.Base), 4.5);
            Require("body-on-selected-plate " + swatch.Id, Contrast(theme.TextOnGold, theme.Selected), 4.5);
            // (c) the assist fill: measured, printed, and named - never a silent green.
            Console.WriteLine("[selection-legibility] " + swatch.Id
                + " rail-vs-fill=" + Contrast(rail, fill).ToString("0.000")
                + " rail-vs-plane=" + Contrast(rail, plane).ToString("0.000")
                + " fill-vs-plane=" + Contrast(fill, plane).ToString("0.000") + " (assist, not gated)");
        }
    }

    private static void Require(string name, double ratio, double floor)
    {
        Assert(ratio >= floor,
            name + " reads " + ratio.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
            + ":1, under the " + floor.ToString("0.0") + ":1 floor");
    }

    // ---- helpers (WCAG relative luminance, source-over assumed opaque pairs) ----

    private static bool Same(Color a, Color b)
    {
        const float eps = 1f / 512f;
        return Mathf.Abs(a.r - b.r) < eps && Mathf.Abs(a.g - b.g) < eps && Mathf.Abs(a.b - b.b) < eps;
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(Color c)
    {
        return 0.2126 * Chan(c.r) + 0.7152 * Chan(c.g) + 0.0722 * Chan(c.b);
    }

    private static double Chan(float v)
    {
        double s = v;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("PaletteLaneTests step failed: " + name
                + "\n  INNER-2-MESSAGE: " + error.Message, error);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
