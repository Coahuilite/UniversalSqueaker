using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S2 分层动作调音记录（Global/Race/Xenotype 统一数据面）。
/// 三态分层：全空=Global，仅 race=Race，race+xeno=Xenotype；hasX=false = 该层不决定、向下继承。
/// actionKey 使用完整ActionId；不读取短键或旧枚举序数。
/// </summary>
public class ActionTuningRecord : IExposable
{
    public string actionKey = "";
    public string raceDefName = "";
    public string xenotypeDefName = "";
    public string serviceFamily = "Pawn";
    public string targetNamespace = "";
    public string objectTarget = "";
    // 来源标记：由预设导入器写入的预设 Def.defName；手工编辑的记录保持空串。
    public string sourcePresetDefName = "";

    public bool hasScope;
    public BehaviorScope scope = BehaviorScope.AnyOccurrence;
    public List<ResponseTuningValue> responses = new();
    public List<ResponseParameterValue> responseParameters = new();
    public bool hasEnabled, enabled = true;
    public bool hasSimulationIntervalTicks; public long simulationIntervalTicks;
    private bool unsupportedLegacyTicks;
    private long retainedLegacyTicks;
    internal bool HasUnsupportedTiming => unsupportedLegacyTicks;
    public bool hasInteractionIntervalSeconds; public double interactionIntervalSeconds;
    public bool hasTalkingEnabled, talkingEnabled;
    public bool hasVolume; public double volume = 1;
    public bool hasPitch; public double pitch = 1;
    public bool hasEatPhase; public EatPhase eatPhase;
    public bool hasNutritionScope; public EatNutritionScope nutritionScope;
    public bool hasDrugScope; public EatDrugScope drugScope;
    public bool hasSelectRequirePlayerControlled, selectRequirePlayerControlled = true;
    public bool hasSelectRequireAwake, selectRequireAwake = true;
    public bool hasSelectRequireNotDowned, selectRequireNotDowned = true;
    public bool CarriesBehaviorFields => unsupportedLegacyTicks || hasEnabled || hasSimulationIntervalTicks || hasInteractionIntervalSeconds || hasTalkingEnabled || hasVolume || hasPitch
        || hasEatPhase || hasNutritionScope || hasDrugScope
        || hasSelectRequirePlayerControlled || hasSelectRequireAwake || hasSelectRequireNotDowned;
    internal void ClearBehaviorFields()
    {
        hasEnabled = hasSimulationIntervalTicks = hasTalkingEnabled = hasVolume = hasPitch = hasEatPhase = hasNutritionScope = hasDrugScope = false;
        hasInteractionIntervalSeconds = false;
        unsupportedLegacyTicks = false;
        retainedLegacyTicks = 0;
        hasSelectRequirePlayerControlled = hasSelectRequireAwake = hasSelectRequireNotDowned = false;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref actionKey, "actionKey", "");
        Scribe_Values.Look(ref raceDefName, "raceDefName", "");
        Scribe_Values.Look(ref xenotypeDefName, "xenotypeDefName", "");
        Scribe_Values.Look(ref serviceFamily, "serviceFamily", "Pawn");
        Scribe_Values.Look(ref targetNamespace, "targetNamespace", "");
        Scribe_Values.Look(ref objectTarget, "objectTarget", "");
        Scribe_Values.Look(ref sourcePresetDefName, "sourcePresetDefName", "");
        Scribe_Values.Look(ref hasScope, "hasScope", false);
        Scribe_Values.Look(ref scope, "scope", BehaviorScope.AnyOccurrence);
        Scribe_Values.Look(ref hasEnabled, "hasEnabled", false); Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref hasSimulationIntervalTicks, "hasSimulationIntervalTicks", false); Scribe_Values.Look(ref simulationIntervalTicks, "simulationIntervalTicks", 0L);
        // Keep rejected bytes representable on unrelated saves; only explicit row Reset/replacement removes them.
        Scribe_Values.Look(ref unsupportedLegacyTicks, "hasHardIntervalTicks", false); Scribe_Values.Look(ref retainedLegacyTicks, "hardIntervalTicks", 0L);
        Scribe_Values.Look(ref hasInteractionIntervalSeconds, "hasInteractionIntervalSeconds", false); Scribe_Values.Look(ref interactionIntervalSeconds, "interactionIntervalSeconds", 0d);
        Scribe_Values.Look(ref hasTalkingEnabled, "hasTalkingEnabled", false); Scribe_Values.Look(ref talkingEnabled, "talkingEnabled", false);
        Scribe_Values.Look(ref hasVolume, "hasVolume", false); Scribe_Values.Look(ref volume, "volume", 1d);
        Scribe_Values.Look(ref hasPitch, "hasPitch", false); Scribe_Values.Look(ref pitch, "pitch", 1d);
        Scribe_Values.Look(ref hasEatPhase, "hasEatPhase", false); Scribe_Values.Look(ref eatPhase, "eatPhase", EatPhase.WholeProcess);
        Scribe_Values.Look(ref hasNutritionScope, "hasNutritionScope", false); Scribe_Values.Look(ref nutritionScope, "nutritionScope", EatNutritionScope.Any);
        Scribe_Values.Look(ref hasDrugScope, "hasDrugScope", false); Scribe_Values.Look(ref drugScope, "drugScope", EatDrugScope.Any);
        Scribe_Values.Look(ref hasSelectRequirePlayerControlled, "hasSelectRequirePlayerControlled", false); Scribe_Values.Look(ref selectRequirePlayerControlled, "selectRequirePlayerControlled", true);
        Scribe_Values.Look(ref hasSelectRequireAwake, "hasSelectRequireAwake", false); Scribe_Values.Look(ref selectRequireAwake, "selectRequireAwake", true);
        Scribe_Values.Look(ref hasSelectRequireNotDowned, "hasSelectRequireNotDowned", false); Scribe_Values.Look(ref selectRequireNotDowned, "selectRequireNotDowned", true);
        Scribe_Collections.Look(ref responses, "responses", LookMode.Deep);
        Scribe_Collections.Look(ref responseParameters, "responseParameters", LookMode.Deep);
        responses ??= new List<ResponseTuningValue>();
        responseParameters ??= new List<ResponseParameterValue>();
    }

    /// <summary>分层判定：全空=Global，仅 race=Race，race+xeno=Xenotype。非法（race 空但 xeno 非空）返回 false。</summary>
    public bool IsValidLayer(out int layer)
    {
        if (serviceFamily == "NonPawn")
        {
            layer = objectTarget == "" ? 0 : 1;
            return targetNamespace != "" && targetNamespace == targetNamespace.Trim() && objectTarget == objectTarget.Trim()
                && raceDefName == "" && xenotypeDefName == "";
        }
        if (serviceFamily != "Pawn" || targetNamespace != "" || objectTarget != "") { layer = -1; return false; }
        bool hasRace = !string.IsNullOrEmpty(raceDefName);
        bool hasXeno = !string.IsNullOrEmpty(xenotypeDefName);
        if (!hasRace && !hasXeno) { layer = 0; return true; }      // Global
        if (hasRace && !hasXeno) { layer = 1; return true; }         // Race
        if (hasRace && hasXeno) { layer = 2; return true; }          // Xenotype
        layer = -1; return false;                                   // 非法
    }

    internal static ActionTuningRecord Clone(ActionTuningRecord value) => new()
    {
        actionKey = value.actionKey,
        raceDefName = value.raceDefName,
        xenotypeDefName = value.xenotypeDefName,
        serviceFamily = value.serviceFamily, targetNamespace = value.targetNamespace, objectTarget = value.objectTarget,
        sourcePresetDefName = value.sourcePresetDefName,
        hasScope = value.hasScope,
        scope = value.scope,
        hasEnabled = value.hasEnabled, enabled = value.enabled,
        hasSimulationIntervalTicks = value.hasSimulationIntervalTicks, simulationIntervalTicks = value.simulationIntervalTicks,
        unsupportedLegacyTicks = value.unsupportedLegacyTicks, retainedLegacyTicks = value.retainedLegacyTicks,
        hasInteractionIntervalSeconds = value.hasInteractionIntervalSeconds, interactionIntervalSeconds = value.interactionIntervalSeconds,
        hasTalkingEnabled = value.hasTalkingEnabled, talkingEnabled = value.talkingEnabled,
        hasVolume = value.hasVolume, volume = value.volume, hasPitch = value.hasPitch, pitch = value.pitch,
        hasEatPhase = value.hasEatPhase, eatPhase = value.eatPhase,
        hasNutritionScope = value.hasNutritionScope, nutritionScope = value.nutritionScope,
        hasDrugScope = value.hasDrugScope, drugScope = value.drugScope,
        hasSelectRequirePlayerControlled = value.hasSelectRequirePlayerControlled, selectRequirePlayerControlled = value.selectRequirePlayerControlled,
        hasSelectRequireAwake = value.hasSelectRequireAwake, selectRequireAwake = value.selectRequireAwake,
        hasSelectRequireNotDowned = value.hasSelectRequireNotDowned, selectRequireNotDowned = value.selectRequireNotDowned,
        responses = value.responses.Select(x => x.Copy()).ToList(),
        responseParameters = value.responseParameters.Select(x => x.Copy()).ToList(),
    };
}

public sealed class ResponseTuningValue : IExposable
{
    public string key = "";
    public bool hasEnabled;
    public bool enabled;
    public bool hasParticipation;
    public bool apply = true;
    public void ExposeData()
    {
        Scribe_Values.Look(ref key, "key", "");
        Scribe_Values.Look(ref hasEnabled, "hasEnabled", false);
        Scribe_Values.Look(ref enabled, "enabled", false);
        Scribe_Values.Look(ref hasParticipation, "hasParticipation", false);
        Scribe_Values.Look(ref apply, "apply", true);
    }
    internal ResponseTuningValue Copy() => (ResponseTuningValue)MemberwiseClone();
}

public sealed class ResponseParameterValue : IExposable
{
    public string key = "";
    public double value;
    public void ExposeData()
    {
        Scribe_Values.Look(ref key, "key", "");
        Scribe_Values.Look(ref value, "value", 0d);
    }
    internal ResponseParameterValue Copy() => (ResponseParameterValue)MemberwiseClone();
}
