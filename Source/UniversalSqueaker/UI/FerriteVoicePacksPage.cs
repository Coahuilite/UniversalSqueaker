using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEngine;
using Verse;
using KitLayoutEngine = FerriteLib.UiKit.LayoutEngine;
using KitLayoutManifest = FerriteLib.UiKit.LayoutManifest;
using KitUiCommand = FerriteLib.UiKit.UiCommand;
using KitUiPageState = FerriteLib.UiKit.UiPageState;
using KitWidgetContext = FerriteLib.UiKit.WidgetContext;

namespace UniversalSqueaker.UI;

/// <summary>
/// FerriteLib.UiKit-backed VoicePacks page. Builds the same read-only view state as the
/// componentized page, projects it into a neutral dictionary, then runs the two-pass layout engine over the
/// embedded manifest. Collected neutral commands are translated back into the existing business
/// <see cref="UiCommand"/> stream and executed through <see cref="VoicePacksPageModel"/>.
/// </summary>
public static class FerriteVoicePacksPage
{
    private const string Source = "coahuilite.universalsqueaker";
    private const string ManifestResourceName = "UniversalSqueaker.UI.Layout.xml";

    private const string HelpText =
        "This page configures which VoicePacks provide sounds for each race and xenotype. "
        + "Changes apply immediately and are saved when the settings window closes.";

    private const string EmptyDomainText =
        "No VoicePack domains are available yet. Install a VoicePack that declares a raceDefName.";

    /// <summary>Width reserved for the vertical scrollbar so content is not clipped by it.</summary>
    private const float ScrollbarWidth = 16f;
    private const float NavWidth = 140f;
    private const float FooterHeight = 28f;

    private static readonly VoicePacksPageState State = new();
    private static readonly Dictionary<string, KitLayoutEngine> Engines = new(StringComparer.Ordinal);
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
            UsWidgetRegistrar.EnsureRegistered();

            UniversalSqueakerSettings settings = UniversalSqueakerMod.Settings;
            if (settings == null)
            {
                EmptyState.Draw(rect, "Universal Squeaker settings are unavailable.");
                return;
            }

            VoicePacksViewState view = VoicePacksPageModel.BuildView(settings, SqueakXenotypeCatalog.Current, State);

            var viewState = new Dictionary<string, object?>
            {
                ["BannerText"] = view.BannerText,
                ["Mode"] = view.Mode.ToString(),
                ["Races"] = view.Races,
                ["XenotypeDomains"] = view.XenotypeDomains,
                ["SelectedDomain"] = view.SelectedDomain,
                ["EmptyText"] = EmptyDomainText,
                ["HelpText"] = HelpText,
                ["AllowEasterEggs"] = view.AllowEasterEggs,
                ["DistancePreset"] = view.DistancePreset.ToString(),
                ["ScaleCooldownWithTimeSpeed"] = view.ScaleCooldownWithTimeSpeed,
                ["ScaleFrequencyWithTalking"] = view.ScaleFrequencyWithTalking,
                ["ScalePeriodicWithAudiblePopulation"] = view.ScalePeriodicWithAudiblePopulation,
                ["ShowCameraIndicator"] = view.ShowCameraIndicator,
                ["GlobalVolumeFactor"] = view.GlobalVolumeFactor,
                ["DistanceRangeMin"] = view.DistanceRangeMin,
                ["DistanceRangeMax"] = view.DistanceRangeMax,
                ["ActionScopes"] = view.ActionScopes,
                ["BaselinePresets"] = view.BaselinePresets,
                ["TuningLayer"] = view.TuningLayer,
                ["TuningRace"] = view.TuningRaceDefName,
                ["TuningXeno"] = view.TuningXenotypeDefName,
                ["TuningDomains"] = view.TuningDomains,
                ["MoodTuningRows"] = view.MoodTuningRows,
                ["BuildIdentity"] = view.BuildIdentity,
                ["SaveStatus"] = view.SaveStatus,
                ["IsDirty"] = view.IsDirty,
                ["Authors"] = view.Authors,
                ["DomainFilter"] = State.DomainFilter,
                ["PackFilter"] = State.PackFilter
            };

            KitUiPageState uiState = new()
            {
                ScrollPosition = State.ScrollPosition,
                SearchText = State.SearchText,
                HelpOpen = State.HelpOpen
            };

            KitWidgetContext ctx = new(Source, viewState, VerseFerriteTextMetrics.Instance, uiState);
            string activeTab = NormalizeTab(State.ActiveTab);
            KitLayoutEngine layoutEngine = GetEngine(activeTab);

            var kitCommands = new List<KitUiCommand>();
            var businessCommands = new List<UiCommand>();

            Rect navRect = new(rect.x, rect.y, NavWidth, Math.Max(1f, rect.height));
            DrawNav(navRect, activeTab, businessCommands.Add);

            float contentWidth = Math.Max(1f, rect.width - NavWidth);
            float contentAreaHeight = Math.Max(1f, rect.height - FooterHeight);
            Rect contentRect = new(rect.x + NavWidth, rect.y, contentWidth, contentAreaHeight);

            float layoutWidth = contentRect.width;
            float contentHeight = layoutEngine.Measure(ctx, layoutWidth);
            if (contentHeight > contentRect.height + 0.01f)
            {
                layoutWidth = Math.Max(1f, contentRect.width - ScrollbarWidth);
                contentHeight = layoutEngine.Measure(ctx, layoutWidth);
            }

