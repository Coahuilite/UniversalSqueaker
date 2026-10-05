using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// V4 evidence lane (contract V4-REMAINING-CONTRACT-20261004 r1, task-20): the Distance editor, the Presets
/// workspace, the single context-help surface and the footer, measured at the REAL configuration of a
/// 1024x768 screen - the one page box the window policy gives (760x524 since BH1 retired the drawer-expanded
/// width), in BOTH help states (help-open really set, and help closed) - in EN and ZH, with
/// production-shaped state.
///
/// Faithful reverts are recorded in the PM V4 evidence: restoring Small for preset rows reddens the
/// real-box fit clause; removing the axis caption reddens its arrangement clause and the baseline note its draw clause;
/// ignoring status visibility reddens the hidden-Idle clause. Completeness and existing help lifecycle
/// checks are GUARDs; a scroll container's presence alone does not prove overflowing content.
/// Removing the dirty-only predicate reddens its named marker clause; final source restores all five fixes.
/// </summary>
internal static class V4RemainingLaneTests
{
    // The page box the shell hands the page on the game's minimum logical screen. BH1 (2026-10-05) retired
    // the drawer-expanded second width: the help panel reserves HEIGHT above the footer, so help open and
    // help closed are arranged in the SAME box, read here out of the policy instead of being spelled.
    private const float MinimumScreenWidth = 1024f;
    private const float MinimumScreenHeight = 768f;

    /// <summary>The policy's page box for a screen: the window minus the side chrome, minus the title bar and
    /// the shell's own bottom inset. Since BH1 this one function answers BOTH help states, because the policy
    /// takes no help argument.</summary>
    private static Vector2 PageBoxAt(float screenWidth, float screenHeight) => new Vector2(
        WindowChromeLayout.SettingsWindowWidth(screenWidth, screenHeight) - WindowChromeLayout.WindowChromeInset,
        WindowChromeLayout.SettingsWindowHeight(screenWidth, screenHeight)
        - WindowChromeLayout.TitleBarHeight - WindowChromeLayout.WindowChromeInset * 0.5f);

    private static readonly Vector2 PageBox = PageBoxAt(MinimumScreenWidth, MinimumScreenHeight);

    private const string LongDevIdentity =
        "dev-1a2b3c4d5e6f (0.4.0+1a2b3c4d5e6f778899aabbccddeeff0011223344)";

