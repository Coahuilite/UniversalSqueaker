using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>Ephemeral native observations only. No policy, clock, random draw or save state.</summary>
internal static class UsIngestionObservation
{
    internal static readonly MethodInfo? Setup = AccessTools.Method(typeof(JobDriver), "SetupToils", Type.EmptyTypes);
    internal static readonly MethodInfo? Cleanup = AccessTools.Method(typeof(JobDriver), "Cleanup", new[] { typeof(JobCondition) });
    internal static readonly MethodInfo? Chew = AccessTools.Method(typeof(Toils_Ingest), "ChewIngestible",
        new[] { typeof(Pawn), typeof(float), typeof(TargetIndex), typeof(TargetIndex) });
    internal static readonly MethodInfo? Current = AccessTools.PropertyGetter(typeof(JobDriver), "CurToil");
    internal static readonly MethodInfo? Ingested = AccessTools.Method(typeof(Thing), "Ingested", new[] { typeof(Pawn), typeof(float) });
    internal static bool ProcessReady => Setup != null && Cleanup != null;
    internal static bool ExecutionReady => ProcessReady && Chew != null && Current != null;
    private sealed class Process
    {
        internal readonly JobDriver Driver;
        internal readonly Job Job;
        internal readonly Pawn Recipient;
        internal Toil? Execution;
        internal TargetIndex ItemIndex = TargetIndex.A;
        internal long Generation;
        internal Process(JobDriver driver, Pawn recipient) { Driver = driver; Job = driver.job; Recipient = recipient; }
        internal bool Active => ReferenceEquals(Driver.pawn.jobs?.curDriver, Driver) && ReferenceEquals(Driver.pawn.CurJob, Job);
        internal Thing? Item => Job.GetTarget(ItemIndex).Thing;
    }
    internal sealed class Result
    {
        internal readonly Pawn Ingester;
        internal readonly string ItemId, ItemDef;
        internal readonly bool Accepted;
        internal float Nutrition;
        internal Result(Thing item, Pawn ingester)
        {
            Ingester = ingester; ItemId = item.ThingID; ItemDef = item.def.defName;
            Accepted = !item.Destroyed && item.IngestibleNow;
        }
    }
    private static readonly Dictionary<JobDriver, Process> processes = new();
    private static ConditionalWeakTable<JobDriver, object> ended = new();
    private static readonly HashSet<string> reported = new(StringComparer.Ordinal);
    internal static IEnumerable<FactDiagnostic> Diagnostics
    {
        get
        {
            foreach (string key in new[] { EatPolicy.Process, EatPolicy.Execution, EatPolicy.Nutrition, EatPolicy.Drug, EatPolicy.Result })
                if (!Available(key)) yield return new FactDiagnostic(key, "IngestionObservation", "MissingTarget:" + MissingTargets);
        }
    }
    internal static string MissingTargets => string.Join(";", new[] {
        Setup == null ? "Verse.AI.JobDriver.SetupToils()" : "",
        Cleanup == null ? "Verse.AI.JobDriver.Cleanup(JobCondition)" : "",
        Chew == null ? "RimWorld.Toils_Ingest.ChewIngestible(Pawn,float,TargetIndex,TargetIndex)" : "",
        Current == null ? "Verse.AI.JobDriver.get_CurToil()" : "",
        Ingested == null ? "Verse.Thing.Ingested(Pawn,float)" : "" }.Where(x => x != ""));
    internal static bool Available(string key) => key == EatPolicy.Execution ? ExecutionReady
        : key == EatPolicy.Result ? Ingested != null : key == EatPolicy.Nutrition || key == EatPolicy.Drug ? ProcessReady : true;
    internal static bool Prepare(string capability, bool ready)
    {
        if (!ready && reported.Add(capability))
            Log.Warning("usdiag capability=IngestionObservation group=" + capability + " reason=MissingTarget:" + MissingTargets);
        return ready;
    }
    internal static void SetupProcess(JobDriver driver)
    {
        ended.Remove(driver);
        Pawn? recipient = driver is JobDriver_Ingest ? driver.pawn
            : driver is JobDriver_FoodFeedPatient ? driver.job.targetB.Pawn : null;
        if (recipient != null) processes[driver] = new Process(driver, recipient);
    }
    internal static void EndProcess(JobDriver driver)
    {
        ended.GetValue(driver, _ => new object());
        if (processes.TryGetValue(driver, out var process))
        {
            processes.Remove(driver);
            SqueakerRuntime.IngestionChanged(process.Recipient);
        }
    }
    internal static void Attach(Toil toil, Pawn chewer, TargetIndex itemIndex)
    {
        var original = toil.initAction;
        Process? entered = null;
        toil.initAction = () =>
        {
            original?.Invoke(); // failed/replaced/throwing init is not entry
            var driver = toil.actor?.jobs?.curDriver;
            if (driver == null || !ReferenceEquals(Current!.Invoke(driver, null), toil)
                || !processes.TryGetValue(driver, out var process) || !process.Active || process.Recipient != chewer) return;
            process.Execution = toil; process.ItemIndex = itemIndex; process.Generation++; entered = process;
            SqueakerRuntime.IngestionChanged(chewer);
        };
        toil.AddFinishAction(() =>
        {
            var process = entered;
            if (process != null && ReferenceEquals(process.Execution, toil))
            {
                process.Execution = null;
                process.ItemIndex = TargetIndex.A; // Supported vanilla process preparation targets A; the execution override ends with its Toil.
                entered = null;
                SqueakerRuntime.IngestionChanged(process.Recipient);
            }
        });
    }
    internal static void Capture(Pawn recipient, IDictionary<string, Truth> facts)
    {
        var active = processes.Values.Where(x => x.Recipient == recipient && x.Active).ToArray();
        var ownDriver = recipient.jobs?.curDriver;
        // A normal self Ingest can be positively classified without a phase hook; feed never uses the feeder's identity.
        bool self = recipient.CurJob?.def == JobDefOf.Ingest && ownDriver != null && !ended.TryGetValue(ownDriver, out _);
        facts[EatPolicy.Process] = active.Length > 0 || self ? Truth.True : ProcessReady ? Truth.False : Truth.Unknown;
        bool ambiguous = active.Length > 1;
        var process = active.Length == 1 ? active[0] : null;
        facts[EatPolicy.Execution] = !ExecutionReady || ambiguous ? Truth.Unknown
            : process?.Execution != null && ReferenceEquals(Current!.Invoke(process.Driver, null), process.Execution) ? Truth.True : Truth.False;
        Thing? item = ambiguous ? null : process?.Item ?? (self ? recipient.CurJob?.targetA.Thing : null);
        bool known = item?.def?.ingestible != null;
        facts[EatPolicy.Nutrition] = known ? Yes(item!.def.IsNutritionGivingIngestible) : Truth.Unknown;
        facts[EatPolicy.Drug] = known ? Yes(item!.def.IsDrug) : Truth.Unknown;
    }
    internal static string Describe(Pawn recipient)
        => string.Join(";", processes.Values.Where(x => x.Recipient == recipient && x.Active)
            .Select(x => "actor=" + x.Driver.pawn.ThingID + ",ingester=" + recipient.ThingID + ",item=" + x.Item?.ThingID + ",phaseGeneration=" + x.Generation));
    internal static void Reset() { processes.Clear(); ended = new ConditionalWeakTable<JobDriver, object>(); }
    private static Truth Yes(bool value) => value ? Truth.True : Truth.False;
}

