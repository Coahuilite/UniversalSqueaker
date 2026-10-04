using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The frame guard: the page frame is a SHAPE, and a shape can be asserted without a screenshot.
///
/// <para>
/// What this lane is for. The page is a stack of bands - the fixed header band, the flex body row (nav plus
/// centre), the bottom help band while help is open, and the footer band - and the acceptance for that
/// skeleton is "five viewports of geometry: no overlap, no negative width, inside the bounds". This is that
/// acceptance, written as a guard every later step has to keep green instead of a milestone nobody re-runs.
/// </para>
/// <para>
/// <b>BH1 (2026-10-05) rewrote the help half of this shape, and this file was re-cut with it.</b> The old
/// presentation was a third column INSIDE the body row, beside the centre, bought by a second window width
/// for the open state, with a defensive full-width band as the fallback whenever a box could not afford the
/// column. All of it existed to answer one question - "does this page box afford the help side by side?" -
/// and BH1 removed the question, not merely its answer. The panel is ONE declared presentation now: the
/// <c>help-scroll</c> Scroll that reserves HEIGHT above the footer, gated by <c>help-open</c> and written
/// only by the footer's <c>help-toggle</c> (command <c>toggle-help-drawer</c>). A height reservation cannot
/// widen or move the window, so the policy has exactly one width per screen and the shell hands the page the
/// same box with help open or closed. The frame lane's job therefore changed from picking the presentation a
/// box affords to proving that a help state can never eat the settings - which is what the two help-shape
/// steps below now assert.
/// </para>
/// <para>
/// The five probe widths are PAGE BOXES handed to <c>MeasureAndArrange</c> (the shell's ContentRect, i.e. the
/// window minus the chrome insets), NOT window or inner widths, and they are SYNTHETIC stress inputs. Since
/// BH1 the policy answers one box per screen and its floor is the smallest box a real window can hand the
/// page, so 736, 480 and 320 are below anything the game produces - they exist to push the body row across
/// its own Breakpoint - while 1280 and 1024 sit inside the reachable range without being the box of any
/// accepted screen. None of them is evidence about the window policy itself (that is
/// <c>WindowChromeLayout</c>'s own lane) and none is a real screen; they prove a stress property of the frame
/// only. Everything is asserted in BOTH shipped languages, with help in BOTH states, on every one of the five
/// workspaces, because the frame is the one thing all of them share.
/// </para>
/// <para>
/// <b>The coordinate-space trap this lane exists next to.</b> A snapshot rect and a recorded control rect
/// are NOT always in the same space: an element inside a <c>Scroll</c> has its arranged rect in the
/// containing space, while the rect a widget hands to <c>UiNative</c> is in the SCROLL's local space (the
/// engine's <c>ToDrawRect</c> subtracts the scroll container's own rect position for its children). A lane
/// that filters one by the other is correct only by alignment, and it breaks the first time the frame
/// shifts - which is how the S3 header band turned `MoodLayoutFocusedTests` red with no product fault at
/// all. Translate both into one space first, or compare within one space only.
/// </para>
/// <para>
/// What it deliberately does not do: it does not restate the engine's own breakpoint arithmetic as an
/// expectation - that would be a tautology. It asserts the structural facts that arithmetic has to produce
/// (the body row's two columns are either side by side or stacked, a stacked column shares the full inner
/// width, a declared width is the arranged width in the side-by-side regime, and the help band spans the body
/// row's width from BELOW in either regime). The two ENDPOINTS are product invariants and are asserted
/// outright: 1280 keeps the body row side by side, 320 stacks it, and both carry the band under the row at the
/// page's inner width - the regime never depends on the help state, because help no longer buys a column.
/// </para>
/// <para>
/// <b>What the first run of this lane found, and what is mutation-proven.</b> Against the frame as it stood,
/// it reddened on a real defect: in the stacked (narrow) regime the fixed-width nav column declared
/// <c>Fill="true"</c> while it was a plain Column, so it took an equal height share (212px) of the stacked row
/// while <c>us/nav</c> measures 271px of natural content - the nav painted 60px into the column below it. The
/// fix at the time removed the redundant <c>Fill</c>; V1 then gave the nav its own Scroll viewport, which is
/// the durable answer, because a short nav clips and scrolls instead of overflowing its slot. The defect was
/// NOT player-reachable when it was found and still is not: the window floor keeps the page box wide enough
/// that the body row's inner width stays above its own Breakpoint 500, so no real screen stacks the row. It
/// becomes reachable the moment a slice lowers that floor or raises the breakpoint, which is why the guard
/// exists.
/// </para>
/// <para>
/// Historical evidence, per clause. <b>Mutation-proven on the old plain Column:</b> containment and
/// cross-parent overlap (restoring <c>Fill="true"</c> overflowed both). V1 now uses a real Scroll, whose
/// clipped content may exceed its viewport; SettingsGeometryLaneTests proves the last destination reachable.
/// The original non-scroll containment guards remain, including the
/// containment clause alone (parking <c>banner</c> outside its parent's rect with
/// <c>AlignX="Left" OffsetX="-60"</c> reddens it with every other step still green). <b>Guarded, not
/// mutation-proven:</b> the finite/non-negative clause and the "every scroll box is a real box" clause -
/// the engine clamps both, so no manifest edit reaches them; they are future-regression guards and are
/// labelled as such rather than counted as evidence. The side-by-side-regime <c>Width</c> clause is redundant
/// on purpose: raising the breakpoint so 1280 stacks reddens the suite, but on the older
/// <c>ThreeViewportMeasureAndDraw</c> version of the same invariant before this lane is reached.
/// </para>
/// </summary>
internal static class FrameGeometryLaneTests
{
    private static readonly float[] Widths = { 1280f, 1024f, 736f, 480f, 320f };
    private static readonly string[] Languages = { "English", "ChineseSimplified" };
    private static readonly string[] Tabs = { "Overview", "Distance", "Packs", "Tuning", "Presets" };

    private const float Height = 720f;
    private const float Tol = 0.51f;

    /// <summary>The live help-contract names (BH1), spelled once here because every help-state step, both
    /// band steps and the structural gates read the SAME four names. A retired name cannot slip back in
    /// through a loose literal when there is exactly one place to write it.</summary>
    private const string HelpBandId = "help-scroll";
    private const string HelpToggleId = "help-toggle";
    private const string HelpOpenKey = "help-open";
    private const string ToggleCommand = "toggle-help-drawer";

