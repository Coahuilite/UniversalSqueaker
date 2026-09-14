using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Draws the S4 diagnostics head marks in the IMGUI pass.
///
/// Why this hook: <c>GenMapUI.DrawText</c> goes through the game's IMGUI text path, so it may only be
/// called from OnGUI. The previous call site was <see cref="CompSqueaker.PostDraw"/> (reached through
/// <c>Pawn.Draw → Comps_PostDraw()</c>, RimWorld <c>Verse/Pawn.cs:2754</c>), which runs during thing
/// rendering, not in OnGUI - the first real run logged "You can only call GUI functions from inside
/// OnGUI" 8558 times (83.8% of the log) and drew no marker at all.
///
/// Why this phase: <c>MapInterface.MapInterfaceOnGUI_AfterMainTabs</c> (RimWorld
/// <c>MapInterface.cs:84</c>) is called from <c>UIRoot_Play.UIRootOnGUI</c> (RimWorld
/// <c>UIRoot_Play.cs:24,35</c>), the game's OnGUI entry point, once per map per frame - the same phase
/// the map HUD itself draws in, so world-to-screen conversion inside GenMapUI is valid there.
///
/// Failure stays fail-closed but is reported through <see cref="SqueakLog.DiagnosticsMarkDrawFailed"/>,
/// which is once-per-exception-type per session: this hook runs every frame, and an undeduplicated
/// per-frame line is exactly the 8558-line incident. The bound is asserted by the log lane.
/// </summary>
[HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceOnGUI_AfterMainTabs))]
internal static class Patch_MapInterface_DiagnosticsMarks
{
    private static void Postfix()
    {
        if (!SqueakDiagnosticsOverlay.IsSessionActive) return;

        Map? map = Find.CurrentMap;
        if (map == null) return;

        try
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                pawns[i].GetComp<CompSqueaker>()?.TryDrawDiagnosticsMark();
            }
        }
        catch (Exception ex)
        {
            // Fail closed - a modded pawn must never break the game frame - but at most once per
            // exception type per session, and never silently.
            SqueakLog.DiagnosticsMarkDrawFailed(ex);
        }
    }
}
