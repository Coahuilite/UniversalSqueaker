using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// Structured help section returned by <see cref="UsHelpCatalog"/>. <see cref="Title"/> and
/// <see cref="Overview"/> are Keyed entry NAMES, never display text; they are resolved through the
/// Host translation seam in <see cref="UsHelpPanelLogic.Resolve"/> and in the panel's band maxima.
/// </summary>
internal sealed class HelpSection
{
    public HelpSection(string key, string titleKey, string overviewKey, IReadOnlyList<HelpItem> items)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Title = titleKey ?? throw new ArgumentNullException(nameof(titleKey));
        Overview = overviewKey ?? throw new ArgumentNullException(nameof(overviewKey));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public string Key { get; }

    /// <summary>Keyed entry name of the section title.</summary>
    public string Title { get; }

    /// <summary>Keyed entry name of the section overview body.</summary>
    public string Overview { get; }

    public IReadOnlyList<HelpItem> Items { get; }
}

/// <summary>
/// One individual help entry inside a <see cref="HelpSection"/>. Label and Text are Keyed entry
/// NAMES; some labels deliberately reuse the control's own caption key (a mode button and its help
/// row must never read differently in either language).
/// </summary>
internal sealed class HelpItem
{
    public HelpItem(string key, string labelKey, string textKey)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Label = labelKey ?? throw new ArgumentNullException(nameof(labelKey));
        Text = textKey ?? throw new ArgumentNullException(nameof(textKey));
    }

    public string Key { get; }

    /// <summary>Keyed entry name of the index row / content label.</summary>
    public string Label { get; }

    /// <summary>Keyed entry name of the help body.</summary>
    public string Text { get; }
}

