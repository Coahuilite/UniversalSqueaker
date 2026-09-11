using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// Focused geometry/interaction tests for the 800-wide three-column Mood layout.
///
/// The expected contract (already agreed with the geometry agent):
///  - at 800px the scope-tree card body is narrow (~288-304px), so each Mood row uses a STACKED layout;
///  - row label at top-left, Auto button at top-right, three full-width stepper lines (Pitch, Volume,
///    Jitter) stacked vertically;
///  - every stepper line still has slider + number field + minus/plus;
///  - the old narrow branch that omitted sliders/numbers/minus/plus must be gone;
///  - minus/plus, slider, number commit and Auto all still invoke typed "set-mood-tuning".
///
/// Rect capture strategy:
///  - the test host has no InternalsVisibleTo, so the internal static UiNative test seams are set by
///    reflection (ButtonOverride/SliderOverride/TextFieldOverride);
///  - during one recording DrawFrame the overrides record every control rect and return neutral values
///    (no clicks, no slider change, no text commit);
///  - snapshot.RectById["scope-tree"] is the page-space card outer rect; snapshot.Viewports and the
///    session scroll position convert scroll-content-local control rects back to page/window space.
/// </summary>
internal static class MoodLayoutFocusedTests
{
    private const float ViewportWidth = 800f;
    private const float ViewportHeight = 600f;
    private const float Epsilon = 0.5f;
    private const float RectMatchEpsilon = 0.01f;

    private static FieldInfo ButtonOverrideField => RequireField("ButtonOverride", typeof(Func<Rect, bool>));
    private static FieldInfo SliderOverrideField => RequireField("SliderOverride", typeof(Func<Rect, float, float, float, float>));
    private static FieldInfo TextFieldOverrideField => RequireField("TextFieldOverride", typeof(Func<Rect, string, string>));

    public static int RunAll()
    {
        Step("800px mood stacked geometry", NarrowMoodRowStackedGeometry);
        Step("minus click routes typed set-mood-tuning", MinusClickRoutesTypedMoodTuning);
        Step("plus click routes typed set-mood-tuning", PlusClickRoutesTypedMoodTuning);
        Step("slider change routes typed set-mood-tuning", SliderChangeRoutesTypedMoodTuning);
        Step("number commit routes typed set-mood-tuning", NumberCommitRoutesTypedMoodTuning);
        Step("auto button routes typed set-mood-tuning", AutoButtonRoutesTypedMoodTuning);
        Step("preset reset routes typed reset-mood-to-preset", PresetResetRoutesTypedMoodTuning);
        Step("reset controls route in all three mood layout modes", ResetRoutingAcrossLayoutModes);
        Step("a popup-covered reset control yields the click", CoveredControlYieldsTheClick);

        Console.WriteLine("MoodLayoutFocusedTests ALL PASS");
        return 0;
    }

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

    private static void NarrowMoodRowStackedGeometry()
    {
        using CaptureContext ctx = CreateCaptureContext();
        AssertMoodGeometry(ctx);
        AssertMoodRowStacking(ctx);
    }

