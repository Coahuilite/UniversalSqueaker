using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Recording stub of the <see cref="IUsKernelOverlaySource"/> business boundary for the overlay
/// Host harness. All three members are mutable so the harness can prove show/hide, map-gone,
/// readout freshness and dispose/reopen behavior without the RimWorld graph. Note: this is
/// contract evidence (recording source), NOT real camera/catalog data — real RimWorld camera
/// reads remain LIMITED.
/// </summary>
internal sealed class RecordingOverlaySource : IUsKernelOverlaySource
{
    public bool Enabled = true;
    public bool MapPresent = true;
    public string Text = "Cam 12.0 / View 8.0";

    public bool IndicatorEnabled => Enabled;

    public bool MapAvailable => MapPresent;

    public string ReadoutText => Text;
}
