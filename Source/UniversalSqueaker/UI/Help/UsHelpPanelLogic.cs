using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure display resolution for the right-hand help panel.
/// Kept free of Verse/Unity so the overview/hover/selection switching can be unit-tested in the
/// zero-Verse UI logic gate. Its own three UI strings are therefore Keyed entry <em>names</em>, never
/// text: the drawing site (<c>UsHelpPanelWidget</c>) hands <see cref="Resolve"/> the Host translation
/// seam, and only these three names go through it. Catalog titles/labels/body are deliberately not
/// passed to the seam while they are still plain English — an unresolved lookup comes back verbatim,
/// which RimWorld then pseudo-translates into accented garbage whenever DevMode is on. Keying the
/// catalog is the next pass; when it lands, those entries route through the same seam.
/// </summary>
internal static class UsHelpPanelLogic
{
    /// <summary>Panel title/label when no help section is selected.</summary>
    internal const string EmptyTitle = "US.Help.EmptyTitle";

    /// <summary>Panel body when no help section is selected.</summary>
    internal const string EmptyText = "US.Help.EmptyText";

    /// <summary>Index row and content label of the section overview (as opposed to one item).</summary>
    internal const string OverviewLabel = "US.Help.Overview";

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

    internal static HelpPanelDisplay Resolve(
        HelpSection? section,
        string helpHoverKey,
        string helpSelectionKey,
        Func<string, string>? translate = null)
    {
        Func<string, string> keyed = translate ?? Identity;

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
                section.Title,
                selectedItem.Label,
                selectedItem.Text,
                isOverview: false,
                itemKey: selectedItem.Key);
        }

        if (!string.IsNullOrEmpty(helpHoverKey)
            && UsHelpCatalog.TryGetItem(section.Key, helpHoverKey, out HelpItem hoveredItem))
        {
            return new HelpPanelDisplay(
                section.Title,
                hoveredItem.Label,
                hoveredItem.Text,
                isOverview: false,
                itemKey: hoveredItem.Key);
        }

        return new HelpPanelDisplay(
            section.Title,
            keyed(OverviewLabel),
            section.Overview,
            isOverview: true);
    }

    /// <summary>Seam used when the caller only wants the switching logic (the zero-Verse gate).</summary>
    private static string Identity(string key)
    {
        return key;
    }
}
