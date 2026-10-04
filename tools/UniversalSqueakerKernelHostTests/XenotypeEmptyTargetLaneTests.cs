using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// XG1 lane: the tuning editor must not OFFER a write it cannot make. When the current XENOTYPE layer has no
/// tunable target - its own identity is empty, which is what an empty (race, xenotype) domain catalog produces
/// - the card states the reason in its existing controls and the action/value/reset controls become INERT: the
/// scope trigger is replaced by a static band (so no popup can open), the number/minus/plus/slider atoms are
/// replaced by static disabled representations (so no focus, draft, drag or hot control can exist), and the
/// reset controls are not even hit-tested. The LAYER segment keeps working so the player can leave the state.
/// On the production side the write entries refuse the same identity, so an empty domain can never be mistaken
/// for the Global one.
///
/// <para>
/// WHAT THIS MEASURES, WITH WHICH INSTRUMENTS. (a) The empty-target state is REAL input: the fixture yields
/// layer 2 with race "" and xeno "", an EMPTY <c>tuning-domains</c> list and the production-shaped action/mood
/// rows, and the lane asserts that state through the page's own bindings before drawing. (b) The reason
/// sentence is asserted through the metrics channel (it was really DRAWN, not only bound) at the ONE real
/// page box on the 1024x768 minimum screen (760x524) in EN and ZH, with help-open truly set - BH1 arranges
/// that SAME box in both help states, so a case names its state by the flag and never by a box.
/// (c) INERTNESS is measured on the CARRIER's own
/// seams, not on the write recorders alone: the button/slider/text-field override seams show that no
/// interactive atom is reached in the blocked card (only the three layer buttons remain), REAL pointer events
/// (each preceded by scrolling the target band into the scroll viewport, and asserting it is inside) leave no
/// popup open and publish no popup layer from the scope band, leave the carrier's own
/// <see cref="UiValueState"/> unfocused and draft-free from the number band, and change neither that state nor
/// any recorder from the slider, stepper and reset bands. (d) SHAPE PRESERVATION: the blocked card's arranged
/// height differs from the valid-target reference card at the same box by EXACTLY the reason band plus the
/// widget's row gap - so every row and band keeps the geometry it has when the layer is writable - and every
/// text-carrying substitute band is asserted to have been drawn through the metrics channel. (e) The CONTROL
/// cases (Global layer 0, Race, and a valid xenotype target) show the SAME real clicks opening the popup, taking
/// field focus and landing the writes, which is what makes (c) a block rather than a broken instrument.
/// </para>
///
/// <para>
/// MUTATION LEDGER (US AGENTS.md:62). PM executed three faithful reverts: re-entering native
/// scope or parameter controls in the blocked branch reddens the card-scoped interactive-atom count;
/// removing the production ResetMoodToPreset identity guard reddens the named refusal before the
/// game-only preset lookup. These prove the inert-input and production-boundary channels. Popup,
/// focus, shape-delta, text-fit, remaining setters and paired valid-target cases are GUARDS; their
/// own mutations were not executed. The preset lookup proof observes the attempted game-only call,
/// not an actual global write in game. Actual input corrections are in the PM XG1 evidence packet.
/// </para>
///
/// <para>
/// NOT PROVEN HERE, stated rather than implied. (1) The model's own <c>BuildTuningDomains</c> empty branch is
/// unreachable in this harness (BuildView needs the game's def database), so the fixture reproduces the
/// RESULTING state/view pair instead of running that model path; "the model derives this state" stays
/// unverified. (2) A completed Global (or valid-identity) settings WRITE is not exercised. The reason is
/// measured, not assumed: the settings mutators' resolver notification returns early in this harness
/// (<c>EnsureMainThread</c> answers false because the mod never initialises it, and the compiled DLL carries
/// NO <c>Debug.Assert</c> member reference nor its message - the <c>[Conditional]</c> call was removed at
/// compile time), but the same write tail calls <c>QueuePersistence</c> &rarr; <c>UniversalSqueakerMod.Instance</c>,
/// a mod type this harness documents as unloadable (the Verse stub ships no <c>Verse.Mod</c>; Program.cs records
/// that boundary). "Layer 0 stays legitimate" is therefore pinned on the shared predicate (a reflection probe of
/// the model's own method) plus the widget-level control cases, not on a completed Global settings write.
/// (3) The carrier's native disabled styling cannot be borrowed for these composite sub-controls (they have no
/// element node, and this step may not add manifest vocabulary), so the disabled APPEARANCE is the widget's own
/// disabled ink on the substitute bands.
/// </para>
/// </summary>
internal static class XenotypeEmptyTargetLaneTests
{
    // BH1: the help toggle changes NO width. The window has one page box on the game's minimum screen - 800
    // less 2 x 20 shell chrome = 760 wide, 600 less the title bar and the bottom inset = 524 high - and BOTH
    // help states arrange it. The retired 984px open box went with the 320px side column; the bottom panel
    // now reserves its declared Height 140 out of the body's HEIGHT instead.
    private const float PageBoxWidth = 760f;
    private const float PageBoxHeight = 524f;
    /// <summary>The EXTERIOR centre column the content-scroll occupies: 760 less page-root Padding 12 x 2 =
    /// 736 inner, less the 200 nav column and the body-row Gap 12 = 524, at both help states.</summary>
    private const float CentreColumnWidth = 524f;
    /// <summary>The engine's own scrollbar reservation (UiLayoutEngine.ScrollbarWidth, mirrored from the
    /// pinned carrier and kept honest by the pair of assertions below): the content of a Scroll is laid out
    /// in the centre column LESS this reserve, so the Tuning CARD is 16 narrower than the column that
    /// contains it - the same card-inside-centre relation MoodLayoutFocusedTests records (400/416 at the
    /// old open box). Asserting a drawn card against the EXTERIOR number was the r1 instrument-scale error.</summary>
    private const float ScrollbarReserve = 16f;
    /// <summary>The INTERIOR width the scope-tree card is really arranged at: centre column less reserve.</summary>
    private const float TuningCardInnerWidth = CentreColumnWidth - ScrollbarReserve;
    /// <summary>The manifest's declared bottom help panel reservation (help-scroll Height="140").</summary>
    private const float HelpPanelHeight = 140f;