    private static void MinusClickRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect minus = FirstMinusRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => RectMatches(rect, minus));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawFrame(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "minus click must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "minus click on the first stepper line must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 0.95f), "Pitch minus must step 1.0 down to 0.95");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void PlusClickRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect plus = FirstPlusRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => RectMatches(rect, plus));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawFrame(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "plus click must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "plus click on the first stepper line must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 1.05f), "Pitch plus must step 1.0 up to 1.05");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void SliderChangeRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect slider = FirstSliderRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => false);
            SetSliderOverride((rect, value, min, max) => RectMatches(rect, slider) ? 1.25f : value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawFrame(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "slider change must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "first slider must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 1.25f), "Pitch slider must write 1.25");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void NumberCommitRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect field = FirstFieldRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => false);
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => RectMatches(rect, field) ? "1.25" : text);
            ctx.Host.DrawFrame(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "number commit must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Pitch, "first number field must target Pitch");
            Assert(FloatEquals(ctx.Source.LastMoodValue, 1.25f), "Pitch number commit must write 1.25");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void AutoButtonRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        Rect auto = FirstAutoRect(ctx);
        ClearLastMood(ctx.Source);

        try
        {
            SetButtonOverride(rect => RectMatches(rect, auto));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawFrame(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMood == SqueakMood.Good, "Auto click must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodFactor == SqueakMoodFactor.Clear, "Auto click must write SqueakMoodFactor.Clear");
            Assert(!ctx.Source.LastMoodValue.HasValue, "Auto clear must write a null mood value");
        }
        finally
        {
            ClearOverrides();
            ClearLastMood(ctx.Source);
        }
    }

    private static void AssertMoodGeometry(CaptureContext ctx)
    {
        Assert(ctx.CardPageRect.width >= 260f && ctx.CardPageRect.width <= 340f,
            "scope-tree card must be a narrow card at 800px three-column layout (got width " + ctx.CardPageRect.width + ")");

        // The Tuning workspace should contain exactly the six mood sliders/fields (all inside the
        // scope-tree card). This also proves the old narrow branch (no sliders/fields/minus/plus) is gone.
        Assert(ctx.CapturedSliderCount == 6, "captured slider count must be 6 in Tuning at 800px (got " + ctx.CapturedSliderCount + ")");
        Assert(ctx.CapturedTextFieldCount == 6, "captured number-field count must be 6 in Tuning at 800px (got " + ctx.CapturedTextFieldCount + ")");
        Assert(ctx.Mood.Sliders.Count == 6, "each of 2 rich mood rows must have 3 sliders (got " + ctx.Mood.Sliders.Count + " total)");
        Assert(ctx.Mood.Fields.Count == 6, "each of 2 rich mood rows must have 3 number fields (got " + ctx.Mood.Fields.Count + " total)");
        Assert(ctx.Mood.SmallButtons.Count == 12, "each of 2 rich mood rows must have 6 minus/plus buttons (got " + ctx.Mood.SmallButtons.Count + " total)");
        Assert(ctx.Mood.AutoButtons.Count == 2, "each of 2 rich mood rows must have 1 \"reset to default\" control (got " + ctx.Mood.AutoButtons.Count + " total)");
        Assert(ctx.Mood.PresetButtons.Count == 2, "each of 2 rich mood rows must have 1 \"reset to preset\" control (got " + ctx.Mood.PresetButtons.Count + " total)");

        Assert(ctx.CapturedSliderCount == ctx.Mood.Sliders.Count,
            "all captured sliders must be inside the scope-tree card (no slider outside the card)");
        Assert(ctx.CapturedTextFieldCount == ctx.Mood.Fields.Count,
            "all captured number fields must be inside the scope-tree card (no field outside the card)");

        List<Rect> allMood = ctx.Mood.All().ToList();
        Assert(allMood.Count == 28, "two stacked rows must yield 28 mood controls (2 * (3 sliders + 3 fields + 6 small buttons + 2 reset controls)); got " + allMood.Count + " = " + ctx.Mood.Sliders.Count + " sliders + " + ctx.Mood.Fields.Count + " fields + " + ctx.Mood.SmallButtons.Count + " small + " + ctx.Mood.AutoButtons.Count + " default + " + ctx.Mood.PresetButtons.Count + " preset");

        foreach (Rect local in allMood)
        {
            Rect page = ToPageRect(local, ctx.ContentViewport, ctx.ContentScrollPosition);
            Assert(IsInside(page, ctx.CardPageRect), "mood control must be inside the scope-tree card: " + page);
        }

        AssertNoOverlaps(allMood, "mood control rects must not overlap");
        AssertAutoDoesNotOverlapSteppers(ctx.Mood);
    }

    private static void AssertMoodRowStacking(CaptureContext ctx)
    {
        List<Rect> sliders = ctx.Mood.Sliders.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
        Assert(sliders.Count == 6, "expected six mood sliders before stacking assertion");

        List<Rect> row1Sliders = sliders.Take(3).ToList();
        List<Rect> row2Sliders = sliders.Skip(3).Take(3).ToList();

        AssertStackedSliders(row1Sliders, "first mood row");
        AssertStackedSliders(row2Sliders, "second mood row");
    }

    private static void AssertStackedSliders(IReadOnlyList<Rect> sliders, string rowName)
    {
        Assert(sliders.Count == 3, rowName + " must have 3 sliders");
        Assert(sliders[0].y < sliders[1].y - Epsilon && sliders[1].y < sliders[2].y - Epsilon,
            rowName + " sliders must be vertically stacked with distinct Y values");
        Assert(Math.Abs(sliders[0].x - sliders[1].x) < 1f && Math.Abs(sliders[1].x - sliders[2].x) < 1f,
            rowName + " stacked sliders must share the same X (full-width stepper lines)");
        Assert(Math.Abs(sliders[0].width - sliders[1].width) < 1f && Math.Abs(sliders[1].width - sliders[2].width) < 1f,
            rowName + " stacked sliders must share the same width");
    }

    private static void AssertNoOverlaps(IReadOnlyList<Rect> rects, string message)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            for (int j = i + 1; j < rects.Count; j++)
            {
                Assert(!Overlaps(rects[i], rects[j]), message + " (" + rects[i] + " vs " + rects[j] + ")");
            }
        }
    }

    private static void AssertAutoDoesNotOverlapSteppers(MoodControls mood)
    {
        foreach (Rect auto in mood.AutoButtons.Concat(mood.PresetButtons))
        {
            foreach (Rect stepper in mood.Sliders.Concat(mood.Fields).Concat(mood.SmallButtons))
            {
                Assert(!Overlaps(auto, stepper), "reset control must not overlap any stepper control: " + auto + " vs " + stepper);
            }
        }
    }

    private static CaptureContext CreateCaptureContext(float viewportWidth = ViewportWidth, float viewportHeight = ViewportHeight)
    {
        var source = new RecordingSettingsSource { RichData = true };
        UiHost host = UsKernelSettingsHost.Create(source);
        try
        {
            host.Bindings.Invoke("set-tab", "Tuning");
            // 0.4.0 keys scroll positions by element node, so the container must be arranged before its
            // id is addressable; this keeps the capture pinned to the top of the scroll content.
            host.MeasureAndArrange(new Vector2(viewportWidth, viewportHeight));
            Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);

            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(viewportWidth, viewportHeight));
            Assert(snapshot.RectById.TryGetValue("scope-tree", out Rect cardPage), "snapshot must contain scope-tree in Tuning workspace");

            Rect contentViewport = snapshot.Viewports["content-scroll"];
            Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
            Rect cardLocal = ToContentLocal(cardPage, contentViewport, scroll);

            var raw = new CapturedRects();
            try
            {
                SetButtonOverride(rect => { raw.Buttons.Add(rect); return false; });
                SetSliderOverride((rect, value, min, max) => { raw.Sliders.Add(rect); return value; });
                SetTextFieldOverride((rect, text) => { raw.TextFields.Add(rect); return text; });
                host.DrawFrame(new Rect(0f, 0f, viewportWidth, viewportHeight));
            }
            finally
            {
                ClearOverrides();
            }

            MoodControls mood = FilterMoodControls(raw, cardLocal);
            return new CaptureContext(
                source,
                host,
                snapshot,
                cardPage,
                cardLocal,
                contentViewport,
                scroll,
                raw.Sliders.Count,
                raw.TextFields.Count,
                mood,
                new Rect(0f, 0f, viewportWidth, viewportHeight));
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private static MoodControls FilterMoodControls(CapturedRects raw, Rect cardLocal)
    {
        MoodControls mood = new()
        {
            Sliders = raw.Sliders.Where(r => IsInside(r, cardLocal)).ToList(),
            Fields = raw.TextFields.Where(r => IsInside(r, cardLocal)).ToList(),
            SmallButtons = raw.Buttons.Where(r => IsInside(r, cardLocal) && IsSmallButton(r)).ToList(),
        };
        SplitResetButtons(
            raw.Buttons.Where(r => IsInside(r, cardLocal) && IsResetButton(r)).ToList(),
            mood.AutoButtons,
            mood.PresetButtons);
        return mood;
    }

    private static bool IsSmallButton(Rect rect)
    {
        return Math.Abs(rect.width - 20f) <= 2f && rect.height >= 14f && rect.height <= 34f;
    }

    /// <summary>
    /// Widths the two mood reset controls draw at (UsScopeTreeWidget.MoodResetWidthFor): each label's
    /// measured Tiny width plus its 12px side padding, floored at 52. Used only as a sanity bound - the
    /// two controls are told apart by POSITION (the left one on a line is "reset to default"), because
    /// both translated labels are the same length in Chinese and width alone cannot separate them. This
    /// lane installs no translator table, so a label resolves to its key literal.
    /// </summary>
    private static readonly float DefaultResetWidth = Math.Max(
        52f,
        FerriteLib.UiKit.Kernel.VerseFerriteTextMetrics.Instance.MeasureWidth("US.Tuning.ResetToDefault", UiFont.Tiny) + 12f);

    private static readonly float PresetResetWidth = Math.Max(
        52f,
        FerriteLib.UiKit.Kernel.VerseFerriteTextMetrics.Instance.MeasureWidth("US.Tuning.ResetToPreset", UiFont.Tiny) + 12f);

    /// <summary>A drawn reset control: wide enough for either measured label (never the 20px minus/plus).</summary>
    private static bool IsResetButton(Rect rect)
    {
        // Both bounds matter: without the upper one the wide layer-segment buttons (212/425px) would be
        // counted as reset controls and the draw-order pairing would then invent ghost pairs.
        float minWidth = Math.Min(DefaultResetWidth, PresetResetWidth) - 1f;
        float maxWidth = Math.Max(DefaultResetWidth, PresetResetWidth) + 1f;
        return rect.width >= minWidth && rect.width <= maxWidth && rect.height >= 14f && rect.height <= 34f;
    }

    /// <summary>
    /// Splits the reset controls into per-line pairs: on each line the left control is "reset to
    /// default" and the right one is "reset to preset". Identity comes from the layout order the widget
    /// draws, not from a width comparison.
    /// </summary>
    private static void SplitResetButtons(IReadOnlyList<Rect> reset, List<Rect> defaults, List<Rect> presets)
    {
        // Draw order per mood row is always default then preset, on one line when they fit and on two
        // when they do not, so pairing by sorted order survives both shapes (width cannot separate them:
        // the two Chinese labels are the same length).
        List<Rect> sorted = reset.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
        for (int i = 0; i + 1 < sorted.Count; i += 2)
        {
            defaults.Add(sorted[i]);
            presets.Add(sorted[i + 1]);
        }
    }

    private static Rect FirstMinusRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.SmallButtons.OrderBy(r => r.y).ThenBy(r => r.x).Take(6).ToList();
        Assert(row1.Count == 6, "first mood row must have 6 small buttons for minus interaction");
        return row1[0];
    }

    private static Rect FirstPlusRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.SmallButtons.OrderBy(r => r.y).ThenBy(r => r.x).Take(6).ToList();
        Assert(row1.Count == 6, "first mood row must have 6 small buttons for plus interaction");
        return row1[1];
    }

    private static Rect FirstSliderRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.Sliders.OrderBy(r => r.y).ThenBy(r => r.x).Take(3).ToList();
        Assert(row1.Count == 3, "first mood row must have 3 sliders for slider interaction");
        return row1[0];
    }

    private static Rect FirstFieldRect(CaptureContext ctx)
    {
        List<Rect> row1 = ctx.Mood.Fields.OrderBy(r => r.y).ThenBy(r => r.x).Take(3).ToList();
        Assert(row1.Count == 3, "first mood row must have 3 number fields for commit interaction");
        return row1[0];
    }

    /// <summary>
    /// The second reset control ("reset to preset") is a different class of write from the first: it
    /// must reach the host as "reset-mood-to-preset" with the row's mood, never as a field-level
    /// "set-mood-tuning" write. The settings semantics live in the migration lane; this step proves the
    /// binding.
    /// </summary>
    private static void PresetResetRoutesTypedMoodTuning()
    {
        using CaptureContext ctx = CreateCaptureContext();
        List<Rect> presets = ctx.Mood.PresetButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
        Assert(presets.Count == 2, "expected 2 \"reset to preset\" controls before the interaction");
        ctx.Source.LastMoodPresetReset = null;
        ctx.Source.LastMoodPresetResetCount = 0;

        try
        {
            SetButtonOverride(rect => RectMatches(rect, presets[0]));
            SetSliderOverride((rect, value, min, max) => value);
            SetTextFieldOverride((rect, text) => text);
            ctx.Host.DrawFrame(new Rect(0f, 0f, ViewportWidth, ViewportHeight));

            Assert(ctx.Source.LastMoodPresetReset == SqueakMood.Good, "the preset reset must write the first rich mood row (Good)");
            Assert(ctx.Source.LastMoodPresetResetCount == 1, "the preset reset must route exactly one reset-mood-to-preset action");
            Assert(ctx.Source.LastMoodFactor == null, "the preset reset must not masquerade as a set-mood-tuning write");
        }
        finally
        {
            ClearOverrides();
            ctx.Source.LastMoodPresetReset = null;
            ctx.Source.LastMoodPresetResetCount = 0;
        }
    }

    /// <summary>
    /// Both reset controls must route their typed write in every mood layout mode. The 800px pass is the
    /// stacked shape; 1000px is wide enough for the steppers to share a line but not for both controls,
    /// so they wrap to a second line; 1920px is the inline shape. The mode is asserted from the captured
    /// geometry, so a layout change that silently collapses two modes into one fails here.
    /// </summary>
    private static void ResetRoutingAcrossLayoutModes()
    {
        (float Width, float Height, string Mode)[] cases =
        {
            (800f, 600f, "stacked"),
            (1000f, 700f, "second-line"),
            (1920f, 1080f, "inline"),
        };

        foreach ((float width, float height, string mode) in cases)
        {
            using CaptureContext ctx = CreateCaptureContext(width, height);
            List<Rect> sliders = ctx.Mood.Sliders.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            List<Rect> defaults = ctx.Mood.AutoButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            List<Rect> presets = ctx.Mood.PresetButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
            Assert(sliders.Count >= 3 && defaults.Count == 2 && presets.Count == 2,
                mode + " pass must expose the mood steppers and both reset controls, got "
                + sliders.Count + "/" + defaults.Count + "/" + presets.Count);

            // The three modes differ by WHERE the controls sit, not by whether they sit together:
            // stacked = steppers on their own lines; second-line = steppers share a line and the controls
            // are pushed to the next one; inline = controls share the steppers' line.
            bool steppersStacked = Math.Abs(sliders[0].y - sliders[1].y) > 1f;
            bool controlsShareStepperLine = Math.Abs(defaults[0].y - sliders[0].y) < 1f;
            string observed = steppersStacked ? "stacked" : controlsShareStepperLine ? "inline" : "second-line";
            Assert(observed == mode, width + "px must select the " + mode + " mood layout, got " + observed
                + " (card body " + ctx.CardLocalRect.width + "px)");

            try
            {
                // Reset to default: the clear write.
                ClearLastMood(ctx.Source);
                SetButtonOverride(rect => RectMatches(rect, defaults[0]));
                SetSliderOverride((rect, value, min, max) => value);
                SetTextFieldOverride((rect, text) => text);
                ctx.Host.DrawFrame(ctx.Viewport);
                Assert(ctx.Source.LastMood == SqueakMood.Good && ctx.Source.LastMoodFactor == SqueakMoodFactor.Clear,
                    mode + ": the \"reset to default\" control must write Clear for the first rich row");

                // Reset to preset: the typed re-write.
                ctx.Source.LastMoodPresetReset = null;
                ctx.Source.LastMoodPresetResetCount = 0;
                SetButtonOverride(rect => RectMatches(rect, presets[0]));
                ctx.Host.DrawFrame(ctx.Viewport);
                Assert(ctx.Source.LastMoodPresetReset == SqueakMood.Good && ctx.Source.LastMoodPresetResetCount == 1,
                    mode + ": the \"reset to preset\" control must route exactly one reset-mood-to-preset action");
            }
            finally
            {
                ClearOverrides();
                ClearLastMood(ctx.Source);
                ctx.Source.LastMoodPresetReset = null;
                ctx.Source.LastMoodPresetResetCount = 0;
            }
        }
    }

    /// <summary>
    /// The ctx-contract assertion: with a popup covering a reset control, aiming a click at that control
    /// must not fire it - the protected overload asks the hit stack first and returns false without ever
    /// reaching the native button. The positive control (popup closed, same click, same pointer) proves the
    /// assertion can go the other way, so this step dies the moment a call site falls back to the
    /// context-free overload.
    /// </summary>
    private static void CoveredControlYieldsTheClick()
    {
        using CaptureContext ctx = CreateCaptureContext();

        // The covered control must belong to a DIFFERENT element than the popup's owner: the hit-stack rule
        // deliberately lets an element keep clicks over its own popup (that is what makes toggle-to-close
        // work), so a popup owned by us/scope-tree can never be stolen from a sibling control inside the
        // same widget. The nav rows are a separate element, are always drawn, and their click is observable
        // through the active-tab binding.
        var navRects = new List<Rect>();
        try
        {
            SetButtonOverride(rect =>
            {
                if (rect.width >= 160f && rect.height >= 50f) navRects.Add(rect);
                return false;
            });
            ctx.Host.DrawFrame(ctx.Viewport);
        }
        finally
        {
            ClearOverrides();
        }

        Assert(navRects.Count >= 1, "expected at least one nav row for the covered-click case");
        Rect target = navRects.OrderBy(r => r.y).ThenBy(r => r.x).First();
        Rect targetWindow = target;
        string tabBefore = ctx.Host.Bindings.TryGet(UiBindings.ActiveTabKey, out string t0) ? t0 : "";
        Assert(tabBefore == "Tuning", "the lane must start on Tuning, got " + tabBefore);

        // Anchor a scope-dropdown popup just above the target so it opens downward over the control.
        string? owner = null;
        Rect popup = default;
        foreach (string key in UniversalSqueaker.Kernel.BuiltInActionKeys.All)
        {
            ctx.Host.Session.OpenPopup("scope-tree-scope-" + key, new Rect(targetWindow.x, targetWindow.y - 24f, targetWindow.width, 22f));
            ctx.Host.DrawFrame(ctx.Viewport);
            ctx.Host.DrawFrame(ctx.Viewport);
            if (TryGetPopupHitLayerFromHost(ctx.Host, out UiHitLayer layer))
            {
                owner = key;
                popup = layer.Rect;
                break;
            }

            ctx.Host.Session.ClosePopup();
        }

        Assert(owner != null, "no scope dropdown published a popup layer for the covered-click case");
        Vector2 pointerWindow = new(targetWindow.x + targetWindow.width / 2f, targetWindow.y + targetWindow.height / 2f);
        Assert(popup.x <= pointerWindow.x && pointerWindow.x <= popup.xMax
            && popup.y <= pointerWindow.y && pointerWindow.y <= popup.yMax,
            "the popup must really cover the target control; popup=" + popup + " target=" + targetWindow);

        try
        {
            SetMousePosition(pointerWindow);
            SetButtonOverride(rect => RectMatches(rect, target));
            ctx.Host.DrawFrame(ctx.Viewport);
            Assert(!(ctx.Host.Bindings.TryGet(UiBindings.ActiveTabKey, out string coveredTab) && coveredTab != tabBefore),
                "a popup-covered control must not take the click (ctx overload); a raw UiNative.Button(Rect) site fails here");

            // Positive control: same pointer, same click, popup gone - the tab must now switch.
            ctx.Host.Session.ClosePopup();
            ctx.Host.DrawFrame(ctx.Viewport);
            ctx.Host.DrawFrame(ctx.Viewport);
            SetButtonOverride(rect => RectMatches(rect, target));
            ctx.Host.DrawFrame(ctx.Viewport);
            string tabAfter = ctx.Host.Bindings.TryGet(UiBindings.ActiveTabKey, out string t1) ? t1 : "";
            Assert(tabAfter != tabBefore,
                "with no popup the same click must reach the control (positive control for the yield assertion); tab stayed " + tabAfter);
        }
        finally
        {
            ClearMousePosition();
            ClearOverrides();
            ctx.Host.Session.ClosePopup();
        }
    }

    private static bool TryGetPopupHitLayerFromHost(UiHost host, out UiHitLayer layer)
    {
        layer = default;
        foreach (UiHitLayer candidate in host.Session.HitLayers)
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

    private static FieldInfo DebugMousePositionField => RequireField("DebugMousePosition", typeof(Vector2));
    private static FieldInfo DebugMousePositionEnabledField => RequireField("DebugMousePositionEnabled", typeof(bool));

    private static Rect FirstAutoRect(CaptureContext ctx)
    {
        List<Rect> autos = ctx.Mood.AutoButtons.OrderBy(r => r.y).ThenBy(r => r.x).ToList();
        Assert(autos.Count == 2, "expected 2 Auto buttons before Auto interaction");
        return autos[0];
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

    private static Rect ToContentLocal(Rect pageRect, Rect contentViewport, Vector2 scroll)
    {
        return new Rect(
            pageRect.x - contentViewport.x + scroll.x,
            pageRect.y - contentViewport.y + scroll.y,
            pageRect.width,
            pageRect.height);
    }

    private static Rect ToPageRect(Rect localRect, Rect contentViewport, Vector2 scroll)
    {
        return new Rect(
            localRect.x + contentViewport.x - scroll.x,
            localRect.y + contentViewport.y - scroll.y,
            localRect.width,
            localRect.height);
    }

    private static void SetButtonOverride(Func<Rect, bool> value)
    {
        SetField(ButtonOverrideField, value);
    }

    private static void SetSliderOverride(Func<Rect, float, float, float, float> value)
    {
        SetField(SliderOverrideField, value);
    }

    private static void SetTextFieldOverride(Func<Rect, string, string> value)
    {
        SetField(TextFieldOverrideField, value);
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

    private static void ClearLastMood(RecordingSettingsSource source)
    {
        source.LastMood = null;
        source.LastMoodFactor = null;
        source.LastMoodValue = null;
    }

    private static bool FloatEquals(float? actual, float expected)
    {
        return actual.HasValue && Math.Abs(actual.Value - expected) < 0.001f;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturedRects
    {
        public List<Rect> Buttons { get; } = new();
        public List<Rect> Sliders { get; } = new();
        public List<Rect> TextFields { get; } = new();
    }

    private sealed class MoodControls
    {
        public List<Rect> Sliders { get; set; } = new();
        public List<Rect> Fields { get; set; } = new();
        public List<Rect> SmallButtons { get; set; } = new();
        public List<Rect> AutoButtons { get; set; } = new();
        public List<Rect> PresetButtons { get; set; } = new();

        public IEnumerable<Rect> All()
        {
            foreach (Rect rect in Sliders) yield return rect;
            foreach (Rect rect in Fields) yield return rect;
            foreach (Rect rect in SmallButtons) yield return rect;
            foreach (Rect rect in AutoButtons) yield return rect;
            foreach (Rect rect in PresetButtons) yield return rect;
        }
    }

    private sealed class CaptureContext : IDisposable
    {
        public RecordingSettingsSource Source { get; }
        public UiHost Host { get; }
        public UiLayoutSnapshot Snapshot { get; }
        public Rect CardPageRect { get; }
        public Rect CardLocalRect { get; }
        public Rect ContentViewport { get; }
        public Vector2 ContentScrollPosition { get; }
        public int CapturedSliderCount { get; }
        public int CapturedTextFieldCount { get; }
        public MoodControls Mood { get; }
        /// <summary>Viewport this pass drew at; the two wide modes are driven through it.</summary>
        public Rect Viewport { get; }

        public CaptureContext(
            RecordingSettingsSource source,
            UiHost host,
            UiLayoutSnapshot snapshot,
            Rect cardPageRect,
            Rect cardLocalRect,
            Rect contentViewport,
            Vector2 contentScrollPosition,
            int capturedSliderCount,
            int capturedTextFieldCount,
            MoodControls mood,
            Rect viewport)
        {
            Source = source;
            Host = host;
            Snapshot = snapshot;
            CardPageRect = cardPageRect;
            CardLocalRect = cardLocalRect;
            ContentViewport = contentViewport;
            ContentScrollPosition = contentScrollPosition;
            CapturedSliderCount = capturedSliderCount;
            CapturedTextFieldCount = capturedTextFieldCount;
            Mood = mood;
            Viewport = viewport;
        }

        public void Dispose()
        {
            Host.Dispose();
        }
    }
}
