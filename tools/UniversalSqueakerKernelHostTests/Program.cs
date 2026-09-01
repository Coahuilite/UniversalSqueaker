using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Real-schema Settings Host creation regression for Gate U. Executes the REAL production
/// <see cref="UsKernelSettingsHost"/> against the REAL embedded Schema=2 resource, the REAL US
/// widget registrations and the REAL typed binding table, using a recording
/// <see cref="IUsKernelSettingsSource"/> instead of the RimWorld graph.
///
/// This harness is deliberately creation/measure/draw focused (stub evidence): it does NOT claim
/// real RimWorld rendering, real translations, real catalog data or maintainer-verified behavior.
/// It proves the production creation-time contract (resource shape, kind resolution, attribute
/// schema, typed binding/action coverage, session lifecycle, layout structure at three viewports,
/// and typed business-boundary routing) executes without Verse.
/// </summary>
internal static class Program
{
    private const string ExpectedSource = "coahuilite.universalsqueaker";

    private static readonly (string Id, string Kind)[] ExpectedWidgets =
    {
        ("nav", "us/nav"),
        ("page-title", "us/page-title"),
        ("banner", "chrome/banner"),
        ("mode-row", "us/mode-row"),
        ("global-volume", "us/global-volume"),
        ("attenuation-editor", "us/attenuation-editor"),
        ("basic-tuning", "us/basic-tuning"),
        ("camera-indicator", "us/camera-indicator"),
        ("scope-tree", "us/scope-tree"),
        ("preset-list", "us/preset-list"),
        ("filter-bar", "us/filter-bar"),
        ("race-layer", "us/race-layer"),
        ("xenotype-layer", "us/xenotype-layer"),
        ("checklist", "us/voice-pack-checklist"),
        ("footer", "us/footer"),
        ("help-panel", "us/help-panel")
    };

