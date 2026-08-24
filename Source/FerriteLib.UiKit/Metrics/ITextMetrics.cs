namespace FerriteLib.UiKit;

/// <summary>
/// Text measurement seam. Production implementations use Verse's IMGUI text engine;
/// tests inject deterministic stubs.
/// </summary>
public interface ITextMetrics
{
    /// <summary>Measures the height needed to render <paramref name="text"/> at <paramref name="font"/> within <paramref name="width"/>.</summary>
    float MeasureText(string text, UiFont font, float width);
}
