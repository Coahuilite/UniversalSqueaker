using UnityEngine;

namespace FerriteLib.UiKit;

/// <summary>Neutral role-based visual palette for the modern built-in skin.</summary>
public static class Palette
{
    // Surfaces
    public static readonly Color Canvas = new(0.055f, 0.055f, 0.055f, 1f);
    public static readonly Color Base = new(0.075f, 0.075f, 0.075f, 1f);
    public static readonly Color Panel = new(0.11f, 0.11f, 0.11f, 1f);
    public static readonly Color Raised = new(0.16f, 0.16f, 0.16f, 1f);
    public static readonly Color Hover = new(0.20f, 0.20f, 0.20f, 1f);
    public static readonly Color Selected = new(0.20f, 0.17f, 0.09f, 1f);
    public static readonly Color Warning = new(0.48f, 0.16f, 0.13f, 1f);
    public static readonly Color Success = new(0.13f, 0.30f, 0.19f, 1f);
    public static readonly Color Danger = new(0.48f, 0.16f, 0.13f, 1f);

    // Text
    public static readonly Color TextPrimary = new(0.91f, 0.91f, 0.88f, 1f);
    public static readonly Color TextSecondary = new(0.62f, 0.62f, 0.59f, 1f);
    public static readonly Color TextOnGold = new(1f, 0.84f, 0.55f, 1f);
    public static readonly Color TextOnDanger = new(1f, 0.65f, 0.46f, 1f);
    public static readonly Color TextDisabled = new(0.42f, 0.42f, 0.40f, 1f);

    // Accents
    public static readonly Color AccentGold = new(0.85f, 0.64f, 0.25f, 1f);
    public static readonly Color AccentGoldAlpha20 = new(0.85f, 0.64f, 0.25f, 0.20f);
    public static readonly Color AccentGoldAlpha10 = new(0.85f, 0.64f, 0.25f, 0.10f);

    // Borders
    public static readonly Color Border = new(0.24f, 0.24f, 0.23f, 1f);
    public static readonly Color BorderStrong = new(0.40f, 0.40f, 0.38f, 1f);
    public static readonly Color Divider = new(0.18f, 0.18f, 0.17f, 1f);
}