    public static int RunAll()
    {
        Step("no degenerate rect, no sibling overlap, every element inside its parent", TheFrameHoldsEverywhere);
        Step("the frame at the minimum logical resolution shares the page with the bottom help band",
            TheMinimumResolutionFrameSharesThePage);
        Step("the unreachable sub-minimum probe: a help state never eats the settings body",
            TheSubMinimumProbeStillSharesThePage);
        Step("the frame keeps its declared shape across the five viewports, help open or closed",
            TheFrameKeepsItsShape);
        Step("the header band is fixed with the title; the footer band is fixed with the page's ONLY help switch",
            TheHeaderAndFooterBandsAreFixed);
        Step("placement vocabulary is refused where its container does not own the axis", PlacementIsRefusedWhereTheContainerDoesNotOwnIt);
        Step("every page container states its own rhythm instead of inheriting it", PageRhythmIsExplicit);
        Console.WriteLine("FrameGeometryLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Structural invariants over the whole matrix.
    // ---------------------------------------------------------------------------------------------

    private static void TheFrameHoldsEverywhere()
    {
        UiLayoutManifest manifest = LoadManifest();
        var problems = new List<string>();

        foreach (string language in Languages)
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach (float width in Widths)
                {
                    foreach (bool open in new[] { false, true })
                    {
                        foreach (string tab in Tabs)
                        {
                            var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                            using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                            host.Bindings.Invoke("set-tab", tab);
                            host.Bindings.Set(HelpOpenKey, open);

                            // Arrange once to create the nodes the scroll write needs, then arrange the
                            // frame this lane records, then draw it: the measured and the drawn page are
                            // one geometry, which is the invariant the whole library exists to keep.
                            host.MeasureAndArrange(new Vector2(width, Height));
                            Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, Height));
                            host.DrawChecked(new Rect(0f, 0f, width, Height));

                            string context = width.ToString("0", CultureInfo.InvariantCulture) + "px/" + language
                                + "/" + tab + "/" + (open ? "open" : "closed");


                            CheckRects(snapshot, context, problems);
                            var window = new Rect(0f, 0f, width, Height);
                            var placed = new List<Placed>();
                            foreach (UiElementSpec root in manifest.Roots)
                            {
                                Collect(root, window, "<page>", new List<string>(), false, snapshot, context,
                                    placed, problems);
                            }

                            CheckOverlaps(placed, problems, context);
                        }
                    }
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }

        Assert(problems.Count == 0, Report(problems));
    }