    /// <summary>The widget's declared dropdown width (<c>ButtonWidth</c>): the filter that tells a popup
    /// trigger apart from every other button at these boxes. The lane asserts the trigger count, so a
    /// non-trigger control that ever lands on that width fails instead of being silently mis-classified.</summary>
    private const float TriggerWidth = 96f;
    private const float TriggerTolerance = 1.5f;
    private const float RectEpsilon = 0.01f;

    // Mirrored from UsScopeTreeWidget's declared constants so the shape clause can compute the band the widget
    // reserved: the empty-target band floor, its left padding inside the body, and its row gap. The delta
    // assertion is what keeps the mirror honest - a moved constant reddens instead of drifting silently.
    private const float WidgetEmptyTargetMinBand = 14f;
    private const float WidgetLeftPadding = 10f;
    private const float WidgetRowGap = 2f;

    private const string EmptyReasonKey = "US.Tuning.EmptyXenotypeTarget";
    private const string TuningCardId = "scope-tree";
    private const string EatScopeElementId = "scope-tree-scope-Eat";
    private const string FirstStepperElementId = "mood-Good-Pitch";

    public static int RunAll()
    {
        Step("the empty xenotype target is inert and keeps the row shapes",
            TheEmptyTargetIsInertAndKeepsTheRowShapes);
        Step("Global, Race and a valid Xenotype target stay interactive and submit (control cases)",
            TheControlLayersStayInteractive);
        Step("every production write entry refuses an empty xenotype identity",
            TheProductionEntriesRefuseAnEmptyIdentity);
        Console.WriteLine("XenotypeEmptyTargetLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // 1. The empty target: reason drawn, every action/value/reset control INERT, shapes preserved
    // ---------------------------------------------------------------------------------------------

    private static void TheEmptyTargetIsInertAndKeepsTheRowShapes()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                // BH1: one page box; the loop switches the help flag and records what the arrangement
                // measured, so the "help changes no width" claim is compared, not assumed.
                var referenceCardByState = new Dictionary<string, float>();
                var blockedCardByState = new Dictionary<string, float>();
                foreach ((string state, bool helpOpen) in new[]
                         {
                             ("help open", true),
                             ("help closed", false),
                         })
                {
                    float box = PageBoxWidth;
                    string where = "XG1 " + language + ", empty target, " + state + " at " + box + "x" + PageBoxHeight;
                    var metrics = new DrawnTextMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        // The reference: the SAME box and language with a VALID layer-2 target. It supplies the
                        // shapes the blocked card must keep and the interactive atoms it must lose.
                        var referenceSource = new RecordingSettingsSource { RichData = true, MirrorTuningWrites = true };
                        referenceSource.TuningDomains = new[]
                        {
                            new TuningDomainOptionView("human", "Sanguophage (Human)", "sanguophage"),
                        };
                        referenceSource.SetTuningLayer(2);
                        referenceSource.SetTuningDomain("human", "sanguophage");
                        using UiHost referenceHost = UsKernelSettingsHost.Create(referenceSource, metrics);
                        referenceHost.Bindings.Set("help-open", helpOpen);
                        referenceHost.Bindings.Invoke("set-tab", "Tuning");
                        UiLayoutSnapshot referenceFrame = referenceHost.MeasureAndArrange(new Vector2(box, PageBoxHeight));
                        Assert(referenceFrame.Viewports.ContainsKey("help-scroll") == helpOpen,
                            where + ": the bottom help panel must be arranged exactly while help is open"
                            + " (help-open really set; BH1 has ONE presentation and no side column)");
                        Assert(referenceFrame.RectById.ContainsKey("body-row")
                                && referenceFrame.Viewports.ContainsKey("content-scroll")
                                && referenceFrame.Viewports.ContainsKey("nav-column"),
                            where + ": BH1 never replaces the body - the body row, the centre content scroll"
                            + " and the nav column stay arranged in BOTH help states");
                        Assert(Math.Abs(referenceFrame.Viewports["content-scroll"].width - CentreColumnWidth) <= 0.5f,
                            where + ": the EXTERIOR centre column is 524 in either help state, got "
                            + Num(referenceFrame.Viewports["content-scroll"].width));
                        if (helpOpen)
                        {
                            Assert(Math.Abs(referenceFrame.Viewports["help-scroll"].height - HelpPanelHeight) <= 0.5f,
                                where + ": the bottom help panel is arranged at its declared Height 140, got "
                                + Num(referenceFrame.Viewports["help-scroll"].height));
                        }
                        Program.SetScrollPositionById(referenceHost.Session, "content-scroll", Vector2.zero);
                        CaseObservation reference = Observe(
                            referenceHost, box, metrics, reports, table, where, expectReason: false);
                        List<Rect> referenceTriggers = TriggerRects(reference.Buttons);
                        Assert(referenceTriggers.Count == 3,
                            where + ": the valid reference must draw its domain trigger plus two scope triggers, got "
                            + referenceTriggers.Count);
                        Assert(reference.Sliders.Count == 4 * 3 && reference.Fields.Count == 4 * 3,
                            where + ": the valid reference must draw every parameter's slider and field, got "
                            + reference.Sliders.Count + " sliders and " + reference.Fields.Count + " fields");

                        // The blocked case.
                        var source = new RecordingSettingsSource { RichData = true, EmptyXenotypeTarget = true };
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        host.Bindings.Set("help-open", helpOpen);
                        host.Bindings.Invoke("set-tab", "Tuning");
                        UiLayoutSnapshot blockedFrame = host.MeasureAndArrange(new Vector2(box, PageBoxHeight));
                        Assert(blockedFrame.Viewports.ContainsKey("help-scroll") == helpOpen,
                            where + ": the bottom help panel must be arranged exactly while help is open");
                        Assert(blockedFrame.RectById.ContainsKey("body-row")
                                && blockedFrame.Viewports.ContainsKey("content-scroll")
                                && blockedFrame.Viewports.ContainsKey("nav-column"),
                            where + ": BH1 never replaces the body - the blocked card keeps the body row, the"
                            + " centre content scroll and the nav column in BOTH help states");

                        Assert(Math.Abs(blockedFrame.Viewports["content-scroll"].width - CentreColumnWidth) <= 0.5f,
                            where + ": the blocked card keeps the same 524 EXTERIOR centre column, got "
                            + Num(blockedFrame.Viewports["content-scroll"].width));
                        // The fixture state is the lane's INPUT (AGENTS.md:61): layer 2, an empty target, no
                        // target options, and real rows whose controls must become inert.
                        Assert(host.Bindings.Get<int>("tuning-layer") == 2,
                            where + ": the fixture must really be on the xenotype layer");
                        Assert(host.Bindings.Get<string>("tuning-race") == "",
                            where + ": the fixture's race identity must also be empty");
                        Assert(host.Bindings.Get<string>("tuning-xeno") == "",
                            where + ": the fixture's xenotype target must really be empty");
                        Assert(host.Bindings.Get<IReadOnlyList<TuningDomainOptionView>>("tuning-domains").Count == 0,
                            where + ": the fixture must really offer no target option");
                        Assert(host.Bindings.Get<IReadOnlyList<ActionScopeRowView>>("action-scopes").Count > 0
                            && host.Bindings.Get<IReadOnlyList<MoodTuningRowView>>("mood-rows").Count > 0,
                            where + ": the inert controls must really exist (production-shaped rows)");

                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                        CaseObservation blocked = Observe(host, box, metrics, reports, table, where, expectReason: true);

                        // (1) NO interactive atom is reached in the blocked card: only the three layer buttons
                        // remain, and neither a slider nor a number field exists any more.
                        Assert(blocked.Buttons.Count == 3,
                            where + ": with no target the card may keep only the three layer buttons interactive, got "
                            + blocked.Buttons.Count + ": " + DescribeAll(blocked.Buttons));
                        Assert(blocked.Sliders.Count == 0 && blocked.Fields.Count == 0,
                            where + ": no slider/field atom may be reached with no target, got "
                            + blocked.Sliders.Count + " sliders and " + blocked.Fields.Count + " fields");

                        // (2) The carrier's own draft/focus seam, before any pointer event: the atoms never ran,
                        // so the value state is still untouched - no draft text and no model value.
                        UiValueState valueState = FirstStepperState(host);
                        Assert(!valueState.Focused && valueState.EditText.Length == 0 && valueState.FloatValue == 0f,
                            where + ": the inert number band must leave no draft, focus or value in the carrier's"
                            + " own value state, got focused=" + valueState.Focused + " edit='" + valueState.EditText
                            + "' value=" + Num(valueState.FloatValue));

                        // (3) Every static substitute band that carries text was really DRAWN (the metrics
                        // channel), so a "shape kept" claim cannot hide a band that stopped being painted.
                        Assert(metrics.Measured(table["US.Tuning.Scope.Any"]),
                            where + ": the inert scope band must show the scope this row holds");
                        Assert(metrics.Measured("−") && metrics.Measured("+"),
                            where + ": the inert stepper must show its minus and plus bands");
                        Assert(metrics.Measured("1"),
                            where + ": the inert number band must show the row's own value (Good/Pitch = 1)");
                        Assert(metrics.Measured(table["US.Tuning.ResetToDefault"])
                            && metrics.Measured(table["US.Tuning.ResetToPreset"]),
                            where + ": the inert reset bands must still name their actions");

                        // (4) SHAPES: the blocked card differs from the valid reference by EXACTLY the reason
                        // band plus the widget's row gap, so every row and band below keeps its geometry.
                        float delta = blocked.Card.height - reference.Card.height;
                        float bodyWidth = reference.Card.width - UsCardLayout.Padding * 2f;
                        float expectedBand = Mathf.Max(WidgetEmptyTargetMinBand, new Program.StubMetrics().MeasureText(
                            table[EmptyReasonKey], UiFont.Tiny, Mathf.Max(1f, bodyWidth - WidgetLeftPadding)));
                        Assert(Math.Abs(delta - (expectedBand + WidgetRowGap)) <= 0.01f,
                            where + ": the blocked card may differ from the valid one only by the reason band ("
                            + Num(expectedBand) + " + " + Num(WidgetRowGap) + "); got delta " + Num(delta)
                            + " (blocked " + Num(blocked.Card.height) + ", valid " + Num(reference.Card.height) + ")");
                        // BH1: the centre column is the same whether or not help is arranged, and a blocked
                        // card loses nothing of it either - both widths are recorded and compared below.
                        referenceCardByState[state] = reference.Card.width;
                        blockedCardByState[state] = blocked.Card.width;

                        // (5) REAL POINTER EVENTS on the scope band: no popup may open, no layer be published.
                        // The band is brought into the scroll viewport first, and that visibility is asserted.
                        ResetRecorders(source);
                        host.Session.ClosePopup();
                        Vector2 scopePoint = BringIntoViewAndGetPagePoint(
                            host, box, Translate(referenceTriggers[1], delta));
                        RealClick(host, box, scopePoint);
                        Assert(!host.Session.IsPopupOpen(EatScopeElementId),
                            where + ": the inert scope band must not open its popup");
                        Assert(!Program.TryGetPopupHitLayer(host.Session, out _),
                            where + ": and no popup layer may be published at all with no target");
                        Assert(source.LastActionScope == null && source.LastActionKey == null,
                            where + ": the inert scope band must not commit anything either");

                        // (6) REAL POINTER EVENTS on the number band: no focus, no draft text.
                        host.Session.ClosePopup();
                        Vector2 fieldPoint = BringIntoViewAndGetPagePoint(host, box, Translate(reference.Fields[0], delta));
                        RealMouseDown(host, box, fieldPoint);
                        Assert(!valueState.Focused,
                            where + ": the inert number band must not take focus");
                        Assert(valueState.EditText.Length == 0,
                            where + ": and it must leave no draft text, got '" + valueState.EditText + "'");
                        RealMouseUp(host, box, fieldPoint);
                        Assert(!valueState.Focused,
                            where + ": no edit session may appear across the release either");

                        // (7) REAL POINTER EVENTS on the slider, stepper and reset bands: nothing responds.
                        ResetRecorders(source);
                        host.Session.ClosePopup();
                        Vector2 sliderPoint = BringIntoViewAndGetPagePoint(host, box, Translate(reference.Sliders[0], delta));
                        RealMouseDown(host, box, sliderPoint);
                        RealMouseDrag(host, box, sliderPoint);
                        RealMouseUp(host, box, sliderPoint);
                        Assert(!valueState.Dragging && valueState.FloatValue == 0f,
                            where + ": the inert slider band must not drag or write a value, got "
                            + Num(valueState.FloatValue) + "/" + valueState.Dragging);

                        Rect stepperBand = Translate(SmallButtons(reference.Buttons)[0], delta);
                        RealClick(host, box, BringIntoViewAndGetPagePoint(host, box, stepperBand));
                        Rect resetBand = Translate(ResetButtons(reference.Buttons)[0], delta);
                        RealClick(host, box, BringIntoViewAndGetPagePoint(host, box, resetBand));
                        Assert(source.LastMood == null && source.LastMoodFactor == null && source.LastMoodValue == null,
                            where + ": a real click on the inert stepper/reset bands must not submit a value");
                        Assert(source.LastMoodPresetReset == null && source.LastMoodPresetResetCount == 0,
                            where + ": and it must not submit the reset action either");
                        Assert(source.LastActionScope == null && source.LastTuningLayer == null,
                            where + ": the inert pass must not touch the scope or layer channels");

                        // (8) the LAYER stays live - a real click on the Race button must reach the boundary.
                        // The layer row sits ABOVE the reason band, so its rect is NOT translated by delta.
                        ResetRecorders(source);
                        Rect raceButton = blocked.Buttons.OrderBy(rect => rect.x).ToList()[1];
                        RealClick(host, box, BringIntoViewAndGetPagePoint(host, box, raceButton));
                        Assert(source.LastTuningLayer == 1,
                            where + ": the layer segment must still submit so the player can leave the state, got "
                            + (source.LastTuningLayer?.ToString(CultureInfo.InvariantCulture) ?? "no write"));

                        Console.WriteLine("[xg1-empty] " + where + " card=" + Num(blocked.Card.height)
                            + " referenceCard=" + Num(reference.Card.height) + " delta=" + Num(delta)
                            + " expectedBand=" + Num(expectedBand)
                            + " buttons=" + blocked.Buttons.Count + " sliders=" + blocked.Sliders.Count
                            + " fields=" + blocked.Fields.Count + " popup=false focus=false layer=live");
                    }
                    finally
                    {
                        ReleasePointer();
                        UiFitAudit.Detach();
                        UiFitAudit.Enabled = false;
                    }
                }
                Assert(Math.Abs(referenceCardByState["help open"] - referenceCardByState["help closed"]) <= 0.01f
                        && Math.Abs(blockedCardByState["help open"] - blockedCardByState["help closed"]) <= 0.01f,
                    language + ": BH1 - help-open changes no width, for either card (valid reference "
                    + Num(referenceCardByState["help open"]) + " -> " + Num(referenceCardByState["help closed"])
                    + ", blocked " + Num(blockedCardByState["help open"]) + " -> "
                    + Num(blockedCardByState["help closed"]) + ")");
                Assert(Math.Abs(referenceCardByState["help open"] - TuningCardInnerWidth) <= 0.5f
                        && Math.Abs(blockedCardByState["help open"] - TuningCardInnerWidth) <= 0.5f,
                    language + ": and that one width is the 508 INTERIOR card (524 centre column less the"
                    + " 16 scrollbar reserve, PM r2 ruling) in both help states, got "
                    + Num(referenceCardByState["help open"]) + " / " + Num(blockedCardByState["help open"]));
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Control cases: the SAME real clicks open the popup, take focus and land the writes
    // ---------------------------------------------------------------------------------------------

    private static void TheControlLayersStayInteractive()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Dictionary<string, string> table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                foreach ((string label, bool helpOpen, Action<RecordingSettingsSource> setUp, int triggers) in new (string, bool, Action<RecordingSettingsSource>, int)[]
                         {
                             ("Global", true, _ => { }, 2),
                             ("Global", false, _ => { }, 2),
                             ("Race", true, source => { source.SetTuningLayer(1); source.SetTuningDomain("human", ""); }, 3),
                             ("valid Xenotype", false, source =>
                             {
                                 source.SetTuningLayer(2);
                                 source.SetTuningDomain("human", "sanguophage");
                             }, 3),
                         })
                {
                    float box = PageBoxWidth;
                    string where = "XG1 control " + language + ", " + label + ", "
                        + (helpOpen ? "help open" : "help closed") + " at " + box + "x" + PageBoxHeight;
                    var metrics = new DrawnTextMetrics();
                    var reports = new List<UiOverflowReport>();
                    UiFitAudit.Attach(metrics, reports.Add);
                    UiFitAudit.Enabled = true;
                    try
                    {
                        var source = new RecordingSettingsSource { RichData = true, MirrorTuningWrites = true };
                        if (label == "valid Xenotype")
                        {
                            source.TuningDomains = new[]
                            {
                                new TuningDomainOptionView("human", "Sanguophage (Human)", "sanguophage"),
                            };
                        }

                        setUp(source);
                        using UiHost host = UsKernelSettingsHost.Create(source, metrics);
                        host.Bindings.Set("help-open", helpOpen);
                        host.Bindings.Invoke("set-tab", "Tuning");
                        UiLayoutSnapshot frame = host.MeasureAndArrange(new Vector2(box, PageBoxHeight));
                        Assert(frame.Viewports.ContainsKey("help-scroll") == helpOpen,
                            where + ": the bottom help panel must be arranged exactly while help is open");
                        Assert(frame.RectById.ContainsKey("body-row")
                                && frame.Viewports.ContainsKey("content-scroll")
                                && frame.Viewports.ContainsKey("nav-column"),
                            where + ": BH1 never replaces the body - the body row, the centre content scroll"
                            + " and the nav column stay arranged in BOTH help states");
                        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);

                        CaseObservation observed = Observe(host, box, metrics, reports, table, where, expectReason: false);
                        Assert(Math.Abs(frame.Viewports["content-scroll"].width - CentreColumnWidth) <= 0.5f
                                && Math.Abs(observed.Card.width - TuningCardInnerWidth) <= 0.5f,
                            where + ": the tuning card is the 508 INTERIOR width inside the 524 EXTERIOR"
                            + " centre column in either help state, got viewport "
                            + Num(frame.Viewports["content-scroll"].width) + " card " + Num(observed.Card.width));
                        List<Rect> triggerRects = TriggerRects(observed.Buttons);
                        Assert(triggerRects.Count == triggers,
                            where + ": expected " + triggers + " dropdown trigger(s), got " + triggerRects.Count);
                        Assert(observed.Sliders.Count == 4 * 3 && observed.Fields.Count == 4 * 3,
                            where + ": every parameter must still register its slider and field, got "
                            + observed.Sliders.Count + " sliders and " + observed.Fields.Count + " fields");
                        List<Rect> layerButtons = LayerButtons(observed.Buttons, triggerRects);
                        Assert(layerButtons.Count == 3,
                            where + ": the layer segment must be drawn, got " + layerButtons.Count);
                        ResetRecorders(source);

                        // (1) THE SAME real click that is inert with no target must OPEN the popup here, and the
                        // real option-row click under it must land the write. Eat is the upper of the last two
                        // triggers (the domain trigger, when the layer has one, comes first).
                        host.Session.ClosePopup();
                        Rect eatTrigger = triggerRects[triggerRects.Count - 2];
                        RealClick(host, box, BringIntoViewAndGetPagePoint(host, box, eatTrigger));
                        Assert(host.Session.IsPopupOpen(EatScopeElementId),
                            where + ": a real click on the scope trigger must open its popup when the layer has a target");
                        Assert(Program.TryGetPopupHitLayer(host.Session, out UiHitLayer popup) && popup.IsPopup,
                            where + ": and that popup must publish its hit layer");
                        int rows = Mathf.Max(1, (int)Math.Round(popup.Rect.height / UiPopup.OptionHeight));
                        Assert(rows >= 3,
                            where + ": the Eat popup must offer Auto, Off and Any, got " + rows + " row(s)");
                        // Row 1 is Off/Disabled: a REAL change from the fixture row's AnyOccurrence, so the
                        // landing write is unambiguous.
                        Vector2 optionPoint = new(
                            popup.Rect.x + popup.Rect.width * 0.5f,
                            popup.Rect.y + UiPopup.OptionHeight + UiPopup.OptionHeight * 0.5f);
                        RealClick(host, box, optionPoint);
                        Assert(source.LastActionScope == SqueakActionScope.Disabled && source.LastActionKey == "Eat",
                            where + ": the same real clicks must land the chosen scope write when a target exists,"
                            + " got '" + (source.LastActionKey ?? "(none)") + "' / "
                            + (source.LastActionScope?.ToString() ?? "(none)"));

                        // (2) THE SAME real press that is inert with no target must take field focus here.
                        ResetRecorders(source);
                        host.Session.ClosePopup();
                        Vector2 fieldPoint = BringIntoViewAndGetPagePoint(host, box, observed.Fields[0]);
                        RealMouseDown(host, box, fieldPoint);
                        UiValueState fieldState = FirstStepperState(host);
                        Assert(fieldState.Focused,
                            where + ": a real press on the number band must take focus when the layer has a target");
                        Assert(fieldState.EditText.Length > 0,
                            where + ": and the field must hold the row's value as its draft, got '"
                            + fieldState.EditText + "'");
                        RealMouseUp(host, box, fieldPoint);

                        // (3) the value and reset controls still submit (clicked through the override seam, the
                        // same instrument the assertions above use).
                        var excluded = new List<Rect>(triggerRects);
                        excluded.AddRange(layerButtons);
                        ResetRecorders(source);
                        ClickEveryControl(host, box, excluded);
                        Assert(source.LastMood != null && source.LastMoodValue != null,
                            where + ": a value control must submit when the layer has a target");
                        Assert(source.LastMoodPresetReset != null && source.LastMoodPresetResetCount > 0,
                            where + ": the reset-to-preset control must submit when the layer has a target");

                        // (4) and the layer segment itself stays live.
                        ResetRecorders(source);
                        ClickOneControl(host, box, layerButtons[0]);
                        Assert(source.LastTuningLayer == 0,
                            where + ": the Global layer button must submit");

                        Console.WriteLine("[xg1-control] " + where + " triggers=" + triggerRects.Count
                            + " layerButtons=" + layerButtons.Count + " sliders=" + observed.Sliders.Count
                            + " fields=" + observed.Fields.Count + " popup=opened focus=taken");
                    }
                    finally
                    {
                        ReleasePointer();
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

    // ---------------------------------------------------------------------------------------------
    // 3. The production write entries
    // ---------------------------------------------------------------------------------------------

    private static void TheProductionEntriesRefuseAnEmptyIdentity()
    {
        var settings = new UniversalSqueakerSettings();
        var globalMood = new MoodTuningRecord
        {
            mood = SqueakMood.Good,
            raceDefName = "",
            xenotypeDefName = "",
            sourcePresetDefName = "us.harness.global.preset",
            hasPitchFactor = true,
            pitchFactor = 1.25f,
            hasVolumeFactor = true,
            volumeFactor = 0.75f,
            hasPitchJitter = true,
            pitchJitter = new FloatRange(1.2f, 1.6f),
        };
        settings.moodTuning = new List<MoodTuningRecord> { globalMood };
        var globalAction = new ActionTuningRecord
        {
            actionKey = "Eat",
            raceDefName = "",
            xenotypeDefName = "",
            hasScope = true,
            scope = SqueakActionScope.ActiveCommand,
        };
        settings.actionTuning = new List<ActionTuningRecord> { globalAction };

        // The exact defective state: the xenotype layer selected, its identity empty. (race, xeno) is then
        // ("","") - the GLOBAL row's identity - which is what the pre-fix reset scan matched.
        var state = new VoicePacksPageState
        {
            TuningLayer = 2,
            TuningRaceDefName = "",
            TuningXenotypeDefName = "",
        };

        // (a) THE FIX. With the guard this is a quiet no-op; reverting it makes the SAME call reach
        // DefDatabase<UniversalSqueakerTuningBaselineDef> (the row's preset anchor), a game-only lookup this
        // stub cannot serve - so the revert reddens here. In game, where the lookup succeeds, the pre-fix code
        // could rewrite the global row's three factors from that preset (not observed in game).
        try
        {
            VoicePacksPageModel.ResetMoodToPreset(settings, state, SqueakMood.Good);
        }
        catch (TypeLoadException ex)
        {
            throw new InvalidOperationException(
                "an empty xenotype identity must be refused before the game-only preset lookup", ex);
        }
        AssertMoodRecordUnchanged(globalMood, "ResetMoodToPreset from an empty xenotype identity");

        // (b) The shared boundary, record-observable for the two VALUE writers: the same empty identity is
        // refused before anything touches the global row.
        VoicePacksPageModel.SetMoodTuning(settings, state, SqueakMood.Good, SqueakMoodFactor.Volume, 0.11f);
        AssertMoodRecordUnchanged(globalMood, "SetMoodTuning(volume) from an empty xenotype identity");
        VoicePacksPageModel.SetMoodTuning(settings, state, SqueakMood.Good, SqueakMoodFactor.Clear, null);
        AssertMoodRecordUnchanged(globalMood, "SetMoodTuning(clear) from an empty xenotype identity");
        VoicePacksPageModel.SetActionScope(settings, state, "Eat", SqueakActionScope.Disabled);
        Assert(globalAction.hasScope && globalAction.scope == SqueakActionScope.ActiveCommand,
            "SetActionScope from an empty xenotype identity must not rewrite the global action row; got hasScope="
            + globalAction.hasScope + " scope=" + globalAction.scope);

        // (c) The identity boundary itself, probed on the model's OWN predicate - the one the two writers and
        // the fixed reset share. This is where "layer 0 (Global) stays legitimate" is pinned: a completed
        // Global settings write cannot be run here (see the class header), so the probe is labelled a GUARD.
        Assert(MoodLayerHasIdentity(0, "", ""), "Global (layer 0) must stay a valid tuning identity");
        Assert(MoodLayerHasIdentity(1, "human", ""), "Race with a race set must stay valid");
        Assert(!MoodLayerHasIdentity(1, "", ""), "Race without a race must be refused");
        Assert(MoodLayerHasIdentity(2, "human", "sanguophage"), "Xenotype with race+xeno must stay valid");
        Assert(!MoodLayerHasIdentity(2, "human", ""), "Xenotype without a target must be refused");
        Assert(!MoodLayerHasIdentity(2, "", ""), "Xenotype with no identity at all must be refused");

        // (d) A race-layer state with a real race is NOT the empty-xenotype case: the value writer reaches the
        // business boundary there. Asserted through the model's own predicate probe as well, because the
        // corresponding settings write's tail is outside this harness (see the class header) - the UI half of
        // this control is the widget lane's Race case above.
        Assert(MoodLayerHasIdentity(1, "human", ""),
            "a race layer with an identity must remain writable (the empty xenotype guard must not leak)");

        Console.WriteLine("[xg1-guard] empty xenotype identity refused by ResetMoodToPreset/SetMoodTuning/"
            + "SetActionScope; global mood row and action row unchanged; predicate layer 0 valid");
    }

    // ---------------------------------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------------------------------

    private sealed class CaseObservation
    {
        public Rect Card;
        public List<Rect> Buttons { get; } = new();
        public List<Rect> Sliders { get; } = new();
        public List<Rect> Fields { get; } = new();
    }

    /// <summary>
    /// One capture pass at one real box, taken with the centre scroll pinned to the top: records every
    /// interactive rect the card drew, asserts the reason sentence's presence matches the state, asserts the
    /// card fits, and returns the arranged card rect. The overrides return "no click", so measuring submits
    /// nothing.
    /// </summary>
    private static CaseObservation Observe(
        UiHost host,
        float box,
        DrawnTextMetrics metrics,
        List<UiOverflowReport> reports,
        Dictionary<string, string> table,
        string where,
        bool expectReason)
    {
        var observation = new CaseObservation();
        try
        {
            SetButtonOverride(rect =>
            {
                if (host.Session.ActiveNode.ElementId == TuningCardId) observation.Buttons.Add(rect);
                return false;
            });
            SetSliderOverride((rect, value, min, max) =>
            {
                if (host.Session.ActiveNode.ElementId == TuningCardId) observation.Sliders.Add(rect);
                return value;
            });
            SetTextFieldOverride((rect, text) =>
            {
                if (host.Session.ActiveNode.ElementId == TuningCardId) observation.Fields.Add(rect);
                return text;
            });
            host.Session.ClosePopup();
            UiFitAudit.Reset();
            reports.Clear();
            metrics.Clear();
            host.DrawChecked(new Rect(0f, 0f, box, PageBoxHeight));
        }
        finally
        {
            ClearOverrides();
        }

        Assert(metrics.Measured(table["US.Tuning.Layer.Xenotype"]),
            where + ": the layer segment must be drawn (the lane's own positive control)");
        Assert(metrics.Measured(table[EmptyReasonKey]) == expectReason,
            where + ": the empty-target reason must be drawn exactly when the target is empty");
        Assert(reports.Count == 0,
            where + ": the tuning page must fit at the real box, got " + Describe(reports));

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(box, PageBoxHeight));
        Assert(snapshot.RectById.TryGetValue(TuningCardId, out Rect card),
            where + ": the Tuning card must be arranged");
        observation.Card = card;
        return observation;
    }

    /// <summary>
    /// Brings one captured band (captured with the scroll at the top, i.e. in content-local space) inside the
    /// centre column's scroll viewport and returns the PAGE-space point a real pointer event must use. The
    /// page point is `local + viewportOrigin - scroll` - the inverse of the capture translation - and the
    /// assertion is what makes "the pointer was really over the band" a measured fact rather than an
    /// assumption: a native hit test refuses a point outside the scroll clip, so an unverified point could
    /// make an inert clause pass for the wrong reason.
    /// </summary>
    private static Vector2 BringIntoViewAndGetPagePoint(UiHost host, float box, Rect localBand)
    {
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(box, PageBoxHeight));
        Assert(snapshot.Viewports.TryGetValue("content-scroll", out Rect viewport),
            "the centre column must publish its viewport for a real pointer event");
        Assert(snapshot.ScrollContents.TryGetValue("content-scroll", out Rect content),
            "the centre column must publish its content box for a real pointer event");
        float maxScroll = Mathf.Max(0f, content.height - viewport.height);
        float wanted = Mathf.Clamp(localBand.y - viewport.height * 0.5f, 0f, maxScroll);
        Program.SetScrollPositionById(host.Session, "content-scroll", new Vector2(0f, wanted));

        snapshot = host.MeasureAndArrange(new Vector2(box, PageBoxHeight));
        viewport = snapshot.Viewports["content-scroll"];
        Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
        var point = new Vector2(
            localBand.x + localBand.width * 0.5f + viewport.x - scroll.x,
            localBand.y + localBand.height * 0.5f + viewport.y - scroll.y);
        Assert(point.y >= viewport.y + 1f && point.y <= viewport.yMax - 1f
            && point.x >= viewport.x + 1f && point.x <= viewport.xMax - 1f,
            "the lane must really bring the target band inside the scroll viewport before a real event"
            + " (point " + Num(point.x) + "," + Num(point.y) + ", viewport " + Num(viewport.x) + ".."
            + Num(viewport.xMax) + " / " + Num(viewport.y) + ".." + Num(viewport.yMax) + ")");
        return point;
    }

