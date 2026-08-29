using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Simplified vanilla fallback page for the VoicePacks settings. It intentionally uses only
/// <c>Verse.Widgets</c> and the existing business model so the player can still change core routing
/// if the Ferrite rendering path fails. Scope tree, mood tuning, and preset import are omitted.
/// </summary>
public static class VanillaVoicePacksPage
{
    private const float RowHeight = 26f;
    private const float ToggleRowHeight = 24f;
    private const float SectionGap = 6f;
    private const float Padding = 8f;

    private static readonly List<UiCommand> Commands = new();

    internal static void ResetSession()
    {
        FerriteVoicePacksPage.State.Reset();
    }

    public static void Draw(Rect rect)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UniversalSqueakerSettings settings = UniversalSqueakerMod.Settings;
        if (settings == null)
        {
            EmptyState.Draw(rect, "Universal Squeaker settings are unavailable.");
            return;
        }

        VoicePacksViewState view = VoicePacksPageModel.BuildView(settings, SqueakXenotypeCatalog.Current, FerriteVoicePacksPage.State);
        Commands.Clear();

        float contentHeight = ComputeContentHeight(view);
        Rect contentRect = new(0f, 0f, rect.width, Math.Max(rect.height, contentHeight));

        Widgets.BeginScrollView(rect, ref FerriteVoicePacksPage.State.ScrollPosition, contentRect);
        try
        {
            float y = 0f;
            float innerWidth = Math.Max(1f, rect.width - Padding * 2f);
            float x = rect.x + Padding;

            y = DrawModeRow(new Rect(x, y, innerWidth, RowHeight), view, y);
            y += SectionGap;

            y = DrawGlobalVolume(new Rect(x, y, innerWidth, RowHeight), view, y);
            y += SectionGap;

            y = DrawDistancePresets(new Rect(x, y, innerWidth, RowHeight), view, y);
            y += SectionGap;

            y = DrawToggles(new Rect(x, y, innerWidth, ToggleRowHeight * 5f + SectionGap * 4f), view, y);
            y += SectionGap;

            y = DrawDomains(new Rect(x, y, innerWidth, RowHeight), view, y);
            y += SectionGap;

            DrawVoicePacks(new Rect(x, y, innerWidth, RowHeight), view, y);
        }
        finally
        {
            Widgets.EndScrollView();
        }