    /// <summary>
    /// The frame at the game's MINIMUM logical resolution - the configuration the B3 playtest actually
    /// walked, and the one the rest of this lane cannot reach.
    ///
    /// <para>
    /// Re-cut 2026-10-05 (BH1). This step used to be about a SECOND page box: the old help presentation
    /// widened the window, so the open state was walked on the widened box and the step's whole purpose was
    /// whether that widened body could still afford a third column. The widening is retired - the policy now
    /// answers exactly ONE width per screen, because the panel pays for itself out of the body's HEIGHT - so
    /// <see cref="PageBoxFor"/> no longer takes a help state and the box below is one box, derived from
    /// <c>WindowChromeLayout.SettingsWindowWidth</c> plus the chrome insets and therefore the SAME box in both
    /// help states. A derived box cannot prove the help state is irrelevant to it, so the step asserts that
    /// the body row's WIDTH is compared BETWEEN the two help-state arranges, which reddens the first policy or
    /// manifest edit that ever makes help buy width again.
    /// </para>
    ///
    /// <para>
    /// What it keeps proving (the frame is a SHARING frame at the smallest real screen): the body row, its nav
    /// column and its centre scroll are arranged in BOTH help states; the band is arranged exactly while help
    /// is open, spans the body row's width, sits under the row and above the footer band; the footer stays
    /// inside the page; nothing overlaps and no rect degenerates. What it used to prove is unreachable now:
    /// "the fallback band is never the presentation here" compared TWO presentations for one box, and a box
    /// selects no presentation any more - the band and the body coexist by construction, so the clause is now
    /// "body arranged in both states, band exactly while open". The shape-premise clause below is still the
    /// one a faithful revert reddens: reintroduce a branch that hides the body while help is open and it
    /// reports before the walk runs.
    /// </para>
    /// </summary>
    private static void TheMinimumResolutionFrameSharesThePage()
    {
        UiLayoutManifest manifest = LoadManifest();
        var problems = new List<string>();
        int savedWidth = Verse.UI.screenWidth;
        int savedHeight = Verse.UI.screenHeight;
        try
        {
            // The game's MINIMUM logical resolution. `box` is the shell's ContentRect for this screen - the
            // page BOX, not the inner width: arranging the inner width (window minus the shell's horizontal
            // chrome only) under-measures the body row by the page's own Padding, which is the 24px error this
            // helper was introduced to remove. One box, because the policy has one width and help reserves
            // height.
            Verse.UI.screenWidth = 1024;
            Verse.UI.screenHeight = 768;
            Vector2 box = PageBoxFor(1024f, 768f);

            foreach (string language in Languages)
            {
                Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
                float bodyWidthClosed = float.NaN;
                foreach (bool open in new[] { false, true })
                {
                    var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                    using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                    host.Bindings.Invoke("set-tab", "Overview");
                    host.Bindings.Set(HelpOpenKey, open);
                    UiLayoutSnapshot snapshot = host.MeasureAndArrange(box);
                    host.DrawChecked(new Rect(0f, 0f, box.x, box.y));

                    string context = "minimum-resolution frame " + (open ? "open" : "closed") + "/" + language;
                    CheckRects(snapshot, context, problems);

                    bool bodyArranged = snapshot.RectById.ContainsKey("body-row");
                    bool navArranged = snapshot.RectById.ContainsKey("nav-column");
                    bool centreArranged = snapshot.Viewports.ContainsKey("content-scroll");
                    bool bandArranged = snapshot.Viewports.ContainsKey(HelpBandId);

                    // The SHAPE PREMISE of the walk below: the settings body row, its nav column and its centre
                    // scroll stay arranged in BOTH help states, and the bottom band joins them exactly while help
                    // is open. A revert that hides the body while help is open reddens this clause before the
                    // walk runs - it is the frame lane's half of the "help never eats the settings" contract.
                    Assert(bodyArranged && navArranged && centreArranged && bandArranged == open,
                        context + ": the minimum-resolution frame must SHARE the page (body row, nav column and"
                        + " centre scroll arranged in both help states; band exactly while help is open); body="
                        + bodyArranged + " nav=" + navArranged + " centre=" + centreArranged
                        + " band=" + bandArranged);

                    Assert(snapshot.RectById.ContainsKey("footer-band"),
                        context + ": the frame must carry its footer band at the minimum resolution");
                    Rect body = snapshot.RectById["body-row"];
                    Rect footer = snapshot.RectById["footer-band"];
                    if (bandArranged)
                    {
                        // The band is a stacked SIBLING of the body row, not a column inside it: it takes the
                        // body's width, it lands below the row, and it stops above the footer band. Every number
                        // here is read off the arrange, so nothing in this file restates the policy.
                        Rect band = snapshot.Viewports[HelpBandId];
                        Assert(Math.Abs(band.width - body.width) <= Tol,
                            context + ": the help band must span the SAME width as the body row, not a column"
                            + " beside it; band=" + Fmt(band) + " body=" + Fmt(body));
                        Assert(Above(body, band) && band.yMax <= footer.y + Tol,
                            context + ": the help band must sit between the body row and the footer band; body="
                            + Fmt(body) + " band=" + Fmt(band) + " footer=" + Fmt(footer));
                    }

                    var window = new Rect(0f, 0f, box.x, box.y);
                    var placed = new List<Placed>();
                    foreach (UiElementSpec root in manifest.Roots)
                    {
                        Collect(root, window, "<page>", new List<string>(), false, snapshot, context,
                            placed, problems);
                    }

                    CheckOverlaps(placed, problems, context);

                    Console.WriteLine("[minimum-frame] " + context
                        + " body=" + bodyArranged + " nav=" + navArranged + " band=" + bandArranged
                        + " contentViewport=" + centreArranged
                        + " bodyWidth=" + body.width.ToString("0.##", CultureInfo.InvariantCulture)
                        + " placed=" + placed.Count
                        + " box=" + box.x.ToString("0", CultureInfo.InvariantCulture)
                        + "x" + box.y.ToString("0", CultureInfo.InvariantCulture));

                    Assert(footer.yMax <= box.y + Tol,
                        context + ": the footer must stay inside the page in BOTH help states; footer="
                        + Fmt(footer));

                    // BH1's own claim, asserted across the pair of arranges instead of from a number: help takes
                    // HEIGHT, never width. The closed arrange is the reference for the open one.
                    if (open)
                    {
                        Assert(!float.IsNaN(bodyWidthClosed) && Math.Abs(body.width - bodyWidthClosed) <= Tol,
                            context + ": opening help must leave the body row's width untouched; closed="
                            + bodyWidthClosed.ToString("0.##", CultureInfo.InvariantCulture) + " open="
                            + body.width.ToString("0.##", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        bodyWidthClosed = body.width;
                    }
                }
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            Verse.UI.screenWidth = savedWidth;
            Verse.UI.screenHeight = savedHeight;
        }

        Assert(problems.Count == 0, Report(problems));
    }

    /// <summary>
    /// The UNREACHABLE sub-minimum probe, re-cut by BH1 to assert the OPPOSITE of what it used to: on the box
    /// a screen BELOW the game's minimum resolution produces, the settings body is STILL arranged and no help
    /// state eats it.
    ///
    /// <para>
    /// PROBE, NOT PRODUCT SHAPE (B3.5): 800x600 is a screen below the game's own minimum logical resolution
    /// (1024x768; <c>RimWorld.ResolutionUtility</c> in the pinned Krafs.Rimworld.Ref), so no reachable screen
    /// selects this box and it decides no product behaviour. It is kept because it is the harshest frame the
    /// walk can be handed that the size policy still produces: the window answers its own floor, so the page
    /// box is as short as the policy ever makes it and the band's reservation has to come out of the smallest
    /// body the policy will draw. It is not real-screen evidence about anything else.
    /// </para>
    ///
    /// <para>
    /// What was dropped here, and why it is unreachable rather than merely untested: this step used to prove
    /// the band REPLACED the body on this box, because the old presentation asked "which of the two does this
    /// page box afford" and a sub-minimum box answered "the band". BH1 removed the branch that made the choice
    /// - the panel is ONE declared Scroll with a declared height reservation, gated by the player's own
    /// <c>help-open</c> intent, and no code asks the box for permission any more - so there is no predicate
    /// left to invert and no box, however short, that can hide the body row. The clause now asserts the
    /// sharing invariant on the sub-minimum box instead: body row, nav column and centre scroll arranged in
    /// BOTH help states, band arranged exactly while help is open, nothing pushed off the page, no rect
    /// degenerate. The probe keeps the overlap walk's original purpose too: the pre-fix defect it was written
    /// to report (a reserved band beside a body whose nav content overflowed its slot) is exactly what a
    /// reintroduced replace-the-body branch would produce here first.
    /// </para>
    /// </summary>
    private static void TheSubMinimumProbeStillSharesThePage()
    {
        UiLayoutManifest manifest = LoadManifest();
        var problems = new List<string>();
        int savedWidth = Verse.UI.screenWidth;
        int savedHeight = Verse.UI.screenHeight;
        try
        {
            // The synthetic probe: a screen below the game's minimum. The policy still answers its window floor
            // for it, and since BH1 that floor is help-state independent, so ONE derived box is arranged twice -
            // once with help closed, once with it open. The numbers stay the policy's, never this file's.
            Verse.UI.screenWidth = 800;
            Verse.UI.screenHeight = 600;
            Vector2 box = PageBoxFor(800f, 600f);

            foreach (string language in Languages)
            {
                Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
                foreach (bool open in new[] { false, true })
                {
                    var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                    using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                    host.Bindings.Invoke("set-tab", "Overview");
                    host.Bindings.Set(HelpOpenKey, open);
                    UiLayoutSnapshot snapshot = host.MeasureAndArrange(box);
                    host.DrawChecked(new Rect(0f, 0f, box.x, box.y));

                    string context = "sub-minimum probe " + (open ? "open" : "closed") + "/" + language
                        + " (synthetic UNREACHABLE screen 800x600)";
                    CheckRects(snapshot, context, problems);

                    bool bodyArranged = snapshot.RectById.ContainsKey("body-row");
                    bool navArranged = snapshot.RectById.ContainsKey("nav-column");
                    bool centreArranged = snapshot.Viewports.ContainsKey("content-scroll");
                    bool bandArranged = snapshot.Viewports.ContainsKey(HelpBandId);

                    // The SHAPE PREMISE, now the opposite of the retired branch: on the harshest box the policy
                    // hands, a help state still may not eat the settings. Body row, nav column and centre scroll
                    // arranged in BOTH states; the band joins them exactly while help is open.
                    Assert(bodyArranged && navArranged && centreArranged && bandArranged == open,
                        context + ": the sub-minimum probe must still SHARE the page (body row, nav column and"
                        + " centre scroll arranged in both help states; band exactly while help is open); body="
                        + bodyArranged + " nav=" + navArranged + " centre=" + centreArranged
                        + " band=" + bandArranged);

                    Assert(snapshot.RectById.ContainsKey("header-band")
                            && snapshot.RectById.ContainsKey("footer-band"),
                        context + ": the probe box must still arrange both fixed bands");
                    Rect body = snapshot.RectById["body-row"];
                    Rect header = snapshot.RectById["header-band"];
                    Rect footerBand = snapshot.RectById["footer-band"];
                    if (bandArranged)
                    {
                        Rect band = snapshot.Viewports[HelpBandId];
                        Assert(Math.Abs(band.width - body.width) <= Tol && Above(body, band)
                                && band.yMax <= footerBand.y + Tol,
                            context + ": the band must stack under the body row at the body's width and stop"
                            + " above the footer band; body=" + Fmt(body) + " band=" + Fmt(band)
                            + " footer=" + Fmt(footerBand));
                        CheckInsidePage(context + " '" + HelpBandId + "'", band, box, problems);
                    }

                    // Nothing is pushed off the page. On a sub-minimum box this is the claim that separates a
                    // reservation (the body yields) from an overflow (a band lands below the page): every band of
                    // the frame keeps its place inside the box, in either help state.
                    CheckInsidePage(context + " 'header-band'", header, box, problems);
                    CheckInsidePage(context + " 'body-row'", body, box, problems);
                    CheckInsidePage(context + " 'footer-band'", footerBand, box, problems);

                    var window = new Rect(0f, 0f, box.x, box.y);
                    var placed = new List<Placed>();
                    foreach (UiElementSpec root in manifest.Roots)
                    {
                        Collect(root, window, "<page>", new List<string>(), false, snapshot, context,
                            placed, problems);
                    }

                    CheckOverlaps(placed, problems, context);

                    Console.WriteLine("[sub-minimum-probe] " + context
                        + " body=" + bodyArranged + " nav=" + navArranged + " band=" + bandArranged
                        + " contentViewport=" + centreArranged
                        + " bodyRect=" + Fmt(body)
                        + " placed=" + placed.Count
                        + " box=" + box.x.ToString("0", CultureInfo.InvariantCulture)
                        + "x" + box.y.ToString("0", CultureInfo.InvariantCulture));

                    Assert(footerBand.yMax <= box.y + Tol,
                        context + ": the footer must stay inside the page in BOTH help states; footer="
                        + Fmt(footerBand));
                }
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            Verse.UI.screenWidth = savedWidth;
            Verse.UI.screenHeight = savedHeight;
        }

        Assert(problems.Count == 0, Report(problems));
    }



    /// <summary>Every rect the frame published is finite and non-negative; every scroll box is real.</summary>
    private static void CheckRects(UiLayoutSnapshot snapshot, string context, List<string> problems)
    {
        foreach (KeyValuePair<string, Rect> pair in snapshot.RectById)
        {
            Rect rect = pair.Value;
            if (!Finite(rect) || rect.width < 0f || rect.height < 0f)
            {
                problems.Add(context + ": element '" + pair.Key + "' has a degenerate rect " + Fmt(rect));
            }
        }

        foreach (KeyValuePair<string, Rect> pair in snapshot.Viewports)
        {
            Rect rect = pair.Value;
            if (!Finite(rect) || rect.width <= 0f || rect.height <= 0f)
            {
                problems.Add(context + ": scroll viewport '" + pair.Key + "' must be a real box, got " + Fmt(rect));
            }
        }

        foreach (KeyValuePair<string, Rect> pair in snapshot.ScrollContents)
        {
            Rect rect = pair.Value;
            if (!Finite(rect) || rect.width <= 0f || rect.height < 0f)
            {
                problems.Add(context + ": scroll content '" + pair.Key + "' must be a real box, got " + Fmt(rect));
            }
        }
    }

    /// <summary>
    /// Walks the DECLARED tree (the manifest) and pairs each element with the rect the arrange published
    /// for it. An element with no published rect is not arranged, which is the visibility answer itself, so
    /// its whole subtree is skipped - that is how a Tab-gated or VisibleKey-gated section stays out of the
    /// assertions without this lane restating any gate.
    /// <para>
    /// The walk collects as it checks, because the overlap rule that matters is NOT "siblings must not
    /// overlap": the engine distributes a flow container's slots so that siblings cannot overlap by
    /// construction, which would make a sibling-only lane vacuous. What can and does go wrong is a child
    /// OVERFLOWING its own slot and landing on a cousin - measured here as the pre-S3 defect where the nav
    /// composite (271px of natural content) was handed an equal height share (212px) in the stacked frame
    /// and painted into the column below it. So the collected rects are compared pairwise across the whole
    /// coordinate space, excluding ancestors (a parent contains its children by definition).
    /// </para>
    /// </summary>
    private static void Collect(
        UiElementSpec spec,
        Rect reference,
        string space,
        List<string> ancestors,
        bool underOverlay,
        UiLayoutSnapshot snapshot,
        string context,
        List<Placed> placed,
        List<string> problems)
    {
        if (spec.Id.Length == 0) return;
        if (!snapshot.RectById.TryGetValue(spec.Id, out Rect rect)) return;

        string where = context + " '" + spec.Id + "'(" + spec.Kind + ")";

        if (!Finite(rect) || rect.width < 0f || rect.height < 0f)
        {
            problems.Add(where + ": degenerate rect " + Fmt(rect));
            return;
        }

        if (rect.x < reference.x - Tol || rect.y < reference.y - Tol
            || rect.xMax > reference.xMax + Tol || rect.yMax > reference.yMax + Tol)
        {
            problems.Add(where + ": leaves its parent's rect; element=" + Fmt(rect) + " parent=" + Fmt(reference));
        }

        bool overlayHere = underOverlay
            || string.Equals(spec.Kind, "Overlay", StringComparison.OrdinalIgnoreCase);
        placed.Add(new Placed
        {
            Id = spec.Id,
            Kind = spec.Kind,
            Rect = rect,
            Space = space,
            Ancestors = new List<string>(ancestors),
            UnderOverlay = overlayHere,
        });

        // A Scroll's children are arranged from the scroll's CONTENT origin, and the snapshot reports
        // that content box at (0,0) on purpose (it is the box handed to BeginScrollView). The children's
        // own rects are in the containing space, so the reference has to be the content box MOVED onto the
        // viewport's origin - comparing against the raw (0,0,..) box is the classic false red here.
        Rect childSpace = rect;
        string childSpaceKey = space;
        if (snapshot.ScrollContents.TryGetValue(spec.Id, out Rect content))
        {
            childSpace = new Rect(rect.x, rect.y, content.width, content.height);
            childSpaceKey = spec.Id;
        }

        var nextAncestors = new List<string>(ancestors) { spec.Id };
        foreach (UiElementSpec child in spec.Children)
        {
            Collect(child, childSpace, childSpaceKey, nextAncestors, overlayHere, snapshot, where, placed, problems);
        }
    }

    /// <summary>
    /// Pairwise overlap across one coordinate space. An <c>Overlay</c> subtree is exempt: placing children
    /// by reference point is exactly what an Overlay is for, so overlap there is the declaration. Ancestors
    /// are exempt for the same reason they are not siblings.
    /// </summary>
    private static void CheckOverlaps(List<Placed> placed, List<string> problems, string context)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            for (int j = i + 1; j < placed.Count; j++)
            {
                Placed a = placed[i];
                Placed b = placed[j];
                if (a.UnderOverlay || b.UnderOverlay) continue;
                if (!string.Equals(a.Space, b.Space, StringComparison.Ordinal)) continue;
                if (a.Ancestors.Contains(b.Id) || b.Ancestors.Contains(a.Id)) continue;
                if (!Overlaps(a.Rect, b.Rect)) continue;
                problems.Add(context + ": '" + a.Id + "'(" + a.Kind + ") " + Fmt(a.Rect)
                    + " overlaps '" + b.Id + "'(" + b.Kind + ") " + Fmt(b.Rect));
            }
        }
    }

    private sealed class Placed
    {
        public string Id = "";
        public string Kind = "";
        public Rect Rect;
        public string Space = "";
        public List<string> Ancestors = new List<string>();
        public bool UnderOverlay;
    }

    // ---------------------------------------------------------------------------------------------
    // 2. The frame's shape: the body row is two columns side by side or a full-width stack - never a
    //    mixture - and the help band is stacked BELOW the row at the page's inner width.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The shape claim BH1 leaves behind. This step used to follow the help presentation THROUGH the body row:
    /// in a wide box the panel was a column BESIDE the centre, in a narrow one it fell back to a band that took
    /// the body's place, and the step's job was to tell the two apart at every probe. The panel is not in the
    /// body row any more, so the only regime left to assert is the row's own (<c>Narrow="Column"</c> under its
    /// Breakpoint) and the help claim became one invariant that holds in both regimes: the band exists exactly while
    /// <c>help-open</c> is set, and it is stacked under the row at the row's width. Nothing in this step may
    /// branch on the help state - that is the point of it.
    /// </summary>
    private static void TheFrameKeepsItsShape()
    {
        UiLayoutManifest manifest = LoadManifest();
        Dictionary<string, UiElementSpec> byId = Index(manifest);
        var problems = new List<string>();

        // The band's place in the DECLARED tree is what makes the geometry below mean "stacked under the
        // settings" instead of "another column of them": a sibling of the body row, gated by the player's intent
        // and by nothing else. A manifest edit that moved it back inside the row, or re-derived its visibility
        // from the box instead of from the key, reddens here before the probe walk starts.
        Assert(IsDescendant(manifest, "page-root", HelpBandId, direct: true),
            "the help band must be a DIRECT child of page-root - a stacked sibling of the body row, not a"
            + " column inside it");
        Assert(!IsDescendant(manifest, "body-row", HelpBandId, direct: false),
            "the help band must not live inside the body row: sharing the row is exactly what made the body's"
            + " width depend on the help state");
        Assert(byId.ContainsKey(HelpBandId), "the manifest must declare the help band '" + HelpBandId + "'");
        UiElementSpec bandSpec = byId[HelpBandId];
        Assert(bandSpec.TryGetAttribute("VisibleKey", out string bandKey)
                && string.Equals(bandKey.Trim(), HelpOpenKey, StringComparison.Ordinal),
            "the help band must be gated by the raw player intent '" + HelpOpenKey + "' and nothing else");
        Assert(!bandSpec.TryGetAttribute("Tab", out _),
            "the help band must carry no Tab attribute: help visibility is its own state, not workspace state");

        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            foreach (float width in Widths)
            {
                foreach (bool open in new[] { false, true })
                {
                    var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                    using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                    host.Bindings.Set(HelpOpenKey, open);
                    UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, Height));

                    string context = "width=" + width.ToString("0", CultureInfo.InvariantCulture)
                        + " help=" + (open ? "open" : "closed");
                    Assert(snapshot.RectById.ContainsKey("body-row"), context + ": the body row must be arranged");
                    Assert(snapshot.RectById.ContainsKey("nav-column"), context + ": the nav column must be arranged");
                    Assert(snapshot.Viewports.ContainsKey("content-scroll"), context + ": the centre column must be arranged");

                    Rect body = snapshot.RectById["body-row"];
                    Rect nav = snapshot.RectById["nav-column"];
                    Rect centre = snapshot.Viewports["content-scroll"];
                    bool hasBand = snapshot.Viewports.ContainsKey(HelpBandId);

                    Assert(hasBand == open,
                        context + ": the bottom help band must be arranged exactly while help is open");

                    if (Beside(nav, centre))
                    {
                        // The side-by-side regime: the nav keeps its declared width and the centre takes the
                        // rest. This is the only regime claim left, because there is no help column to place.
                        float declaredNav = DeclaredWidth(byId, "nav-column");
                        if (Math.Abs(nav.width - declaredNav) > Tol)
                        {
                            problems.Add(context + ": in the side-by-side regime the nav column must keep its"
                                + " declared width " + declaredNav + ", got "
                                + nav.width.ToString("0.##", CultureInfo.InvariantCulture));
                        }
                    }
                    else if (Above(nav, centre))
                    {
                        // Narrow="Column": the row's OWN two columns stack, so both share x and width.
                        CheckStacked("nav-column", nav, centre, context, problems);
                    }
                    else
                    {
                        problems.Add(context + ": the nav column is neither beside nor above the centre column;"
                            + " nav=" + Fmt(nav) + " centre=" + Fmt(centre));
                    }

                    if (hasBand)
                    {
                        // One invariant for both regimes and both help states: the band is stacked under the
                        // body row, spans the row's width (the page's inner width, therefore), and never lands on
                        // a column of the row or on the footer band.
                        Rect band = snapshot.Viewports[HelpBandId];
                        CheckStacked(HelpBandId, band, body, context, problems);
                        if (!Above(body, band))
                        {
                            problems.Add(context + ": the help band must sit BELOW the body row; body="
                                + Fmt(body) + " band=" + Fmt(band));
                        }

                        if (snapshot.RectById.ContainsKey("footer-band"))
                        {
                            Rect footerBand = snapshot.RectById["footer-band"];
                            if (band.yMax > footerBand.y + Tol)
                            {
                                problems.Add(context + ": the help band may not overlap or pass the footer band;"
                                    + " band=" + Fmt(band) + " footer=" + Fmt(footerBand));
                            }
                        }

                        if (Overlaps(band, nav) || Overlaps(band, centre))
                        {
                            problems.Add(context + ": the help band must not overlap a body column; band="
                                + Fmt(band) + " nav=" + Fmt(nav) + " centre=" + Fmt(centre));
                        }
                    }
                }
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }

        Assert(problems.Count == 0, Report(problems));

        // The two endpoints stay product invariants, and after BH1 they are invariants about the BODY ROW's own
        // breakpoint: the widest probe keeps nav and centre side by side, the narrowest stacks them. The retired
        // argument - a wide three-column frame against a stacked one whose help fell back to a body-replacing
        // band - described a presentation choice that no longer exists, so each endpoint is now walked in BOTH
        // help states: the regime must not move when help opens, and the band must sit under the row either way.
        AssertShapeAt(1280f, expectSideBySide: true);
        AssertShapeAt(320f, expectSideBySide: false);
    }

