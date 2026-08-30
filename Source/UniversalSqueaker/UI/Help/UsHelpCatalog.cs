using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>Structured help section returned by <see cref="UsHelpCatalog"/>.</summary>
internal sealed class HelpSection
{
    public HelpSection(string key, string title, string overview, IReadOnlyList<HelpItem> items)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Overview = overview ?? throw new ArgumentNullException(nameof(overview));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public string Key { get; }

    public string Title { get; }

    public string Overview { get; }

    public IReadOnlyList<HelpItem> Items { get; }
}

/// <summary>One individual help entry inside a <see cref="HelpSection"/>.</summary>
internal sealed class HelpItem
{
    public HelpItem(string key, string label, string text)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public string Key { get; }

    public string Label { get; }

    public string Text { get; }
}

/// <summary>
/// US-side structured help catalog for widget-attached inline help. Keys are stable US widget keys;
/// content is English for now (localization is out of scope for this task).
/// </summary>
internal static class UsHelpCatalog
{
    private static readonly Dictionary<string, HelpSection> Sections = new(StringComparer.Ordinal)
    {
        ["us/page-title"] = new HelpSection(
            "us/page-title",
            "VoicePack Routing",
            "This page configures which VoicePacks provide sounds for each race and xenotype. Changes apply immediately and are saved when the settings window closes.",
            new[]
            {
                new HelpItem(
                    "us/page-title/nav",
                    "Navigation",
                    "Use the left navigation to jump between Basic settings, Tuning, and the Pack list. Selecting a section updates the right help panel."),
                new HelpItem(
                    "us/page-title/apply",
                    "Apply & Save",
                    "Every change on this page applies immediately. Values are saved when the settings window closes."),
            }),
        ["us/mode-row"] = new HelpSection(
            "us/mode-row",
            "Routing Mode",
            "Choose how VoicePack audio is routed. Vanilla keeps only vanilla audio, Fallback uses built-in profiles when no pack is enabled, Remix mixes enabled packs, and Disabled bypasses the mod.",
            new[]
            {
                new HelpItem(
                    "us/mode-row/vanilla",
                    "Vanilla",
                    "Route only vanilla audio and keep VoicePacks disabled."),
                new HelpItem(
                    "us/mode-row/fallback",
                    "Fallback",
                    "Use built-in fallback profiles when no VoicePack is enabled for a domain."),
                new HelpItem(
                    "us/mode-row/remix",
                    "Remix",
                    "Mix enabled VoicePacks within each selected domain."),
                new HelpItem(
                    "us/mode-row/disabled",
                    "Disabled",
                    "Fully bypass the mod and play nothing."),
            }),
        ["us/global-volume"] = new HelpSection(
            "us/global-volume",
            "Global Volume",
            "Global volume scales every final sound from 0% to 100%. 0% is not a Disabled short-circuit; the normal sound flow still runs, but the final volume is zero.",
            new[]
            {
                new HelpItem(
                    "us/global-volume/slider",
                    "Volume Slider",
                    "Drag to scale every final sound from 0% to 100%."),
                new HelpItem(
                    "us/global-volume/number",
                    "Number Field",
                    "Type an exact percentage between 0% and 100%. Press Enter or click elsewhere to commit."),
            }),
        ["us/attenuation-editor"] = new HelpSection(
            "us/attenuation-editor",
            "Camera Height Attenuation",
            "Shows the camera-height attenuation curve from 15 to 65. The start point is locked at 100% and the end point at 0%; drag them horizontally to set a linear fade. Quick presets are Conservative 15-65, Balanced 15-50, and Strong 15-40.",
            new[]
            {
                new HelpItem(
                    "us/attenuation-editor/chart",
                    "Attenuation Chart",
                    "Drag the two horizontal control points to set where the fade starts and ends. The first point stays at 100% audibility and the last at 0%."),
                new HelpItem(
                    "us/attenuation-editor/presets",
                    "Quick Presets",
                    "Conservative keeps audio to 15-65, Balanced uses 15-50, and Strong uses 15-40."),
            }),
        ["us/basic-tuning"] = new HelpSection(
            "us/basic-tuning",
            "Basic Toggles",
            "Controls Easter egg sounds, the distance preset cycle, and the three runtime scaling toggles: cooldown with time speed, frequency with talking, and periodic with audible population.",
            new[]
            {
                new HelpItem(
                    "us/basic-tuning/egg",
                    "Easter Egg Sounds",
                    "When On, Easter egg entries join the normal sound pool. When Off, only ordinary entries play."),
                new HelpItem(
                    "us/basic-tuning/distance",
                    "Distance Preset",
                    "Cycles through Conservative (15-65), Balanced (15-50), Strong (15-40), and Custom."),
                new HelpItem(
                    "us/basic-tuning/scaling",
                    "Scaling Toggles",
                    "Scale cooldown with time speed, scale frequency with talking, and scale periodic sounds with the audible population."),
            }),
        ["us/camera-indicator"] = new HelpSection(
            "us/camera-indicator",
            "Camera Indicator",
            "Shows a small on-map indicator above pawns that currently have a Squeaker component and are eligible for audio.",
            new[]
            {
                new HelpItem(
                    "us/camera-indicator/toggle",
                    "Show Camera Indicator",
                    "Toggles the on-map indicator that helps you see which pawns are eligible for Squeaker audio."),
            }),
        ["us/scope-tree"] = new HelpSection(
            "us/scope-tree",
            "Tuning Editor",
            "Edits the three tuning layers: Global, Race, and Xenotype. Each action can inherit (Auto) or use Off, Any, or Command scopes depending on the action's supported states.",
            new[]
            {
                new HelpItem(
                    "us/scope-tree/layer",
                    "Tuning Layer",
                    "Choose whether the edits below apply globally, to one race, or to one xenotype."),
                new HelpItem(
                    "us/scope-tree/domain",
                    "Layer Domain",
                    "For Race and Xenotype layers, pick the exact race or xenotype domain being edited."),
                new HelpItem(
                    "us/scope-tree/action-scope",
                    "Action Scope",
                    "Each action can inherit its effective scope or be forced to Off, Any, or Command where supported."),
                new HelpItem(
                    "us/scope-tree/mood-tuning",
                    "Mood Tuning",
                    "Adjust pitch, volume, and jitter per mood. Auto clears the row back to inherited values."),
                new HelpItem(
                    "us/scope-tree/auto",
                    "Auto / Clear",
                    "Auto removes the current layer's override so the row falls back to the next effective layer."),
            }),
        ["us/preset-list"] = new HelpSection(
            "us/preset-list",
            "Tuning Baseline Presets",
            "Imports tuning baseline presets. Expand a preset, select races and xenotypes, then press Import to apply the saved action and mood tuning rows.",
            new[]
            {
                new HelpItem(
                    "us/preset-list/tree",
                    "Preset Tree",
                    "Expand a preset to choose which races and xenotypes should receive the baseline rows."),
                new HelpItem(
                    "us/preset-list/import",
                    "Import",
                    "Applies the checked baseline rows to the selected domains as the current tuning values."),
            }),
        ["us/filter-bar"] = new HelpSection(
            "us/filter-bar",
            "Quick Filters",
            "Filter which domains and VoicePacks are shown. Enabled only keeps domains with enabled packs, Conflicts shows conflicting or unavailable domains, Orphan only shows orphaned selections, and Author narrows pack rows by creator.",
            new[]
            {
                new HelpItem(
                    "us/filter-bar/domain",
                    "Domain Filters",
                    "All, Enabled only, Conflicts, and Orphan only change which domains and packs are visible in the Pack list."),
                new HelpItem(
                    "us/filter-bar/race-xeno",
                    "Race / Xenotype",
                    "Narrow the pack list to a specific race or xenotype domain."),
                new HelpItem(
                    "us/filter-bar/author",
                    "Author",
                    "Narrow VoicePack rows to a single author or mod creator."),
            }),
        ["us/race-layer"] = new HelpSection(
            "us/race-layer",
            "Race Layer",
            "Lists VoicePack domains by race. Select a row to configure which VoicePacks are enabled for that race.",
            new[]
            {
                new HelpItem(
                    "us/race-layer/row",
                    "Race Row",
                    "Click a race row to load that race's VoicePack checklist on the right."),
            }),
        ["us/xenotype-layer"] = new HelpSection(
            "us/xenotype-layer",
            "Xenotype Layer",
            "Lists VoicePack domains by xenotype, keyed by the (race, xenotype) pair so the same xenotype can be configured independently per race.",
            new[]
            {
                new HelpItem(
                    "us/xenotype-layer/row",
                    "Xenotype Row",
                    "Click a xenotype row to load that domain's VoicePack checklist on the right."),
            }),
        ["us/voice-pack-checklist"] = new HelpSection(
            "us/voice-pack-checklist",
            "VoicePack Checklist",
            "Search and toggle VoicePacks for the selected domain. Orphaned selections are shown when saved pack keys are no longer installed; use Forget Unavailable to clean them.",
            new[]
            {
                new HelpItem(
                    "us/voice-pack-checklist/search",
                    "Search",
                    "Filters the visible pack rows by name, mod, defName, or key."),
                new HelpItem(
                    "us/voice-pack-checklist/row",
                    "VoicePack Row",
                    "Click a row to toggle that VoicePack for the selected domain."),
                new HelpItem(
                    "us/voice-pack-checklist/forget",
                    "Forget Unavailable",
                    "Removes saved selections whose pack keys are no longer installed."),
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

    /// <summary>Compatibility accessor: returns a section's overview text, or null when missing.</summary>
    internal static string? Get(string key)
    {
        return TryGetSection(key, out HelpSection section) ? section.Overview : null;
    }
}
