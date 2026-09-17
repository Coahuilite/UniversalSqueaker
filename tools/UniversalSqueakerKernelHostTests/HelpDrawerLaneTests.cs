using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Retractable right-side help drawer (outcome 1). The drawer is hidden DECLARATIVELY: the manifest's
/// help-scroll element carries VisibleKey="help-open", and the Host's revision bumper advances the clock
/// so the next arrange re-reads it. The element stays in the definition while closed - that is what
/// keeps its node and its scroll position - which the retired root-list VARIANT could not do on 0.6
/// (a removed element is released together with its scroll position).
///
/// This lane proves the whole contract against the REAL production Host, the REAL embedded Schema=2
/// manifest, the REAL widget registrations and the REAL typed binding table:
///   1. the shipped page opens RETRACTED at the declared 160px nav column (task-10: the window opens
///      narrow like the vanilla ModSettings window), and an explicit open installs the 176px help
///      column without moving the nav column;
///   2. closing (value binding and toggle action) removes the column and gives the centre column
///      exactly the help width plus the removed row gap;
///   3. close/open preserves the node identity, the help scroll position and the selected help topic;
///   4. switching tabs while open keeps the drawer open and updates the help topic;
///   5. every toggle advances the session revision without disposing or recreating the host/session;
///   6. 480px and 320px resolve the three-column Row to a stacked Column with no horizontal overflow;
///   7. the manifest's drawer element carries no Tab gate and a closed drawer survives every tab;
///   8. the header toggle writes the help-open binding through the real selection-button path;
///   9. the drawer is PER-WINDOW view state: closing it and reopening the window (page-state Reset or
///      the production fresh-source reopen) starts RETRACTED again (the shipped default), and the
///      Settings Scribe boundary plus the Mod save path carry no drawer member at all;
///  10. a workspace switch while the drawer is open keeps the topic and the drawer, and a following
///      close/reopen keeps the topic and BOTH page scroll positions with one revision bump per toggle;
///  11. FL-first statement: the four drawer-path files contain no direct GUI/Widgets/Event/manual
///      scroll-view/input-routing call, the drawer flows through UsKernelDraw/UiThemeDraw/UiNative and
///      the typed binding table, and the only direct backend call in the production source is the
///      documented camera-indicator exception (brief, "Explicit FL exception").
/// </summary>
internal static class HelpDrawerLaneTests
{
    private const float HelpWidth = 176f;
    private const float BodyRowGap = 12f;
    /// <summary>The manifest's fixed nav-column width (compact ruling: was 192).</summary>
    private const float NavColumnWidth = 160f;
    private const string HelpColumnId = "help-scroll";
    private const string HelpOpenKey = "help-open";

    /// <summary>
    /// Every identifier the drawer contract uses. The persistence scan proves none of them ever
    /// reaches the Settings Scribe boundary or the Mod save path; the owner scan proves they live
    /// only on the UI view-state files.
    /// </summary>
    private static readonly string[] DrawerStateTokens =
    {
        "HelpDrawerOpen",
        "SetHelpDrawerOpen",
        "help-open",
        "toggle-help-drawer",
        "HelpOpen",
    };

    /// <summary>
    /// The six production files allowed to carry drawer view state: the page's ephemeral state, its
    /// typed business boundary, the host binding table, the drawer's header widget, and the settings
    /// window - which since task-10 reads the per-window drawer state to size itself (narrow closed,
    /// closed + 188 open). Anything else owning a drawer token means the per-window state leaked into
    /// the persisted layer; the Scribe/save scan below still proves none of it is persisted.
    /// </summary>
    private static readonly string[] DrawerStateOwners =
    {
        "Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs",
        "Source/UniversalSqueaker/UI/IUsKernelSettingsSource.cs",
        "Source/UniversalSqueaker/UI/UsKernelSettingsSource.cs",
        "Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs",
        "Source/UniversalSqueaker/UI/Kernel/UsPageTitleWidget.cs",
        "Source/UniversalSqueaker/UI/UniversalSqueakerSettingsWindow.cs",
    };

    /// <summary>
    /// Direct host-UI entry points the brief forbids on the settings page: raw IMGUI paint/widget
    /// calls, the global event queue, manual scroll views and manual input routing.
    /// </summary>
    private static readonly string[] ForbiddenDirectUiTokens =
    {
        "GUI.",
        "Widgets.",
        "GUILayout",
        "Event.current",
        "EventType.",
        "Input.Get",
        "Input.mousePosition",
        "BeginScrollView",
        "EndScrollView",
        "InvalidateButton",
        "KeyCode",
    };

