using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using Verse.AI;
using UniversalSqueaker.OneShot;

namespace UniversalSqueaker;

/// <summary>Audited work processes, including their preparation Toils, not a workGiver label test.
/// Core WorkGiver return branches are audited; response scope/order remains separate and Dev-gated.</summary>
internal static class UsWorkObservation
{
    private static readonly (string Job, string Driver, Type? Type, string? Origin)[] supported = new[] {
        Pair("BeatFire", "RimWorld.JobDriver_BeatFire"),
        Pair("BuildRoof", "RimWorld.JobDriver_BuildRoof"),
        Pair("Clean", "RimWorld.JobDriver_CleanFilth"),
        Pair("ClearSnow", "RimWorld.JobDriver_ClearSnowAndSand"),
        Pair("CutPlant", "RimWorld.JobDriver_PlantCut"),
        Pair("CutPlantDesignated", "RimWorld.JobDriver_PlantCut_Designated"),
        Pair("Deconstruct", "RimWorld.JobDriver_Deconstruct"),
        Pair("DeconstructForBlueprint", "RimWorld.JobDriver_DeconstructForBlueprint"),
        Pair("DeliverFood", "RimWorld.JobDriver_FoodDeliver"),
        Pair("DoBill", "Verse.AI.JobDriver_DoBill"),
        Pair("EjectFuel", "RimWorld.JobDriver_EjectFuel"),
        Pair("EmptyThingContainer", "RimWorld.JobDriver_EmptyThingContainer"),
        Pair("EscortPrisonerToBed", "RimWorld.JobDriver_TakeToBed"),
        Pair("ExtractSkull", "RimWorld.JobDriver_ExtractSkull"),
        Pair("ExtractTree", "RimWorld.JobDriver_ExtractTree"),
        Pair("FeedPatient", "RimWorld.JobDriver_FoodFeedPatient"),
        Pair("FillFermentingBarrel", "RimWorld.JobDriver_FillFermentingBarrel"),
        Pair("FillIn", "RimWorld.JobDriver_FillIn"),
        Pair("FinishFrame", "RimWorld.JobDriver_ConstructFinishFrame"),
        Pair("FixBrokenDownBuilding", "RimWorld.JobDriver_FixBrokenDownBuilding"),
        Pair("Flick", "RimWorld.JobDriver_Flick"),
        Pair("GuiltyColonistExecution", "RimWorld.JobDriver_ExecuteGuiltyColonist"),
        Pair("Hack", "RimWorld.JobDriver_Hack"),
        Pair("Harvest", "RimWorld.JobDriver_PlantHarvest"),
        Pair("HarvestDesignated", "RimWorld.JobDriver_PlantHarvest_Designated"),
        Pair("HaulToCell", "Verse.AI.JobDriver_HaulToCell"),
        Pair("HaulToContainer", "Verse.AI.JobDriver_HaulToContainer"),
        Pair("HaulToPortal", "RimWorld.JobDriver_HaulToPortal"),
        Pair("HaulToTransporter", "RimWorld.JobDriver_HaulToTransporter"),
        Pair("Hunt", "RimWorld.JobDriver_Hunt"),
        Pair("Milk", "RimWorld.JobDriver_Milk"),
        Pair("Mine", "RimWorld.JobDriver_Mine"),
        Pair("Open", "RimWorld.JobDriver_Open"),
        Pair("OperateDeepDrill", "RimWorld.JobDriver_OperateDeepDrill"),
        Pair("OperateScanner", "RimWorld.JobDriver_OperateScanner"),
        Pair("PaintBuilding", "RimWorld.JobDriver_PaintBuilding"),
        Pair("PaintFloor", "RimWorld.JobDriver_PaintFloor"),
        Pair("PlaceNoCostFrame", "RimWorld.JobDriver_PlaceNoCostFrame"),
        Pair("PlantSeed", "RimWorld.JobDriver_PlantSeed"),
        Pair("PrepareCaravan_GatherItems", "RimWorld.JobDriver_PrepareCaravan_GatherItems"),
        Pair("PrisonerAttemptRecruit", "RimWorld.JobDriver_ChatWithPrisoner"),
        Pair("PrisonerExecution", "RimWorld.JobDriver_Execute"),
        Pair("RearmTurret", "RimWorld.JobDriver_Refuel"),
        Pair("RearmTurretAtomic", "RimWorld.JobDriver_RefuelAtomic"),
        Pair("Refuel", "RimWorld.JobDriver_Refuel"),
        Pair("RefuelAtomic", "RimWorld.JobDriver_RefuelAtomic"),
        Pair("ReleaseAnimalToWild", "RimWorld.JobDriver_ReleaseAnimalToWild"),
        Pair("ReleasePrisoner", "Verse.AI.JobDriver_ReleasePrisoner"),
        Pair("RemoveFloor", "RimWorld.JobDriver_RemoveFloor"),
        Pair("RemoveFoundation", "RimWorld.JobDriver_RemoveFoundation"),
        Pair("RemovePaintBuilding", "RimWorld.JobDriver_RemovePaintBuilding"),
        Pair("RemovePaintFloor", "RimWorld.JobDriver_RemovePaintFloor"),
        Pair("RemoveRoof", "RimWorld.JobDriver_RemoveRoof"),
        Pair("Repair", "RimWorld.JobDriver_Repair"),
        Pair("Replant", "RimWorld.JobDriver_Replant"),
        Pair("Rescue", "RimWorld.JobDriver_TakeToBed"),
        Pair("Research", "RimWorld.JobDriver_Research"),
        Pair("RopeRoamerToHitchingPost", "RimWorld.JobDriver_RopeRoamerToHitchingPost"),
        Pair("RopeRoamerToUnenclosedPen", "RimWorld.JobDriver_RopeRoamerToUnenclosedPen"),
        Pair("RopeToPen", "RimWorld.JobDriver_RopeToPen"),
        Pair("Shear", "RimWorld.JobDriver_Shear"),
        Pair("Slaughter", "RimWorld.JobDriver_Slaughter"),
        Pair("SmoothFloor", "RimWorld.JobDriver_SmoothFloor"),
        Pair("SmoothWall", "RimWorld.JobDriver_SmoothWall"),
        Pair("Sow", "RimWorld.JobDriver_PlantSow"),
        Pair("Strip", "RimWorld.JobDriver_Strip"),
        Pair("StudyInteract", "RimWorld.JobDriver_StudyInteract"),
        Pair("TakeBeerOutOfFermentingBarrel", "RimWorld.JobDriver_TakeBeerOutOfFermentingBarrel"),
        Pair("TakeInventory", "Verse.AI.JobDriver_TakeInventory", "Train"),
        Pair("TakeToBedToOperate", "RimWorld.JobDriver_TakeToBed"),
        Pair("TakeWoundedPrisonerToBed", "RimWorld.JobDriver_TakeToBed"),
        Pair("Tame", "RimWorld.JobDriver_Tame"),
        Pair("TendPatient", "RimWorld.JobDriver_TendPatient"),
        Pair("Train", "RimWorld.JobDriver_Train"),
        Pair("Uninstall", "RimWorld.JobDriver_Uninstall"),
        Pair("UnloadInventory", "RimWorld.JobDriver_UnloadInventory"),
        Pair("Unrope", "RimWorld.JobDriver_Unrope"),
        Pair("VisitSickPawn", "RimWorld.JobDriver_VisitSickPawn", "VisitSickPawn") };
    private static (string, string, Type?, string?) Pair(string job, string driver, string? origin = null) => (job, driver, AccessTools.TypeByName(driver), origin);
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
        // Native EndCurrentJob marks ended before Cleanup/ClearDriver. Its retained
        // current Toil during that exit window is not a live work process.
        if (driver.ended) return Truth.False;
        if (row.Origin != null && job.workGiverDef?.defName != row.Origin)
        {
            diagnostic = new("us.core/WorkState", "WorkObservation", "SpecificWorkGiverRequired:" + row.Origin);
            return Truth.Unknown;
        }
        return UsIngestionObservation.Current!.Invoke(driver, null) != null ? Truth.True : Truth.False;
    }
}
