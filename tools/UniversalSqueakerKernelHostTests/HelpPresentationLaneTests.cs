using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// (乙1) lane: ONE player intent, TWO mutually exclusive help presentations, and the SCREEN decides which.
///
/// <para>
/// Why the mechanism exists. When the logical screen cannot host the widened window, the drawer used to
/// keep taking a third column and the centre column collapsed: at 736 (the minimum real window inner width)
/// with the drawer open the column was 168px and the fit audit reported 7 findings in EN / 2 in ZH - real
/// text overflow, measured, where the drawer-retracted case reported none. The fix is not a narrower column
/// but a different SHAPE: the same help panel appears as a full-width band between the body and the footer,
/// so the centre column keeps the geometry it has with the drawer retracted.
/// </para>
/// <para>
/// What is asserted. (1) Exactly one presentation is arranged, on a wide screen and on a capped one - the
/// clause the mutation "always present the wide column" reddens, because a capped screen would then arrange
/// the column. (2) Which one is the policy answer: WindowChromeLayout.DrawerWidensTheWindow, pinned
/// directly for both ends. (3) The retracted drawer arranges NEITHER, so the band cannot become a second
/// place the drawer state lives. (4) On the capped screen with the drawer open, the fit audit reports
/// nothing - the acceptance (乙1) is written against, measured rather than argued. (5) At the game's
/// MINIMUM logical resolution with the drawer open the band REPLACES the body (exactly one of the two is
/// arranged) and the band's height is at least the body's own measured content floor - the budget clause
/// the earlier revision of this lane was missing, and the one that reddens when the band only SHARES the
/// page with the body.
/// </para>
/// <para>
/// The screen is set explicitly because that is the input the decision reads: a 1920 monitor reaches the
/// capped case at UI scale >= ~2.5. Both fields are restored in a finally, so no later lane inherits them.
/// </para>
/// </summary>
internal static class HelpPresentationLaneTests
{
    /// <summary>
    /// Each case pairs its SCREEN with the page width that screen's window actually produces
    /// (<c>SettingsWindowWidth - 64</c>): the two are one configuration, and feeding a page width the policy
    /// cannot produce is how the old sweep ended up asserting against a shape no player can reach.
    /// </summary>
    private static readonly Vector2 WideViewport = new(1228f, 600f);
    private static readonly Vector2 CappedViewport = new(736f, 600f);
    private const string WideId = "help-scroll";
    private const string NarrowId = "help-band";

    public static int RunAll()
    {
        Step("the drawer's presentation is decided by the screen, and exactly one is ever arranged",
            ExactlyOnePresentationPerScreen);
        Console.WriteLine("HelpPresentationLaneTests ALL PASS");
        return 0;
    }

    private static void ExactlyOnePresentationPerScreen()
    {
        int savedWidth = Verse.UI.screenWidth;
        int savedHeight = Verse.UI.screenHeight;
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // The policy question, pinned at both ends before any page exists.
            Assert(WindowChromeLayout.DrawerWidensTheWindow(1920f, 1080f),
                "a 1920x1080 logical screen must be able to host the expanded window");
            Assert(!WindowChromeLayout.DrawerWidensTheWindow(800f, 600f),
                "an 800-wide logical screen cannot widen, so the drawer must not take a third column there");

            // The real Keyed tables, because this lane measures WIDTHS. Without them the translator passes
            // the key through and every caption is measured as its own key text - the first build of this
            // lane reported the header switch as needing 168px for "US.Help.Drawer.Toggle" when the shipped
            // caption is "Help" (EN) / "帮助" (ZH) and fits. A fit assertion whose input is a key name is
            // measuring the harness, not the product: the failure was real, the cause was the instrument.
            foreach (string language in new[] { "English", "ChineseSimplified" })
            {
                Program.SetTranslatorResolver(Program.ReadKeyedTable(language));

                // The game's MINIMUM logical resolution (RimWorld's ResolutionUtility floors the logical
                // size at 1024x768). There the window cannot widen - SettingsOpenWidth caps at the screen -
                // so the narrow band is the ONLY help presentation, and it is the case the maintainer walked.
                AssertMinimumResolution(language, metrics, reports);

                AssertPresentations(language, 1920, 1080, WideViewport, expectWide: true, metrics, reports,
                    "a wide screen hosts the widened window, so the help takes its column");

                AssertPresentations(language, 800, 600, CappedViewport, expectWide: false, metrics, reports,
                    "a capped screen must show the full-width band instead of stealing the centre column");
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Verse.UI.screenWidth = savedWidth;
            Verse.UI.screenHeight = savedHeight;
        }
    }