    /// <summary>
    /// One endpoint of the body row's regime, asserted in BOTH help states on the same probe box: the row is
    /// side by side or stacked, the band exists exactly while help is open and shares the row's width from
    /// below. There is no wide-versus-stacked help branch left to name, so the argument is the row's regime and
    /// the help state is a loop, not a choice.
    /// </summary>
    private static void AssertShapeAt(float width, bool expectSideBySide)
    {
        foreach (bool open in new[] { false, true })
        {
            var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
            using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
            host.Bindings.Set(HelpOpenKey, open);
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, Height));

            string context = width.ToString("0", CultureInfo.InvariantCulture) + "px help="
                + (open ? "open" : "closed");
            Assert(snapshot.RectById.ContainsKey("body-row") && snapshot.RectById.ContainsKey("nav-column")
                    && snapshot.Viewports.ContainsKey("content-scroll"),
                context + ": the shape assertion needs the body row, its nav column and its centre scroll arranged");

            Rect body = snapshot.RectById["body-row"];
            Rect nav = snapshot.RectById["nav-column"];
            Rect centre = snapshot.Viewports["content-scroll"];

            bool sideBySide = Beside(nav, centre);
            bool stacked = Above(nav, centre);
            Assert(expectSideBySide ? sideBySide : stacked,
                context + " must keep the body row " + (expectSideBySide ? "side by side" : "stacked")
                + ": nav=" + Fmt(nav) + " centre=" + Fmt(centre));

