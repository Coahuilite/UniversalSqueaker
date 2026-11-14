#if US_OFFLINE_WORKBENCH
using System.Globalization;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public sealed partial class UsDiagnosticsMarkWidget : IUiDescriptiveWidget
{
    public UiDescription Describe(UiWidgetContext context)
    {
        // Actual last paint, not a guessed font box computed by an observation request.
        string Rect(UiLayoutRect value) => string.Join(",", value.X.ToString("R", CultureInfo.InvariantCulture),
            value.Y.ToString("R", CultureInfo.InvariantCulture), value.Width.ToString("R", CultureInfo.InvariantCulture), value.Height.ToString("R", CultureInfo.InvariantCulture));
        return new UiDescription(UiDescriptionRole.Text, "US diagnostics screen mark", painted
            ? "painted-frame=" + paintedFrame + "; drawing-local-logical glyph=" + Rect(lastGeometry.Glyph)
                + "; outline-bounds=" + Rect(lastGeometry.Bounds) + "; font=" + lastGeometry.Font
            : "not-painted; no actual glyph geometry", available: context.IsActive);
    }
}
#endif
