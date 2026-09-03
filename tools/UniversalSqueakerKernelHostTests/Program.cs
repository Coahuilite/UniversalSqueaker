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
        Step("settings window uses the pageUnavailable failure model", SettingsWindowPageUnavailableModel);
        Step("overlay host without settings window", OverlayHostCreatedWithoutSettingsWindow);
        Step("overlay widget contract fails at creation", OverlayWidgetContractFailsAtCreation);
        Step("overlay dual-host session isolation", OverlayDualHostSessionIsolation);
        Step("text-fit audit against both shipped language tables", TextFitAuditAcrossLanguages);
        Step("wrapping Packs layer text grows both layer cards", WrappingDomainTextGrowsLayerRows);
        Step("composite dropdown popup publishes its covering rect", CompositeDropdownPublishesCoveringRect);
        Step("prerequisite range tracks the compiled FerriteLib Api", PrerequisiteRangeTracksCompiledApi);
        Step("overlay show/hide/dispose/reopen", OverlayShowHideDisposeReopen);
        Step("overlay no-map safe exit", OverlayNoMapSafeExit);
        Step("overlay draw failure does not double-reserve the row cursor", OverlayDrawFailureDoesNotDoubleReserveRow);
        Step("closing settings window does not affect overlay", ClosingSettingsWindowDoesNotAffectOverlay);
    }

    /// <summary>
    /// The in-game failure reported on 2026-09-04: picking Auto in a Tuning scope dropdown opened the
    /// next row's dropdown instead of selecting it. The yield guard in UiNative consults
    /// UiSession.OpenPopupRect, and the composite popup path (UsKernelDraw.Dropdown: scope rows,
    /// domain picker, filter dropdowns) never published that rect, so every covered trigger kept
    /// stealing the click. The library lanes prove the guard and the rect rule given a published
    /// rect; this lane proves the composite path publishes one, through the real production host.
    /// </summary>
    private static void CompositeDropdownPublishesCoveringRect()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        host.Bindings.Invoke("set-tab", "Tuning");
        Rect viewport = new(0f, 0f, 800f, 600f);
        host.DrawFrame(viewport);

        // Anchor low enough that two or more option rows cannot fit below it: the composite popup
        // must flip above the trigger, the same branch the reported click loss exercised.
        Rect anchor = new(300f, 560f, 120f, 22f);
        string? publishedBy = null;
        Rect popup = default;
        foreach (string key in UniversalSqueaker.Kernel.BuiltInActionKeys.All)
        {
            host.Session.OpenPopup("scope-tree-scope-" + key, anchor);
            host.DrawFrame(viewport);
            if (host.Session.OpenPopupRect.HasValue)
            {
                publishedBy = key;
                popup = host.Session.OpenPopupRect.Value;
                break;
            }

            host.Session.ClosePopup();
        }

        Assert(publishedBy != null,
            "no Tuning scope dropdown published its popup rect; composite popups can never win a covered click");
        Assert(popup.height >= 24f && Math.Abs(popup.height % 24f) < 0.01f,
            "composite popup height must be a whole number of option rows: " + popup.height);
        Assert(popup.y >= -0.01f && popup.yMax <= 600f + 0.01f,
            "composite popup must stay inside the host viewport: " + popup);
        Assert(Math.Abs(popup.yMax - anchor.y) < 0.01f,
            "a composite popup that cannot fit below its anchor must flip above it: " + popup);
        Assert(host.Session.IsPointOverPopup(new Vector2(popup.x + popup.width / 2f, popup.y + 12f)),
            "the yield predicate must see a point inside the composite popup");
        Assert(!host.Session.IsPointOverPopup(new Vector2(popup.x + popup.width / 2f, popup.yMax + 40f)),
            "the yield predicate must not fire below the composite popup");

        // The rect is per-frame state and a click arrives in the frame after the draw, so a second
        // frame must republish it; closing must clear it so a stale rect cannot shadow later clicks.
        host.DrawFrame(viewport);
        Assert(host.Session.OpenPopupRect.HasValue, "composite popup rect must be republished every frame it draws");
        host.Session.ClosePopup();
        host.DrawFrame(viewport);
        Assert(!host.Session.OpenPopupRect.HasValue, "a closed composite popup must leave no published rect");
    }

    /// <summary>
    /// Build-time lockstep pin: US compiles against the sibling carrier payload, so the range floor in
    /// Mod.cs must equal the Api of the library this harness just linked. If the library bumps its
    /// public surface, this step goes red until PrerequisiteApiMin/Max move and both mods ship
    /// together - the desync that produced the 2026-09-04 TypeLoadException cannot recur silently.
    /// </summary>
    private static void PrerequisiteRangeTracksCompiledApi()
    {
        Version compiledFloor = new Version(0, 2, 0);
        Assert(FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Equals(compiledFloor),
            "US is compiled against FerriteLib Api " + compiledFloor + " but the linked carrier reports "
            + FerriteLib.UiKit.Kernel.FerriteLibVersion.Api
            + "; move PrerequisiteApiMin/Max in Mod.cs and ship both mods in lockstep");
    }

    private static void SettingsWindowPageUnavailableModel()
    {
        // New failure semantics (replaces the deleted legacy-fallback-reachability tests):
        //   * the page model has no Execute/ExecuteAll command dispatcher any more — the typed
        //     facade is the ONLY write authority, so no generic command can smuggle a legacy
        //     page switch back in;
        //   * the settings source interface exposes no Execute* surface either;
        //   * after a whole-frame failure the window's own catch disposes the host, and the
        //     disposed-host contract below proves the kernel page can never be drawn again —
        //     the window therefore shows only the pageUnavailable notice (its source-level
        //     shape is pinned by the UiLogicTests failure-model invariant; the window derives
        //     from Verse.Window, which the stub runtime deliberately does not provide).
        var (types, names) = LoadProductionTypeNames();

        Type? model = FindLoadedType(types, "VoicePacksPageModel");
        Assert(model != null, "the page model must be loadable for this gate to be meaningful");

        Assert(model!.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static) == null,
            "VoicePacksPageModel.Execute (generic legacy command dispatcher) must be gone");
        Assert(model.GetMethod("ExecuteAll", BindingFlags.Public | BindingFlags.Static) == null,
            "VoicePacksPageModel.ExecuteAll must be gone");
        foreach (MethodInfo method in model.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            Assert(!method.Name.StartsWith("Execute", StringComparison.Ordinal),
                "no Execute* dispatcher may return on the page model: found " + method.Name);
        }

        string[] typedFacade =
        {
            "SetActiveTab", "ScrollToSection", "SetDomainFilter", "SetMoodTuning",
            "ToggleVoicePack", "ForgetUnavailable"
        };
        foreach (string facade in typedFacade)
        {
            Assert(model.GetMethod(facade, BindingFlags.Public | BindingFlags.Static) != null,
                "the typed facade entry '" + facade + "' must exist as the sole write authority");
        }

        Type? source = FindLoadedType(types, "IUsKernelSettingsSource");
        Assert(source != null, "the kernel settings source interface must be loadable");
        foreach (MethodInfo method in source!.GetMethods())
        {
            Assert(!method.Name.StartsWith("Execute", StringComparison.Ordinal),
                "IUsKernelSettingsSource must not grow an Execute* command surface: found " + method.Name);
        }

        // Terminal state proof against the REAL host: once a whole-frame failure disposes it,
        // every further draw attempt is refused — there is nothing left to draw except the
        // pageUnavailable notice, and no second page implementation exists to fall back to.
        var fake = new RecordingSettingsSource();
        UiHost host = UsKernelSettingsHost.Create(fake);
        host.Dispose();
        AssertThrows<InvalidOperationException>(
            () => host.DrawFrame(new Rect(0f, 0f, 900f, 700f)),
            "after the window's whole-frame failure catch disposed the host, no frame can ever render the page again");

        // Types deleted with the legacy UI chain, plus the caller-less preview surface that only
        // existed to serve the removed Dev audio browser. They must not reappear in the shipped
        // assembly by name in any namespace: a resurrection under a different folder would otherwise
        // pass a source-level check.
        string[] deletedTypes =
        {
            "FerriteVoicePacksPage", "VanillaVoicePacksPage", "VoicePacksPage",
            "UsWidgetRegistrar", "UsCommandPayload", "UsWidgetCommandAdapter",
            "UiCommand",
            "SqueakFinalPreviewStatus", "SqueakFinalPreviewResult", "SqueakSettingsGameContext"
        };
        foreach (string dead in deletedTypes)
        {
            Assert(!names.Contains(dead),
                "deleted fallback/preview type '" + dead + "' must not exist in the shipped assembly");
        }
    }

    private static (List<Type> Loaded, HashSet<string> AllNames) LoadProductionTypeNames()
    {
        Assembly production = typeof(UsKernelSettingsHost).Assembly;
        var loaded = new List<Type>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        Type[] candidates;
        try
        {
            candidates = production.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            candidates = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (Type type in candidates)
        {
            loaded.Add(type);
            names.Add(type.Name);
        }

        return (loaded, names);
    }

    private static Type? FindLoadedType(List<Type> types, string name)
    {
        foreach (Type type in types)
        {
            if (type.Name == name) return type;
        }

        return null;
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

    private static void CollectKinds(IReadOnlyList<FerriteLib.UiKit.Kernel.UiElementSpec> elements, HashSet<string> kinds)
    {
        foreach (FerriteLib.UiKit.Kernel.UiElementSpec element in elements)
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

    /// <summary>
    /// End-to-end text-fitting gate. Production widgets, the production Schema=2 host, and the two
    /// shipped Keyed tables are driven through the kernel text-fit audit at three window widths. Two
    /// things must hold: no single-line label overflows the rect it is given (in either language), and
    /// the audit demonstrably fires when a label does overflow — an assertion that can only pass because
    /// nothing is measured would be worthless, so the second half injects a deliberately impossible
    /// option name. Widths come from the Verse stub's half-width advance model (CJK = one em, Latin =
    /// half an em); absolute pixel truth remains an in-game property.
    /// </summary>
    private static void TextFitAuditAcrossLanguages()
    {
        Dictionary<string, string> english = ReadKeyedTable("English");
        Dictionary<string, string> chinese = ReadKeyedTable("ChineseSimplified");
        Assert(english.Count >= 100, "the shipped English Keyed table should be populated, got " + english.Count);
        string[] tabs = { "Overview", "Distance", "Packs", "Tuning", "Presets" };
        var viewports = new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) };
        var reports = new List<UiOverflowReport>();

        var metrics = new StubMetrics();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // Positive control, and the reason the zero-finding sweeps below mean anything: prove in this
            // process, through the same production drawing outlet, that a label which cannot fit is seen.
            // Without it, a silently disabled audit would pass every "no overflow" assertion forever.
            UiFitAudit.Reset();
            reports.Clear();
            UiFitAudit.BeginElement("probe/single-line");
            FerriteLib.UiKit.Kernel.UiThemeDraw.Label(
                new Rect(0f, 0f, 20f, 16f), "probe text", UiTheme.DarkGold, null, FerriteLib.UiKit.Kernel.UiFont.Tiny,
                TextAnchor.MiddleLeft, singleLine: true);
            UiFitAudit.EndElement();
            Assert(reports.Count == 1, "positive control failed: the audit saw nothing for a label that cannot fit (" + Describe(reports) + ")");
            CheckLanguageTable(reports, tabs, viewports, english, "english", metrics);
            CheckLanguageTable(reports, tabs, viewports, chinese, "chinese", metrics);

            // Failure sensitivity for the sweeping checks: one Keyed string is replaced with a value no
            // fixed column can hold, then the real page is drawn again. If the audit stays silent here,
            // every "no overflow" result above is meaningless — that is the exact failure this guards.
            string impossible = new string('\u6d4b', 30);
            var stretched = new Dictionary<string, string>(chinese, StringComparer.Ordinal)
            {
                ["US.Packs.Filter.Race"] = impossible
            };

            SetTranslatorResolver(stretched);
            UiFitAudit.Reset();
            reports.Clear();
            using (UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, metrics))
            {
                host.Bindings.Invoke("set-tab", "Packs");
                host.MeasureAndArrange(viewports[0]);
                host.DrawFrame(new Rect(0f, 0f, viewports[0].x, viewports[0].y));
            }

            bool caught = false;
            foreach (UiOverflowReport report in reports)
            {
                if (report.Axis == UiOverflowAxis.Width
                    && report.ElementPath.IndexOf("filter-bar", StringComparison.Ordinal) >= 0)
                {
                    caught = true;
                }
            }

            Assert(caught, "the audit must report a Keyed string too wide for its column, findings: " + Describe(reports));
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    /// <summary>
    /// Failure sensitivity for the Packs layer row heights. The bilingual fit sweep compares one label
    /// against its own band, so it cannot see a row that is too short for the content stacked inside it —
    /// exactly the bug that made the race and xenotype layers overdraw the sections below them. This step
    /// drives the real Host twice, once with text that fits one line and once with text that cannot, and
    /// requires both cards to grow. Under the old height formulas (title only, measured from the bare
    /// xenotype name) the two arrangements come out identical and this fails.
    /// </summary>
    private static void WrappingDomainTextGrowsLayerRows()
    {
        var metrics = new StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // The detail and title templates are Keyed: without a loaded table Translate returns the key
            // itself, the numeric arguments are dropped and nothing is long enough to wrap, which would
            // make this step pass without measuring anything.
            SetTranslatorResolver(ReadKeyedTable("English"));
            var narrow = new Vector2(800f, 600f);
            (float raceShort, float xenotypeShort) = LayerCardHeights(narrow, wrapping: false, metrics);
            (float raceLong, float xenotypeLong) = LayerCardHeights(narrow, wrapping: true, metrics);

            Assert(raceShort > 0f && xenotypeShort > 0f, "the rich fixture must place both layer cards");
            Assert(raceLong > raceShort + 10f,
                "a race detail line that cannot fit one line must grow the race-layer card: one-line "
                + raceShort + "px, wrapping " + raceLong + "px");
            Assert(xenotypeLong > xenotypeShort + 10f,
                "a composed xenotype title that cannot fit one line must grow the xenotype-layer card: one-line "
                + xenotypeShort + "px, wrapping " + xenotypeLong + "px");
            Assert(reports.Count == 0,
                "wrapping layer text must be measured into its band, not clipped: " + Describe(reports));
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    private static (float Race, float Xenotype) LayerCardHeights(Vector2 viewport, bool wrapping, StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true, WrappingDomainText = wrapping };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Packs");
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(viewport);
        float Race = snapshot.RectById.TryGetValue("race-layer", out Rect raceRect) ? raceRect.height : 0f;
        float Xenotype = snapshot.RectById.TryGetValue("xenotype-layer", out Rect xenoRect) ? xenoRect.height : 0f;
        return (Race, Xenotype);
    }

    private static void CheckLanguageTable(
        List<UiOverflowReport> reports,
        string[] tabs,
        Vector2[] viewports,
        Dictionary<string, string> table,
        string label,
        StubMetrics metrics)
    {
        SetTranslatorResolver(table);
        UiFitAudit.Reset();
        reports.Clear();

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        foreach (string tab in tabs)
        {
            host.Bindings.Invoke("set-tab", tab);
            foreach (Vector2 viewport in viewports)
            {
                host.MeasureAndArrange(viewport);
                host.DrawFrame(new Rect(0f, 0f, viewport.x, viewport.y));
            }
        }

        var findings = new List<string>();
        foreach (UiOverflowReport report in reports)
        {
            findings.Add(report.ElementPath + " " + report.Axis + " needs " + report.Needed + "px, has "
                + report.Available + "px at width " + report.RectWidth + "px, text=\"" + Short(report.Text) + "\"");
        }

        Assert(findings.Count == 0,
            label + ": every label on the shipped page must fit the rect it is given; offenders: "
            + string.Join(" | ", findings));
        Console.WriteLine("  ok: " + label + " table draws 5 workspaces x 3 viewports with no text overflow");
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        var parts = new List<string>();
        foreach (UiOverflowReport report in reports)
        {
            parts.Add(report.ElementPath + "/" + report.Axis + " " + report.Needed + ">" + report.Available);
        }

        return parts.Count == 0 ? "(none)" : string.Join(" | ", parts);
    }

    /// <summary>Trims an offender string so the harness message stays one readable line per finding.</summary>
    private static string Short(string? text)
    {
        string value = text ?? "";
        return value.Length <= 28 ? value : value.Substring(0, 28) + "…";
    }

    private static Dictionary<string, string> ReadKeyedTable(string languageFolder)
    {
        string path = System.IO.Path.Combine(
            RepoRoot(), "1.6", "Languages", languageFolder, "Keyed", "UniversalSqueaker.xml");
        var document = new System.Xml.XmlDocument();
        document.Load(path);

        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Xml.XmlNode node in document.SelectNodes("/LanguageData/*")!)
        {
            table[node.Name] = node.InnerText.Trim();
        }

        return table;
    }

    private static string RepoRoot()
    {
        System.IO.DirectoryInfo? dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "scripts", "verify-local.ps1"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Installs the harness language database into the Verse stub's resolver. Reached by reflection
    /// because the field exists only on the stub: call sites compile against the Krafs reference
    /// assembly, which has no such member.
    /// </summary>
    private static void SetTranslatorResolver(Dictionary<string, string>? table)
    {
        System.Reflection.FieldInfo? field = typeof(Translator).GetField(
            "Resolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert(field != null, "the Verse stub must expose Translator.Resolve for language-driven checks");
        if (field == null) return;

        if (table == null)
        {
            field.SetValue(null, null);
            return;
        }

        field.SetValue(null, new Func<string, string>(
            key => table.TryGetValue(key, out string? text) ? text : key));
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

    private sealed class StubMetrics : FerriteLib.UiKit.Kernel.ITextMetrics
    {
        /// <summary>
        /// Wrap-aware on purpose. The fit audit's height axis compares this against the band it was
        /// drawn into, so a font-constant answer makes every band at or above that constant pass no
        /// matter how long the string is: that is exactly how the help-panel header and the Packs row
        /// detail lines stayed green in this gate while visibly overdrawn in game. Height is therefore
        /// derived from the same half-width advance model as MeasureWidth.
        /// </summary>
        public float MeasureText(string text, FerriteLib.UiKit.Kernel.UiFont font, float width)
        {
            float need = MeasureWidth(text, font);
            if (need <= 0f) return LineHeight(font);
            int lines = (int)Math.Ceiling(need / Math.Max(1f, width));
            return lines * LineHeight(font);
        }

        /// <summary>
        /// One text line including leading, per font. Calibrated 2026-09-04 against the real font
        /// engine: the in-game fit audit (ui.text.overflow, English client) reported one-line needs of
        /// 18.0px tiny, 21.33333px small and 30.0px medium while these constants said 15/19/21, which
        /// is how constant bands of 16/18/24/26/28 passed this sweep and still clipped in game. Line
        /// advance is a per-font constant (it does not depend on the glyph set), so one calibrated
        /// table serves both language tables; the width axis keeps its own half-width model.
        /// </summary>
        private static float LineHeight(FerriteLib.UiKit.Kernel.UiFont font) => font switch
        {
            FerriteLib.UiKit.Kernel.UiFont.Tiny => 18f,
            FerriteLib.UiKit.Kernel.UiFont.Medium => 30f,
            _ => 21.33333f,
        };

        // Half-width advance model (CJK/full-width = one em, Latin = half an em), matching the Verse
        // stub's own CalcSize so harness-level and production-level widths agree.
        public float MeasureWidth(string text, FerriteLib.UiKit.Kernel.UiFont font)
        {
            float em = font switch
            {
                FerriteLib.UiKit.Kernel.UiFont.Tiny => 12f,
                FerriteLib.UiKit.Kernel.UiFont.Medium => 18f,
                _ => 16f
            };

            float units = 0f;
            foreach (char c in text ?? "")
            {
                units += IsWide(c) ? 2f : 1f;
            }

            return units * em * 0.5f;
        }

        private static bool IsWide(char c)
        {
            return c >= '\u2E80' && (
                c <= '\u303F'
                || (c >= '\u3400' && c <= '\u4DBF')
                || (c >= '\u4E00' && c <= '\u9FFF')
                || (c >= '\uAC00' && c <= '\uD7AF')
                || (c >= '\uF900' && c <= '\uFAFF')
                || (c >= '\uFF00' && c <= '\uFF60')
                || (c >= '\uFFE0' && c <= '\uFFE6'));
        }
    }

    private sealed class StubTranslation : FerriteLib.UiKit.Kernel.IUiTranslation
    {
        public string Translate(string key)
        {
            return key ?? "";
        }

        public int TranslationRevision => 0;
    }
}
