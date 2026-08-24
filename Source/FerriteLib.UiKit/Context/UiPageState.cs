using UnityEngine;

namespace FerriteLib.UiKit;

/// <summary>Page-level UI state shared by widgets and owned by the hosting mod.</summary>
public sealed class UiPageState
{
    public Vector2 ScrollPosition;

    public string SearchText = "";

    public bool HelpOpen;

    public object? Selected;

    public void Reset()
    {
        ScrollPosition = default;
        SearchText = "";
        HelpOpen = false;
        Selected = null;
    }
}
