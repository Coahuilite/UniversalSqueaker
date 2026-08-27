using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Composes the componentized minimal VoicePack settings page. Owns page-local UI state
/// (scroll/search/selected domain), projects the view state each frame, collects component commands,
/// and funnels them through <see cref="VoicePacksPageModel"/>.
/// </summary>
public static class VoicePacksPage
{
    private const string HelpText =
        "This page configures which VoicePacks provide sounds for each race and xenotype. "
        + "Changes apply immediately and are saved when the settings window closes.";

    private static readonly VoicePacksPageState State = new();
    private static bool sessionActive;

    /// <summary>
    /// Phase A integration switch. When true the page renders through FerriteLib.UiKit
    /// (<see cref="FerriteVoicePacksPage"/>) instead of the componentized path below.
    /// </summary>
    public static bool UseFerriteUi = true;

    /// <summary>Width reserved for the vertical scrollbar so content is not clipped by it.</summary>
    private const float ScrollbarWidth = 16f;

    public static void BeginSession()
    {
        if (UseFerriteUi)
        {
            UsWidgetRegistrar.EnsureRegistered();
            FerriteVoicePacksPage.BeginSession();
            return;
        }

        if (!sessionActive) State.Reset();
        sessionActive = true;
    }

    public static void EndSession()
    {
        State.Reset();
        sessionActive = false;
        FerriteVoicePacksPage.EndSession();
    }

    public static void Draw(Rect rect)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        if (UseFerriteUi)
        {
            UsWidgetRegistrar.EnsureRegistered();
            FerriteVoicePacksPage.Draw(rect);
            return;
        }

