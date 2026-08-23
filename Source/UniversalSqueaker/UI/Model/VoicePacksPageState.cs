using UnityEngine;

namespace UniversalSqueaker.UI;

/// <summary>
/// Page-local UI ephemeral state. It is not persisted and is reset on settings session begin/end.
/// The view model is rebuilt from settings/catalog each frame; this state only remembers what the
/// user is looking at (selected domain, search text, help, scroll).
/// </summary>
public sealed class VoicePacksPageState
{
    public SqueakVoicePackScope SelectedScope = SqueakVoicePackScope.Race;
    public string SelectedTargetName = "";
    public string SearchText = "";
    public bool HelpOpen;
    public Vector2 ScrollPosition;

    public VoicePacksPageState()
    {
    }

    public void Reset()
    {
        SelectedScope = SqueakVoicePackScope.Race;
        SelectedTargetName = "";
        SearchText = "";
        HelpOpen = false;
        ScrollPosition = Vector2.zero;
    }
}
