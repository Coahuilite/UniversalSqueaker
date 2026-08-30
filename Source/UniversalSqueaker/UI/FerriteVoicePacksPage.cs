using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
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
    private const float NavWidth = 176f;
    private const float FooterHeight = UsFooterWidget.FooterHeight;

    internal static readonly VoicePacksPageState State = new();
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
        if (rect.width - NavWidth < VoicePacksLayout.MinMinimalWidth)
        {
            EmptyState.Draw(rect, "Window too narrow");
            return;
        }

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
            foreach (string helpKey in State.OpenHelpKeys)
            {
                uiState.OpenHelpKeys.Add(helpKey);
            }

            KitWidgetContext ctx = new(Source, viewState, VerseFerriteTextMetrics.Instance, uiState);
            string activeTab = NormalizeTab(State.ActiveTab);

            var kitCommands = new List<KitUiCommand>();
            var businessCommands = new List<UiCommand>();
            var navCommands = new List<UiCommand>();

            UiInteract.BeginFrame();
            try
            {
                Rect navRect = new(rect.x, rect.y, NavWidth, Math.Max(1f, rect.height));
                DrawNav(navRect, activeTab, navCommands.Add);

                float contentWidth = Math.Max(1f, rect.width - NavWidth);
                float contentAreaHeight = Math.Max(1f, rect.height - FooterHeight);
                Rect contentRect = new(rect.x + NavWidth, rect.y, contentWidth, contentAreaHeight);

                DrawContent(contentRect, ctx, uiState, kitCommands.Add);

                State.ScrollPosition = uiState.ScrollPosition;
                State.SearchText = uiState.SearchText;
                State.HelpOpen = uiState.HelpOpen;
                State.OpenHelpKeys.Clear();
                foreach (string helpKey in uiState.OpenHelpKeys)
                {
                    State.OpenHelpKeys.Add(helpKey);
                }

                Rect footerRect = new(rect.x + NavWidth, rect.y + contentRect.height, contentWidth, FooterHeight);
                DrawFooter(footerRect, ctx);

                UiInteract.ProcessEvents();

                bool toggleHelp = false;
                foreach (KitUiCommand kitCommand in kitCommands)
                {
                    if (kitCommand.Name == "ToggleHelp")
                    {
                        if (kitCommand.Payload is string helpKey)
                        {
                            uiState.ToggleHelpKey(helpKey);
                        }
                        else
                        {
                            toggleHelp = !toggleHelp;
                        }
                        continue;
                    }

                    if (TryTranslate(kitCommand, out UiCommand businessCommand))
                    {
                        businessCommands.Add(businessCommand);
                    }
                }

                if (toggleHelp) State.HelpOpen = !State.HelpOpen;

                VoicePacksPageModel.ExecuteAll(settings, navCommands, State);
                VoicePacksPageModel.ExecuteAll(settings, businessCommands, State);
            }
            finally
            {
                UiInteract.EndFrame();
            }
        }
        catch (Exception ex)
        {
            UiGuard.LogFallback("us/ferrite-page", "UniversalSqueaker", ex);
            try
            {
                DrawFallback(rect);
            }
            catch (Exception fallbackEx)
            {
                UiGuard.LogFallback("us/vanilla-fallback", "UniversalSqueaker", fallbackEx);
                EmptyState.Draw(rect, "VoicePacks settings UI failed to render. Settings are safe; check the log.");
            }
        }
    }

    private static KitLayoutEngine GetEngine(string activeTab, string? column = null)
    {
        string cacheKey = activeTab + "\n" + (column ?? "");
        if (Engines.TryGetValue(cacheKey, out KitLayoutEngine? engine)) return engine;

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
                continue;
            }

            if (column != null)
            {
                string? widgetColumn = (string?)widget.Attribute("Column");
                string effective = widgetColumn ?? "";
                if (!string.Equals(effective, column, StringComparison.OrdinalIgnoreCase))
                {
                    widget.Remove();
                }
            }
        }

        KitLayoutManifest manifest = KitLayoutManifest.Parse(doc.ToString(SaveOptions.DisableFormatting));
        KitLayoutEngine created = new(manifest);
        Engines[cacheKey] = created;
        return created;
    }

    private static KitLayoutEngine GetEngineAll(string? column = null)
    {
        string cacheKey = "ALL\n" + (column ?? "");
        if (Engines.TryGetValue(cacheKey, out KitLayoutEngine? engine)) return engine;

        XDocument doc = XDocument.Parse(ReadLayoutXml());
        XElement? root = doc.Root;
        if (root == null)
        {
            throw new InvalidOperationException("UI layout XML has no root element.");
        }

        List<XElement> widgets = new();
        foreach (XElement widget in root.Elements("Widget")) widgets.Add(widget);
        if (column != null)
        {
            foreach (XElement widget in widgets)
            {
                string? widgetColumn = (string?)widget.Attribute("Column");
                string effective = widgetColumn ?? "";
                if (!string.Equals(effective, column, StringComparison.OrdinalIgnoreCase))
                {
                    widget.Remove();
                }
            }
        }

        KitLayoutManifest manifest = KitLayoutManifest.Parse(doc.ToString(SaveOptions.DisableFormatting));
        KitLayoutEngine created = new(manifest);
        Engines[cacheKey] = created;
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

    private static void DrawContent(
        Rect contentRect,
        KitWidgetContext ctx,
        KitUiPageState uiState,
        Action<KitUiCommand> emit)
    {
        if (contentRect.width >= 1000f)
        {
            DrawMultiColumn(contentRect, ctx, uiState, emit);
        }
        else
        {
            DrawSingleColumn(contentRect, ctx, uiState, emit);
        }
    }

    private static void DrawSingleColumn(
        Rect contentRect,
        KitWidgetContext ctx,
        KitUiPageState uiState,
        Action<KitUiCommand> emit)
    {
        KitLayoutEngine engine = GetEngineAll();
        float layoutWidth = contentRect.width;
        float contentHeight = engine.Measure(ctx, layoutWidth);
        if (contentHeight > contentRect.height + 0.01f)
        {
            layoutWidth = Math.Max(1f, contentRect.width - ScrollbarWidth);
            contentHeight = engine.Measure(ctx, layoutWidth);
        }

        ApplyScrollTarget(engine, contentRect, uiState, contentHeight);
        engine.ClampScroll(uiState, contentRect.height);

        Widgets.BeginScrollView(contentRect, ref uiState.ScrollPosition, new Rect(0f, 0f, layoutWidth, contentHeight));
        UiInteract.PushScrollView(contentRect, uiState.ScrollPosition);
        try
        {
            engine.Draw(new Rect(0f, 0f, layoutWidth, contentHeight), ctx, emit, processEvents: false);
        }
        finally
        {
            UiInteract.PopScrollView();
            Widgets.EndScrollView();
        }
    }

    private static void DrawMultiColumn(
        Rect contentRect,
        KitWidgetContext ctx,
        KitUiPageState uiState,
        Action<KitUiCommand> emit)
    {
        const float gap = 12f;
        const float minColumnWidth = 260f;
        const float leftRatio = 0.38f;

        KitLayoutEngine topEngine = GetEngineAll("");
        KitLayoutEngine leftEngine = GetEngineAll("Left");
        KitLayoutEngine rightEngine = GetEngineAll("Right");

        float contentWidth = contentRect.width;
        float leftWidth = Mathf.Max(minColumnWidth, (contentWidth - gap) * leftRatio);
        float rightWidth = Mathf.Max(minColumnWidth, contentWidth - leftWidth - gap);
        if (leftWidth + rightWidth + gap > contentWidth)
        {
            rightWidth = Mathf.Max(1f, contentWidth - leftWidth - gap);
        }

        float topHeight = topEngine.Measure(ctx, contentWidth);
        float leftHeight = leftEngine.Measure(ctx, leftWidth);
        float rightHeight = rightEngine.Measure(ctx, rightWidth);
        float columnsHeight = Mathf.Max(leftHeight, rightHeight);
        float contentHeight = topHeight + (columnsHeight > 0f ? columnsHeight + gap : 0f);

        if (contentHeight > contentRect.height + 0.01f)
        {
            contentWidth = Mathf.Max(1f, contentRect.width - ScrollbarWidth);
            leftWidth = Mathf.Max(minColumnWidth, (contentWidth - gap) * leftRatio);
            rightWidth = Mathf.Max(minColumnWidth, contentWidth - leftWidth - gap);
            if (leftWidth + rightWidth + gap > contentWidth)
            {
                rightWidth = Mathf.Max(1f, contentWidth - leftWidth - gap);
            }

            topHeight = topEngine.Measure(ctx, contentWidth);
            leftHeight = leftEngine.Measure(ctx, leftWidth);
            rightHeight = rightEngine.Measure(ctx, rightWidth);
            columnsHeight = Mathf.Max(leftHeight, rightHeight);
            contentHeight = topHeight + (columnsHeight > 0f ? columnsHeight + gap : 0f);
        }

        ApplyScrollTarget(topEngine, leftEngine, rightEngine, topHeight, gap, contentRect, uiState, contentHeight);

        float maxY = Mathf.Max(0f, contentHeight - contentRect.height);
        uiState.ScrollPosition.y = Mathf.Min(Mathf.Max(0f, uiState.ScrollPosition.y), maxY);

        Widgets.BeginScrollView(contentRect, ref uiState.ScrollPosition, new Rect(0f, 0f, contentWidth, contentHeight));
        UiInteract.PushScrollView(contentRect, uiState.ScrollPosition);
        try
        {
            topEngine.Draw(new Rect(0f, 0f, contentWidth, topHeight), ctx, emit, processEvents: false);
            float y = topHeight + (columnsHeight > 0f ? gap : 0f);
            leftEngine.Draw(new Rect(0f, y, leftWidth, leftHeight), ctx, emit, processEvents: false);
            rightEngine.Draw(new Rect(leftWidth + gap, y, rightWidth, rightHeight), ctx, emit, processEvents: false);
        }
        finally
        {
            UiInteract.PopScrollView();
            Widgets.EndScrollView();
        }
    }

    private static void ApplyScrollTarget(
        KitLayoutEngine engine,
        Rect contentRect,
        KitUiPageState uiState,
        float contentHeight)
    {
        if (string.IsNullOrEmpty(State.ScrollTargetKey)) return;
        string key = State.ScrollTargetKey;
        State.ScrollTargetKey = "";
        if (engine.TryGetElementY(key, out float y))
        {
            uiState.ScrollPosition.y = Mathf.Clamp(y, 0f, Mathf.Max(0f, contentHeight - contentRect.height));
        }
    }

    private static void ApplyScrollTarget(
        KitLayoutEngine topEngine,
        KitLayoutEngine leftEngine,
        KitLayoutEngine rightEngine,
        float topHeight,
        float gap,
        Rect contentRect,
        KitUiPageState uiState,
        float contentHeight)
    {
        if (string.IsNullOrEmpty(State.ScrollTargetKey)) return;
        string key = State.ScrollTargetKey;
        State.ScrollTargetKey = "";

        float targetY;
        if (topEngine.TryGetElementY(key, out float topY))
        {
            targetY = topY;
        }
        else if (leftEngine.TryGetElementY(key, out float leftY))
        {
            targetY = topHeight + (leftY > 0f ? gap : 0f) + leftY;
        }
        else if (rightEngine.TryGetElementY(key, out float rightY))
        {
            targetY = topHeight + (rightY > 0f ? gap : 0f) + rightY;
        }
        else
        {
            return;
        }

        uiState.ScrollPosition.y = Mathf.Clamp(targetY, 0f, Mathf.Max(0f, contentHeight - contentRect.height));
    }

    private static void DrawNavWithFrame(Rect navRect, string activeTab, Action<UiCommand> addCommand)
    {
        // Standalone nav frame used by the safety-net fallback page. The normal Ferrite page owns a
        // single UiInteract frame around nav + content + footer and calls DrawNav directly.
        UiInteract.BeginFrame();
        try
        {
            DrawNav(navRect, activeTab, addCommand);
            UiInteract.ProcessEvents();
        }
        finally
        {
            UiInteract.EndFrame();
        }
    }

    private static void DrawNav(Rect navRect, string activeTab, Action<UiCommand> addCommand)
    {
        const float groupHeaderHeight = 22f;
        const float itemHeight = 30f;
        const float gap = 3f;
        const float sidePadding = 10f;
        const float itemIndent = 6f;
        const float brandHeight = 52f;

        Widgets.DrawBoxSolid(navRect, Palette.Base);
        SurfaceFrame.DrawBorder(navRect, Palette.Border);

        float y = navRect.y + 12f;

        // Brand / page identity.
        Rect brandRect = new(navRect.x + sidePadding, y, Math.Max(1f, navRect.width - sidePadding * 2f), brandHeight);
        UiText.DrawLabel(new Rect(brandRect.x, brandRect.y, brandRect.width, 20f), "Universal Squeaker", Palette.TextPrimary);
        UiText.DrawCaption(new Rect(brandRect.x, brandRect.y + 20f, brandRect.width, 16f), "VoicePack Routing", Palette.TextSecondary);
        UiPanel.DrawDivider(new Rect(brandRect.x, brandRect.yMax - 1f, brandRect.width, 1f));
        y += brandHeight + 8f;

        foreach ((string group, string groupLabel) in NavGroups)
        {
            bool groupActive = string.Equals(group, activeTab, StringComparison.Ordinal);
            DrawNavGroupHeader(
                new Rect(navRect.x + sidePadding, y, Math.Max(1f, navRect.width - sidePadding * 2f), groupHeaderHeight),
                groupLabel,
                groupActive);
            y += groupHeaderHeight + gap;

            foreach ((string sectionKey, string itemLabel) in NavItemsByGroup(group))
            {
                Rect itemRect = new(
                    navRect.x + sidePadding + itemIndent,
                    y,
                    Math.Max(1f, navRect.width - sidePadding * 2f - itemIndent),
                    itemHeight);

                bool active = string.Equals(State.ActiveSectionKey, sectionKey, StringComparison.Ordinal);
                bool hovered = Mouse.IsOver(itemRect);
                Color fill = active ? Palette.Selected : hovered ? Palette.Hover : Color.clear;
                if (fill.a > 0f)
                {
                    Widgets.DrawBoxSolid(itemRect, fill);
                }
                SurfaceFrame.DrawBorder(itemRect, active ? Palette.BorderStrong : hovered ? Palette.Border : Color.clear);
                if (active)
                {
                    UiPanel.DrawAccentBar(
                        new Rect(itemRect.x - itemIndent + 2f, itemRect.y + 2f, 3f, Math.Max(1f, itemRect.height - 4f)),
                        Palette.AccentGold);
                }

                Color textColor = active ? Palette.TextOnGold
                    : hovered ? Palette.TextPrimary
                    : Palette.TextSecondary;
                UiText.DrawLabel(
                    new Rect(itemRect.x + 8f, itemRect.y, Math.Max(1f, itemRect.width - 12f), itemRect.height),
                    itemLabel,
                    textColor);

                string capturedKey = sectionKey;
                UiInteract.Button(itemRect, UiLayer.TopAction,
                    () => addCommand(new UiCommand(UiCommandKind.ScrollToSection, arg: capturedKey)));

                y += itemHeight + gap;
            }

            y += 8f;
        }
    }

    private static void DrawNavGroupHeader(Rect rect, string label, bool active)
    {
        UiText.DrawCaption(rect, label, active ? Palette.AccentGold : Palette.TextSecondary);
    }

    private static readonly (string Group, string Label)[] NavGroups =
    {
        ("Basic", "基础设置"),
        ("Tuning", "调音"),
        ("Packs", "包清单")
    };

    private static (string Key, string Label)[] NavItemsByGroup(string group)
    {
        return group switch
        {
            "Basic" => new[]
            {
                ("mode-row", "路由模式"),
                ("global-volume", "全局音量"),
                ("attenuation-editor", "距离/衰减"),
                ("basic-tuning", "基础开关"),
                ("camera-indicator", "相机指示")
            },
            "Tuning" => new[]
            {
                ("scope-tree", "动作作用域"),
                ("preset-list", "预设导入")
            },
            _ => new[]
            {
                ("filter-bar", "筛选"),
                ("race-layer", "种族层"),
                ("xenotype-layer", "异种层"),
                ("checklist", "语音包清单")
            }
        };
    }

    private static void DrawFallback(Rect rect)
    {
        if (rect.width - NavWidth < VoicePacksLayout.MinMinimalWidth)
        {
            EmptyState.Draw(rect, "Window too narrow");
            return;
        }

        var commands = new List<UiCommand>();
        Rect navRect = new(rect.x, rect.y, NavWidth, Math.Max(1f, rect.height));
        DrawNavWithFrame(navRect, NormalizeTab(State.ActiveTab), commands.Add);

        UniversalSqueakerSettings settings = UniversalSqueakerMod.Settings;
        if (settings != null)
        {
            VoicePacksPageModel.ExecuteAll(settings, commands, State);
        }

        Rect contentRect = new(
            rect.x + NavWidth,
            rect.y,
            Math.Max(1f, rect.width - NavWidth),
            Math.Max(1f, rect.height));
        VanillaVoicePacksPage.Draw(contentRect);
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
