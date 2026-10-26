using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.Responses;

/// <summary>Captured revision references only; not another data/lifecycle authority.</summary>
public readonly struct RuntimeView
{
    public readonly DefinitionCatalog Definitions;
    public readonly BehaviorConfiguration Configuration;
    public readonly AudioCatalog Audio;
    public RuntimeView(DefinitionCatalog definitions, BehaviorConfiguration configuration, AudioCatalog audio)
    { Definitions = definitions; Configuration = configuration; Audio = audio; }
}

/// <summary>Current neutral observations. The game adapter samples Pawn/Job/Def values before entering the owner.</summary>
public sealed class ResponseFrame
{
    public string Subject = "";
    public BehaviorDomain Domain = null!;
    public long Tick;
    public bool StateOpportunity;
    public InteractionOpportunity? Interaction;
    public string EventFact = "";
    public string SubjectRole = "Subject";
    public string? ActionFilter;
    public bool Eligible = true;
    public bool ActiveCommand;
    public bool HasVocalOrgans = true;
    public double TalkingChance = 1;
    public double StateWaitFactor = 1;
    public FactSnapshot Facts = new(Array.Empty<KeyValuePair<string, Truth>>());
    public IReadOnlyDictionary<string, double> NumericFacts = new Dictionary<string, double>();
    public ISet<string> AvailableFacts = new HashSet<string>(StringComparer.Ordinal);
    public AgeBucket Age = AgeBucket.Adult;
    public ISoundGate SoundGate = null!;
}

/// <summary>The same fixed/default/options explanation is used by response, sustained review and readers.</summary>
public static class BehaviorContract
{
    public static bool UsesEat(Condition condition) => condition.Operation == "ref"
        && (condition.Fact == EatPolicy.Process || condition.Fact == EatPolicy.Execution) || condition.Children.Any(UsesEat);
    public static bool ConsumesIngestionResult(ActionPlan action)
        => action.Condition.RequiredFacts().Contains(EatPolicy.Result, StringComparer.Ordinal);
    public static Condition EffectiveCondition(ActionPlan action, BehaviorValues behavior)
    {
        var condition = UsesEat(action.Condition) ? new Condition("all", "", action.Condition, EatConditions.Restriction(
            behavior.Phase ?? EatPhase.WholeProcess, behavior.NutritionScope ?? EatNutritionScope.Any, behavior.DrugScope ?? EatDrugScope.Any)) : action.Condition;
        return SelectConditions.UsesSelection(action.Condition) && SelectPolicy.HasParameters(behavior)
            ? new Condition("all", "", condition, SelectConditions.Restriction(behavior)) : condition;
    }
    public static BehaviorValues DeclaredDefaults(ActionPlan action) => new()
    {
        Scope = action.Scope, Enabled = true,
        SimulationIntervalTicks = BuiltInActionKeys.Contains(action.Key) ? null : action.Interval,
        InteractionIntervalSeconds = BuiltInActionKeys.Contains(action.Key) ? null : action.InteractionIntervalSeconds,
        TalkingEnabled = !action.SkipTalking, Volume = 1, Pitch = 1,
        Phase = UsesEat(action.Condition) ? EatPhase.WholeProcess : null,
        NutritionScope = UsesEat(action.Condition) ? EatNutritionScope.Any : null,
        DrugScope = UsesEat(action.Condition) ? EatDrugScope.Any : null,
        SelectRequirePlayerControlled = action.Key == CoreActions.Select ? true : null,
        SelectRequireAwake = action.Key == CoreActions.Select ? true : null,
        SelectRequireNotDowned = action.Key == CoreActions.Select ? true : null
    };
    public static BehaviorValues Defaults(ActionPlan action, UsDevelopmentPolicy policy)
        => policy.RuntimePolicy.Defaults(action);
    public static BehaviorValues Defaults(ActionPlan action, UsResponsePolicy policy) => policy.Defaults(action);
    public static string InvalidTimingUnit(string action, BehaviorValues behavior)
        => UsResponsePolicy.IsOfficialInteraction(action) && behavior.SimulationIntervalTicks.HasValue
            ? "InteractionUsesRealSeconds:RemoveSimulationIntervalTicks:" + action
            : BuiltInActionKeys.Contains(action) && !UsResponsePolicy.IsOfficialInteraction(action) && behavior.InteractionIntervalSeconds.HasValue
                ? "SimulationUsesGameTicks:RemoveInteractionIntervalSeconds:" + action : "";
    public static string InvalidParameters(ActionPlan action, BehaviorValues behavior)
    {
        string timing = InvalidTimingUnit(action.Key, behavior);
        if (timing != "") return timing;
        return !UsesEat(action.Condition) && (behavior.Phase.HasValue || behavior.NutritionScope.HasValue || behavior.DrugScope.HasValue)
            ? "EatParametersRequireIngestionContract"
            : !SelectConditions.UsesSelection(action.Condition) && SelectPolicy.HasParameters(behavior)
                ? "SelectParametersRequireSelectionContract" : "";
    }
    public static bool Authorized(BehaviorConfiguration configuration, EffectiveBehavior effective, BehaviorValues behavior, ResponseFrame frame)
        => configuration.SystemEnabled && !effective.Paused && behavior.Enabled != false && behavior.Scope != BehaviorScope.Disabled
            && (behavior.Scope != BehaviorScope.ActiveCommand || frame.ActiveCommand && frame.Facts.Read("us.core/PlayerControlled") == Truth.True);
}