        VoicePacksPageModel.ExecuteAll(settings, Commands, FerriteVoicePacksPage.State);
    }

    private static float DrawModeRow(Rect rect, VoicePacksViewState view, float startY)
    {
        float y = startY;
        SqueakVoicePackMode[] modes = { SqueakVoicePackMode.Vanilla, SqueakVoicePackMode.Fallback, SqueakVoicePackMode.Remix, SqueakVoicePackMode.Disabled };
        foreach (SqueakVoicePackMode mode in modes)
        {
            string label = (view.Mode == mode ? "● " : "") + mode.ToString();
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, RowHeight), label))
            {
                Commands.Add(new UiCommand(UiCommandKind.SetMode, mode: mode));
            }
            y += RowHeight + 2f;
        }
        return y;
    }

    private static float DrawGlobalVolume(Rect rect, VoicePacksViewState view, float startY)
    {
        float y = startY;
        Widgets.Label(new Rect(rect.x, y, rect.width, 20f), "Global volume: " + Mathf.RoundToInt(view.GlobalVolumeFactor * 100f) + "%");
        y += 20f;
        float next = Widgets.HorizontalSlider(new Rect(rect.x, y, rect.width, 20f), view.GlobalVolumeFactor, 0f, 1f, middleAlignment: true);
        if (Math.Abs(next - view.GlobalVolumeFactor) > 0.0001f)
        {
            Commands.Add(new UiCommand(UiCommandKind.SetGlobalVolume, arg: next.ToString("0.###", CultureInfo.InvariantCulture)));
        }
        return y + 20f;
    }

    private static float DrawDistancePresets(Rect rect, VoicePacksViewState view, float startY)
    {
        float y = startY;
        Widgets.Label(new Rect(rect.x, y, rect.width, 20f), "Distance preset: " + view.DistancePreset);
        y += 20f;
        SqueakDistancePreset[] presets = { SqueakDistancePreset.Conservative, SqueakDistancePreset.Balanced, SqueakDistancePreset.Strong };
        foreach (SqueakDistancePreset preset in presets)
        {
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, RowHeight), preset.ToString()))
            {
                Commands.Add(new UiCommand(UiCommandKind.SetDistancePreset, arg: preset.ToString()));
            }
            y += RowHeight + 2f;
        }
        return y;
    }

    private static float DrawToggles(Rect rect, VoicePacksViewState view, float startY)
    {
        float y = startY;
        y = DrawCheckboxRow(new Rect(rect.x, y, rect.width, ToggleRowHeight), "Easter egg sounds", view.AllowEasterEggs,
            () => new UiCommand(UiCommandKind.ToggleEgg, flag: !view.AllowEasterEggs), y);
        y = DrawCheckboxRow(new Rect(rect.x, y, rect.width, ToggleRowHeight), "Scale cooldown with time speed", view.ScaleCooldownWithTimeSpeed,
            () => new UiCommand(UiCommandKind.ToggleBasic, arg: "ScaleCooldown", flag: !view.ScaleCooldownWithTimeSpeed), y);
        y = DrawCheckboxRow(new Rect(rect.x, y, rect.width, ToggleRowHeight), "Scale frequency with talking", view.ScaleFrequencyWithTalking,
            () => new UiCommand(UiCommandKind.ToggleBasic, arg: "ScaleTalking", flag: !view.ScaleFrequencyWithTalking), y);
        y = DrawCheckboxRow(new Rect(rect.x, y, rect.width, ToggleRowHeight), "Scale periodic with audible population", view.ScalePeriodicWithAudiblePopulation,
            () => new UiCommand(UiCommandKind.ToggleBasic, arg: "ScalePopulation", flag: !view.ScalePeriodicWithAudiblePopulation), y);
        y = DrawCheckboxRow(new Rect(rect.x, y, rect.width, ToggleRowHeight), "Show camera indicator", view.ShowCameraIndicator,
            () => new UiCommand(UiCommandKind.ToggleBasic, arg: "CameraIndicator", flag: !view.ShowCameraIndicator), y);
        return y;
    }

    private static float DrawCheckboxRow(Rect rect, string label, bool value, Func<UiCommand> command, float startY)
    {
        bool checkboxValue = value;
        Widgets.Checkbox(new Vector2(rect.x, startY + 3f), ref checkboxValue, 18f);
        Widgets.Label(new Rect(rect.x + 24f, startY + 3f, Math.Max(1f, rect.width - 28f), 18f), label);
        if (Widgets.ButtonInvisible(rect))
        {
            Commands.Add(command());
        }
        return startY + rect.height;
    }

    private static float DrawDomains(Rect rect, VoicePacksViewState view, float startY)
    {
        float y = startY;
        Widgets.Label(new Rect(rect.x, y, rect.width, 20f), "Domains");
        y += 20f;

        foreach (RaceLayerRowView race in view.Races)
        {
            bool selected = view.SelectedDomain != null
                && view.SelectedDomain.Value.Scope == SqueakVoicePackScope.Race
                && string.Equals(view.SelectedDomain.Value.RaceDefName, race.RaceDefName, StringComparison.Ordinal);
            string label = (selected ? "● " : "") + race.DisplayName;
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, RowHeight), label))
            {
                Commands.Add(new UiCommand(UiCommandKind.SelectDomain, scope: SqueakVoicePackScope.Race, raceDefName: race.RaceDefName));
            }
            y += RowHeight + 2f;
        }

        foreach (VoicePackDomainView domain in view.XenotypeDomains)
        {
            bool selected = view.SelectedDomain != null
                && view.SelectedDomain.Value.Scope == SqueakVoicePackScope.Xenotype
                && string.Equals(view.SelectedDomain.Value.RaceDefName, domain.RaceDefName, StringComparison.Ordinal)
                && string.Equals(view.SelectedDomain.Value.TargetDefName, domain.TargetDefName, StringComparison.Ordinal);
            string label = (selected ? "● " : "") + domain.DisplayName + " (" + domain.RaceDefName + ")";
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, RowHeight), label))
            {
                Commands.Add(new UiCommand(
                    UiCommandKind.SelectDomain,
                    scope: SqueakVoicePackScope.Xenotype,
                    raceDefName: domain.RaceDefName,
                    targetDefName: domain.TargetDefName));
            }
            y += RowHeight + 2f;
        }

        return y;
    }

    private static void DrawVoicePacks(Rect rect, VoicePacksViewState view, float startY)
    {
        if (view.SelectedDomain == null) return;

        VoicePackDomainView domain = view.SelectedDomain.Value;
        float y = startY;
        Widgets.Label(new Rect(rect.x, y, rect.width, 20f), "VoicePacks");
        y += 20f;

        foreach (VoicePackRowView row in domain.Packs)
        {
            bool checkboxValue = row.IsSelected;
            Widgets.Checkbox(new Vector2(rect.x, y + 3f), ref checkboxValue, 18f);
            Widgets.Label(new Rect(rect.x + 24f, y + 3f, Math.Max(1f, rect.width - 28f), 18f), row.Label);
            if (Widgets.ButtonInvisible(new Rect(rect.x, y, rect.width, ToggleRowHeight)))
            {
                Commands.Add(new UiCommand(
                    UiCommandKind.TogglePack,
                    scope: domain.Scope,
                    raceDefName: domain.RaceDefName,
                    targetDefName: domain.TargetDefName,
                    arg: row.Key,
                    flag: !row.IsSelected));
            }
            y += ToggleRowHeight;
        }
    }

    private static float ComputeContentHeight(VoicePacksViewState view)
    {
        float height = 0f;
        height += RowHeight * 4f + 2f * 3f + SectionGap; // modes
        height += 40f + SectionGap; // global volume
        height += 20f + RowHeight * 3f + 2f * 2f + SectionGap; // distance presets
        height += ToggleRowHeight * 5f + SectionGap * 4f + SectionGap; // toggles
        height += 20f + (view.Races.Count + view.XenotypeDomains.Count) * (RowHeight + 2f) + SectionGap;
        if (view.SelectedDomain != null)
        {
            height += 20f + view.SelectedDomain.Value.Packs.Count * ToggleRowHeight;
        }
        return height + 20f;
    }
}
