using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Zero-Verse filter-bar layout rules shared by <see cref="UsFilterBarWidget"/> Measure and Draw.
/// At narrow card-body widths the three filter dropdowns stack full-width (each keeps a usable
/// label + field) instead of sitting three-up with a tiny clickable field. Keeping the rule here,
/// outside the widget, lets the UI logic tests assert Measure/Draw parity without Verse.
/// </summary>
public static class UsFilterBarLayout
{
    public const float RowHeight = 24f;
    public const float Gap = 4f;

    /// <summary>Label reservation per dropdown, mirrored from DropdownWidget.LabelWidth.</summary>
    public const float DropdownLabelWidth = 80f;

    /// <summary>Minimum usable dropdown field width; below that the row stacks.</summary>
    public const float MinDropdownFieldWidth = 96f;

    /// <summary>
    /// True when three dropdowns side by side would leave each with less than
    /// <see cref="DropdownLabelWidth"/> + <see cref="MinDropdownFieldWidth"/> of width; the row
    /// then stacks full-width rows instead. Measure and Draw share this rule via the card body
    /// width.
    /// </summary>
    public static bool DropdownsStack(float bodyWidth)
    {
        float dropdownWidth = (bodyWidth - Gap * 2f) / 3f;
        return dropdownWidth < DropdownLabelWidth + MinDropdownFieldWidth;
    }

    /// <summary>Extra height when the dropdown row stacks: two more rows plus the gaps between them.</summary>
    public static float ExtraDropdownRows(float bodyWidth)
    {
        return DropdownsStack(bodyWidth) ? RowHeight * 2f + Gap * 2f : 0f;
    }

    /// <summary>Total body height (card chrome excluded) for the filter bar.</summary>
    public static float BodyHeight(float bodyWidth)
    {
        return RowHeight * 2f + ExtraDropdownRows(bodyWidth);
    }
}
