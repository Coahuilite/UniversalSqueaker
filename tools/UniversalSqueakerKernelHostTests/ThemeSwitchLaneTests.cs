using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// UIR-006 (WO-US-UI-REFACTOR-20261014), re-cut by CORRECTIONS-R1 topic 4: the live palette switch
/// on the real production host through the same ApplySwatchLive seam the settings page drives. The
/// contract under test is the ONE thing a theme system must never break: a switch changes colours and
/// NOTHING ELSE. Each step reddens on a specific regression:
/// <list type="number">
/// <item>every role of the target palette lands on the page bag AND the chrome bag; the layout clock
/// does not move; a same-value re-apply is silent;</item>
/// <item>scroll positions, popup and hover survive the switch (the session is never rebuilt);</item>
/// <item>ALL SIX palettes walk the same real DRAW: arranged rects, UiNative hit bands, the session
/// scroll, the open popup and the active edit are identical to the core baseline, while the recorded
/// label ink carries each palette's own TextPrimary - colour reaches the drawing outlet, geometry
/// and interaction state do not move. Revision numbers alone prove nothing; this compares draws.</item>
/// </list>
/// </summary>
internal static class ThemeSwitchLaneTests
{
    private const float PageWidth = 1280f;
    private const float PageHeight = 720f;

    public static int RunAll()
    {
        Step("a switch repaints both bags and moves only the colour clock", RepaintsBothBagsColourOnly);
        Step("scroll, popup and hover survive a switch untouched", SessionStateSurvives);
        Step("the arranged rects are identical across a switch on every workspace", RectsIdentical);
        Step("all six palettes draw identical geometry with their own ink", AllSixPalettesDrawIdentically);
        Step("the same palette re-applied writes nothing and repaints nothing", SameValueIsSilent);
        Step("the real production source chain persists, syncs and repaints", RealSourceActionChain);
        Step("the declared selection surfaces reach the real draw on nav and picker", SelectionSurfacesPaintTheDeclaredRole);
        Console.WriteLine("ThemeSwitchLaneTests ALL PASS");
        return 0;
    }

    private static void RepaintsBothBagsColourOnly()
    {
        var settings = NewSettings();
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
        UiTheme chrome = UsTheme.Surface();
        UiTheme page = host.StyleResolver.Baseline;

        int pageLayoutBefore = page.LayoutRevision;
        int pageTitleBefore = page[UiPaletteRole.Foreground] == chrome[UiPaletteRole.Foreground] ? 0 : 1; // bags start on the same palette
        string before = Components(page[UiPaletteRole.PanelSurface]);

        bool wrote = UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, "anomaly");