        try
        {
            UniversalSqueakerSettings settings = UniversalSqueakerMod.Settings;
            if (settings == null)
            {
                EmptyState.Draw(rect, "Universal Squeaker settings are unavailable.");
                return;
            }

            VoicePacksViewState view = VoicePacksPageModel.BuildView(settings, SqueakXenotypeCatalog.Current, State);
            List<UiCommand> commands = new();
            Action<UiCommand> emit = commands.Add;
            ITextMetrics metrics = VerseTextMetrics.Instance;
            float layoutWidth = rect.width;
            float contentHeight = VoicePacksLayout.MeasureContentHeight(layoutWidth, view, State, metrics);
            if (contentHeight > rect.height + 0.01f)
            {
                layoutWidth = Math.Max(1f, rect.width - ScrollbarWidth);
                contentHeight = VoicePacksLayout.MeasureContentHeight(layoutWidth, view, State, metrics);
            }

            Rect content = new(0f, 0f, layoutWidth, contentHeight);
            Widgets.BeginScrollView(rect, ref State.ScrollPosition, content);
            try
            {
                DrawContents(content, view, State, metrics, emit);
            }
            finally
            {
                Widgets.EndScrollView();
            }
            VoicePacksPageModel.ExecuteAll(settings, commands, State);
        }
        catch (Exception ex)
        {
            Log.Warning("[UniversalSqueaker] VoicePacks settings UI render failed: " + ex);
            EmptyState.Draw(rect, "VoicePacks settings UI failed to render. Settings are safe; check the log.");
        }
    }

    private static void DrawContents(
        Rect content,
        VoicePacksViewState view,
        VoicePacksPageState state,
        ITextMetrics metrics,
        Action<UiCommand> emit)
    {
        SectionFrame.Draw(content, SectionFrame.SurfaceKind.Emphasized);
        float innerX = VoicePacksLayout.Padding;
        float innerWidth = VoicePacksLayout.InnerWidth(content.width);
        float y = VoicePacksLayout.Padding;

        // Title row.
        Rect titleRect = new(innerX, y, Math.Max(1f, innerWidth - VoicePacksLayout.TitleHeight - 6f), VoicePacksLayout.TitleHeight);
        DrawTitle(titleRect, "VoicePack Routing");
        Rect helpRect = new(innerX + innerWidth - VoicePacksLayout.TitleHeight, y, VoicePacksLayout.TitleHeight, VoicePacksLayout.TitleHeight);
        if (HelpToggle.Draw(helpRect, state.HelpOpen)) state.HelpOpen = !state.HelpOpen;
        y += VoicePacksLayout.TitleHeight + VoicePacksLayout.Gap;

        if (state.HelpOpen)
        {
            float helpHeight = VoicePacksLayout.BannerHeight(HelpText, innerWidth, metrics);
            StatusBanner.Draw(new Rect(innerX, y, innerWidth, helpHeight), HelpText, SectionFrame.SurfaceKind.Base);
            y += helpHeight + VoicePacksLayout.Gap;
        }

        if (!string.IsNullOrEmpty(view.BannerText))
        {
            float bannerHeight = VoicePacksLayout.BannerHeight(view.BannerText, innerWidth, metrics);
            StatusBanner.Draw(new Rect(innerX, y, innerWidth, bannerHeight), view.BannerText,
                view.Mode == SqueakVoicePackMode.Vanilla ? SectionFrame.SurfaceKind.Warning : SectionFrame.SurfaceKind.Base);
            y += bannerHeight + VoicePacksLayout.Gap;
        }

        // Mode cards.
        float modeGap = VoicePacksLayout.Gap;
        float modeWidth = (innerWidth - modeGap * 3f) / 4f;
        DrawModeCard(innerX, y, modeWidth, VoicePacksLayout.ModeCardHeight, view.Mode, SqueakVoicePackMode.Vanilla,
            "Vanilla", "Route only vanilla audio; keep VoicePacks disabled.", emit);
        DrawModeCard(innerX + modeWidth + modeGap, y, modeWidth, VoicePacksLayout.ModeCardHeight, view.Mode,
            SqueakVoicePackMode.Fallback, "Fallback", "Use built-in fallback profiles when no pack is enabled.", emit);
        DrawModeCard(innerX + (modeWidth + modeGap) * 2f, y, modeWidth, VoicePacksLayout.ModeCardHeight, view.Mode,
            SqueakVoicePackMode.Remix, "Remix", "Mix enabled VoicePacks within each selected domain.", emit);
        DrawModeCard(innerX + (modeWidth + modeGap) * 3f, y, modeWidth, VoicePacksLayout.ModeCardHeight, view.Mode,
            SqueakVoicePackMode.Disabled, "Disabled", "Fully bypass the mod; play nothing.", emit);
        y += VoicePacksLayout.ModeCardHeight + VoicePacksLayout.Gap;

        // Easter-egg toggle (A7): main settings, alongside the mode cards, default off.
        float eggRowHeight = 28f;
        Rect eggRect = new(innerX, y, innerWidth, eggRowHeight);
        DrawEasterEggToggle(eggRect, view.AllowEasterEggs, emit);
        y += eggRowHeight + VoicePacksLayout.Gap;

        // Distance preset (A3): conservative/balanced/strong quick switch.
        float distRowHeight = 28f;
        Rect distRect = new(innerX, y, innerWidth, distRowHeight);
        DrawDistancePresetRow(distRect, view.DistancePreset, emit);
        y += distRowHeight + VoicePacksLayout.Gap;

        // Basic global tuning (A4/A5): three scaling toggles.
        float basicRowHeight = 26f;
        DrawBasicToggle(new Rect(innerX, y, innerWidth, basicRowHeight), "ScaleCooldown", "Scale cooldown with time speed", view.ScaleCooldownWithTimeSpeed, emit);
        y += basicRowHeight + 2f;
        DrawBasicToggle(new Rect(innerX, y, innerWidth, basicRowHeight), "ScaleTalking", "Scale frequency with talking", view.ScaleFrequencyWithTalking, emit);
        y += basicRowHeight + 2f;
        DrawBasicToggle(new Rect(innerX, y, innerWidth, basicRowHeight), "ScalePopulation", "Scale periodic with audible population", view.ScalePeriodicWithAudiblePopulation, emit);
        y += basicRowHeight + VoicePacksLayout.Gap;

        // Race layer.
        if (view.Races.Count > 0)
        {
            DrawSectionHeader(new Rect(innerX, y, innerWidth, VoicePacksLayout.SectionHeaderHeightFor("Race Layer", innerWidth, metrics)), "Race Layer");
            y += VoicePacksLayout.SectionHeaderHeightFor("Race Layer", innerWidth, metrics) + VoicePacksLayout.Gap;
            foreach (RaceLayerRowView race in view.Races)
            {
                bool selected = view.SelectedDomain != null
                    && view.SelectedDomain.Value.Scope == SqueakVoicePackScope.Race
                    && string.Equals(view.SelectedDomain.Value.RaceDefName, race.RaceDefName, StringComparison.Ordinal);
                Rect raceRect = new(innerX, y, innerWidth, VoicePacksLayout.RaceLayerRowHeight);
                RaceLayerRow.Draw(raceRect, race, selected, emit);
                y += VoicePacksLayout.RaceLayerRowHeight + VoicePacksLayout.Gap;
            }
        }

        // Selected domain checklist.
        if (view.SelectedDomain != null)
        {
            DrawSectionHeader(new Rect(innerX, y, innerWidth, VoicePacksLayout.SectionHeaderHeightFor("VoicePack Checklist", innerWidth, metrics)), "VoicePack Checklist");
            y += VoicePacksLayout.SectionHeaderHeightFor("VoicePack Checklist", innerWidth, metrics) + VoicePacksLayout.Gap;
            VoicePackDomainView domain = view.SelectedDomain.Value;
            float checklistHeight = VoicePacksLayout.ChecklistHeight(domain, state.SearchText, innerWidth, metrics);
            Rect checklistRect = new(innerX, y, innerWidth, checklistHeight);
            VoicePackChecklist.Draw(checklistRect, domain, ref state.SearchText, emit);
            y += checklistHeight + VoicePacksLayout.Gap;
        }
        else
        {
            DrawSectionHeader(new Rect(innerX, y, innerWidth, VoicePacksLayout.SectionHeaderHeightFor("VoicePack Checklist", innerWidth, metrics)), "VoicePack Checklist");
            y += VoicePacksLayout.SectionHeaderHeightFor("VoicePack Checklist", innerWidth, metrics) + VoicePacksLayout.Gap;
            EmptyState.Draw(new Rect(innerX, y, innerWidth, VoicePacksLayout.EmptyStateHeight),
                "No VoicePack domains are available yet. Install a VoicePack that declares a raceDefName.");
            y += VoicePacksLayout.EmptyStateHeight + VoicePacksLayout.Gap;
        }

        // Footer.
        Footer.Draw(new Rect(innerX, y, innerWidth, VoicePacksLayout.FooterHeight),
            "Changes apply immediately. Settings are saved automatically when this window closes.");
    }

    private static void DrawModeCard(float x, float y, float width, float height,
        SqueakVoicePackMode current, SqueakVoicePackMode target,
        string title, string description, Action<UiCommand> emit)
    {
        ModeCard.Draw(new Rect(x, y, width, height), current, target, title, description, emit);
    }

    private static void DrawEasterEggToggle(Rect rect, bool enabled, Action<UiCommand> emit)
    {
        bool next = !enabled;
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);
        Rect labelRect = new(rect.x + 10f, rect.y + 4f, rect.width - 20f, 20f);
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(labelRect, "Easter egg sounds");
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(.82f, .80f, .74f, .92f);
        Widgets.Label(new Rect(rect.x + 10f, rect.y + 20f, rect.width - 20f, 20f), enabled ? "On (eggs join the pool)" : "Off (ordinary entries only)");
        Text.Font = oldFont;
        GUI.color = oldColor;
        bool toggled = Widgets.ButtonInvisible(rect);
        if (toggled)
            emit?.Invoke(new UiCommand(UiCommandKind.ToggleEgg, flag: next));
    }

    private static void DrawDistancePresetRow(Rect rect, SqueakDistancePreset current, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);
        Rect labelRect = new(rect.x + 10f, rect.y + 4f, rect.width - 20f, 20f);
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(labelRect, "Distance preset");
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(.82f, .80f, .74f, .92f);
        string desc = current switch
        {
            SqueakDistancePreset.Conservative => "Conservative (15~65)",
            SqueakDistancePreset.Strong => "Strong (15~40)",
            SqueakDistancePreset.Balanced => "Balanced (15~50)",
            _ => "Custom",
        };
        Widgets.Label(new Rect(rect.x + 10f, rect.y + 20f, rect.width - 20f, 20f), desc);
        Text.Font = oldFont;
        GUI.color = oldColor;
        if (Widgets.ButtonInvisible(rect))
        {
            SqueakDistancePreset next = current switch
            {
                SqueakDistancePreset.Conservative => SqueakDistancePreset.Balanced,
                SqueakDistancePreset.Balanced => SqueakDistancePreset.Strong,
                SqueakDistancePreset.Strong => SqueakDistancePreset.Conservative,
                _ => SqueakDistancePreset.Balanced,
            };
            emit?.Invoke(new UiCommand(UiCommandKind.SetDistancePreset, arg: next.ToString()));
        }
    }

    private static void DrawBasicToggle(Rect rect, string key, string label, bool enabled, Action<UiCommand> emit)
    {
        bool hovered = Mouse.IsOver(rect);
        Widgets.DrawBoxSolid(rect, hovered ? new Color(.16f, .145f, .12f, .94f) : UiPalette.Panel);
        SectionFrame.DrawBorder(rect);
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x + 10f, rect.y + 3f, rect.width - 60f, 20f), label);
        // checkbox on the right
        Rect checkRect = new(rect.xMax - 40f, rect.y + 4f, 20f, 20f);
        bool value = enabled;
        Widgets.Checkbox(checkRect.position, ref value, 20f);
        GUI.color = oldColor;
        Text.Font = oldFont;
        bool toggled = Widgets.ButtonInvisible(rect);
        if (toggled)
            emit?.Invoke(new UiCommand(UiCommandKind.ToggleBasic, arg: key, flag: !enabled));
    }

    private static void DrawTitle(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Medium;
        GUI.color = Color.white;
        Widgets.Label(rect, text);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static void DrawSectionHeader(Rect rect, string text)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = new Color(.95f, .92f, .84f);
        Widgets.Label(rect, text);
        Text.Font = oldFont;
        GUI.color = oldColor;
    }
}
