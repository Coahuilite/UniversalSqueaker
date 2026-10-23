using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using Verse;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

/// <summary>Required native event seams. One missing implementation isolates that capability.</summary>
internal static class UsPawnEventObservation
{
    internal static readonly MethodInfo? Wounded = AccessTools.Method(typeof(Pawn), "PostApplyDamage", new[] { typeof(DamageInfo), typeof(float) });
    internal static readonly MethodInfo? Death = AccessTools.Method(typeof(Pawn), "Kill", new[] { typeof(DamageInfo?), typeof(Hediff) });
    internal static readonly MethodInfo? Equip = AccessTools.Method(typeof(Pawn_EquipmentTracker), "Notify_EquipmentAdded", new[] { typeof(ThingWithComps) });
    internal static readonly MethodInfo? Draft = AccessTools.Method(typeof(Pawn), "GetGizmos", Type.EmptyTypes);
    private static readonly (string Type, MethodInfo? Target)[] mentalBreak = new[] {
        BreakTarget("MentalBreakWorker"), BreakTarget("MentalBreakWorker_Catatonic"),
        BreakTarget("MentalBreakWorker_RunWild"), BreakTarget("MentalBreakWorker_WildDecree") };
    internal static IEnumerable<MethodBase> MentalBreakTargets => mentalBreak.Where(x => x.Target != null).Select(x => (MethodBase)x.Target!);
    private static (string, MethodInfo?) BreakTarget(string type)
    {
        Type? resolved = AccessTools.TypeByName("Verse.AI." + type) ?? AccessTools.TypeByName("RimWorld." + type);
        return (type, resolved == null ? null : AccessTools.DeclaredMethod(resolved, "TryStart", new[] { typeof(Pawn), typeof(string), typeof(bool) }));
    }
    private static readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private static IEnumerable<(string Key, MethodInfo? Target, string Name)> Seams
    {
        get
        {
            yield return ("us.core/WoundedEvent", Wounded, "Pawn.PostApplyDamage(DamageInfo,float)");
            yield return ("us.core/DeathEvent", Death, "Pawn.Kill(DamageInfo?,Hediff)");
            yield return ("us.core/EquipEvent", Equip, "Pawn_EquipmentTracker.Notify_EquipmentAdded(ThingWithComps)");
            yield return ("us.core/DraftEvent", Draft, "Pawn.GetGizmos()");
            yield return ("us.core/UndraftEvent", Draft, "Pawn.GetGizmos()");
            foreach (var row in mentalBreak) yield return ("us.core/MentalBreakEvent", row.Target, row.Type + ".TryStart(Pawn,string,bool)");
        }
    }
    internal static bool Available(string key)
    {
        if (key == "us.core/MentalBreakEvent") return mentalBreak.Any(x => x.Target != null);
        foreach (var seam in Seams) if (seam.Key == key) return seam.Target != null;
        return true;
    }
    internal static IEnumerable<FactDiagnostic> Diagnostics
    {
        get { foreach (var seam in Seams) if (seam.Target == null) yield return new(seam.Key, "PawnEventObservation", "MissingTarget:" + seam.Name); }
    }
    internal static bool Prepare(string key)
    {
        foreach (var item in Diagnostics)
            if (item.Key == key && reported.Add(key + item.Reason)) Log.Warning("usdiag capability=PawnEventObservation fact=" + key + " reason=" + item.Reason);
        return Available(key);
    }
}
