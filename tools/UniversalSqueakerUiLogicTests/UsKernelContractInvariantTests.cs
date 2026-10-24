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
    /// Existing real-resource structure guard, migrated to WO r3: one home or secondary content
    /// destination, the same independent wrap-aware footer, and a full-content detailed-help page.
    /// The old fixed sidebar/bottom reservation is explicitly replaced, not silently tolerated.
    /// </summary>
    private static void VerifyRealSchema2RootStructure(string root)
    {
        string path = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml");
        if (!File.Exists(path)) throw new Exception("Real Schema2 manifest missing: " + path);
        var document = new XmlDocument { XmlResolver = null };
        document.Load(path);
        XmlElement? page = document.DocumentElement;
        Assert(page != null && page.Name == "UiPage" && page.GetAttribute("Schema") == "2",
            "real Schema2 manifest root is UiPage Schema=2");
        var pageRoot = page!.SelectSingleNode("Column[@Id='page-root']") as XmlElement;
        Assert(pageRoot != null && pageRoot.GetAttribute("Gap") == "6" && pageRoot.GetAttribute("Padding") == "12",
            "compact page root keeps the declared six-unit gap and twelve-unit inner padding");
        var home = pageRoot!.SelectSingleNode("Column[@Id='voice-home']") as XmlElement;
        var footerBand = pageRoot.SelectSingleNode("*[@Id='footer-band']") as XmlElement;
        Assert(home != null && IsTrue(home.GetAttribute("Fill")) && home.GetAttribute("VisibleBind") == "page-Voices",
            "the Voice management home occupies one flexible page destination");
        Assert(footerBand != null && footerBand.Name == "Row" && !footerBand.HasAttribute("Height"),
            "footer is a direct page sibling outside every scroll, with wrap-aware height");
        XmlElement? footer = null;
        XmlElement? retry = null;
        foreach (XmlNode node in footerBand!.ChildNodes)
        {
            if (node is not XmlElement element) continue;
            if (element.GetAttribute("Id") == "footer")
            {
                Assert(footer == null, "the footer band must carry exactly one status widget");
                footer = element;
            }
            else if (element.GetAttribute("Id") == "save-retry")
            {
                Assert(retry == null, "the footer band must carry exactly one save-retry action");
                retry = element;
            }
            else Assert(false, "footer may hold only real save status and Retry, found " + element.GetAttribute("Id"));
        }
        Assert(footer != null && footer.Name == "Widget" && footer.GetAttribute("Kind") == "us/footer"
                && !footer.HasAttribute("Height"),
            "status widget has no fixed height that could truncate translated save feedback");
        Assert(retry != null && retry.GetAttribute("Kind") == "input/button"
                && retry.GetAttribute("ActionBind") == "retry-save" && retry.GetAttribute("VisibleBind") == "save-failed",
            "footer action is the existing save-owner Retry, visible only on real failure");
        Assert(home!.GetAttribute("Gap") == "6", "home retains compact six-unit vertical gaps");
        var browse = home.SelectSingleNode("Row[@Id='voice-browse-tabs']") as XmlElement;
        var split = home.SelectSingleNode("Row[@Id='voice-split']") as XmlElement;
        bool navigation = browse != null && !browse.HasAttribute("Width")
            && document.SelectSingleNode("//*[@Id='nav-column']") == null;
        bool listScroll = split?.SelectSingleNode("Scroll[@Id='voice-list-scroll'][@Fill='true']") != null;
        bool detailScroll = split?.SelectSingleNode("Scroll[@Id='voice-detail-scroll'][@Fill='true']") != null;
        Assert(navigation && listScroll && detailScroll && home.SelectSingleNode(".//*[@Id='help-panel']") == null,
            "home has compact browse navigation and flexible list/detail scrolls, never a permanent help column");
        Assert(listScroll && detailScroll,
            "both list and detail remain shrinking scroll viewports rather than overflowing natural-height columns");
        var help = pageRoot.SelectSingleNode("Scroll[@Id='page-Help-scroll']") as XmlElement;
        Assert(help != null && !help.HasAttribute("Height") && IsTrue(help.GetAttribute("Fill"))
                && help.GetAttribute("VisibleBind") == "page-Help",
            "detailed help is a full-content destination, not a reserved 140-unit bottom band");
        bool hasPageSections = false;
        foreach (XmlNode node in pageRoot.ChildNodes)
            if (node is XmlElement element && element.Name == "Scroll"
                && element.GetAttribute("VisibleBind").StartsWith("page-", StringComparison.Ordinal)) hasPageSections = true;
        Assert(hasPageSections, "secondary content scrolls reflow through explicit page visibility bindings");
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
            // US-PACK1: the per-list searches retired with the browse cards; the card gesture and the
            // result-layer return are the new layout-affecting writes (toggle-pack left with the checklist
            // list - the card row's switch is the item-scoped enabled VALUE instead).
            "toggle-pack-card", "cancel-pack-results",
            "toggle-baseline-preset", "toggle-baseline-race", "toggle-baseline-xenotype",
            "import-baseline", "forget-unavailable"
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
        Assert(draw.Contains("DropdownButton(rect, popupId, ctx);") && draw.Contains("OpenPopupAnchor"),
            "UsKernelDraw dropdown trigger stores the Host window-space anchor and draws from it");

        // Re-cut 2026-09-24 (T3-1): the write-registration funnel renamed the raw `BindAction<...>` calls to
        // `writes.Action<...>`. Both clauses assert the same INTENT - the action exists on the Host and its
        // handler invalidates the layout - so the receiver is no longer pinned; "it must be registered through
        // the funnel" is asserted by VerifyWriteBindingsGoThroughTheRegistry instead.
        Assert(host.Contains("Action<string>(\"set-pack-filter\"") && host.Contains("source.SetPackFilter(value); bump();"),
            "FilterBar reset action exists and invalidates layout");
        // US-PACK1: the checklist's page-level "toggle-pack" ACTION retired with the list; the card row's
        // switch is an item-scoped VALUE whose write resolves the (pack, domain) identity and bumps.
        Assert(host.Contains("ItemValue<bool>(") && host.Contains("source.ToggleVoicePack(row.Value.Scope")
                && host.Contains("ToggleRow(key, value)"),
            "VoicePack card rows toggle the filtered dynamic layout through the item-scoped enabled value");

        string sourceInterface = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "IUsKernelSettingsSource.cs"));
        Assert(sourceInterface.Contains("SetTalkingScaling(bool")
            && sourceInterface.Contains("SetDomainFilter(SqueakDomainFilterKind")
            && sourceInterface.Contains("SetActionScope(string actionKey, BehaviorScope?"),
            "current business selectors preserve boolean and closed scope/filter types");
        string settings = File.ReadAllText(Path.Combine(root,"Source","UniversalSqueaker","Settings","GameSettings.cs"));
        Assert(!settings.Contains("ActionPresentations") && settings.Contains("BuiltInPawnContracts.NormalizeScope"),
            "UI labels do not own settings scope normalization");
        Assert(typeof(ActionPresentation).GetField("SupportedScopes") == null && typeof(ActionPresentation).GetField("DefaultScope") == null,
            "presentation has no second scope capability/default table");
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

        foreach (string expected in new[] { "allow-eggs", "scale-talking" })
        {
            Assert(binds.Contains(expected),
                "the declarative basic-tuning card owns a control on binding '" + expected + "'");
        }

        foreach (string axis in new[] { "phase", "nutrition", "drug" })
        {
            var choice = card.SelectSingleNode(".//Widget[@Bind='eat-" + axis + "']") as XmlElement;
            Assert(choice != null && choice.GetAttribute("Kind") == "input/dropdown" && !choice.HasAttribute("VisibleBind"),
                "DB three independent Eat selectors exist without parent/child visibility");
        }
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
        string cwd = Environment.CurrentDirectory;
        if (File.Exists(Path.Combine(cwd, "Source", "UniversalSqueaker", "UniversalSqueaker.csproj"))) return cwd;
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