            bool hasBand = snapshot.Viewports.ContainsKey(HelpBandId);
            Assert(hasBand == open, context + ": the help band must be arranged exactly while help is open");
            if (hasBand)
            {
                Rect band = snapshot.Viewports[HelpBandId];
                Assert(Math.Abs(band.width - body.width) <= Tol && Above(body, band),
                    context + ": the help band must be stacked under the body row at the body's width; body="
                    + Fmt(body) + " band=" + Fmt(band));
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 3. The two fixed bands: structure first, then the geometry each of them exists for.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The header and footer bands are what make "the title does not scroll away" and "the help switch does not
    /// scroll away" true, so both are asserted STRUCTURALLY, not by comparing snapshots across a scroll write: a
    /// scroll position never changes a layout snapshot (positions are arrangement, not scroll offset), so such a
    /// comparison would pass whatever the manifest said. What decides the property is descendance - an element
    /// inside <c>content-scroll</c> is drawn under the scroll and moves; one in a band that is a direct child of
    /// page-root cannot. The predicate is made two-sided by asserting the positive control in the same run: the
    /// banner IS inside the scroll, which is exactly why it is allowed to scroll away.
    ///
    /// <para>
    /// BH1 moved the help switch OUT of the header band and INTO the footer band, because the space it opens is
    /// now the panel directly ABOVE that band rather than a column beside the body. So the header's claim shrank
    /// to what stayed true of it - fixed, outside every scroll, carrying the title - and the switch's claims (the
    /// page's ONLY one, on its band's right edge, centred on that band's cross axis, a real hit target) are
    /// footer-band claims now. Both bands are Rows and neither pins a height: a pinned band overrode the
    /// wrap-aware measure and cut a wrapped footer, which is the shape the retired Overlay carried.
    /// </para>
    /// </summary>
    private static void TheHeaderAndFooterBandsAreFixed()
    {
        UiLayoutManifest manifest = LoadManifest();
        Dictionary<string, UiElementSpec> byId = Index(manifest);

        int switches = 0;
        foreach (UiElementSpec spec in byId.Values)
        {
            if (spec.TryGetAttribute("ActionBind", out string action)
                && string.Equals(action.Trim(), ToggleCommand, StringComparison.Ordinal))
            {
                switches++;
            }
        }

        Assert(switches == 1,
            "exactly one manifest element may bind the help command; found " + switches
            + " (the page-title widget carried a second, hand-drawn switch until S3-2a, and the retired header"
            + " position carried the only remaining one until BH1 moved it to the footer)");

        // The header band: fixed, outside every scroll, and it carries the title.
        Assert(byId.ContainsKey("header-band"), "the manifest must declare the header band");
        UiElementSpec headerSpec = byId["header-band"];
        Assert(string.Equals(headerSpec.Kind, "Row", StringComparison.Ordinal),
            "the manifest must declare the header band as a Row: the three frame bands are one container kind,"
            + " and the header's own geometry was measured as a Row");
        Assert(IsDescendant(manifest, "page-root", "header-band", direct: true),
            "header-band must be a DIRECT child of page-root, i.e. in the flow beside the body row, the help"
            + " band and the footer, never inside a scroll");
        Assert(IsDescendant(manifest, "header-band", "page-title", direct: true),
            "the header band must hold the page title: since BH1 moved the switch to the footer, that is the"
            + " band's whole job");
        Assert(!IsDescendant(manifest, "header-band", HelpToggleId, direct: false),
            "the header band must NOT hold the help switch any more: the switch belongs beside the panel it"
            + " opens, and a header that carried it was the pre-BH1 shape");
        Assert(!IsDescendant(manifest, "content-scroll", HelpToggleId, direct: false)
            && !IsDescendant(manifest, "content-scroll", "page-title", direct: false),
            "neither the switch nor the title may live inside the scrolling centre column: that is the whole"
            + " point of the two bands, and it is the assertion that says neither of them scrolls away");
        Assert(IsDescendant(manifest, "content-scroll", "banner", direct: false),
            "positive control: the banner IS inside content-scroll, so the descendance predicate above"
            + " discriminates instead of answering false for everything");

        // The footer band: fixed like the header, and the page's action strip since BH1. Its KIND is the claim
        // that makes the switch's placement fall out of the container instead of out of an attribute.
        Assert(byId.ContainsKey("footer-band"), "the manifest must declare the footer band");
        UiElementSpec footerSpec = byId["footer-band"];
        Assert(string.Equals(footerSpec.Kind, "Row", StringComparison.Ordinal),
            "the footer band must be a Row: it hands us/footer the leftover after the fixed-width switch, which"
            + " is what puts the switch on the band's right edge with no reserved width spelled in code (the"
            + " pinned Overlay it replaced overrode the wrap-aware measure the footer needs)");
        Assert(IsDescendant(manifest, "page-root", "footer-band", direct: true),
            "footer-band must be a direct child of page-root, never inside a scroll");
        Assert(IsDescendant(manifest, "footer-band", "footer", direct: true)
            && string.Equals(byId["footer"].Kind, "us/footer", StringComparison.Ordinal),
            "the footer band must hold the us/footer widget directly");
        Assert(!IsDescendant(manifest, "content-scroll", "footer", direct: false),
            "the footer must not live inside the scrolling centre column");

        // The band is the switch's ONLY home on this page: two children, the unsized text first and the
        // fixed-width toggle LAST. Being last in a Row is what right-aligns it, because AlignX is refused (below).
        var footerIds = new List<string>();
        int bandToggles = 0;
        foreach (UiElementSpec child in footerSpec.Children)
        {
            footerIds.Add(child.Id);
            if (string.Equals(child.Id, HelpToggleId, StringComparison.Ordinal)) bandToggles++;
        }

        Assert(footerSpec.Children.Count == 2 && bandToggles == 1
                && string.Equals(footerIds[0], "footer", StringComparison.Ordinal)
                && string.Equals(footerIds[1], HelpToggleId, StringComparison.Ordinal),
            "the footer band must hold us/footer plus exactly ONE help toggle, with the toggle LAST; children="
            + string.Join(", ", footerIds));
        Assert(byId.ContainsKey(HelpToggleId), "the manifest must declare the footer's '" + HelpToggleId + "'");
        UiElementSpec toggleSpec = byId[HelpToggleId];
        Assert(toggleSpec.TryGetAttribute("ActionBind", out string toggleBind)
                && string.Equals(toggleBind.Trim(), ToggleCommand, StringComparison.Ordinal),
            "the footer's toggle must be the declared element that carries the '" + ToggleCommand + "' command:"
            + " the player's single switch is one typed binding, not a hand-drawn hit band");
        Assert(toggleSpec.TryGetAttribute("AlignY", out string alignY)
                && string.Equals(alignY.Trim(), "Middle", StringComparison.Ordinal),
            "the switch is centred on the band's cross axis by AlignY: the footer Row resolves its own height as"
            + " the max of its children, so centring is the only cross-axis placement it can declare");
        Assert(!toggleSpec.TryGetAttribute("AlignX", out _),
            "the switch must carry NO AlignX: being the band's last fixed-width child is the only"
            + " right-alignment a Row can be given, which is what the refusal step below keeps refused");

        UiElementSpec pageRoot = manifest.Roots[0];
        UiElementSpec lastChild = pageRoot.Children[pageRoot.Children.Count - 1];
        Assert(string.Equals(lastChild.Id, "footer-band", StringComparison.Ordinal),
            "the footer band must be the LAST flow child of page-root, so neither the filling body row nor the"
            + " help band can push it off screen; found '" + lastChild.Id + "'");

        // And the panel the switch opens has to be the space directly above it: declared order in the flow is
        // what makes that true before any geometry is measured.
        int bandIndex = -1;
        int footerIndex = -1;
        for (int i = 0; i < pageRoot.Children.Count; i++)
        {
            if (string.Equals(pageRoot.Children[i].Id, HelpBandId, StringComparison.Ordinal)) bandIndex = i;
            if (string.Equals(pageRoot.Children[i].Id, "footer-band", StringComparison.Ordinal)) footerIndex = i;
        }

        Assert(bandIndex >= 0 && footerIndex == bandIndex + 1,
            "the help band must be declared immediately ABOVE the footer band in page-root's flow, so the switch"
            + " opens the space right above itself; bandIndex=" + bandIndex + " footerIndex=" + footerIndex);

        var problems = new List<string>();
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            foreach (float width in Widths)
            {
                Rect? headerClosed = null;
                foreach (bool open in new[] { false, true })
                {
                    var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                    using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                    host.Bindings.Invoke("set-tab", "Tuning");
                    host.Bindings.Set(HelpOpenKey, open);
                    UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, Height));
                    host.DrawChecked(new Rect(0f, 0f, width, Height));

                    string context = "bands at " + width.ToString("0", CultureInfo.InvariantCulture)
                        + "px help=" + (open ? "open" : "closed");
                    Assert(snapshot.RectById.ContainsKey("header-band"),
                        context + ": the header band must be arranged");
                    Assert(snapshot.RectById.ContainsKey("page-title"), context + ": the page title must be arranged");
                    Assert(snapshot.RectById.ContainsKey("footer-band"),
                        context + ": the footer band must be arranged");
                    Assert(snapshot.RectById.ContainsKey(HelpToggleId), context + ": the help toggle must be arranged");
                    Assert(snapshot.RectById.ContainsKey("body-row"), context + ": the body row must be arranged");
                    Assert(snapshot.RectById.ContainsKey("footer"), context + ": the frame must carry its footer");

                    Rect header = snapshot.RectById["header-band"];
                    Rect title = snapshot.RectById["page-title"];
                    Rect body = snapshot.RectById["body-row"];
                    Rect footer = snapshot.RectById["footer-band"];
                    Rect toggle = snapshot.RectById[HelpToggleId];

                    // The header is FIXED: the title sits inside it, the band sits above the body row, and a help
                    // state never moves or resizes it - the reservation is paid by the body, not by the header.
                    Assert(title.x >= header.x - Tol && title.xMax <= header.xMax + Tol
                            && title.y >= header.y - Tol && title.yMax <= header.yMax + Tol,
                        context + ": the page title must sit inside the fixed header band; title=" + Fmt(title)
                        + " band=" + Fmt(header));
                    Assert(Above(header, body),
                        context + ": the fixed header band must sit above the body row; header=" + Fmt(header)
                        + " body=" + Fmt(body));
                    if (!open)
                    {
                        headerClosed = header;
                    }
                    else if (headerClosed.HasValue)
                    {
                        Rect closedHeader = headerClosed.Value;
                        Assert(Math.Abs(header.x - closedHeader.x) <= Tol
                                && Math.Abs(header.y - closedHeader.y) <= Tol
                                && Math.Abs(header.width - closedHeader.width) <= Tol
                                && Math.Abs(header.height - closedHeader.height) <= Tol,
                            context + ": opening help must not move or resize the fixed header band; closed="
                            + Fmt(closedHeader) + " open=" + Fmt(header));
                    }

                    // The switch's geometry, now a footer-band claim: right edge of the band, centred on the
                    // band's cross axis by its AlignY, and a hit target rather than a painted label.
                    if (Math.Abs(toggle.xMax - footer.xMax) > 1.5f)
                    {
                        problems.Add(context + ": the help toggle must sit on the footer band's right edge;"
                            + " toggle=" + Fmt(toggle) + " band=" + Fmt(footer));
                    }

                    float bandMiddle = footer.y + footer.height / 2f;
                    float toggleMiddle = toggle.y + toggle.height / 2f;
                    if (Math.Abs(bandMiddle - toggleMiddle) > 1.5f)
                    {
                        problems.Add(context + ": the toggle must be vertically centred in the footer band, the"
                            + " geometry its AlignY declares; toggle=" + Fmt(toggle) + " band=" + Fmt(footer));
                    }

                    if (toggle.width < 1f || toggle.height < 1f)
                    {
                        problems.Add(context + ": the toggle must be a real hit target; got " + Fmt(toggle));
                    }

                    CheckInsidePage(context + " 'footer-band'", footer, new Vector2(width, Height), problems);
                }
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }

