using HarmonyLib;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S4 diagnostics lifecycle: per-frame teardown guard. The panel and the on-pawn marks are
/// driven only through public APIs (CompSqueaker.PostDraw / Window), so the only hook needed is
/// a Root.Update postfix that re-checks the current map and clears the session when it changes.
/// No MapInterface reflection hook is restored (US red line).
/// </summary>
[HarmonyPatch(typeof(Root), nameof(Root.Update))]
public static class Patch_Root_DiagnosticsLifecycle
{
    private static void Postfix()
    {
        SqueakDiagnosticsOverlay.MaintainLifecycle();
        // S4 diagnostics: Root.Update runs every frame in a map scene and is the only layout-period
        // driver. Without this the cached snapshot set is never filled (the SR MapInterface hook was
        // deliberately not restored), so RefreshIfDue must be driven here too.
        SqueakDiagnosticsOverlay.RefreshIfDue();
    }
}
