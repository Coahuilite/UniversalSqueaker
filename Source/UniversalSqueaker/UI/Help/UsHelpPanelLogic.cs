using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure display resolution for the right-hand help panel (C+A model; D2 ruling retired the
/// persistent index list and pinned selection with it). A hover claim wins while the pointer holds
/// it and resolves across the WHOLE catalog (the always-visible surfaces - navigation, footer -
/// claim entries of sections the content column is not showing, and the header follows the claimed
/// entry's section). With no hover, the panel explains the active section's overview - the
/// big-level fallback. The window clears the claim at the start of every frame, so "moving away
/// falls back" needs no cleanup in any widget. Kept free of Verse/Unity so the switching can be
/// unit-tested in the zero-Verse UI logic gate. Every string returned - the panel's own three AND
/// every catalog title/label/body - resolves through the Host translation seam handed to
/// <see cref="Resolve"/>; the catalog stores Keyed entry names only, so measured and drawn text can
/// never come from different resolution passes. The zero-Verse seam default is identity, which
/// makes the logic tests compare key names directly.
/// </summary>
internal static class UsHelpPanelLogic
{
    /// <summary>Panel title/label when no help section is active.</summary>
    internal const string EmptyTitle = "US.Help.EmptyTitle";

    /// <summary>Panel body when no help section is active.</summary>
    internal const string EmptyText = "US.Help.EmptyText";

    /// <summary>Content label of the section overview (as opposed to a hovered item).</summary>
    internal const string OverviewLabel = "US.Help.Overview";

    internal readonly struct HelpPanelDisplay
    {
        public HelpPanelDisplay(string title, string label, string text)
        {
            Title = title;
            Label = label;
            Text = text;
        }

        public string Title { get; }

        public string Label { get; }

        public string Text { get; }
    }

    internal static HelpPanelDisplay Resolve(
        HelpSection? section,
        string helpHoverKey,
        Func<string, string>? translate = null)
    {
        Func<string, string> keyed = translate ?? Identity;

        if (!string.IsNullOrEmpty(helpHoverKey)
            && UsHelpCatalog.TryFindItem(helpHoverKey, out HelpItem hoveredItem, out HelpSection hoveredSection))
        {
            return new HelpPanelDisplay(
                keyed(hoveredSection.Title),
                keyed(hoveredItem.Label),
                keyed(hoveredItem.Text));
        }

        if (section == null)
        {
            return new HelpPanelDisplay(keyed(EmptyTitle), keyed(EmptyTitle), keyed(EmptyText));
        }

        return new HelpPanelDisplay(
            keyed(section.Title),
            keyed(OverviewLabel),
            keyed(section.Overview));
    }

    /// <summary>Seam used when the caller only wants the switching logic (the zero-Verse gate).</summary>
    private static string Identity(string key)
    {
        return key;
    }
}
