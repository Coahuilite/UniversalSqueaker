using UnityEngine;
using FerriteLib.UiKit;

namespace UniversalSqueaker.UI;

/// <summary>
/// Compatibility palette forwarding to the neutral UiKit <see cref="Palette"/>. Kept only while
/// diagnostics/shell code still references the old names; new widget code should use
/// <see cref="Palette"/> directly.
/// </summary>
internal static class UiPalette
{
    internal static readonly Color Ink = Palette.Base;
    internal static readonly Color Panel = Palette.Panel;
    internal static readonly Color Raised = Palette.Raised;
    internal static readonly Color Emphasized = Palette.Raised;
    internal static readonly Color Warning = Palette.Warning;
    internal static readonly Color Success = Palette.Success;
    internal static readonly Color Border = Palette.Border;
    internal static readonly Color Gold = Palette.AccentGold;
    internal static readonly Color Muted = Palette.TextSecondary;
    internal static readonly Color Disabled = Palette.TextDisabled;
    internal static readonly Color Selected = Palette.Selected;
    internal static readonly Color Danger = Palette.Danger;
}
