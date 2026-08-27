using System.Collections.Generic;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// S5 分层心情调音记录（Global/Race/Xenotype 统一数据面，镜像 <see cref="ActionTuningRecord"/>）。
/// 三态分层：全空=Global，仅 race=Race，race+xeno=Xenotype；
/// hasX=false = 该因子不决定、向下继承底层（字段级 last-wins）。
/// 运行时消费：BuildGlobalMoods / BuildRaceBehavior / BuildBehavior（层 0/1/2），
/// 层合并全部发生在快照层（H3 完成：ResolveMoodMod 不再实时读 settings）。
/// </summary>
public class MoodTuningRecord : IExposable
{
    public SqueakMood mood = SqueakMood.Neutral;
    public string raceDefName = "";
    public string xenotypeDefName = "";
    // 来源标记：由预设导入器写入的预设 Def.defName；手工编辑的记录保持空串（与 ActionTuningRecord 对齐）。
    public string sourcePresetDefName = "";

    public bool hasPitchFactor;
    public float pitchFactor = 1f;
    public bool hasVolumeFactor;
    public float volumeFactor = 1f;
    public bool hasPitchJitter;
    public FloatRange pitchJitter = FloatRange.One;

    public void ExposeData()
    {
        Scribe_Values.Look(ref mood, "mood");
        Scribe_Values.Look(ref raceDefName, "raceDefName", "");
        Scribe_Values.Look(ref xenotypeDefName, "xenotypeDefName", "");
        Scribe_Values.Look(ref sourcePresetDefName, "sourcePresetDefName", "");
        Scribe_Values.Look(ref hasPitchFactor, "hasPitchFactor", false);
        Scribe_Values.Look(ref pitchFactor, "pitchFactor", 1f);
        Scribe_Values.Look(ref hasVolumeFactor, "hasVolumeFactor", false);
        Scribe_Values.Look(ref volumeFactor, "volumeFactor", 1f);
        Scribe_Values.Look(ref hasPitchJitter, "hasPitchJitter", false);
        Scribe_Values.Look(ref pitchJitter, "pitchJitter", FloatRange.One);
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

    internal static MoodTuningRecord Clone(MoodTuningRecord value) => new()
    {
        mood = value.mood,
        raceDefName = value.raceDefName,
        xenotypeDefName = value.xenotypeDefName,
        sourcePresetDefName = value.sourcePresetDefName,
        hasPitchFactor = value.hasPitchFactor,
        pitchFactor = value.pitchFactor,
        hasVolumeFactor = value.hasVolumeFactor,
        volumeFactor = value.volumeFactor,
        hasPitchJitter = value.hasPitchJitter,
        pitchJitter = value.pitchJitter,
    };
}