    /// <summary>Every button carrying the widget's declared dropdown width: the popup triggers.</summary>
    private static List<Rect> TriggerRects(List<Rect> buttons)
    {
        return buttons
            .Where(rect => Math.Abs(rect.width - TriggerWidth) <= TriggerTolerance)
            .OrderBy(rect => rect.y).ThenBy(rect => rect.x)
            .ToList();
    }

    /// <summary>The layer segment: the three equal cells drawn ABOVE the first dropdown trigger.</summary>
    private static List<Rect> LayerButtons(List<Rect> buttons, List<Rect> triggers)
    {
        float firstTriggerY = triggers.Count > 0 ? triggers.Min(rect => rect.y) : float.MaxValue;
        return buttons
            .Where(rect => rect.width > 40f && rect.width <= 144f && rect.y < firstTriggerY - 0.5f)
            .OrderBy(rect => rect.y).ThenBy(rect => rect.x)
            .ToList();
    }

    /// <summary>The stepper minus/plus cells: the narrowest button bands the card draws.</summary>
    private static List<Rect> SmallButtons(List<Rect> buttons)
    {
        return buttons
            .Where(rect => rect.width <= 21f)
            .OrderBy(rect => rect.y).ThenBy(rect => rect.x)
            .ToList();
    }

    /// <summary>The mood reset bands: wider than a stepper cell and drawn below the action-scope rows.</summary>
    private static List<Rect> ResetButtons(List<Rect> buttons)
    {
        List<Rect> triggers = TriggerRects(buttons);
        float firstTriggerY = triggers.Count > 0 ? triggers.Min(rect => rect.y) : float.MaxValue;
        return buttons
            .Where(rect => rect.width > 40f && rect.width <= 144f && rect.y > firstTriggerY - 0.5f)
            .OrderBy(rect => rect.y).ThenBy(rect => rect.x)
            .ToList();
    }

