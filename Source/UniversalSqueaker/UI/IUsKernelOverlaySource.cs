namespace UniversalSqueaker.UI;

/// <summary>
/// Business boundary of the camera-indicator overlay Host (the mod's second UiKit host, fully
/// independent of the Settings Window). The production implementation reads the live camera
/// height/view size and the player toggle; <see cref="UsKernelOverlayController"/> keeps the
/// Host/session lifecycle (create on show, dispose on hide / map-gone / failure) against this
/// narrow typed surface, and the harness can substitute a recording stub without the RimWorld
/// graph. The interface deliberately carries no Settings Window, page state or command bridge.
/// </summary>
public interface IUsKernelOverlaySource
{
    /// <summary>
    /// Player-facing toggle. The single source of truth is the settings value
    /// (<see cref="SqueakDebug.ShowCameraIndicator"/>, kept in sync by
    /// <see cref="UniversalSqueakerSettings.SetCameraIndicator"/>).
    /// </summary>
    bool IndicatorEnabled { get; }

    /// <summary>True while a map is loaded and the bottom-left date bar is being drawn.</summary>
    bool MapAvailable { get; }

    /// <summary>Right-aligned readout text (camera height / view size), already translated.</summary>
    string ReadoutText { get; }
}
