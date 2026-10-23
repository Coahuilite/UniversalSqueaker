namespace UniversalSqueaker;

public enum EatPhase { WholeProcess, Execution }
public enum EatNutritionScope { Any, NutritionGiving, NonNutritionGiving }
public enum EatDrugScope { Any, ExcludeDrugs, OnlyDrugs }

/// <summary>One returned native ingestion call, not Job completion or a cached ongoing state.</summary>
public sealed class IngestionResultFact
{
    public readonly string Ingester, Item, ItemDef;
    public readonly double Nutrition;
    public IngestionResultFact(string ingester, string item, string itemDef, double nutrition)
    { Ingester = ingester; Item = item; ItemDef = itemDef; Nutrition = nutrition; }
}

/// <summary>Parameter-selected restrictions only; fixed author conditions remain outside this policy.</summary>
public static class EatPolicy
{
    public const string Process = "us.core/IngestionProcess";
    public const string Execution = "us.core/IngestionExecution";
    public const string Nutrition = "us.core/IngestibleNutrition";
    public const string Drug = "us.core/IngestibleDrug";
    public const string Result = "us.core/IngestionResultEvent";
}