    /// <summary>The same content-local rect shifted down by the reason band the blocked card adds above rows.</summary>
    private static Rect Translate(Rect rect, float delta)
    {
        return new Rect(rect.x, rect.y + delta, rect.width, rect.height);
    }

    private static void RealClick(UiHost host, float box, Vector2 pagePoint)
    {
        RealMouseDown(host, box, pagePoint);
        RealMouseUp(host, box, pagePoint);
    }

    private static void RealMouseDown(UiHost host, float box, Vector2 pagePoint)
    {
        Program.DrawWithEvent(host, new Rect(0f, 0f, box, PageBoxHeight), EventType.MouseDown, pagePoint);
    }

    private static void RealMouseDrag(UiHost host, float box, Vector2 pagePoint)
    {
        Program.DrawWithEvent(host, new Rect(0f, 0f, box, PageBoxHeight), EventType.MouseDrag, pagePoint);
    }

    private static void RealMouseUp(UiHost host, float box, Vector2 pagePoint)
    {
        Program.DrawWithEvent(host, new Rect(0f, 0f, box, PageBoxHeight), EventType.MouseUp, pagePoint);
    }

    /// <summary>Drops any IMGUI hot control the real events left behind, so one clause cannot steer the next.</summary>
    private static UiValueState FirstStepperState(UiHost host)
    {
        UiNode node = host.Session.GetNodeByElementId(TuningCardId)
            ?? throw new InvalidOperationException("Missing tuning state owner.");
        return host.Session.GetOrCreateValueState(node.Id, FirstStepperElementId);
    }

