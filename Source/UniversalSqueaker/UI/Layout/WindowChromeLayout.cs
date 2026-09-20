using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Window geometry owned by this consumer, kept pure (no Verse/Unity) so a lane can assert the rule
/// instead of a screenshot.
///
/// <para>
/// <b>The close affordance is NOT here any more</b> (TODO:15, closed 2026-09-20). This file used to carry
/// a second definition of the shell's close-button rule - a 16px padding, measured through a hardcoded
/// <c>VerseFerriteTextMetrics.Instance</c> - while the shell derived the same affordance as
/// <c>max(110, measured + 2 x CloseButtonPadding)</c> through its own <c>ITextMetrics</c> seam.
/// Two definitions of one padding is a seam defect by construction: the shell could widen the button for a
/// label and the audit could still report that label as overflowing, and a changed padding had to be
/// changed twice. The shell owns the close affordance (its label is <c>CloseText</c>, which is the
/// consumer's string), so the duplicate is deleted and the rule now has exactly one home.
/// </para>
///
/// What remains is this consumer's own window-size policy, which IS consumer policy and has no library
/// equivalent.
/// </summary>
public static class WindowChromeLayout
{
    // ---- Settings window size policy (task-10) ------------------------------------------------------
    // The settings window opens NARROW like the vanilla ModSettings window and only widens when the
    // retractable help drawer expands. This is consumer policy - one consumer's screen fraction stays
    // that consumer's policy - so it lives here beside the close affordance rule, still free of
    // Verse/Unity: the zero-Verse UI gate compiles this file, and the Verse boundary (the window)
    // composes the two floats into its own Vector2. Keep it that way.

    /// <summary>
    /// Vanilla's own options window: <c>Dialog_Options.InitialSize = (650, 600)</c>, a FIXED size that does
    /// not scale with resolution at all (read from the 1.6 source; confirmed by the maintainer's 1024x768
    /// in-game screenshot). This pair is the FLOOR of the policy below: a US settings window is never
    /// smaller than the dialog the game itself puts in the same place.
    /// </summary>
    public const float VanillaBaselineWidth = 650f;

    /// <summary>Vanilla's own options-window height; see <see cref="VanillaBaselineWidth"/>: 650x600 is
    /// 13:12 (about 1.083), i.e. nearly square - far closer to 4:3 than to 16:9.</summary>
    public const float VanillaBaselineHeight = 600f;

    /// <summary>Fraction of the screen width the retracted window opens at, above the floor below.</summary>
    public const float SettingsClosedWidthFraction = 0.5f;

    /// <summary>Widest the retracted window may open, so a 4K screen does not get a near-full-width dialog.</summary>
    public const float SettingsWidthCeiling = 1600f;

    /// <summary>Every width is a multiple of this, so the derived height is an exact integer.</summary>
    public const float SettingsWidthStep = 4f;

    /// <summary>
    /// Narrowest the window may be: the SMALLEST 4:3 box that is at least vanilla's own 650x600, which is
    /// 800x600. Deriving it (rather than declaring 800) is what keeps "never smaller than the dialog the
    /// game itself opens" and "integer 4:3" from drifting apart. The page declares Breakpoint 500 on
    /// body-row, so this floor plus the chrome insets holds the two-column regime in the worst case
    /// (800 - 2*20 chrome - 2*12 page padding = 736 inner >= 500).
    /// </summary>
    public const float SettingsWidthFloor = 800f;

    /// <summary>Declared <c>Width</c> of the help Scroll in <c>Layout.Schema2.xml</c>.</summary>
    public const float HelpDrawerWidth = 320f;

    /// <summary>Declared <c>Gap</c> of body-row in <c>Layout.Schema2.xml</c>: the extra gap a third column costs.</summary>
    public const float BodyRowGap = 12f;

    /// <summary>
    /// What the help drawer costs the window when it expands: the column it occupies plus the row gap
    /// its third child introduces. Keep this derived from the two declarations above, so the window
    /// width and the manifest cannot drift apart silently.
    /// </summary>
    public const float DrawerWidthDelta = HelpDrawerWidth + BodyRowGap;

