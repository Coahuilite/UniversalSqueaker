using FerriteLib.UiKit;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production Verse-backed implementation of the neutral FerriteLib.UiKit text metric seam.
/// Mirrors the existing <see cref="VerseTextMetrics"/> mapping, but exposes the library's
/// <see cref="UiFont"/> enum instead of the fontless US layout metric.
/// </summary>
internal sealed class VerseFerriteTextMetrics : FerriteLib.UiKit.ITextMetrics
{
    internal static readonly VerseFerriteTextMetrics Instance = new();

    public float MeasureText(string text, UiFont font, float width)
    {
        GameFont oldFont = Text.Font;
        Text.Font = ToGameFont(font);
        try
        {
            return Text.CalcHeight(text ?? "", Mathf.Max(1f, width));
        }
        finally
        {
            Text.Font = oldFont;
        }
    }

    private static GameFont ToGameFont(UiFont font)
    {
        return font switch
        {
            UiFont.Tiny => GameFont.Tiny,
            UiFont.Medium => GameFont.Medium,
            _ => GameFont.Small
        };
    }
}
