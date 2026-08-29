using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Role-based visual tokens for the modern US settings skin. Pure presentation values; no state.
/// Values match the approved P0 palette and the neutral FerriteLib palette.
/// </summary>
internal static class UsVisualTokens
{
    internal static readonly Color SurfaceBase = new(0.10f, 0.10f, 0.10f, 1f);
    internal static readonly Color Panel = new(0.14f, 0.14f, 0.14f, 1f);
    internal static readonly Color Raised = new(0.18f, 0.18f, 0.18f, 1f);
    internal static readonly Color Hover = new(0.22f, 0.22f, 0.22f, 1f);
    internal static readonly Color Selected = new(0.20f, 0.17f, 0.10f, 1f);
    internal static readonly Color Warning = new(0.55f, 0.18f, 0.15f, 1f);
    internal static readonly Color Success = new(0.16f, 0.35f, 0.22f, 1f);
    internal static readonly Color Danger = new(0.55f, 0.18f, 0.15f, 1f);

    internal static readonly Color TextPrimary = new(0.92f, 0.92f, 0.90f, 1f);
    internal static readonly Color TextSecondary = new(0.65f, 0.65f, 0.62f, 1f);
    internal static readonly Color TextOnGold = new(1f, 0.86f, 0.58f, 1f);
    internal static readonly Color TextOnDanger = new(1f, 0.67f, 0.48f, 1f);
    internal static readonly Color TextDisabled = new(0.45f, 0.45f, 0.42f, 1f);

    internal static readonly Color AccentGold = new(0.92f, 0.68f, 0.30f, 1f);
    internal static readonly Color AccentGoldAlpha20 = new(0.92f, 0.68f, 0.30f, 0.20f);
    internal static readonly Color Border = new(0.30f, 0.30f, 0.28f, 1f);
    internal static readonly Color BorderStrong = new(0.45f, 0.45f, 0.42f, 1f);

    internal const float Spacing2 = 2f;
    internal const float Spacing4 = 4f;
    internal const float Spacing6 = 6f;
    internal const float Spacing8 = 8f;
    internal const float Spacing10 = 10f;

    internal const float RowHeightS = 22f;
    internal const float RowHeightM = 26f;
    internal const float RowHeightL = 32f;
    internal const float RowHeightXl = 50f;
}