    /// <summary>
    /// V4.1/V4.2 at the real box, in both help states: the Distance workspace keeps its chart, status and
    /// controls, the Presets workspace keeps its composite and its controls, both stay inside the page with
    /// the fit audit green, and the content really scrolls.
    /// </summary>
    private static void DistanceAndPresetsAtTheRealBoxes()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach ((string state, Vector2 box, bool helpOpen) in new[]
                         {
                             ("help open", PageBox, true),
                             ("help closed", PageBox, false),
                         })
                {
                    var metrics = new RecordingMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource { RichData = true, PresetLabel = null };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        host.Bindings.Set("help-open", helpOpen);

                        // Distance (V4.1).
                        host.Bindings.Invoke("set-tab", "Distance");
                        host.MeasureAndArrange(box);
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        UiFitAudit.Reset();
                        reports.Clear();
                        UiLayoutSnapshot distance = host.MeasureAndArrange(box);
                        metrics.Clear();
                        host.DrawChecked(new Rect(0f, 0f, box.x, box.y));

                        string where = "V4 " + language + ", " + state + " at " + box.x + "x" + box.y;
                        foreach (string id in new[] { "attenuation-editor", "attenuation-axis-caption", "attenuation-chart", "attenuation-status",
                                     "attenuation-preset-conservative", "attenuation-preset-balanced", "attenuation-preset-strong" })
                        {
                            Assert(distance.RectById.ContainsKey(id), where + ": Distance must arrange '" + id + "'");
                        }

                        Assert(distance.Viewports.ContainsKey("help-scroll") == helpOpen,
                            where + ": the bottom help panel must be arranged exactly while help is open");
                        if (helpOpen)
                        {
                            Assert(distance.RectById.ContainsKey("body-row")
                                    && distance.RectById.ContainsKey("nav-column")
                                    && distance.Viewports.ContainsKey("content-scroll"),
                                where + ": the open help panel must be arranged with the body row, the nav"
                                + " column and the centre scroll still arranged - it takes height above the"
                                + " footer, not the settings");
                        }
                        else
                        {
                            Assert(distance.RectById.TryGetValue("body-row", out Rect closedBody),
                                where + ": the body row must stay arranged with help closed");
                            Assert(distance.RectById.TryGetValue("footer-band", out Rect closedFooter),
                                where + ": the footer band must stay arranged with help closed");
                            Assert(Math.Abs(closedBody.yMax
                                    - (closedFooter.y - DeclaredFloat(host.Manifest.Roots[0], "Gap"))) <= 0.5f,
                                where + ": with help closed the body row must keep the WHOLE leftover down to"
                                + " the footer, paying only the page gap (body yMax " + Num(closedBody.yMax)
                                + ", footer y " + Num(closedFooter.y) + ")");
                        }

                        Rect distanceViewport = distance.Viewports["content-scroll"];
                        bool distanceScrolls = distance.ScrollContents.TryGetValue("content-scroll", out Rect distanceContent)
                            && distanceContent.height > 0f && distanceViewport.height > 0f;
                        Assert(distanceScrolls,
                            where + ": Distance must have non-empty content and a live scroll viewport");
                        Assert(metrics.Seen(host.Bindings.Get<string>("attenuation-axis-caption")),
                            where + ": the camera-height and volume axes must be drawn, not only bound");
                        Assert(reports.Count == 0,
                            where + ": Distance must have no fit finding, got " + Describe(reports));

                        // BH1's cross-state clauses on this host: the policy has ONE width function, so both
                        // help states must be arranged in the same page box, and the panel's whole cost must
                        // be the height it reserves. Runs after the fit clause so its extra passes cannot
                        // add findings to this report set; the caller's help state is restored.
                        AssertHelpPanelReservesHeightNotWidth(host, box, where);

                        // Presets (V4.2): first the exact default fixture that exposed the xenotype red.
                        host.Bindings.Invoke("set-tab", "Presets");
                        host.MeasureAndArrange(box);
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        UiFitAudit.Reset();
                        reports.Clear();
                        UiLayoutSnapshot presets = host.MeasureAndArrange(box);
                        metrics.Clear();
                        host.DrawChecked(new Rect(0f, 0f, box.x, box.y));
                        Assert(presets.RectById.ContainsKey("preset-list"),
                            where + ": the Presets workspace must arrange preset-list");
                        Rect presetViewport = presets.Viewports["content-scroll"];
                        bool presetScrolls = presets.ScrollContents.TryGetValue("content-scroll", out Rect presetContent)
                            && presetContent.height > 0f && presetViewport.height > 0f;
                        Assert(presetScrolls,
                            where + ": Presets must have non-empty content and a live scroll viewport");
                        Assert(metrics.Seen(Program.ReadKeyedTable(language)["US.Preset.List.BaselineNote"]),
                            where + ": the read-only baseline versus current-value note must be drawn");
                        Assert(reports.Count == 0,
                            where + ": Presets must have no fit finding at the real box (default fixture), got " + Describe(reports));

                        var longSource = new RecordingSettingsSource { RichData = true,
                            PresetLabel = string.Concat(Enumerable.Repeat("Long readable baseline name ", 20)) };
                        using UiHost longHost = UsKernelSettingsHost.Create(longSource, metrics);
                        longHost.Bindings.Set("help-open", helpOpen);
                        longHost.Bindings.Invoke("set-tab", "Presets");
                        reports.Clear();
                        UiFitAudit.Reset();
                        UiLayoutSnapshot longSnapshot = longHost.MeasureAndArrange(box);
                        longHost.DrawChecked(new Rect(0f, 0f, box.x, box.y));
                        Rect longContent = longSnapshot.ScrollContents["content-scroll"];
                        Rect longViewport = longSnapshot.Viewports["content-scroll"];
                        Assert(longContent.height > longViewport.height && reports.Count == 0,
                            where + ": actual long preset label must wrap and overflow into a usable scroll viewport");
                        Program.SetScrollPositionById(longHost.Session, "content-scroll", new Vector2(0f, 50f));
                        longHost.MeasureAndArrange(box);
                        Assert(Math.Abs(Program.ScrollPositionById(longHost.Session, "content-scroll").y - 50f) < 0.01f,
                            where + ": overflowing long preset content must retain a nonzero scroll position");

                        Console.WriteLine("[v4-workspaces] " + where
                            + " distance=" + Describe(distance.RectById["attenuation-editor"])
                            + " content=" + Num(distanceContent.height) + "/" + Num(distanceViewport.height)
                            + " presets=" + Describe(presets.RectById["preset-list"])
                            + " presetContent=" + Num(presetContent.height) + "/" + Num(presetViewport.height)
                            + " fit=0 help=" + (helpOpen ? "open" : "closed"));
                    }
                    finally
                    {
                        UiFitAudit.Detach();
                        UiFitAudit.Enabled = false;
                    }
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    /// <summary>
    /// V4.4: the footer must keep the save status AND still carry the FULL build identity - the band grows to
    /// fit the longest form (the US_DEV form Mod.cs:132-143 builds) instead of clipping it, at the one real
    /// box in both help states and in both languages. Since BH1 the band is a ROW that shares that width with
    /// the help switch, so the identity is wrapped inside the leftover beside it. The SHIPPED downgrade is a
    /// build-configuration property, not a display
    /// truncation: BuildIdentity returns the short <c>AssemblyInformationalVersion</c> unless US_DEV.
    /// </summary>
    private static void FooterCarriesStatusAndTheFullIdentity()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach ((string state, Vector2 box, bool helpOpen) in new[]
                         {
                             ("help open", PageBox, true),
                             ("help closed", PageBox, false),
                         })
                {
                    var metrics = new Program.StubMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource
                        {
                            RichData = true,
                            BuildIdentityOverride = LongDevIdentity,
                        };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        host.Bindings.Set("help-open", helpOpen);
                        UiFitAudit.Reset();
                        reports.Clear();
                        UiLayoutSnapshot snapshot = host.MeasureAndArrange(box);
                        host.DrawChecked(new Rect(0f, 0f, box.x, box.y));

                        string where = "V4 footer " + language + ", " + state + " at " + box.x + "x" + box.y;
                        Assert(snapshot.RectById.TryGetValue("footer", out Rect footer),
                            where + ": the footer must be arranged");
                        float half = Math.Max(1f, footer.width * 0.5f - 10f);
                        float needed = Math.Max(
                            metrics.MeasureText(LongDevIdentity, UiFont.Tiny, half),
                            metrics.MeasureText(Program.ReadKeyedTable(language)["US.Footer.SaveStatus.Idle"], UiFont.Tiny, half)) + 6f;
                        Assert(footer.height + 0.01f >= Math.Max(needed, 28f),
                            where + ": the footer band must fit the identity it draws (need " + Num(needed)
                            + ", have " + Num(footer.height) + ")");
                        Assert(reports.Count == 0,
                            where + ": the long identity must not trip the fit audit, got " + Describe(reports));
                        Assert(host.Bindings.Get<string>("build-identity") == LongDevIdentity,
                            where + ": the FULL identity must still reach the footer binding (no truncation)");

                        // BH1 moved the single help switch INTO this band: the composite keeps the leftover
                        // beside the switch's declared width, so the identity above is measured in the width
                        // the band actually hands it - never across the whole page.
                        UiElementSpec footerBandSpec = FindAnywhere(host.Manifest, "footer-band")
                            ?? throw new InvalidOperationException("the manifest must declare the footer band");
                        UiElementSpec toggleSpec = FindAnywhere(host.Manifest, "help-toggle")
                            ?? throw new InvalidOperationException("the manifest must declare the help switch");
                        Assert(string.Equals(footerBandSpec.Kind, "Row", StringComparison.Ordinal),
                            where + ": BH1 declares the footer band as a ROW");
                        Assert(snapshot.RectById.TryGetValue("footer-band", out Rect footerBand),
                            where + ": the footer band must be arranged");
                        Assert(snapshot.RectById.TryGetValue("help-toggle", out Rect toggleRect),
                            where + ": the help switch must be arranged inside the footer band");
                        float bandPad = DeclaredFloat(footerBandSpec, "Padding");
                        Assert(Math.Abs(toggleRect.width - DeclaredFloat(toggleSpec, "Width")) <= 0.5f
                                && Math.Abs(footer.width + DeclaredFloat(footerBandSpec, "Gap")
                                    + toggleRect.width - (footerBand.width - 2f * bandPad)) <= 0.5f,
                            where + ": the band's INNER width (less the SA1.4 2x padding) must be exactly the"
                            + " us/footer composite plus the gap and the declared-width switch - footer "
                            + Describe(footer) + ", switch " + Describe(toggleRect) + ", band "
                            + Describe(footerBand) + ", padding " + bandPad);
                        Assert(toggleRect.y >= footerBand.y - 0.5f && toggleRect.yMax <= footerBand.yMax + 0.5f,
                            where + ": the help switch must be arranged inside its band's height");
                        // SA1.4: the switch stops short of the band's right edge by the declared padding - the
                        // container-edge merge the user reported is the gap this clause pins.
                        Assert(Math.Abs((footerBand.xMax - toggleRect.xMax) - bandPad) <= 0.5f
                                && bandPad > 0f,
                            where + ": the help switch must clear the band's right edge by the SA1.4 padding, got"
                            + " inset " + Num(footerBand.xMax - toggleRect.xMax) + " vs padding " + bandPad);

                        Console.WriteLine("[v4-footer] " + where + " footer=" + Describe(footer)
                            + " half=" + Num(half) + " identity='" + host.Bindings.Get<string>("build-identity")
                            + "' fit=" + reports.Count);
                    }
                    finally
                    {
                        UiFitAudit.Detach();
                        UiFitAudit.Enabled = false;
                    }
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    /// <summary>
    /// V4.5 completeness: the stable surface of the four V4 areas is still declared by VALUE (a rename or a
    /// dropped binding reddens here by name), and every key those elements use still resolves in BOTH
    /// language tables.
    /// </summary>
    private static void CompletenessOfTheV4Surface()
    {
        var pins = new (string Id, string Attribute, string Value)[]
        {
            // Distance (V4.1).
            ("attenuation-editor", "Tab", "Distance"),
            ("attenuation-header", "TitleKey", "US.Section.DistanceAttenuation"),
            ("attenuation-header", "HelpKey", "us/attenuation-editor"),
            ("attenuation-chart", "Bind", "attenuation-points"),
            ("attenuation-chart", "ActionBind", "attenuation-point"),
            ("attenuation-status", "Bind", "attenuation-status"),
            ("attenuation-preset-conservative", "ActionBind", "set-distance-preset"),
            ("attenuation-preset-balanced", "ActionBind", "set-distance-preset"),
            ("attenuation-preset-strong", "ActionBind", "set-distance-preset"),
            // Presets (V4.2).
            ("preset-list", "Kind", "us/preset-list"),
            ("preset-list", "TitleKey", "US.Section.DefBaselines"),
            ("preset-list", "HelpKey", "us/preset-list"),
            ("preset-list", "Tab", "Presets"),
            // Help (V4.3), re-cut by BH1: ONE declared presentation - the bottom panel, gated by the player's
            // own help-open value and holding a declared HEIGHT reservation instead of a column width. The
            // band clause below used to pin the second, defensive full-width presentation ("help-band",
            // VisibleKey="help-open-narrow"); that element is retired, so the same intent is pinned as the
            // negative inventory just after this loop.
            ("help-scroll", "Kind", "Scroll"),
            ("help-scroll", "VisibleKey", "help-open"),
            ("help-scroll", "Height", "140"),
            ("help-panel", "Kind", "us/help-panel"),
            // Footer (V4.4) - BH1 moved the single help switch into this band.
            ("footer", "Kind", "us/footer"),
            ("footer-band", "Kind", "Row"),
            ("help-toggle", "Kind", "input/button"),
            ("help-toggle", "ActionBind", "toggle-help-drawer"),
        };

        var requiredKeys = new List<string>();
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        foreach ((string id, string attribute, string value) in pins)
        {
            UiElementSpec element = FindAnywhere(host.Manifest, id)
                ?? throw new InvalidOperationException("the V4 surface lost the element '" + id + "'");
            string actual = string.Equals(attribute, "Kind", StringComparison.Ordinal)
                ? element.Kind
                : (element.TryGetAttribute(attribute, out string raw) ? raw : "");
            Assert(string.Equals(actual, value, StringComparison.Ordinal),
                "the V4 surface changed '" + id + "'." + attribute + ": expected '" + value + "', got '" + actual + "'");
            if ((attribute == "TextKey" || attribute == "TitleKey") && !string.IsNullOrEmpty(actual)) requiredKeys.Add(actual);
        }

        // The page must declare exactly ONE help surface: the Scroll under the STABLE id 'help-scroll'
        // pinned above, and nothing else. The defensive full-width band and its second panel instance were
        // retired with the two-presentation machinery, so those ids must be gone from the definition - a
        // reappearing duplicate is the band swap coming back, not a rename of the surviving element.
        foreach (string retired in new[] { "help-band", "help-panel-narrow" })
        {
            Assert(FindAnywhere(host.Manifest, retired) == null,
                "BH1 retired the '" + retired + "' element; the page must declare exactly one help surface");
        }

        // Every key the four areas declare must resolve in both shipped tables (a key that exists in only
        // one language is the parity defect the zero-Verse gate catches globally; this pins the V4 subset).
        foreach (string table in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> keys = Program.ReadKeyedTable(table);
            foreach (string key in requiredKeys)
            {
                Assert(keys.ContainsKey(key), table + ": the V4 surface key '" + key + "' must resolve");
            }
        }

        Console.WriteLine("[v4-completeness] " + pins.Length + " by-value pins over the four V4 areas; "
            + requiredKeys.Count + " keys resolve in both tables: " + string.Join(", ", requiredKeys));
    }