    /// <summary>
    /// The narrow band must be USABLE at the resolution the game will not go below, and usable means the
    /// body has YIELDED its slot to it - not that the two share a page too small for both.
    ///
    /// <para>
    /// Why sharing cannot work, in the arithmetic the maintainer's 1024x768 walkthrough exposed. The page
    /// box is 960x530; header ~60 + footer ~26 + the gaps ~16 leave 428 for body + band, while the body's
    /// own content floor is 271 (us/nav). So the band can never exceed ~157, and the shipped reserved band
    /// of 280 left the body ~148 - its content then overflowed its box by ~123px and painted into the band.
    /// "The band takes more than half the content area" and "the content fits" cannot both be true here.
    /// The fix is therefore EXCLUSION (the body row is not arranged while the band is), which also makes the
    /// overflow structurally impossible: with the body out of the flow the band is page-root's only flexible
    /// fill child and takes the whole leftover.
    /// </para>
    ///
    /// <para>
    /// Five numeric properties, all of them about ROOM: (1) the band is the presentation and the body is NOT
    /// arranged - exactly one of the two occupies the slot; (2) the band's viewport is at least the panel's
    /// own measured band, so the measured model never shows a truncated panel; (3) the SPACE BUDGET - the
    /// band is at least the body's own content floor, measured on the same ruler in the retracted case, which
    /// is the clause the previous revision of this lane did not have (it asserted the band was big enough and
    /// the footer was in, but never that the room the body needs still exists anywhere); (4) the footer is
    /// still inside the page; (5) the fit audit reports nothing.
    /// </para>
    ///
    /// <para>
    /// Clause (3) is also what makes the failure-mode mutation honest. Putting Fill="true" on the band while
    /// letting the body stay arranged is not caught by clauses (2) or (4) - the band is still bigger than the
    /// panel and the footer is still in - it is caught here: two flexible fill children split the leftover
    /// (~198 each against a 271 floor). And the PRE-FIX shape (a fixed 280 band with the body arranged) is
    /// caught by clause (1) and by the frame lane's containment/overlap check, where the nav column's 271px
    /// of content overflows its ~148px slot into the band.
    /// </para>
    /// </summary>
    private static void AssertMinimumResolution(string language, Program.StubMetrics metrics, List<UiOverflowReport> reports)
    {
        // 1024x768 logical -> an 800x600 window, widened to 1024x600 because the screen caps it, so the
        // page box is 960 wide; 530 is the tight height that window leaves for the page.
        const int ScreenWidth = 1024;
        const int ScreenHeight = 768;
        var page = new Vector2(960f, 530f);

        Verse.UI.screenWidth = ScreenWidth;
        Verse.UI.screenHeight = ScreenHeight;

        string where = "at the minimum logical resolution " + ScreenWidth + "x" + ScreenHeight
            + " (" + language + ", page " + page.x + "x" + page.y + ")";

        // THE RULER FIRST. The budget clause is stated against the BODY's own content floor, so the floor is
        // MEASURED - the same metrics, the same page box, the same language, drawer retracted, i.e. the
        // configuration in which the body is the presentation - instead of being written down as a number
        // somebody picked. The measurement is then checked against the documented floor (us/nav = 271px) so a
        // ruler that came back smaller cannot silently turn the comparison below into a tautology.
        const float DocumentedContentFloor = 271f;
        float contentFloor;
        float bodySlotRetracted;
        var retractedFake = new RecordingSettingsSource { RichData = true };
        using (UiHost retractedHost = UsKernelSettingsHost.Create(retractedFake, metrics))
        {
            retractedHost.Bindings.Invoke("set-tab", "Overview");
            UiLayoutSnapshot retracted = retractedHost.MeasureAndArrange(page);
            Assert(retracted.RectById.TryGetValue("nav-column", out Rect navColumn),
                where + ": the retracted drawer must arrange the nav column the content floor is measured on");
            Assert(retracted.RectById.TryGetValue("body-row", out Rect retractedBody),
                where + ": the retracted drawer must arrange the body row");
            contentFloor = navColumn.height;
            bodySlotRetracted = retractedBody.height;
        }

        Assert(contentFloor >= DocumentedContentFloor - 0.5f,
            where + ": the measured content floor must still be the documented us/nav "
            + DocumentedContentFloor + "px, got " + contentFloor
            + " - the budget clause below would otherwise compare against a ruler that shrank");

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(page);
        UiFitAudit.Reset();
        reports.Clear();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(page);
        host.DrawChecked(new Rect(0f, 0f, page.x, page.y));

        // (1) MUTUALLY EXCLUSIVE: the band is the presentation, and the body row is not arranged at all.
        // Both halves are asserted: "the band is there" alone is what a SHARED page also satisfies.
        Assert(snapshot.Viewports.TryGetValue("help-band", out Rect band),
            where + ": the narrow band must be the presentation (the screen cannot host the widened window)");
        Assert(snapshot.RectById.TryGetValue("help-panel-narrow", out Rect panel),
            where + ": the narrow panel must be arranged");
        Assert(!snapshot.RectById.ContainsKey("body-row"),
            where + ": the narrow band REPLACES the body, so the body row must not be arranged while it is -"
            + " a shared page is exactly the collision this shape removes");
        Assert(!snapshot.Viewports.ContainsKey("content-scroll") && !snapshot.RectById.ContainsKey("nav-column"),
            where + ": the body's own sub-tree (its scroll and its nav column) goes with the body row");
        Assert(!snapshot.Viewports.ContainsKey("help-scroll"),
            where + ": the wide column must not be arranged on a screen that cannot host the widened window");

        // (2) the band's viewport is a real reading surface, not a sliver of one.
        Assert(band.height >= panel.height - 0.5f,
            where + ": the band must be at least as tall as the panel's own measured band, or the help is"
            + " truncated on the only screen where it can be read: band=" + band.height + " panel=" + panel.height);

        // (3) THE SPACE BUDGET: the room the help took must be at least the room the body's content needs.
        Assert(band.height >= contentFloor - 0.5f,
            where + ": the band must be handed at least the body's own content floor, or the help has been"
            + " bought with the content's room: band=" + band.height + " floor(nav-column)=" + contentFloor
            + " bodySlot(retracted)=" + bodySlotRetracted);

        Assert(snapshot.RectById.TryGetValue("footer-band", out Rect footer),
            where + ": the footer band must be arranged");
        Assert(footer.yMax <= page.y + 0.5f,
            where + ": the footer must stay inside the page - the band's whole shape exists to keep it there:"
            + " footer bottom=" + footer.yMax + " page=" + page.y);

        Assert(reports.Count == 0,
            where + ": the fit audit must report nothing with the narrow band arranged, got " + Describe(reports));

        Console.WriteLine("[narrow-help] " + language + " screen " + ScreenWidth + "x" + ScreenHeight
            + " page " + page.x.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + "x" + page.y.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + ": band=" + band.height.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " panel=" + panel.height.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " floor(nav-column)=" + contentFloor.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " bodySlot(retracted)=" + bodySlotRetracted.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " footerBottom=" + footer.yMax.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " bodyArranged=false wideColumn=false fit=0");

        // (5) The body is HIDDEN and never REMOVED, and that difference is observable: the player's place in
        // the centre column survives the band replacing it. A removed element's node and scroll position are
        // released by the session's prune, so an implementation that dropped the body from the definition
        // (or rebuilt the root list) would fail here rather than in the game.
        host.Bindings.Set("help-open", false);
        host.MeasureAndArrange(page);
        Program.SetScrollPositionById(host.Session, "content-scroll", new Vector2(0f, 90f));
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(page);
        host.Bindings.Set("help-open", false);
        host.MeasureAndArrange(page);
        Assert(Math.Abs(Program.ScrollPositionById(host.Session, "content-scroll").y - 90f) < 0.01f,
            where + ": the centre column's scroll position must survive the body being hidden by the band -"
            + " hidden keeps the node and the state, removed does not");
    }

