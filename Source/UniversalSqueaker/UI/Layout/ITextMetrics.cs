namespace UniversalSqueaker.UI;

/// <summary>
/// Text measurement seam used by layout functions. Production uses <see cref="VerseTextMetrics"/>;
/// tests can inject a stub without Verse.
/// </summary>
public interface ITextMetrics
{
    float CalcHeight(string text, float width);
    float CalcWidth(string text);
}
