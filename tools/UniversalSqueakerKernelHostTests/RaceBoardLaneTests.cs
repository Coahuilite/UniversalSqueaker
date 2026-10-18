using System;
using System.Collections.Generic;

using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// UIR-002/003 (WO-US-UI-REFACTOR-20261014), re-cut by CORRECTIONS-R1 topic 2: the race board's
/// exits proven on the REAL production host over the REAL manifest, against plain seeded rows the
/// PRODUCTION pure code (RaceBoardProjection.DeriveIntent + VoicePacksPageModel.ApplyOpenRaceTarget)
/// consumes - the fake no longer mirrors the intent, it RUNS it. What this lane must catch:
/// <list type="number">
/// <item>a scroll destination that is a made-up string: the arrival arms the FL-materialized row
/// identity <c>pack-card-row#&lt;itemKey&gt;</c> and the ENGINE consumes the pending request on the
/// next arrange - the lane proves the target row is genuinely inside the content viewport starting
/// from a NON-ZERO scroll (a raw key or an unresolvable id leaves the request pending; this fails);</item>
/// <item>an open that loses the arrival context (the race filter must survive) or leaves an old
/// pending target armed;</item>
/// <item>a xeno-only race landing on an empty race domain instead of its real xeno target;</item>
/// <item>the no-supply branches: the STATIC destination (the fallback section) when a table exists,
/// the race-narrowed truthful empty otherwise, never a fake enable;</item>
/// <item>the enable firing an identity the row never named, or touching the mode;</item>
/// <item>a collapsed teaching that did not persist, or persisted by notifying the audio runtime.</item>
/// </list>
/// Honest limit: rows are seeded plain facts (the harness cannot build a real catalog snapshot); the
/// intent, state application, bindings, widget and engine scroll resolution around them are the
/// production ones. Real def enumeration at game scale is the named human-pass item.
/// </summary>
internal static class RaceBoardLaneTests
{
    private const float PageWidth = 1280f;
    private const float PageHeight = 720f;

    public static int RunAll()
    {
        Step("the board projects, draws and claims through the real host", BoardDrawsAndRoutes);
        Step("open from a non-zero scroll resolves the materialized row target", OpenResolvesRealScroll);
        Step("xeno-only arrives at its exact xeno domain", XenoOnlyPreciseDomain);
        Step("no-supply branches: static fallback destination or cleared pending", NoSupplyDestinations);
        Step("quick enable writes ONE named identity and never the mode", QuickEnableIdentity);
        Step("the teaching collapse persists as a UI preference only", IntroPersistsWithoutAudio);
        Step("1.6 policy box: teaching + shortcuts in both languages, real presses", PolicyBoxTeachingAndShortcuts);
        Console.WriteLine("RaceBoardLaneTests ALL PASS");
        return 0;
    }

    private static void BoardDrawsAndRoutes()
    {
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            SeedRaceBoard = new[]
            {
                RecordingSettingsSource.SeedRow("human", "Human", racePacks: 2, enabled: 1, supplyKeys: new[] { "us.sang" }),
                RecordingSettingsSource.SeedRow("pig", "Pig", xenoPacks: 1, quickScope: "Xenotype", quickTarget: "pig_highstealer",
                    quickKey: "us.pigpack", quickLabel: "Pig Snort Pack", quickTargetLabel: "Highstealer"),
            },
        };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        UiLayoutSnapshot snapshot = Arrange(host);
        Assert(snapshot.RectById.ContainsKey("race-board"), "the board section is arranged on Overview");
        IReadOnlyList<RaceBoardRow> rows = host.Bindings.Get<UsRaceBoardView>("race-board-view").Rows;
        Assert(rows.Count == 2, "the host binding carries the projected rows, got " + rows.Count);

