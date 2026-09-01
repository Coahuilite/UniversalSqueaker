namespace UniversalSqueaker;

// 包域枚举（零 Verse 引用，自 SqueakVoicePackModels.cs 提取）。
// 0.3.1 起 SelectionMode 是内核 API；本文件的 SqueakVoicePackMode 仅保留为设置/适配层 ABI。

public enum SqueakVoicePackScope
{
    Unspecified = 0,
    Race = 1,
    Xenotype = 2
}

/// <summary>Audio selection policy. It is intentionally versioned separately from retired remix settings.</summary>
public enum SqueakVoicePackMode
{
    Vanilla,
    Fallback,
    Remix,
    Disabled
}

/// <summary>Camera-height attenuation quick presets. Zero-Verse enum; the settings class consumes it.</summary>
public enum SqueakDistancePreset
{
    Conservative,
    Balanced,
    Strong,
    Custom
}

/// <summary>Typed selector for the three cheap runtime-scaling settings.</summary>
public enum SqueakBasicToggle
{
    ScaleCooldown,
    ScaleTalking,
    ScalePopulation
}

/// <summary>Typed field selector for one layered mood-tuning write.</summary>
public enum SqueakMoodFactor
{
    Clear,
    Pitch,
    Volume,
    Jitter
}

/// <summary>Typed selector for the Packs-page domain filter chips.</summary>
public enum SqueakDomainFilterKind
{
    EnabledOnly,
    ConflictOnly,
    OrphanOnly
}
