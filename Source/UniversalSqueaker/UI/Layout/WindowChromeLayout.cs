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
}
