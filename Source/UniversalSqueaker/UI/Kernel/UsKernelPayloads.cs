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
    public readonly BehaviorScope? Scope;

    public UsScopeWrite(string actionKey, BehaviorScope? scope)
    {
        ActionKey = actionKey ?? "";
        Scope = scope;
    }
}

/// <summary>VF1: fallback editor selection. Null halves mean "leave unchanged" - one command
/// covers the race dropdown and the entry-row click without a second payload type.</summary>
public readonly struct UsFallbackSelection
{
    public readonly string? Race;
    public readonly string? EntryAction;

    public UsFallbackSelection(string? race, string? entryAction)
    {
        Race = race;
        EntryAction = entryAction;
    }
}

/// <summary>VF1 (r5 semantics): one entry write in the selected race's final table. SoundDefName
/// non-empty = override; empty string = the EXPLICIT no-sound marker (key stays present, resolves
/// to silence); null = restore INHERITANCE (key removed, future shipped updates apply again).
/// Explicit silence and inheritance are different states with different controls; the closed
/// 17-key set is enforced by the store.</summary>
public readonly struct UsFallbackEntryWrite
{
    public readonly string ActionKey;
    public readonly string? SoundDefName;

    public UsFallbackEntryWrite(string actionKey, string? soundDefName)
    {
        ActionKey = actionKey ?? "";
        SoundDefName = soundDefName;
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
