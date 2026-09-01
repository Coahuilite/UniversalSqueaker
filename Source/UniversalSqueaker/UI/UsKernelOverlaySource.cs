using System.Globalization;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production <see cref="IUsKernelOverlaySource"/>. Reads the player toggle
/// (<see cref="SqueakDebug.ShowCameraIndicator"/>), the current map, and formats the same
/// "US.Debug.CameraIndicator" translation the legacy pure-Verse patch uses, so the kernel overlay
/// and the fallback show identical text at the identical (already proven) date-bar position.
/// Camera reads are null-guarded; the readout is empty when no camera is available.
/// </summary>
public sealed class UsKernelOverlaySource : IUsKernelOverlaySource
{
    public static readonly UsKernelOverlaySource Instance = new();

    public bool IndicatorEnabled => SqueakDebug.ShowCameraIndicator;

    public bool MapAvailable => Find.CurrentMap != null;

    public string ReadoutText
    {
        get
        {
            Camera? camera = Find.Camera;
            if (camera == null || camera.transform == null)
            {
                return "";
            }

            float height = camera.transform.position.y;
            float viewSize = camera.orthographicSize;
            return "US.Debug.CameraIndicator".Translate(
                height.ToString("0.0", CultureInfo.InvariantCulture),
                viewSize.ToString("0.0", CultureInfo.InvariantCulture)).ToString();
        }
    }
}
