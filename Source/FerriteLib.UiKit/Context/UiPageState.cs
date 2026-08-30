using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit;

/// <summary>Page-level UI state shared by widgets and owned by the hosting mod.</summary>
public sealed class UiPageState
{
    public Vector2 ScrollPosition;

    public Vector2 HelpScrollPosition;

    public string SearchText = "";

    /// <summary>Help key of the control/item currently under the pointer. Recomputed every frame.</summary>
    public string HelpHoverKey = "";

    /// <summary>Help key of the item the user pinned in the help panel. Persists across frames.</summary>
    public string HelpSelectionKey = "";

    public bool HelpOpen;

    public object? Selected;

    public readonly HashSet<string> OpenHelpKeys = new(StringComparer.Ordinal);

    public void ToggleHelpKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!OpenHelpKeys.Add(key))
        {
            OpenHelpKeys.Remove(key);
        }
    }

    public void Reset()
    {
        ScrollPosition = default;
        HelpScrollPosition = default;
        SearchText = "";
        HelpHoverKey = "";
        HelpSelectionKey = "";
        HelpOpen = false;
        Selected = null;
        OpenHelpKeys.Clear();
    }
}
