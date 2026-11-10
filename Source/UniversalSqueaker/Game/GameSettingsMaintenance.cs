using HarmonyLib;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>Process/Mod-lifetime main-thread pump, independent of maps, simulation and windows.</summary>
internal static class GameSettingsMaintenance
{
    private static GameSettingsPersistence? persistence;
    private static int lastFrame = -1;

    // Startup publishes one ready owner, not a multicast subscription. Scene/window replacement
    // neither unregisters it nor creates another queue; repeated attachment is idempotent.
    internal static void Start(GameSettingsPersistence owner)
    {
        if (ReferenceEquals(persistence, owner)) return;
        persistence = owner;
        lastFrame = -1;
    }

    internal static void Update(bool rootDestroyed)
    {
        if (persistence == null || rootDestroyed || !UnityData.IsInMainThread
            || LongEventHandler.ShouldWaitForEvent || Scribe.mode != LoadSaveMode.Inactive) return;
        // Both 1.6 Root_Entry and Root_Play call base.Update, including while paused/no map.
        // A scene handover or duplicate callback in one Unity frame gets one periodic pass.
        int frame = Time.frameCount;
        if (lastFrame == frame) return;
        lastFrame = frame;
        persistence.Tick();
    }
}

[HarmonyPatch(typeof(Root), nameof(Root.Update))]
internal static class Patch_Root_SettingsMaintenance
{
    private static void Postfix(bool ___destroyed) => GameSettingsMaintenance.Update(___destroyed);
}
