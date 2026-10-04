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
/// workspace, the single context-help surface and the footer, measured at the two REAL configurations of a
/// 1024x768 screen - drawer OPEN (help-open really set, page box 984x524) and drawer RETRACTED (760x524) - in
/// EN and ZH, with production-shaped state.
///
/// Faithful reverts are recorded in the PM V4 evidence: restoring Small for preset rows reddens the
/// real-box fit clause; removing the axis caption reddens its arrangement clause and the baseline note its draw clause;
/// ignoring status visibility reddens the hidden-Idle clause. Completeness and existing help lifecycle
/// checks are GUARDs; a scroll container's presence alone does not prove overflowing content.
/// Removing the dirty-only predicate reddens its named marker clause; final source restores all five fixes.
/// </summary>
internal static class V4RemainingLaneTests
{
    private static readonly Vector2 OpenPageBox = new(984f, 524f);
    private static readonly Vector2 RetractedPageBox = new(760f, 524f);

    private const string LongDevIdentity =
        "dev-1a2b3c4d5e6f (0.4.0+1a2b3c4d5e6f778899aabbccddeeff0011223344)";

    /// <summary>
    /// V4.1/V4.2 at the real boxes: the Distance workspace keeps its chart, status and three preset
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
                             ("drawer open", OpenPageBox, true),
                             ("drawer retracted", RetractedPageBox, false),
                         })
                {
                    var metrics = new RecordingMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource { RichData = true, PresetLabel = null };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics, () => box.x);
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
                            where + ": the help column must be arranged exactly while the drawer is open");
                        Rect distanceViewport = distance.Viewports["content-scroll"];
                        bool distanceScrolls = distance.ScrollContents.TryGetValue("content-scroll", out Rect distanceContent)
                            && distanceContent.height > 0f && distanceViewport.height > 0f;
                        Assert(distanceScrolls,
                            where + ": Distance must have non-empty content and a live scroll viewport");
                        Assert(metrics.Seen(host.Bindings.Get<string>("attenuation-axis-caption")),
                            where + ": the camera-height and volume axes must be drawn, not only bound");
                        Assert(reports.Count == 0,
                            where + ": Distance must have no fit finding, got " + Describe(reports));

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
                        using UiHost longHost = UsKernelSettingsHost.Create(longSource, metrics, () => box.x);
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
                            + " fit=0 help=" + (helpOpen ? "open" : "retracted"));
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
    /// fit the longest form (the US_DEV form Mod.cs:132-143 builds) instead of clipping it, at both real
    /// boxes and in both languages. The SHIPPED downgrade is a build-configuration property, not a display
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
                             ("drawer open", OpenPageBox, true),
                             ("drawer retracted", RetractedPageBox, false),
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
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics, () => box.x);
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
            // Help (V4.3).
            ("help-scroll", "Width", "320"),
            ("help-scroll", "VisibleKey", "help-open-wide"),
            ("help-panel", "Kind", "us/help-panel"),
            ("help-band", "VisibleKey", "help-open-narrow"),
            // Footer (V4.4).
            ("footer", "Kind", "us/footer"),
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
                    using UiHost host = UsKernelSettingsHost.Create(source, metrics, () => OpenPageBox.x);
                    host.Bindings.Set("help-open", true);
                    host.MeasureAndArrange(OpenPageBox);
                    metrics.Clear();
                    reports.Clear();
                    UiFitAudit.Reset();
                    host.DrawChecked(new Rect(0f, 0f, OpenPageBox.x, OpenPageBox.y));
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
        Step("V4.1/V4.2: Distance and Presets at the real boxes, EN and ZH", DistanceAndPresetsAtTheRealBoxes);
        Step("V4.4: the footer keeps the status and the FULL identity at the real boxes", FooterCarriesStatusAndTheFullIdentity);
        Step("V4.4: the footer follows actual status visibility", FooterVisibilityUsesTheProductionBinding);
        Step("V4.5: the V4 stable surface is complete by value and resolves in both languages", CompletenessOfTheV4Surface);
        Console.WriteLine("V4RemainingLaneTests ALL PASS");
        return 0;
    }
}
