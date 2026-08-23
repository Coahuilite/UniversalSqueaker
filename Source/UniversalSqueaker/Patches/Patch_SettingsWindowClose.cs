using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

/// <summary>Flushes this mod's debounced settings write while preserving normal window close behavior.</summary>
[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) })]
internal static class Patch_SettingsWindowClose
{
    [HarmonyPrefix]
    private static bool Prefix(Window window, ref bool __result)
    {
        // Dialog_Options is shared by every mod. Only a dialog instance opened and registered by this mod can flush it.
        if (UniversalSqueakerMod.Instance?.IsOwnedSettingsWindow(window) == true)
        {
            SqueakRuntimeResolver.FlushPendingRuntimeChanges(true);
            UniversalSqueakerMod.NotifySettingsWindowClosing(window);
        }
        return true;
    }
}
