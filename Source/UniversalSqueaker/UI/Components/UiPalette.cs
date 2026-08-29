using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Compatibility palette forwarding to <see cref="UsVisualTokens"/>. Kept only while external page
/// shell code still references the old names; new widget code should use <see cref="UsVisualTokens"/>.
/// </summary>
internal static class UiPalette
{
    internal static readonly Color Ink = UsVisualTokens.SurfaceBase;
    internal static readonly Color Panel = UsVisualTokens.Panel;
    internal static readonly Color Raised = UsVisualTokens.Raised;
    internal static readonly Color Emphasized = UsVisualTokens.Raised;
    internal static readonly Color Warning = UsVisualTokens.Warning;
    internal static readonly Color Success = UsVisualTokens.Success;
    internal static readonly Color Border = UsVisualTokens.Border;
    internal static readonly Color Gold = UsVisualTokens.AccentGold;
    internal static readonly Color Muted = UsVisualTokens.TextSecondary;
    internal static readonly Color Disabled = UsVisualTokens.TextDisabled;
    internal static readonly Color Selected = UsVisualTokens.Selected;
    internal static readonly Color Danger = UsVisualTokens.Danger;
}
