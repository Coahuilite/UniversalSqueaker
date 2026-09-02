using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production Verse-backed <see cref="ITextMetrics"/> for both kernel Hosts. It is the only place the
/// settings page and the camera overlay measure text; the font mapping itself is the library's
/// <see cref="UiKitFonts"/> so measurement and drawing can never disagree about a font size. Verse's
/// text engine measures through process-global state, so every entry saves and restores all three
/// properties it touches.
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

    public float MeasureWidth(string text, UiFont font)
    {
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        bool oldWrap = Text.WordWrap;
        Text.Font = UiKitFonts.ToGameFont(font);
        Text.Anchor = TextAnchor.UpperLeft;
        // Wrapping would cap the reported width at the wrap point and hide exactly the overflow the
        // caller is looking for; the unwrapped extent is the number that must be compared to a rect.
        Text.WordWrap = false;
        try
        {
            return Text.CalcSize(text ?? "").x;
        }
        finally
        {
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            Text.WordWrap = oldWrap;
        }
    }
}