    private static void ReleasePointer()
    {
        GUIUtility.hotControl = 0;
        Event.current = null;
    }

    /// <summary>Every button outside <paramref name="excluded"/> is clicked, every slider dragged and every
    /// number field committed, in one pass. The excluded rects are the dropdown triggers (which open popups
    /// that would shadow the controls painted after them) and the layer segment (asserted separately).</summary>
    private static void ClickEveryControl(UiHost host, float box, List<Rect> excluded)
    {
        try
        {
            SetButtonOverride(rect => host.Session.ActiveNode.ElementId == TuningCardId
                && !excluded.Any(ex => RectMatches(rect, ex)));
            SetSliderOverride((rect, value, min, max) => host.Session.ActiveNode.ElementId == TuningCardId
                ? (value <= min + 0.0001f ? min + 0.05f : min) : value);
            SetTextFieldOverride((rect, text) => host.Session.ActiveNode.ElementId == TuningCardId ? "0.42" : text);
            host.Session.ClosePopup();
            host.DrawChecked(new Rect(0f, 0f, box, PageBoxHeight));
        }
        finally
        {
            ClearOverrides();
        }
    }

    /// <summary>One real click on one previously captured control rect, through the override seam.</summary>
    private static void ClickOneControl(UiHost host, float box, Rect target)
    {
        try
        {
            SetButtonOverride(rect => host.Session.ActiveNode.ElementId == TuningCardId && RectMatches(rect, target));
            host.Session.ClosePopup();
            host.DrawChecked(new Rect(0f, 0f, box, PageBoxHeight));
        }
        finally
        {
            ClearOverrides();
        }
    }

