using System;
using System.Collections.Generic;

namespace UniversalSqueaker.UI;

/// <summary>
/// US-side help text catalog for widget-attached inline help. Keys are stable US widget keys;
/// content is English for now (localization is out of scope for this task).
/// </summary>
internal static class UsHelpCatalog
{
    private static readonly Dictionary<string, string> Entries = new(StringComparer.Ordinal)
    {
        ["us/mode-row"] = "Choose how VoicePack audio is routed. Vanilla keeps only vanilla audio, Fallback uses built-in profiles when no pack is enabled, Remix mixes enabled packs, and Disabled bypasses the mod.",
        ["us/filter-bar"] = "Filter which domains and VoicePacks are shown. Enabled only keeps domains with enabled packs, Conflicts shows conflicting or unavailable domains, Orphan only shows orphaned selections, and Author narrows pack rows by creator.",
        ["us/basic-tuning"] = "Controls Easter egg sounds, the distance preset cycle, and the three runtime scaling toggles: cooldown with time speed, frequency with talking, and periodic with audible population.",
        ["us/global-volume"] = "Global volume scales every final sound from 0% to 100%. 0% is not a Disabled short-circuit; the normal sound flow still runs, but the final volume is zero.",
        ["us/attenuation-editor"] = "Shows the camera-height attenuation curve from 15 to 65. The start point is locked at 100% and the end point at 0%; drag them horizontally to set a linear fade. Quick presets are Conservative 15-65, Balanced 15-50, and Strong 15-40.",
        ["us/camera-indicator"] = "Shows a small on-map indicator above pawns that currently have a Squeaker component and are eligible for audio.",
        ["us/scope-tree"] = "Edits the three tuning layers: Global, Race, and Xenotype. Each action can inherit (Auto) or use Off, Any, or Command scopes depending on the action's supported states.",
        ["us/preset-list"] = "Imports tuning baseline presets. Expand a preset, select races and xenotypes, then press Import to apply the saved action and mood tuning rows.",
        ["us/race-layer"] = "Lists VoicePack domains by race. Select a row to configure which VoicePacks are enabled for that race.",
        ["us/xenotype-layer"] = "Lists VoicePack domains by xenotype, keyed by the (race, xenotype) pair so the same xenotype can be configured independently per race.",
        ["us/voice-pack-checklist"] = "Search and toggle VoicePacks for the selected domain. Orphaned selections are shown when saved pack keys are no longer installed; use Forget Unavailable to clean them.",
        ["us/page-title"] = "This page configures which VoicePacks provide sounds for each race and xenotype. Changes apply immediately and are saved when the settings window closes.",
    };

    internal static string? Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return Entries.TryGetValue(key, out string text) ? text : null;
    }
}
