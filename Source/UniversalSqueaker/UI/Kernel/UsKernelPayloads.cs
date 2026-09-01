// Typed payloads for the US kernel settings path. Every payload is a plain readonly value type
// carrying exactly the business identity needed by one IUsKernelSettingsSource write, so widgets never
// parse strings into business commands. Distance-range writes pass their endpoints as typed values,
// which is why no range payload type exists here.

namespace UniversalSqueaker.UI;

/// <summary>Layer domain identity for the tuning editor (Race layer = race only; Xenotype = race + target).</summary>
public readonly struct UsTuningDomainSelection
{
    public readonly string RaceDefName;
    public readonly string TargetDefName;

    public UsTuningDomainSelection(string raceDefName, string targetDefName)
    {
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
    }
}

/// <summary>Scope write for one built-in action at the current tuning layer (scope == null clears the layer record).</summary>
public readonly struct UsScopeWrite
{
    public readonly string ActionKey;
    public readonly SqueakActionScope? Scope;

    public UsScopeWrite(string actionKey, SqueakActionScope? scope)
    {
        ActionKey = actionKey ?? "";
        Scope = scope;
    }
}

/// <summary>Field-level mood write. Clear restores inheritance; other factors require a value.</summary>
public readonly struct UsMoodWrite
{
    public readonly SqueakMood Mood;
    public readonly SqueakMoodFactor Factor;
    public readonly float? Value;

    public UsMoodWrite(SqueakMood mood, SqueakMoodFactor factor, float? value)
    {
        Mood = mood;
        Factor = factor;
        Value = value;
    }
}

public readonly struct UsBaselineRaceToggle
{
    public readonly string PresetDefName;
    public readonly string RaceDefName;
    public readonly bool Selected;

    public UsBaselineRaceToggle(string presetDefName, string raceDefName, bool selected)
    {
        PresetDefName = presetDefName ?? "";
        RaceDefName = raceDefName ?? "";
        Selected = selected;
    }
}

public readonly struct UsBaselineXenoToggle
{
    public readonly string PresetDefName;
    public readonly string RaceDefName;
    public readonly string XenotypeDefName;
    public readonly bool Selected;

    public UsBaselineXenoToggle(string presetDefName, string raceDefName, string xenotypeDefName, bool selected)
    {
        PresetDefName = presetDefName ?? "";
        RaceDefName = raceDefName ?? "";
        XenotypeDefName = xenotypeDefName ?? "";
        Selected = selected;
    }
}

/// <summary>Domain selection write for the Packs page (race or xenotype domain).</summary>
public readonly struct UsDomainSelection
{
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    public readonly string TargetDefName;

    public UsDomainSelection(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
    }
}

/// <summary>One VoicePack checkbox write inside a domain.</summary>
public readonly struct UsPackToggle
{
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    public readonly string TargetDefName;
    public readonly string PackKey;
    public readonly bool Enabled;

    public UsPackToggle(SqueakVoicePackScope scope, string raceDefName, string targetDefName, string packKey, bool enabled)
    {
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
        PackKey = packKey ?? "";
        Enabled = enabled;
    }
}

/// <summary>Domain identity used by Forget Unavailable.</summary>
public readonly struct UsDomainIdentity
{
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    public readonly string TargetDefName;

    public UsDomainIdentity(SqueakVoicePackScope scope, string raceDefName, string targetDefName)
    {
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
    }
}

/// <summary>Typed domain-filter chip write.</summary>
public readonly struct UsDomainFilterWrite
{
    public readonly SqueakDomainFilterKind Kind;
    public readonly bool Flag;

    public UsDomainFilterWrite(SqueakDomainFilterKind kind, bool flag)
    {
        Kind = kind;
        Flag = flag;
    }
}
