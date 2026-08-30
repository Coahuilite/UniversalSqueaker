using FerriteLib.UiKit;
using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Adapts the neutral FerriteLib.UiKit text metric seam to the existing US layout metric seam.
/// The US layout functions only expose a fontless <c>CalcHeight(text, width)</c>; the majority of
/// their callers render banners/rows in <c>GameFont.Tiny</c>, so height measurements are mapped to
/// <see cref="UiFont.Tiny"/>. <c>CalcWidth</c> has no fontless equivalent in the neutral seam and
/// falls back to the Verse-backed US metric because the current US layout functions do not use it.
/// </summary>
internal sealed class FerriteTextMetricsAdapter : ITextMetrics
{
    private readonly FerriteLib.UiKit.ITextMetrics inner;
    private readonly UiFont font;

    internal FerriteTextMetricsAdapter(FerriteLib.UiKit.ITextMetrics inner, UiFont font = UiFont.Tiny)
    {
        this.inner = inner;
        this.font = font;
    }

    public float CalcHeight(string text, float width)
    {
        return inner.MeasureText(text ?? "", font, Mathf.Max(1f, width));
    }

    public float CalcWidth(string text)
    {
        return VerseTextMetrics.Instance.CalcWidth(text ?? "");
    }
}