        Assert(problems.Count == 0, Report(problems));
    }

    /// <summary>
    /// Negative evidence for the bands' shape: <c>AlignX</c> on a Row's child is refused AT CREATION, which is
    /// the mechanical reason the footer's switch is right-aligned by being the band's LAST fixed-width child
    /// instead of by a placement attribute. Pinning the refusal here means nobody "fixes" the switch by adding
    /// AlignX back and finds out in the game - and it is why both bands can be Rows.
    /// </summary>
    private static void PlacementIsRefusedWhereTheContainerDoesNotOwnIt()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        UiWidgetRegistry.InitializeCore();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
            + "<Row Id=\"band\"><Widget Id=\"t\" Kind=\"us/page-title\" AlignX=\"Right\" /></Row>"
            + "</UiPage>");

        bool refused = false;
        try
        {
            using var host = new UiHost(
                "coahuilite.universalsqueaker",
                manifest,
                new UiBindings(),
                UsTheme.Surface(),
                new Program.StubMetrics(),
                new LaneTranslation());
        }
        catch (UiContractException)
        {
            refused = true;
        }

        Assert(refused,
            "AlignX on a Row's child must be refused at creation: a Row's main axis already has an owner,"
            + " so a second one is the ambiguity the placement vocabulary exists to refuse - and it is why"
            + " the footer band's switch carries AlignY and no AlignX");
    }

    /// <summary>
    /// The ruling behind S3-5, made checkable: the page's rhythm is DECLARED on the page containers, and the
    /// style document's density tokens stay the library baseline. A container that omits `Padding` silently
    /// inherits that baseline (CP-0: an absent Padding falls back to `theme.Geometry.Padding`), which is
    /// exactly the coupling this ruling removes - so a missing attribute is a failure here, not a default.
    /// Explicit zero is the escape hatch and is used deliberately (`Padding="0"`), while a container with
    /// two or more children must also state its `Gap`, because the space BETWEEN page regions is the thing
    /// the rhythm is.
    /// </summary>
    private static void PageRhythmIsExplicit()
    {
        UiLayoutManifest manifest = LoadManifest();
        var problems = new List<string>();
        foreach (UiElementSpec root in manifest.Roots) CheckRhythm(root, problems);
        Assert(problems.Count == 0, Report(problems));
    }

    private static void CheckRhythm(UiElementSpec spec, List<string> problems)
    {
        if (IsContainerKind(spec.Kind))
        {
            if (!spec.TryGetAttribute("Padding", out string padding) || padding.Trim().Length == 0)
            {
                problems.Add("container '" + spec.Id + "'(" + spec.Kind + ") declares no Padding, so it"
                    + " inherits the density token instead of stating its own rhythm");
            }

            if (spec.Children.Count >= 2
                && (!spec.TryGetAttribute("Gap", out string gap) || gap.Trim().Length == 0))
            {
                problems.Add("container '" + spec.Id + "'(" + spec.Kind + ") has " + spec.Children.Count
                    + " children but declares no Gap, so the space BETWEEN page regions comes from the token");
            }
        }

        foreach (UiElementSpec child in spec.Children) CheckRhythm(child, problems);
    }

    /// <summary>The engine's container kinds, restated here because UiHost.IsContainerKind is private.</summary>
    private static bool IsContainerKind(string kind)
    {
        switch (kind)
        {
            case "Column":
            case "Row":
            case "Wrap":
            case "Overlay":
            case "Section":
            case "Surface":
            case "Scroll":
            case "Clip":
                return true;
            default:
                return false;
        }
    }

    /// <summary>Key-echo translation: the lane only needs the manifest to be reachable, not readable.</summary>
    private sealed class LaneTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }

    /// <summary>
    /// True when <paramref name="ancestorId"/> contains <paramref name="descendantId"/> in the DECLARED
    /// tree (<paramref name="direct"/> narrows it to a direct child).
    /// </summary>
    private static bool IsDescendant(UiLayoutManifest manifest, string ancestorId, string descendantId, bool direct)
    {
        foreach (UiElementSpec root in manifest.Roots)
        {
            UiElementSpec? node = Find(root, ancestorId);
            if (node == null) continue;
            if (direct)
            {
                foreach (UiElementSpec child in node.Children)
                {
                    if (string.Equals(child.Id, descendantId, StringComparison.Ordinal)) return true;
                }

                return false;
            }

            return Contains(node, descendantId);
        }

        return false;
    }

    private static UiElementSpec? Find(UiElementSpec spec, string id)
    {
        if (string.Equals(spec.Id, id, StringComparison.Ordinal)) return spec;
        foreach (UiElementSpec child in spec.Children)
        {
            UiElementSpec? found = Find(child, id);
            if (found != null) return found;
        }

        return null;
    }

    private static bool Contains(UiElementSpec spec, string id)
    {
        foreach (UiElementSpec child in spec.Children)
        {
            if (string.Equals(child.Id, id, StringComparison.Ordinal)) return true;
            if (Contains(child, id)) return true;
        }

        return false;
    }

    /// <summary>
    /// A stacked element must share the x and width of the container it stacks with: the centre column for the
    /// body row's own two columns, and the body row itself for the help band. The reference is a parameter
    /// because BH1 moved the band OUT of the row - a lane that still compared it against the centre column would
    /// be asserting the retired side-by-side presentation from below the row.
    /// </summary>
    private static void CheckStacked(string id, Rect rect, Rect reference, string context, List<string> problems)
    {
        if (Math.Abs(rect.x - reference.x) > Tol || Math.Abs(rect.width - reference.width) > Tol)
        {
            problems.Add(context + ": stacked element '" + id + "' must share the reference's x and width; got "
                + Fmt(rect) + " reference=" + Fmt(reference));
        }
    }

    /// <summary>
    /// The frame's own containment claim, spelled per band so a failure names the band: a reservation shrinks the
    /// body, an overflow drops a band below the page, and only the second one is a product defect. The step-1 walk
    /// checks every element against its parent; this checks the named bands against the page box, which is the
    /// claim a sub-minimum screen exists to test.
    /// </summary>
    private static void CheckInsidePage(string where, Rect rect, Vector2 box, List<string> problems)
    {
        if (rect.x < -Tol || rect.y < -Tol || rect.xMax > box.x + Tol || rect.yMax > box.y + Tol)
        {
            problems.Add(where + ": pushed off the page; element=" + Fmt(rect) + " page="
                + box.x.ToString("0", CultureInfo.InvariantCulture) + "x"
                + box.y.ToString("0", CultureInfo.InvariantCulture));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The page BOX the shell hands its host for a screen: <c>UiWindowHost.ContentRect</c>, i.e. the window the
    /// size policy produces minus the horizontal chrome and minus the title bar plus the bottom inset
    /// (<see cref="WindowChromeLayout.WindowChromeInset"/>, <see cref="WindowChromeLayout.TitleBarHeight"/>). It
    /// is NOT the inner width: <c>page-root</c> takes its own declared Padding out of this box, so arranging the
    /// inner width instead measures a narrower frame than the game builds - the fixed error this helper exists to
    /// remove.
    /// <para>
    /// BH1 removed the help-state argument, and with it the second box. The policy answers ONE width per screen
    /// because the panel reserves HEIGHT above the footer, so the same box reaches the page whether help is open
    /// or closed. A helper that took a help state here would be inventing a box for the frame to disagree with,
    /// which is exactly the mismatch the retired widening branch existed to paper over.
    /// </para>
    /// </summary>
    private static Vector2 PageBoxFor(float screenWidth, float screenHeight)
    {
        float windowWidth = WindowChromeLayout.SettingsWindowWidth(screenWidth, screenHeight);
        float windowHeight = WindowChromeLayout.SettingsWindowHeight(screenWidth, screenHeight);
        return new Vector2(
            windowWidth - WindowChromeLayout.WindowChromeInset,
            windowHeight - WindowChromeLayout.TitleBarHeight - WindowChromeLayout.WindowChromeInset * 0.5f);
    }

    private static UiLayoutManifest LoadManifest()
    {
        using Stream? stream = typeof(UsKernelSettingsHost).Assembly
            .GetManifestResourceStream("UniversalSqueaker.UI.Layout.Schema2.xml");
        Assert(stream != null, "the embedded Schema=2 manifest must be readable");
        using var reader = new StreamReader(stream!);
        return UiLayoutManifest.Parse(reader.ReadToEnd());
    }

    private static Dictionary<string, UiElementSpec> Index(UiLayoutManifest manifest)
    {
        var byId = new Dictionary<string, UiElementSpec>(StringComparer.Ordinal);
        foreach (UiElementSpec root in manifest.Roots) Add(root, byId);
        return byId;
    }

    private static void Add(UiElementSpec spec, Dictionary<string, UiElementSpec> byId)
    {
        if (spec.Id.Length > 0) byId[spec.Id] = spec;
        foreach (UiElementSpec child in spec.Children) Add(child, byId);
    }

    private static float DeclaredWidth(Dictionary<string, UiElementSpec> byId, string id)
    {
        Assert(byId.TryGetValue(id, out UiElementSpec? spec), "the manifest must declare '" + id + "'");
        Assert(spec!.TryGetAttribute("Width", out string raw), "'" + id + "' must declare a Width");
        Assert(float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            "'" + id + "' must declare a NUMERIC Width for the side-by-side-regime assertion, got '" + raw + "'");
        return float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static bool Beside(Rect left, Rect right)
    {
        return left.xMax <= right.x + Tol
            && left.y < right.yMax - Tol
            && right.y < left.yMax - Tol;
    }

    private static bool Above(Rect upper, Rect lower)
    {
        return upper.yMax <= lower.y + Tol
            && upper.x < lower.xMax - Tol
            && lower.x < upper.xMax - Tol;
    }

    private static bool Overlaps(Rect a, Rect b)
    {
        return a.x < b.xMax - Tol && b.x < a.xMax - Tol
            && a.y < b.yMax - Tol && b.y < a.yMax - Tol;
    }

    private static bool Finite(Rect rect)
    {
        return !float.IsNaN(rect.x) && !float.IsNaN(rect.y) && !float.IsNaN(rect.width) && !float.IsNaN(rect.height)
            && !float.IsInfinity(rect.x) && !float.IsInfinity(rect.y)
            && !float.IsInfinity(rect.width) && !float.IsInfinity(rect.height);
    }

    /// <summary>At most 25 violations are printed, so a systematic failure stays readable.</summary>
    private static string Report(List<string> problems, int max = 25)
    {
        var head = new List<string>();
        for (int i = 0; i < problems.Count && i < max; i++) head.Add("  " + problems[i]);
        if (problems.Count > max) head.Add("  ... and " + (problems.Count - max) + " more");
        return problems.Count + " frame violation(s):" + Environment.NewLine + string.Join(Environment.NewLine, head);
    }

    /// <summary>
    /// An explicit rect formatter: the stub's <c>Rect.ToString()</c> prints the type name, and a geometry
    /// lane whose failures cannot be read is a lane nobody can act on.
    /// </summary>
    private static string Fmt(Rect rect)
    {
        return "(" + rect.x.ToString("0.##", CultureInfo.InvariantCulture)
            + ", " + rect.y.ToString("0.##", CultureInfo.InvariantCulture)
            + ", " + rect.width.ToString("0.##", CultureInfo.InvariantCulture)
            + ", " + rect.height.ToString("0.##", CultureInfo.InvariantCulture) + ")";
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("FrameGeometryLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
