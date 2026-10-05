using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Bottom help panel (BH1, re-cut from the retired right-side drawer lane). The panel is hidden
/// DECLARATIVELY: the manifest's help-scroll element carries VisibleKey="help-open" - the one key the
/// player's single footer switch writes - and the Host's revision bumper advances the clock so the next
/// arrange re-reads it. There is exactly ONE presentation now: the pre-BH1 question "which of the two
/// presentations does this page box afford" (side column vs defensive full-width band) is gone with the
/// band, so no lane here feeds or fakes a page-box decision.
/// The element stays in the definition while closed - that is what keeps its node and its scroll
/// position - which the retired root-list VARIANT could not do on 0.6 (a removed element is released
/// together with its scroll position).
///
/// This lane checks the contract against the production Host, embedded Schema=2 manifest, widget
/// registrations and typed binding table, with stub text metrics and synthetic game/input facts:
///   1. the shipped page opens RETRACTED, and an explicit open reserves the manifest's declared band
///      height above the footer WITHOUT changing the body row's width (BH1.2) and without moving the nav
///      column;
///   2. closing (value binding and the toggle command) removes the band and hands back exactly the
///      reserved height plus the page gap it consumed;
///   3. close/open preserves the node identity, the panel's own scroll position and the selected topic,
///      and the body keeps its own scroll position throughout (BH1.3);
///   4. switching workspaces while open keeps the panel open and follows the help topic;
///   5. every toggle advances the session revision without disposing or recreating the host/session;
///   6. the body row is arranged at EVERY width the engine can be handed, open or closed - the panel
///      never replaces it (this is where the retired band branch used to be probed; BH1.1 forbids it);
///   7. the manifest gates the panel on the one player intent, declares exactly one switch, declares it
///      inside the footer band, and the retired keys/elements do not come back;
///   8. real Host input on the bottom switch executes the typed command, and a settings control in the
///      body still takes a real click while the panel is open (BH1.3);
///   9. the panel is PER-WINDOW view state: the page-state Reset and the production fresh-source reopen
///      both answer RETRACTED again, and the Settings Scribe boundary plus the Mod save path carry no
///      panel member at all;
///  10. a workspace switch while open keeps the topic, and a following close/reopen keeps the topic and
///      BOTH scroll positions with one revision bump per toggle;
///  11. FL-first statement: the panel-path files contain no direct GUI/Widgets/Event/manual scroll-view/
///      input-routing call, the panel flows through UsKernelDraw/UiThemeDraw/UiNative and the typed
///      binding table, and the only direct backend call in the production source is the documented
///      camera-indicator exception.
///
/// MUTATION LEDGER (BH1 r2): moving help-toggle back to its pre-BH1 header position reddened the
/// "header must no longer carry the help switch" clause here and the footer-row clause in gate 12;
/// byte-identical restoration was followed by a green Host run. That placement clause has a fresh
/// faithful-revert proof. The other clauses are current-input GUARDS, without individual fresh
/// faithful-revert proofs. Removing VisibleKey was a fault-injection control, not a historical undo:
/// it reddened Program's default-hidden clause before this lane ran. It proves no clause in this lane.
/// Artifacts: PM evidence/bh1-bottom-help-20261005/{faithful-revert-A-header-toggle.log,
/// faithful-revert-A-gate12.log,faithful-revert-B-visiblekey.log,host-r2-restoration-green.log}.
/// </summary>
internal static class HelpPanelLaneTests
{
    private const string HelpBandId = "help-scroll";
    private const string HelpPanelWidgetId = "help-panel";
    private const string ToggleElementId = "help-toggle";
    private const string HelpOpenKey = "help-open";
    private const string ToggleCommand = "toggle-help-drawer";

    /// <summary>
    /// BH1.2: the page box the shell hands the page at the game's minimum logical resolution (1024x768) -
    /// window 800x600 minus 2 x 20 chrome, minus the 56px title bar and the 20px bottom inset. This one
    /// box is the input in BOTH help states; the retired 984px open box is gone because the toggle no
    /// longer widens anything.
    /// </summary>
    private static readonly Vector2 AcceptancePageBox = new Vector2(760f, 524f);

    /// <summary>
    /// Every identifier the panel contract uses. The persistence scan proves none of them ever reaches
    /// the Settings Scribe boundary or the Mod save path; the owner scan proves they live only on the UI
    /// view-state files. The command token is the STABLE name the switch has carried since S3-2a - the
    /// bottom move kept it, so the same literal participates in the persistence scan and in the retired
    /// scan (which watches the briefly proposed rename): one truth, watched twice from both sides.
    /// </summary>
    private static readonly string[] PanelStateTokens =
    {
        "HelpPanelOpen",
        "SetHelpPanelOpen",
        "help-open",
        "toggle-help-drawer",
    };

    /// <summary>
    /// The four production files allowed to carry panel view state: the page's ephemeral state, its typed
    /// business boundary (declaration + implementation) and the Host binding table. BH1 REMOVED the fifth
    /// (UniversalSqueakerSettingsWindow.cs): the window used to read the drawer state to size itself, and
    /// with one window width there is nothing left for it to read - an owner list that still named the
    /// window would be pinning the widening it just retired.
    /// </summary>
    private static readonly string[] PanelStateOwners =
    {
        "Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs",
        "Source/UniversalSqueaker/UI/IUsKernelSettingsSource.cs",
        "Source/UniversalSqueaker/UI/UsKernelSettingsSource.cs",
        "Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs",
    };

    /// <summary>
    /// The retired presentation machinery, asserted ABSENT (after comment-stripping) from both the manifest
    /// and the Host source. A key from this list reappearing is the drawer coming back in some form (a
    /// second writable truth, or a band that replaces the settings), and the renamed command token
    /// reappearing is the machine name being churned again - which is what BH1 correction r2 removed.
    /// </summary>
    private static readonly string[] RetiredPresentationKeys =
    {
        "help-open-wide",
        "help-open-narrow",
        "body-visible",
        "help-band",
        "help-panel-narrow",
        "toggle-help-panel",
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
            OpenReservesTheDeclaredBandWithoutMovingAnything(metrics);
            ClosingReturnsTheReservedHeightToTheBody(metrics);
            ReopenPreservesTopicNodeAndScroll(metrics);
            TabSwitchKeepsThePanelOpen(metrics);
            ToggleIsRevisionDrivenAndReusesTheHost(metrics);
            BodyIsNeverReplacedAtAnyWidth(metrics);
            ManifestGatesThePanelOnTheOneIntent(metrics);
            BottomSwitchPressRoutesAndSettingsStayOperable(metrics);
            PanelIsPerWindowViewStateAndNeverPersisted(metrics);
            CombinedScrollTabCloseReopenKeepsState(metrics);
            PanelPathIsFlFirst();
            FooterCarriesIdentityAndEverySaveStatusWithHelpOpen(metrics);
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }

