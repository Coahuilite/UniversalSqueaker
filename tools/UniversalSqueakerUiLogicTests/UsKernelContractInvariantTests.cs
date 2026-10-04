using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// Structural (source/XML-level) guards for the Gate U kernel contract. The UiKit kernel widgets
/// and the production Settings Host depend on Verse/Unity at runtime, so this zero-Verse gate
/// cannot instantiate them. These checks are intentionally NARROW STRUCTURAL GUARDS — they fail
/// when a required contract line disappears, but they are NOT behavioral evidence. Real-schema
/// Host creation behavior is covered by the main assembly Dev/Release build gates and by the
/// neutral behavioral fixture in the UiKit test harness; full real-schema runtime behavior stays
/// LIMITED until an in-game run proves it.
/// </summary>
internal static class UsKernelContractInvariantTests
{
    public static void RunAll()
    {
        string root = RepoRoot();
        VerifyRealSchema2RootStructure(root);
        VerifyHostRevisionBumpBoundary(root);
        VerifyBasicTuningHasNoDistanceDuplicate(root);
    }

    /// <summary>
    /// Structural check of the REAL embedded Schema=2 manifest: the fixed footer must be a sibling
    /// of the body row (never inside the content scroll), the body row must be a Row of Fill
    /// children, and the footer must declare Height=28. Layout math (non-overlap) is asserted
    /// behaviorally by the neutral equivalent fixture in the UiKit harness; this guard pins the
    /// real XML shape.
    /// </summary>
    private static void VerifyRealSchema2RootStructure(string root)
    {
        string path = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml");
        if (!File.Exists(path))
        {
            throw new Exception("Real Schema2 manifest missing: " + path);
        }

        var document = new XmlDocument();
        document.XmlResolver = null;
        document.Load(path);

        XmlElement? page = document.DocumentElement;
        Assert(page != null && page.Name == "UiPage" && page.GetAttribute("Schema") == "2",
            "real Schema2 manifest root is UiPage Schema=2");

        XmlElement? pageRoot = null;
        foreach (XmlNode node in page!.ChildNodes)
        {
            if (node is XmlElement element && element.Name == "Column" && element.GetAttribute("Id") == "page-root")
            {
                pageRoot = element;
            }
        }

        Assert(pageRoot != null && pageRoot.GetAttribute("Gap") == "8" && pageRoot.GetAttribute("Padding") == "12",
            "page-root Column declares Gap=8 Padding=12");

        XmlElement? bodyRow = null;
        XmlElement? footerBand = null;
        foreach (XmlNode node in pageRoot!.ChildNodes)
        {
            if (node is not XmlElement element) continue;
            if (element.Name == "Row" && element.GetAttribute("Id") == "body-row") bodyRow = element;
            if (element.GetAttribute("Id") == "footer-band") footerBand = element;
        }

        Assert(bodyRow != null, "body-row Row is a direct child of page-root");
        // The footer band is a direct child of page-root (outside every scroll), and NEITHER it nor its
        // children may pin a Height: the attribute overrides the wrap-aware measure, which is how the
        // in-game log kept reporting "footer needs 33px has 28px" while the attribute held 28 (2026-09-15).
        // BH1 turned the band from an Overlay into a ROW, because the page's single help switch now lives
        // beside the status text: the Row hands the unsized us/footer the leftover after the fixed-width
        // switch, so no reserved width is spelled in code. The kind is asserted, not assumed.
        Assert(footerBand != null && footerBand.Name == "Row"
            && footerBand.GetAttribute("Height") == "",
            "footer-band must be a Row that is a direct child of page-root, outside every scroll, with no"
            + " Height (BH1: the band holds the status widget AND the single help switch)");

        XmlElement? footer = null;
        XmlElement? helpToggle = null;
        foreach (XmlNode node in footerBand!.ChildNodes)
        {
            if (node is not XmlElement element) continue;
            if (element.GetAttribute("Id") == "footer")
            {
                Assert(footer == null, "the footer band must carry exactly one status widget");
                footer = element;
            }
            else if (element.GetAttribute("Id") == "help-toggle")
            {
                Assert(helpToggle == null, "the footer band must carry exactly one help switch");
                helpToggle = element;
            }
            else
            {
                Assert(false, "the footer band may hold only the status widget and the help switch, found "
                    + element.Name + " Id='" + element.GetAttribute("Id") + "'");
            }
        }

        Assert(footer != null
                && footer.Name == "Widget"
                && footer.GetAttribute("Kind") == "us/footer"
                && footer.GetAttribute("Height") == "",
            "footer Widget (us/footer, no Height attribute) is the band's status half");
        Assert(helpToggle != null
                && helpToggle.GetAttribute("Kind") == "input/button"
                && helpToggle.GetAttribute("ActionBind") == "toggle-help-drawer"
                && helpToggle.GetAttribute("VisibleKey") == "",
            "the band's switch is the carrier's own input/button bound to the panel command and gated by"
            + " nothing: the panel it opens is the page's other help state surface, and a second visibility"
            + " key would be a second truth about whether help is showing");

        Assert(bodyRow!.GetAttribute("Gap") == "12", "body-row declares Gap=12");

        bool navFixed = false;
        bool navFlexSlot = false;
        bool contentScrollFill = false;
        int helpInsideBody = 0;
        foreach (XmlNode node in bodyRow.ChildNodes)
        {
            if (node is not XmlElement element) continue;
            if (element.Name == "Scroll" && element.GetAttribute("Id") == "nav-column")
            {
                navFixed = element.GetAttribute("Width") == "200";
                navFlexSlot = IsTrue(element.GetAttribute("Fill"));
            }

            if (element.Name == "Scroll" && element.GetAttribute("Id") == "content-scroll"
                && IsTrue(element.GetAttribute("Fill")))
            {
                contentScrollFill = true;
            }

            // BH1: nothing help-shaped may sit inside the body row again. A help Scroll returning here is
            // the side column coming back, which is the shape that cost the settings their width.
            if (element.GetAttribute("Id").StartsWith("help", StringComparison.Ordinal)) helpInsideBody++;
        }

        // V1 replaces the plain Column with an actual Scroll: its Fill viewport can shrink while its
        // natural nav content is clipped and remains reachable. A plain Fill Column still violates the
        // frame containment guards; SettingsGeometryLaneTests drives the scrolled final destination.
        Assert(navFixed && navFlexSlot && contentScrollFill && helpInsideBody == 0,
            "body-row contains the width-fixed nav Scroll (200, Fill) and content-scroll (Fill) and NO help"
            + " element of its own (BH1: the panel is a page-root sibling below the body)");
        Assert(navFlexSlot,
            "nav-column must be a shrinking scroll viewport so its natural content cannot overflow the frame");

        XmlElement? helpBand = null;
        foreach (XmlNode node in pageRoot!.ChildNodes)
        {
            if (node is XmlElement element && element.Name == "Scroll"
                && element.GetAttribute("Id") == "help-scroll")
            {
                helpBand = element;
            }
        }

        Assert(helpBand != null && helpBand.GetAttribute("Height") == "140"
                && !IsTrue(helpBand!.GetAttribute("Fill"))
                && helpBand.GetAttribute("VisibleKey") == "help-open",
            "page-root must declare the help band as a fixed-Height, non-Filling Scroll gated by the one"
            + " player intent (BH1.1's reservation: height out of the body, never a column beside it)");

        bool hasTabSections = false;
        foreach (XmlNode node in bodyRow.ChildNodes)
        {
            if (node is not XmlElement element || element.Name != "Scroll") continue;
            foreach (XmlNode child in element.ChildNodes)
            {
                if (child is XmlElement widget && widget.GetAttribute("Tab").Length > 0)
                {
                    hasTabSections = true;
                }
            }
        }

        Assert(hasTabSections, "content scroll contains Tab-gated dynamic sections (layout reflows per tab)");
    }

