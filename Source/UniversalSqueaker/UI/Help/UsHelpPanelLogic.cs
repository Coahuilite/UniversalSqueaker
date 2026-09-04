using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure display resolution for the right-hand help panel (C+A: hovered control's entry wins, the
/// active section's overview is the fallback). Kept free of Verse/Unity so the overview/hover/
/// selection switching can be unit-tested in the zero-Verse UI logic gate. Every string it returns
/// - the panel's own three AND every catalog title/label/body - is resolved through the Host
/// translation seam handed to <see cref="Resolve"/>; the catalog stores Keyed entry names only, so
/// measured and drawn text can never come from different resolution passes. The zero-Verse seam
/// default is identity, which makes the logic tests compare key names directly.
/// </summary>
internal static class UsHelpPanelLogic
{
    /// <summary>Panel title/label when no help section is selected.</summary>
    internal const string EmptyTitle = "US.Help.EmptyTitle";

    /// <summary>Panel body when no help section is selected.</summary>
    internal const string EmptyText = "US.Help.EmptyText";

    /// <summary>Index row and content label of the section overview (as opposed to one item).</summary>
    internal const string OverviewLabel = "US.Help.Overview";

    /// <summary>
    /// Hover claim of the panel's own overview index row. A distinct sentinel (item keys all start
    /// with "us/") so hovering "总览" beats a pinned selection and shows the section overview,
    /// while an empty hover key keeps its meaning of "nothing is hovered".
    /// </summary>
    internal const string OverviewHoverKey = "overview";

    internal readonly struct HelpPanelDisplay
    {
        public HelpPanelDisplay(string title, string label, string text, bool isOverview, string? itemKey = null)
        {
            Title = title;
            Label = label;
            Text = text;
            IsOverview = isOverview;
            ItemKey = itemKey;
        }

        public string Title { get; }

        public string Label { get; }

        public string Text { get; }

        public bool IsOverview { get; }

        public string? ItemKey { get; }
    }

    /// <summary>
    /// C+A resolution: a hover claim wins while the pointer holds it and resolves across the WHOLE
    /// catalog (the always-visible surfaces - navigation, footer - claim entries of sections the
    /// content column is not showing). The panel's overview sentinel explicitly claims the section
    /// overview, beating a pinned selection. With no hover, an index selection stays pinned; with
    /// neither, the panel explains the current section. The window clears the claim at the start of
    /// every frame, so "moving away falls back" needs no cleanup in any widget.
    /// </summary>
    internal static HelpPanelDisplay Resolve(
        HelpSection? section,
        string helpHoverKey,
        string helpSelectionKey,
        Func<string, string>? translate = null)
    {
        Func<string, string> keyed = translate ?? Identity;

        if (!string.IsNullOrEmpty(helpHoverKey) && helpHoverKey != OverviewHoverKey
            && UsHelpCatalog.TryFindItem(helpHoverKey, out HelpItem hoveredItem, out HelpSection hoveredSection))
        {
            return new HelpPanelDisplay(
                keyed(hoveredSection.Title),
                keyed(hoveredItem.Label),
                keyed(hoveredItem.Text),
                isOverview: false,
                itemKey: hoveredItem.Key);
        }

        // The panel's own overview row explicitly claims the section overview: it beats a pinned
        // selection for as long as the pointer holds it.
        if (helpHoverKey == OverviewHoverKey)
        {
            return section == null
                ? new HelpPanelDisplay(keyed(EmptyTitle), keyed(EmptyTitle), keyed(EmptyText), isOverview: true)
                : new HelpPanelDisplay(
                    keyed(section.Title),
                    keyed(OverviewLabel),
                    keyed(section.Overview),
                    isOverview: true);
        }

        if (section == null)
        {
            return new HelpPanelDisplay(
                keyed(EmptyTitle),
                keyed(EmptyTitle),
                keyed(EmptyText),
                isOverview: true);
        }

        if (!string.IsNullOrEmpty(helpSelectionKey)
            && UsHelpCatalog.TryGetItem(section.Key, helpSelectionKey, out HelpItem selectedItem))
        {
            return new HelpPanelDisplay(
                keyed(section.Title),
                keyed(selectedItem.Label),
                keyed(selectedItem.Text),
                isOverview: false,
                itemKey: selectedItem.Key);
        }

        return new HelpPanelDisplay(
            keyed(section.Title),
            keyed(OverviewLabel),
            keyed(section.Overview),
            isOverview: true);
    }

    /// <summary>Seam used when the caller only wants the switching logic (the zero-Verse gate).</summary>
    private static string Identity(string key)
    {
        return key;
    }
}
