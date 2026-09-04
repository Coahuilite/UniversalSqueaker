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
        XmlElement? footer = null;
        foreach (XmlNode node in pageRoot!.ChildNodes)
        {
            if (node is not XmlElement element) continue;
            if (element.Name == "Row" && element.GetAttribute("Id") == "body-row") bodyRow = element;
            if (element.Name == "Widget" && element.GetAttribute("Id") == "footer") footer = element;
        }

        Assert(bodyRow != null, "body-row Row is a direct child of page-root");
        Assert(footer != null
            && footer.GetAttribute("Kind") == "us/footer"
            && footer.GetAttribute("Height") == "28",
            "footer Widget (us/footer, Height=28) is a direct child of page-root, outside every scroll");

        Assert(bodyRow!.GetAttribute("Gap") == "12", "body-row declares Gap=12");

        bool navFill = false;
        bool contentScrollFill = false;
        bool helpScrollFill = false;
        foreach (XmlNode node in bodyRow.ChildNodes)
        {
            if (node is not XmlElement element) continue;
            if (element.Name == "Column" && element.GetAttribute("Id") == "nav-column"
                && element.GetAttribute("Width") == "192" && IsTrue(element.GetAttribute("Fill")))
            {
                navFill = true;
            }

            if (element.Name == "Scroll" && element.GetAttribute("Id") == "content-scroll"
                && IsTrue(element.GetAttribute("Fill")))
            {
                contentScrollFill = true;
            }

            if (element.Name == "Scroll" && element.GetAttribute("Id") == "help-scroll"
                && element.GetAttribute("Width") == "232" && IsTrue(element.GetAttribute("Fill")))
            {
                helpScrollFill = true;
            }
        }

        Assert(navFill && contentScrollFill && helpScrollFill,
            "body-row contains nav-column (192 Fill), content-scroll (Fill) and help-scroll (232 Fill)");

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
            "import-baseline", "toggle-pack", "forget-unavailable", "set-help-selection"
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
        // C+A: hover claims never change the panel height (bands measure against the whole catalog),
        // so the widget must not bump the revision at all; only the Host-boundary selection bump remains.
        Assert(!help.Contains("BumpContentRevision"),
            "UsHelpPanelWidget must not bump the revision itself - hover is height-invariant and selection bumps at the Host boundary");
        Assert(help.Contains("UsKernelDraw.HelpHover("),
            "the panel's index rows claim hover through the single UsKernelDraw.HelpHover outlet");
        Assert(!help.Contains("ctx.Bindings.Invoke(\"set-help-selection\"")
            || help.Contains("Host binding boundary"),
            "help selection bump is owned by the Host boundary, not the widget");

        string draw = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsKernelDraw.cs"));
        Assert(draw.Contains("DropdownButton(rect, elementId, ctx);") && draw.Contains("OpenPopupAnchor"),
            "UsKernelDraw dropdown trigger stores the Host window-space anchor and draws from it");

        Assert(host.Contains("BindAction<string>(\"set-pack-filter\"") && host.Contains("source.SetPackFilter(value); bump();"),
            "FilterBar reset action exists and invalidates layout");
        Assert(host.Contains("BindAction<UsPackToggle>") && host.Contains("toggle.Enabled); bump();"),
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
    /// Structural guard for the Overview workspace: Basic tuning owns Easter eggs plus the three
    /// runtime scaling toggles. Distance presets belong exclusively to the attenuation workspace.
    /// Measure and fallback must share one height formula.
    /// </summary>
    private static void VerifyBasicTuningHasNoDistanceDuplicate(string root)
    {
        string path = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsBasicTuningWidget.cs");
        string text = File.ReadAllText(path);

        Assert(!text.Contains("DrawDistanceRow") && !text.Contains("set-distance-preset"),
            "Basic tuning does not duplicate the Distance workspace preset control");
        Assert(text.Contains("FallbackHeight") && text.Contains("MeasureBody") && text.Contains("ContentHeight"),
            "basic tuning shares one content-height formula across fallback and measure");
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