/// <summary>
/// US-side structured help catalog backing the right-hand panel (C+A model: the panel shows the
/// hovered control's entry, falling back to the active section's overview). Every string here is a
/// Keyed entry name - the ChineseSimplified table is the authoritative copy (说明书体, terminology
/// rulings applied: 异种/强效/已失效/清除失效) and the English table translates it. The audit
/// gate (gate 15) drives both tables through the real panel, so every body must fit the 232px
/// help column in both languages.
/// Item keys are globally unique and embed their section prefix ("us/&lt;section&gt;/&lt;item&gt;");
/// hover claims in the widget tree resolve through <see cref="TryFindItem"/> across the whole
/// catalog, and the zero-Verse gate pins both the uniqueness and the claim↔entry equality.
/// </summary>
internal static class UsHelpCatalog
{
    private static readonly Dictionary<string, HelpSection> Sections = new(StringComparer.Ordinal)
    {
        // Window-level fallback section: the section-map guard keeps it reachable via
        // SectionHelpKeyOf's default branch; its two items are claimed live by the navigation
        // rows and the footer save status.
        ["us/page-title"] = new HelpSection(
            "us/page-title",
            "US.Help.PageTitle.Title",
            "US.Help.PageTitle.Overview",
            new[]
            {
                new HelpItem(
                    "us/page-title/nav",
                    "US.Help.PageTitle.Nav.Label",
                    "US.Help.PageTitle.Nav.Text"),
                new HelpItem(
                    "us/page-title/apply",
                    "US.Help.PageTitle.Apply.Label",
                    "US.Help.PageTitle.Apply.Text"),
            }),
        ["us/mode-row"] = new HelpSection(
            "us/mode-row",
            "US.Help.ModeRow.Title",
            "US.Help.ModeRow.Overview",
            new[]
            {
                // Mode labels reuse the button captions themselves: one word, two surfaces, can
                // never drift apart per language.
                new HelpItem(
                    "us/mode-row/vanilla",
                    "US.Tuning.Mode.Vanilla",
                    "US.Help.ModeRow.Vanilla.Text"),
                new HelpItem(
                    "us/mode-row/fallback",
                    "US.Tuning.Mode.Fallback",
                    "US.Help.ModeRow.Fallback.Text"),
                new HelpItem(
                    "us/mode-row/remix",
                    "US.Tuning.Mode.Remix",
                    "US.Help.ModeRow.Remix.Text"),
                new HelpItem(
                    "us/mode-row/disabled",
                    "US.Tuning.Mode.Disabled",
                    "US.Help.ModeRow.Disabled.Text"),
            }),
        ["us/global-volume"] = new HelpSection(
            "us/global-volume",
            "US.Section.OutputLevel",
            "US.Help.GlobalVolume.Overview",
            new[]
            {
                new HelpItem(
                    "us/global-volume/slider",
                    "US.Help.GlobalVolume.Slider.Label",
                    "US.Help.GlobalVolume.Slider.Text"),
                new HelpItem(
                    "us/global-volume/number",
                    "US.Help.GlobalVolume.Number.Label",
                    "US.Help.GlobalVolume.Number.Text"),
            }),
        ["us/attenuation-editor"] = new HelpSection(
            "us/attenuation-editor",
            "US.Section.DistanceAttenuation",
            "US.Help.Attenuation.Overview",
            new[]
            {
                new HelpItem(
                    "us/attenuation-editor/chart",
                    "US.Help.Attenuation.Chart.Label",
                    "US.Help.Attenuation.Chart.Text"),
                new HelpItem(
                    "us/attenuation-editor/presets",
                    "US.Help.Attenuation.Presets.Label",
                    "US.Help.Attenuation.Presets.Text"),
            }),
        // The dead basic-tuning/distance item (its control moved to the Distance workspace) stays
        // removed; the claim↔entry equality guard makes re-adding it without wiring fail the gate.
        ["us/basic-tuning"] = new HelpSection(
            "us/basic-tuning",
            "US.Section.PlaybackBehaviour",
            "US.Help.BasicTuning.Overview",
            new[]
            {
                new HelpItem(
                    "us/basic-tuning/egg",
                    "US.Help.BasicTuning.Egg.Label",
                    "US.Help.BasicTuning.Egg.Text"),
                new HelpItem(
                    "us/basic-tuning/scaling",
                    "US.Help.BasicTuning.Scaling.Label",
                    "US.Help.BasicTuning.Scaling.Text"),
            }),
        ["us/timing"] = new HelpSection(
            "us/timing",
            "US.Section.TriggerTiming",
            "US.Help.Timing.Overview",
            new[]
            {
                new HelpItem(
                    "us/timing/interval",
                    "US.Help.Timing.Interval.Label",
                    "US.Help.Timing.Interval.Text"),
                new HelpItem(
                    "us/timing/multiplier",
                    "US.Help.Timing.Multiplier.Label",
                    "US.Help.Timing.Multiplier.Text"),
            }),
        ["us/camera-indicator"] = new HelpSection(
            "us/camera-indicator",
            "US.Section.InWorldIndicator",
            "US.Help.CameraIndicator.Overview",
            new[]
            {
                new HelpItem(
                    "us/camera-indicator/toggle",
                    "US.Tuning.CameraIndicator",
                    "US.Help.CameraIndicator.Toggle.Text"),
            }),
        ["us/diagnostics"] = new HelpSection(
            "us/diagnostics",
            "US.Section.Diagnostics",
            "US.Help.Diagnostics.Overview",
            new[]
            {
                new HelpItem(
                    "us/diagnostics/logging-auto",
                    "US.Diagnostics.Logging.Auto",
                    "US.Help.Diagnostics.LoggingAuto.Text"),
                new HelpItem(
                    "us/diagnostics/logging-enabled",
                    "US.Diagnostics.Logging.Enabled",
                    "US.Help.Diagnostics.LoggingEnabled.Text"),
                new HelpItem(
                    "us/diagnostics/logging-disabled",
                    "US.Diagnostics.Logging.Disabled",
                    "US.Help.Diagnostics.LoggingDisabled.Text"),
                new HelpItem(
                    "us/diagnostics/localize-debug",
                    "US.Diagnostics.LocalizeDebugMenu",
                    "US.Help.Diagnostics.LocalizeDebug.Text"),
            }),
        ["us/scope-tree"] = new HelpSection(
            "us/scope-tree",
            "US.Section.LayeredTuning",
            "US.Help.ScopeTree.Overview",
            new[]
            {
                new HelpItem(
                    "us/scope-tree/layer",
                    "US.Tuning.Layer",
                    "US.Help.ScopeTree.Layer.Text"),
                new HelpItem(
                    "us/scope-tree/domain",
                    "US.Tuning.Domain",
                    "US.Help.ScopeTree.Domain.Text"),
                new HelpItem(
                    "us/scope-tree/action-scope",
                    "US.Tuning.ActionScope",
                    "US.Help.ScopeTree.ActionScope.Text"),
                new HelpItem(
                    "us/scope-tree/mood-tuning",
                    "US.Tuning.MoodTuning",
                    "US.Help.ScopeTree.MoodTuning.Text"),
                new HelpItem(
                    "us/scope-tree/auto",
                    "US.Help.ScopeTree.Auto.Label",
                    "US.Help.ScopeTree.Auto.Text"),
            }),
        ["us/preset-list"] = new HelpSection(
            "us/preset-list",
            "US.Section.DefBaselines",
            "US.Help.PresetList.Overview",
            new[]
            {
                new HelpItem(
                    "us/preset-list/tree",
                    "US.Help.PresetList.Tree.Label",
                    "US.Help.PresetList.Tree.Text"),
                new HelpItem(
                    "us/preset-list/import",
                    "US.Preset.List.Import",
                    "US.Help.PresetList.Import.Text"),
            }),
        ["us/filter-bar"] = new HelpSection(
            "us/filter-bar",
            "US.Section.FilterDomains",
            "US.Help.FilterBar.Overview",
            new[]
            {
                new HelpItem(
                    "us/filter-bar/domain",
                    "US.Help.FilterBar.Domain.Label",
                    "US.Help.FilterBar.Domain.Text"),
                new HelpItem(
                    "us/filter-bar/race-xeno",
                    "US.Help.FilterBar.RaceXeno.Label",
                    "US.Help.FilterBar.RaceXeno.Text"),
                new HelpItem(
                    "us/filter-bar/author",
                    "US.Packs.Filter.Author",
                    "US.Help.FilterBar.Author.Text"),
            }),
        ["us/race-layer"] = new HelpSection(
            "us/race-layer",
            "US.Section.RaceDomain",
            "US.Help.RaceLayer.Overview",
            new[]
            {
                new HelpItem(
                    "us/race-layer/row",
                    "US.Help.RaceLayer.Row.Label",
                    "US.Help.RaceLayer.Row.Text"),
            }),
        ["us/xenotype-layer"] = new HelpSection(
            "us/xenotype-layer",
            "US.Section.XenotypeDomain",
            "US.Help.XenotypeLayer.Overview",
            new[]
            {
                new HelpItem(
                    "us/xenotype-layer/row",
                    "US.Help.XenotypeLayer.Row.Label",
                    "US.Help.XenotypeLayer.Row.Text"),
            }),
        ["us/voice-pack-checklist"] = new HelpSection(
            "us/voice-pack-checklist",
            "US.Section.ChooseVoicePacks",
            "US.Help.Checklist.Overview",
            new[]
            {
                new HelpItem(
                    "us/voice-pack-checklist/search",
                    "US.Help.Checklist.Search.Label",
                    "US.Help.Checklist.Search.Text"),
                new HelpItem(
                    "us/voice-pack-checklist/row",
                    "US.Help.Checklist.Row.Label",
                    "US.Help.Checklist.Row.Text"),
                new HelpItem(
                    "us/voice-pack-checklist/forget",
                    "US.Packs.Checklist.ForgetUnavailable",
                    "US.Help.Checklist.Forget.Text"),
            }),
    };

