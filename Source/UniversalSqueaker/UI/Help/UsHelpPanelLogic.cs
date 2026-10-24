using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure display resolution for the bottom help panel (C+A model; D2 ruling retired the
/// persistent index list and pinned selection with it). The current help reference resolves across
/// the WHOLE catalog, including navigation/footer topics outside the active content section.
/// The fixed carrier's session retains it for 15 completed passes without a fresh publication;
/// only grace expiry returns the section overview. This resolver consumes that effective reference,
/// not raw pointer state, and does not clear it. Kept free of Verse/Unity so switching can be
/// unit-tested in the zero-Verse UI logic gate. Every string returned - the panel's own three AND
/// every catalog title/label/body - resolves through the Host translation seam handed to
/// <see cref="Resolve"/>; the catalog stores Keyed entry names only, so measured and drawn text can
/// never come from different resolution passes. The zero-Verse seam default is identity, which
/// makes the logic tests compare key names directly.
/// </summary>
internal static class UsHelpPanelLogic
{
    /// <summary>Panel title/label when no help section is active.</summary>
    internal const string EmptyTitleKey = "US.Help.EmptyTitle";

    /// <summary>Panel body when no help section is active.</summary>
    internal const string EmptyTextKey = "US.Help.EmptyText";

    /// <summary>Content label of the section overview (as opposed to a hovered item).</summary>
    internal const string OverviewLabelKey = "US.Help.Overview";

    internal readonly struct HelpPanelDisplay
    {
        public HelpPanelDisplay(string title, string label, string text)
        {
            TitleText = title;
            LabelText = label;
            BodyText = text;
        }

        public string TitleText { get; }

        public string LabelText { get; }

        public string BodyText { get; }
    }

    internal static HelpPanelDisplay Resolve(
        HelpSection? section,
        string currentHelpRef,
        Func<string, string>? translate = null)
    {
        Func<string, string> keyed = translate ?? Identity;

        if (!string.IsNullOrEmpty(currentHelpRef)
            && UsHelpCatalog.TryFindItem(currentHelpRef, out HelpItem hoveredItem, out HelpSection hoveredSection))
        {
            return new HelpPanelDisplay(
                keyed(hoveredSection.TitleKey),
                keyed(hoveredItem.LabelKey),
                keyed(hoveredItem.TextKey));
        }

        // Explicit page help may address a section overview; unknown refs still use the origin.
        if (UsHelpCatalog.TryGetSection(currentHelpRef, out HelpSection explicitSection)) section = explicitSection;

        if (section == null)
        {
            return new HelpPanelDisplay(keyed(EmptyTitleKey), keyed(EmptyTitleKey), keyed(EmptyTextKey));
        }

        return new HelpPanelDisplay(
            keyed(section.TitleKey),
            keyed(OverviewLabelKey),
            keyed(section.OverviewKey));
    }

    /// <summary>Seam used when the caller only wants the switching logic (the zero-Verse gate).</summary>
    private static string Identity(string key)
    {
        return key;
    }
}