    private static int Main()
    {
        try
        {
            RunAll();
            Console.WriteLine("ALL PASS");
            return 0;
        }
        catch (Exception ex)
        {
            // Robust failure report: Exception.ToString can itself fail (e.g. when the message
            // getter or a stack frame cannot be resolved), so print the parts separately.
            Console.Error.WriteLine("FAIL: " + ex.GetType().FullName);
            try
            {
                Console.Error.WriteLine("MESSAGE: " + ex.Message);
            }
            catch (Exception messageError)
            {
                Console.Error.WriteLine("MESSAGE-UNPRINTABLE: " + messageError.GetType().FullName);
            }

            if (ex.InnerException != null)
            {
                Console.Error.WriteLine("INNER: " + ex.InnerException.GetType().FullName);
                try
                {
                    Console.Error.WriteLine("INNER-MESSAGE: " + ex.InnerException.Message);
                }
                catch (Exception innerMessageError)
                {
                    Console.Error.WriteLine("INNER-MESSAGE-UNPRINTABLE: " + innerMessageError.GetType().FullName);
                }
            }

            return 1;
        }
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Step failed: " + name, ex);
        }
    }

    private static void RunAll()
    {
        Step("real embedded resource + schema shape", RealEmbeddedResourcePresentAndShaped);
        Step("real host creation validates full schema", RealHostCreationValidatesFullSchema);
        Step("reopen session isolation", RealHostReopenIsolation);
        Step("unknown kind fails at creation", UnknownKindFailsAtCreation);
        Step("unknown attribute fails at creation", UnknownAttributeFailsAtCreation);
        Step("typed bindings route to business boundary", TypedBindingsRouteToBusinessBoundary);
        Step("full typed write coverage", FullTypedWriteCoverage);
        Step("three viewport measure + draw", ThreeViewportMeasureAndDraw);
        Step("five workspaces across viewports", FiveWorkspacesAcrossViewports);
        Step("workspace switch resets session scroll", WorkspaceSwitchResetsSessionScroll);
        Step("rich dynamic data measure + draw", RichDynamicDataMeasureAndDraw);
        Step("800px three-column mood layout focused geometry/interaction", () => MoodLayoutFocusedTests.RunAll());
        Step("session popup isolation + cleanup", SessionPopupIsolationAndCleanup);
        Step("disposed host cannot draw", DisposedHostCannotDraw);
        Step("overlay host without settings window", OverlayHostCreatedWithoutSettingsWindow);
        Step("overlay widget contract fails at creation", OverlayWidgetContractFailsAtCreation);
        Step("overlay dual-host session isolation", OverlayDualHostSessionIsolation);
        Step("overlay safe area at three viewports", OverlaySafeAreaThreeViewports);
        Step("overlay show/hide/dispose/reopen", OverlayShowHideDisposeReopen);
        Step("overlay no-map safe exit", OverlayNoMapSafeExit);
        Step("overlay draw failure does not double-reserve the row cursor", OverlayDrawFailureDoesNotDoubleReserveRow);
        Step("closing settings window does not affect overlay", ClosingSettingsWindowDoesNotAffectOverlay);
    }

    private static void RealEmbeddedResourcePresentAndShaped()
    {
        Assembly production = typeof(UsKernelSettingsHost).Assembly;
        string[] names = production.GetManifestResourceNames();
        Assert(Array.Exists(names, name => name == "UniversalSqueaker.UI.Layout.Schema2.xml"),
            "production assembly must embed UniversalSqueaker.UI.Layout.Schema2.xml");

        string xml;
        using (Stream? stream = production.GetManifestResourceStream("UniversalSqueaker.UI.Layout.Schema2.xml"))
        {
            Assert(stream != null, "Schema=2 resource stream must be readable");
            using var reader = new StreamReader(stream!);
            xml = reader.ReadToEnd();
        }

        Assert(xml.Contains("Schema=\"2\""), "resource root declares Schema=2");
        Assert(xml.Contains("Source=\"" + ExpectedSource + "\""), "resource root declares the US source scope");
        Assert(xml.Contains("<Scroll Id=\"content-scroll\""), "content scroll container present");
        Assert(xml.Contains("<Scroll Id=\"help-scroll\""), "help scroll container present");
        Assert(xml.Contains("<Widget Id=\"footer\" Kind=\"us/footer\" Height=\"28\""), "fixed footer declared as body sibling");

        foreach ((string id, string kind) in ExpectedWidgets)
        {
            Assert(xml.Contains("<Widget Id=\"" + id + "\" Kind=\"" + kind + "\""),
                "resource declares widget " + id + " (" + kind + ")");
        }
    }

    private static void RealHostCreationValidatesFullSchema()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);

        Assert(host.Source == ExpectedSource, "host source matches the resource scope");
        Assert(host.Manifest.SchemaVersion == "2", "host manifest is schema 2");
        Assert(host.Session.IsActive, "host session is active after creation");

        // Creation succeeded => every kind in the real resource resolved through the real US/core
        // registries and every widget's Validate passed against the real typed binding table.
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        CollectKinds(host.Manifest.Roots, kinds);
        foreach ((string id, string kind) in ExpectedWidgets)
        {
            Assert(kinds.Contains(kind), "manifest contains kind " + kind + " (widget " + id + ")");
        }

        Assert(kinds.Contains("chrome/banner"), "core scope fallback resolved chrome/banner for the US scope");
        Assert(UiWidgetRegistry.KnownKinds(ExpectedSource).Count >= 15, "US scope registry holds the kernel composite kinds");
    }

    private static void CollectKinds(IReadOnlyList<FerriteLib.UiKit.UiElementSpec> elements, HashSet<string> kinds)
    {
        foreach (FerriteLib.UiKit.UiElementSpec element in elements)
        {
            kinds.Add(element.Kind);
            CollectKinds(element.Children, kinds);
        }
    }

    private static void RealHostReopenIsolation()
    {
        var fake = new RecordingSettingsSource();
        using UiHost first = UsKernelSettingsHost.Create(fake);
        using UiHost second = UsKernelSettingsHost.Create(fake);

        Assert(!ReferenceEquals(first.Session, second.Session),
            "each Host owns its own session; reopening must not share session state");
        Assert(first.Session.IsActive && second.Session.IsActive, "both sessions are active while both hosts are open");

        first.Dispose();
        Assert(!first.Session.IsActive, "closing the host disposes exactly its session");
        Assert(second.Session.IsActive, "closing one host must not touch the other host's session");
    }

    private static void UnknownKindFailsAtCreation()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiWidgetRegistry.InitializeCore();
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + ExpectedSource + "\">"
            + "<Widget Id=\"x\" Kind=\"us/no-such-kind\" />"
            + "</UiPage>");

        AssertThrows<UiContractException>(
            () => new UiHost(ExpectedSource, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation()),
            "unknown US kind must fail at Host creation");
    }

    private static void UnknownAttributeFailsAtCreation()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiWidgetRegistry.InitializeCore();
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + ExpectedSource + "\">"
            + "<Widget Id=\"x\" Kind=\"us/page-title\" Bogus=\"1\" />"
            + "</UiPage>");

        AssertThrows<UiContractException>(
            () => new UiHost(ExpectedSource, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation()),
            "unknown widget attribute must fail at Host creation");
    }

    private static void TypedBindingsRouteToBusinessBoundary()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);
        IUiBindings bindings = host.Bindings;

        // Creation-time contract: every typed binding/action key the real widgets consume must
        // exist with the exact registered type. Any mismatch fails here.
        bindings.ValidateValue<SqueakVoicePackMode>("mode", "test");
        bindings.ValidateValue<float>("global-volume", "test");
        bindings.ValidateValue<string>("distance-preset", "test");
        bindings.ValidateValue<bool>("allow-eggs", "test");
        bindings.ValidateValue<bool>("scale-cooldown", "test");
        bindings.ValidateValue<bool>("scale-talking", "test");
        bindings.ValidateValue<bool>("scale-population", "test");
        bindings.ValidateValue<bool>("camera-indicator", "test");
        bindings.ValidateValue<int>("tuning-layer", "test");
        bindings.ValidateValue<IReadOnlyList<ActionScopeRowView>>("action-scopes", "test");
        bindings.ValidateValue<IReadOnlyList<MoodTuningRowView>>("mood-rows", "test");
        bindings.ValidateValue<IReadOnlyList<BaselinePresetView>>("baseline-presets", "test");
        bindings.ValidateValue<IReadOnlyList<RaceLayerRowView>>("races", "test");
        bindings.ValidateValue<IReadOnlyList<VoicePackDomainView>>("xenotype-domains", "test");
        bindings.ValidateValue<VoicePackDomainView?>("selected-domain", "test");
        bindings.ValidateValue<UiDomainFilter>("domain-filter", "test");
        bindings.ValidateOptions<string>("race-filter-options", "test");
        bindings.ValidateOptions<string>("xenotype-filter-options", "test");
        bindings.ValidateOptions<string>("author-options", "test");
        bindings.ValidateAction<UsDomainFilterWrite>("set-domain-filter", "test");
        bindings.ValidateAction<string>("clear-pack-filters", "test");
        bindings.ValidateAction<UsScopeWrite>("set-action-scope", "test");
        bindings.ValidateAction<UsMoodWrite>("set-mood-tuning", "test");
        bindings.ValidateAction<UsBaselineRaceToggle>("toggle-baseline-race", "test");
        bindings.ValidateAction<UsBaselineXenoToggle>("toggle-baseline-xenotype", "test");
        bindings.ValidateAction<UsDomainSelection>("select-domain", "test");
        bindings.ValidateAction<UsPackToggle>("toggle-pack", "test");
        bindings.ValidateAction<UsDomainIdentity>("forget-unavailable", "test");
        bindings.ValidateAction<UiChartPointChange>("attenuation-point", "test");

        // Typed writes must reach the business boundary (the recording source) without any string
        // command bridge.
        bindings.Set("global-volume", 0.5f);
        Assert(fake.LastGlobalVolume == 0.5f, "global-volume value write routes to the business setter");

        bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(fake.LastMode == SqueakVoicePackMode.Remix, "mode value write routes to the business setter");

        bindings.Invoke("set-tab", "Packs");
        Assert(fake.LastActiveTab == "Packs", "set-tab action routes to the business surface");

        bindings.Invoke("set-action-scope", new UsScopeWrite("Eat", SqueakActionScope.ActiveCommand));
        Assert(fake.LastActionKey == "Eat" && fake.LastActionScope == SqueakActionScope.ActiveCommand,
            "set-action-scope typed action routes to the business surface");

        bindings.Invoke("set-mood-tuning", new UsMoodWrite(SqueakMood.Neutral, SqueakMoodFactor.Pitch, 1.25f));
        Assert(fake.LastMood == SqueakMood.Neutral
            && fake.LastMoodFactor == SqueakMoodFactor.Pitch
            && fake.LastMoodValue == 1.25f,
            "set-mood-tuning typed action routes to the business surface");

        bindings.Invoke("toggle-pack", new UsPackToggle(SqueakVoicePackScope.Race, "human", "", "us.human", true));
        Assert(fake.LastPackScope == SqueakVoicePackScope.Race
            && fake.LastPackRace == "human"
            && fake.LastPackKey == "us.human"
            && fake.LastPackEnabled == true,
            "toggle-pack typed action routes to the business surface");

        bindings.Invoke("forget-unavailable", new UsDomainIdentity(SqueakVoicePackScope.Xenotype, "human", "sanguophage"));
        Assert(fake.LastForgetScope == SqueakVoicePackScope.Xenotype && fake.LastForgetTarget == "sanguophage",
            "forget-unavailable typed action routes to the business surface");

        bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.ConflictOnly, true));
        Assert(fake.LastDomainFilterKind == SqueakDomainFilterKind.ConflictOnly && fake.LastDomainFilterFlag == true,
            "set-domain-filter typed action routes to the business surface");

        bindings.Set("search-text", "sang");
        Assert(fake.LastSearchText == "sang", "search-text value write routes to the business setter");

        int clearRevision = host.Session.ContentRevision;
        bindings.Invoke("clear-pack-filters", "");
        Assert(fake.LastDomainFilterFlag == false
            && fake.LastRaceFilter == ""
            && fake.LastXenotypeFilter == ""
            && fake.LastPackFilter == ""
            && fake.LastSearchText == "",
            "clear-pack-filters clears every filter represented by the UI");
        Assert(host.Session.ContentRevision == clearRevision + 1,
            "clear-pack-filters invalidates the layout once after the batch");

        // Options bindings are read-only projections; they must exist and be typed.
        Assert(bindings.GetOptions<string>("author-options").Count == 0, "author-options reads as typed string list");

        // Layout-affecting actions bump the session content revision through the Host boundary.
        int revision = host.Session.ContentRevision;
        bindings.Invoke("scroll-to", "global-volume");
        Assert(host.Session.ContentRevision == revision + 1, "scroll-to bumps the session content revision");
        Assert(host.Session.ScrollTargetElementId == "global-volume",
            "scroll-to forwards the target into the session scroll-target state");
    }

    private static void ThreeViewportMeasureAndDraw()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);

        // The maintainer-specified safe-area sizes (window ≈ 60-75% of the three reference
        // resolutions, floored at 800x600).
        var viewports = new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) };
        foreach (Vector2 viewport in viewports)
        {
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(viewport);
            Assert(snapshot.Viewports.ContainsKey("content-scroll"), "content scroll viewport present at " + viewport);
            Assert(snapshot.Viewports.ContainsKey("help-scroll"), "help scroll viewport present at " + viewport);
            Assert(snapshot.RectById.ContainsKey("footer"), "footer rect present at " + viewport);

            Rect contentScroll = snapshot.Viewports["content-scroll"];
            Assert(contentScroll.width > 100f && contentScroll.height > 100f,
                "content scroll viewport is usable at " + viewport + " (got " + contentScroll + ")");

            Rect navColumn = snapshot.RectById["nav"];
            Assert(Math.Abs(navColumn.width - 192f) < 0.5f, "nav column keeps its declared 192 width at " + viewport);
            Assert(snapshot.ScrollContents.ContainsKey("content-scroll"), "content scroll content rect present at " + viewport);

            Rect footer = snapshot.RectById["footer"];
            Assert(footer.height == 28f, "footer keeps its declared 28 height at " + viewport);
            Assert(footer.yMax <= viewport.y + 0.5f, "footer stays inside the viewport at " + viewport);

            // Overview is the default workspace: behavior controls are visible while every other
            // player task is gated out.
            foreach (string overviewId in new[] { "mode-row", "global-volume", "basic-tuning", "camera-indicator" })
            {
                Assert(snapshot.RectById.ContainsKey(overviewId), "Overview section " + overviewId + " visible at " + viewport);
            }

            Assert(!snapshot.RectById.ContainsKey("attenuation-editor"), "Distance workspace hidden by default at " + viewport);
            Assert(!snapshot.RectById.ContainsKey("scope-tree"), "Tuning workspace hidden by default at " + viewport);
            Assert(!snapshot.RectById.ContainsKey("checklist"), "Packs workspace hidden by default at " + viewport);
            Assert(!snapshot.RectById.ContainsKey("preset-list"), "Presets workspace hidden by default at " + viewport);

            Rect contentArea = snapshot.ScrollContents["content-scroll"];
            Assert(contentArea.width <= contentScroll.width + 0.5f,
                "Overview content never creates horizontal overflow at " + viewport);

            // One complete synchronous frame on the real tree at each viewport; native scopes must
            // all close (no leaked BeginScrollView/BeginGroup).
            host.DrawFrame(new Rect(0f, 0f, viewport.x, viewport.y));
            Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope after DrawFrame at " + viewport);
            Assert(StubGroupDepth() == 0, "no leaked GUI group scope after DrawFrame at " + viewport);
            Assert(host.Session.IsActive, "session stays active after a complete frame at " + viewport);
        }
    }

    private static void FullTypedWriteCoverage()
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake);
        IUiBindings bindings = host.Bindings;

        // Every typed write the real widgets emit must reach the business boundary.
        bindings.Set("mode", SqueakVoicePackMode.Fallback);
        Assert(fake.LastMode == SqueakVoicePackMode.Fallback, "mode value write routes");
        bindings.Set("global-volume", 0.25f);
        Assert(Math.Abs(fake.LastGlobalVolume.GetValueOrDefault() - 0.25f) < 0.001f, "global-volume value write routes");
        bindings.Set("allow-eggs", true);
        Assert(fake.LastEasterEggs == true, "allow-eggs value write routes");
        bindings.Set("scale-cooldown", false);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleCooldown && fake.LastBasicToggleValue == false, "scale-cooldown value write routes");
        bindings.Set("scale-talking", true);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleTalking && fake.LastBasicToggleValue == true, "scale-talking value write routes");
        bindings.Set("scale-population", false);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScalePopulation && fake.LastBasicToggleValue == false, "scale-population value write routes");
        bindings.Set("camera-indicator", true);
        Assert(fake.LastCameraIndicator == true, "camera-indicator value write routes");
        bindings.Invoke("toggle-egg", false);
        Assert(fake.LastEasterEggs == false, "toggle-egg action routes");
        bindings.Invoke("toggle-scale-cooldown", true);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleCooldown, "toggle-scale-cooldown action routes");
        bindings.Invoke("toggle-scale-talking", false);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScaleTalking, "toggle-scale-talking action routes");
        bindings.Invoke("toggle-scale-population", true);
        Assert(fake.LastBasicToggle == SqueakBasicToggle.ScalePopulation, "toggle-scale-population action routes");
        bindings.Invoke("toggle-camera-indicator", false);
        Assert(fake.LastCameraIndicator == false, "toggle-camera-indicator action routes");
        bindings.Invoke("set-distance-preset", SqueakDistancePreset.Conservative);
        Assert(fake.LastDistancePreset == SqueakDistancePreset.Conservative, "set-distance-preset action routes");
        bindings.Invoke("set-action-scope", new UsScopeWrite("Work", null));
        Assert(fake.LastActionKey == "Work" && fake.LastActionScope == null, "set-action-scope null-clear routes");
        bindings.Invoke("set-mood-tuning", new UsMoodWrite(SqueakMood.Bad, SqueakMoodFactor.Volume, 0.8f));
        Assert(fake.LastMood == SqueakMood.Bad
            && fake.LastMoodFactor == SqueakMoodFactor.Volume
            && Math.Abs(fake.LastMoodValue.GetValueOrDefault() - 0.8f) < 0.001f,
            "set-mood-tuning action routes");
        bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.OrphanOnly, true));
        Assert(fake.LastDomainFilterKind == SqueakDomainFilterKind.OrphanOnly && fake.LastDomainFilterFlag == true, "set-domain-filter action routes");
        bindings.Invoke("set-help-hover", "us/global-volume");
        Assert(fake.LastHelpHover == "us/global-volume", "set-help-hover action routes");
        bindings.Invoke("set-help-selection", "us/global-volume");
        Assert(fake.LastHelpSelection == "us/global-volume", "set-help-selection action routes");
        bindings.Invoke("scroll-to", "preset-list");
        Assert(fake.LastScrollToSection == "preset-list", "scroll-to action routes");

        // Attenuation chart drag: a normalized X on the second point writes the min distance.
        bindings.Invoke("attenuation-point", new UiChartPointChange(1, 0.25f, 0f));
        Assert(fake.LastDistanceRangeStart.HasValue, "attenuation-point action routes to SetDistanceRange");
    }

    private static void FiveWorkspacesAcrossViewports()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);

        var viewports = new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) };
        (string Tab, string[] Visible, string[] Hidden)[] tabs =
        {
            ("Overview", new[] { "mode-row", "global-volume", "basic-tuning", "camera-indicator" },
                new[] { "attenuation-editor", "scope-tree", "preset-list", "filter-bar", "checklist" }),
            ("Distance", new[] { "attenuation-editor" },
                new[] { "mode-row", "scope-tree", "preset-list", "filter-bar", "checklist" }),
            ("Packs", new[] { "filter-bar", "race-layer", "xenotype-layer", "checklist" },
                new[] { "mode-row", "attenuation-editor", "scope-tree", "preset-list" }),
            ("Tuning", new[] { "scope-tree" },
                new[] { "mode-row", "attenuation-editor", "preset-list", "checklist" }),
            ("Presets", new[] { "preset-list" },
                new[] { "mode-row", "attenuation-editor", "scope-tree", "checklist" })
        };

        foreach ((string tab, string[] visible, string[] hidden) in tabs)
        {
            int before = host.Session.ContentRevision;
            host.Bindings.Invoke("set-tab", tab);
            Assert(fake.LastActiveTab == tab, "set-tab routes to the business surface");
            Assert(host.Session.ContentRevision == before + 1,
                "workspace switch bumps the session content revision (layout cache invalidation)");

            foreach (Vector2 viewport in viewports)
            {
                UiLayoutSnapshot snapshot = host.MeasureAndArrange(viewport);
                foreach (string id in visible)
                {
                    Assert(snapshot.RectById.ContainsKey(id), tab + " section " + id + " visible at " + viewport);
                }
                foreach (string id in hidden)
                {
                    Assert(!snapshot.RectById.ContainsKey(id), "non-" + tab + " section " + id + " hidden at " + viewport);
                }

                host.DrawFrame(new Rect(0f, 0f, viewport.x, viewport.y));
                Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope at workspace " + tab + " viewport " + viewport);
                Assert(StubGroupDepth() == 0, "no leaked GUI group scope at workspace " + tab + " viewport " + viewport);
                Assert(host.Session.IsActive, "session active at workspace " + tab + " viewport " + viewport);
            }
        }
    }

    private static void WorkspaceSwitchResetsSessionScroll()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);

        host.Session.SetScrollPosition("content-scroll", new Vector2(50f, 120f));
        host.Session.SetScrollPosition("help-scroll", new Vector2(0f, 40f));
        host.Bindings.Invoke("set-tab", "Packs");

        Vector2 content = host.Session.GetScrollPosition("content-scroll");
        Vector2 help = host.Session.GetScrollPosition("help-scroll");
        Assert(content.x == 0f && content.y == 0f,
            "workspace switch resets the centre content scroll to top");
        Assert(help.x == 0f && help.y == 0f,
            "workspace switch resets the right help scroll to top");
    }

    private static void RichDynamicDataMeasureAndDraw()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        IUiBindings bindings = host.Bindings;

        // Filter/options/dynamic-list bindings read the rich projections.
        Assert(bindings.GetOptions<string>("author-options").Count == 2, "author-options reads the rich author list");
        Assert(bindings.GetOptions<string>("race-filter-options").Count == 2, "race-filter-options reads the rich list");
        Assert(bindings.GetOptions<string>("xenotype-filter-options").Count == 2, "xenotype-filter-options reads the rich list");
        Assert(bindings.Get<IReadOnlyList<RaceLayerRowView>>("races").Count == 2, "races reads the rich race list");
        Assert(bindings.Get<IReadOnlyList<BaselinePresetView>>("baseline-presets").Count == 1, "baseline-presets reads the rich preset list");
        Assert(bindings.Get<IReadOnlyList<VoicePackDomainView>>("xenotype-domains").Count == 1, "xenotype-domains reads the rich list");
        Assert(bindings.Get<VoicePackDomainView?>("selected-domain").HasValue, "selected-domain reads the rich selected domain");
        Assert(bindings.Get<IReadOnlyList<ActionScopeRowView>>("action-scopes").Count == 2, "action-scopes reads the rich list");
        Assert(bindings.Get<IReadOnlyList<MoodTuningRowView>>("mood-rows").Count == 2, "mood-rows reads the rich list");
        Assert(bindings.Get<IReadOnlyList<TuningDomainOptionView>>("tuning-domains").Count == 2, "tuning-domains reads the rich list");

        // Full dynamic draw: every workspace with non-empty lists at the reference viewports.
        foreach (string tab in new[] { "Overview", "Distance", "Packs", "Tuning", "Presets" })
        {
            host.Bindings.Invoke("set-tab", tab);
            foreach (Vector2 viewport in new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
            {
                host.DrawFrame(new Rect(0f, 0f, viewport.x, viewport.y));
                Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope with rich data at " + tab + " " + viewport);
                Assert(StubGroupDepth() == 0, "no leaked GUI group scope with rich data at " + tab + " " + viewport);
            }
        }

        // Filter/search value writes route to the boundary and bump the layout revision.
        int before = host.Session.ContentRevision;
        bindings.Set("race-filter", "human");
        Assert(fake.LastRaceFilter == "human", "race-filter value write routes to the business setter");
        Assert(host.Session.ContentRevision == before + 1, "race-filter write bumps the layout revision");
        bindings.Set("xenotype-filter", "sanguophage");
        Assert(fake.LastXenotypeFilter == "sanguophage", "xenotype-filter value write routes");
        bindings.Set("pack-filter", "AuthorA");
        Assert(fake.LastPackFilter == "AuthorA", "pack-filter value write routes");
        bindings.Set("search-text", "sang");
        Assert(fake.LastSearchText == "sang", "search-text value write routes");
        bindings.Invoke("set-pack-filter", "AuthorB");
        Assert(fake.LastPackFilter == "AuthorB", "set-pack-filter action routes");

        // Dynamic list interactions: domain selection + checklist toggle + forget + baseline.
        bindings.Invoke("select-domain", new UsDomainSelection(SqueakVoicePackScope.Xenotype, "human", "sanguophage"));
        Assert(fake.LastSelectedScope == SqueakVoicePackScope.Xenotype && fake.LastSelectedTarget == "sanguophage", "select-domain routes");
        bindings.Invoke("toggle-pack", new UsPackToggle(SqueakVoicePackScope.Xenotype, "human", "sanguophage", "us.sang2", true));
        Assert(fake.LastPackKey == "us.sang2" && fake.LastPackEnabled == true, "toggle-pack routes");
        bindings.Invoke("forget-unavailable", new UsDomainIdentity(SqueakVoicePackScope.Xenotype, "human", "sanguophage"));
        Assert(fake.LastForgetTarget == "sanguophage", "forget-unavailable routes");
        bindings.Invoke("toggle-baseline-preset", "us.preset1");
        Assert(fake.LastBaselinePresetToggle == "us.preset1", "toggle-baseline-preset routes");
        bindings.Invoke("toggle-baseline-race", new UsBaselineRaceToggle("us.preset1", "human", true));
        Assert(fake.LastBaselineRace == "human" && fake.LastBaselineRaceSelected == true, "toggle-baseline-race routes");
        bindings.Invoke("toggle-baseline-xenotype", new UsBaselineXenoToggle("us.preset1", "human", "sanguophage", true));
        Assert(fake.LastBaselineXeno == "sanguophage", "toggle-baseline-xenotype routes");
        bindings.Invoke("import-baseline", "us.preset1");
        Assert(fake.LastBaselineImport == "us.preset1", "import-baseline routes");
        bindings.Invoke("set-tuning-layer", 2);
        Assert(fake.LastTuningLayer == 2, "set-tuning-layer routes");
        bindings.Invoke("set-tuning-domain", new UsTuningDomainSelection("human", "sanguophage"));
        Assert(fake.LastTuningDomainRace == "human" && fake.LastTuningDomainTarget == "sanguophage", "set-tuning-domain routes");
    }

    private static void SessionPopupIsolationAndCleanup()
    {
        var fake = new RecordingSettingsSource();
        using UiHost a = UsKernelSettingsHost.Create(fake);
        using UiHost b = UsKernelSettingsHost.Create(fake);

        a.Session.OpenPopup("a-popup", new Rect(10f, 10f, 120f, 100f));
        Assert(a.Session.IsPopupOpen("a-popup"), "session A owns its popup");
        Assert(!b.Session.IsPopupOpen("a-popup"), "session B never sees session A's popup");
        a.Session.RegisterPopupDraw(() => { });
        Assert(a.Session.PopupDrawActions.Count == 1, "popup draw action registered on A");
        Assert(b.Session.PopupDrawActions.Count == 0, "session B has no popup draw actions");

        a.DrawFrame(new Rect(0f, 0f, 800f, 600f));
        Assert(a.Session.PopupDrawActions.Count == 0, "popup draw actions are consumed at EndFrame");
        Assert(a.Session.IsPopupOpen("a-popup"), "popup ownership survives the frame");

        a.Session.ClosePopup();
        Assert(!a.Session.IsPopupOpen("a-popup"), "ClosePopup releases the popup");

        // Dispose clears popup + hot-control state for exactly this session.
        a.Session.OpenPopup("a-popup", new Rect(0f, 0f, 10f, 10f));
        a.Session.CaptureHotControl(4242);
        Assert(a.Session.IsHotControlOwned(4242), "session A owns the captured hot control");
        a.Dispose();
        Assert(!a.Session.IsActive, "dispose deactivates session A");
        Assert(!a.Session.IsPopupOpen("a-popup"), "dispose clears popup state");
        Assert(!a.Session.IsHotControlOwned(4242), "dispose releases exactly its hot control");
        Assert(b.Session.IsActive, "session B untouched by A's dispose");
    }

    private static void OverlayHostCreatedWithoutSettingsWindow()
    {
        // Real embedded overlay Schema=2 resource present in the production assembly.
        Assembly production = typeof(UsKernelOverlayHost).Assembly;
        string[] names = production.GetManifestResourceNames();
        Assert(Array.Exists(names, name => name == "UniversalSqueaker.UI.Layout.Overlay.Schema2.xml"),
            "production assembly must embed UniversalSqueaker.UI.Layout.Overlay.Schema2.xml");

        var source = new RecordingOverlaySource();
        using UiHost host = UsKernelOverlayHost.Create(source);

        Assert(host.Source == ExpectedSource, "overlay host source matches the resource scope");
        Assert(host.Manifest.SchemaVersion == "2", "overlay manifest is schema 2");
        Assert(host.Session.IsActive, "overlay session active after creation");
        Assert(host.Bindings.Get<string>("camera-readout") == source.Text,
            "camera-readout typed binding reads the overlay source");
        Assert(UiWidgetRegistry.KnownKinds(ExpectedSource).Contains("us/camera-readout"),
            "the shared US-scope registry holds the overlay readout kind");

        // The overlay host is fully independent: no settings host/source/window involved. Draw at
        // the three reference resolutions; the layout must not throw and native scopes close.
        foreach (Vector2 viewport in new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
        {
            host.DrawFrame(new Rect(0f, 0f, viewport.x, viewport.y));
            Assert(StubScrollDepth() == 0, "no leaked Verse scroll scope after overlay DrawFrame at " + viewport);
            Assert(StubGroupDepth() == 0, "no leaked GUI group scope after overlay DrawFrame at " + viewport);
        }

        Assert(host.Session.IsActive, "overlay session stays active after frames");
    }

    private static void OverlayWidgetContractFailsAtCreation()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiWidgetRegistry.InitializeCore();
        string xml;
        using (Stream? stream = typeof(UsKernelOverlayHost).Assembly.GetManifestResourceStream("UniversalSqueaker.UI.Layout.Overlay.Schema2.xml"))
        {
            Assert(stream != null, "overlay Schema2 resource stream must be readable");
            using var reader = new StreamReader(stream!);
            xml = reader.ReadToEnd();
        }

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        // Missing the required typed binding => the real widget's Validate must fail at creation.
        AssertThrows<UiContractException>(
            () => new UiHost(ExpectedSource, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation()),
            "missing camera-readout binding must fail at overlay Host creation");
    }

    private static void OverlayDualHostSessionIsolation()
    {
        var settingsFake = new RecordingSettingsSource();
        var overlayFake = new RecordingOverlaySource();
        using UiHost settingsHost = UsKernelSettingsHost.Create(settingsFake);
        using UiHost overlayA = UsKernelOverlayHost.Create(overlayFake);
        using UiHost overlayB = UsKernelOverlayHost.Create(overlayFake);

        overlayA.Dispose();
        Assert(!overlayA.Session.IsActive, "disposing overlay A deactivates exactly A");
        Assert(overlayB.Session.IsActive, "overlay B session untouched by A's dispose");
        Assert(settingsHost.Session.IsActive, "settings session untouched by overlay dispose");

        settingsHost.Dispose();
        Assert(!settingsHost.Session.IsActive, "settings dispose deactivates the settings session");
        Assert(overlayB.Session.IsActive, "overlay B survives the settings window close");
    }

    private static void OverlaySafeAreaThreeViewports()
    {
        // Simulated date-bar rects at the three reference resolutions (bottom-left, above the date
        // bar — the position already proven by the legacy patch; no invented offsets).
        var source = new RecordingOverlaySource();
        using var controller = new UsKernelOverlayController(source);

        foreach (Vector2 screen in new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
        {
            float leftX = 16f;
            float width = screen.x * 0.35f;
            float curBaseY = screen.y - 34f;
            Assert(controller.TryDraw(leftX, width, ref curBaseY), "overlay draws at " + screen);
            Assert(controller.IsActive, "overlay host active at " + screen);
            Assert(curBaseY >= 0f && curBaseY + 26f <= screen.y + 0.5f,
                "overlay row stays inside the safe area at " + screen + " (y=" + curBaseY + ")");
            Assert(StubScrollDepth() == 0 && StubGroupDepth() == 0, "no leaked scopes at " + screen);
        }

        // Resolution change: a wider rect re-measures (engine cache keyed by size); the session stays.
        float y = 1080f - 34f;
        Assert(controller.TryDraw(20f, 1920f * 0.5f, ref y), "overlay re-draws after a resolution-width change");
        Assert(controller.IsActive, "overlay stays active across the size change");
        Assert(y >= 0f && y + 26f <= 1080f + 0.5f, "overlay stays inside the safe area after the size change");
    }

    private static void OverlayShowHideDisposeReopen()
    {
        var source = new RecordingOverlaySource();
        var controller = new UsKernelOverlayController(source);
        Assert(!controller.IsActive, "overlay starts inactive");

        // Show: the first enabled frame creates the host.
        float y = 600f - 34f;
        Assert(controller.TryDraw(16f, 200f, ref y), "enabled overlay draws");
        Assert(controller.IsActive, "overlay active after the first enabled frame");

        // Hide: disable -> Maintain disposes the host; no draw while disabled.
        source.Enabled = false;
        controller.Maintain();
        Assert(!controller.IsActive, "overlay disposed when the toggle is off");
        Assert(!controller.TryDraw(16f, 200f, ref y), "overlay refuses to draw while disabled");

        // Reopen: a fresh host and a fresh session.
        source.Enabled = true;
        Assert(controller.TryDraw(16f, 200f, ref y), "overlay draws again after re-enable");
        Assert(controller.IsActive, "overlay active after re-enable");

        // Dispose: full cleanup; a disposed controller never draws.
        controller.Dispose();
        Assert(!controller.IsActive, "overlay disposed by controller.Dispose");
        Assert(!controller.TryDraw(16f, 200f, ref y), "disposed controller never draws");
    }

    private static void OverlayNoMapSafeExit()
    {
        var source = new RecordingOverlaySource { MapPresent = false };
        var controller = new UsKernelOverlayController(source);
        float y = 600f - 34f;
        Assert(!controller.TryDraw(16f, 200f, ref y), "no-map overlay does not draw");
        Assert(!controller.IsActive, "no-map overlay never activates");
        Assert(StubScrollDepth() == 0 && StubGroupDepth() == 0, "no scopes leaked by the no-map path");

        // Map present then gone mid-session: teardown without exception.
        source.MapPresent = true;
        Assert(controller.TryDraw(16f, 200f, ref y), "overlay draws while the map is present");
        source.MapPresent = false;
        controller.Maintain();
        Assert(!controller.IsActive, "map-gone tears the overlay session down");
        Assert(!controller.TryDraw(16f, 200f, ref y), "map-gone overlay does not draw");
    }

    private static void OverlayDrawFailureDoesNotDoubleReserveRow()
    {
        // Regression: when the kernel overlay DrawFrame throws, TryDraw must not commit the row
        // cursor. The caller then draws the legacy pure-Verse readout at the same curBaseY; if the
        // kernel path had already decremented it, the fallback would double-reserve the row height.
        const string throwKind = "test/throw-overlay-draw";
        UiWidgetRegistry.Register(
            ExpectedSource,
            throwKind,
            () => new ThrowingDrawWidget(),
            new[] { "Id", "Kind", "Hidden", "Tab" });

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + ExpectedSource + "\">"
            + "<Widget Id=\"x\" Kind=\"" + throwKind + "\" />"
            + "</UiPage>");
        using UiHost throwingHost = new(
            ExpectedSource,
            manifest,
            new UiBindings(),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        var source = new RecordingOverlaySource();
        var controller = new UsKernelOverlayController(source);
        FieldInfo? hostField = typeof(UsKernelOverlayController).GetField("host", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(hostField != null, "overlay controller exposes a host field for failure injection");
        hostField!.SetValue(controller, throwingHost);

        float y = 600f - 34f;
        float before = y;
        Assert(!controller.TryDraw(16f, 200f, ref y), "overlay draw failure returns false");
        Assert(Math.Abs(y - before) < 0.001f,
            "failed overlay draw leaves curBaseY untouched so the legacy fallback does not double-reserve the row");
        Assert(controller.CreationFailed, "draw failure trips the permanent fallback flag");
        Assert(!controller.IsActive, "draw failure disposes the overlay host");
        Assert(StubScrollDepth() == 0 && StubGroupDepth() == 0, "no scopes leaked by the failure path");
    }

    private static void ClosingSettingsWindowDoesNotAffectOverlay()
    {
        var settingsFake = new RecordingSettingsSource();
        UiHost settingsHost = UsKernelSettingsHost.Create(settingsFake);
        settingsHost.DrawFrame(new Rect(0f, 0f, 800f, 600f)); // a live settings frame
        settingsHost.Dispose(); // window close

        var source = new RecordingOverlaySource();
        var controller = new UsKernelOverlayController(source);
        float y = 720f - 34f;
        Assert(controller.TryDraw(16f, 260f, ref y), "overlay draws after the settings window closed");
        Assert(controller.IsActive, "overlay session independent of the settings window");
    }

    private static void DisposedHostCannotDraw()
    {
        var fake = new RecordingSettingsSource();
        UiHost host = UsKernelSettingsHost.Create(fake);
        host.Dispose();
        Assert(!host.Session.IsActive, "disposed host session is inactive");
        AssertThrows<InvalidOperationException>(
            () => host.DrawFrame(new Rect(0f, 0f, 800f, 600f)),
            "a disposed session must refuse DrawFrame (close-path contract)");
    }

    private static int StubScrollDepth()
    {
        FieldInfo? field = typeof(Widgets).GetField("ScrollViewDepth", BindingFlags.Public | BindingFlags.Static);
        return field != null ? (int)(field.GetValue(null) ?? 0) : -1;
    }

    private static int StubGroupDepth()
    {
        FieldInfo? field = typeof(GUI).GetField("GroupDepth", BindingFlags.Public | BindingFlags.Static);
        return field != null ? (int)(field.GetValue(null) ?? 0) : -1;
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException("Expected " + typeof(TException).Name + ": " + message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ThrowingDrawWidget : IUiWidget
    {
        public string Kind => "test/throw-overlay-draw";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 26f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            throw new InvalidOperationException("injected overlay draw failure");
        }
    }

    private sealed class StubMetrics : FerriteLib.UiKit.ITextMetrics
    {
        public float MeasureText(string text, FerriteLib.UiKit.UiFont font, float width)
        {
            return 16f;
        }
    }

    private sealed class StubTranslation : FerriteLib.UiKit.Kernel.IUiTranslation
    {
        public string Translate(string key)
        {
            return key ?? "";
        }
    }
}