    /// <summary>
    /// Structural guard for the Host binding/action revision boundary: layout-affecting actions
    /// must bump the session content revision through the Host's SessionRevisionBumper, and the
    /// kernel widgets must not double-bump themselves (help hover keeps its text-diff guard).
    /// Behavioral cache invalidation is asserted by the neutral UiKit harness.
    /// </summary>
    private static void VerifyHostRevisionBumpBoundary(string root)
    {
        string hostFile = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsHost.cs");
        string host = File.ReadAllText(hostFile);

        Assert(host.Contains("SessionRevisionBumper") && host.Contains("bump();"),
            "UsKernelSettingsHost wires a SessionRevisionBumper at the binding boundary");

        string[] layoutAffectingKeys =
        {
            "set-tab", "scroll-to", "set-tuning-layer", "set-tuning-domain", "select-domain",
            "set-domain-filter", "set-pack-filter", "race-filter", "xenotype-filter", "pack-filter", "search-text",
            "toggle-baseline-preset", "toggle-baseline-race", "toggle-baseline-xenotype",
            "import-baseline", "toggle-pack", "forget-unavailable"
        };
        foreach (string key in layoutAffectingKeys)
        {
            Assert(host.Contains("\"" + key + "\""),
                "Host binding boundary covers layout-affecting key '" + key + "'");
        }

        string nav = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsNavWidget.cs"));
        Assert(!nav.Contains("BumpContentRevision"),
            "UsNavWidget must not bump the revision itself (Host boundary owns tab/scroll-to)");

        string checklist = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsVoicePackChecklistWidget.cs"));
        Assert(!checklist.Contains("BumpContentRevision"),
            "UsVoicePackChecklistWidget must not bump the revision itself (Host boundary owns search-text)");

        string help = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsHelpPanelWidget.cs"));
        // C+A + D2: the panel is a pure read surface. Hover claims never change its height (bands
        // measure against the whole catalog) and the pinned selection is retired, so it owns no
        // write channel and must never bump the revision.
        Assert(!help.Contains("BumpContentRevision"),
            "UsHelpPanelWidget must not bump the revision itself - hover is height-invariant and the panel writes nothing");
        Assert(!help.Contains(".Invoke"),
            "the help panel has no write channel at all: claims arrive only from the controls via UsKernelDraw.HelpHover");

        string draw = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsKernelDraw.cs"));
        Assert(draw.Contains("DropdownButton(rect, elementId, ctx);") && draw.Contains("OpenPopupAnchor"),
            "UsKernelDraw dropdown trigger stores the Host window-space anchor and draws from it");

        // Re-cut 2026-09-24 (T3-1): the write-registration funnel renamed the raw `BindAction<...>` calls to
        // `writes.Action<...>`. Both clauses assert the same INTENT - the action exists on the Host and its
        // handler invalidates the layout - so the receiver is no longer pinned; "it must be registered through
        // the funnel" is asserted by VerifyWriteBindingsGoThroughTheRegistry instead.
        Assert(host.Contains("Action<string>(\"set-pack-filter\"") && host.Contains("source.SetPackFilter(value); bump();"),
            "FilterBar reset action exists and invalidates layout");
        Assert(host.Contains("Action<UsPackToggle>") && host.Contains("toggle.Enabled); bump();"),
            "VoicePack toggles invalidate filtered dynamic layout");

        string sourceInterface = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "IUsKernelSettingsSource.cs"));
        Assert(sourceInterface.Contains("SetBasicToggle(SqueakBasicToggle")
            && sourceInterface.Contains("SetDomainFilter(SqueakDomainFilterKind")
            && sourceInterface.Contains("SetMoodTuning(SqueakMood mood, SqueakMoodFactor"),
            "Kernel business selectors use closed enum types");
        Assert(!sourceInterface.Contains("SetBasicToggle(string")
            && !sourceInterface.Contains("SetDomainFilter(string")
            && !sourceInterface.Contains("SetMoodTuning(SqueakMood mood, string"),
            "Kernel business boundary contains no string selector protocol");
    }

    /// <summary>
    /// Structural guard for the Overview workspace. S4-1 dissolved the us/basic-tuning composite into a
    /// declared Section subtree, so the guard reads the manifest instead of a widget file - the property
    /// is unchanged: Basic tuning owns Easter eggs plus the three runtime scaling toggles, each on its own
    /// bool value binding, and the Distance workspace's preset action is named nowhere in the page.
    /// </summary>
    private static void VerifyBasicTuningHasNoDistanceDuplicate(string root)
    {
        string path = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml");
        var document = new XmlDocument();
        document.XmlResolver = null;
        document.Load(path);

        XmlElement? card = null;
        foreach (XmlNode node in document.SelectNodes("//*[@Id='basic-tuning']")!)
        {
            if (node is XmlElement element) card = element;
        }

        Assert(card != null && card.Name == "Section",
            "the basic-tuning card is the declarative Section container (us/basic-tuning is retired)");

        var binds = new List<string>();
        foreach (XmlNode node in card!.SelectNodes(".//Widget")!)
        {
            if (node is XmlElement widget && widget.HasAttribute("Bind")) binds.Add(widget.GetAttribute("Bind"));
        }

        foreach (string expected in new[] { "allow-eggs", "scale-cooldown", "scale-talking", "scale-population" })
        {
            Assert(binds.Contains(expected),
                "the declarative basic-tuning card owns a control on binding '" + expected + "'");
        }

        // The parent/child rule is declarative too: the child row is gated by the parent's own bool.
        XmlElement? child = null;
        foreach (XmlNode node in card.SelectNodes(".//*[@Id='basic-eat-child-row']")!)
        {
            if (node is XmlElement element) child = element;
        }

        Assert(child != null && child.GetAttribute("VisibleKey") == "eat-precision",
            "the eat-precision child row is gated by VisibleKey reading the parent's own bool binding");

        // The control lives in the Distance workspace and must not appear on this card. The assertion is
        // scoped to the CARD rather than to the whole manifest: since S4-3b the Distance workspace's own
        // three preset buttons are declarative input/button atoms, so the action legitimately exists in this
        // file now - what must stay true is that no control on basic-tuning carries it.
        foreach (XmlNode node in card.SelectNodes(".//*[@ActionBind]")!)
        {
            if (node is XmlElement bound)
            {
                Assert(bound.GetAttribute("ActionBind") != "set-distance-preset",
                    "Basic tuning does not duplicate the Distance workspace preset control");
            }
        }
    }

    private static bool IsTrue(string value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static string RepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && current != null; i++)
        {
            // Only US's own tree: FerriteLib left for its own repository, and the neutrality guard
            // that used to live here moved with it - asserted by the library from inside, with a
            // positive control. The old note that the library "cannot assert its own neutrality"
            // turned out to be wrong once the exemption was pinned to the scanner file itself.
            if (Directory.Exists(Path.Combine(current, "Source", "UniversalSqueaker")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new DirectoryNotFoundException("Could not locate repository root from " + AppContext.BaseDirectory);
    }
}