    private static void AssertPresentations(
        string language,
        int screenWidth,
        int screenHeight,
        Vector2 viewport,
        bool expectWide,
        Program.StubMetrics metrics,
        List<UiOverflowReport> reports,
        string why)
    {
        Verse.UI.screenWidth = screenWidth;
        Verse.UI.screenHeight = screenHeight;

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Overview");

        UiLayoutSnapshot retracted = host.MeasureAndArrange(viewport);
        Assert(!retracted.Viewports.ContainsKey(WideId) && !retracted.Viewports.ContainsKey(NarrowId),
            "the retracted drawer must arrange NEITHER presentation at " + screenWidth + "x" + screenHeight);

        host.Bindings.Set("help-open", true);
        UiFitAudit.Reset();
        reports.Clear();
        host.MeasureAndArrange(viewport);
        UiLayoutSnapshot expanded = host.MeasureAndArrange(viewport);
        host.DrawChecked(new Rect(0f, 0f, viewport.x, viewport.y));

        bool wide = expanded.Viewports.ContainsKey(WideId);
        bool narrow = expanded.Viewports.ContainsKey(NarrowId);
        Assert(wide != narrow,
            why + ": exactly ONE presentation may be arranged at " + screenWidth + "x" + screenHeight
            + " (wide=" + wide + " narrow=" + narrow + ")");
        Assert(wide == expectWide,
            why + ": expected the " + (expectWide ? "WIDE column" : "NARROW band") + " at "
            + screenWidth + "x" + screenHeight + ", got wide=" + wide);

        // The acceptance (乙1) is written against: with the drawer open on a screen that cannot widen, the
        // page must be as free of text overflow as the drawer-retracted one - which is what the narrow
        // presentation buys by not touching the centre column.
        Assert(reports.Count == 0,
            why + ": the fit audit must report nothing at " + screenWidth + "x" + screenHeight
            + " (" + language + ") with the drawer open, got " + Describe(reports));

        host.Bindings.Set("help-open", false);
        UiLayoutSnapshot closed = host.MeasureAndArrange(viewport);
        Assert(!closed.Viewports.ContainsKey(WideId) && !closed.Viewports.ContainsKey(NarrowId),
            "closing must retract whichever presentation was arranged (the band is not a second home for the"
            + " drawer state)");
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        var text = new System.Text.StringBuilder();
        text.Append(reports.Count).Append(" finding(s)");
        foreach (UiOverflowReport report in reports)
        {
            text.Append("; ").Append(report.ElementPath).Append(" ").Append(report.Axis)
                .Append(" needs ").Append(report.Needed).Append(" has ").Append(report.Available);
        }

        return text.ToString();
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("HelpPresentationLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
