namespace UniversalSqueaker;

/// <summary>「重置为默认」的可用性：本层是否还有可清的东西。判定只看**旗标**，不看行是否存在（F-Q）。</summary>
public enum SqueakMoodResetDefaultState
{
    /// <summary>至少一个因子旗标为 true —— 本层有设置可清。</summary>
    Ready,

    /// <summary>三个旗标皆 false —— 本行没有本层设置。</summary>
    NoLocalSetting,
}

/// <summary>「重置为预设」的可用性。判定只看**来源字段**与预设 Def/条目的可解析性，不看行是否存在（F-Q）。</summary>
public enum SqueakMoodResetPresetState
{
    /// <summary>来源非空、Def 可解析、且该 (mood,race,xeno) 在预设里有基线条目。</summary>
    Ready,

    /// <summary>来源为空 —— 此行不是预设导入的。</summary>
    NotFromPreset,

    /// <summary>来源非空但 Def 已失效（<c>GetNamedSilentFail</c> 返回 null）—— 该预设已失效。</summary>
    PresetMissing,

    /// <summary>Def 在，但该 (mood,race,xeno) 没有基线条目 —— 预设被改过，这份来源已无处可回。</summary>
    PresetHasNoEntry,
}

/// <summary>
/// 两个心情行动作的可用性判定。放在 Pure 层：不引用 Verse/Unity，也不需要 Def 注册表 —— 调用方把
/// 「Def 是否可解析」「预设是否有该行条目」作为布尔量喂进来，于是「四态 × 两动作」的矩阵可以在链接本文件
/// 的 harness 里穷举。输入刻意只有旗标与来源：**行是否存在不算可用性证据**，那是 F-Q 的教训。
/// </summary>
public static class SqueakMoodResetActions
{
    /// <summary>「重置为默认」：三个因子旗标全 false 时不可用（清无可清）。</summary>
    public static SqueakMoodResetDefaultState EvaluateDefault(bool hasPitchFactor, bool hasVolumeFactor, bool hasPitchJitter)
        => hasPitchFactor || hasVolumeFactor || hasPitchJitter
            ? SqueakMoodResetDefaultState.Ready
            : SqueakMoodResetDefaultState.NoLocalSetting;

    /// <summary>「重置为预设」：来源空 ⇒ 不是预设导入的；Def 失效 ⇒ 该预设已失效；Def 在但无该行条目 ⇒ 无处可回。</summary>
    public static SqueakMoodResetPresetState EvaluatePreset(string? sourcePresetDefName, bool presetDefResolved, bool presetHasEntry)
    {
        if (string.IsNullOrEmpty(sourcePresetDefName)) return SqueakMoodResetPresetState.NotFromPreset;
        if (!presetDefResolved) return SqueakMoodResetPresetState.PresetMissing;
        return presetHasEntry ? SqueakMoodResetPresetState.Ready : SqueakMoodResetPresetState.PresetHasNoEntry;
    }
}