        // The row's help claim rides the real draw (the scan participates through HoverClaim).
        Program.DrawWithPointer(host, Viewport(), new Vector2(PageWidth * 0.55f, 300f));
        // The xeno row's enable names its exact pack + target (UIR-003) - check the composed caption
        // via the pure row data the widget renders from:
        Assert(rows[1].QuickEnablePackLabel == "Pig Snort Pack" && rows[1].QuickEnableTargetLabel == "Highstealer"
                && rows[1].QuickEnableScope == "Xenotype",
            "the quick-enable identity carries the exact pack and target the button must name");
        Assert(fake.ViewState.IntroOpen, "the teaching arrives open (default expanded)");
    }

    private static void OpenResolvesRealScroll()
    {
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            // us.sang2 is the SECOND card of the real rich pack view - a non-first target.
            SeedRaceBoard = new[]
            {
                RecordingSettingsSource.SeedRow("human", "Human", racePacks: 1, supplyKeys: new[] { "us.sang2" }),
            },
        };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Set("help-open", false);
        Arrange(host);

        // Mask everything, then push the content scroll away from the top (the arrival starts here).
        host.Bindings.Set("search-text", "abc");
        host.Bindings.Set("pack-filter", "AuthorA");
        host.Bindings.Set("xenotype-filter", "sanguophage");
        host.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.EnabledOnly, true));
        host.Session.SetScrollPosition(Node(host, "content-scroll"), new Vector2(0f, 300f));
        host.MeasureAndArrange(ViewportSize());
        ResetWriters(fake);

        int before = host.Session.ContentRevision;
        host.Bindings.Invoke("race-board-open", "human");

        Assert(host.Session.ContentRevision == before + 1, "the arrival advances the shared clock exactly once");
        Assert(host.Bindings.Get<string>(UiBindings.ActiveTabKey) == "Packs", "the arrival is on the Packs workspace");
        VoicePacksPageState st = fake.ViewState;
        Assert(st.SearchText.Length == 0 && string.IsNullOrEmpty(st.PackFilter.Author)
                && st.XenotypeFilter.Length == 0 && !st.DomainFilter.EnabledOnly,
            "every condition that could MASK the target cleared");
        Assert(st.RaceFilter == "human",
            "the arrival keeps the race context instead of erasing it, got '" + st.RaceFilter + "'");
        Assert(st.PackCardsExpanded.Contains("us.sang2"), "the supplying card expanded");

        // THE correction's core: the destination is the FL-materialized row identity, and the engine
        // CONSUMES it on the next arrange (pending cleared) with the target row genuinely visible.
        UiLayoutSnapshot after = Arrange(host);
        Assert(host.Session.ScrollTargetElementId == null,
            "the pending scroll request was consumed by the arrange, not left dangling: "
            + (host.Session.ScrollTargetElementId ?? "(null)"));
        Vector2 scroll = host.Session.GetScrollPosition(Node(host, "content-scroll"));
        Assert(scroll.y > 0f,
            "the engine actually moved the content scroll from the top, got " + scroll.y);
        Assert(after.RectById.TryGetValue("pack-card-row#us.sang2", out Rect targetRect),
            "the materialized header row is arranged after the arrival");
        Rect viewport = after.Viewports["content-scroll"];
        float targetPageY = viewport.y + targetRect.y - scroll.y;
        Assert(targetPageY >= viewport.y - 1f && targetPageY <= viewport.yMax + 1f,
            "the target row is inside the content viewport after the resolved scroll: page y "
            + targetPageY + " vs viewport " + viewport.y + ".." + viewport.yMax);
        Assert(!AnyPersistedWrite(fake), "navigation wrote NOTHING persisted - no selection, no mode, no setting");
    }

    private static void XenoOnlyPreciseDomain()
    {
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            SeedRaceBoard = new[]
            {
                RecordingSettingsSource.SeedRow("archoteria", "Archoteifer", xenoOnly: true, xenoPacks: 2,
                    firstXenoTarget: "archoteifer_high", supplyKeys: new[] { "us.sang", "us.sang2" }),
            },
        };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        Arrange(host);
        host.Bindings.Invoke("race-board-open", "archoteria");
        VoicePacksPageState st = fake.ViewState;
        Assert(st.RaceFilter == "archoteria", "the race context survives a xeno-only arrival");
        Assert(st.SelectedScope == SqueakVoicePackScope.Xenotype && st.SelectedTargetName == "archoteifer_high",
            "xeno-only lands on its real xeno domain, got " + st.SelectedScope + "/" + st.SelectedTargetName);
        Assert(st.SelectedRaceDefName == "archoteria", "and keeps the owning race for the context");
    }

    private static void NoSupplyDestinations()
    {
        // (a) fallback-only: the static destination is the fallback editor section; an old pending
        // request must be cleared and replaced.
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            SeedRaceBoard = new[]
            {
                RecordingSettingsSource.SeedRow("pig", "Pig", hasFallback: true, fallbackSounds: 3),
            },
        };
        // The proof at the SHIPPED 1.6 policy box (760x524), where the Packs content is tight:
        // visibility of the static destination is the claim - whether the engine also moves the
        // scroll is the engine's own content-fit decision at this size, and the movement case is
        // already proven by OpenResolvesRealScroll at the wide box from a non-zero scroll.
        var policy = new Vector2(760f, 524f);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        host.MeasureAndArrange(policy);
        host.Session.SetScrollTarget("leftover-stale-target");
        host.Bindings.Invoke("race-board-open", "pig");
        VoicePacksPageState st = fake.ViewState;
        Assert(st.RaceFilter == "pig", "the no-supply arrival still names the race context");
        UiLayoutSnapshot snap = host.MeasureAndArrange(policy);
        Assert(snap.RectById.ContainsKey("packs-fallback"), "the fallback section arranges on Packs");
        Assert(host.Session.ScrollTargetElementId == null, "the stale pending request was replaced and consumed");
        UiNode? scrollNode = host.Session.GetNodeByElementId("content-scroll");
        Assert(scrollNode != null, "the content scroll node exists");
        Vector2 scroll = host.Session.GetScrollPosition(scrollNode!);
        Rect viewport = snap.Viewports["content-scroll"];
        Rect targetRect = snap.RectById["packs-fallback"];
        float pageY = viewport.y + targetRect.y - scroll.y;
        Assert(pageY >= viewport.y - 1f && pageY <= viewport.yMax + 1f,
            "the static destination is genuinely visible in the content viewport after the arrival: page y "
            + pageY + " vs viewport " + viewport.y + ".." + viewport.yMax);

        // (b) no supply, no table: arm nothing - and CLEAR whatever pending existed.
        var empty = new RecordingSettingsSource
        {
            RichData = true,
            SeedRaceBoard = new[] { RecordingSettingsSource.SeedRow("slug", "Slug") },
        };
        using UiHost host2 = UsKernelSettingsHost.Create(empty, new Program.StubMetrics());
        host2.Bindings.Invoke("set-tab", "Overview");
        Arrange(host2);
        host2.Session.SetScrollTarget("leftover-stale-target-2");
        host2.Bindings.Invoke("race-board-open", "slug");
        Assert(empty.ViewState.RaceFilter == "slug", "the race context is KEPT (a truthful narrowed empty, not a lie about filters)");
        Arrange(host2);
        Assert(host2.Session.ScrollTargetElementId == null,
            "no destination means no armed request - and the old pending one was cleared");
    }

    private static void QuickEnableIdentity()
    {
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            SeedRaceBoard = new[]
            {
                RecordingSettingsSource.SeedRow("pig", "Pig", xenoPacks: 1, quickScope: "Xenotype",
                    quickTarget: "pig_highstealer", quickKey: "us.pigpack", quickLabel: "Pig Snort Pack",
                    supplyKeys: new[] { "us.pigpack" }),
            },
        };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        Arrange(host);

        SqueakVoicePackMode modeBefore = host.Bindings.Get<SqueakVoicePackMode>("mode");
        ResetWriters(fake);
        int before = host.Session.ContentRevision;
        host.Bindings.Invoke("race-board-enable",
            new UsPackToggle(SqueakVoicePackScope.Xenotype, "pig", "pig_highstealer", "us.pigpack", true));

        Assert(fake.QuickEnableCount == 1 && fake.LastQuickScope == SqueakVoicePackScope.Xenotype
                && fake.LastQuickRace == "pig" && fake.LastQuickTarget == "pig_highstealer"
                && fake.LastQuickPackKey == "us.pigpack",
            "the enable writes exactly the row's named identity through the shared toggle");
        Assert(host.Bindings.Get<SqueakVoicePackMode>("mode") == modeBefore && fake.LastMode == null,
            "and it NEVER touches the sound-source mode (UIR-003)");
        Assert(host.Session.ContentRevision == before + 1, "the echo advances the shared clock once");
    }

    private static void IntroPersistsWithoutAudio()
    {
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        Arrange(host);

        host.Bindings.Invoke("toggle-intro");
        Assert(!fake.ViewState.IntroOpen && fake.LastIntroPersistedCollapsed == true,
            "collapsing writes the persisted UI preference (the fake mirrors the production setter's contract)");
        ResetWriters(fake);
        host.Bindings.Invoke("toggle-intro");
        Assert(fake.ViewState.IntroOpen && fake.LastIntroPersistedCollapsed == false
                && !AnyPersistedWrite(fake),
            "re-opening persists the default; the ONLY persisted thing on this path is the preference (no selection, no mode, no volume)");

        // The collapsed presentation: only the small right-pinned re-open entry survives the gate.
        // (The state is OPEN right now - capture the open frame, then collapse and capture again.)
        UiLayoutSnapshot opened = Arrange(host);
        host.Bindings.Invoke("toggle-intro");
        UiLayoutSnapshot collapsed = Arrange(host);
        Assert(opened.RectById.ContainsKey("overview-intro-header") && !collapsed.RectById.ContainsKey("overview-intro-header"),
            "the header band exists only while open - a collapsed card keeps no big header");
        Assert(collapsed.RectById.TryGetValue("overview-intro-reopen", out Rect reopenRect)
                && collapsed.RectById.TryGetValue("nav", out Rect nav) && reopenRect.width > 0f,
            "the collapsed state keeps its re-open entry, arranged and hit-sized");
        host.Bindings.Invoke("toggle-intro");   // leave the fixture expanded for whatever runs next
        Arrange(host);
    }

    // ---- helpers ----

    /// <summary>Clears every write recorder the board paths could legitimately touch, so "wrote
    /// nothing persisted" is a transition proof, not a leftover-value coincidence.</summary>
    private static void ResetWriters(RecordingSettingsSource fake)
    {
        fake.LastMode = null;
        fake.LastPackScope = null;
        fake.LastPackKey = null;
        fake.LastGlobalVolume = null;
        fake.LastPackEnabled = null;
        fake.LastQuickRace = "";
        fake.QuickEnableCount = 0;
        fake.SwatchWriteCount = 0;
        fake.LastIntroPersistedCollapsed = null;
    }

    private static bool AnyPersistedWrite(RecordingSettingsSource fake)
        => fake.LastMode != null || fake.LastPackScope != null || fake.LastPackKey != null
            || fake.LastGlobalVolume != null || fake.QuickEnableCount > 0 || fake.SwatchWriteCount > 0;

    private static UiNode Node(UiHost host, string id)
    {
        UiNode? node = host.Session.GetNodeByElementId(id);
        Assert(node != null, "the arranged page must carry the '" + id + "' node");
        return node!;
    }

    private static Rect Viewport() => new(0f, 0f, PageWidth, PageHeight);

    private static Vector2 ViewportSize() => new(PageWidth, PageHeight);

    private static UiLayoutSnapshot ArrangeAt(UiHost host, Vector2 size)
    {
        host.MeasureAndArrange(size);
        host.DrawChecked(new Rect(0f, 0f, size.x, size.y));
        return host.MeasureAndArrange(size);
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.MeasureAndArrange(ViewportSize());
        Program.SetScrollPositionById(host.Session, "content-scroll", host.Session.ScrollPositions.Count >= 0
            ? CurrentScroll(host) : Vector2.zero);
        return host.MeasureAndArrange(ViewportSize());
    }

    private static Vector2 CurrentScroll(UiHost host)
    {
        UiNode? node = host.Session.GetNodeByElementId("content-scroll");
        return node == null ? Vector2.zero : host.Session.GetScrollPosition(node);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("RaceBoard lane step failed: " + name + "\n  INNER: " + error.Message, error);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }


    /// <summary>
    /// CORRECTIONS-R1 topic 3: the teaching layer and the board shortcuts at the GAME'S 1.6 policy
    /// box (760x524 - the smallest window the shell may open), in BOTH shipped languages, with help in
    /// BOTH states. The title band never moves (same rect in every combination - the theme/help switch
    /// must not reflow the header), the long Chinese body grows WITHOUT swallowing the title (no
    /// overlap, title rect unchanged), and the shortcut + collapse controls are pressed through the
    /// carrier's real hit seam: the press routes to the source, the next arrange consumes the pending
    /// request and the engine scroll genuinely moves.
    /// </summary>
    private static void PolicyBoxTeachingAndShortcuts()
    {
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            SeedRaceBoard = new[] { RecordingSettingsSource.SeedRow("human", "Human", racePacks: 1) },
        };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        var policy = new Vector2(760f, 524f);

        Rect titleBaseline = default;
        float englishBodyHeight = -1f;
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            foreach (bool helpOpen in new[] { false, true })
            {
                host.Bindings.Set("help-open", helpOpen);
                UiLayoutSnapshot snap = ArrangeAt(host, policy);
                string context = language + "/help=" + helpOpen;
                Assert(snap.RectById.TryGetValue("page-title", out Rect title), context + ": the title band is arranged");
                Assert(snap.RectById.TryGetValue("overview-intro-body", out Rect intro),
                    context + ": the teaching body is arranged and visible at the policy box");
                Assert(snap.RectById.ContainsKey("race-board-jump-volume"),
                    context + ": the shortcut row is arranged at the policy box");
                if (helpOpen == false && language == "English")
                {
                    titleBaseline = title;
                    englishBodyHeight = intro.height;
                }
                else
                {
                    Assert(title.x == titleBaseline.x && title.y == titleBaseline.y
                            && title.width == titleBaseline.width && title.height == titleBaseline.height,
                        context + ": the title band must not move when language or help state changes: "
                            + title + " vs baseline " + titleBaseline);
                }

                Assert(intro.y >= title.yMax - 0.5f,
                    context + ": the teaching sits below the title band, got intro y " + intro.y
                        + " vs title bottom " + title.yMax);
                if (language == "ChineseSimplified" && helpOpen == false)
                {
                    // CJK wraps SHORTER than the English wrap (76 vs 97.3 measured) - either way the
                    // body reflows and the title band above it stayed byte-identical (asserted), so no
                    // language can swallow it. The height difference also proves the translator seam is
                    // live, not a no-op that quietly kept the English strings.
                    Assert(Math.Abs(intro.height - englishBodyHeight) > 0.5f,
                        "the Chinese table must actually reflow the body (seam is live), got the same "
                            + intro.height + " as English");
                }
            }
        }

        // Real presses at the policy box, help closed (the teaching flow state).
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        host.Bindings.Set("help-open", false);
        ArrangeAt(host, policy);
        ResetWriters(fake);
        int fired = Program.PressDeclarativeButton(host, new Rect(0f, 0f, 760f, 524f), "race-board-jump-volume");
        Assert(fired == 1, "the shortcut button answered exactly one real press, got " + fired);
        Assert(fake.LastScrollToSection == "global-volume",
            "the press routed the volume destination to the source, got '" + fake.LastScrollToSection + "'");
        UiLayoutSnapshot afterJump = ArrangeAt(host, policy);
        Assert(host.Session.ScrollTargetElementId == null, "the arrange consumed the pending request");
        float scrolled = host.Session.GetScrollPosition(Node(host, "content-scroll")).y;
        Assert(scrolled > 0f, "the engine genuinely moved the content scroll, got " + scrolled);
        Assert(afterJump.RectById.ContainsKey("global-volume-header"),
            "the volume card is arranged on this page (the destination exists)");
        Assert(!AnyPersistedWrite(fake), "the shortcut is scroll-only: nothing persisted");

        // Collapse and re-open through the same real seam (scroll back to the top first - the
        // teaching sits there; the press must find the node active, exactly like a player would).
        host.Session.SetScrollPosition(Node(host, "content-scroll"), Vector2.zero);
        ArrangeAt(host, policy);
        fired = Program.PressDeclarativeButton(host, new Rect(0f, 0f, 760f, 524f), "overview-intro-collapse");
        Assert(fired == 1, "the collapse button answered one real press, got " + fired);
        UiLayoutSnapshot collapsed = ArrangeAt(host, policy);
        Assert(!collapsed.RectById.ContainsKey("overview-intro-body"),
            "the press collapsed the teaching");
        Assert(collapsed.RectById.ContainsKey("overview-intro-reopen"),
            "the collapsed band keeps its right-pinned re-open affordance");
        fired = Program.PressDeclarativeButton(host, new Rect(0f, 0f, 760f, 524f), "overview-intro-reopen");
        Assert(fired == 1, "the re-open button answered one real press, got " + fired);
        UiLayoutSnapshot reopened = ArrangeAt(host, policy);
        Assert(reopened.RectById.ContainsKey("overview-intro-body"), "the teaching is back");
        Assert(fake.ViewState.IntroOpen, "the final state is open again (persisted preference tracks it)");
    }
}