    /// <summary>Planted code carrying one real form of every forbidden token, for the scanner's positive control.</summary>
    private const string ForbiddenControlSource =
        "if (GUI.Button(rect, text)) { }\n"
        + "Widgets.Label(rect, text);\n"
        + "GUILayout.Space(4f);\n"
        + "if (Event.current.type == EventType.Repaint) { }\n"
        + "if (Input.GetKeyDown(KeyCode.Escape)) { }\n"
        + "Vector2 pointer = Input.mousePosition;\n"
        + "BeginScrollView(rect, ref scroll, content);\n"
        + "EndScrollView();\n"
        + "InvalidateButton();\n";

    public static int RunAll()
    {
        var metrics = new Program.StubMetrics();
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            DefaultClosedThenOpenReservesTheDeclaredColumn(metrics);
            ClosingReallocatesTheReservedWidth(metrics);
            ReopenPreservesTopicAndScroll(metrics);
            TabSwitchKeepsTheDrawerOpen(metrics);
            ToggleIsRevisionDrivenAndReusesTheHost(metrics);
            NarrowWidthsStackWithoutOverflow(metrics);
            ManifestAndStateAreIndependentOfActiveTab(metrics);
            HeaderToggleWritesThroughTheBinding(metrics);
            DrawerIsPerWindowViewStateAndNeverPersisted(metrics);
            CombinedScrollTabCloseReopenKeepsState(metrics);
            DrawerPathIsFlFirst();
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }

