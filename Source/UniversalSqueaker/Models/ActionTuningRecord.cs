using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S2 分层动作调音记录（Global/Race/Xenotype 统一数据面）。
/// 三态分层：全空=Global，仅 race=Race，race+xeno=Xenotype；hasX=false = 该层不决定、向下继承。
/// actionKey 是字符串（内置键 = BuiltInActionKeys，与 SqueakAction 枚举序数对齐）。
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
    public SqueakActionScope scope = SqueakActionScope.AnyOccurrence;
    public bool hasIntervalMultiplier;
    public float intervalMultiplier = 1f;
    public bool hasProbabilityMultiplier;
    public float probabilityMultiplier = 1f;
    public List<ResponseTuningValue> responses = new();
    public List<ResponseParameterValue> responseParameters = new();
    public bool hasEnabled, enabled = true;
    public bool hasHardIntervalTicks; public long hardIntervalTicks;
    public bool hasTalkingEnabled, talkingEnabled;
    public bool hasVolume; public double volume = 1;
    public bool hasPitch; public double pitch = 1;
    public bool hasEatPhase; public EatPhase eatPhase;
    public bool hasNutritionScope; public EatNutritionScope nutritionScope;
    public bool hasDrugScope; public EatDrugScope drugScope;
    public bool CarriesBehaviorFields => hasEnabled || hasHardIntervalTicks || hasTalkingEnabled || hasVolume || hasPitch
        || hasEatPhase || hasNutritionScope || hasDrugScope;
    internal void ClearBehaviorFields()
    {
        hasEnabled = hasHardIntervalTicks = hasTalkingEnabled = hasVolume = hasPitch = hasEatPhase = hasNutritionScope = hasDrugScope = false;
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
        Scribe_Values.Look(ref scope, "scope", SqueakActionScope.AnyOccurrence);
        Scribe_Values.Look(ref hasIntervalMultiplier, "hasIntervalMultiplier", false);
        Scribe_Values.Look(ref intervalMultiplier, "intervalMultiplier", 1f);
        Scribe_Values.Look(ref hasProbabilityMultiplier, "hasProbabilityMultiplier", false);
        Scribe_Values.Look(ref probabilityMultiplier, "probabilityMultiplier", 1f);
        Scribe_Values.Look(ref hasEnabled, "hasEnabled", false); Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref hasHardIntervalTicks, "hasHardIntervalTicks", false); Scribe_Values.Look(ref hardIntervalTicks, "hardIntervalTicks", 0L);
        Scribe_Values.Look(ref hasTalkingEnabled, "hasTalkingEnabled", false); Scribe_Values.Look(ref talkingEnabled, "talkingEnabled", false);
        Scribe_Values.Look(ref hasVolume, "hasVolume", false); Scribe_Values.Look(ref volume, "volume", 1d);
        Scribe_Values.Look(ref hasPitch, "hasPitch", false); Scribe_Values.Look(ref pitch, "pitch", 1d);
        Scribe_Values.Look(ref hasEatPhase, "hasEatPhase", false); Scribe_Values.Look(ref eatPhase, "eatPhase", EatPhase.WholeProcess);
        Scribe_Values.Look(ref hasNutritionScope, "hasNutritionScope", false); Scribe_Values.Look(ref nutritionScope, "nutritionScope", EatNutritionScope.Any);
        Scribe_Values.Look(ref hasDrugScope, "hasDrugScope", false); Scribe_Values.Look(ref drugScope, "drugScope", EatDrugScope.Any);
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
        hasIntervalMultiplier = value.hasIntervalMultiplier,
        intervalMultiplier = value.intervalMultiplier,
        hasProbabilityMultiplier = value.hasProbabilityMultiplier,
        probabilityMultiplier = value.probabilityMultiplier,
        hasEnabled = value.hasEnabled, enabled = value.enabled,
        hasHardIntervalTicks = value.hasHardIntervalTicks, hardIntervalTicks = value.hardIntervalTicks,
        hasTalkingEnabled = value.hasTalkingEnabled, talkingEnabled = value.talkingEnabled,
        hasVolume = value.hasVolume, volume = value.volume, hasPitch = value.hasPitch, pitch = value.pitch,
        hasEatPhase = value.hasEatPhase, eatPhase = value.eatPhase,
        hasNutritionScope = value.hasNutritionScope, nutritionScope = value.nutritionScope,
        hasDrugScope = value.hasDrugScope, drugScope = value.drugScope,
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
