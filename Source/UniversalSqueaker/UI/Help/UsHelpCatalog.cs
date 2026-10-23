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
/// US-side structured help catalog backing the bottom help panel (C+A model: the panel shows the
/// hovered control's entry, falling back to the active section's overview). Every string here is a
/// Keyed entry name - the ChineseSimplified table is the authoritative copy (说明书体, terminology
/// rulings applied: 异种/强效/已失效/清除失效) and the English table translates it. The audit
/// gate drives both tables through the real panel, so every body must fit the panel's own wrapped band
/// at the shipped page box in both languages (BH1: the panel spans the page's inner width above the
/// footer instead of the old 320px side column; HelpPanelLaneTests measures the band it hands the
/// widget and keeps the audit attached while it does).
/// Item keys are globally unique and embed their section prefix ("us/&lt;section&gt;/&lt;item&gt;");
/// hover claims in the widget tree resolve through <see cref="TryFindItem"/> across the whole
/// catalog, and the zero-Verse gate pins both the uniqueness and the claim↔entry equality.
/// </summary>
internal static class UsHelpCatalog
{
    private static readonly Dictionary<string, HelpSection> Sections = new(StringComparer.Ordinal)
    {
        // Window-level fallback section: the section-map guard keeps it reachable via
        // SectionHelpKeyOf's default branch; its three items are claimed live by the navigation
        // rows, the footer save status and the footer Help switch.
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
                // The footer Help switch (BH1): the single control that shows/hides the bottom help panel.
                // W1 wires the matching HelpHover claim through the declared element's HelpKey; without that
                // literal this entry is unclaimed and the claim<->catalog gate flags it.
                new HelpItem(
                    "us/page-title/help-drawer",
                    "US.Help.PageTitle.HelpDrawer.Label",
                    "US.Help.PageTitle.HelpDrawer.Text"),
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
                // The status line under the chart is a read-out (active preset + current range), not a
                // writable control, but it is still a surface a player hovers: it owns this entry in both
                // the wide status line and the narrow summary shape.
                new HelpItem(
                    "us/attenuation-editor/status",
                    "US.Help.Attenuation.Status.Label",
                    "US.Help.Attenuation.Status.Text"),
            }),
        // The dead basic-tuning/distance item (its control moved to the Distance workspace) stays
        // removed; the claim↔entry equality guard makes re-adding it without wiring fail the gate.
        // Each playback-behaviour row owns its own entry: the three runtime scalings were one shared
        // "us/basic-tuning/scaling" claim, which made three different toggles explain themselves with one
        // sentence (and left the hovered row unable to say what it alone does). Each new item carries its
        // own label + body keys in both languages rather than reusing the row's full-sentence caption, so
        // the help index name stays short and the two surfaces can still be worded for their own job.
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
                    "us/basic-tuning/scale-talking",
                    "US.Help.BasicTuning.ScaleTalking.Label",
                    "US.Help.BasicTuning.ScaleTalking.Text"),
                // Independent phase and item filters; none depends on another control's visibility.
                new HelpItem(
                    "us/basic-tuning/eat-phase",
                    "US.Help.BasicTuning.EatPhase.Label",
                    "US.Help.BasicTuning.EatPhase.Text"),
                new HelpItem(
                    "us/basic-tuning/eat-nutrition",
                    "US.Help.BasicTuning.EatNutrition.Label",
                    "US.Help.BasicTuning.EatNutrition.Text"),
                new HelpItem("us/basic-tuning/eat-drug", "US.Help.BasicTuning.EatDrug.Label", "US.Help.BasicTuning.EatDrug.Text"),
                new HelpItem("us/basic-tuning/select", "US.Help.Select.Label", "US.Help.Select.Text"),
            }),
        ["us/timing"] = new HelpSection("us/timing", "US.Section.TriggerTiming", "US.Help.Timing.Overview", new[]
        {
            new HelpItem("us/timing/policy", "US.Section.TriggerTiming", "US.Help.Timing.Overview"),
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
                // R3-B: the developer layout diagnosis controls. One entry per control, because the help
                // gate is bidirectional - a manifest HelpKey with no catalog item is dead hover, and a
                // catalog item no control claims is dead content.
                new HelpItem(
                    "us/diagnostics/layout-diagnosis",
                    "US.Diagnostics.Geometry.Label",
                    "US.Help.Diagnostics.LayoutDiagnosis.Text"),
                new HelpItem(
                    "us/diagnostics/layout-capture",
                    "US.Diagnostics.Geometry.Capture",
                    "US.Help.Diagnostics.LayoutCapture.Text"),
                new HelpItem(
                    "us/diagnostics/layout-outline",
                    "US.Diagnostics.Geometry.Outline",
                    "US.Help.Diagnostics.LayoutOutline.Text"),
                new HelpItem(
                    "us/diagnostics/layout-report",
                    "US.Diagnostics.Geometry.Report",
                    "US.Help.Diagnostics.LayoutReport.Text"),
            }),
        // US-RESET1: the restore-defaults help. One section for the global entry (the Overview
        // restore card) and one for the per-area entries; the fallback tables' own restore entries
        // stay in us/fallback - these never touch them and say so.
        ["us/reset-center"] = new HelpSection(
            "us/reset-center",
            "US.Section.RestoreDefaults",
            "US.Help.ResetCenter.Overview",
            new[]
            {
                new HelpItem(
                    "us/reset-center/restore-all",
                    "US.Reset.RestoreAll",
                    "US.Help.ResetCenter.RestoreAll.Text"),
            }),
        ["us/reset-area"] = new HelpSection(
            "us/reset-area",
            "US.Reset.Area",
            "US.Help.ResetArea.Overview",
            new[]
            {
                new HelpItem(
                    "us/reset-area/button",
                    "US.Reset.Area",
                    "US.Help.ResetArea.Button.Text"),
            }),
        ["us/scope-tree"] = new HelpSection("us/scope-tree", "US.Section.LayeredTuning", "US.Help.ScopeTree.Overview", new[]
        {
            new HelpItem("us/scope-tree/layer", "US.Tuning.Layer", "US.Help.ScopeTree.Layer.Text"),
            new HelpItem("us/scope-tree/domain", "US.Tuning.Domain", "US.Help.ScopeTree.Domain.Text"),
            new HelpItem("us/scope-tree/action-scope", "US.Tuning.ActionScope", "US.Help.ScopeTree.ActionScope.Text"),
            new HelpItem("us/scope-tree/auto", "US.Help.ScopeTree.Auto.Label", "US.Help.ScopeTree.Auto.Text"),
            new HelpItem("us/scope-tree/response-details", "US.Tuning.Area.Mood", "US.Help.ScopeTree.ResponseDetails.Text"),
            new HelpItem("us/scope-tree/reset-default", "US.Reset.Row", "US.Help.ScopeTree.ResetDefault.Text"),
            new HelpItem("us/scope-tree/reset-preset", "US.Tuning.ResetToPreset", "US.Help.ScopeTree.ResetPreset.Text"),
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
                    "us/filter-bar/keyword",
                    "US.Help.FilterBar.Keyword.Label",
                    "US.Help.FilterBar.Keyword.Text"),
                new HelpItem(
                    "us/filter-bar/domain",
                    "US.Help.FilterBar.Domain.Label",
                    "US.Help.FilterBar.Domain.Text"),
                new HelpItem(
                    "us/filter-bar/author",
                    "US.Packs.Filter.Author",
                    "US.Help.FilterBar.Author.Text"),
                new HelpItem(
                    "us/filter-bar/race",
                    "US.Packs.Filter.Race",
                    "US.Help.FilterBar.Race.Text"),
                new HelpItem(
                    "us/filter-bar/xenotype",
                    "US.Packs.Filter.Xenotype",
                    "US.Help.FilterBar.Xenotype.Text"),
                new HelpItem(
                    "us/filter-bar/summary",
                    "US.Help.FilterBar.Summary.Label",
                    "US.Help.FilterBar.Summary.Text"),
            }),
        ["us/pack-cards"] = new HelpSection(
            "us/pack-cards",
            "US.Section.PackResults",
            "US.Help.PackCards.Overview",
            new[]
            {
                new HelpItem(
                    "us/pack-cards/card",
                    "US.Help.PackCards.Card.Label",
                    "US.Help.PackCards.Card.Text"),
                new HelpItem(
                    "us/pack-cards/expand",
                    "US.Packs.Card.Contents",
                    "US.Help.PackCards.Expand.Text"),
                new HelpItem(
                    "us/pack-cards/row",
                    "US.Help.PackCards.Row.Label",
                    "US.Help.PackCards.Row.Text"),
                new HelpItem(
                    "us/pack-cards/enable",
                    "US.Help.PackCards.Enable.Label",
                    "US.Help.PackCards.Enable.Text"),
            }),
        ["us/voice-pack-checklist"] = new HelpSection(
            "us/voice-pack-checklist",
            "US.Section.ChooseVoicePacks",
            "US.Help.Checklist.Overview",
            new[]
            {
                new HelpItem(
                    "us/voice-pack-checklist/forget",
                    "US.Packs.Checklist.ForgetUnavailable",
                    "US.Help.Checklist.Forget.Text"),
            }),
        // UIR-001/002/003/006 (WO-US-UI-REFACTOR-20261014): the four workspaces' new sections. Copy
        // lives in the Keyed tables (zh authoritative, en translated); the fit audit drives both.
        ["us/overview-intro"] = new HelpSection(
            "us/overview-intro",
            "US.Section.Intro",
            "US.Help.Intro.Overview",
            new[]
            {
                new HelpItem(
                    "us/overview-intro/collapse",
                    "US.Intro.Collapse",
                    "US.Help.Intro.Collapse.Text"),
            }),
        ["us/race-board"] = new HelpSection(
            "us/race-board",
            "US.Section.RaceBoard",
            "US.Help.RaceBoard.Overview",
            new[]
            {
                new HelpItem(
                    "us/race-board/row",
                    "US.Help.RaceBoard.Row.Label",
                    "US.Help.RaceBoard.Row.Text"),
                new HelpItem(
                    "us/race-board/enable",
                    "US.Board.Enable",
                    "US.Help.RaceBoard.Enable.Text"),
                new HelpItem(
                    "us/race-board/summary",
                    "US.Help.RaceBoard.Summary.Label",
                    "US.Help.RaceBoard.Summary.Text"),
                // OVT-002: the browse band items (claims ride the manifest HelpKey attributes and
                // the xenotype rows' hover; the bidirectional gate keeps both sides honest).
                new HelpItem(
                    "us/race-board/group",
                    "US.Board.Group.Player",
                    "US.Help.RaceBoard.Group.Text"),
                new HelpItem(
                    "us/race-board/filters",
                    "US.Board.Filter.Har",
                    "US.Help.RaceBoard.Filters.Text"),
                new HelpItem(
                    "us/race-board/search",
                    "US.Board.SearchPlaceholder",
                    "US.Help.RaceBoard.Search.Text"),
                new HelpItem(
                    "us/race-board/counts",
                    "US.Board.Counts",
                    "US.Help.RaceBoard.Counts.Text"),
                new HelpItem(
                    "us/race-board/xenodir",
                    "US.Board.XenoDir",
                    "US.Help.RaceBoard.XenoDir.Text"),
                new HelpItem(
                    "us/race-board/xenorow",
                    "US.Board.XenoDir",
                    "US.Help.RaceBoard.XenoRow.Text"),
            }),
        ["us/fallback-editor"] = new HelpSection(
            "us/fallback-editor",
            "US.Section.FallbackTable",
            "US.Help.FallbackEditor.Overview",
            new[]
            {
                new HelpItem(
                    "us/fallback-editor/states",
                    "US.Help.FallbackEditor.States.Label",
                    "US.Help.FallbackEditor.States.Text"),
            }),
        ["us/theme-picker"] = new HelpSection(
            "us/theme-picker",
            "US.Section.Theme",
            "US.Help.Theme.Overview",
            new[]
            {
                new HelpItem(
                    "us/theme-picker/switch",
                    "US.Help.Theme.Switch.Label",
                    "US.Help.Theme.Switch.Text"),
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
