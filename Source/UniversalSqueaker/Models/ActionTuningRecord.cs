using System.Collections.Generic;
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

    public bool hasScope;
    public SqueakActionScope scope = SqueakActionScope.AnyOccurrence;
    public bool hasIntervalMultiplier;
    public float intervalMultiplier = 1f;
    public bool hasProbabilityMultiplier;
    public float probabilityMultiplier = 1f;

    public void ExposeData()
    {
        Scribe_Values.Look(ref actionKey, "actionKey", "");
        Scribe_Values.Look(ref raceDefName, "raceDefName", "");
        Scribe_Values.Look(ref xenotypeDefName, "xenotypeDefName", "");
        Scribe_Values.Look(ref hasScope, "hasScope", false);
        Scribe_Values.Look(ref scope, "scope", SqueakActionScope.AnyOccurrence);
        Scribe_Values.Look(ref hasIntervalMultiplier, "hasIntervalMultiplier", false);
        Scribe_Values.Look(ref intervalMultiplier, "intervalMultiplier", 1f);
        Scribe_Values.Look(ref hasProbabilityMultiplier, "hasProbabilityMultiplier", false);
        Scribe_Values.Look(ref probabilityMultiplier, "probabilityMultiplier", 1f);
    }

    /// <summary>分层判定：全空=Global，仅 race=Race，race+xeno=Xenotype。非法（race 空但 xeno 非空）返回 false。</summary>
    public bool IsValidLayer(out int layer)
    {
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
        hasScope = value.hasScope,
        scope = value.scope,
        hasIntervalMultiplier = value.hasIntervalMultiplier,
        intervalMultiplier = value.intervalMultiplier,
        hasProbabilityMultiplier = value.hasProbabilityMultiplier,
        probabilityMultiplier = value.probabilityMultiplier,
    };
}
