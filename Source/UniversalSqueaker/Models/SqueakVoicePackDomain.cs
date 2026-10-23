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
/// <summary>Domain scan state (PURE enum - the whole UI and the kernel speak it).
/// (Moved next to the other routing vocabulary so zero-game harnesses can compile it.)</summary>
public enum SqueakVoicePackDomainState { Available, Dormant, TargetUnavailable, Orphan }

public enum SqueakVoicePackMode
{
    Vanilla,
    Fallback,
    Remix,
    Disabled
}

/// <summary>
/// The explicitly handled routing-mode set. The resolver normalises persisted values through
/// <see cref="IsKnown"/> instead of a growing ternary, so an unrecognised value is reported rather than
/// silently absorbed as Vanilla; the UI logic gate additionally asserts that this list still equals
/// <c>Enum.GetValues(typeof(SqueakVoicePackMode))</c>, which makes adding a fifth member without writing
/// down its semantics a build failure instead of a silent behaviour change.
/// </summary>
public static class SqueakVoicePackModes
{
    public static readonly SqueakVoicePackMode[] All =
    {
        SqueakVoicePackMode.Vanilla,
        SqueakVoicePackMode.Fallback,
        SqueakVoicePackMode.Remix,
        SqueakVoicePackMode.Disabled,
    };

    public static bool IsKnown(SqueakVoicePackMode mode)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i] == mode) return true;
        }
        return false;
    }
}

/// <summary>Camera-height attenuation quick presets. Zero-Verse enum; the settings class consumes it.</summary>
public enum SqueakDistancePreset
{
    Conservative,
    Balanced,
    Strong,
    Custom
}

/// <summary>Typed selector for the Packs-page domain filter chips.</summary>
public enum SqueakDomainFilterKind
{
    EnabledOnly,
    ConflictOnly,
    OrphanOnly
}
