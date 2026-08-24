using UnityEngine;

namespace FerriteLib.UiKit.Widgets;

/// <summary>Neutral visual palette for built-in widgets. Values match the private Ferrite UI look.</summary>
internal static class Palette
{
    internal static readonly Color Ink = new(0.06f, 0.055f, 0.05f, 0.94f);
    internal static readonly Color Panel = new(0.09f, 0.085f, 0.075f, 0.92f);
    internal static readonly Color Raised = new(0.115f, 0.107f, 0.095f, 0.96f);
    internal static readonly Color Emphasized = new(0.13f, 0.112f, 0.082f, 0.92f);
    internal static readonly Color Warning = new(0.17f, 0.095f, 0.073f, 0.94f);
    internal static readonly Color Success = new(0.075f, 0.135f, 0.095f, 0.94f);
    internal static readonly Color Border = new(0.34f, 0.32f, 0.28f, 0.82f);
    internal static readonly Color Gold = new(0.92f, 0.68f, 0.30f);
    internal static readonly Color Muted = new(0.68f, 0.66f, 0.61f, 0.92f);
    internal static readonly Color Selected = new(0.235f, 0.195f, 0.125f, 0.96f);
    internal static readonly Color Hover = new(0.16f, 0.145f, 0.12f, 0.94f);
    internal static readonly Color TextLight = new(0.95f, 0.92f, 0.84f);
    internal static readonly Color TextSelected = new(1f, 0.86f, 0.58f);
    internal static readonly Color TextSecondary = new(0.82f, 0.80f, 0.74f, 0.92f);
}