    private static void ResetRecorders(RecordingSettingsSource source)
    {
        source.LastTuningLayer = null;
        source.LastTuningDomainRace = null;
        source.LastTuningDomainTarget = null;
        source.LastActionKey = null;
        source.LastActionScope = null;
        source.LastMood = null;
        source.LastMoodFactor = null;
        source.LastMoodValue = null;
        source.LastMoodPresetReset = null;
        source.LastMoodPresetResetCount = 0;
    }

    private static void AssertMoodRecordUnchanged(MoodTuningRecord record, string what)
    {
        Assert(record.mood == SqueakMood.Good
            && record.raceDefName.Length == 0 && record.xenotypeDefName.Length == 0
            && record.sourcePresetDefName == "us.harness.global.preset"
            && record.hasPitchFactor && Math.Abs(record.pitchFactor - 1.25f) < 0.0001f
            && record.hasVolumeFactor && Math.Abs(record.volumeFactor - 0.75f) < 0.0001f
            && record.hasPitchJitter
            && Math.Abs(record.pitchJitter.min - 1.2f) < 0.0001f
            && Math.Abs(record.pitchJitter.max - 1.6f) < 0.0001f,
            what + " must leave the GLOBAL mood record untouched; got mood=" + record.mood
            + " race='" + record.raceDefName + "' xeno='" + record.xenotypeDefName
            + "' source='" + record.sourcePresetDefName + "'"
            + " pitch=" + Num(record.pitchFactor) + "/" + record.hasPitchFactor
            + " volume=" + Num(record.volumeFactor) + "/" + record.hasVolumeFactor
            + " jitter=" + Num(record.pitchJitter.min) + ".." + Num(record.pitchJitter.max) + "/"
            + record.hasPitchJitter);
    }

