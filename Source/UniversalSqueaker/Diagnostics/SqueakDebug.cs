using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Minimal production-facing debug surface. Every successful audio dispatch emits one dev-level route
/// record; vanilla fallback dispatches are warning-level. No statistical/UI diagnostics are retained.
/// </summary>
public static class SqueakDebug
{
    /// <summary>
    /// S4 diagnostics foundation: player-facing camera indicator. Owned by the settings
    /// toggle (<see cref="GameSettings.SetCameraIndicator"/>) and consumed by
    /// <see cref="Patch_GlobalControlsUtility_CameraIndicator"/> — no DevMode gating.
    /// </summary>
    public static bool ShowCameraIndicator = false;

    /// <summary>
    /// S4 diagnostics: DebugAction entry point. Opens the session's main window; the detail
    /// column follows the current selection on its own (round-9 model - no more Selected/Visible
    /// mode fork at the entry).
    /// </summary>
    public static void OpenDiagnostics()
    {
        SqueakDiagnosticsOverlay.BeginSession();
    }

}
