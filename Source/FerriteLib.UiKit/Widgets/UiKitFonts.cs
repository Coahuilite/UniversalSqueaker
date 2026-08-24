using Verse;

namespace FerriteLib.UiKit.Widgets;

internal static class UiKitFonts
{
    public static GameFont ToGameFont(UiFont font)
    {
        return font switch
        {
            UiFont.Tiny => GameFont.Tiny,
            UiFont.Small => GameFont.Small,
            _ => GameFont.Medium
        };
    }
}