    /// <summary>
    /// The model's own identity predicate, invoked through reflection. It is private, and the write path that
    /// uses it cannot complete in this harness (the class header records why), so the probe is the honest way
    /// to pin "layer 0 stays legitimate" without a game-only call; the lane labels it a GUARD for that reason.
    /// </summary>
    private static bool MoodLayerHasIdentity(int layer, string race, string xeno)
    {
        MethodInfo? method = typeof(VoicePacksPageModel).GetMethod(
            "MoodLayerHasIdentity", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(method != null,
            "REFLECTION BLOCKER: VoicePacksPageModel.MoodLayerHasIdentity is missing; the boundary this lane"
            + " pins has been renamed or removed");
        var state = new VoicePacksPageState
        {
            TuningLayer = layer,
            TuningRaceDefName = race,
            TuningXenotypeDefName = xeno,
        };
        object? result = method!.Invoke(null, new object[] { state });
        return result is bool value && value;
    }

    private static bool RectMatches(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) <= RectEpsilon && Math.Abs(a.y - b.y) <= RectEpsilon
            && Math.Abs(a.width - b.width) <= RectEpsilon && Math.Abs(a.height - b.height) <= RectEpsilon;
    }

    private static string Describe(Rect rect)
    {
        return "(" + Num(rect.x) + "," + Num(rect.y) + " " + Num(rect.width) + "x" + Num(rect.height) + ")";
    }

