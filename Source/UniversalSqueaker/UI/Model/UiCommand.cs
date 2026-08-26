namespace UniversalSqueaker.UI;

/// <summary>
/// Commands emitted by UI components. Components never mutate settings directly;
/// <see cref="VoicePacksPageModel"/> is the single funnel that executes them.
/// UI-only navigation commands (SelectDomain) are also funneled here so the page has one command path.
/// </summary>
public enum UiCommandKind
{
    SetMode,
    SelectDomain,
    TogglePack,
    ForgetUnavailable,
    ToggleEgg,
    SetDistancePreset,
    ToggleBasic
}

/// <summary>
/// Read-only command value. Business commands carry the exact (scope, raceDefName, targetDefName)
/// domain identity so multi-race catalogs stay neutral and race-aware.
/// </summary>
public readonly struct UiCommand
{
    public readonly UiCommandKind Kind;
    public readonly SqueakVoicePackMode Mode;
    public readonly SqueakVoicePackScope Scope;
    public readonly string RaceDefName;
    public readonly string TargetDefName;
    public readonly string Arg;
    public readonly bool Flag;

    public UiCommand(
        UiCommandKind kind,
        SqueakVoicePackMode mode = default,
        SqueakVoicePackScope scope = default,
        string? raceDefName = null,
        string? targetDefName = null,
        string? arg = null,
        bool flag = false)
    {
        Kind = kind;
        Mode = mode;
        Scope = scope;
        RaceDefName = raceDefName ?? "";
        TargetDefName = targetDefName ?? "";
        Arg = arg ?? "";
        Flag = flag;
    }
}