            layoutEngine.ClampScroll(uiState, contentRect.height);

            Widgets.BeginScrollView(contentRect, ref uiState.ScrollPosition, new Rect(0f, 0f, layoutWidth, contentHeight));
            try
            {
                layoutEngine.Draw(new Rect(0f, 0f, layoutWidth, contentHeight), ctx, kitCommands.Add);
            }
            finally
            {
                Widgets.EndScrollView();
            }

            State.ScrollPosition = uiState.ScrollPosition;
            State.SearchText = uiState.SearchText;
            State.HelpOpen = uiState.HelpOpen;

            Rect footerRect = new(rect.x + NavWidth, rect.y + contentRect.height, contentWidth, FooterHeight);
            DrawFooter(footerRect, ctx);

            bool toggleHelp = false;
            foreach (KitUiCommand kitCommand in kitCommands)
            {
                if (kitCommand.Name == "ToggleHelp")
                {
                    toggleHelp = !toggleHelp;
                    continue;
                }

                if (TryTranslate(kitCommand, out UiCommand businessCommand))
                {
                    businessCommands.Add(businessCommand);
                }
            }

            if (toggleHelp) State.HelpOpen = !State.HelpOpen;

            VoicePacksPageModel.ExecuteAll(settings, businessCommands, State);
        }
        catch (Exception ex)
        {
            Log.Warning("[UniversalSqueaker] Ferrite VoicePacks settings UI render failed: " + ex);
            EmptyState.Draw(rect, "VoicePacks settings UI failed to render. Settings are safe; check the log.");
        }
    }

    private static KitLayoutEngine GetEngine(string activeTab)
    {
        if (Engines.TryGetValue(activeTab, out KitLayoutEngine? engine)) return engine;

        XDocument doc = XDocument.Parse(ReadLayoutXml());
        XElement? root = doc.Root;
        if (root == null)
        {
            throw new InvalidOperationException("UI layout XML has no root element.");
        }

        List<XElement> widgets = new();
        foreach (XElement widget in root.Elements("Widget")) widgets.Add(widget);
        foreach (XElement widget in widgets)
        {
            string? tab = (string?)widget.Attribute("Tab");
            if (!string.IsNullOrEmpty(tab) && !string.Equals(tab, activeTab, StringComparison.OrdinalIgnoreCase))
            {
                widget.Remove();
            }
        }

        KitLayoutManifest manifest = KitLayoutManifest.Parse(doc.ToString(SaveOptions.DisableFormatting));
        KitLayoutEngine created = new(manifest);
        Engines[activeTab] = created;
        return created;
    }

    private static string ReadLayoutXml()
    {
        using Stream? stream = typeof(FerriteVoicePacksPage).Assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded UI layout resource '{ManifestResourceName}' was not found. Add UI\\Layout.xml as an EmbeddedResource.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string NormalizeTab(string tab)
    {
        if (string.Equals(tab, "Tuning", StringComparison.OrdinalIgnoreCase)) return "Tuning";
        if (string.Equals(tab, "Packs", StringComparison.OrdinalIgnoreCase)) return "Packs";
        return "Basic";
    }

    private static void DrawNav(Rect navRect, string activeTab, Action<UiCommand> addCommand)
    {
        const float buttonHeight = 32f;
        const float gap = 4f;
        const float sidePadding = 4f;

        Widgets.DrawBoxSolid(navRect, UiPalette.Panel);
        SectionFrame.DrawBorder(navRect);

        float y = navRect.y + 8f;
        foreach ((string tab, string label) in new[] { ("Basic", "基础设置"), ("Tuning", "调音"), ("Packs", "包清单") })
        {
            Rect buttonRect = new(
                navRect.x + sidePadding,
                y,
                Math.Max(1f, navRect.width - sidePadding * 2f),
                buttonHeight);

            if (string.Equals(tab, activeTab, StringComparison.Ordinal))
            {
                Widgets.DrawBoxSolid(buttonRect, new Color(.20f, .17f, .10f, .8f));
            }

            if (Widgets.ButtonText(buttonRect, label, drawBackground: false))
            {
                addCommand(new UiCommand(UiCommandKind.SetActiveTab, arg: tab));
            }

            y += buttonHeight + gap;
        }
    }

    private static void DrawFooter(Rect footerRect, KitWidgetContext ctx)
    {
        var footerSpec = new FerriteLib.UiKit.UiElementSpec("footer", UsFooterWidget.Kind);
        UsFooterWidget footer = new();
        footer.Configure(footerSpec);
        footer.Draw(footerRect, ctx, _ => { });
    }

    private static bool TryTranslate(KitUiCommand kitCommand, out UiCommand businessCommand)
    {
        businessCommand = default;

        if (kitCommand.Name == "SetMode"
            && kitCommand.Payload is string modeText
            && Enum.TryParse(modeText, true, out SqueakVoicePackMode mode))
        {
            businessCommand = new UiCommand(UiCommandKind.SetMode, mode: mode);
            return true;
        }

        if (kitCommand.Payload is UsCommandPayload payload)
        {
            businessCommand = new UiCommand(
                payload.Kind,
                payload.Mode,
                payload.Scope,
                payload.RaceDefName,
                payload.TargetDefName,
                payload.Arg,
                payload.Flag);
            return true;
        }

        return false;
    }
}