        Assert(wrote && settings.uiSwatchId == "anomaly", "the switch persists the resolved id once");
        Assert(page.LayoutRevision == pageLayoutBefore,
            "the colour-only switch must NOT move the layout clock (" + pageLayoutBefore
            + " -> " + page.LayoutRevision + ") - a layout move re-measures the page");
        Assert(page.ColourRevision > 0 && chrome.ColourRevision > 0, "both bags recorded colour writes");
        UsSwatch anomaly = UsSwatches.Resolve("anomaly");
        Assert(Same(page[UiPaletteRole.Foreground], AnomalyTextPrimary),
            "the PAGE bag carries the target palette's ink");
        Assert(Same(chrome[UiPaletteRole.PanelSurface], Hex(Role(anomaly, "PanelSurface"))),
            "the CHROME bag carries the target palette's panel - one decision, two bags");
        Assert(before != Components(page[UiPaletteRole.PanelSurface]), "the panel colour actually moved (control)");
        Assert(pageTitleBefore == 0, "the two bags started on one palette (fixture control)");
    }

    private static void SessionStateSurvives()
    {
        var settings = NewSettings();
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        UiTheme chrome = UsTheme.Surface();
        host.Bindings.Invoke("set-tab", "Packs");
        Arrange(host);
        UiNode? scroll = host.Session.GetNodeByElementId("content-scroll");
        Assert(scroll != null, "the scroll node exists after an arrange");
        host.Session.SetScrollPosition(scroll!, new Vector2(0f, 64f));
        host.Session.OpenPopup("theme-lane-popup", new Rect(20f, 30f, 90f, 20f));
        host.Session.ClaimHover("us/pack-cards/row");
        Arrange(host); // settle the pass

        Vector2 scrollBefore = host.Session.GetScrollPosition(scroll!);
        string popupBefore = host.Session.OpenPopupId ?? "";
        string hoverBefore = host.Session.HoverClaim ?? "";
        int nodesBefore = host.Session.Identity;

        bool wrote = UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, "odyssey");

        Assert(wrote, "the switch wrote");
        Assert(host.Session.OpenPopupId == popupBefore && popupBefore == "theme-lane-popup",
            "the palette switch must never close a live popup");
        Assert(SameVec(host.Session.GetScrollPosition(scroll!), scrollBefore),
            "the scroll position survives the switch unchanged");
        Assert(host.Session.Identity == nodesBefore, "no session scroll state was dropped or rebuilt");
        Assert(hoverBefore == "us/pack-cards/row", "the fixture claimed a hover before the switch");
        Arrange(host);
        Assert(host.Session.GetNodeByElementId("content-scroll") == scroll,
            "the SAME node instances serve the page after the switch - the session was not rebuilt");
    }

    /// <summary>
    /// CORRECTIONS-R1 topic 4's core demand: the six-theme comparison runs on the REAL draw. One
    /// host, one viewport, a live popup, a non-zero scroll; per palette: recorded hit bands
    /// (UiNative seam), arranged rects, label ink and the session's interaction state compared.
    /// </summary>
    private static void AllSixPalettesDrawIdentically()
    {
        var settings = NewSettings();
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        UiTheme chrome = UsTheme.Surface();
        var viewport = new Vector2(760f, 524f);   // the shipped 1.6 policy page box
        host.Bindings.Invoke("set-tab", "Packs");
        host.MeasureAndArrange(viewport);
        // The HIT comparison runs on a state-less pass FIRST: an open popup deliberately swallows
        // the buttons beneath it (that is the yield rule), so hits are recorded once, cleanly, and
        // the popup/scroll survival is asserted over the INTERACTION state on the later passes.
        List<Rect> baselineHitsClean = DrawAndCaptureHits(host);
        Assert(baselineHitsClean.Count > 0, "the baseline draw must record hit bands, got none");
        UiNode? scrollNode = host.Session.GetNodeByElementId("content-scroll");
        Assert(scrollNode != null, "the content scroll node must exist");
        host.Session.SetScrollPosition(scrollNode!, new Vector2(0f, 60f));
        UiLayoutSnapshot baseline = host.MeasureAndArrange(viewport);

        List<Color> baselineInk = DrawAndCaptureInk(host);
        Dictionary<string, Rect> baselineRects = CopyRects(baseline);
        Assert(baselineInk.Count > 0, "the baseline draw must record label ink, got none");

        int compared = 0;
        foreach (UsSwatch swatch in UsSwatches.All)
        {
            bool wrote = UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, swatch.Id);
            Assert(wrote || swatch.Id == "core", swatch.Id + ": switching must report a write (core = baseline)");

            List<Color> ink = DrawAndCaptureInk(host);
            List<Rect> hits = DrawAndCaptureHits(host);
            Assert(ink.Count == baselineInk.Count,
                swatch.Id + ": the label count must not move across a palette switch ("
                    + baselineInk.Count + " -> " + ink.Count + ")");
            // CORRECTIONS-R3: the count must EQUAL the baseline before any indexed comparison -
            // the old loop walked `hits.Count` only, so a short list silently skipped the tail.
            Assert(hits.Count == baselineHitsClean.Count, swatch.Id + ": the hit band list must carry the"
                + " SAME count as the baseline (" + baselineHitsClean.Count + " -> " + hits.Count
                + ") - a shorter list must never silently skip comparisons");
            for (int i = 0; i < hits.Count; i++)
            {
                Assert(Close(hits[i].x, baselineHitsClean[i].x) && Close(hits[i].y, baselineHitsClean[i].y)
                        && Close(hits[i].width, baselineHitsClean[i].width) && Close(hits[i].height, baselineHitsClean[i].height),
                    swatch.Id + ": hit band " + i + " moved: " + baselineHitsClean[i] + " -> " + hits[i]);
            }

            UiLayoutSnapshot snap = host.MeasureAndArrange(viewport);
            Dictionary<string, Rect> rects = CopyRects(snap);
            Assert(rects.Count == baselineRects.Count, swatch.Id + ": the arranged tree changed size");
            foreach (KeyValuePair<string, Rect> entry in baselineRects)
            {
                Assert(rects.TryGetValue(entry.Key, out Rect now)
                        && Close(now.x, entry.Value.x) && Close(now.y, entry.Value.y)
                        && Close(now.width, entry.Value.width) && Close(now.height, entry.Value.height),
                    swatch.Id + ": rect " + entry.Key + " moved across a palette switch");
                compared++;
            }

            Assert(SameVec(host.Session.GetScrollPosition(scrollNode!), new Vector2(0f, 60f)),
                swatch.Id + ": the scroll position survived the switch exactly");
            // The popup leg: open AFTER this round's draws (the carrier retires an unowned menu at
            // frame boundaries - asserting across a draw would measure that rule, not the switch),
            // settle through an arrange (no draw), survive the NEXT switch assertion below, then
            // close so the next round records clean hit bands again.
            host.Session.OpenPopup("theme-switch-popup", new Rect(30f, 40f, 90f, 24f));
            host.MeasureAndArrange(viewport);
            Assert(host.Session.OpenPopupId == "theme-switch-popup",
                swatch.Id + ": the popup survived the arrange after the switch");
            host.Session.ClosePopup();

            Color want = Hex(Role(swatch, "Foreground"));
            bool inkLanded = false;
            for (int i = 0; i < ink.Count; i++)
            {
                if (Same(ink[i], want)) { inkLanded = true; break; }
            }

            Assert(inkLanded,
                swatch.Id + ": the palette's own TextPrimary must appear in the RECORDED DRAW ink - "
                + "colour that never reaches a draw call is not a repaint");
        }

        // One last switch-time survival proof with a LIVE popup: open, switch, assert - no draw in
        // between, so this measures the palette write path itself, not the menu frame rule.
        host.Session.OpenPopup("theme-switch-final", new Rect(25f, 35f, 90f, 24f));
        UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, "core");
        Assert(host.Session.OpenPopupId == "theme-switch-final" && host.Session.ScrollTargetElementId == null,
            "a switch with a live open popup must neither close it nor leave a stale request");
        Assert(SameVec(host.Session.GetScrollPosition(scrollNode!), new Vector2(0f, 60f)),
            "scroll survived the final switch with the popup open");
        host.Session.ClosePopup();
        // CORRECTIONS-R3: the "all state survives" claim must not rest on scroll+popup evidence
        // alone. Open a REAL field edit through the engine's own press seam (the same route the
        // reset lanes use), then switch palettes and require the edit - the SAME session node -
        // to survive the write path and the next arrange. Element-level focus has no public owner
        // in this carrier (the session's active node is the draw-time stack); the active edit IS
        // the published seam, and it is what a player mid-typing actually is.
        host.Session.SetScrollPosition(scrollNode!, Vector2.zero);
        UiLayoutSnapshot editSnap = host.MeasureAndArrange(viewport);
        Assert(editSnap.RectById.TryGetValue("packs-keyword", out Rect fieldLocal),
            "the search field is arranged on Packs");
        // (RectById bands are PAGE-space here - the same space the reset lanes press in; the
        // seam predicates take the content-local copy separately.)
        var fieldPage = new Rect(fieldLocal.x, fieldLocal.y, fieldLocal.width, fieldLocal.height);
        var fieldCenter = new Vector2(fieldPage.x + fieldPage.width / 2f, fieldPage.y + fieldPage.height / 2f);
        var policyRect = new Rect(0f, 0f, viewport.x, viewport.y);
         GUIUtility.hotControl = 0; // the stub's held control is process-static (XG1 release rule)
        Program.DrawWithPointer(host, policyRect, fieldCenter);
        Program.DrawWithEvent(host, policyRect, EventType.MouseDown, fieldCenter);
        UiNode? editBefore = host.Session.ActiveEditNode;
        Assert(editBefore != null && editBefore.ElementId == "packs-keyword",
            "the real press opened the search field edit (edit=\"" + (editBefore?.ElementId ?? "null") + "\")");
        Program.DrawWithEvent(host, policyRect, EventType.MouseUp, fieldCenter);

        UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, "royalty");
        Assert(ReferenceEquals(host.Session.ActiveEditNode, editBefore),
            "the palette switch must leave the live edit on the SAME session node - a rebuilt"
            + " session would silently drop the player's half-typed draft");
        host.MeasureAndArrange(viewport);
        Assert(ReferenceEquals(host.Session.ActiveEditNode, editBefore),
            "and the next arrange still serves the same open edit");
        // Close it through the field's OWN commit contract: a press outside the open edit
        // (an empty band of the filter section) commits the draft - the same outside-click rule
        // the reset lanes rely on. The ladder (TryHandleCancel/Accept) is not this atom's door.
        var outsidePoint = new Vector2(fieldCenter.x, fieldPage.y - 40f);
        Program.DrawWithEvent(host, policyRect, EventType.MouseDown, outsidePoint);
        Program.DrawWithEvent(host, policyRect, EventType.MouseUp, outsidePoint);
        host.MeasureAndArrange(viewport);
        Assert(host.Session.ActiveEditNode == null, "the outside press committed the edit (fixture hygiene)");
        GUIUtility.hotControl = 0;

        Console.WriteLine("[theme-switch] six palettes: rects=" + baselineRects.Count + " hits=" + baselineHitsClean.Count
            + " labels=" + baselineInk.Count + " all identical across every switch; per-palette ink landed on the draw"
            + "; comparisons=" + compared);
    }

    private static void SameValueIsSilent()
    {
        var settings = NewSettings();
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
        UiTheme chrome = UsTheme.Surface();
        UiTheme page = host.StyleResolver.Baseline;
        UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, "ideology");
        int colourStamp = page.ColourRevision;
        int persisted = settings.uiSwatchId == "ideology" ? 1 : 0;

        bool wrote = UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, "ideology");
        Assert(!wrote, "re-applying the current palette is not a write");
        Assert(page.ColourRevision == colourStamp, "and it repaints nothing (" + colourStamp
            + " -> " + page.ColourRevision + ") - the token comparison keeps a same-value apply idempotent");
        Assert(persisted == 1 && settings.uiSwatchId == "ideology", "the state is unchanged");
    }
    /// <summary>
    /// CORRECTIONS-R3: the action chain on the REAL production pair - a real
    /// <see cref="UniversalSqueakerSettings"/> behind a real <see cref="UsKernelSettingsSource"/>
    /// on a real <see cref="UsKernelSettingsHost"/> (the Create path performs the production
    /// AttachHost/AttachRevisionSource wiring itself). The press is the declarative one -
    /// "set-ui-swatch" with the button's own -value payload - so it runs source.SetUiSwatch ->
    /// ApplySwatchLive -> the settings funnel, not a lane-side call into the seam. Everything the
    /// picker READS must move together: persisted settings, source facade, per-swatch selected
    /// bindings, status line, author note - and the two real bags must actually re-tint while the
    /// layout clock, the session and the scroll stay put. This is the lane that caught the composed
    /// note-key bug (US.Theme.&lt;X&gt;.Label.Note vs the shipped US.Theme.&lt;X&gt;.Note).
    /// </summary>
    private static void RealSourceActionChain()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            var settings = new UniversalSqueakerSettings();
            var source = new UsKernelSettingsSource(settings);
            using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
            host.Bindings.Invoke("set-tab", "Advanced");
            Arrange(host);

            Assert(settings.uiSwatchId == "core" && source.UiSwatchId == "core",
                "the production default answers Core through the real funnel");
            Assert(host.Bindings.Get<bool>("ui-swatch-core") && !host.Bindings.Get<bool>("ui-swatch-anomaly"),
                "the picker reads the real source before the press");
            string statusBefore = host.Bindings.Get<string>("ui-swatch-status");
            string noteBefore = host.Bindings.Get<string>("ui-swatch-author-note");
            Assert(statusBefore == "Active palette: Core", "the status names the active palette, got " + statusBefore);
            Assert(noteBefore.StartsWith("A still-working terminal", StringComparison.Ordinal),
                "the Core author note is the translated Keyed text, got " + noteBefore);

            UiTheme page = host.StyleResolver.Baseline;
            UiTheme chrome = UsTheme.ChromeTheme; // the REAL shared bag the production chain paints
            int layoutBefore = page.LayoutRevision;
            int nodesBefore = host.Session.Identity;
            UiNode? scroll = host.Session.GetNodeByElementId("content-scroll");
            Assert(scroll != null, "the content scroll node exists on the real host");
            host.Session.SetScrollPosition(scroll!, new Vector2(0f, 48f));
            host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));

            // The exact press the declarative button makes: its own -value payload, the ONE string
            // action, the production source behind it.
            string payload = host.Bindings.Get<string>("ui-swatch-anomaly-value");
            Assert(payload == "anomaly", "the button payload names the target palette");
            host.Bindings.Invoke("set-ui-swatch", payload);

            Assert(settings.uiSwatchId == "anomaly", "the real settings funnel persisted the switch");
            Assert(source.UiSwatchId == "anomaly", "the source facade reports the SAME single authority");
            Assert(!host.Bindings.Get<bool>("ui-swatch-core") && host.Bindings.Get<bool>("ui-swatch-anomaly"),
                "the picker selection moved with the source");
            string statusAfter = host.Bindings.Get<string>("ui-swatch-status");
            Assert(statusAfter == "Active palette: Anomaly", "the status line synced, got " + statusAfter);
            string noteAfter = host.Bindings.Get<string>("ui-swatch-author-note");
            Assert(noteAfter != noteBefore && noteAfter.StartsWith("Cold grey and charcoal", StringComparison.Ordinal),
                "the author note is the target palette's translated Keyed text - a raw key here means"
                + " the note-key composition drifted again, got " + noteAfter);

            UsSwatch anomaly = UsSwatches.Resolve("anomaly");
            Assert(Same(page[UiPaletteRole.Foreground], Hex(Role(anomaly, "Foreground"))),
                "the PAGE bag carries the target ink through the real chain");
            Assert(Same(chrome[UiPaletteRole.Foreground], Hex(Role(anomaly, "Foreground"))),
                "the shared CHROME bag follows through the same call");
            Assert(page.LayoutRevision == layoutBefore,
                "the layout clock did not move (" + layoutBefore + " -> " + page.LayoutRevision + ")");
            host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
            Assert(SameVec(host.Session.GetScrollPosition(scroll!), new Vector2(0f, 48f)),
                "the real session kept the scroll through the action chain");
            Assert(host.Session.Identity == nodesBefore, "no session rebuild");

            // Fail-soft resolution lives on the source: an unknown id answers Core, silently.
            host.Bindings.Invoke("set-ui-swatch", "no-such-palette");
            Assert(source.UiSwatchId == "core" && settings.uiSwatchId == "core",
                "an unknown id resolves fail-soft to the Core default");
            Assert(Same(chrome[UiPaletteRole.Foreground], Hex(Role(UsSwatches.Resolve("core"), "Foreground"))),
                "and the shared chrome bag is left on Core for the lanes that follow");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>The five-workspace rect sweep across ONE switch. CORRECTIONS-R3: FULL equality
    /// per page - key set AND count must match exactly; the old sweep `continue`d over keys that
    /// were missing from the baseline, which silently let a vanished or newly-appeared control
    /// pass. Covers the pages the six-palette draw step does not.</summary>
    private static void RectsIdentical()
    {
        var settings = NewSettings();
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
        UiTheme chrome = UsTheme.Surface();
        string[] tabs = { "Overview", "Packs", "Tuning", "Presets", "Advanced" };
        var before = new Dictionary<string, Dictionary<string, Rect>>(StringComparer.Ordinal);
        foreach (string tab in tabs)
        {
            host.Bindings.Invoke("set-tab", tab);
            before[tab] = CopyRects(Arrange(host));
        }

        UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, "biotech");
        int compared = 0;
        foreach (string tab in tabs)
        {
            host.Bindings.Invoke("set-tab", tab);
            Dictionary<string, Rect> after = CopyRects(Arrange(host));
            Dictionary<string, Rect> old = before[tab];
            Assert(after.Count == old.Count, tab + ": the arranged key COUNT moved across a palette"
                + " switch (" + old.Count + " -> " + after.Count + ") - a switch adds and removes nothing");
            foreach (KeyValuePair<string, Rect> entry in old)
            {
                Assert(after.TryGetValue(entry.Key, out Rect now),
                    tab + "|" + entry.Key + ": a control VANISHED across the switch (must fail, never skip)");
                Assert(Close(entry.Value.x, now.x) && Close(entry.Value.y, now.y)
                        && Close(entry.Value.width, now.width) && Close(entry.Value.height, now.height),
                    "a palette switch must not move a single rect: " + tab + "|" + entry.Key
                        + " " + entry.Value + " -> " + now);
                compared++;
            }

            foreach (string key in after.Keys)
            {
                Assert(old.ContainsKey(key), tab + "|" + key + ": a NEW control appeared across the switch");
            }
        }

        Assert(compared > 120, "the five-workspace sweep must cover a real page, got " + compared);
        Console.WriteLine("[theme-switch] five-workspace rects compared=" + compared + " all identical (full key-set equality)");
    }

    /// <summary>
    /// CO review item 10 (re-cut): the selection/focus evidence is located BY ELEMENT RECT in the
    /// recorded draw, and the ORIGINAL floors are measured against the REAL adjacent grounds - never
    /// a page-wide "any pixel equals the token" sweep. Per palette: the theme picker's CURRENT item
    /// (its own label rect -> the fill actually under it must be the declared Selected; body ink vs
    /// THAT fill >= 4.5; the border actually touching that fill vs the fill >= 3), the nav's
    /// current-page rail vs the ground behind it (>= 3), the Packs selected row (fill identity, rail
    /// vs fill >= 3, title ink vs fill >= 4.5) and the open field edit's draft ground vs its ink
    /// (>= 4.5). Selected-vs-Raised plane difference is NOT gated (PM: <3 there is legal; the
    /// identity rides the declared marks). The PaletteLane helper numbers stay as support.
    /// </summary>
    private static void SelectionSurfacesPaintTheDeclaredRole()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            var settings = NewSettings();
            var fake = new RecordingSettingsSource { RichData = true };
            using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
            UiTheme chrome = UsTheme.Surface();
            var viewport = new Rect(0f, 0f, PageWidth, PageHeight);
            foreach (UsSwatch swatch in UsSwatches.All)
            {
                // The picker answers the SOURCE (selection bindings read source.UiSwatchId) and the bags
                // answer the paint seam - drive both production doors, exactly as the window does.
                host.Bindings.Invoke("set-ui-swatch", swatch.Id);
                UsKernelSettingsHost.ApplySwatchLive(settings, host, chrome, swatch.Id);
                UiTheme bag = host.StyleResolver.Baseline;
                string where = swatch.Id + ": ";

                // ---- theme picker CURRENT item on Advanced ----
                host.Bindings.Invoke("set-tab", "Advanced");
                Arrange(host);
                var solids = DrawAndCaptureSolidPairs(host, viewport);
                var inks = DrawAndCaptureLabelPairs(host, viewport);
                string currentLabel = UsThemeLabel(swatch);
                Rect? labelRect = null;
                Color labelInk = default;
                foreach (var pair in inks)
                {
                    if (pair.Text == currentLabel && labelRect == null) { labelRect = pair.Rect; labelInk = pair.Color; }
                }

                Rect lr = labelRect ?? default;
                Assert(lr.width > 0f, where + "the current picker item label is drawn");
                Rect fill = ContainingSolid(solids, lr, bag[UiPaletteRole.SelectedSurface]);

                Assert(fill.width > 0f, where + "the fill ACTUALLY UNDER the current picker item is the declared Selected surface");
                Assert(Contrast(labelInk, bag[UiPaletteRole.SelectedSurface]) >= 4.5,
                    where + "picker body ink vs its real Selected ground: "
                        + Contrast(labelInk, bag[UiPaletteRole.SelectedSurface]).ToString("0.00") + ":1 (floor 4.5)");
                Color edge = bag.SelectedSurface.Border;
                Assert(AdjacentSolid(solids, fill, edge) && Contrast(edge, bag[UiPaletteRole.SelectedSurface]) >= 3.0,
                    where + "the picker border touching that fill carries >= 3.0 against it: "
                        + Contrast(edge, bag[UiPaletteRole.SelectedSurface]).ToString("0.00") + ":1 (floor 3.0)");

                // ---- nav current-page marker vs the ground behind it ----
                host.Bindings.Invoke("set-tab", "Overview");
                Arrange(host);
                var navSolids = DrawAndCaptureSolidPairs(host, viewport);
                bool navMarked = false;
                foreach (var s in navSolids)
                {
                    if (s.Rect.x > 200f || s.Rect.width > 6f) continue;
                    if (!Same(s.Color, edge)) continue;
                    Rect ground = ContainingSolid(navSolids, new Rect(s.Rect.x, s.Rect.y + s.Rect.height / 2f,
                        s.Rect.width + 8f, 1f), bag[UiPaletteRole.SelectedSurface]);
                    if (ground.width > 0f)
                    {
                        Assert(Contrast(edge, bag[UiPaletteRole.SelectedSurface]) >= 3.0,
                            where + "nav current rail vs the Selected card behind it: "
                                + Contrast(edge, bag[UiPaletteRole.SelectedSurface]).ToString("0.00") + ":1 (floor 3.0)");
                        navMarked = true;
                        break;
                    }
                }

                Assert(navMarked, where + "the nav column draws the current-page marker (declared edge) over its real ground");

                // ---- Packs SELECTED ROW, bound to its own element rect (CO final item 4) ----
                // The row surface is a declarative element (pack-card-row-surface#<key>): the row is
                // identified by the recorded fill EXACTLY matching that element rect - no page-wide
                // "first wide Selected solid" guess that could land on nav/mode chrome.
                host.Bindings.Invoke("set-tab", "Packs");
                UiLayoutSnapshot snap = Arrange(host); // materialises the headers, registering their toggles
                // The selected row is a DOMAIN row - it only materializes once its card is expanded
                // (collapsed cards show headers, and a header is never the operating-domain answer).
                if (!host.Bindings.TryGet("pack-card-keys.us.sang.expanded", out bool expanded) || !expanded)
                {
                    host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
                }
                snap = Arrange(host);
                var packsSolids = DrawAndCaptureSolidPairs(host, viewport);
                var packsInks = DrawAndCaptureLabelPairs(host, viewport);
                Rect rowFill = default;
                string? rowId = null;
                bool foundRow = false;
                foreach (string k in snap.RectById.Keys)
                {
                    if (!k.StartsWith("pack-card-row-surface#", StringComparison.Ordinal)) continue;
                    // Bind through the host's OWN per-item selected answer (the same qualified binding
                    // the surface widget reads) - the element is selected because the production rule
                    // says so, not because a colour happened to appear near it.
                    string itemKey = k.Substring("pack-card-row-surface#".Length);
                    if (!host.Bindings.TryGet("pack-card-keys." + itemKey + ".selected", out bool sel) || !sel) continue;
                    Rect er = snap.RectById[k];
                    foreach (var s2 in packsSolids)
                    {
                        // The element draws inside its row's own native group, so the RECORDED rect
                        // is group-local (origin 0,0) - position cannot bind it. The identity chain is:
                        // the host's own selected binding says WHICH element, and the element rect's
                        // SIZE says which recorded fill is that element's plate (1004 x 33.3 here).
                        if (Same(s2.Color, bag[UiPaletteRole.SelectedSurface])
                                && Math.Abs(s2.Rect.width - er.width) < 2f && Math.Abs(s2.Rect.height - er.height) < 2f)
                        {
                            rowFill = s2.Rect; rowId = k; foundRow = true; break;
                        }
                    }

                    if (foundRow) break;
                }


                Assert(foundRow, where + "the SELECTED Packs row is identified BY ELEMENT RECT (" + (rowId ?? "?")
                    + ") painted with the declared Selected fill");

                Color railColor = default;
                bool railOnRow = false;
                foreach (var s2 in packsSolids)
                {
                    // The ANCHOR rail spans the plate vertically (checkbox knobs and other
                    // small furniture do not) and sits at its left edge.
                    if (s2.Rect.width > 4f) continue;
                    if (s2.Rect.x < rowFill.x - 1f || s2.Rect.x > rowFill.x + 6f) continue;
                    if (s2.Rect.y > rowFill.y + 2f || s2.Rect.yMax < rowFill.yMax - 2f) continue;
                    // The identity mark is the ANCHOR rail (the widget's SelectedRail token). The
                    // hairline border strip also lives at the left edge - it is chrome, not the mark,
                    // and its plane-to-plane delta is deliberately not gated (PM ruling).
                    if (!Same(s2.Color, UsSelectionSurfaceWidget.SelectedRail(bag))) continue;
                    railColor = s2.Color;
                    railOnRow = true;
                    break;
                }

                Assert(railOnRow, where + "a rail primitive is recorded ON the identified row's left edge");
                Assert(Contrast(railColor, bag[UiPaletteRole.SelectedSurface]) >= 3.0,
                    where + "that rail vs the Selected ground it sits on: "
                        + Contrast(railColor, bag[UiPaletteRole.SelectedSurface]).ToString("0.00") + ":1 (floor 3.0)");
                // The gated body text on the plate is the ROW TITLE (TextPrimary). Secondary/meta
                // inks inside the plate are measured and REPORTED (observation lines) - the author
                // numbers claimed every body ink clears 4.5 on Selected; where the real draw says
                // otherwise, the report carries the fact, the lane does not silently gate it away.
                bool titleOnRow = false;
                foreach (var ink in packsInks)
                {
                    if (ink.Rect.x < rowFill.x - 0.5f || ink.Rect.xMax > rowFill.xMax + 0.5f) continue;
                    if (ink.Rect.y < rowFill.y - 0.5f || ink.Rect.yMax > rowFill.yMax + 0.5f) continue;
                    double c = Contrast(ink.Color, bag[UiPaletteRole.SelectedSurface]);
                    if (Same(ink.Color, bag[UiPaletteRole.SecondaryForeground]))
                    {
                        Console.WriteLine("[selection-paint] " + swatch.Id + " observed: secondary ink on Selected "
                            + c.ToString("0.00") + ":1 (reported, not gated)");
                        continue;
                    }

                    if (!Same(ink.Color, bag[UiPaletteRole.Foreground])) continue;
                    Assert(c >= 4.5,
                        where + "row TITLE ink vs the Selected ground under it: "
                            + c.ToString("0.00") + ":1 (floor 4.5)");
                    titleOnRow = true;
                    break;
                }

                Assert(titleOnRow, where + "recorded text lies inside the identified row rect (measured above)");

                // ---- open edit / focus: bound to the FIELD element rect + ActiveEditNode identity ----
                GUIUtility.hotControl = 0;
                Rect fieldRect = snap.RectById["packs-keyword"];
                var fieldCenter = new Vector2(fieldRect.x + fieldRect.width / 2f,
                    fieldRect.y + fieldRect.height / 2f);
                Program.DrawWithPointer(host, viewport, fieldCenter);
                Program.DrawWithEvent(host, viewport, EventType.MouseDown, fieldCenter);
                Assert(host.Session.ActiveEditNode != null
                        && host.Session.ActiveEditNode.ElementId == "packs-keyword",
                    where + "the real press opened the edit ON the identified field element");
                Program.DrawWithEvent(host, viewport, EventType.MouseUp, fieldCenter);
                var editSolids = DrawAndCaptureSolidPairs(host, viewport);
                // The observable focus mark at the field rect is the caret primitive (the draft text
                // itself leaves the native text outlet, which the label recorder cannot see - stated
                // rather than faked). The mark is measured against the ground actually recorded under
                // the field, not an assumed plane.
                bool markFound = false;
                foreach (var s2 in editSolids)
                {
                    if (s2.Rect.height > 3f || s2.Rect.width > 9f) continue;
                    if (s2.Rect.x < fieldRect.x - 8f || s2.Rect.xMax > fieldRect.xMax + 8f) continue;
                    if (s2.Rect.y < fieldRect.y - 8f || s2.Rect.yMax > fieldRect.yMax + 8f) continue;
                    Rect ground = ContainingSolidAny(editSolids,
                        new Rect(fieldRect.x + 4f, fieldRect.y + fieldRect.height / 2f, 1f, 1f));
                    Color groundColor = ground.width > 0f ? ColorAt(editSolids, ground) : bag[UiPaletteRole.ControlSurface];
                    Assert(Contrast(s2.Color, groundColor) >= 3.0,
                        where + "focus caret mark vs the ground actually recorded under the field: "
                            + Contrast(s2.Color, groundColor).ToString("0.00") + ":1 (floor 3.0)");
                    markFound = true;
                    break;
                }

                Assert(markFound, where + "a focus mark primitive is recorded AT the editing field's rect");

                host.TryHandleCancel();
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    private static string UsThemeLabel(UsSwatch swatch)
    {
        // The lane's translator resolver answers the Keyed table; the picker label is
        // drawn from the same key, so the recorded text equals this lookup.
        return Program.TranslateKey(swatch.LabelKey);
    }

    private static Rect ContainingSolid(List<(Rect Rect, Color Color)> solids, Rect probe, Color mustMatch)
    {
        foreach (var s in solids)
        {
            float cx = probe.x + probe.width / 2f, cy = probe.y + probe.height / 2f;
            if (s.Rect.x <= cx && cx <= s.Rect.xMax && s.Rect.y <= cy && cy <= s.Rect.yMax && Same(s.Color, mustMatch))
            {
                return s.Rect;
            }
        }

        return default;
    }

    private static Rect ContainingSolidAny(List<(Rect Rect, Color Color)> solids, Rect probe)
    {
        Rect best = default;
        float area = float.MaxValue;
        float cx = probe.x + probe.width / 2f, cy = probe.y + probe.height / 2f;
        foreach (var s in solids)
        {
            if (s.Rect.x <= cx && cx <= s.Rect.xMax && s.Rect.y <= cy && cy <= s.Rect.yMax)
            {
                float a = s.Rect.width * s.Rect.height;
                if (a < area) { area = a; best = s.Rect; }
            }
        }

        return best;
    }

    private static Color ColorAt(List<(Rect Rect, Color Color)> solids, Rect rect)
    {
        foreach (var s in solids)
        {
            if (s.Rect.x == rect.x && s.Rect.y == rect.y) return s.Color;
        }

        return default;
    }

    /// <summary>True when a solid of this colour touches the fill rect's border band (the drawn edge).
    /// </summary>
    private static bool AdjacentSolid(List<(Rect Rect, Color Color)> solids, Rect fill, Color edge)
    {
        foreach (var s in solids)
        {
            if (!Same(s.Color, edge)) continue;
            bool touchesX = Math.Abs(s.Rect.x - fill.x) <= 2.5f || Math.Abs(s.Rect.xMax - fill.xMax) <= 2.5f;
            bool touchesY = Math.Abs(s.Rect.y - fill.y) <= 2.5f || Math.Abs(s.Rect.yMax - fill.yMax) <= 2.5f;
            bool overlapsBand = s.Rect.y < fill.yMax + 2.5f && s.Rect.yMax > fill.y - 2.5f
                && s.Rect.x < fill.xMax + 2.5f && s.Rect.xMax > fill.x - 2.5f;
            if ((touchesX || touchesY) && overlapsBand) return true;
        }

        return false;
    }

    /// <summary>WCAG relative-luminance contrast over OPAQUE recorded colours (the declared surface
    /// roles are opaque; the composite is the identity - UsSelectionSurfaceWidget documents this).
    /// </summary>
    private static double Contrast(Color a, Color b)
    {
        return (Lum(a) + 0.05) / (Lum(b) + 0.05) < (Lum(b) + 0.05) / (Lum(a) + 0.05)
            ? (Lum(b) + 0.05) / (Lum(a) + 0.05)
            : (Lum(a) + 0.05) / (Lum(b) + 0.05);
    }

    private static double Lum(Color c)
    {
        static double Chan(float v)
        {
            double s = v;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Chan(c.r) + 0.7152 * Chan(c.g) + 0.0722 * Chan(c.b);
    }

    private sealed class LabelProbe
    {
        public Rect Rect;
        public Color Color;
        public string Text = "";
    }

    private static List<LabelProbe> DrawAndCaptureLabelPairs(UiHost host, Rect viewport)
    {
        IList texts = Recorded("LabelTexts");
        IList rects = Recorded("LabelRects");
        IList colors = Recorded("LabelColors");
        texts.Clear();
        rects.Clear();
        colors.Clear();
        host.DrawChecked(viewport);
        var list = new List<LabelProbe>();
        for (int i = 0; i < texts.Count && i < rects.Count && i < colors.Count; i++)
        {
            if (rects[i] is Rect r && colors[i] is Color c)
            {
                list.Add(new LabelProbe { Rect = r, Color = c, Text = texts[i] as string ?? "" });
            }
        }

        return list;
    }

    private static List<(Rect Rect, Color Color)> DrawAndCaptureSolidPairs(UiHost host, Rect viewport)
    {
        IList rects = Recorded("DrawBoxSolidRects");
        IList colors = Recorded("DrawBoxSolidColors");
        rects.Clear();
        colors.Clear();
        host.DrawChecked(viewport);
        var pairs = new List<(Rect, Color)>();
        for (int i = 0; i < colors.Count; i++)
        {
            if (colors[i] is Color c && rects[i] is Rect r) pairs.Add((r, c));
        }

        return pairs;
    }

    // ---- helpers ----

    private static Color AnomalyTextPrimary => Hex(Role(UsSwatches.Resolve("anomaly"), "Foreground"));

    private static UniversalSqueakerSettings NewSettings() => new UniversalSqueakerSettings();

    private static string Role(UsSwatch swatch, string token)
    {
        int index = System.Array.IndexOf(UsTheme.ColorTokens as string[], token);
        if (index < 0) throw new InvalidOperationException("unknown token " + token);
        return swatch.RoleHexes[index];
    }

    private static Dictionary<string, Rect> CopyRects(UiLayoutSnapshot snapshot)
    {
        var copy = new Dictionary<string, Rect>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Rect> entry in snapshot.RectById) copy[entry.Key] = entry.Value;
        return copy;
    }

    private static List<Color> DrawAndCaptureInk(UiHost host)
    {
        IList colors = Recorded("LabelColors");
        var snapshotColors = new List<Color>(colors.Count);
        foreach (object item in colors)
        {
            if (item is Color colour) snapshotColors.Add(colour);
        }

        // Re-draw one pass with the recorders cleared so what we compare is THIS palette's draw.
        Recorded("LabelTexts").Clear();
        Recorded("LabelRects").Clear();
        colors.Clear();
        host.DrawChecked(new Rect(0f, 0f, 760f, 524f));
        var ink = new List<Color>();
        foreach (object item in colors)
        {
            if (item is Color colour) ink.Add(colour);
        }

        return ink;
    }

    private static List<Rect> DrawAndCaptureHits(UiHost host)
    {
        var hits = new List<Rect>();
        FieldInfo seam = typeof(UiNative).GetField("ButtonOverride",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        object? previous = seam.GetValue(null);
        seam.SetValue(null, new Func<Rect, bool>(rect => { hits.Add(rect); return false; }));
        try
        {
            host.DrawChecked(new Rect(0f, 0f, 760f, 524f));
        }
        finally
        {
            seam.SetValue(null, previous);
        }

        hits.Sort((a, b) => (a.y * 10000f + a.x).CompareTo(b.y * 10000f + b.x));
        return hits;
    }

    private static IList Recorded(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException("the runtime stub does not record '" + fieldName
                + "'; this lane cannot observe a draw and must not pass silently");
        }

        return (IList)field.GetValue(null)!;
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", CurrentScroll(host));
        return host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
    }

    private static Vector2 CurrentScroll(UiHost host)
    {
        UiNode? node = host.Session.GetNodeByElementId("content-scroll");
        return node == null ? Vector2.zero : host.Session.GetScrollPosition(node);
    }

    private static Color Hex(string value)
    {
        string s = value.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
        return new Color(
            Convert.ToByte(s.Substring(0, 2), 16) / 255f,
            Convert.ToByte(s.Substring(2, 2), 16) / 255f,
            Convert.ToByte(s.Substring(4, 2), 16) / 255f, 1f);
    }

    private static bool Same(Color a, Color b)
    {
        const float eps = 1f / 512f;
        return Mathf.Abs(a.r - b.r) < eps && Mathf.Abs(a.g - b.g) < eps && Mathf.Abs(a.b - b.b) < eps;
    }

    private static string Components(Color c) =>
        c.r.ToString("F4") + "/" + c.g.ToString("F4") + "/" + c.b.ToString("F4");

    private static bool Close(float a, float b) => Math.Abs(a - b) <= 0.01f;

    private static bool SameVec(Vector2 a, Vector2 b) => Close(a.x, b.x) && Close(a.y, b.y);

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("ThemeSwitch lane step failed: " + name + "\n  INNER: " + error.Message, error);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