    private sealed class RecordingMetrics : ITextMetrics
    {
        private readonly Program.StubMetrics inner = new();
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);
        public bool Seen(string text) => seen.Contains(text);
        public void Clear() => seen.Clear();
        public float MeasureText(string text, UiFont font, float width)
        { seen.Add(text); return inner.MeasureText(text, font, width); }
        public float MeasureWidth(string text, UiFont font)
        { seen.Add(text); return inner.MeasureWidth(text, font); }
    }

    private static void FooterVisibilityUsesTheProductionBinding()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            var table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            var metrics = new RecordingMetrics();
            var reports = new List<UiOverflowReport>();
            UiFitAudit.Attach(metrics, reports.Add);
            UiFitAudit.Enabled = true;
            try
            {
                foreach ((string state, bool visible, bool dirty) in new[] {
                    ("Idle", false, false), ("Saving", true, false), ("Saved", true, false),
                    ("Failed", true, false), ("Unknown", true, false), ("Idle", false, true) })
                {
                    var source = new RecordingSettingsSource { RichData = true, SaveStatusOverride = state,
                        SaveStatusVisibleOverride = visible, IsDirtyOverride = dirty };
                    using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                    host.Bindings.Set("help-open", true);
                    host.MeasureAndArrange(PageBox);
                    metrics.Clear();
                    reports.Clear();
                    UiFitAudit.Reset();
                    host.DrawChecked(new Rect(0f, 0f, PageBox.x, PageBox.y));
                    string expected = (state == "Saving" || dirty ? "● " : "") + table["US.Footer.SaveStatus." + state];
                    if (dirty) Assert(metrics.Seen(expected), language + ": footer draw must preserve the dirty-only status");
                    Assert(metrics.Seen(expected) == (source.SaveStatusVisible || source.IsDirty),
                        language + ": footer draw must follow visibility for " + state);
                    Assert(metrics.Seen(source.BuildIdentity), language + ": full build identity remains readable");
                    Assert(reports.Count == 0, language + ": hidden/visible/dirty-only footer must keep fit=0");
                }
                Console.WriteLine("[v4-status] " + language + " hidden clean Idle; visible Saving/Saved/Failed/Unknown and dirty-only marker; full identity retained; fit=0");
            }
            finally { UiFitAudit.Detach(); UiFitAudit.Enabled = false; Program.SetTranslatorResolver(null); }
        }
    }

    /// <summary>
    /// BH1's cross-state clause, asserted on the host the caller is measuring: the window policy has ONE width
    /// function and the help state is not an input to it, so both help states must be arranged in the SAME
    /// page box; the bottom panel must appear exactly in the open pass; the open pass must still arrange the
    /// body row, the nav column and the centre scroll; and closing the panel must hand its WHOLE reservation
    /// (its declared height plus the page gap it no longer pays) back to the body. Numbers come from the
    /// manifest and the policy, never from a re-declaration here. The caller's help state is restored.
    /// </summary>
    private static void AssertHelpPanelReservesHeightNotWidth(UiHost host, Vector2 box, string where)
    {
        UiElementSpec pageRoot = host.Manifest.Roots[0];
        Assert(string.Equals(pageRoot.Id, "page-root", StringComparison.Ordinal),
            where + ": the page's band column must be 'page-root', got '" + pageRoot.Id + "'");
        UiElementSpec panel = Find(pageRoot, "help-scroll")
            ?? throw new InvalidOperationException("the manifest must declare the bottom help panel 'help-scroll'");
        float panelHeight = DeclaredFloat(panel, "Height");
        float pageGap = DeclaredFloat(pageRoot, "Gap");
        float pagePadding = DeclaredFloat(pageRoot, "Padding");
        float policyPageWidth = WindowChromeLayout.SettingsWindowWidth(MinimumScreenWidth, MinimumScreenHeight)
            - WindowChromeLayout.WindowChromeInset;
        bool state = host.Bindings.Get<bool>("help-open");

        host.Bindings.Set("help-open", true);
        UiLayoutSnapshot open = host.MeasureAndArrange(box);
        host.Bindings.Set("help-open", false);
        UiLayoutSnapshot closed = host.MeasureAndArrange(box);
        host.Bindings.Set("help-open", state);

        Assert(open.RectById.TryGetValue("page-root", out Rect openPage),
            where + ": the open pass must arrange the page root");
        Assert(closed.RectById.TryGetValue("page-root", out Rect closedPage),
            where + ": the closed pass must arrange the page root");
        Assert(Math.Abs(openPage.width - policyPageWidth) <= 0.5f
                && Math.Abs(closedPage.width - policyPageWidth) <= 0.5f
                && Math.Abs(openPage.x - closedPage.x) <= 0.5f
                && Math.Abs(openPage.y - closedPage.y) <= 0.5f
                && Math.Abs(openPage.width - closedPage.width) <= 0.5f
                && Math.Abs(openPage.height - closedPage.height) <= 0.5f,
            where + ": BH1 leaves BOTH help states the same policy page box " + Num(policyPageWidth) + "x"
            + Num(box.y) + " - open " + Describe(openPage) + ", closed " + Describe(closedPage));

        Assert(open.Viewports.TryGetValue("help-scroll", out Rect helpRect),
            where + ": the open pass must arrange the bottom help panel");
        Assert(!closed.Viewports.ContainsKey("help-scroll"),
            where + ": the closed pass must not arrange the bottom help panel");
        Assert(Math.Abs(helpRect.height - panelHeight) <= 0.5f
                && Math.Abs(helpRect.width - (openPage.width - pagePadding * 2f)) <= 0.5f,
            where + ": the panel must hold its declared reservation across the page's inner width, got "
            + Describe(helpRect) + " (declared Height " + Num(panelHeight) + ", inner width "
            + Num(openPage.width - pagePadding * 2f) + ")");

        Assert(open.RectById.TryGetValue("body-row", out Rect openBody),
            where + ": the open pass must arrange the body row");
        Assert(closed.RectById.TryGetValue("body-row", out Rect closedBody),
            where + ": the closed pass must arrange the body row");
        Assert(open.RectById.ContainsKey("nav-column") && open.Viewports.ContainsKey("content-scroll")
                && closed.RectById.ContainsKey("nav-column") && closed.Viewports.ContainsKey("content-scroll"),
            where + ": the body row, the nav column and the centre scroll must stay arranged in BOTH help states");
        Assert(Math.Abs(closedBody.height - (openBody.height + panelHeight + pageGap)) <= 0.5f,
            where + ": closing the panel must hand its whole reservation back to the body row (open "
            + Num(openBody.height) + " + panel " + Num(panelHeight) + " + gap " + Num(pageGap) + " vs closed "
            + Num(closedBody.height) + ")");
        Assert(Math.Abs(openBody.width - closedBody.width) <= 0.01f
                && Math.Abs(open.Viewports["content-scroll"].width
                    - closed.Viewports["content-scroll"].width) <= 0.01f,
            where + ": the help panel may cost the settings no width at all (body " + Num(openBody.width)
            + " vs " + Num(closedBody.width) + ", centre " + Num(open.Viewports["content-scroll"].width)
            + " vs " + Num(closed.Viewports["content-scroll"].width) + ")");
    }

    /// <summary>One declared numeric attribute, so a lane never re-spells a manifest number.</summary>
    private static float DeclaredFloat(UiElementSpec spec, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw))
        {
            throw new InvalidOperationException("'" + spec.Id + "' must declare " + attribute);
        }

        return float.Parse(raw.Trim(), CultureInfo.InvariantCulture);
    }

    private static UiElementSpec? Find(UiElementSpec root, string id)
    {
        if (string.Equals(root.Id, id, StringComparison.Ordinal)) return root;
        foreach (UiElementSpec child in root.Children)
        {
            UiElementSpec? found = Find(child, id);
            if (found != null) return found;
        }

        return null;
    }

    private static UiElementSpec? FindAnywhere(UiLayoutManifest manifest, string id)
    {
        foreach (UiElementSpec root in manifest.Roots)
        {
            UiElementSpec? found = Find(root, id);
            if (found != null) return found;
        }

        foreach (UiElementSpec template in manifest.Templates.Values)
        {
            UiElementSpec? found = Find(template, id);
            if (found != null) return found;
        }

        return null;
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        if (reports.Count == 0) return "none";
        var text = new System.Text.StringBuilder();
        foreach (UiOverflowReport report in reports)
        {
            text.Append("[").Append(report.ElementPath).Append(" ").Append(report.Axis)
                .Append(" needs ").Append(report.Needed).Append(" has ").Append(report.Available).Append("]");
        }

        return text.ToString();
    }

    private static string Describe(Rect rect)
    {
        return "(" + Num(rect.x) + "," + Num(rect.y) + "," + Num(rect.width) + "," + Num(rect.height) + ")";
    }

    private static string Num(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("V4RemainingLaneTests step failed: " + name, ex);
        }
    }

    public static int RunAll()
    {
        Step("V4.1/V4.2: Distance and Presets at the real box in both help states, EN and ZH",
            DistanceAndPresetsAtTheRealBoxes);
        Step("V4.4: the footer keeps the status and the FULL identity at the real box in both help states",
            FooterCarriesStatusAndTheFullIdentity);
        Step("V4.4: the footer follows actual status visibility", FooterVisibilityUsesTheProductionBinding);
        Step("V4.5: the V4 stable surface is complete by value and resolves in both languages", CompletenessOfTheV4Surface);
        Console.WriteLine("V4RemainingLaneTests ALL PASS");
        return 0;
    }
}
