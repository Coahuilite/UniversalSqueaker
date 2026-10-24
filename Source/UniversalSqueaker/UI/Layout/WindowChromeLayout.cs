using System;

namespace UniversalSqueaker.UI;

/// <summary>US opening-size policy in Verse logical units; no independent UI scale.</summary>
public static class WindowChromeLayout
{
    public const float VanillaBaselineWidth = 650f;
    public const float VanillaBaselineHeight = 600f;
    public const float SettingsWidthFraction = .8f;
    public const float SettingsWidthCeiling = 1280f;
    public const float SettingsWidthFloor = 960f;
    public const float SettingsWidthStep = 1f;
    public const float SettingsHeightFloor = 600f;
    public const float WindowChromeInset = 40f;
    public const float TitleBarHeight = 56f;
    // Retained numeric vocabulary for source consumers; sizing is no longer a fixed aspect ratio.
    public const float WindowAspectWidth = 4f;
    public const float WindowAspectHeight = 3f;
    public const float SettingsHeightCeilingFraction = .9f;

    public static float SettingsWindowWidth(float screenWidth, float screenHeight)
        => Math.Max(1, Math.Min(Math.Min(SettingsWidthCeiling,
            Math.Max(SettingsWidthFloor, (float)Math.Round(SettingsWidthFraction * screenWidth, MidpointRounding.AwayFromZero))), screenWidth - 64));

    public static float SettingsWindowHeight(float screenWidth, float screenHeight)
        => Math.Max(1, Math.Min(820, screenHeight - 64));

    /// <summary>Call with the actual FL content rectangle BEFORE the common page header is allocated.</summary>
    public static bool UsesWideLayout(float contentWidth, float contentHeight)
        => contentWidth - 24 >= 1100 && contentHeight - 24 >= 640;
}
