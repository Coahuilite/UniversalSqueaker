using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
namespace UniversalSqueaker.KernelHostTests;
/// <summary>Surviving navigation, single-click and permission-label geometry counterexamples.
/// Old uncalled mood-multiplier controls are retired; response configuration is tested through its actual fold.</summary>
internal static class MoodLayoutFocusedTests
{
    private const float ViewportWidth=800, ViewportHeight=600, Epsilon=.5f, V3PageBoxWidth=760, V3PageBoxHeight=524, RectMatchEpsilon=.01f;
    private sealed class CapturedRects { public List<Rect> Buttons { get; }=new(); public List<Rect> Sliders { get; }=new(); public List<Rect> TextFields { get; }=new(); }
    private static FieldInfo ButtonOverrideField => RequireField("ButtonOverride", typeof(Func<Rect,bool>));
    private static FieldInfo SliderOverrideField => RequireField("SliderOverride", typeof(Func<Rect,float,float,float,float>));
    private static FieldInfo TextFieldOverrideField => RequireField("TextFieldOverride", typeof(Func<Rect,string,string>));
    private static FieldInfo DebugMousePositionField => RequireField("DebugMousePosition", typeof(Vector2));
    private static FieldInfo DebugMousePositionEnabledField => RequireField("DebugMousePositionEnabled", typeof(bool));
    public static int RunAll()
    {
        // S4 retires the old mood/probability editor policies, not navigation or action permissions.
        // Current retired-editor geometry is covered once in S4RetiredPolicyUiTests.
        Step("a popup-covered nav control yields the click", CoveredControlYieldsTheClick);
        Step("every nav card paints its own enclosure (V1)", EveryNavCardPaintsItsOwnEnclosure);
        Step("a checkbox-row press is decided by one control and flips the value once", CheckboxRowPressDecidesOnce);
        Step("resolved action labels grow their live permission rows", ResolvedActionLabelGrowsTheRow);        Console.WriteLine("MoodLayoutFocusedTests ALL PASS");
        return 0;
    }
    private static void CheckboxRowPressDecidesOnce()
    {
        const float Width = ViewportWidth;
        const float Height = 900f;
        // Three independent Eat dropdowns replace the retired dependent checkbox pair.
        var source = new RecordingSettingsSource { RichData = true };
        UiHost host = UsKernelSettingsHost.Create(source);
        try
        {
            host.Bindings.Invoke("set-tab", "Tuning"); // UIR-001: the basic-tuning card is a rules-band control now
            host.MeasureAndArrange(new Vector2(Width, Height));
            Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(Width, Height));
            Assert(snapshot.RectById.TryGetValue("basic-tuning", out Rect card),
                "the Tuning workspace must hold the basic-tuning card");

            var raw = new CapturedRects();
            try
            {
                SetButtonOverride(rect => { raw.Buttons.Add(rect); return false; });
                host.DrawChecked(new Rect(0f, 0f, Width, Height));
            }
            finally
            {
                ClearOverrides();
            }

            // RECT SPACES, and this is not cosmetic: the card's rect from the snapshot is PAGE space while
            // the button rects recorded above are in the scroll's LOCAL space (the engine's ToDrawRect
            // subtracts the scroll container's own rect position for its children). Filtering across the two
            // is a coordinate-space bug that happens to work until a layout shift moves the boundaries -
            // which the S3 header band did, and this lane then selected a LATER section's checkbox row as
            // "the first slot inside the card" and failed on the egg write. Translate the card into the
            // controls' space first; ordering by y then really is the widget's own draw order.
            Rect scrollViewport = snapshot.Viewports["content-scroll"];
            var cardLocal = new Rect(
                card.x - scrollViewport.x,
                card.y - scrollViewport.y,
                card.width,
                card.height);

            List<Rect> slots = raw.Buttons
                .Where(r => IsInside(r, cardLocal) && IsDeclaredCheckbox(r))
                .OrderBy(r => r.y)
                .ToList();
            Assert(slots.Count == 2,
                "the current card draws two boolean rows (egg + talking; Eat and Select have independent dropdowns)"
                + " got " + slots.Count);

            // THE DRAW FACT, stated as the property itself: for every declared checkbox band, that band is
            // the ONLY recorded hit surface that intersects it. Nothing is assumed about which band belongs
            // to which row - a page that adds a row-wide hit area behind a checkbox fails here whichever row
            // it is, because IMGUI hands one press to every hit rect it overlaps.
            //
            // The assertion is per-band rather than "everything inside the card rect" on purpose: the
            // recorded rects live in scroll-CONTENT space while the card rect comes from the page-space
            // snapshot, and trying to filter foreign controls by that translation is the coordinate-space
            // bug this suite already paid for once.
            for (int i = 0; i < slots.Count; i++)
            {
                int overlapping = raw.Buttons.Count(r => Overlaps(r, slots[i]));
                Assert(overlapping == 1,
                    "a declared checkbox band must be its row's only hit surface; band " + slots[i]
                    + " intersects " + overlapping + " recorded surfaces. A second band behind a checkbox is"
                    + " the double-toggle IMGUI hands to both controls");
                for (int j = i + 1; j < slots.Count; j++)
                {
                    Assert(!Overlaps(slots[i], slots[j]),
                        "two declared checkbox bands must not overlap: " + slots[i] + " vs " + slots[j]);
                }
            }

            // THE BEHAVIOUR FACT. The egg row is the first control row in the manifest, and that it really
            // is the egg row is verified by what the press does rather than assumed: if it were another row,
            // allow-eggs would not move and the lane fails with a name.
            Rect slot = slots[0];
            bool? before = source.LastEasterEggs;
            int revisionBefore = host.Session.ContentRevision;
            try
            {
                SetButtonOverride(rect => RectMatches(rect, slot));
                host.DrawChecked(new Rect(0f, 0f, Width, Height));
            }
            finally
            {
                ClearOverrides();
            }

            Assert(source.LastEasterEggs != before, "a press on the declared checkbox must flip the value exactly once");
            Assert(host.Session.ContentRevision == revisionBefore + 1,
                "and it must be exactly one write (revision " + revisionBefore + " -> " + host.Session.ContentRevision + ")");

            DbEatUiTests.CheckBindings(host, source);
        }
        finally
        {
            host.Dispose();
        }
    }
    private static bool IsDeclaredCheckbox(Rect rect)
    {
        return Math.Abs(rect.width - 36f) <= 0.5f && Math.Abs(rect.height - 30f) <= 0.5f;
    }
    private static Vector2 Centre(Rect rect) => new(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("MoodLayoutFocusedTests step failed: " + name, ex);
        }
    }
    private static void CoveredControlYieldsTheClick()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        const string xml = "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
            + "<Column Id=\"probe-column\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"probe-dropdown\" Kind=\"input/dropdown\" OptionsBind=\"probe-options\" Height=\"28\" />"
            + "<Widget Id=\"probe-nav\" Kind=\"us/nav\" />"
            + "</Column></UiPage>";
        string activeTab = "Tuning", selected = "one";
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => activeTab, value => activeTab = value);
        bindings.BindAction<string>("set-tab", value => activeTab = value);
        bindings.BindValue("probe-dropdown", () => selected, value => selected = value);
        bindings.BindOptions("probe-options", () => new[] { "one", "two", "three", "four" });
        using UiHost host = new(UsKernelWidgetRegistrar.Scope, UiLayoutManifest.Parse(xml), bindings,
            UsTheme.Surface(), new Program.StubMetrics(), new PopupProbeTranslation());
        Rect viewport = new(0f, 0f, 200f, 600f);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        var navRects = new List<Rect>();
        try
        {
            SetButtonOverride(rect =>
            {
                if (rect.width >= 100f && rect.height >= 30f) navRects.Add(rect);
                return false;
            });
            host.DrawChecked(viewport);
        }
        finally { ClearOverrides(); }
        Assert(navRects.Count == 5, "the fixture must capture exactly the five production nav rows");
        Rect target = navRects.OrderBy(r => r.y).First();
        host.Session.OpenPopup("probe-dropdown", snapshot.RectById["probe-dropdown"]);
        host.DrawChecked(viewport);
        host.DrawChecked(viewport);
        Assert(TryGetPopupHitLayerFromHost(host, out UiHitLayer layer), "the real dropdown must publish a popup");
        Vector2 pointerWindow = new(target.x + target.width / 2f, target.y + target.height / 2f);
        Assert(layer.Rect.x <= pointerWindow.x && pointerWindow.x <= layer.Rect.xMax
            && layer.Rect.y <= pointerWindow.y && pointerWindow.y <= layer.Rect.yMax,
            "the actual popup must cover the first nav row");
        try
        {
            SetMousePosition(pointerWindow);
            SetButtonOverride(rect => RectMatches(rect, target));
            host.DrawChecked(viewport);
            Assert(activeTab == "Tuning", "a popup-covered nav control must not take the click (ctx overload)");
            host.Session.ClosePopup();
            host.DrawChecked(viewport);
            Assert(activeTab == "Overview", "without the popup the same click must reach the nav row");
        }
        finally
        {
            ClearMousePosition();
            ClearOverrides();
            host.Session.ClosePopup();
        }
    }
    private static void ResolvedActionLabelGrowsTheRow()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            var table = Program.ReadKeyedTable(language);
            // One draw per resolved label, returning the ARRANGED card so the growth is asserted against the
            // body this box really gives and never against a restated width.
            (float CardWidth, float CardHeight) Draw(string draft)
            {
                var translated = new Dictionary<string, string>(table, StringComparer.Ordinal) { ["US.Action.Draft"] = draft };
                Program.SetTranslatorResolver(translated);
                // Labels are now resolved by the production read-model, not inside the widget.
                // Feed that real projection instead of the recording fake's fixed English labels.
                var projection = typeof(VoicePacksPageModel).GetMethod("BuildActionScopes", BindingFlags.NonPublic | BindingFlags.Static)!;
                var source = new RecordingSettingsSource {
                    RichData = true,
                    TuningActionScopes = ((IReadOnlyList<ActionScopeRowView>)projection.Invoke(null,
                        new object[] {new GameSettings(), 0, "", ""})!).ToArray()
                };
                using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
                host.Bindings.Invoke("set-tab", "Tuning");
                // VF1 three-area: the SCOPE rows live in the action area; the mood-area pin hides them.
                host.Bindings.Set("tuning-area", 0);
                host.Bindings.Set("help-open", true);
                Rect card = host.MeasureAndArrange(new Vector2(V3PageBoxWidth, V3PageBoxHeight)).RectById["scope-tree"];
                return (card.width, card.height);
            }
            try
            {
                (float cardWidth, float ordinary) = Draw(table["US.Action.Draft"]);
                string longer = string.Concat(Enumerable.Repeat(language == "English" ? "Long drafted action " : "很长的征召动作名称", 20));
                (float longCardWidth, float expanded) = Draw(longer);
                var ruler = new Program.StubMetrics();
                float oneLine = ruler.MeasureText("x", UiFont.Small, 100000f);
                float body = cardWidth - UsCardLayout.Padding * 2f;
                string hint = "→ " + table["US.Tuning.Scope.Command"];
                float predicted = UsScopeTreeWidget.ScopeRowLayoutFor(body, longer, hint, ruler).RowHeight
                    - UsScopeTreeWidget.ScopeRowLayoutFor(body, table["US.Action.Draft"], hint, ruler).RowHeight;
                // BH1 re-cut of the old flat 100px floor: that number was reachable ONLY because the retired
                // 320px side column left the row a 392 body - the same Chinese label that wrapped into eight
                // lines there wraps into five at the 524 centre column. The business claim is unchanged (the
                // RESOLVED label wraps and a key never does), so it is stated in calibrated text lines read
                // off the arrangement: the card must gain at least one wrapped line, and it must gain what the
                // production row-layout helper predicts at the body this box measures, less one line of seam
                // slack. A narrower drawn row than the probed body only ever ADDS lines, never removes them.
                Assert(Math.Abs(longCardWidth - cardWidth) <= 0.01f,
                    language + ": BH1 - the resolved label changes no width, got " + Num(cardWidth)
                    + " -> " + Num(longCardWidth));
                Assert(expanded > ordinary + oneLine - 0.01f,
                    language + ": resolved long action label must grow the production row by at least one"
                    + " wrapped text line (" + Num(oneLine) + "), key-only measurement cannot; got "
                    + Num(ordinary) + " -> " + Num(expanded));
                Assert(expanded >= ordinary + predicted - oneLine,
                    language + ": the drawn growth " + Num(expanded - ordinary) + " must follow the row layout"
                    + " the production helper predicts at the measured body " + Num(body) + " (" + Num(predicted) + ")");
                Console.WriteLine("[v3-resolved-row] " + language + " card=" + Num(cardWidth) + " body=" + Num(body)
                    + " normal=" + Num(ordinary) + " long=" + Num(expanded) + " predictedWrapGrowth=" + Num(predicted));
            }
            finally { Program.SetTranslatorResolver(null); }
        }
    }
    private static string Num(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    private static void EveryNavCardPaintsItsOwnEnclosure()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        const string xml = "<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\">"
            + "<Styles Schema=\"1\" Density=\"regular\">"
            + "<Scheme Name=\"us-flat-panel\">"
            + "<Palette Role=\"WorkspacePlane\" Value=\"#191612\" />"
            + "<Palette Role=\"Raised\" Value=\"#191612\" />"
            + "<Palette Role=\"RaisedBorder\" Value=\"#191612\" />"
            + "<Palette Role=\"Hover\" Value=\"#242019\" />"
            + "<Palette Role=\"HoverBorder\" Value=\"#242019\" />"
            + "<Palette Role=\"Selected\" Value=\"#3a311f\" />"
            + "<Palette Role=\"SelectedBorder\" Value=\"#3a311f\" />"
            + "</Scheme></Styles>"
            + "<Column Id=\"probe-column\" Padding=\"0\" Gap=\"0\" Scheme=\"us-flat-panel\">"
            + "<Widget Id=\"probe-nav\" Kind=\"us/nav\" />"
            + "</Column></UiPage>";
        string activeTab = "Tuning";
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => activeTab, value => activeTab = value);
        bindings.BindAction<string>("set-tab", value => activeTab = value);
        using UiHost host = new(UsKernelWidgetRegistrar.Scope, UiLayoutManifest.Parse(xml), bindings,
            UsTheme.Surface(), new Program.StubMetrics(), new PopupProbeTranslation());
        Rect viewport = new(0f, 0f, 200f, 600f);
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));

        IList rects = RecordedStub("DrawBoxSolidRects");
        IList colours = RecordedStub("DrawBoxSolidColors");
        rects.Clear();
        colours.Clear();
        var cards = new List<Rect>();
        try
        {
            SetButtonOverride(rect =>
            {
                if (rect.width >= 100f && rect.height >= 30f) cards.Add(rect);
                return false;
            });
            host.DrawChecked(viewport);
        }
        finally
        {
            ClearOverrides();
        }

        Assert(cards.Count == 5, "the probe must capture exactly the five nav cards, got " + cards.Count);
        cards = cards.OrderBy(r => r.y).ToList();

        Assert(rects.Count == colours.Count,
            "the stub's two solid recorders must stay in step: " + rects.Count + " rects vs " + colours.Count);

        // The SELECTED card is the one whose fill is the palette's Selected tone; the ordinary ones take the
        // raised plane. Asking which enclosures were painted is the whole assertion: the pre-V1 neutral path
        // inherited an invisible frame, so an ordinary card painted no edge at all.
        var enclosures = new List<Color>();
        for (int i = 0; i < cards.Count; i++)
        {
            Color? edge = EdgeOf(cards[i], rects, colours);
            Assert(edge.HasValue,
                "EveryCardIsEnclosed: nav card " + i + " painted no enclosing surface at "
                + cards[i].x + "," + cards[i].y + " " + cards[i].width + "x" + cards[i].height);
            enclosures.Add(edge!.Value);
        }

        List<Color> distinct = enclosures.Distinct().ToList();
        Assert(distinct.Count == 2,
            "SelectedAndOrdinaryDiffer: the five cards must paint exactly two enclosures - the selected tone"
            + " and the ordinary one - got " + distinct.Count + " (" + string.Join(", ",
                distinct.Select(c => "#" + Channel(c.r) + Channel(c.g) + Channel(c.b))) + ")");
    }
    private static Color? EdgeOf(Rect card, IList rects, IList colours)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i] is not Rect rect || !RectMatches(rect, card)) continue;
            if (colours[i] is not Color fill) continue;

            for (int j = 0; j < rects.Count; j++)
            {
                if (rects[j] is not Rect strip || colours[j] is not Color edge) continue;
                bool isCardEdge = Math.Abs(strip.x - card.x) <= 0.01f
                    && Math.Abs(strip.y - card.y) <= 0.01f
                    && Math.Abs(strip.height - 1f) <= 0.01f
                    && Math.Abs(strip.width - card.width) <= 0.01f;
                if (!isCardEdge) continue;
                if (!SameColour(edge, fill)) return edge;
            }
        }

        return null;
    }
    private static bool SameColour(Color a, Color b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
    private static string Channel(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 255f).ToString("X2");
    }
    private static IList RecordedStub(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record '" + fieldName + "'; this lane cannot observe a draw and"
                + " must not pass silently");
        }

        return field.GetValue(null) as IList
            ?? throw new InvalidOperationException("the stub's '" + fieldName + "' recorder is not a list");
    }
    private static bool TryGetPopupHitLayerFromHost(UiHost host, out UiHitLayer layer)
    {
        layer = default;
        foreach (UiHitLayer candidate in Fl08PublicObservation.Popup(host.Session, out var observedPopup) ? new[] { observedPopup } : Array.Empty<UiHitLayer>())
        {
            if (candidate.IsPopup)
            {
                layer = candidate;
                return true;
            }
        }
        return false;
    }
    private static void SetMousePosition(Vector2 position)
    {
        SetField(DebugMousePositionField, position);
        SetField(DebugMousePositionEnabledField, true);
    }
    private static void ClearMousePosition()
    {
        SetField(DebugMousePositionEnabledField, false);
    }
    private static bool RectMatches(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) <= RectMatchEpsilon
            && Math.Abs(a.y - b.y) <= RectMatchEpsilon
            && Math.Abs(a.width - b.width) <= RectMatchEpsilon
            && Math.Abs(a.height - b.height) <= RectMatchEpsilon;
    }
    private static bool Overlaps(Rect a, Rect b)
    {
        return a.x < b.xMax - Epsilon
            && b.x < a.xMax - Epsilon
            && a.y < b.yMax - Epsilon
            && b.y < a.yMax - Epsilon;
    }
    private static bool IsInside(Rect inner, Rect outer)
    {
        return inner.x >= outer.x - Epsilon
            && inner.y >= outer.y - Epsilon
            && inner.xMax <= outer.xMax + Epsilon
            && inner.yMax <= outer.yMax + Epsilon;
    }
    private static void SetButtonOverride(Func<Rect, bool> value)
    {
        SetField(ButtonOverrideField, value);
    }
    private static void ClearOverrides()
    {
        SetField(ButtonOverrideField, null);
        SetField(SliderOverrideField, null);
        SetField(TextFieldOverrideField, null);
    }
    private static void SetField(FieldInfo field, object? value)
    {
        try
        {
            field.SetValue(null, value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: cannot set UiNative." + field.Name + " on net472. "
                + "Add InternalsVisibleTo(\"UniversalSqueakerKernelHostTests\") in "
                + "Source/FerriteLib.UiKit/Properties/AssemblyInfo.cs, rebuild, then rerun. "
                + "Do not modify production files in this step.",
                ex);
        }
    }
    private static FieldInfo RequireField(string name, Type fieldType)
    {
        FieldInfo? field = typeof(UiNative).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: UiNative." + name + " is not accessible via reflection on net472. "
                + "Add InternalsVisibleTo(\"UniversalSqueakerKernelHostTests\") in "
                + "Source/FerriteLib.UiKit/Properties/AssemblyInfo.cs, rebuild, then rerun. "
                + "Do not modify production files in this step.");
        }

        if (field.FieldType != fieldType)
        {
            throw new InvalidOperationException(
                "UiNative." + name + " has unexpected type " + field.FieldType + "; expected " + fieldType);
        }

        return field;
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private sealed class PopupProbeTranslation : IUiTranslation
    {
        public string Translate(string key) => key;
        public int TranslationRevision => 0;
    }
    private sealed class RecordingMetrics : ITextMetrics
    {
        private readonly Program.StubMetrics inner = new();
        private readonly HashSet<string> measured = new(StringComparer.Ordinal);

        public bool Measured(string text) => measured.Contains(text ?? "");

        public float MeasureText(string text, UiFont font, float width)
        {
            string value = text ?? "";
            measured.Add(value);
            return inner.MeasureText(value, font, width);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            string value = text ?? "";
            measured.Add(value);
            return inner.MeasureWidth(value, font);
        }
    }
}