    internal static bool TryGetSection(string key, out HelpSection section)
    {
        if (string.IsNullOrEmpty(key))
        {
            section = null!;
            return false;
        }

        return Sections.TryGetValue(key, out section!);
    }

    internal static bool TryGetItem(string sectionKey, string itemKey, out HelpItem item)
    {
        item = null!;
        if (string.IsNullOrEmpty(sectionKey)
            || string.IsNullOrEmpty(itemKey)
            || !TryGetSection(sectionKey, out HelpSection section))
        {
            return false;
        }

        foreach (HelpItem candidate in section.Items)
        {
            if (string.Equals(candidate.Key, itemKey, StringComparison.Ordinal))
            {
                item = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Global item lookup by item key. Hover claims are made by whichever control is under the
    /// pointer, and the always-visible surfaces (navigation, footer) claim entries of sections the
    /// content column is not currently showing, so hover resolves across the whole catalog. Item
    /// keys embed their section prefix and are globally unique; the uniqueness is asserted by the
    /// zero-Verse gate.
    /// </summary>
    internal static bool TryFindItem(string itemKey, out HelpItem item, out HelpSection section)
    {
        item = null!;
        section = null!;
        if (string.IsNullOrEmpty(itemKey)) return false;
        foreach (HelpSection candidate in Sections.Values)
        {
            if (TryGetItem(candidate.Key, itemKey, out HelpItem found))
            {
                item = found;
                section = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>All section title keys; the hover-invariant header band measures against these.</summary>
    internal static IEnumerable<string> AllSectionTitles()
    {
        foreach (HelpSection section in Sections.Values) yield return section.Title;
    }

    /// <summary>All item label keys; the hover-invariant label band measures against these.</summary>
    internal static IEnumerable<string> AllItemLabels()
    {
        foreach (HelpSection section in Sections.Values)
        {
            foreach (HelpItem item in section.Items) yield return item.Label;
        }
    }

    /// <summary>All item body keys; the hover-invariant body band measures against these.</summary>
    internal static IEnumerable<string> AllItemTexts()
    {
        foreach (HelpSection section in Sections.Values)
        {
            foreach (HelpItem item in section.Items) yield return item.Text;
        }
    }

    /// <summary>All section overview keys; the hover-invariant body band measures against these too.</summary>
    internal static IEnumerable<string> AllSectionOverviews()
    {
        foreach (HelpSection section in Sections.Values) yield return section.Overview;
    }
}
