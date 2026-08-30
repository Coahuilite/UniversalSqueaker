using UnityEngine;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>
/// Backwards-compatible US token names forwarding to the neutral UiKit <see cref="Palette"/>.
/// New code should use <see cref="Palette"/> directly; this shim exists only to keep the current
/// US widget surface untouched during the OB-01 convergence.
/// </summary>
internal static class UsVisualTokens
{
    internal static readonly Color SurfaceBase = Palette.Base;
    internal static readonly Color Panel = Palette.Panel;
    internal static readonly Color Raised = Palette.Raised;
    internal static readonly Color Hover = Palette.Hover;
    internal static readonly Color Selected = Palette.Selected;
    internal static readonly Color Warning = Palette.Warning;
    internal static readonly Color Success = Palette.Success;
    internal static readonly Color Danger = Palette.Danger;

    internal static readonly Color TextPrimary = Palette.TextPrimary;
    internal static readonly Color TextSecondary = Palette.TextSecondary;
    internal static readonly Color TextOnGold = Palette.TextOnGold;
    internal static readonly Color TextOnDanger = Palette.TextOnDanger;
    internal static readonly Color TextDisabled = Palette.TextDisabled;

    internal static readonly Color AccentGold = Palette.AccentGold;
    internal static readonly Color AccentGoldAlpha20 = Palette.AccentGoldAlpha20;
    internal static readonly Color Border = Palette.Border;
    internal static readonly Color BorderStrong = Palette.BorderStrong;

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
