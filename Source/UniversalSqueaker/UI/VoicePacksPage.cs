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

    public static void BeginSession()
    {
        if (!sessionActive) State.Reset();
        sessionActive = true;
    }

    public static void EndSession()
    {
        State.Reset();
        sessionActive = false;
    }

    public static void Draw(Rect rect)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;
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
            float contentHeight = VoicePacksLayout.MeasureContentHeight(rect.width, view, State, metrics);
            Rect content = new(0f, 0f, rect.width, contentHeight);
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
                view.Mode == SqueakVoicePackMode.Off ? SectionFrame.SurfaceKind.Warning : SectionFrame.SurfaceKind.Base);
            y += bannerHeight + VoicePacksLayout.Gap;
        }

        // Mode cards.
        float modeGap = VoicePacksLayout.Gap;
        float modeWidth = (innerWidth - modeGap * 2f) / 3f;
        DrawModeCard(innerX, y, modeWidth, VoicePacksLayout.ModeCardHeight, view.Mode, SqueakVoicePackMode.Off,
            "Off", "Disable VoicePack audio routing.", emit);
        DrawModeCard(innerX + modeWidth + modeGap, y, modeWidth, VoicePacksLayout.ModeCardHeight, view.Mode,
            SqueakVoicePackMode.Fallback, "Fallback", "Use built-in fallback profiles when no pack is enabled.", emit);
        DrawModeCard(innerX + (modeWidth + modeGap) * 2f, y, modeWidth, VoicePacksLayout.ModeCardHeight, view.Mode,
            SqueakVoicePackMode.Remix, "Remix", "Mix enabled VoicePacks within each selected domain.", emit);
        y += VoicePacksLayout.ModeCardHeight + VoicePacksLayout.Gap;

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
