using System.Globalization;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S4 diagnostics foundation: player-facing camera indicator readout above the bottom-left
/// date bar. US-adapted from the SR CameraIndicator patch: the <c>Prefs.DevMode</c> gate is
/// removed, and the show is driven by the player settings toggle
/// (<see cref="SqueakDebug.ShowCameraIndicator"/> / <see cref="UniversalSqueakerSettings.SetCameraIndicator"/>).
/// </summary>
[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate))]
public static class Patch_GlobalControlsUtility_CameraIndicator
{
    private const float RowHeight = 26f;

    private static void Postfix(float leftX, float width, ref float curBaseY)
    {
        if (!SqueakDebug.ShowCameraIndicator || Find.CurrentMap == null) return;
        if (Event.current?.type == EventType.Layout) return;

        float height = Find.Camera.transform.position.y;
        float viewSize = Find.Camera.orthographicSize;

        curBaseY -= RowHeight;
        Rect rect = new(leftX, curBaseY, width, RowHeight);
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(rect, "US.Debug.CameraIndicator".Translate(
            height.ToString("0.0", CultureInfo.InvariantCulture),
            viewSize.ToString("0.0", CultureInfo.InvariantCulture)));
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
