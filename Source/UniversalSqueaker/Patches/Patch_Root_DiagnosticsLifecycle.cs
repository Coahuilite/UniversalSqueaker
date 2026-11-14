using HarmonyLib;
using Verse;
using UniversalSqueaker.UI;

namespace UniversalSqueaker;

/// <summary>
/// S4 diagnostics lifecycle: per-frame teardown guard. The panel and the on-pawn marks are
/// kept separate: Root.Update re-checks the map and refreshes the existing cached diagnostics;
/// AfterMainTabs paints the cached marks through the US screen control. No drawing or extra snapshot
/// work belongs in this lifecycle guard, and CompSqueaker.PostDraw is not a UI entry.
/// </summary>
[HarmonyPatch(typeof(Root), nameof(Root.Update))]
public static class Patch_Root_DiagnosticsLifecycle
{
    private static void Postfix()
    {
        SqueakDiagnosticsOverlay.MaintainLifecycle();
        // S4 diagnostics: Root.Update runs every frame in a map scene and is the only layout-period
        // driver. The AfterMainTabs screen path reads cached values and must never fill snapshots.
        SqueakDiagnosticsOverlay.RefreshIfDue();
        // Camera indicator overlay (second UiKit host): same per-frame teardown gate. The host is
        // disposed as soon as the toggle is off or the map is gone, even in frames where the date
        // bar (and therefore the draw dispatcher) does not run.
        UsCameraIndicatorOverlay.Maintain();
    }
}
