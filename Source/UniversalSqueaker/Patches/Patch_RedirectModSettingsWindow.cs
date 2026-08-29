using HarmonyLib;
using RimWorld;
using Verse;
using UniversalSqueaker.UI;

namespace UniversalSqueaker;

/// <summary>
/// Redirects the stock RimWorld mod-settings dialog to US's full-screen custom window whenever the
/// selected mod is Universal Squeaker. Both <c>Page_ModsConfig</c> and <c>Dialog_Options</c> open
/// <c>Dialog_ModSettings</c>; intercepting <see cref="WindowStack.Add"/> covers every entry point.
/// </summary>
[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
internal static class Patch_RedirectModSettingsWindow
{
    private static bool Prefix(Window window)
    {
        if (window is Dialog_ModSettings dialog)
        {
            Mod? owner = AccessTools.Field(typeof(Dialog_ModSettings), "mod")?.GetValue(dialog) as Mod;
            if (owner is UniversalSqueakerMod usMod)
            {
                var custom = new UniversalSqueakerSettingsWindow(usMod);
                usMod.RegisterSettingsWindow(custom);
                Find.WindowStack.Add(custom);
                return false;
            }
        }

        return true;
    }
}
