using UnityEngine;

namespace FerriteLib.UiKit;

/// <summary>Neutral role-based visual palette for the modern built-in skin. Values match the approved P0 palette.</summary>
public static class Palette
{
    public static readonly Color Base = new(0.10f, 0.10f, 0.10f, 1f);
    public static readonly Color Panel = new(0.14f, 0.14f, 0.14f, 1f);
    public static readonly Color Raised = new(0.18f, 0.18f, 0.18f, 1f);
    public static readonly Color Hover = new(0.22f, 0.22f, 0.22f, 1f);
    public static readonly Color Selected = new(0.20f, 0.17f, 0.10f, 1f);
    public static readonly Color Warning = new(0.55f, 0.18f, 0.15f, 1f);
    public static readonly Color Success = new(0.16f, 0.35f, 0.22f, 1f);
    public static readonly Color Danger = new(0.55f, 0.18f, 0.15f, 1f);

    public static readonly Color TextPrimary = new(0.92f, 0.92f, 0.90f, 1f);
    public static readonly Color TextSecondary = new(0.65f, 0.65f, 0.62f, 1f);
    public static readonly Color TextOnGold = new(1f, 0.86f, 0.58f, 1f);
    public static readonly Color TextOnDanger = new(1f, 0.67f, 0.48f, 1f);
    public static readonly Color TextDisabled = new(0.45f, 0.45f, 0.42f, 1f);

    public static readonly Color AccentGold = new(0.92f, 0.68f, 0.30f, 1f);
    public static readonly Color AccentGoldAlpha20 = new(0.92f, 0.68f, 0.30f, 0.20f);
    public static readonly Color Border = new(0.30f, 0.30f, 0.28f, 1f);
    public static readonly Color BorderStrong = new(0.45f, 0.45f, 0.42f, 1f);
}
