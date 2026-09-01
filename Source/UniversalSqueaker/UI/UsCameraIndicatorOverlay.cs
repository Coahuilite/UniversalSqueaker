namespace UniversalSqueaker.UI;

/// <summary>
/// Production entry for the UiKit camera-indicator overlay (the mod's second Host). One static
/// controller per game session. The draw dispatcher
/// (<see cref="Patch_GlobalControlsUtility_CameraIndicator"/>) calls <see cref="TryDraw"/> first
/// and only draws the legacy pure-Verse readout when it returns false, so the kernel overlay and
/// the legacy patch are mutually exclusive by construction — never drawn in parallel.
/// <see cref="Maintain"/> is driven from the Root.Update lifecycle postfix so the Host is disposed
/// as soon as the toggle is off or the map is gone, even in frames where the date bar does not
/// draw. A failed creation/draw permanently switches this game session to the legacy path (one
/// warning, no retry spam); a show/hide round-trip always yields a fresh Host/session.
/// </summary>
public static class UsCameraIndicatorOverlay
{
    private static UsKernelOverlayController? controller;
    private static bool permanentlyFailed;

    /// <summary>True while the kernel overlay Host is active (created and its session alive).</summary>
    public static bool IsActive => controller != null && controller.IsActive;

    /// <summary>
    /// Per-frame teardown gate, called from the Root.Update lifecycle postfix. No-op unless the
    /// overlay is active and its conditions no longer hold (toggle off or map gone).
    /// </summary>
    public static void Maintain()
    {
        controller?.Maintain();
    }

    /// <summary>
    /// Draws the kernel overlay at the date-bar rect. Returns true only when the kernel Host drew;
    /// false means the legacy pure-Verse readout must be drawn instead.
    /// </summary>
    public static bool TryDraw(float leftX, float width, ref float curBaseY)
    {
        if (permanentlyFailed) return false;

        controller ??= new UsKernelOverlayController(UsKernelOverlaySource.Instance);
        if (controller.TryDraw(leftX, width, ref curBaseY))
        {
            return true;
        }

        if (controller.CreationFailed)
        {
            permanentlyFailed = true;
            controller = null;
        }
        else if (!controller.IsActive)
        {
            // Disabled/map-gone this frame: drop the instance so a later enabled frame builds a
            // fresh Host/session (show/hide round-trip contract).
            controller = null;
        }

        return false;
    }
}
