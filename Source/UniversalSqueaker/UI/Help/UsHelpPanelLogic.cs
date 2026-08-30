using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure display resolution for the right-hand help panel.
/// Kept free of Verse/Unity so the overview/hover/selection switching can be unit-tested in the
/// zero-Verse UI logic gate.
/// </summary>
internal static class UsHelpPanelLogic
{
    internal const string EmptyTitle = "Help";
    internal const string EmptyText = "Select a section to see its help here.";
    internal const string OverviewLabel = "Overview";

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

    internal static HelpPanelDisplay Resolve(HelpSection? section, string helpHoverKey, string helpSelectionKey)
    {
        if (section == null)
        {
            return new HelpPanelDisplay(EmptyTitle, EmptyTitle, EmptyText, isOverview: true);
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
            OverviewLabel,
            section.Overview,
            isOverview: true);
    }
}