        Console.WriteLine("HelpPanelLaneTests ALL PASS");
        return 0;
    }

    /// <summary>
    /// 1. The shipped page opens RETRACTED, and an explicit open reserves the declared band above the
    /// footer. The two claims that make BH1.2 measurable: the reservation is the DECLARED height (read
    /// out of the manifest, never restated here), and opening help leaves the body row's WIDTH, the nav
    /// column's WIDTH and the footer's own geometry untouched - only the body's height yields.
    /// </summary>
    private static void OpenReservesTheDeclaredBandWithoutMovingAnything(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);

        UiLayoutSnapshot closed = host.MeasureAndArrange(AcceptancePageBox);
        Assert(!fake.ViewState.HelpPanelOpen,
            "the per-window panel state must default to retracted (the window opens narrow)");
        Assert(!closed.Viewports.ContainsKey(HelpBandId),
            "the shipped page must open with NO help band arranged");
        Assert(closed.RectById.TryGetValue("body-row", out Rect bodyClosed), "the body row must be arranged");
        Assert(closed.RectById.TryGetValue("nav-column", out Rect navClosed), "the nav column must be arranged");
        Assert(closed.RectById.TryGetValue("footer-band", out Rect footerClosed),
            "the footer band must be arranged while help is closed");

        float declaredHeight = DeclaredBandHeight(host);
        float pageGap = DeclaredPageRootGap(host);

        host.Bindings.Set(HelpOpenKey, true);
        UiLayoutSnapshot open = host.MeasureAndArrange(AcceptancePageBox);
        Assert(open.Viewports.TryGetValue(HelpBandId, out Rect panel),
            "an explicit open must arrange the bottom help band");
        Assert(Math.Abs(panel.height - declaredHeight) < 0.01f,
            "the band must be arranged at the manifest's declared " + declaredHeight + " height, got "
            + panel.height);
        Assert(open.RectById.TryGetValue("body-row", out Rect bodyOpen),
            "opening help must keep the body row arranged (BH1.1)");
        Assert(open.RectById.TryGetValue("nav-column", out Rect navOpen),
            "opening help must keep the nav column arranged (BH1.1)");
        Assert(open.Viewports.ContainsKey("content-scroll"),
            "opening help must keep the centre scroll arranged (BH1.1)");
        Assert(Math.Abs(bodyOpen.width - bodyClosed.width) < 0.01f
                && Math.Abs(navOpen.width - navClosed.width) < 0.01f,
            "opening help must not widen or shift the page: body " + bodyClosed.width + " -> "
            + bodyOpen.width + ", nav " + navClosed.width + " -> " + navOpen.width);
        Assert(Math.Abs(panel.width - bodyOpen.width) < 0.01f,
            "the band must share the page's inner width with the body, not take a column beside it: band="
            + panel.width + " body=" + bodyOpen.width);
        Assert(Math.Abs((bodyClosed.height - (declaredHeight + pageGap)) - bodyOpen.height) < 0.5f,
            "the band's height plus the page gap must come OUT of the body row, not be added to the page: "
            + "closed body=" + bodyClosed.height + " open body=" + bodyOpen.height
            + " reservation=" + (declaredHeight + pageGap));
        Assert(bodyOpen.height > 200f,
            "the body must keep an operable viewport after the reservation, got " + bodyOpen.height);
        Assert(open.RectById.TryGetValue("footer-band", out Rect footerOpen),
            "the footer band must still be arranged with help open");
        Assert(footerOpen.y >= footerClosed.y - 0.01f,
            "the footer must not be pushed up off the page by the reservation: closed=" + footerClosed
                + " open=" + footerOpen);

        Console.WriteLine("[bh1-geometry] page box " + AcceptancePageBox.x + "x" + AcceptancePageBox.y
            + " declared band=" + declaredHeight + " gap=" + pageGap + "; body " + bodyClosed.width + "x"
            + bodyClosed.height + " -> " + bodyOpen.width + "x" + bodyOpen.height + "; footer y "
            + footerClosed.y + " -> " + footerOpen.y);
    }

    /// <summary>2. Closing removes the band and gives back exactly what it reserved.</summary>
    private static void ClosingReturnsTheReservedHeightToTheBody(Program.StubMetrics metrics)
    {
        foreach (string route in new[] { "binding", "command" })
        {
            var fake = new RecordingSettingsSource { RichData = true };
            using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
            host.Bindings.Set(HelpOpenKey, true);
            UiLayoutSnapshot open = host.MeasureAndArrange(AcceptancePageBox);
            float openBodyHeight = open.RectById["body-row"].height;

            if (route == "binding")
            {
                host.Bindings.Set(HelpOpenKey, false);
            }
            else
            {
                host.Bindings.Invoke(ToggleCommand);
            }

            Assert(fake.LastHelpPanelOpen == false, route + ": the close write must reach the business boundary");
            UiLayoutSnapshot closed = host.MeasureAndArrange(AcceptancePageBox);
            Assert(!closed.Viewports.ContainsKey(HelpBandId),
                route + ": a closed panel must leave NO reserved band in the measured viewports");
            float givenBack = closed.RectById["body-row"].height - openBodyHeight;
            float expected = DeclaredBandHeight(host) + DeclaredPageRootGap(host);
            Assert(Math.Abs(givenBack - expected) < 0.5f,
                route + ": closing must hand the body exactly the band height plus the page gap it consumed: "
                + openBodyHeight + " -> " + closed.RectById["body-row"].height + " (grew " + givenBack
                + ", expected " + expected + ")");
        }
    }

    /// <summary>
    /// 3. close -> open preserves the topic, the node identity and the band's OWN scroll position, and the
    /// centre column keeps its own (BH1.3: help scrolls independently of body/nav).
    ///
    /// <para>
    /// Two input boxes, because how much a band can scroll is a property of the box, not of the toggle. At
    /// the shipped page box the panel is 736 wide and the widget sizes itself against the hover-invariant
    /// catalog maximum, so the whole panel is barely taller than the declared band and the real headroom is
    /// a handful of pixels - the lane preserves what exists and says so. The stacked narrow box wraps the
    /// same content into a genuinely tall panel, which is where a 40px preserved position is a meaningful
    /// claim. Neither input is presented as the other's appearance, and the engine clamps a stored position
    /// to the headroom, so each case demands strictly more headroom than the offset it writes: a band that
    /// could not scroll would read back zero and redden instead of passing quietly.
    /// </para>
    /// </summary>
    private static void ReopenPreservesTopicNodeAndScroll(Program.StubMetrics metrics)
    {
        foreach ((Vector2 viewport, float preserved, float extra) in new[]
                 {
                     (new Vector2(760f, 524f), 2f, 1f),
                     (new Vector2(320f, 524f), 40f, 4f),
                 })
        {
            var fake = new RecordingSettingsSource { RichData = true };
            using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
            // The shipped default is retracted, and this step preserves state ACROSS a toggle, so open first.
            host.Bindings.Set(HelpOpenKey, true);
            UiLayoutSnapshot open = host.MeasureAndArrange(viewport);

            UiNode? helpNode = host.Session.GetNodeByElementId(HelpBandId);
            Assert(helpNode != null, "the open panel must have an arranged help-scroll node at " + viewport);
            float headroom = open.ScrollContents[HelpBandId].height - open.Viewports[HelpBandId].height;
            Assert(headroom >= preserved + extra,
                viewport + ": the lane needs more real panel headroom than the position it preserves, got "
                + headroom + "px for a " + preserved + "px offset");
            float centreHeadroom = open.ScrollContents["content-scroll"].height
                - open.Viewports["content-scroll"].height;
            Assert(centreHeadroom >= 30f + 1f,
                viewport + ": the centre column needs its own headroom for the independence claim, got "
                + centreHeadroom);

            Program.SetScrollPositionById(host.Session, HelpBandId, new Vector2(0f, preserved));
            Program.SetScrollPositionById(host.Session, "content-scroll", new Vector2(0f, 30f));
            string topic = host.Bindings.Get<string>("help-section-key");

            host.Bindings.Set(HelpOpenKey, false);
            UiLayoutSnapshot closed = host.MeasureAndArrange(viewport);
            Assert(!closed.Viewports.ContainsKey(HelpBandId), "the closed arrange must drop the help band");

            host.Bindings.Set(HelpOpenKey, true);
            UiLayoutSnapshot reopened = host.MeasureAndArrange(viewport);
            Assert(reopened.Viewports.ContainsKey(HelpBandId), "the reopened arrange must carry the help band");

            Assert(ReferenceEquals(helpNode, host.Session.GetNodeByElementId(HelpBandId)),
                "the help Scroll must keep its node identity across close/open, or its state would be lost");
            Assert(Math.Abs(Program.ScrollPositionById(host.Session, HelpBandId).y - preserved) < 0.01f,
                viewport + ": the panel's own scroll position set before the toggle must survive it");
            Assert(Math.Abs(Program.ScrollPositionById(host.Session, "content-scroll").y - 30f) < 0.01f,
                viewport + ": the centre column's scroll position must survive the help toggle untouched");
            Assert(string.Equals(host.Bindings.Get<string>("help-section-key"), topic, StringComparison.Ordinal),
                "the selected help topic must survive a close/open cycle: '" + topic + "' -> '"
                + host.Bindings.Get<string>("help-section-key") + "'");

            Console.WriteLine("[bh1-scroll] box " + viewport.x + "x" + viewport.y + " panelHeadroom=" + headroom
                + " centreHeadroom=" + centreHeadroom + " preserved panel.y=" + preserved + " centre.y=30");
        }
    }

    /// <summary>4. Workspace switching while open keeps the panel and follows the section's topic.</summary>
    private static void TabSwitchKeepsThePanelOpen(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        // The shipped default is retracted; this step needs the panel open across the switch.
        host.Bindings.Set(HelpOpenKey, true);
        host.MeasureAndArrange(AcceptancePageBox);
        string before = host.Bindings.Get<string>("help-section-key");

        host.Bindings.Invoke("set-tab", "Packs");
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(AcceptancePageBox);
        Assert(snapshot.Viewports.ContainsKey(HelpBandId),
            "switching workspaces while the panel is open must not close it");
        string after = host.Bindings.Get<string>("help-section-key");
        Assert(!string.Equals(after, before, StringComparison.Ordinal) && after.Length > 0,
            "the help topic must follow the workspace while the panel stays open: '"
            + before + "' -> '" + after + "'");
        Assert(fake.ViewState.HelpPanelOpen, "the panel state must stay open across a tab switch");
    }

    /// <summary>5. Revision-driven invalidation, with the host/session never recreated.</summary>
    private static void ToggleIsRevisionDrivenAndReusesTheHost(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource();
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        UiSession session = host.Session;
        // The shipped default is retracted; start open so each toggle below is a real state change.
        host.Bindings.Set(HelpOpenKey, true);
        host.MeasureAndArrange(AcceptancePageBox);

        int closeRevision = session.ContentRevision;
        host.Bindings.Set(HelpOpenKey, false);
        Assert(session.ContentRevision == closeRevision + 1,
            "the close value write must advance the session revision by exactly one ("
            + closeRevision + " -> " + session.ContentRevision + ")");
        host.DrawChecked(new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y));
        Assert(ReferenceEquals(host.Session, session) && session.IsActive,
            "toggling must not dispose or recreate the host/session");
        Assert(fake.LastHelpPanelOpen == false, "the close write must route through the business boundary");

        int openRevision = session.ContentRevision;
        host.Bindings.Invoke(ToggleCommand);
        Assert(session.ContentRevision == openRevision + 1,
            "the toggle command must advance the session revision by exactly one ("
            + openRevision + " -> " + session.ContentRevision + ")");
        Assert(fake.LastHelpPanelOpen == true, "the toggle command must flip the state through the boundary");
        host.DrawChecked(new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y));
        Assert(ReferenceEquals(host.Session, session) && session.IsActive,
            "the reopen must reuse the same host/session");

        int secondCloseRevision = session.ContentRevision;
        host.Bindings.Invoke(ToggleCommand);
        Assert(session.ContentRevision == secondCloseRevision + 1 && fake.LastHelpPanelOpen == false,
            "a second toggle must close again and advance the revision once more");
    }

    /// <summary>
    /// 6. The body row is arranged at EVERY width the page can be handed, in both help states, with no
    /// horizontal overflow. This is where the retired "defensive full-width band that replaces the body"
    /// used to be probed on a synthetic 800x600 screen: BH1.1 makes replacing the body illegal, so the
    /// same sub-shipping widths now have to keep BOTH the body and the band, stacked. The widths are the
    /// shipped page boxes (760 at the 1024x768 minimum, 920 at 1920x1080 - the same numbers in both help
    /// states, which is the point) plus the two sub-Breakpoint probes that used to stack the three columns.
    /// The retired 1252px "open at 1920x1080" box is gone: no help state produces a wider page any more.
    /// </summary>
    private static void BodyIsNeverReplacedAtAnyWidth(Program.StubMetrics metrics)
    {
        foreach (float width in new[] { 480f, 320f, 760f, 920f })
        {
            foreach (bool open in new[] { true, false })
            {
                var fake = new RecordingSettingsSource { RichData = true };
                using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
                // The shipped default is retracted; set the case's state explicitly either way.
                host.Bindings.Set(HelpOpenKey, open);

                var viewport = new Vector2(width, 524f);
                UiLayoutSnapshot snapshot = host.MeasureAndArrange(viewport);
                host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));

                Assert(snapshot.Viewports.Count > 0, "the page must still arrange scroll containers at " + width);
                Assert(snapshot.RectById.ContainsKey("body-row"),
                    "the settings body must stay arranged at " + width + " (open=" + open + "): BH1.1 forbids"
                    + " a help presentation that replaces it");
                Assert(snapshot.Viewports.ContainsKey("content-scroll")
                        && snapshot.RectById.ContainsKey("nav-column"),
                    "the centre scroll and the nav column must be arranged at " + width + " (open=" + open + ")");

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
                }

                if (open)
                {
                    Assert(snapshot.Viewports.TryGetValue(HelpBandId, out Rect panel),
                        "the open panel must be a real scroll container at " + width);
                    Assert(snapshot.RectById.TryGetValue("body-row", out Rect body),
                        "the body row must be arranged at " + width + " while help is open");
                    Assert(panel.y >= body.yMax - 0.5f,
                        "at " + width + " the panel must sit BELOW the body row, not beside it as a column:"
                        + " body=" + body + " panel=" + panel);
                    Assert(Math.Abs(panel.width - body.width) < 0.5f,
                        "the panel must share the page's inner width with the body at " + width
                        + ": panel=" + panel.width + " body=" + body.width);
                }
                else
                {
                    Assert(!snapshot.Viewports.ContainsKey(HelpBandId),
                        "the closed state must arrange no panel at " + width);
                }

                Assert(host.Session.IsActive, "the session must stay active at " + width + " (open=" + open + ")");
            }
        }
    }

    /// <summary>
    /// 7. The declarative half of the contract: one visibility key on the band, no Tab gate on it, the
    /// single switch declared inside the footer band, and none of the retired presentation machinery
    /// coming back.
    /// </summary>
    private static void ManifestGatesThePanelOnTheOneIntent(Program.StubMetrics metrics)
    {
        using (Stream? stream = typeof(UsKernelSettingsHost).Assembly
            .GetManifestResourceStream("UniversalSqueaker.UI.Layout.Schema2.xml"))
        {
            Assert(stream != null, "the embedded Schema=2 manifest must be readable");
            if (stream == null) return;
            using var reader = new StreamReader(stream);
            UiLayoutManifest manifest = UiLayoutManifest.Parse(reader.ReadToEnd());

            UiElementSpec? band = FindById(manifest.Roots, HelpBandId);
            Assert(band != null, "the shipped manifest must keep the help-scroll band element");
            Assert(!band!.TryGetAttribute("Tab", out _),
                "help-scroll must never carry a Tab attribute: help visibility is independent state, not"
                + " the engine's active-tab gate");
            Assert(band.TryGetAttribute("VisibleKey", out string visibleKey)
                    && string.Equals(visibleKey, HelpOpenKey, StringComparison.Ordinal),
                "the band must be gated by the RAW player intent 'help-open' and nothing else, got '"
                + visibleKey + "'");
            Assert(band.TryGetAttribute("Height", out string height) && height.Length > 0,
                "the band must declare its reservation as a Height (the engine refuses a Fill child that"
                + " pins one, so asking for both is how the footer leaves the page)");
            Assert(!TryDeclared(band, "Fill", out _),
                "the band must not ask to Fill while it pins a Height: " + height + "px of reservation and"
                + " the flexible slot are mutually exclusive");

            UiElementSpec? header = FindById(manifest.Roots, "header-band");
            UiElementSpec? footer = FindById(manifest.Roots, "footer-band");
            Assert(header != null && footer != null, "the page must declare both the header and the footer band");
            Assert(FindById(header!.Children, ToggleElementId) == null,
                "the header must no longer carry the help switch: BH1 moved it into the footer row");
            UiElementSpec? toggle = FindById(footer!.Children, ToggleElementId);
            Assert(toggle != null, "the footer band must declare the single help switch beside the status row");
            Assert(toggle!.TryGetAttribute("ActionBind", out string actionBind)
                    && string.Equals(actionBind, ToggleCommand, StringComparison.Ordinal),
                "the footer switch must bind the panel command, got '" + actionBind + "'");
            Assert(CountActions(footer.Children, ToggleCommand) == 1 && CountActions(manifest.Roots, ToggleCommand) == 1,
                "exactly ONE element in the manifest may bind the panel command");

            Assert(FindById(manifest.Roots, HelpBandId) is { } panel && FindById(panel.Children, "help-panel") != null
                    && FindById(manifest.Roots, "help-band") == null,
                "the single help element keeps the stable id 'help-scroll' with the 'help-panel' child, and"
                + " the defensive full-width band must not come back beside it");
            Assert(FindById(manifest.Roots, "body-row") is { } bodyRow
                    && !bodyRow.TryGetAttribute("VisibleKey", out _),
                "the body row must be unconditional again: BH1 removed the 'body-visible' swap, so a band"
                + " can never replace the settings");
        }

        // Behavioural half: a Tab-driven panel would reappear whenever the workspace matched, so a closed
        // panel must stay closed across every workspace switch.
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.MeasureAndArrange(AcceptancePageBox);
        host.Bindings.Set(HelpOpenKey, false);
        foreach (string tab in new[] { "Overview", "Distance", "Packs", "Tuning", "Presets" })
        {
            host.Bindings.Invoke("set-tab", tab);
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(AcceptancePageBox);
            Assert(!snapshot.Viewports.ContainsKey(HelpBandId),
                "a closed panel must stay closed across workspace '" + tab
                + "' (help visibility is not active-tab)");
        }

        Assert(!fake.ViewState.HelpPanelOpen, "the closed panel state must survive every tab switch");
    }

    /// <summary>
    /// 8. BH1.3's real-input half, driven through the carrier's faithful button stub (hot-control capture
    /// on MouseDown, activation on MouseUp) against the rects the PRODUCTION page arranged:
    ///  - the single switch at the bottom executes the typed command (open -> closed), and
    ///  - a navigation control inside the body still takes a real click while the panel is open, so the
    ///    reservation did not eat the settings' operability.
    /// Both halves need rects, and a hand-drawn switch would show up here as a second recorded button of
    /// the toggle's shape, so "the page has exactly one Help switch" is proved behaviourally too.
    /// </summary>
    private static void BottomSwitchPressRoutesAndSettingsStayOperable(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Tuning");
        host.Bindings.Set(HelpOpenKey, true);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(AcceptancePageBox);
        Assert(snapshot.Viewports.ContainsKey(HelpBandId), "this step needs the panel open at the acceptance box");
        Rect panelRect = snapshot.Viewports[HelpBandId];

        FieldInfo? overrideField = typeof(UiNative).GetField(
            "ButtonOverride", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(overrideField != null, "the harness needs UiNative.ButtonOverride to enumerate the page's buttons");
        if (overrideField == null) return;

        var buttons = new List<Rect>();
        overrideField.SetValue(null, new Func<Rect, bool>(rect =>
        {
            buttons.Add(rect);
            return false;
        }));
        try
        {
            host.DrawChecked(new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y));
        }
        finally
        {
            overrideField.SetValue(null, null);
        }

        // The toggle is the only 128-wide, 26-tall button the page declares (the manifest's own numbers).
        var toggleCandidates = new List<Rect>();
        foreach (Rect rect in buttons)
        {
            if (Math.Abs(rect.width - 128f) < 0.5f && Math.Abs(rect.height - 26f) < 0.5f)
            {
                toggleCandidates.Add(rect);
            }
        }

        Assert(toggleCandidates.Count == 1,
            "exactly ONE drawn control may be the help switch, found " + toggleCandidates.Count
            + " of the declared 128x26 shape in " + buttons.Count + " drawn buttons");
        Rect toggle = toggleCandidates[0];
        // The switch sits in the footer row, i.e. below the band it opens (the drawn rects of a non-scrolled
        // band's children are in the page's own space, so the relation is directly comparable).
        Assert(toggle.y >= panelRect.yMax - 0.5f,
            "the switch must live in the footer row BELOW the panel band it controls: toggle.y=" + toggle.y
            + " toggle.yMax=" + toggle.yMax + " panel.y=" + panelRect.y + " panel.yMax=" + panelRect.yMax);

        // A navigation control inside the body, above the panel: a real click must still route. The nav
        // cards are recorded in the nav column's scroll-content space (the same convention
        // SettingsGeometryLaneTests uses), so the filter compares them against the nav element mapped into
        // that space, and only a real card (>=100 wide, >=40 tall) counts.
        bool hasNavElement = snapshot.RectById.TryGetValue("nav", out Rect navElement);
        bool hasNavViewport = snapshot.Viewports.TryGetValue("nav-column", out Rect navViewport);
        Assert(hasNavElement && hasNavViewport,
            "the nav element and its own scroll viewport must be arranged for the operability half");
        // The nav's own scroll is at its top in this case, so the drawn card rects are the nav element's
        // page rect mapped into the scroll's content space: origin minus the viewport origin.
        Rect navLocal = new(navElement.x - navViewport.x, navElement.y - navViewport.y,
            navElement.width, navElement.height);
        var navCards = new List<Rect>();
        foreach (Rect rect in buttons)
        {
            if (rect.width >= 100f && rect.height >= 40f
                && rect.x >= navLocal.x - 0.5f && rect.xMax <= navLocal.xMax + 0.5f
                && rect.y >= navLocal.y - 0.5f && rect.yMax <= navLocal.yMax + 0.5f)
            {
                navCards.Add(rect);
            }
        }

        Assert(navCards.Count >= 2,
            "the nav column must draw real clickable cards inside the body while help is open, got "
            + navCards.Count);

        ResetWrites(fake);
        // SA1.4 interaction: the footer inset costs the help-open body 12px, so the LAST nav card can
        // sit below the fold at top scroll. The operability claim needs a card the player can really
        // click WITHOUT scrolling: take the bottom-most FULLY visible card that is not the current tab
        // (the nav draws Workspaces top-to-bottom), and expect the tab it names.
        string[] workspaceOrder = { "Overview", "Distance", "Packs", "Tuning", "Presets" };
        int pick = -1;
        Rect card = default;
        for (int i = navCards.Count - 1; i >= 0; i--)
        {
            Rect candidate = navCards[i];
            if (i >= workspaceOrder.Length) continue;
            if (string.Equals(workspaceOrder[i], "Tuning", StringComparison.Ordinal)) continue;
            if (navViewport.y + candidate.y >= navViewport.y - 0.5f
                    && navViewport.y + candidate.yMax <= navViewport.yMax + 0.5f)
            {
                pick = i;
                card = candidate;
                break;
            }
        }
        Assert(pick >= 0,
            "at least one non-current nav card must be fully visible at the top scroll while help is"
            + " open (navViewport " + navViewport.y + ".." + navViewport.yMax + ")");
        string expectedTab = workspaceOrder[pick];
        // The recorded card rect is in the nav scroll's CONTENT space; the pointer the stub sees is in the
        // page's space, so it converts through the nav viewport (the same pair SettingsGeometryLaneTests
        // uses). The nav is at its top here, so no scroll offset enters the conversion.
        Assert(Math.Abs(Program.ScrollPositionById(host.Session, "nav-column").y) < 0.01f,
            "the operability click assumes the nav scroll is at its top");
        var pointer = new Vector2(navViewport.x + card.x + card.width * 0.5f,
            navViewport.y + card.y + card.height * 0.5f);
        Program.DrawWithEvent(host, new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y),
            EventType.MouseDown, pointer);
        Program.DrawWithEvent(host, new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y),
            EventType.MouseUp, pointer);
        Assert(string.Equals(fake.LastActiveTab, expectedTab, StringComparison.Ordinal),
            "a navigation click must still route to the business boundary while the panel is open, got '"
            + (fake.LastActiveTab ?? "(none)") + "' for the " + expectedTab + " card (pointer "
            + pointer.x + "," + pointer.y + "; card " + card.x + "," + card.y + " " + card.width
            + "x" + card.height + "; navViewport " + navViewport.x + "," + navViewport.y + " "
            + navViewport.width + "x" + navViewport.height + ")");
        Assert(string.Equals(fake.ViewState.ActiveTab, expectedTab, StringComparison.Ordinal),
            "the routed click must land in the page state, so the workspace really moved");
        Assert(host.MeasureAndArrange(AcceptancePageBox).Viewports.ContainsKey(HelpBandId),
            "operating a settings control must not close the panel");

        ResetWrites(fake);
        var togglePointer = new Vector2(toggle.x + toggle.width * 0.5f, toggle.y + toggle.height * 0.5f);
        Program.DrawWithEvent(host, new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y),
            EventType.MouseDown, togglePointer);
        Program.DrawWithEvent(host, new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y),
            EventType.MouseUp, togglePointer);
        Assert(fake.LastHelpPanelOpen == false,
            "pressing the bottom switch must execute the typed panel command (open -> closed)");
        Assert(!fake.ViewState.HelpPanelOpen, "the switch's write must land in the per-window page state");
        Assert(!host.MeasureAndArrange(AcceptancePageBox).Viewports.ContainsKey(HelpBandId),
            "the pressed switch must retract the band on the next arrange");

        Console.WriteLine("[bh1-switch] drawn buttons=" + buttons.Count + " toggle=" + toggle
            + " navCards=" + navCards.Count + " card=" + card + " -> click routed while open");
    }

    /// <summary>
    /// 9. The panel is per-window VIEW state and is never persisted.
    ///  - the Settings Scribe boundary and the Mod save path carry no panel member at all, which is why
    ///    this outcome leaves the migration/config-copy suites untouched;
    ///  - every panel identifier in the production source lives on the four UI view-state files (BH1
    ///    dropped the settings window from that set: it no longer reads the state);
    ///  - runtime: the shipped default is retracted; Reset and the production reopen path of a fresh
    ///    source + host both answer retracted again, while an explicit open installs the declared band.
    /// </summary>
    private static void PanelIsPerWindowViewStateAndNeverPersisted(Program.StubMetrics metrics)
    {
        string root = RepoRoot();

        // ---- Persistence layer: the settings Scribe boundary and the Mod save path carry no panel member.
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
            foreach (string token in PanelStateTokens)
            {
                Assert(CountToken(text, token) == 0,
                    "the settings/save path must not carry panel visibility, but '" + token + "' appears in "
                    + Relative(root, file));
            }
        }

        // Positive control: a scanner that found nothing because it read nothing must not pass.
        Assert(CountToken(StripComments("public bool HelpPanelOpen = true;"), "HelpPanelOpen") == 1,
            "positive control: the persistence scanner must flag a planted panel member");
        Assert(CountToken(StripComments("var key = \"toggle-help-drawer\";"), "toggle-help-drawer") == 1,
            "positive control: the persistence scanner must flag a planted panel binding key");

        // ---- The owner set: the state never leaks out of the UI view layer. Settings semantics stay
        // owned by the migration/config-copy suites; this lane deliberately does not modify them.
        var owners = new List<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Source", "UniversalSqueaker"), "*.cs", SearchOption.AllDirectories))
        {
            string text = StripComments(File.ReadAllText(file));
            if (TokensPresent(text, PanelStateTokens).Count > 0)
            {
                owners.Add(Relative(root, file));
            }
        }

        owners.Sort(StringComparer.Ordinal);
        var expectedOwners = new List<string>(PanelStateOwners);
        expectedOwners.Sort(StringComparer.Ordinal);
        bool sameOwners = owners.Count == expectedOwners.Count;
        for (int i = 0; sameOwners && i < owners.Count; i++)
        {
            sameOwners = string.Equals(owners[i], expectedOwners[i], StringComparison.Ordinal);
        }

        Assert(sameOwners,
            "panel view state must live only on the page's ephemeral state + its UI boundary and host;"
            + " found: " + string.Join(" | ", owners));

        // ---- Runtime reopen 1: the page state's own Reset (the session begin/end reset). The shipped
        // default is RETRACTED (task-10: the window opens narrow), so the fresh page and Reset both
        // answer retracted, while an explicit open must still install the declared band.
        var fake = new RecordingSettingsSource();
        using (UiHost host = UsKernelSettingsHost.Create(fake, metrics))
        {
            UiLayoutSnapshot initial = host.MeasureAndArrange(AcceptancePageBox);
            Assert(!initial.Viewports.ContainsKey(HelpBandId),
                "the window must open with the panel retracted (the narrow default)");
            Assert(!fake.ViewState.HelpPanelOpen, "the page state must default to retracted");

            host.Bindings.Set(HelpOpenKey, true);
            UiLayoutSnapshot open = host.MeasureAndArrange(AcceptancePageBox);
            Assert(open.Viewports.ContainsKey(HelpBandId)
                    && Math.Abs(open.Viewports[HelpBandId].height - DeclaredBandHeight(host)) < 0.01f,
                "the open write must install the declared help band");

            host.Bindings.Set(HelpOpenKey, false);
            UiLayoutSnapshot closed = host.MeasureAndArrange(AcceptancePageBox);
            Assert(!closed.Viewports.ContainsKey(HelpBandId), "the close write must retract the panel");
            Assert(!fake.ViewState.HelpPanelOpen, "the close write must land in the per-window view state");

            fake.ViewState.Reset();
            Assert(!fake.ViewState.HelpPanelOpen,
                "Reset must restore the default-RETRACTED panel: visibility is per-window view state, not a setting");
            Assert(!host.Bindings.Get<bool>(HelpOpenKey),
                "the help-open binding must read the reset default, so a reopened session starts retracted");
            host.Bindings.Invoke("set-tab", "Overview");
            UiLayoutSnapshot reset = host.MeasureAndArrange(AcceptancePageBox);
            Assert(!reset.Viewports.ContainsKey(HelpBandId),
                "after Reset, a real revision-bumping write must keep the panel retracted");
        }

        // ---- Runtime reopen 2 (what the window really does): closing disposes the host and its source;
        // reopening builds a fresh pair, so the retracted default is what a player sees again.
        var fresh = new RecordingSettingsSource();
        Assert(fresh.LastHelpPanelOpen == null, "a reopened window's source must start with no panel write");
        using (UiHost reopened = UsKernelSettingsHost.Create(fresh, metrics))
        {
            UiLayoutSnapshot snapshot = reopened.MeasureAndArrange(AcceptancePageBox);
            Assert(!snapshot.Viewports.ContainsKey(HelpBandId),
                "a reopened window must build the panel retracted: visibility is view state, never persisted");
            Assert(!fresh.ViewState.HelpPanelOpen, "the reopened window's page state must default retracted");
        }

        Console.WriteLine("[bh1-persist] settings/save files scanned=" + persistenceFiles.Count
            + " settings/save panel tokens=0; panel owners=" + owners.Count + " (" + string.Join(", ", owners)
            + "); Reset reopened=retracted, fresh-source reopen=retracted");
    }

    /// <summary>
    /// 10. Combined state: the panel scroll is really scrolled, a real workspace switch keeps the panel
    /// open and moves the topic, then close + reopen keeps the topic and BOTH scroll positions with
    /// exactly one revision bump per toggle. The switch itself resets both page scrolls through the
    /// Host's ResetScroll (existing semantics), so the lane re-seeds them after it.
    /// </summary>
    private static void CombinedScrollTabCloseReopenKeepsState(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        UiSession session = host.Session;
        // The STACKED narrow box on purpose: the reservation is a declared height rather than a share of the
        // box, so only a narrow box wraps the hover-invariant panel content past the band and gives BOTH
        // scrolls headroom big enough for a 40/24px preserved position to mean something. The shipped
        // 760x524 box is covered by step 3 with the smaller offset its own headroom allows; neither input
        // stands in for the other, and neither is presented as the other's appearance.
        var viewport = new Vector2(320f, 524f);
        host.Bindings.Set(HelpOpenKey, true);
        UiLayoutSnapshot initial = host.MeasureAndArrange(viewport);
        Assert(initial.Viewports.ContainsKey(HelpBandId), "the combined-state lane needs the panel open");

        Program.SetScrollPositionById(session, HelpBandId, new Vector2(0f, 40f));
        Program.SetScrollPositionById(session, "content-scroll", new Vector2(0f, 24f));
        Assert(Math.Abs(Program.ScrollPositionById(session, HelpBandId).y - 40f) < 0.01f
                && Math.Abs(Program.ScrollPositionById(session, "content-scroll").y - 24f) < 0.01f,
            "the lane must really move both page scrolls before it asserts preservation");

        string beforeTopic = host.Bindings.Get<string>("help-section-key");
        host.Bindings.Invoke("set-tab", "Tuning");
        UiLayoutSnapshot switched = host.MeasureAndArrange(viewport);
        Assert(switched.Viewports.ContainsKey(HelpBandId), "a workspace switch must not close the panel");
        Assert(fake.ViewState.HelpPanelOpen, "the panel state must stay open across a workspace switch");
        Assert(Math.Abs(Program.ScrollPositionById(session, HelpBandId).y) < 0.01f
                && Math.Abs(Program.ScrollPositionById(session, "content-scroll").y) < 0.01f,
            "a workspace switch resets both page scrolls to top by design (SessionRevisionBumper.ResetScroll)");

        string topic = host.Bindings.Get<string>("help-section-key");
        Assert(!string.Equals(topic, beforeTopic, StringComparison.Ordinal),
            "the help topic must follow the switched workspace: '" + beforeTopic + "' -> '" + topic + "'");
        Assert(string.Equals(topic, fake.SectionHelpKey(fake.ViewState.ActiveSectionKey), StringComparison.Ordinal),
            "the open panel must show the active section's help: topic '" + topic + "' vs active section '"
            + fake.ViewState.ActiveSectionKey + "'");

        float helpHeadroom = switched.ScrollContents[HelpBandId].height - switched.Viewports[HelpBandId].height;
        float contentHeadroom = switched.ScrollContents["content-scroll"].height
            - switched.Viewports["content-scroll"].height;
        Assert(helpHeadroom >= 40f, "the lane needs real panel headroom after the switch, got " + helpHeadroom);
        Assert(contentHeadroom >= 24f, "the lane needs real centre headroom after the switch, got " + contentHeadroom);

        Program.SetScrollPositionById(session, HelpBandId, new Vector2(0f, 40f));
        Program.SetScrollPositionById(session, "content-scroll", new Vector2(0f, 24f));

        int closeRevision = session.ContentRevision;
        host.Bindings.Set(HelpOpenKey, false);
        Assert(session.ContentRevision == closeRevision + 1,
            "the close write must advance the session revision by exactly one ("
            + closeRevision + " -> " + session.ContentRevision + ")");
        UiLayoutSnapshot closed = host.MeasureAndArrange(viewport);
        Assert(!closed.Viewports.ContainsKey(HelpBandId), "the closed arrange must drop the band");

        int reopenRevision = session.ContentRevision;
        host.Bindings.Set(HelpOpenKey, true);
        Assert(session.ContentRevision == reopenRevision + 1,
            "the reopen write must advance the session revision by exactly one ("
            + reopenRevision + " -> " + session.ContentRevision + ")");
        UiLayoutSnapshot reopened = host.MeasureAndArrange(viewport);
        Assert(reopened.Viewports.ContainsKey(HelpBandId), "the reopened arrange must carry the band");

        Assert(string.Equals(host.Bindings.Get<string>("help-section-key"), topic, StringComparison.Ordinal),
            "close/reopen must keep the topic the workspace switch selected: '" + topic + "'");
        Assert(Math.Abs(Program.ScrollPositionById(session, HelpBandId).y - 40f) < 0.01f,
            "close/reopen must keep the panel's own scroll position");
        Assert(Math.Abs(Program.ScrollPositionById(session, "content-scroll").y - 24f) < 0.01f,
            "close/reopen must keep the centre content scroll position");
        Assert(ReferenceEquals(host.Session, session) && session.IsActive && fake.ViewState.HelpPanelOpen,
            "the combined close/reopen must leave the same live session, panel open");

        Console.WriteLine("[bh1-combined] tab switch -> topic=" + topic + " panelHeadroom=" + helpHeadroom
            + " contentHeadroom=" + contentHeadroom + "; close/reopen kept topic, panel.y=40, content.y=24,"
            + " revision +1 per toggle");
    }

    /// <summary>
    /// 11. FL-first statement for the panel path, per the brief's hard rule and its "Explicit FL
    /// exception" section: no direct GUI/Widgets/Event/manual scroll-view/input-routing call in any
    /// panel-path file, the panel flows through UsKernelDraw/UiThemeDraw/UiNative and the typed binding
    /// table, and the only direct backend call in the whole production source is the documented
    /// camera-indicator exception.
    /// </summary>
    private static void PanelPathIsFlFirst()
    {
        string root = RepoRoot();
        string[] panelPaths =
        {
            "Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs",
            "Source/UniversalSqueaker/UI/Kernel/UsFooterWidget.cs",
            "Source/UniversalSqueaker/UI/Kernel/UsHelpPanelWidget.cs",
            "Source/UniversalSqueaker/UI/Kernel/UsPageTitleWidget.cs",
            "Source/UniversalSqueaker/UI/Layout.Schema2.xml",
        };

        foreach (string relative in panelPaths)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert(File.Exists(path), "the FL-first scan needs " + relative);
            List<string> hits = TokensPresent(StripComments(File.ReadAllText(path)), ForbiddenDirectUiTokens);
            Assert(hits.Count == 0,
                relative + " must stay FL-first: found direct " + string.Join(", ", hits)
                + " (the panel path goes through UsKernelDraw/UiNative/UiThemeDraw only)");
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

        // The title band draws only through the FL primitives and owns no switch at all: since BH1 the
        // switch is the CORE input/button the manifest declares inside the footer row, so a Button call
        // reappearing in either widget file means a second, hand-drawn switch is back.
        string title = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsPageTitleWidget.cs"));
        Assert(CountToken(title, "UsKernelDraw.Label(") >= 1
                && CountToken(title, "UiThemeDraw.SectionBand(") >= 1,
            "the title band must draw through UsKernelDraw and UiThemeDraw");
        Assert(CountToken(title, "UsKernelDraw.SelectionButton(") == 0 && CountToken(title, "UiNative.Button(") == 0,
            "the page-title widget must draw no switch of its own: the help toggle is the declared"
            + " input/button in the footer band");
        string footerWidget = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsFooterWidget.cs"));
        Assert(CountToken(footerWidget, "UiNative.Button(") == 0
                && CountToken(footerWidget, "UsKernelDraw.SelectionButton(") == 0,
            "the footer widget must stay non-interactive: the switch is a declared element, not a band it draws");

        string manifestText = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml"));
        Assert(CountToken(manifestText, "ActionBind=\"" + ToggleCommand + "\"") == 1,
            "exactly ONE manifest element may be the help switch (found "
            + CountToken(manifestText, "ActionBind=\"" + ToggleCommand + "\"") + "); a second one is a second"
            + " switch on the page");
        Assert(CountToken(manifestText, "VisibleKey=\"" + HelpOpenKey + "\"") == 1,
            "exactly ONE element may be gated by the raw player intent 'help-open' (found "
            + CountToken(manifestText, "VisibleKey=\"" + HelpOpenKey + "\"") + ")");

        string hostSource = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsHost.cs"));
        Assert(CountToken(hostSource, "UiNative.") >= 1,
            "the host must route its native trace/hit surface through UiNative");
        Assert(CountToken(hostSource, "\"" + HelpOpenKey + "\"") >= 1
                && CountToken(hostSource, "\"" + ToggleCommand + "\"") >= 1,
            "the panel toggle must go through the typed binding table, never a raw event handler");

        // The retired presentation machinery must be gone from the CODE of both halves, and the two files
        // that used to own the width question must not re-introduce it: a page-width feed with no derived key
        // left to feed is the second truth BH1 removed. The scan strips comments first on purpose - this file
        // and the manifest both NAME the retired keys while explaining what BH1 deleted, and a documentation
        // mention is not a wired binding (the same stripper the FL-first scan above is calibrated with).
        string manifestCode = StripComments(manifestText);
        string hostCode = StripComments(hostSource);
        foreach (string retired in RetiredPresentationKeys)
        {
            Assert(CountToken(manifestCode, retired) == 0,
                "the retired key/element '" + retired + "' must not come back as manifest code");
            Assert(CountToken(hostCode, retired) == 0,
                "the retired key '" + retired + "' must not come back in the Host binding table");
        }

        string windowSource = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UniversalSqueakerSettingsWindow.cs"));
        Assert(CountToken(windowSource, "pageWidthFeed") == 0 && CountToken(windowSource, "ApplyDrawerWidth") == 0,
            "the settings window must stop feeding a page-width decision it no longer owns");
        Assert(CountToken(hostSource, "pageWidthFeed") == 0,
            "the Host must not keep a page-width feed parameter with no derived key left to decide");
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

        Console.WriteLine("[bh1-flfirst] panel files scanned=" + panelPaths.Length + " direct-call hits=0;"
            + " production direct-call offenders=" + offenders.Count + " [" + string.Join(" | ", offenders) + "]");
    }

    /// <summary>
    /// The band's declared reservation, read from the REAL embedded manifest the Host parsed - never
    /// restated as a constant in this lane (a number written here could disagree with the ship shape and
    /// still pass).
    /// </summary>
    private static float DeclaredBandHeight(UiHost host)
    {
        UiElementSpec? band = FindById(host.Manifest.Roots, HelpBandId);
        Assert(band != null, "the help band must exist in the embedded manifest the Host parsed");
        if (!band!.TryGetAttribute("Height", out string raw))
        {
            throw new InvalidOperationException(
                "the help band must declare a Height in the embedded manifest");
        }

        return float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>page-root's declared gap: the spacing the reservation costs the body row.</summary>
    private static float DeclaredPageRootGap(UiHost host)
    {
        UiElementSpec? pageRoot = FindById(host.Manifest.Roots, "page-root");
        Assert(pageRoot != null, "page-root must exist in the embedded manifest");
        if (!pageRoot!.TryGetAttribute("Gap", out string raw))
        {
            throw new InvalidOperationException(
                "page-root must declare its Gap (the reservation costs one of these)");
        }

        return float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool TryDeclared(UiElementSpec spec, string attribute, out string value)
    {
        // Fill="true" is the only spelling the engine accepts, and a declared-but-refused pair is exactly
        // the failure this guard exists for.
        if (!spec.TryGetAttribute(attribute, out value)) return false;
        return !string.Equals(value.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountActions(IReadOnlyList<UiElementSpec> specs, string command)
    {
        int count = 0;
        foreach (UiElementSpec spec in specs)
        {
            if (spec.TryGetAttribute("ActionBind", out string bound)
                && string.Equals(bound.Trim(), command, StringComparison.Ordinal))
            {
                count++;
            }

            count += CountActions(spec.Children, command);
        }

        return count;
    }

    /// <summary>
    /// Clears the fixture's recorded writes so the click below cannot be credited to an earlier binding
    /// call in the same host (assert on the increment, not the accumulator).
    /// </summary>
    private static void ResetWrites(RecordingSettingsSource fake)
    {
        fake.LastActiveTab = null;
        fake.LastHelpPanelOpen = null;
        fake.LastTuningLayer = null;
        fake.LastScrollToSection = null;
    }

    /// <summary>
    /// The reservation must not cost the footer its own truth (BH1.1 / BH1.4): with help OPEN the build
    /// identity, the save-status sentence and the switch all fit the footer row in both shipped language
    /// tables - for the fixture's short identity AND for a deliberately long full identity, and for each
    /// of the idle / saving / saved / failed / unknown states, dirty or not. The status inputs are
    /// SYNTHETIC through the fixture's own knobs: this is not an in-game save failure and is not claimed
    /// as one, it is the input that makes the failure sentence's band measurable.
    ///
    /// <para>
    /// The instrument is the real Host: the rects come from the arrangement, the text is measured by the
    /// SAME calibrated StubMetrics the layout uses (attaching the audit to a different ruler is the
    /// two-metrics defect this repository already paid for), and the fit audit is reset+cleared
    /// immediately before the measured draw, so a finding here is this case's, not another lane's.
    /// Mutation this is evidence for: pinning a Height on the footer band (the pre-S3-3 shape, and the
    /// exact spelling the 2026-09-14/15 in-game log reported as "footer needs 33px has 28px") reddens the
    /// long-identity cases in both languages.
    /// </para>
    /// </summary>
    private static void FooterCarriesIdentityAndEverySaveStatusWithHelpOpen(Program.StubMetrics metrics)
    {
        // Every width used below is read out of the embedded manifest per case (the switch's Width, the
        // footer row's Gap, page-root's Padding): a constant restated in this lane is a number that can
        // drift from the ship shape and still pass.

        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            foreach (string identity in new[]
                     {
                         "test-build",
                         "0.7.1+universal-squeaker-empty-xenotype-20261004-a-deliberately-long-full-build-identity",
                     })
            {
                foreach (string status in new[] { "Idle", "Saving", "Saved", "Failed", "Unknown" })
                {
                    foreach (bool dirty in new[] { false, true })
                    {
                        var fake = new RecordingSettingsSource
                        {
                            RichData = true,
                            BuildIdentityOverride = identity,
                            SaveStatusOverride = status,
                            IsDirtyOverride = dirty,
                            SaveStatusVisibleOverride = status != "Idle",
                        };
                        var reports = new List<UiOverflowReport>();
                        UiFitAudit.Attach(metrics, reports.Add);
                        UiFitAudit.Enabled = true;
                        string where = language + " identity=" + (identity.Length > 20 ? "long" : "short")
                            + " status=" + status + " dirty=" + dirty;
                        try
                        {
                            using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
                            host.Bindings.Set(HelpOpenKey, true);
                            UiLayoutSnapshot snapshot = host.MeasureAndArrange(AcceptancePageBox);
                            float pageRootPadding = DeclaredAttribute(host, "page-root", "Padding");
                            float declaredToggleWidth = DeclaredAttribute(host, ToggleElementId, "Width");
                            float declaredFooterGap = DeclaredAttribute(host, "footer-band", "Gap");
                            // SA1.4: the footer band now carries its own Padding, so the width left to the
                            // status half is the page inner MINUS the band's two padding edges.
                            float footerBandPadding = DeclaredAttribute(host, "footer-band", "Padding");
                            Assert(footerBandPadding >= 4f,
                                where + ": SA1.4 - the footer band must declare a real inset (>=4), got "
                                + footerBandPadding);
                            float footerBandInnerWidth = AcceptancePageBox.x - pageRootPadding * 2f
                                - footerBandPadding * 2f;
                            UiFitAudit.Reset();
                            reports.Clear();
                            host.DrawChecked(new Rect(0f, 0f, AcceptancePageBox.x, AcceptancePageBox.y));

                            Assert(snapshot.RectById.TryGetValue("footer", out Rect footerRect),
                                where + ": the footer row must arrange the status widget");
                            Assert(snapshot.RectById.TryGetValue(ToggleElementId, out Rect toggleRect),
                                where + ": the footer row must arrange the switch");
                            Assert(snapshot.Viewports.ContainsKey(HelpBandId),
                                where + ": the open panel band must still be arranged beside them");
                            Assert(Math.Abs(footerRect.width
                                    - (footerBandInnerWidth - declaredToggleWidth - declaredFooterGap)) < 0.5f,
                                where + ": the switch must cost the status text exactly its declared width"
                                + " plus the row gap, got footer=" + footerRect.width);
                            Assert(toggleRect.x >= footerRect.xMax - 0.5f
                                    && toggleRect.xMax
                                        <= footerBandInnerWidth + footerBandPadding + pageRootPadding + 0.5f,
                                where + ": the switch must sit to the right of the status text inside the"
                                + " page (past the band's own SA1.4 padding): footer=" + footerRect
                                + " toggle=" + toggleRect);
                            Assert(Math.Abs(toggleRect.height - 26f) < 0.01f,
                                where + ": the switch keeps its declared 26px band inside the wrap-aware"
                                + " footer row, got " + toggleRect.height);
                            Assert(reports.Count == 0,
                                where + ": the footer row and the open panel must draw with no fit finding;"
                                + " got " + string.Join(" | ", System.Linq.Enumerable.Select(reports,
                                    r => r.ElementPath + " " + r.Axis + " needs " + r.Needed + " has "
                                        + r.Available)));

                            if (identity == "test-build" && status == "Failed" && !dirty
                                && language == "English")
                            {
                                Console.WriteLine("[bh1-footer] footer=" + footerRect.width + "x"
                                    + footerRect.height + " toggle=" + toggleRect
                                    + " panel=" + snapshot.Viewports[HelpBandId].height
                                    + " status band measured with help open (EN/ZH, short+long identity,"
                                    + " idle/saving/saved/failed/unknown, dirty both ways)");
                            }
                        }
                        finally
                        {
                            UiFitAudit.Detach();
                            UiFitAudit.Enabled = false;
                            reports.Clear();
                        }
                    }
                }
            }
        }
    }
    /// <summary>A numeric manifest declaration, read from the embedded document the Host parsed.</summary>
    private static float DeclaredAttribute(UiHost host, string elementId, string attribute)
    {
        UiElementSpec? element = FindById(host.Manifest.Roots, elementId);
        Assert(element != null, "the embedded manifest must declare the element " + elementId);
        if (!element!.TryGetAttribute(attribute, out string raw))
        {
            throw new InvalidOperationException(
                "the manifest must declare " + attribute + " on " + elementId + "; this lane reads the"
                + " declaration instead of restating it");
        }

        return float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
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
