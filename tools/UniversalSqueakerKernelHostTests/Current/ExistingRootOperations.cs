using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

// Existing Program.cs cases and helpers moved to the fixed public-Execution runner.
// No new scenarios or business assertions; root case identity is recorded in the A003 migration map.
internal static partial class Program
{
    private static void RichDynamicDataMeasureAndDraw()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        IUiBindingAccess bindings = host.Bindings;

        // Filter/options/dynamic-list bindings read the rich projections.
        Assert(bindings.GetOptions<FilterOptionView>("author-options").Count == 2, "author-options reads the rich author list");
        Assert(bindings.GetOptions<FilterOptionView>("race-filter-options").Count == 4, "race-filter-options reads the rich list");
        Assert(bindings.GetOptions<FilterOptionView>("xenotype-filter-options").Count == 2, "xenotype-filter-options reads the rich list");
        Assert(bindings.Get<IReadOnlyList<RaceLayerRowView>>("races").Count == 3, "races reads the rich race list");
        Assert(bindings.Get<IReadOnlyList<BaselinePresetView>>("baseline-presets").Count == 1, "baseline-presets reads the rich preset list");
        Assert(bindings.Get<IReadOnlyList<VoicePackDomainView>>("xenotype-domains").Count == 1, "xenotype-domains reads the rich list");
        Assert(bindings.Get<VoicePackDomainView?>("selected-domain").HasValue, "selected-domain reads the rich selected domain");
        Assert(bindings.Get<IReadOnlyList<ActionScopeRowView>>("action-scopes").Count == 2, "action-scopes reads the rich list");
        // The rich fixture carries ALL FOUR product moods (Good/Neutral/Bad/Break): the closure pass
        // requires every mood card to be covered, so the count below tracks the fixture, not a
        // two-mood sample.
        Assert(bindings.Get<IReadOnlyList<TuningDomainOptionView>>("tuning-domains").Count == 2, "tuning-domains reads the rich list");