    /// <summary>Narrowest the window may be vertically: vanilla's own height (the baseline above), which
    /// is also the height the 800-wide floor derives to.</summary>
    public const float SettingsHeightFloor = VanillaBaselineHeight;

    /// <summary>
    /// The growth shape above the floor: 4:3, in whole pixels. Chosen on two grounds. (a) The old shape grew
    /// the WRONG axis - 24% of the width against 66% of the height opened portrait (614x950 at 2560x1440),
    /// which wrapped every long label (the 2026-09-14 in-game log: footer needs 47.3px in a 28px band,
    /// global-volume 32.7/18) and lengthened the stack. (b) Vanilla's own 650x600 is 13:12, so 4:3 sits much
    /// closer to it than 16:9 would, and w*3/4 is an exact integer for every width that is a multiple of
    /// <see cref="SettingsWidthStep"/> - no fractional window sizes reach the player.
    /// </summary>
    public const float WindowAspectWidth = 4f;

    /// <summary>The other half of <see cref="WindowAspectWidth"/>.</summary>
    public const float WindowAspectHeight = 3f;

    /// <summary>Largest share of the screen height the window may take, so a 16:9 window never runs off screen.</summary>
    public const float SettingsHeightCeilingFraction = 0.9f;

    /// <summary>
    /// Width the window opens at while the help drawer is retracted. Every constraint is folded in HERE so the
    /// window can never disagree with itself: half the screen, capped by the 90%-of-screen-height limit at 4:3
    /// (a short, ultra-wide display shrinks the WIDTH instead of breaking the shape), floored at the 4:3 box
    /// that covers vanilla's own dialog, capped for 4K, and rounded up to a whole pixel step so the derived
    /// height is an exact integer.
    /// </summary>
    public static float SettingsClosedWidth(float screenWidth, float screenHeight)
    {
        float byWidth = screenWidth * SettingsClosedWidthFraction;
        float byHeight = screenHeight * SettingsHeightCeilingFraction * WindowAspectWidth / WindowAspectHeight;
        float wanted = Clamp(Math.Min(byWidth, byHeight), SettingsWidthFloor, SettingsWidthCeiling);
        return RoundUpToStep(wanted);
    }

    /// <summary>
    /// Width the window needs once the help drawer is expanded: the closed width plus its cost, capped at
    /// the screen so an expanded window can never be wider than the display it lives in. Below the cap the
    /// page keeps its own narrow-layout capability (body-row declares Breakpoint 500), so the centre column
    /// and the drawer stay inside the window instead of the drawer hanging off the screen edge.
    /// </summary>
    public static float SettingsOpenWidth(float screenWidth, float screenHeight)
    {
        float closed = SettingsClosedWidth(screenWidth, screenHeight);
        return Math.Min(closed + DrawerWidthDelta, Math.Max(closed, screenWidth));
    }

    /// <summary>Width for one drawer state. The single entry the window and the lanes both read.</summary>
    public static float SettingsWindowWidth(float screenWidth, float screenHeight, bool drawerExpanded)
    {
        return drawerExpanded
            ? SettingsOpenWidth(screenWidth, screenHeight)
            : SettingsClosedWidth(screenWidth, screenHeight);
    }

    /// <summary>
    /// Height for a screen: the closed width at 4:3, which is an exact whole number for every width this class
    /// returns, floored at vanilla's own height because the width floor derives from it.
    /// </summary>
    public static float SettingsWindowHeight(float screenWidth, float screenHeight)
    {
        return SettingsClosedWidth(screenWidth, screenHeight) * WindowAspectHeight / WindowAspectWidth;
    }

    private static float RoundUpToStep(float value)
    {
        return (float)Math.Ceiling(value / SettingsWidthStep) * SettingsWidthStep;
    }

    private static float Clamp(float value, float min, float max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
