namespace UniversalSqueaker.UI;

/// <summary>
/// Small US-owned payload carried by neutral <see cref="FerriteLib.UiKit.UiCommand"/> values.
/// The Ferrite page translates this back into the existing business
/// <see cref="UniversalSqueaker.UI.UiCommand"/> before calling
/// <see cref="VoicePacksPageModel.ExecuteAll"/>.
/// </summary>
public sealed class UsCommandPayload
{
    public UiCommandKind Kind { get; set; }

    public SqueakVoicePackMode Mode { get; set; }

    public SqueakVoicePackScope Scope { get; set; }

    public string RaceDefName { get; set; } = "";

    public string TargetDefName { get; set; } = "";

    public string Arg { get; set; } = "";

    public bool Flag { get; set; }
}
