using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production Verse-backed <see cref="ITextMetrics"/> for both kernel Hosts. It is the only place the
/// settings page and the camera overlay measure wrapped text; the font mapping itself is the library's
/// <see cref="UiKitFonts"/> so measurement and drawing can never disagree about a font size.
/// </summary>
internal sealed class VerseFerriteTextMetrics : ITextMetrics
{
    internal static readonly VerseFerriteTextMetrics Instance = new();

    public float MeasureText(string text, UiFont font, float width)
    {
        GameFont oldFont = Text.Font;
        Text.Font = UiKitFonts.ToGameFont(font);
        try
        {
            return Text.CalcHeight(text ?? "", Mathf.Max(1f, width));
        }
        finally
        {
            Text.Font = oldFont;
        }
    }
}
