using System;
using FerriteLib.UiKit;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Lightweight Camera+ style help linkage for US widgets.
/// It updates the frame-local hover help key and draws a gold border on any control whose help key
/// matches the current hover or pinned selection. Drawing is purely visual; interaction remains
/// owned by the existing UiInteract/native widgets.
/// </summary>
public static class UsHelpHighlight
{
    /// <summary>
    /// Sets <see cref="UiPageState.HelpHoverKey"/> when the pointer is over <paramref name="rect"/>
    /// and then draws the gold border when the help key is active.
    /// </summary>
    public static void DrawFor(Rect rect, string helpKey, UiPageState state)
    {
        if (string.IsNullOrEmpty(helpKey) || state == null) return;

        if (Mouse.IsOver(rect))
        {
            state.HelpHoverKey = helpKey;
        }

        Draw(rect, helpKey, state);
    }

    /// <summary>Draws a gold border when the key matches the hover or selection state.</summary>
    public static void Draw(Rect rect, string helpKey, UiPageState state)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
        if (string.IsNullOrEmpty(helpKey) || state == null) return;

        if (string.Equals(helpKey, state.HelpHoverKey, StringComparison.Ordinal)
            || string.Equals(helpKey, state.HelpSelectionKey, StringComparison.Ordinal))
        {
            SurfaceFrame.DrawBorder(rect, Palette.AccentGold);
        }
    }

    /// <summary>
    /// Clears the hover key while the pointer is over the given rect. Useful for the help panel's
    /// “Overview” row so it can deliberately show the section overview instead of a stale item.
    /// </summary>
    public static void ClearHover(Rect rect, UiPageState state)
    {
        if (state == null) return;
        if (Mouse.IsOver(rect))
        {
            state.HelpHoverKey = "";
        }
    }
}
