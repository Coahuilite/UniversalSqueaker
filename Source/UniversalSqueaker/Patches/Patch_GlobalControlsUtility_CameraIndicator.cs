using System.Globalization;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using UniversalSqueaker.UI;

namespace UniversalSqueaker;

/// <summary>
/// S4 diagnostics foundation: player-facing camera indicator readout above the bottom-left
/// date bar. US-adapted from the SR CameraIndicator patch: the <c>Prefs.DevMode</c> gate is
/// removed, and the show is driven by the player settings toggle
/// (<see cref="SqueakDebug.ShowCameraIndicator"/> / <see cref="UniversalSqueakerSettings.SetCameraIndicator"/>).
///
/// This postfix is the SINGLE draw dispatcher for the camera readout: it first asks the UiKit
/// overlay Host (second host, <see cref="UsCameraIndicatorOverlay"/>) to draw; the legacy
/// pure-Verse readout below runs only when the kernel path did not draw (disabled by contract
/// failure or permanent session fallback). The two paths are therefore mutually exclusive by
/// construction — never drawn in parallel — and both use the same already-proven date-bar rect.
/// </summary>
[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate))]
public static class Patch_GlobalControlsUtility_CameraIndicator
{
    private const float RowHeight = 26f;

    private static void Postfix(float leftX, float width, ref float curBaseY)
    {
        if (!SqueakDebug.ShowCameraIndicator || Find.CurrentMap == null) return;
        if (Event.current?.type == EventType.Layout) return;

        if (UsCameraIndicatorOverlay.TryDraw(leftX, width, ref curBaseY)) return;

        // Legacy pure-Verse fallback: draws only when the kernel overlay did not draw.
        float height = Find.Camera.transform.position.y;
        float viewSize = Find.Camera.orthographicSize;

        curBaseY -= RowHeight;
        Rect rect = new(leftX, curBaseY, width, RowHeight);
        TextAnchor previousAnchor = Text.Anchor;
        Text.Anchor = TextAnchor.UpperRight;
        try
        {
            Widgets.Label(rect, "US.Debug.CameraIndicator".Translate(
                height.ToString("0.0", CultureInfo.InvariantCulture),
                viewSize.ToString("0.0", CultureInfo.InvariantCulture)));
        }
        finally
        {
            Text.Anchor = previousAnchor;
        }
    }
}
