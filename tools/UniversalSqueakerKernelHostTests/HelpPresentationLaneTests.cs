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
/// nothing - the acceptance (乙1) is written against, measured rather than argued.
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
    /// The narrow band must be USABLE at the resolution the game will not go below. The maintainer's
    /// 1024x768 walkthrough found the opposite: the band appeared but was too short to hold the help, so
    /// on that screen - where the band is the only way to read help at all - it amounted to no help.
    ///
    /// <para>
    /// Three numeric properties, all of them about ROOM rather than about pixels for their own sake:
    /// (1) the band's viewport is at least the panel's own measured band, so the measured model never shows
    /// a truncated panel; (2) the band takes at least HALF of the page's vertical content area (the band
    /// plus the body row) - on a screen that cannot widen, the help the player just opened takes the
    /// majority of the room, and the content, still a Scroll, keeps the rest; (3) the footer is still inside
    /// the page, which is the property the band's shape was chosen for and which the previous Fill-based
    /// shape did NOT actually deliver (measured: page natural height 660.7 against a 530 viewport, footer
    /// bottom 648.7).
    ///
    /// <para>
    /// The first attempt at this lane asserted the stricter "band >= body row". That FAILED at the band
    /// height the first fix chose (band 240, body 271), and the band was raised to 280 rather than the rule
    /// being lowered - so the criterion here (half the content area, which is the floor the lead named) is
    /// now satisfied with margin by the shipped number, and the previously-red properties stay red under the
    /// old shape.
    /// </para>
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

        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Set("help-open", true);
        host.MeasureAndArrange(page);
        UiFitAudit.Reset();
        reports.Clear();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(page);
        host.DrawChecked(new Rect(0f, 0f, page.x, page.y));

        string where = "at the minimum logical resolution " + ScreenWidth + "x" + ScreenHeight
            + " (" + language + ", page " + page.x + "x" + page.y + ")";

        Assert(snapshot.Viewports.TryGetValue("help-band", out Rect band),
            where + ": the narrow band must be the presentation (the screen cannot host the widened window)");
        Assert(snapshot.RectById.TryGetValue("help-panel-narrow", out Rect panel),
            where + ": the narrow panel must be arranged");
        Assert(snapshot.RectById.TryGetValue("body-row", out Rect body),
            where + ": the body row must be arranged");
        Assert(snapshot.RectById.TryGetValue("footer-band", out Rect footer),
            where + ": the footer band must be arranged");

        Assert(band.height >= panel.height - 0.5f,
            where + ": the band must be at least as tall as the panel's own measured band, or the help is"
            + " truncated on the only screen where it can be read: band=" + band.height + " panel=" + panel.height);

        float contentArea = band.height + body.height;
        Assert(band.height >= contentArea * 0.5f - 0.5f,
            where + ": on a screen that cannot widen, the help the player opened must take at least half of"
            + " the page's vertical content area: band=" + band.height + " body=" + body.height
            + " contentArea=" + contentArea);

        Assert(footer.yMax <= page.y + 0.5f,
            where + ": the footer must stay inside the page - the band's whole shape exists to keep it there:"
            + " footer bottom=" + footer.yMax + " page=" + page.y);

        Assert(reports.Count == 0,
            where + ": the fit audit must report nothing with the narrow band arranged, got " + Describe(reports));
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
