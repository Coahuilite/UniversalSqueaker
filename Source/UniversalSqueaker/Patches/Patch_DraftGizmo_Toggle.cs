using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UniversalSqueaker;

/// <summary>Wraps only the player-facing Draft gizmo action, never Pawn_DraftController.Drafted setters.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
public static class Patch_DraftGizmo_Toggle
{
    private static readonly ConditionalWeakTable<Command_Toggle, object> WrappedCommands = new();
    private static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
    {
        __result = WrapDraftToggle(__result, __instance);
    }

    private static IEnumerable<Gizmo> WrapDraftToggle(IEnumerable<Gizmo> source, Pawn pawn)
    {
        foreach (Gizmo gizmo in source)
        {
            if (gizmo is Command_Toggle toggle && IsVanillaDraftCommand(toggle) && !WrappedCommands.TryGetValue(toggle, out _))
            {
                WrappedCommands.Add(toggle, new object());
                Action? original = toggle.toggleAction;
                toggle.toggleAction = () =>
                {
                    bool before = pawn.Drafted;
                    original?.Invoke();
                    if (pawn.Drafted != before) pawn.GetComp<CompSqueaker>()?.Notify_Draft(pawn.Drafted);
                };
            }
            yield return gizmo;
        }
    }

    private static bool IsVanillaDraftCommand(Command_Toggle toggle)
    {
        // Stable Core command metadata; do not inspect translated labels.
        return toggle.hotKey == KeyBindingDefOf.Command_ColonistDraft
            && (toggle.tutorTag == "Draft" || toggle.tutorTag == "Undraft");
    }
}