    private static string DescribeAll(List<Rect> rects)
    {
        return rects.Count == 0 ? "(none)" : string.Join(" ", rects.ConvertAll(rect => Describe(rect)));
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        return reports.Count == 0
            ? "(none)"
            : string.Join(" | ", reports.ConvertAll(report =>
                (string.IsNullOrEmpty(report.ElementPath) ? "(unscoped)" : report.ElementPath) + " " + report.Axis
                + " needs " + Num(report.Needed) + " has " + Num(report.Available)));
    }

    private static string Num(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    // --- the carrier's override seams, reflection only (same shape as MoodLayoutFocusedTests) --------------

    private static readonly FieldInfo ButtonOverrideField =
        RequireField("ButtonOverride", typeof(Func<Rect, bool>));
    private static readonly FieldInfo SliderOverrideField =
        RequireField("SliderOverride", typeof(Func<Rect, float, float, float, float>));
    private static readonly FieldInfo TextFieldOverrideField =
        RequireField("TextFieldOverride", typeof(Func<Rect, string, string>));

    private static void SetButtonOverride(Func<Rect, bool>? value) => SetField(ButtonOverrideField, value);

    private static void SetSliderOverride(Func<Rect, float, float, float, float>? value) =>
        SetField(SliderOverrideField, value);

    private static void SetTextFieldOverride(Func<Rect, string, string>? value) =>
        SetField(TextFieldOverrideField, value);

    private static void ClearOverrides()
    {
        SetButtonOverride(null);
        SetSliderOverride(null);
        SetTextFieldOverride(null);
    }

    private static FieldInfo RequireField(string name, Type fieldType)
    {
        FieldInfo? field = typeof(UiNative).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: UiNative." + name + " is not accessible via reflection on net472.");
        }

        if (field.FieldType != fieldType)
        {
            throw new InvalidOperationException(
                "UiNative." + name + " has unexpected type " + field.FieldType + "; expected " + fieldType);
        }

        return field;
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
                "REFLECTION BLOCKER: cannot set UiNative." + field.Name + " on net472.", ex);
        }
    }

    /// <summary>Every text measurement on one channel, so "the sentence was drawn" is asserted against the
    /// outlet the page really painted through.</summary>
    private sealed class DrawnTextMetrics : ITextMetrics
    {
        private readonly Program.StubMetrics inner = new();
        private readonly HashSet<string> measured = new(StringComparer.Ordinal);

        public bool Measured(string text) => measured.Contains(text ?? "");

        public void Clear() => measured.Clear();

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

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("XenotypeEmptyTargetLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