        Console.WriteLine("HelpDrawerLaneTests ALL PASS");
        return 0;
    }

    /// <summary>1. The shipped page opens RETRACTED, and an explicit open installs the help column.</summary>
    private static void DefaultClosedThenOpenReservesTheDeclaredColumn(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        UiLayoutSnapshot closed = host.MeasureAndArrange(new Vector2(800f, 600f));

        Assert(!fake.ViewState.HelpDrawerOpen,
            "the per-window drawer state must default to retracted (the window opens narrow)");
        Assert(!closed.Viewports.ContainsKey(HelpColumnId),
            "the shipped page must open with NO reserved help column");
        Assert(closed.RectById.ContainsKey("nav"), "the nav column must be arranged");
        Assert(Math.Abs(closed.RectById["nav"].width - NavColumnWidth) < 0.01f,
            "the nav column must keep its declared " + NavColumnWidth + ", got " + closed.RectById["nav"].width);

        host.Bindings.Set(HelpOpenKey, true);
        UiLayoutSnapshot open = host.MeasureAndArrange(new Vector2(800f, 600f));
        Assert(open.Viewports.ContainsKey(HelpColumnId),
            "an explicit open must install the help column");
        Assert(Math.Abs(open.Viewports[HelpColumnId].width - HelpWidth) < 0.01f,
            "the open drawer must keep its declared " + HelpWidth + " width, got " + open.Viewports[HelpColumnId].width);
        Assert(Math.Abs(open.RectById["nav"].width - NavColumnWidth) < 0.01f,
            "opening the drawer must not move the nav column off its declared " + NavColumnWidth + ", got "
            + open.RectById["nav"].width);
    }

    /// <summary>2. Closing removes the column and hands its width plus the removed gap to the centre.</summary>
    private static void ClosingReallocatesTheReservedWidth(Program.StubMetrics metrics)
    {
        foreach (string route in new[] { "binding", "action" })
        {
            var fake = new RecordingSettingsSource();
            using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
            var viewport = new Vector2(800f, 600f);
            // The shipped default is retracted; this step is about the close routes, so start open.
            host.Bindings.Set(HelpOpenKey, true);
            UiLayoutSnapshot open = host.MeasureAndArrange(viewport);
            float openContentWidth = open.Viewports["content-scroll"].width;

            if (route == "binding")
            {
                host.Bindings.Set(HelpOpenKey, false);
            }
            else
            {
                host.Bindings.Invoke("toggle-help-drawer", "");
            }

            Assert(fake.LastHelpDrawerOpen == false,
                route + ": the close write must reach the business boundary");
            UiLayoutSnapshot closed = host.MeasureAndArrange(viewport);
            Assert(!closed.Viewports.ContainsKey(HelpColumnId),
                route + ": a closed drawer must leave NO reserved help column in the measured viewports");
            float grown = closed.Viewports["content-scroll"].width - openContentWidth;
            Assert(Math.Abs(grown - (HelpWidth + BodyRowGap)) < 0.5f,
                route + ": closing must give the centre column exactly the help width plus the removed row gap: "
                + openContentWidth + " -> " + closed.Viewports["content-scroll"].width
                + " (grew " + grown + ", expected " + (HelpWidth + BodyRowGap) + ")");
        }
    }

    /// <summary>3. close -> open preserves the topic, the node identity and the help scroll position.</summary>
    private static void ReopenPreservesTopicAndScroll(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        // Short viewport on purpose: the lane must be able to really move the help scroll, and the
        // engine clamps a stored position to the content/viewport headroom on every arrange.
        var viewport = new Vector2(1280f, 240f);
        // The shipped default is retracted, and this step preserves state ACROSS a toggle, so open first.
        host.Bindings.Set(HelpOpenKey, true);
        UiLayoutSnapshot open = host.MeasureAndArrange(viewport);

        UiNode? helpNode = host.Session.GetNodeByElementId(HelpColumnId);
        Assert(helpNode != null, "the open drawer must have an arranged help-scroll node");
        float headroom = open.ScrollContents[HelpColumnId].height - open.Viewports[HelpColumnId].height;
        Assert(headroom >= 40f,
            "the lane must have real help scroll headroom to preserve, got " + headroom + "px");

        Program.SetScrollPositionById(host.Session, HelpColumnId, new Vector2(0f, 40f));
        string topic = host.Bindings.Get<string>("help-section-key");

        host.Bindings.Set(HelpOpenKey, false);
        UiLayoutSnapshot closed = host.MeasureAndArrange(viewport);
        Assert(!closed.Viewports.ContainsKey(HelpColumnId), "the closed arrange must drop the help column");

        host.Bindings.Set(HelpOpenKey, true);
        UiLayoutSnapshot reopened = host.MeasureAndArrange(viewport);
        Assert(reopened.Viewports.ContainsKey(HelpColumnId), "the reopened arrange must carry the help column again");

        Assert(ReferenceEquals(helpNode, host.Session.GetNodeByElementId(HelpColumnId)),
            "the help Scroll must keep its node identity across close/open, or its state would be lost");
        Vector2 scroll = Program.ScrollPositionById(host.Session, HelpColumnId);
        Assert(Math.Abs(scroll.y - 40f) < 0.01f,
            "the help scroll position set before the toggle must survive it, got " + scroll);
        Assert(string.Equals(host.Bindings.Get<string>("help-section-key"), topic, StringComparison.Ordinal),
            "the selected help topic must survive a close/open cycle: '" + topic + "' -> '"
            + host.Bindings.Get<string>("help-section-key") + "'");
    }

    /// <summary>4. Tab switching while open keeps the drawer and follows the workspace's help topic.</summary>
    private static void TabSwitchKeepsTheDrawerOpen(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        var viewport = new Vector2(1280f, 720f);
        // The shipped default is retracted; this step needs the drawer open across the switch.
        host.Bindings.Set(HelpOpenKey, true);
        host.MeasureAndArrange(viewport);
        string before = host.Bindings.Get<string>("help-section-key");

        host.Bindings.Invoke("set-tab", "Packs");
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(viewport);
        Assert(snapshot.Viewports.ContainsKey(HelpColumnId),
            "switching tabs while the drawer is open must not close it");
        string after = host.Bindings.Get<string>("help-section-key");
        Assert(!string.Equals(after, before, StringComparison.Ordinal) && after.Length > 0,
            "the help topic must follow the workspace while the drawer stays open: '"
            + before + "' -> '" + after + "'");
        Assert(fake.ViewState.HelpDrawerOpen, "the drawer state must stay open across a tab switch");
    }

    /// <summary>5. Revision-driven invalidation, with the host/session never recreated.</summary>
    private static void ToggleIsRevisionDrivenAndReusesTheHost(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        UiSession session = host.Session;
        var viewport = new Vector2(800f, 600f);
        // The shipped default is retracted; start open so each toggle below is a real state change.
        host.Bindings.Set(HelpOpenKey, true);
        host.MeasureAndArrange(viewport);

        int closeRevision = session.ContentRevision;
        host.Bindings.Set(HelpOpenKey, false);
        Assert(session.ContentRevision == closeRevision + 1,
            "the close value write must advance the session revision by exactly one ("
            + closeRevision + " -> " + session.ContentRevision + ")");
        host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
        Assert(ReferenceEquals(host.Session, session) && session.IsActive,
            "toggling must not dispose or recreate the host/session");
        Assert(fake.LastHelpDrawerOpen == false, "the close write must route through the business boundary");

        int openRevision = session.ContentRevision;
        host.Bindings.Invoke("toggle-help-drawer", "");
        Assert(session.ContentRevision == openRevision + 1,
            "the toggle action must advance the session revision by exactly one ("
            + openRevision + " -> " + session.ContentRevision + ")");
        Assert(fake.LastHelpDrawerOpen == true, "the toggle action must flip the state through the boundary");
        host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
        Assert(ReferenceEquals(host.Session, session) && session.IsActive,
            "the reopen must reuse the same host/session");

        int secondCloseRevision = session.ContentRevision;
        host.Bindings.Invoke("toggle-help-drawer", "");
        Assert(session.ContentRevision == secondCloseRevision + 1 && fake.LastHelpDrawerOpen == false,
            "a second toggle must close again and advance the revision once more");
    }

    /// <summary>6. Narrow widths stack the three columns and never overflow horizontally.</summary>
    private static void NarrowWidthsStackWithoutOverflow(Program.StubMetrics metrics)
    {
        foreach (float width in new[] { 480f, 320f })
        {
            foreach (bool open in new[] { true, false })
            {
                var fake = new RecordingSettingsSource { RichData = true };
                using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
                // The shipped default is retracted; set the case's state explicitly either way.
                host.Bindings.Set(HelpOpenKey, open);

                var viewport = new Vector2(width, 600f);
                UiLayoutSnapshot snapshot = host.MeasureAndArrange(viewport);
                host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));

                Assert(snapshot.Viewports.Count > 0, "the page must still arrange scroll containers at " + width);
                foreach (KeyValuePair<string, Rect> pair in snapshot.Viewports)
                {
                    Assert(pair.Value.width > 0f && pair.Value.width <= width + 0.01f,
                        "viewport '" + pair.Key + "' must measure a positive width inside the window at "
                        + width + " (open=" + open + "): " + pair.Value);
                }

                foreach (KeyValuePair<string, Rect> pair in snapshot.ScrollContents)
                {
                    Assert(pair.Value.width <= width + 0.01f,
                        "scroll content '" + pair.Key + "' must not overflow the window horizontally at "
                        + width + ": " + pair.Value);
                    if (snapshot.Viewports.TryGetValue(pair.Key, out Rect viewportRect))
                    {
                        Assert(pair.Value.width <= viewportRect.width + 0.5f,
                            "scroll content '" + pair.Key + "' must fit its own viewport at " + width
                            + ": content=" + pair.Value.width + " viewport=" + viewportRect.width);
                    }
                }

                if (open)
                {
                    Assert(snapshot.Viewports.ContainsKey(HelpColumnId),
                        "the open drawer must still be a real scroll container at " + width);
                    Rect content = snapshot.Viewports["content-scroll"];
                    Rect help = snapshot.Viewports[HelpColumnId];
                    Assert(help.y >= content.yMax - 0.5f,
                        "at " + width + " the help drawer must stack BELOW the centre column, not sit beside"
                        + " it as a third squeezed column: content=" + content + " help=" + help);
                    Assert(Math.Abs(help.width - content.width) < 0.5f,
                        "the stacked help drawer must share the full inner width at " + width
                        + ": help=" + help.width + " content=" + content.width);
                }
                else
                {
                    Assert(!snapshot.Viewports.ContainsKey(HelpColumnId),
                        "the closed drawer must be absent at " + width);
                }

                Assert(host.Session.IsActive,
                    "the session must stay active at " + width + " (open=" + open + ")");
            }
        }
    }

    /// <summary>7. No Tab gate on the drawer element, and a closed drawer survives every workspace.</summary>
    private static void ManifestAndStateAreIndependentOfActiveTab(Program.StubMetrics metrics)
    {
        using (Stream? stream = typeof(UsKernelSettingsHost).Assembly
            .GetManifestResourceStream("UniversalSqueaker.UI.Layout.Schema2.xml"))
        {
            Assert(stream != null, "the embedded Schema=2 manifest must be readable");
            if (stream == null) return;
            using var reader = new StreamReader(stream);
            UiLayoutManifest manifest = UiLayoutManifest.Parse(reader.ReadToEnd());
            UiElementSpec? drawer = FindById(manifest.Roots, HelpColumnId);
            Assert(drawer != null, "the shipped manifest must keep the help-scroll drawer element");
            Assert(!drawer!.TryGetAttribute("Tab", out _),
                "help-scroll must never carry a Tab attribute: help visibility is independent state, not"
                + " the engine's active-tab gate");
        }

        // Behavioural half: a Tab-driven drawer would reappear whenever the workspace matched, so a
        // closed drawer must stay closed across every workspace switch.
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        var viewport = new Vector2(1280f, 720f);
        host.MeasureAndArrange(viewport);
        host.Bindings.Set(HelpOpenKey, false);
        foreach (string tab in new[] { "Overview", "Distance", "Packs", "Tuning", "Presets" })
        {
            host.Bindings.Invoke("set-tab", tab);
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(viewport);
            Assert(!snapshot.Viewports.ContainsKey(HelpColumnId),
                "a closed drawer must stay closed across workspace '" + tab + "' (help visibility is not active-tab)");
        }

        Assert(!fake.ViewState.HelpDrawerOpen, "the closed drawer state must survive every tab switch");
    }

    /// <summary>
    /// 8. The header toggle is a real control: the page title widget's selection button writes the
    /// help-open binding through the production binding table (pressed through UiNative's harness seam,
    /// so this exercises the widget's actual Draw path rather than re-stating the binding).
    /// </summary>
    private static void HeaderToggleWritesThroughTheBinding(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        // The shipped default is retracted, so open first: the press below must close it again.
        host.Bindings.Set(HelpOpenKey, true);
        var widget = new UsPageTitleWidget();
        UiWidgetContext ctx = host.CreateContext(420f, "page-title");
        float height = widget.Measure(ctx);
        var rect = new Rect(0f, 0f, 420f, height);

        FieldInfo? overrideField = typeof(UiNative).GetField(
            "ButtonOverride", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(overrideField != null, "the harness needs UiNative.ButtonOverride to press the header toggle");
        if (overrideField == null) return;

        Rect pressed = default;
        overrideField.SetValue(null, new Func<Rect, bool>(r => { pressed = r; return true; }));
        try
        {
            widget.Draw(rect, ctx);
        }
        finally
        {
            overrideField.SetValue(null, null);
        }

        Assert(pressed.width > 0f, "the page header must draw the help toggle inside its title band");
        Assert(Math.Abs(pressed.height - 26f) < 0.01f,
            "the header toggle must reserve the ~26px control band: " + pressed);
        Assert(pressed.width >= 72f - 0.01f,
            "the header toggle must keep its ~72px floor so a short translation still fits: " + pressed);
        Assert(pressed.xMax <= rect.xMax + 0.01f && pressed.xMax >= rect.xMax - 1.01f,
            "the header toggle must sit at the right edge of the title band: " + pressed);
        Assert(fake.LastHelpDrawerOpen == false,
            "pressing the header toggle must write the help-open binding (open -> closed)");
        Assert(!fake.ViewState.HelpDrawerOpen, "the header toggle write must land in the page state");
    }

    /// <summary>
    /// 9. The drawer is per-window VIEW state and is never persisted.
    ///  - the Settings Scribe boundary and the Mod save path carry no drawer member at all, which is
    ///    why this outcome leaves the migration/config-copy suites untouched;
    ///  - every drawer identifier in the production source lives on the five UI view-state files;
    ///  - runtime: the shipped default is retracted; Reset and the production reopen path of a fresh
    ///    source + host both answer retracted again, and an explicit open installs the declared width
    ///    - because nothing about visibility was ever written to settings.
    /// </summary>
    private static void DrawerIsPerWindowViewStateAndNeverPersisted(Program.StubMetrics metrics)
    {
        string root = RepoRoot();

        // ---- Persistence layer: the settings Scribe boundary and the Mod save path carry no drawer member.
        var persistenceFiles = new List<string>();
        string settingsDirectory = Path.Combine(root, "Source", "UniversalSqueaker", "Settings");
        Assert(Directory.Exists(settingsDirectory), "the settings scan root must exist: " + settingsDirectory);
        persistenceFiles.AddRange(Directory.GetFiles(settingsDirectory, "*.cs", SearchOption.AllDirectories));
        string modPath = Path.Combine(root, "Source", "UniversalSqueaker", "Mod.cs");
        Assert(File.Exists(modPath), "the Mod save path must exist: " + modPath);
        persistenceFiles.Add(modPath);

        Assert(persistenceFiles.Count >= 4,
            "the persistence scan must really walk the settings + save path; found " + persistenceFiles.Count + " files");
        foreach (string file in persistenceFiles)
        {
            string text = StripComments(File.ReadAllText(file));
            foreach (string token in DrawerStateTokens)
            {
                Assert(CountToken(text, token) == 0,
                    "the settings/save path must not carry drawer visibility, but '" + token + "' appears in "
                    + Relative(root, file));
            }
        }

        // Positive control: a scanner that found nothing because it read nothing must not pass.
        Assert(CountToken(StripComments("public bool HelpDrawerOpen = true;"), "HelpDrawerOpen") == 1,
            "positive control: the persistence scanner must flag a planted drawer member");
        Assert(CountToken(StripComments("var key = \"toggle-help-drawer\";"), "toggle-help-drawer") == 1,
            "positive control: the persistence scanner must flag a planted drawer binding key");

        // ---- The owner set: the state never leaks out of the UI view layer. Settings semantics stay
        // owned by the migration/config-copy suites; this lane deliberately does not modify them.
        var owners = new List<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Source", "UniversalSqueaker"), "*.cs", SearchOption.AllDirectories))
        {
            string text = StripComments(File.ReadAllText(file));
            if (TokensPresent(text, DrawerStateTokens).Count > 0)
            {
                owners.Add(Relative(root, file));
            }
        }

        owners.Sort(StringComparer.Ordinal);
        var expectedOwners = new List<string>(DrawerStateOwners);
        expectedOwners.Sort(StringComparer.Ordinal);
        bool sameOwners = owners.Count == expectedOwners.Count;
        for (int i = 0; sameOwners && i < owners.Count; i++)
        {
            sameOwners = string.Equals(owners[i], expectedOwners[i], StringComparison.Ordinal);
        }

        Assert(sameOwners,
            "drawer view state must live only on the page's ephemeral state + its UI boundary/widgets;"
            + " found: " + string.Join(" | ", owners));

        // ---- Runtime reopen 1: the page state's own Reset (the session begin/end reset). The shipped
        // default is RETRACTED (task-10: the window opens narrow), so the fresh page and Reset both
        // answer retracted, while an explicit open must still install the declared width.
        var fake = new RecordingSettingsSource();
        var viewport = new Vector2(800f, 600f);
        using (UiHost host = UsKernelSettingsHost.Create(fake, metrics))
        {
            UiLayoutSnapshot initial = host.MeasureAndArrange(viewport);
            Assert(!initial.Viewports.ContainsKey(HelpColumnId),
                "the window must open with the drawer retracted (the narrow default)");
            Assert(!fake.ViewState.HelpDrawerOpen, "the page state must default to retracted");

            host.Bindings.Set(HelpOpenKey, true);
            UiLayoutSnapshot open = host.MeasureAndArrange(viewport);
            Assert(open.Viewports.ContainsKey(HelpColumnId)
                && Math.Abs(open.Viewports[HelpColumnId].width - HelpWidth) < 0.01f,
                "the open write must install the declared " + HelpWidth + "px help column");

            host.Bindings.Set(HelpOpenKey, false);
            UiLayoutSnapshot closed = host.MeasureAndArrange(viewport);
            Assert(!closed.Viewports.ContainsKey(HelpColumnId), "the close write must retract the drawer");
            Assert(!fake.ViewState.HelpDrawerOpen, "the close write must land in the per-window view state");

            fake.ViewState.Reset();
            Assert(!fake.ViewState.HelpDrawerOpen,
                "Reset must restore the default-RETRACTED drawer: visibility is per-window view state, not a setting");
            Assert(!host.Bindings.Get<bool>(HelpOpenKey),
                "the help-open binding must read the reset default, so a reopened session starts retracted");
            host.Bindings.Invoke("set-tab", "Overview");
            UiLayoutSnapshot reset = host.MeasureAndArrange(viewport);
            Assert(!reset.Viewports.ContainsKey(HelpColumnId),
                "after Reset, a real revision-bumping write must keep the drawer retracted");
        }

        // ---- Runtime reopen 2 (what the window really does): closing disposes the host and its source;
        // reopening builds a fresh pair, so the retracted default is what a player sees again.
        var fresh = new RecordingSettingsSource();
        Assert(fresh.LastHelpDrawerOpen == null, "a reopened window's source must start with no drawer write");
        using (UiHost reopened = UsKernelSettingsHost.Create(fresh, metrics))
        {
            UiLayoutSnapshot snapshot = reopened.MeasureAndArrange(viewport);
            Assert(!snapshot.Viewports.ContainsKey(HelpColumnId),
                "a reopened window must build the drawer retracted: visibility is view state, never persisted");
            Assert(!fresh.ViewState.HelpDrawerOpen, "the reopened window's page state must default retracted");
        }

        Console.WriteLine("[drawer-persist] settings/save files scanned=" + persistenceFiles.Count
            + " settings/save drawer tokens=0; drawer owners=" + owners.Count + " (" + string.Join(", ", owners)
            + "); Reset reopened=retracted, fresh-source reopen=retracted");
    }

    /// <summary>
    /// 10. Combined state: the help scroll is really scrolled, a real workspace switch keeps the drawer
    /// open and moves the topic, then close + reopen keeps the topic, the drawer and BOTH page scroll
    /// positions with exactly one revision bump per toggle. The switch itself resets both page scrolls
    /// through the Host's ResetScroll (existing semantics), so the lane re-seeds the scrolls after it.
    /// </summary>
    private static void CombinedScrollTabCloseReopenKeepsState(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        UiSession session = host.Session;
        var viewport = new Vector2(1280f, 240f);
        // The shipped default is retracted; this combined-state lane needs the drawer open first.
        host.Bindings.Set(HelpOpenKey, true);
        UiLayoutSnapshot initial = host.MeasureAndArrange(viewport);
        Assert(initial.Viewports.ContainsKey(HelpColumnId), "the combined-state lane needs the drawer open");

        Program.SetScrollPositionById(session, HelpColumnId, new Vector2(0f, 40f));
        Program.SetScrollPositionById(session, "content-scroll", new Vector2(0f, 24f));
        Assert(Math.Abs(Program.ScrollPositionById(session, HelpColumnId).y - 40f) < 0.01f
            && Math.Abs(Program.ScrollPositionById(session, "content-scroll").y - 24f) < 0.01f,
            "the lane must really move both page scrolls before it asserts preservation");

        string beforeTopic = host.Bindings.Get<string>("help-section-key");
        host.Bindings.Invoke("set-tab", "Tuning");
        UiLayoutSnapshot switched = host.MeasureAndArrange(viewport);
        Assert(switched.Viewports.ContainsKey(HelpColumnId), "a workspace switch must not close the drawer");
        Assert(fake.ViewState.HelpDrawerOpen, "the drawer state must stay open across a workspace switch");
        Assert(Math.Abs(Program.ScrollPositionById(session, HelpColumnId).y) < 0.01f
            && Math.Abs(Program.ScrollPositionById(session, "content-scroll").y) < 0.01f,
            "a workspace switch resets both page scrolls to top by design (SessionRevisionBumper.ResetScroll)");

        string topic = host.Bindings.Get<string>("help-section-key");
        Assert(!string.Equals(topic, beforeTopic, StringComparison.Ordinal),
            "the help topic must follow the switched workspace: '" + beforeTopic + "' -> '" + topic + "'");
        Assert(string.Equals(topic, fake.SectionHelpKey(fake.ViewState.ActiveSectionKey), StringComparison.Ordinal),
            "the open drawer must show the active section's help: topic '" + topic + "' vs active section '"
            + fake.ViewState.ActiveSectionKey + "'");

        float helpHeadroom = switched.ScrollContents[HelpColumnId].height - switched.Viewports[HelpColumnId].height;
        float contentHeadroom = switched.ScrollContents["content-scroll"].height - switched.Viewports["content-scroll"].height;
        Assert(helpHeadroom >= 40f, "the lane needs real help headroom after the switch, got " + helpHeadroom);
        Assert(contentHeadroom >= 24f, "the lane needs real centre headroom after the switch, got " + contentHeadroom);

        Program.SetScrollPositionById(session, HelpColumnId, new Vector2(0f, 40f));
        Program.SetScrollPositionById(session, "content-scroll", new Vector2(0f, 24f));

        int closeRevision = session.ContentRevision;
        host.Bindings.Set(HelpOpenKey, false);
        Assert(session.ContentRevision == closeRevision + 1,
            "the close write must advance the session revision by exactly one ("
            + closeRevision + " -> " + session.ContentRevision + ")");
        UiLayoutSnapshot closed = host.MeasureAndArrange(viewport);
        Assert(!closed.Viewports.ContainsKey(HelpColumnId), "the closed arrange must drop the help column");

        int reopenRevision = session.ContentRevision;
        host.Bindings.Set(HelpOpenKey, true);
        Assert(session.ContentRevision == reopenRevision + 1,
            "the reopen write must advance the session revision by exactly one ("
            + reopenRevision + " -> " + session.ContentRevision + ")");
        UiLayoutSnapshot reopened = host.MeasureAndArrange(viewport);
        Assert(reopened.Viewports.ContainsKey(HelpColumnId), "the reopened arrange must carry the help column");

        Assert(string.Equals(host.Bindings.Get<string>("help-section-key"), topic, StringComparison.Ordinal),
            "close/reopen must keep the topic the workspace switch selected: '" + topic + "'");
        Vector2 helpScroll = Program.ScrollPositionById(session, HelpColumnId);
        Vector2 contentScroll = Program.ScrollPositionById(session, "content-scroll");
        Assert(Math.Abs(helpScroll.y - 40f) < 0.01f,
            "close/reopen must keep the help scroll position, got " + helpScroll);
        Assert(Math.Abs(contentScroll.y - 24f) < 0.01f,
            "close/reopen must keep the centre content scroll position, got " + contentScroll);
        Assert(ReferenceEquals(host.Session, session) && session.IsActive && fake.ViewState.HelpDrawerOpen,
            "the combined close/reopen must leave the same live session, drawer open");

        Console.WriteLine("[drawer-combined] tab switch -> topic=" + topic + " helpHeadroom=" + helpHeadroom
            + " contentHeadroom=" + contentHeadroom + "; close/reopen kept topic, help.y=" + helpScroll.y
            + ", content.y=" + contentScroll.y + ", revision +1 per toggle");
    }

    /// <summary>
    /// 11. FL-first statement for the drawer path, per the brief's hard rule and its "Explicit FL
    /// exception" section: no direct GUI/Widgets/Event/manual scroll-view/input-routing call in any
    /// drawer-path file, the drawer flows through UsKernelDraw/UiThemeDraw/UiNative and the typed
    /// binding table, and the only direct backend call in the whole production source is the
    /// documented camera-indicator exception.
    /// </summary>
    private static void DrawerPathIsFlFirst()
    {
        string root = RepoRoot();
        string[] drawerPaths =
        {
            "Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs",
            "Source/UniversalSqueaker/UI/Kernel/UsPageTitleWidget.cs",
            "Source/UniversalSqueaker/UI/Layout.Schema2.xml",
        };

        foreach (string relative in drawerPaths)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert(File.Exists(path), "the FL-first scan needs " + relative);
            List<string> hits = TokensPresent(StripComments(File.ReadAllText(path)), ForbiddenDirectUiTokens);
            Assert(hits.Count == 0,
                relative + " must stay FL-first: found direct " + string.Join(", ", hits)
                + " (the drawer path goes through UsKernelDraw/UiNative/UiThemeDraw only)");
        }

        // Positive control: every forbidden form the scanner claims to cover must be flagged, or an empty
        // scan could pass this step silently.
        List<string> planted = TokensPresent(ForbiddenControlSource, ForbiddenDirectUiTokens);
        Assert(planted.Count == ForbiddenDirectUiTokens.Length,
            "positive control: the FL-first scanner must flag every forbidden form; flagged "
            + planted.Count + "/" + ForbiddenDirectUiTokens.Length);
        // The comment stripper is what keeps a documentation mention from faking a violation: pin it.
        Assert(CountToken(StripComments("// the carrier's engine owns BeginScrollView"), "BeginScrollView") == 0,
            "a documentation mention of a forbidden call must not count as a violation");
        Assert(CountToken(StripComments(ForbiddenControlSource), "Widgets.") >= 1,
            "a real forbidden call must still be flagged after comment stripping");

        // Positive routing evidence: the drawer control draws only through the FL primitives, and the
        // toggle writes only through the typed binding table.
        string title = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsPageTitleWidget.cs"));
        Assert(CountToken(title, "UsKernelDraw.Label(") >= 1
            && CountToken(title, "UsKernelDraw.SelectionButton(") >= 1
            && CountToken(title, "UiThemeDraw.SectionBand(") >= 1,
            "the drawer's header toggle must draw through UsKernelDraw and UiThemeDraw");
        string hostSource = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsHost.cs"));
        Assert(CountToken(hostSource, "UiNative.") >= 1,
            "the host must route its native trace/hit surface through UiNative");
        Assert(CountToken(hostSource, "\"help-open\"") >= 1 && CountToken(hostSource, "\"toggle-help-drawer\"") >= 1,
            "the drawer toggle must go through the typed binding table, never a raw event handler");

        // The retired root-list variant must not come back: removing an element from the definition is the
        // path whose node the 0.6 engine releases, so the drawer's visibility has to stay declarative.
        string manifest = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml"));
        Assert(CountToken(manifest, "VisibleKey=\"help-open\"") == 1,
            "the manifest must hide the drawer through VisibleKey=\"help-open\" exactly once");
        Assert(!File.Exists(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout", "UsLayoutVariants.cs")),
            "the root-list variant must not come back: the engine releases an omitted element's node and scroll position");

        // Whole-source audit: the ONLY direct backend call in the production source is the documented
        // camera-indicator exception (brief 2026-09-13, "Explicit FL exception").
        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Source", "UniversalSqueaker"), "*.cs", SearchOption.AllDirectories))
        {
            List<string> hits = TokensPresent(StripComments(File.ReadAllText(file)), ForbiddenDirectUiTokens);
            if (hits.Count > 0)
            {
                offenders.Add(Relative(root, file) + " [" + string.Join(",", hits) + "]");
            }
        }

        Assert(offenders.Count == 1
            && string.Equals(offenders[0],
                "Source/UniversalSqueaker/Patches/Patch_GlobalControlsUtility_CameraIndicator.cs [Widgets.]",
                StringComparison.Ordinal),
            "the only direct GUI/Widgets/Event call in the production source must be the documented"
            + " camera-indicator exception; found: " + (offenders.Count == 0 ? "(none)" : string.Join(" | ", offenders)));

        Console.WriteLine("[drawer-flfirst] drawer files scanned=" + drawerPaths.Length + " direct-call hits=0;"
            + " production direct-call offenders=" + offenders.Count + " [" + string.Join(" | ", offenders) + "]");
    }

    /// <summary>The harness's own copy of Program.RepoRoot's rule (that helper is private): walk up from
    /// the binary until the repository's gate script appears.</summary>
    private static string RepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "verify-local.ps1"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

    /// <summary>Workspace-relative, forward-slashed path for a scan report.</summary>
    private static string Relative(string root, string path)
    {
        return path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
    }

    /// <summary>
    /// Drops line comments, block comments and XML comments. Without this, a documentation mention of a
    /// forbidden backend call would read as a violation; with it, only real code can trip the scanners.
    /// </summary>
    private static string StripComments(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }

            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = Math.Min(text.Length, i + 2);
                continue;
            }

            if (i + 3 < text.Length && text[i] == '<' && text[i + 1] == '!' && text[i + 2] == '-' && text[i + 3] == '-')
            {
                i += 4;
                while (i + 2 < text.Length && !(text[i] == '-' && text[i + 1] == '-' && text[i + 2] == '>')) i++;
                i = Math.Min(text.Length, i + 3);
                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    private static int CountToken(string text, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static List<string> TokensPresent(string text, string[] tokens)
    {
        var present = new List<string>();
        foreach (string token in tokens)
        {
            if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
            {
                present.Add(token);
            }
        }

        return present;
    }

    private static UiElementSpec? FindById(IReadOnlyList<UiElementSpec> specs, string id)
    {
        foreach (UiElementSpec spec in specs)
        {
            if (string.Equals(spec.Id, id, StringComparison.Ordinal)) return spec;
            UiElementSpec? nested = FindById(spec.Children, id);
            if (nested != null) return nested;
        }

        return null;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
