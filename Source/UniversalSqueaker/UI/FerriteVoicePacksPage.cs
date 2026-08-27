using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using KitLayoutEngine = FerriteLib.UiKit.LayoutEngine;
using KitLayoutManifest = FerriteLib.UiKit.LayoutManifest;
using KitUiCommand = FerriteLib.UiKit.UiCommand;
using KitUiPageState = FerriteLib.UiKit.UiPageState;
using KitWidgetContext = FerriteLib.UiKit.WidgetContext;

namespace UniversalSqueaker.UI;

/// <summary>
/// FerriteLib.UiKit-backed VoicePacks page. Builds the same read-only view state as the legacy
/// page, projects it into a neutral dictionary, then runs the two-pass layout engine over the
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

    private static readonly VoicePacksPageState State = new();
    private static bool sessionActive;
    private static KitLayoutEngine? engine;

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
                ["ActionScopes"] = view.ActionScopes,
                ["BaselinePresets"] = view.BaselinePresets
            };

            KitUiPageState uiState = new()
            {
                ScrollPosition = State.ScrollPosition,
                SearchText = State.SearchText,
                HelpOpen = State.HelpOpen
            };

            KitWidgetContext ctx = new(Source, viewState, VerseFerriteTextMetrics.Instance, uiState);
            KitLayoutEngine layoutEngine = GetEngine();

            float layoutWidth = rect.width;
            float contentHeight = layoutEngine.Measure(ctx, layoutWidth);
            if (contentHeight > rect.height + 0.01f)
            {
                layoutWidth = Math.Max(1f, rect.width - ScrollbarWidth);
                contentHeight = layoutEngine.Measure(ctx, layoutWidth);
            }

            layoutEngine.ClampScroll(uiState, rect.height);

            var kitCommands = new List<KitUiCommand>();
            Widgets.BeginScrollView(rect, ref uiState.ScrollPosition, new Rect(0f, 0f, layoutWidth, contentHeight));
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

            var businessCommands = new List<UiCommand>();
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

    private static KitLayoutEngine GetEngine()
    {
        if (engine != null) return engine;

        using Stream? stream = typeof(FerriteVoicePacksPage).Assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded UI layout resource '{ManifestResourceName}' was not found. Add UI\\Layout.xml as an EmbeddedResource.");
        }

        using var reader = new StreamReader(stream);
        KitLayoutManifest manifest = KitLayoutManifest.Parse(reader.ReadToEnd());
        engine = new KitLayoutEngine(manifest);
        return engine;
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
