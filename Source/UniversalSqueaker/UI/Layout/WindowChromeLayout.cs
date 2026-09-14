using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Chrome geometry owned by this consumer, kept pure (no Verse/Unity) so a lane can assert the rule
/// instead of a screenshot. The shell (UiWindowHost) sizes its close affordance from a fixed 110x30 box;
/// the first real in-game run caught that box as ui.text.overflow (width/tiny, needs 128.0px, has
/// 110.0px) - the chrome draws outside the layout engine's element scope, so the finding was reported as
/// "(unscoped)" and named no code site.
///
/// The rule: never smaller than the shell's own box, never smaller than the text that will actually be
/// drawn plus a padding margin, measured through the same metrics seam the fit audit uses so the two
/// agree by construction. Font size is deliberately not an input: widening and shortening are the
/// answers the rulings allow, shrinking the font is not.
/// </summary>
public static class WindowChromeLayout
{
    /// <summary>Horizontal room around the close caption inside the affordance.</summary>
    public const float ClosePadding = 16f;

    /// <summary>The shell's own box width, kept as the floor so the shipped English/Chinese shape is unchanged.</summary>
    public const float CloseWidthFloor = 110f;

    /// <summary>The shell's own box height.</summary>
    public const float CloseHeight = 30f;

    /// <summary>Width of the close affordance for a caption measured through the injected text metrics.</summary>
    public static float CloseButtonWidth(float measuredTextWidth)
    {
        float wanted = measuredTextWidth > 0f ? measuredTextWidth + ClosePadding : 0f;
        return Math.Max(CloseWidthFloor, wanted);
    }

    // ---- Settings window size policy (task-10) ------------------------------------------------------
    // The settings window opens NARROW like the vanilla ModSettings window and only widens when the
    // retractable help drawer expands. This is consumer policy - one consumer's screen fraction stays
    // that consumer's policy - so it lives here beside the close affordance rule, still free of
    // Verse/Unity: the zero-Verse UI gate compiles this file, and the Verse boundary (the window)
    // composes the two floats into its own Vector2. Keep it that way.

    /// <summary>Fraction of the screen width the retracted window opens at.</summary>
    public const float SettingsClosedWidthFraction = 0.24f;

    /// <summary>Widest the retracted window may open, so a 4K screen does not get a near-full-width dialog.</summary>
    public const float SettingsWidthCeiling = 860f;

    /// <summary>
    /// Narrowest the window may be. The page declares Breakpoint 500 on body-row, so this floor plus
    /// the chrome insets keeps the closed window in the two-column regime (600 - 2*20 padding - 2*12
    /// page padding = 536 inner >= 500).
    /// </summary>
    public const float SettingsWidthFloor = 600f;

    /// <summary>Declared <c>Width</c> of the help Scroll in <c>Layout.Schema2.xml</c>.</summary>
    public const float HelpDrawerWidth = 176f;

    /// <summary>Declared <c>Gap</c> of body-row in <c>Layout.Schema2.xml</c>: the extra gap a third column costs.</summary>
    public const float BodyRowGap = 12f;

    /// <summary>
    /// What the help drawer costs the window when it expands: the column it occupies plus the row gap
    /// its third child introduces. Keep this derived from the two declarations above, so the window
    /// width and the manifest cannot drift apart silently.
    /// </summary>
    public const float DrawerWidthDelta = HelpDrawerWidth + BodyRowGap;

    /// <summary>Fraction of the screen height the window keeps (unchanged, product ruling).</summary>
    public const float SettingsHeightFraction = 0.66f;

    /// <summary>Narrowest the window may be vertically (unchanged, product ruling).</summary>
    public const float SettingsHeightFloor = 600f;

    /// <summary>Width the window opens at while the help drawer is retracted.</summary>
    public static float SettingsClosedWidth(float screenWidth)
    {
        return Clamp(screenWidth * SettingsClosedWidthFraction, SettingsWidthFloor, SettingsWidthCeiling);
    }

    /// <summary>
    /// Width the window needs once the help drawer is expanded: the closed width plus its cost, capped at
    /// the screen so an expanded window can never be wider than the display it lives in. Below the cap the
    /// page keeps its own narrow-layout capability (body-row declares Breakpoint 500), so the centre column
    /// and the drawer stay inside the window instead of the drawer hanging off the screen edge.
    /// </summary>
    public static float SettingsOpenWidth(float screenWidth)
    {
        float closed = SettingsClosedWidth(screenWidth);
        return Math.Min(closed + DrawerWidthDelta, Math.Max(closed, screenWidth));
    }

    /// <summary>Width for one drawer state. The single entry the window and the lanes both read.</summary>
    public static float SettingsWindowWidth(float screenWidth, bool drawerExpanded)
    {
        return drawerExpanded ? SettingsOpenWidth(screenWidth) : SettingsClosedWidth(screenWidth);
    }

    /// <summary>Height the window keeps: the shipped 0.66-of-screen shape with a 600 floor.</summary>
    public static float SettingsWindowHeight(float screenHeight)
    {
        return Math.Max(SettingsHeightFloor, screenHeight * SettingsHeightFraction);
    }

    private static float Clamp(float value, float min, float max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