        // Full dynamic draw: every workspace with non-empty lists at the reference viewports.
        foreach (string tab in new[] { "Overview", "Packs", "Tuning", "Presets", "Advanced" })
        {
            host.Bindings.Invoke("set-tab", tab);
            foreach (Vector2 viewport in new[] { new Vector2(800f, 600f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
            {
                host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));
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
        bindings.Invoke("select-domain", "human|sanguophage");
        Assert(fake.LastSelectedScope == SqueakVoicePackScope.Xenotype && fake.LastSelectedRace == "human"
            && fake.LastSelectedTarget == "sanguophage", "a composite row key decodes into a xenotype selection");
        bindings.Invoke("select-domain", "testrace");
        Assert(fake.LastSelectedScope == SqueakVoicePackScope.Race && fake.LastSelectedRace == "testrace"
            && fake.LastSelectedTarget == "", "a bare row key decodes into a race selection");
        // US-PACK1: "toggle-pack" retired with the checklist list; the card row's enable is the
        // item-scoped VALUE asserted through a real materialised row in PackCardsLaneTests.
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

    private static void CompositeDropdownPublishesCoveringRect()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        host.Bindings.Invoke("set-tab", "Tuning");
        // The covered element this lane asserts against is the help panel, which only exists while help
        // is open (the shipped default is retracted).
        host.Bindings.Set("help-open", true);
        Rect viewport = new(0f, 0f, 800f, 600f);
        // UIR-001: the rule bands joined the Tuning tab, so the scope-tree composite sits below the
        // fold at scroll 0 and the dropdown never draws - scroll it into view first (the player can).
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(800f, 600f)));
        UiNode? foldNode = host.Session.GetNodeByElementId("page-Behavior-scroll");
        Assert(foldNode != null, "the content scroll must exist to reach the scope tree");
        host.Session.SetScrollPosition(foldNode!, VerseUiExecution.Point(new Vector2(0f, 700f)));
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(800f, 600f)));
        host.DrawChecked(viewport);

        // Deliberately stale seed: the composite must replace it with its drawn trigger's live anchor.
        // This lane no longer teleports the popup over another widget to force a flip. FL owns the
        // independent below/flip/clamp geometry lanes; here we assert publication, live attachment and
        // dispatch for a covered point through the actual production composite.
        Rect anchor = new(620f, 560f, 120f, 22f);
        string? publishedBy = null;
        Rect popup = default;
        UiNode? popupOwner = null;
        foreach (string key in UniversalSqueaker.Kernel.BuiltInActionKeys.All)
        {
            host.Session.OpenPopup("scope-tree-scope-" + Uri.EscapeDataString(key), VerseUiExecution.Rect(anchor));
            host.DrawChecked(viewport);
            if (TryGetPopupHitLayer(host.Session, out UiHitLayer layer))
            {
                publishedBy = key;
                popup = VerseUiExecution.Rect(layer.Rect);
                popupOwner = layer.Element;
                break;
            }

            host.Session.ClosePopup();
        }

        Assert(publishedBy != null,
            "no Tuning scope dropdown published a popup layer; composite popups can never win a covered click");
        Assert(popup.height >= 24f && Math.Abs(popup.height % 24f) < 0.01f,
            "composite popup height must be a whole number of option rows: " + popup.height);
        Assert(popup.y >= -0.01f && popup.yMax <= 600f + 0.01f,
            "composite popup must stay inside the host viewport: " + popup);
        UiLayoutRect? liveAnchor = host.Session.OpenPopupAnchor;
        Assert(liveAnchor.HasValue && Math.Abs(liveAnchor.Value.Y - anchor.y) > 0.01f,
            "the composite must replace the stale opening anchor with its actual trigger");
        Assert(liveAnchor.HasValue && (Math.Abs(popup.y - liveAnchor.Value.YMax) < 0.01f
            || Math.Abs(popup.yMax - liveAnchor.Value.Y) < 0.01f),
            "the composite popup must attach immediately below or above its live trigger");

        // 0.4.0 replaced the single published popup rect with the owned hit stack: UiPopup pushes a
        // popup layer and dispatch consults it topmost-first (UiSession.IsPointerOverHigherLayer).
        // Input arrives in the frame AFTER the draw, and a pass dispatches against the stack the
        // previous pass finished, so the predicate is asserted after a second frame - the same
        // next-frame reason the retired rect had to be republished before it was read.
        host.DrawChecked(viewport);

        // The layer names its owner by node identity, so the owner comes from the layer itself: the
        // scope rows are drawn inside one widget, so they are not tree elements a string id resolves.
        UiNode? owner = popupOwner;
        UiNode? covered = host.Session.GetNodeByElementId("help-panel");
        Assert(owner != null, "the published popup layer must name the element that pushed it");
        Assert(covered != null, "the help panel must be an arranged element for the covered case");

        Vector2 inside = new(popup.x + popup.width / 2f, popup.y + 12f);
        Vector2 outside = new(inside.x, popup.yMax + 40f);
        Assert(host.Session.IsPointerOverHigherLayer(covered!, VerseUiExecution.Point(inside)),
            "a popup layer must make a covered element yield the click (owned hit stack)");
        Assert(!host.Session.IsPointerOverHigherLayer(covered!, VerseUiExecution.Point(outside)),
            "the yield predicate must not fire outside the popup");
        // FL-IC1 (frozen d5af4d8) RETIRED the owner-id exemption: coverage is geometric for every
        // caller, "the popup owner's identity is not a licence over the whole covering area"
        // (INTERACTION-CONVERGENCE-TASKS §3.1). So a point INSIDE the covering menu now outranks even
        // the owner's own trigger - the overlap the old exemption kept was the reported defect - and
        // toggle-to-close lives on where the menu does NOT cover: an uncovered press still reaches the
        // trigger, which the dropdown's own seam then routes to close. The 3-arg overload survives as
        // public surface that ignores the id; the lane reads the two-arg truth instead of pinning the
        // retired licence.
        Assert(host.Session.IsPointerOverHigherLayer(owner!, VerseUiExecution.Point(inside)),
            "the covering menu wins the in-menu point over its own trigger - the retired exemption "
            + "must not come back through a sibling of the composite");
        Assert(!host.Session.IsPointerOverHigherLayer(owner!, VerseUiExecution.Point(outside)),
            "an uncovered point keeps reaching the owner - that is where toggle-to-close lives now");

        // The layer is per-frame state: a second frame must republish it, and closing must drop it so a
        // stale layer cannot shadow later clicks (the dispatch stack is read one pass after the close).
        Assert(TryGetPopupHitLayer(host.Session, out _), "the popup layer must be republished every frame it draws");
        host.Session.ClosePopup();
        host.DrawChecked(viewport);
        host.DrawChecked(viewport);
        Assert(!TryGetPopupHitLayer(host.Session, out _), "a closed composite popup must leave no popup layer");
        Assert(!host.Session.IsPointerOverHigherLayer(covered!, VerseUiExecution.Point(inside)),
            "a closed popup must stop shadowing the elements it covered");
    }

    private static void TypedScopeChoiceCommitsTheValue()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake);
        host.Bindings.Invoke("set-tab", "Tuning");
        host.Bindings.Set("help-open", false);
        Rect viewport = new(0f, 0f, 800f, 600f);
        // UIR-001: the rule bands joined the Tuning tab, so the scope rows sit below the fold at top
        // scroll; reveal them once, and every popup helper below draws against live rows.
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(800f, 600f)));
        UiNode? revealNode = host.Session.GetNodeByElementId("page-Behavior-scroll");
        Assert(revealNode != null, "the content scroll node must exist to reach the scope rows");
        host.Session.SetScrollTarget("scope-tree");
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(800f, 600f)));
        host.DrawChecked(viewport);

        // The fixture's Eat row is AnyOccurrence, so the press below is a real change; if that ever moves,
        // the lane must say so rather than pass on a no-op commit.
        Assert(fake.LastActionScope == null, "the lane starts from a clean commit record");

        BehaviorScope? committed = ClickScopeOption(host, fake, viewport, CoreActions.Eat, BehaviorScope.Disabled);
        Assert(committed == BehaviorScope.Disabled,
            "a chosen scope option must commit the real enum instance, got "
            + (committed.HasValue ? committed.Value.ToString() : "null"));
        Assert(fake.LastActionKey == CoreActions.Eat && fake.LastActionScope == BehaviorScope.Disabled,
            "and it must arrive at SetActionScope unchanged");

        committed = ClickScopeOption(host, fake, viewport, CoreActions.Draft, null);
        Assert(committed == null,
            "a row with no own scope must commit null (inherit), not a default scope - got "
            + (committed.HasValue ? committed.Value.ToString() : "null"));
        Assert(fake.LastActionKey == CoreActions.Draft && fake.LastActionScope == null,
            "and the Draft setter must actually receive null");

        // A press that misses the option list must not write anything: the typed path is not a wider target
        // than the row hit rule was.
        PressScopePopup(host, fake, viewport, CoreActions.Draft, out Rect missed);
        Assert(fake.LastActionKey == null && fake.LastActionScope == null,
            "a press outside the option list must not commit any scope ("
            + missed + " is outside the popup)");
    }

    private static void LongAuthorFilterTruncatesTheDisplayOnly()
    {
        const string shortAuthor = "AuthorA";
        string longAuthor = new string('A', 200); // 1600px raw, beyond the settings-window display cap
        var stub = new StubMetrics();
        var fake = new RecordingSettingsSource { RichData = true, Authors = new[] { shortAuthor, longAuthor } };
        using UiHost host = UsKernelSettingsHost.Create(fake, stub);
        host.Bindings.Invoke("set-tab", "Packs");
        // The author control belongs to the initially collapsed advanced filters.
        host.Bindings.Invoke("voice-filters");
        host.Bindings.Invoke("set-pack-filter", shortAuthor);
        // Keep the actual full-width trigger below the display cap while leaving room for cap + 12px.
        // At the stub's default screen: cap=640, trigger=700-24(root padding)-80(label)=596.
        Rect viewport = new(0f, 0f, 700f, 600f);
        float cap = OptionDisplayCap();
        float rawNeeded = stub.MeasureWidth(longAuthor, UiFont.Small) + 12f;

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(stub, reports.Add);
        UiFitAudit.Enabled = true;
        SetTranslatorResolver(ReadKeyedTable("English"));
        try
        {
            host.MeasureAndArrange(VerseUiExecution.Point(new UnityEngine.Vector2(viewport.width, viewport.height)));
            host.DrawChecked(viewport); // prime the view cache and popup state
            UiFitAudit.Reset();
            reports.Clear();
            host.MeasureAndArrange(VerseUiExecution.Point(new UnityEngine.Vector2(viewport.width, viewport.height)));
            host.DrawChecked(viewport);
            Assert(
                reports.FindAll(r => r.ElementPath.Contains("filter-bar")).Count == 0,
                "a selected long author must ellipsize inside the fixed trigger column, not overflow: "
                + DescribeOverflow(reports));

            Rect anchor = new(300f, 200f, 143f, 24f);
            host.Session.OpenPopup("pack-filter", VerseUiExecution.Rect(anchor));
            host.MeasureAndArrange(VerseUiExecution.Point(new UnityEngine.Vector2(viewport.width, viewport.height)));
            host.DrawChecked(viewport);
            Assert(TryGetPopupHitLayer(host.Session, out UiHitLayer popupLayer),
                "the opened pack-filter must publish its popup layer");
            Rect popup = VerseUiExecution.Rect(popupLayer.Rect);

            Assert(cap < rawNeeded,
                "the truncation clause needs a cap under the raw credit, or it asserts nothing: cap=" + cap);
            Assert(popup.width < rawNeeded,
                "the popup must follow the truncated display, not the raw credit: " + popup.width + " vs raw " + rawNeeded);
            Assert(popup.width - 12f <= cap + 0.01f,
                "the truncated display must fit the cap: " + (popup.width - 12f) + " > " + cap);
            Assert(popup.width <= viewport.width + 0.01f,
                "the grown popup must stay capped at the viewport: " + popup.width);

            UiFitAudit.Reset();
            reports.Clear();
            host.MeasureAndArrange(VerseUiExecution.Point(new UnityEngine.Vector2(viewport.width, viewport.height)));
            host.DrawChecked(viewport); // repaint with the popup open: the row is measured against its band
            Assert(
                reports.FindAll(r => r.ElementPath.Length == 0 || r.ElementPath.Contains("filter-bar")).Count == 0,
                "the bounded popup must render its long row without overflow: " + DescribeOverflow(reports));

            // The display-only proof: the option rows are drawn after content and outside the layout
            // groups, so the popup layer's window-space rect is the row's own space. Row 1 is the
            // over-cap author (row 0 is "AuthorA"), one OptionHeight below the popup top.
            Vector2 rowClick = new(popup.x + 8f, popup.y + UiPopup.OptionHeight + UiPopup.OptionHeight / 2f);
            DrawWithEvent(host, viewport, EventType.MouseDown, rowClick);
            DrawWithEvent(host, viewport, EventType.MouseUp, rowClick);
            Assert(!host.Session.IsPopupOpen("pack-filter"),
                "clicking an option row must close the popup; the pointer pump did not reach the row");
            Assert(string.Equals(fake.LastPackFilter, longAuthor, StringComparison.Ordinal),
                "the option row must write the machine token, not the truncated display: '"
                + (fake.LastPackFilter ?? "(none)") + "'");
        }
        finally
        {
            UiFitAudit.Detach();
            SetTranslatorResolver(null);
        }
    }

    private static void PopupOverflowReportCarriesElementIdentity()
    {
        string overCapAuthor = new string('A', 200); // StubMetrics Small: 1600px, beyond the 640px cap
        int pinnedWidth = Verse.UI.screenWidth;
        int pinnedHeight = Verse.UI.screenHeight;
        Verse.UI.screenWidth = 4000; // the current 1280px window ceiling keeps the display cap at 640px
        Verse.UI.screenHeight = 2000;
        try
        {
            var stub = new StubMetrics();
            var fake = new RecordingSettingsSource { RichData = true, Authors = new[] { overCapAuthor } };
            using UiHost host = UsKernelSettingsHost.Create(fake, stub);
            host.Bindings.Invoke("set-tab", "Packs");
            host.Bindings.Invoke("voice-filters");
            // The current 1280px window ceiling gives a 640px display cap, so a capped row must
            // overflow this viewport's 588px padded text band; the old 800px viewport no longer did.
            Rect viewport = new(0f, 0f, 600f, 600f);

            var reports = new List<UiOverflowReport>();
            UiFitAudit.Attach(stub, reports.Add);
            UiFitAudit.Enabled = true;
            SetTranslatorResolver(ReadKeyedTable("English"));
            try
            {
                host.MeasureAndArrange(VerseUiExecution.Point(new UnityEngine.Vector2(viewport.width, viewport.height)));
                host.DrawChecked(viewport);
                host.Session.OpenPopup("pack-filter", VerseUiExecution.Rect(new Rect(300f, 200f, 143f, 24f)));
                UiFitAudit.Reset();
                reports.Clear();
                host.MeasureAndArrange(VerseUiExecution.Point(new UnityEngine.Vector2(viewport.width, viewport.height)));
                host.DrawChecked(viewport);

                Assert(reports.Count > 0, "a popup row wider than the capped popup must be reported at all");
                foreach (UiOverflowReport report in reports)
                {
                    Assert(!string.IsNullOrEmpty(report.ElementPath) && report.ElementPath != "(unscoped)",
                        "every popup overflow report must carry the owner's element identity, got '" + report.ElementPath + "'");
                }

                Assert(reports.Exists(r => r.ElementPath.EndsWith("/popup", StringComparison.Ordinal)),
                    "the popup finding must be scoped to the owner's popup path: " + DescribeOverflow(reports));
            }
            finally
            {
                UiFitAudit.Detach();
                SetTranslatorResolver(null);
            }
        }
        finally
        {
            Verse.UI.screenWidth = pinnedWidth;
            Verse.UI.screenHeight = pinnedHeight;
        }
    }

    private static void PopupWidthFollowsWidestOptionThenViewport()
    {
        Rect anchor = new(300f, 200f, 143f, 24f);
        // As in the long-author case, the real trigger must leave room for option-driven growth.
        Rect viewport = new(0f, 0f, 700f, 600f);
        var stub = new StubMetrics();
        float perChar = stub.MeasureWidth("A", UiFont.Small);
        Assert(perChar > 0f, "the stub must measure a character, or every width clause below is vacuous");
        float cap = OptionDisplayCap();
        float floorWidth = DrawnPopupWidth(new[] { "a" }, stub, ref anchor, viewport);
        Assert(Math.Abs(floorWidth - anchor.width) < 0.01f,
            "an option list that fits its trigger must keep the trigger width as the popup floor: "
            + floorWidth + " vs anchor " + anchor.width);

        // Both samples have to survive the F5 cut, or this clause silently re-tests the truncation clause.
        int shortChars = Math.Max(20, (int)Math.Ceiling(anchor.width / perChar) + 1);
        int longChars = Math.Max(50, shortChars + 1);
        string twenty = new('A', shortChars);
        string fifty = new('A', longChars);
        Assert(shortChars * perChar <= cap && longChars * perChar <= cap,
            "the growth clause needs both option texts under the truncation cap (" + cap + "): the stub screen moved");
        float twentyWidth = DrawnPopupWidth(new[] { twenty }, stub, ref anchor, viewport);
        float fiftyWidth = DrawnPopupWidth(new[] { fifty }, stub, ref anchor, viewport);
        Assert(Math.Abs(twentyWidth - (shortChars * perChar + 12f)) < 0.01f,
            "the popup must be exactly the widest label plus the 6px-per-side row padding: "
            + twentyWidth + " vs " + (shortChars * perChar + 12f));
        Assert(Math.Abs(fiftyWidth - twentyWidth - (longChars - shortChars) * perChar) < 0.01f,
            "extra option characters must widen the popup by exactly their measured width: "
            + (fiftyWidth - twentyWidth) + " vs " + ((longChars - shortChars) * perChar));

        // The longest label that still fits the cap, and one far past it: both are cut to the same display,
        // so both popups are that display plus the row padding, and neither follows the raw label.
        int fittingChars = (int)Math.Floor(cap / perChar);
        string justFits = new('A', fittingChars);
        string overCap = new('A', 200);
        Assert(stub.MeasureWidth(justFits, UiFont.Small) <= cap + 0.01f,
            "the reference label must still fit the truncation cap");
        Assert(stub.MeasureWidth(overCap, UiFont.Small) > cap,
            "the truncation clause needs a label wider than the cap, or it asserts nothing");
        float fitsWidth = DrawnPopupWidth(new[] { justFits }, stub, ref anchor, viewport);
        float overCapWidth = DrawnPopupWidth(new[] { overCap }, stub, ref anchor, viewport);
        Assert(Math.Abs(overCapWidth - fitsWidth) < 0.01f,
            "an over-cap option must draw the same popup width as the longest option that fits the cap: "
            + overCapWidth + " vs " + fitsWidth);
        Assert(overCapWidth - 12f <= cap + 0.01f,
            "the option display must be truncated at the cap: " + (overCapWidth - 12f) + " > " + cap);
        Assert(overCapWidth - 12f > cap - perChar - 0.01f,
            "the truncated display must use the whole cap (within one glyph), not a smaller cut: "
            + (overCapWidth - 12f) + " <= " + (cap - perChar));
        Assert(overCapWidth < stub.MeasureWidth(overCap, UiFont.Small) + 12f,
            "the popup must not follow the raw over-cap label: " + overCapWidth);

        // Viewport clause. The window's current 1280px ceiling caps display at 640px even on a large
        // screen. Use a 600px viewport to exercise the same clamp rather than the old 800px assumption.
        int pinnedWidth = Verse.UI.screenWidth;
        int pinnedHeight = Verse.UI.screenHeight;
        try
        {
            Verse.UI.screenWidth = 4000;
            Verse.UI.screenHeight = 2000;
            viewport.width = 600f;
            float viewportCap = OptionDisplayCap();
            Assert(viewportCap >= viewport.width,
                "the viewport clause needs the truncation cap at or above the viewport: " + viewportCap);
            float cappedWidth = DrawnPopupWidth(new[] { overCap }, stub, ref anchor, viewport);
            Assert(Math.Abs(cappedWidth - viewport.width) < 0.01f,
                "a popup wider than its viewport must be capped at the viewport width: "
                + cappedWidth + " vs " + viewport.width);
        }
        finally
        {
            Verse.UI.screenWidth = pinnedWidth;
            Verse.UI.screenHeight = pinnedHeight;
        }
    }

    private static void OverlayDrawFailureDoesNotDoubleReserveRow()
    {
        // Regression: when the kernel overlay's pass fails, TryDraw must not commit the row cursor.
        // The caller then draws the legacy pure-Verse readout at the same curBaseY; if the kernel path
        // had already decremented it, the fallback would double-reserve the row height.
        //
        // The injection moved one level up with FL 0.3.0 item C: a widget that throws mid-draw is now
        // recovered BY THE ENGINE (UiSessionGuard.DrawWidget paints a recovery band and keeps the page
        // alive), so it never reaches this catch - the old throwing widget simply no longer tests the
        // path it was written for, and FL proves that split from its side. What still escapes
        // UiHost.DrawFrame is frame-level work the guard does not wrap, and the session popup pass is
        // exactly that: UiHost.Draw runs the registered popup callbacks after the content tree, with
        // nothing between them and the consumer's catch. That is the failure planted here.
        var source = new RecordingOverlaySource();
        var controller = new UsKernelOverlayController(source);
        FieldInfo? hostField = typeof(UsKernelOverlayController).GetField("host", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(hostField != null, "overlay controller exposes a host field for failure injection");

        float y = 600f - 34f;
        Assert(controller.TryDraw(16f, 200f, ref y), "the live overlay draws before anything fails");
        Assert(Math.Abs(y - (600f - 34f - 26f)) < 0.001f, "a successful kernel pass reserves exactly one row");

        var host = hostField!.GetValue(controller) as UiHost;
        Assert(host != null && host.Session.IsActive, "the draw pass must have created a live overlay host to fail");
        host!.Session.RegisterPopupDraw(() => throw new InvalidOperationException("injected overlay frame failure"));

        float before = y;
        Assert(!controller.TryDraw(16f, 200f, ref y), "a frame-level overlay failure returns false");
        Assert(Math.Abs(y - before) < 0.001f,
            "failed overlay draw leaves curBaseY untouched so the legacy fallback does not double-reserve the row");
        Assert(controller.CreationFailed, "draw failure trips the permanent fallback flag");
        Assert(!controller.IsActive, "draw failure disposes the overlay host");
        Assert(!host.Session.IsActive, "and the failing session is the one that was torn down");
        Assert(StubScrollDepth() == 0 && StubGroupDepth() == 0, "no scopes leaked by the failure path");

        // The permanent flag is the whole point of the handover: no retry, even though the injected
        // failure is gone.
        float stillY = before;
        Assert(!controller.TryDraw(16f, 200f, ref stillY) && Math.Abs(stillY - before) < 0.001f,
            "the controller never re-enters the kernel overlay once it tripped");
    }

    private static void SettingsWindowShellCarriesTheFailureContract()
    {
        var failing = new ShellProbeWindow(throwOnCreate: true) { Prerequisite = true };
        failing.windowRect = new Rect(0f, 0f, 900f, 700f);

        failing.WindowOnGUI();
        Assert(failing.HostCreations == 1, "the first pass tries to build the page");
        Assert(failing.Failures == 1, "and reports the throw exactly once");
        Assert(failing.Notices.Count == 0,
            "the throwing pass must not switch to the notice inside itself - that pass already claimed"
            + " the page's layout state (the condition c next-frame rule)");

        failing.WindowOnGUI();
        Assert(failing.Notices.Count == 1 && failing.Notices[0] == UiWindowNotice.PageUnavailable,
            "the next pass is the first unavailable notice, drawn from a clean pass");

        failing.WindowOnGUI();
        Assert(failing.HostCreations == 1 && failing.Failures == 1 && failing.Notices.Count == 2,
            "a tripped window neither retries the page nor re-logs it; it just keeps saying the same thing");

        var blocked = new ShellProbeWindow(throwOnCreate: false) { Prerequisite = false };
        blocked.windowRect = new Rect(0f, 0f, 900f, 700f);
        blocked.WindowOnGUI();
        Assert(blocked.HostCreations == 0, "an unmet prerequisite never reaches page creation");
        Assert(blocked.BuiltSession == null,
            "and there is no page session to check: a pass that builds nothing has no page that could have drawn");
        Assert(blocked.Notices.Count == 1 && blocked.Notices[0] == UiWindowNotice.Prerequisite,
            "and it names the desync instead of the generic unavailable page");

        var healthy = new ShellProbeWindow(throwOnCreate: false) { Prerequisite = true };
        healthy.windowRect = new Rect(0f, 0f, 900f, 700f);
        healthy.WindowOnGUI();
        Assert(healthy.HostCreations == 1 && healthy.Notices.Count == 0 && healthy.Failures == 0,
            "a real US page under the shell draws with no notice and no failure");
        UiSession? built = healthy.BuiltSession;
        Assert(built != null && built.IsActive,
            "the probe must have a live session to test the close path");
        // The two notice passes above build no session at all (nothing drew), but this pass draws a real
        // page under the shell: assert the page DREW, not merely that the window lived (see UsTripGuard).
        UsTripGuard.ExpectNoTrips(built!, "SettingsWindowShellCarriesTheFailureContract");
        healthy.PreClose();
        Assert(built != null && !built.IsActive,
            "the shell's PreClose disposes the page session - US keeps only its own audit teardown");
        Assert(healthy.BasePreCloseCalls == 1, "and the consumer override still chains to the shell");
    }

    private static void PressScopePopup(
        UiHost host, RecordingSettingsSource fake, Rect viewport, string actionKey, out Rect popup)
    {
        fake.LastActionScope = null;
        fake.LastActionKey = null;
        host.Session.ClosePopup();
        host.Session.OpenPopup("scope-tree-scope-" + Uri.EscapeDataString(actionKey), VerseUiExecution.Rect(new Rect(600f, 200f, 120f, 24f)));
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(viewport.width, viewport.height)));
        host.DrawChecked(viewport);
        Assert(TryGetPopupHitLayer(host.Session, out UiHitLayer layer),
            "the " + actionKey + " scope dropdown must publish its popup layer");
        popup = VerseUiExecution.Rect(layer.Rect);

        Vector2 missedPoint = new(popup.x + popup.width / 2f, popup.yMax + 8f);
        DrawWithEvent(host, viewport, EventType.MouseDown, missedPoint);
        DrawWithEvent(host, viewport, EventType.MouseUp, missedPoint);
    }

    private static BehaviorScope? ClickScopeOption(
        UiHost host, RecordingSettingsSource fake, Rect viewport, string actionKey, BehaviorScope? target)
    {
        // The option list the widget builds: Auto (null) first, then the action's supported states. Locating
        // the target the same way the widget does keeps the ROW index honest without this lane re-deriving
        // the filtering rule.
        var states = new List<BehaviorScope>();
        foreach (BehaviorScope scope in new[]
                 { BehaviorScope.Disabled, BehaviorScope.AnyOccurrence, BehaviorScope.ActiveCommand })
        {
            if (Authoring.BuiltInPawnContracts.NormalizeScope(TestActionOf(actionKey), scope) == scope) states.Add(scope);
        }

        int row = 0;
        if (target.HasValue)
        {
            row = states.IndexOf(target.Value) + 1;
            Assert(row > 0, "the target scope must be an allowed option for " + actionKey);
        }

        fake.LastActionScope = null;
        fake.LastActionKey = null;
        host.Session.ClosePopup();
        host.Session.OpenPopup("scope-tree-scope-" + Uri.EscapeDataString(actionKey), VerseUiExecution.Rect(new Rect(600f, 200f, 120f, 24f)));
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(viewport.width, viewport.height)));
        host.DrawChecked(viewport);

        Assert(Program.TryGetPopupHitLayer(host.Session, out UiHitLayer layer),
            "the " + actionKey + " scope dropdown must publish its popup layer");
        Assert(layer.Rect.Height >= (row + 1) * UiPopup.OptionHeight - 0.01f,
            "the popup must hold the row this press targets (row " + row + " of " + layer.Rect.Height + "px)");

        Vector2 press = new(layer.Rect.X + layer.Rect.Width / 2f,
            layer.Rect.Y + row * UiPopup.OptionHeight + UiPopup.OptionHeight / 2f);
        DrawWithEvent(host, viewport, EventType.MouseDown, press);
        DrawWithEvent(host, viewport, EventType.MouseUp, press);

        return fake.LastActionScope;
    }

    private static string TestActionOf(string actionKey)
    {
        if (!UniversalSqueaker.Kernel.BuiltInActionKeys.Contains(actionKey)) throw new ArgumentException("Unknown fixture action: " + actionKey);
        return actionKey;
    }

    internal static bool TryGetPopupHitLayer(UiSession session, out UiHitLayer layer)
    {
        foreach (UiHitLayer candidate in Fl08PublicObservation.Popup(session, out var observedPopup) ? new[] { observedPopup } : Array.Empty<UiHitLayer>())
        {
            if (candidate.IsPopup)
            {
                layer = candidate;
                return true;
            }
        }

        layer = default;
        return false;
    }

    internal static void DrawWithEvent(UiHost host, Rect viewport, EventType type, Vector2 pointer)
    {
        Event e = Event.KeyboardEvent("dummy");
        e.type = type;
        e.button = 0;
        e.mousePosition = pointer;
        Event.current = e;
        try
        {
            host.DrawChecked(viewport);
        }
        finally
        {
            Event.current = null;
        }
    }

    private static float OptionDisplayCap()
    {
        return Math.Max(
            UsFilterBarWidget.MinOptionDisplayWidth,
            WindowChromeLayout.SettingsWindowWidth(Verse.UI.screenWidth, Verse.UI.screenHeight) * 0.5f);
    }

    private static float DrawnPopupWidth(string[] authors, StubMetrics stub, ref Rect anchor, Rect viewport)
    {
        var fake = new RecordingSettingsSource { RichData = true, Authors = authors };
        using UiHost host = UsKernelSettingsHost.Create(fake, stub);
        host.Bindings.Invoke("set-tab", "Packs");
        host.Bindings.Invoke("voice-filters");
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(viewport.width, viewport.height)));
        host.DrawChecked(viewport); // prime the view cache before the popup opens
        host.Session.OpenPopup("pack-filter", VerseUiExecution.Rect(anchor));
        host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(viewport.width, viewport.height)));
        host.DrawChecked(viewport);
        Assert(TryGetPopupHitLayer(host.Session, out UiHitLayer layer),
            "an open pack-filter dropdown must publish its popup layer (authors: " + string.Join(",", authors) + ")");
        Assert(host.Session.OpenPopupAnchor.HasValue, "the drawn popup must have a live owner anchor");
        anchor = VerseUiExecution.Rect(host.Session.OpenPopupAnchor!.Value);
        return layer.Rect.Width;
    }

    private static string DescribeOverflow(List<UiOverflowReport> reports)
    {
        return string.Join(" | ", reports.ConvertAll(
            r => "(" + (r.ElementPath.Length == 0 ? "unscoped" : r.ElementPath) + " " + r.Axis
            + " " + r.Needed + "/" + r.Available + ")"));
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

    private sealed class ShellProbeWindow : UiWindowHost
    {
        private readonly bool throwOnCreate;

        public ShellProbeWindow(bool throwOnCreate)
        {
            this.throwOnCreate = throwOnCreate;
        }

        public readonly List<UiWindowNotice> Notices = new();
        public int HostCreations;
        public int Failures;
        public int BasePreCloseCalls;
        public bool Prerequisite = true;
        public UiSession? BuiltSession;

        protected override UiTheme Theme => UsTheme.Surface();
        protected override string Title => "probe title";
        protected override string Subtitle => "probe subtitle";
        protected override string CloseText => "probe close";
        protected override bool PrerequisiteVerified => Prerequisite;

        protected override UiHost CreateHost()
        {
            HostCreations++;
            if (throwOnCreate)
            {
                throw new InvalidOperationException("planted page creation failure");
            }

            // A real production US page, so the healthy pass exercises the shell against the manifest,
            // widgets and bindings the settings window actually uses.
            UiHost host = UsKernelOverlayHost.Create(new RecordingOverlaySource());
            BuiltSession = host.Session;
            return host;
        }

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
            Notices.Add(notice);
        }

        protected override void OnDrawFailure(Exception error)
        {
            Failures++;
        }

        public override void PreClose()
        {
            BasePreCloseCalls++;
            base.PreClose();
        }
    }

    internal static void SetScrollPositionById(UiSession session, string elementId, UiLayoutPoint position)
    {
        UiNode? node = session.GetNodeByElementId(elementId);
        Assert(node != null, "no arranged element carries the scroll id " + elementId + "; arrange before writing a scroll position");
        session.SetScrollPosition(node!, position);
    }

    internal static UiLayoutPoint ScrollPositionById(UiSession session, string elementId)
    {
        UiNode? node = session.GetNodeByElementId(elementId);
        return node == null ? UiLayoutPoint.Zero : session.GetScrollPosition(node);
    }
}
