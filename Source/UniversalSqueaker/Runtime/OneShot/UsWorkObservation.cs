using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using Verse.AI;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

/// <summary>Audited work processes, including their preparation Toils, not a workGiver label test.
/// The ordinary coverage/qualification decision remains pending; the runtime has only the explicit Dev owner.</summary>
internal static class UsWorkObservation
{
    private static readonly (string Job, string Driver, Type? Type)[] supported = new[] {
        Pair("Research", "RimWorld.JobDriver_Research"), Pair("Mine", "RimWorld.JobDriver_Mine"),
        Pair("DoBill", "Verse.AI.JobDriver_DoBill"), Pair("Repair", "RimWorld.JobDriver_Repair"),
        Pair("Clean", "RimWorld.JobDriver_CleanFilth"), Pair("HaulToCell", "Verse.AI.JobDriver_HaulToCell"),
        Pair("FinishFrame", "RimWorld.JobDriver_ConstructFinishFrame") };
    private static (string, string, Type?) Pair(string job, string driver) => (job, driver, AccessTools.TypeByName(driver));
    internal static bool Ready => UsIngestionObservation.Current != null && supported.Any(x => x.Type != null);
    internal static Truth Capture(Pawn pawn, out FactDiagnostic? diagnostic)
    {
        diagnostic = null;
        var job = pawn.CurJob;
        if (job == null) return Truth.False;
        var row = supported.FirstOrDefault(x => x.Job == job.def?.defName);
        // An unaudited job might genuinely be work. Neither its label nor its absence proves otherwise.
        string missing = row.Job == null ? "UnsupportedWorkProcess:" + job.def?.defName
            : row.Type == null ? "MissingDriver:" + row.Driver
            : UsIngestionObservation.Current == null ? "MissingTarget:JobDriver.get_CurToil" : "";
        if (missing != "")
        {
            diagnostic = new("us.core/WorkState", "WorkObservation", missing);
            return Truth.Unknown;
        }
        var driver = pawn.jobs?.curDriver;
        if (driver == null || !ReferenceEquals(driver.job, job) || !ReferenceEquals(driver.pawn, pawn)
            || !row.Type!.IsInstanceOfType(driver))
        {
            diagnostic = new("us.core/WorkState", "WorkObservation", "CurrentDriverSubjectOrJobMismatch:" + row.Driver);
            return Truth.Unknown;
        }
        return UsIngestionObservation.Current!.Invoke(driver, null) != null ? Truth.True : Truth.False;
    }
}