[HarmonyPatch]
internal static class Patch_Ingestion_Setup
{
    private static bool Prepare() => UsIngestionObservation.Prepare("Process:SetupToils/Cleanup", UsIngestionObservation.ProcessReady);
    private static MethodBase TargetMethod() => UsIngestionObservation.Setup!;
    private static void Postfix(JobDriver __instance) => UsIngestionObservation.SetupProcess(__instance);
}
[HarmonyPatch]
internal static class Patch_Ingestion_Cleanup
{
    private static bool Prepare() => UsIngestionObservation.Prepare("Process:SetupToils/Cleanup", UsIngestionObservation.ProcessReady);
    private static MethodBase TargetMethod() => UsIngestionObservation.Cleanup!;
    private static void Prefix(JobDriver __instance) => UsIngestionObservation.EndProcess(__instance);
}
[HarmonyPatch]
internal static class Patch_Ingestion_Chew
{
    private static bool Prepare() => UsIngestionObservation.Prepare("Execution:ChewIngestible/CurToil/Process", UsIngestionObservation.ExecutionReady);
    private static MethodBase TargetMethod() => UsIngestionObservation.Chew!;
    private static void Postfix(Toil __result, Pawn __0, TargetIndex __2) => UsIngestionObservation.Attach(__result, __0, __2);
}
[HarmonyPatch]
internal static class Patch_Ingestion_Result
{
    private static bool Prepare() => UsIngestionObservation.Prepare("Result:Thing.Ingested", UsIngestionObservation.Ingested != null);
    private static MethodBase TargetMethod() => UsIngestionObservation.Ingested!;
    private static void Prefix(Thing __instance, Pawn __0, out UsIngestionObservation.Result __state)
        => __state = new UsIngestionObservation.Result(__instance, __0);
    private static void Postfix(float __result, UsIngestionObservation.Result __state)
    {
        if (!__state.Accepted) return; // vanilla guard return is not ingestion; a valid zero result is.
        __state.Nutrition = __result;
        SqueakerRuntime.IngestionResult(__state);
    }
}
