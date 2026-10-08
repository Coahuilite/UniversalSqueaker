using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// US-ESC1 + US-UI1 (FL-IC2 integration): the tree cancel layers, the cancelled projection marks, the
/// F06 area-scoped tuning presentation and the F07 measured reset line, driven on the REAL embedded
/// Schema2 page against the frozen carrier.
/// <para>
/// Honest limits: (1) the production <c>ResolveSelectedDomain</c> guard line runs only with a non-empty
/// catalog, and the catalog snapshot's constructor is internal with no InternalsVisibleTo, so the packs
/// return-to-null behaviour is proven here through the SAME production <c>VoicePacksPageModel</c> statics
/// and state rules the guard reads (the fake view mirrors the mark exactly as production projects it);
/// a real catalog's first-entry default versus cancelled state is a named item of the short human pass.
/// (2) The two-press Esc lane drives the FL Verse DOUBLE: its eligibility AND and GetsInput are the
/// frozen contract's reading, not the real window stack; which window a real multi-window stack hands
/// the key to remains human evidence.
/// </para>
/// </summary>
internal static class CancelLayerLaneTests
{
    private const float PageWidth = 1280f;
    private const float PageHeight = 720f;

    internal static void RunAll()
    {
        PacksReturnLayerOnePressOneLayer();
        ReSelectAndWorkspaceSwitchRebuildTheBranch();
        ExplicitTargetsExpireAndRealClicksClimb();
        TuningLadderIsScopedToTheVisibleBranch();
        PureLadderStaticsRoundTrip();
        F06FallbackAreaDropsTheTuningContext();
        F07MeasuredResetLineGrowsInsteadOfOverdrawing();
        WindowLayeringRelationships();
        DiagnosticsTwoPressEscThroughTheNativeStackDouble();
        Console.WriteLine("[us-cancel] packs return = one press then decline-unconsumed; help at page-root;"
            + " tuning ladder scoped to the visible branch (fallback: entry>table>context-folds-stale;"
            + " action: row>context); explicit targets expire off-play, real clicks climb the node chain;"
            + " two-press Esc armed + close-branch through the stack double with native eligibility set");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("CancelLayer lane: " + message);
    }

    private static void ArrangeAndDraw(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.DrawWithPointer(host, new Rect(0f, 0f, PageWidth, PageHeight), new Vector2(PageWidth / 2f, 140f));
    }

    /// <summary>One Escape press through the host's ladder door (what the shell calls before Verse).
    /// Returns the answered flag and the event, so consumption is observable, not assumed.</summary>
    private static (bool answered, Event ev) PressEscape(UiHost host)
    {
        Event esc = Event.KeyboardEvent("dummy");
        esc.type = EventType.KeyDown;
        esc.keyCode = KeyCode.Escape;
        Event.current = esc;
        try
        {
            return (host.TryHandleCancel(), esc);
        }
        finally
        {
            Event.current = null;
        }
    }

    private static VoicePacksViewState BuildPacksView(out RecordingSettingsSource fake)
    {
        fake = new RecordingSettingsSource { RichData = true };
        return fake.BuildView();
    }

    private static void PacksReturnLayerOnePressOneLayer()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);
        host.Bindings.Set("help-open", true);   // the layer BELOW the packs return, for the climb to find next
        ArrangeAndDraw(host);

        // The state the return must NOT touch (§4.1: filters stay).
        host.Bindings.Set("race-search-text", "hum");
        host.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.EnabledOnly, true));
        ArrangeAndDraw(host);

        Assert(host.Session.SetCancelTarget("checklist-card"),
            "the packs enable card must be materialised as the walk's subject");
        UiNode? subject = host.Session.CancelTargetNode;
        Assert(subject != null && string.Equals(subject.CancelBindingKey, "cancel-domain-selection", StringComparison.Ordinal),
            "the manifest's CancelBind on checklist-card must be published on the node (the word only works "
            + "when the engine reads it), got '" + (subject?.CancelBindingKey ?? "null") + "'");

        (bool answered, Event first) = PressEscape(host);
        Assert(answered && first.type == EventType.Used,
            "press 1 answers the packs return layer and the shell consumes the key");
        Assert(fake.ViewState.DomainSelectionCanceled,
            "press 1 exits the operation domain into the explicit cancelled state");
        Assert(fake.BuildView().SelectedDomain == null,
            "the cancelled projection answers NO domain - it must not re-pick the first row");
        Assert(fake.ViewState.HelpPanelOpen,
            "one press, ONE layer: the help layer below stays open while the return was answered");
        Assert(string.Equals(fake.ViewState.RaceSearchText, "hum", StringComparison.Ordinal)
                && fake.LastDomainFilterKind == SqueakDomainFilterKind.EnabledOnly && fake.LastDomainFilterFlag == true,
            "the filter conditions survive the return (§4.1) - the domain search still reads 'hum' and the "
            + "Enabled chip write recorded before the presses is unchanged; the return touched neither");

        (bool answered2, Event second) = PressEscape(host);
        Assert(answered2 && second.type == EventType.Used && !fake.ViewState.HelpPanelOpen,
            "press 2 climbs to the page-root help layer and closes the panel");
        Assert(!fake.ViewState.DomainSelectionCanceled
                || fake.BuildView().SelectedDomain == null,
            "closing help must not silently re-select anything either");

        (bool answered3, Event third) = PressEscape(host);
        Assert(!answered3 && third.type != EventType.Used,
            "press 3: every declared layer declined, so nothing consumed the key - the settings main layer "
            + "keeps Verse's meaning of Escape (only the root closes), which is the next press's job");
    }

    private static void ReSelectAndWorkspaceSwitchRebuildTheBranch()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);

        string raceRowKey = fake.BuildView().Races[0].RaceDefName;
        Assert(fake.BuildView().SelectedDomain.HasValue,
            "first entry keeps the accepted initial default (the projection may auto-select)");

        host.Session.SetCancelTarget("checklist-card");
        (bool answered, _) = PressEscape(host);
        Assert(answered && fake.ViewState.DomainSelectionCanceled, "setup: the return was taken");

        host.Bindings.Invoke("select-domain", raceRowKey);
        Assert(!fake.ViewState.DomainSelectionCanceled,
            "a fresh selection replaces the cancel mark (the user chose again, the ladder is not stuck)");
        Assert(fake.BuildView().SelectedDomain.HasValue,
            "after re-selection the card shows the chosen domain");

        host.Session.SetCancelTarget("checklist-card");
        PressEscape(host);
        Assert(fake.ViewState.DomainSelectionCanceled, "setup: cancelled again");

        host.Bindings.Invoke("set-tab", "Tuning");
        Assert(!fake.ViewState.DomainSelectionCanceled && !fake.ViewState.FallbackTableCanceled,
            "switching workspace RE-ESTABLISHES the active branch (§4.1): last page's return marks do not "
            + "carry over, so each page opens with its first-entry default again");
        host.Bindings.Invoke("set-tab", "Packs");
        Assert(fake.BuildView().SelectedDomain.HasValue,
            "the rebuilt branch auto-selects once more (cancel was this branch's, not the page's)");
    }

    private static void TuningLadderIsScopedToTheVisibleBranch()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        fake.MirrorTuningWrites = true; // set-tuning-layer must really move the shared page state
        fake.FallbackTableAutoRace = "RaceA"; // what the projection would show for the table (first entry)
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Tuning");
        ArrangeAndDraw(host);

        Assert(host.Session.SetCancelTarget("scope-tree"), "the tuning composite must be a material subject");
        UiNode? subject = host.Session.CancelTargetNode;
        Assert(subject != null && string.Equals(subject.CancelBindingKey, "cancel-tuning-target", StringComparison.Ordinal),
            "the scope-tree element carries the CancelBind the manifest declares, got '"
            + (subject?.CancelBindingKey ?? "null") + "'");

        // PATH A (PM observation 10): the fallback branch WITH a row target left over from another area.
        // The stale selection is INVISIBLE here, so it must NOT be a return layer of its own - the ladder
        // is entry -> table -> context, and the context exit folds the stale state in as ONE step.
        host.Bindings.Set("tuning-area", 2);
        fake.ViewState.FallbackSelectedEntryAction = "Call";
        host.Bindings.Set("tuning-selected-action", "Eat");
        host.Bindings.Invoke("set-tuning-layer", 2);
        ArrangeAndDraw(host);

        (bool p1, _) = PressEscape(host);
        Assert(p1 && fake.ViewState.FallbackSelectedEntryAction.Length == 0
                && fake.ViewState.TuningSelectedAction == "Eat" && !fake.ViewState.FallbackTableCanceled,
            "press 1 = the fallback entry only; the invisible stale action is not a step here");
        (bool p2, _) = PressEscape(host);
        Assert(p2 && fake.ViewState.FallbackTableCanceled && fake.BuildView().FallbackSelectedRace.Length == 0,
            "press 2 = return the race table; the projection must not re-pick the first race");
        Assert(fake.BuildView().FallbackRaces.Count > 0,
            "the table LIST is untouched: cancellation is a selection state, never a data change");
        (bool p3, _) = PressEscape(host);
        Assert(p3 && fake.ViewState.TuningLayer == 0 && fake.ViewState.TuningArea == 0
                && fake.ViewState.TuningSelectedAction.Length == 0,
            "press 3 = leave the tuning context to the page default, folding the stale row target in - "
            + "no invisible extra layer swallows a press");
        (bool p4, Event fourth) = PressEscape(host);
        Assert(!p4 && fourth.type != EventType.Used,
            "press 4: every step declined, the key is left for the layers above (root)");
        // The context exit rests on the Global layer with an EMPTY domain identity, at the STATE level
        // the real BuildTuningDomains reads. The fake's view hard-codes the identity column, so this is
        // asserted on the shared state, not the projected view - and it is what makes PM observation 6
        // moot: BuildTuningDomains' layer-0 branch (read at source) sets race="" xeno="" and returns an
        // EMPTY option list, so there is no first item to auto-pick and the third projection point needs
        // no cancelled mark of its own. The completed real-catalog layer-0 projection is the human pass.
        Assert(fake.ViewState.TuningLayer == 0
                && fake.ViewState.TuningRaceDefName.Length == 0
                && fake.ViewState.TuningXenotypeDefName.Length == 0,
            "after the context exit the page rests on the Global layer with an empty domain identity");

        // PATH B: in the ACTION area the row editor IS visible, so it IS its own step before the context.
        host.Bindings.Invoke("set-tuning-layer", 1);
        host.Bindings.Set("tuning-selected-action", "Eat");
        ArrangeAndDraw(host);
        (bool q1, _) = PressEscape(host);
        Assert(q1 && fake.ViewState.TuningSelectedAction.Length == 0 && fake.ViewState.TuningLayer == 1,
            "press 1 in the action area closes the visible row editor and leaves the layer/context");
        (bool q2, _) = PressEscape(host);
        Assert(q2 && fake.ViewState.TuningLayer == 0 && fake.ViewState.TuningArea == 0,
            "press 2 = the layer/domain/area context step");
        (bool q3, Event third) = PressEscape(host);
        Assert(!q3 && third.type != EventType.Used, "then the ladder declines - the root keeps the next press");

        // The return ladder must never have written a persisted value: the fake settings side is not
        // wired here (no ToggleVoicePack/queue), and each step above asserts the exact field it moved.
        Assert(fake.LastPackScope == null,
            "no cancel step may write an enable selection");
    }

    /// <summary>
    /// PM observation 9: declaring CancelBind is not the same as the key REACHING the layer. (a) An
    /// explicit business target must die with the branch that hid it - set on Packs, survive nothing
    /// across a workspace pass. (b) A real press on a SIBLING band (the footer help switch - not under
    /// any page section) must climb the node parent chain to the page-root help layer on its own, with
    /// no SetCancelTarget anywhere: that is the reachability proof the declaration alone cannot give.
    /// </summary>
    private static void ExplicitTargetsExpireAndRealClicksClimb()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);

        Assert(host.Session.SetCancelTarget("checklist-card") && host.Session.CancelTargetNode != null,
            "setup: the explicit target is held while its branch is on the page");
        host.Bindings.Invoke("set-tab", "Tuning");
        ArrangeAndDraw(host);   // the pass boundary reconciles the interaction records
        Assert(host.Session.CancelTargetNode == null && host.Session.LastInteractionNode == null,
            "a workspace switch must not keep the hidden branch's node as an active cancel subject");
        (bool stale, Event se) = PressEscape(host);
        Assert(!stale && se.type != EventType.Used,
            "the key cannot be answered through the stale subject - nothing invisible is reachable");

        host.Bindings.Invoke("set-tab", "Packs");
        UiLayoutSnapshot snap = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.DrawWithPointer(host, new Rect(0f, 0f, PageWidth, PageHeight), new Vector2(PageWidth / 2f, 140f));
        Assert(!fake.ViewState.HelpPanelOpen, "the help layer starts closed");
        if (!snap.RectById.TryGetValue("help-toggle", out Rect toggle))
        {
            throw new Exception("CancelLayer lane: the footer help switch did not materialise a rect");
        }

        Vector2 press = new(toggle.x + toggle.width / 2f, toggle.y + toggle.height / 2f);
        Program.DrawWithEvent(host, new Rect(0f, 0f, PageWidth, PageHeight), EventType.MouseDown, press);
        Program.DrawWithEvent(host, new Rect(0f, 0f, PageWidth, PageHeight), EventType.MouseUp, press);
        if (!fake.ViewState.HelpPanelOpen)
        {
            throw new Exception("CancelLayer lane: the real click did not open the help panel");
        }

        Assert(host.Session.LastInteractionNode != null,
            "a real press must record its subject - the walk starts from the interaction, not the last draw");
        (bool climbed, Event ce) = PressEscape(host);
        Assert(climbed && ce.type == EventType.Used && !fake.ViewState.HelpPanelOpen,
            "from the footer sibling the Escape climbed the actual parent chain to the page-root help "
            + "layer and closed it - reachability without any SetCancelTarget");
        (bool after, Event ae) = PressEscape(host);
        Assert(!after && ae.type != EventType.Used, "and the same press never repeats: help closed, decline");
    }

    private static void PureLadderStaticsRoundTrip()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        VoicePacksPageState state = fake.ViewState; // the view and the state must be ONE pairing
        VoicePacksViewState view = fake.BuildView();

        Assert(VoicePacksPageModel.CanCancelDomainSelection(state, view),
            "with a shown domain and no mark, the packs layer can answer");
        VoicePacksPageModel.CancelDomainSelection(state);
        Assert(state.DomainSelectionCanceled
                && !VoicePacksPageModel.CanCancelDomainSelection(state, view),
            "after the return the veto is honest: TryInvokeCommand would climb past");
        VoicePacksPageModel.SelectDomain(state, SqueakVoicePackScope.Race, "human", "");
        Assert(!state.DomainSelectionCanceled && VoicePacksPageModel.CanCancelDomainSelection(state, view),
            "a new selection re-arms the layer");

        BuildPacksView(out RecordingSettingsSource fake2);
        fake2.FallbackTableAutoRace = "RaceA";
        // The view under test is the fixture's OWN projection of its OWN page state - the ladder's
        // area/race/layer steps come from the view, the entry/action steps from the state, exactly as
        // in production. A detached VoicePacksPageState here would test a pairing that cannot exist.
        VoicePacksPageState s2 = fake2.ViewState;
        s2.TuningArea = 2;
        s2.FallbackSelectedEntryAction = "Call";
        s2.TuningSelectedAction = "Eat";   // invisible here - must NOT act as its own step (PM 10)
        s2.TuningLayer = 1;
        VoicePacksViewState v2 = fake2.BuildView();
        Assert(VoicePacksPageModel.CanCancelTuningTarget(s2, v2), "the tuning layer can answer");
        VoicePacksPageModel.CancelTuningTarget(s2, v2);
        Assert(s2.FallbackSelectedEntryAction.Length == 0 && s2.FallbackTableCanceled == false
                && s2.TuningSelectedAction == "Eat" && s2.TuningLayer == 1,
            "step 1 = entry only");
        v2 = fake2.BuildView();
        VoicePacksPageModel.CancelTuningTarget(s2, v2);
        Assert(s2.FallbackTableCanceled && s2.FallbackSelectedRace.Length == 0, "step 2 = table");
        v2 = fake2.BuildView();
        VoicePacksPageModel.CancelTuningTarget(s2, v2);
        Assert(s2.TuningLayer == 0 && s2.TuningArea == 0 && s2.TuningSelectedAction.Length == 0,
            "step 3 = context exit, folding the stale action in - not a fourth invisible press");
        v2 = fake2.BuildView();
        Assert(!VoicePacksPageModel.CanCancelTuningTarget(s2, v2),
            "then the whole ladder vetoes - the root keeps the next press");

        // The same statics on the ACTION area: the row target IS visible and IS its own step.
        BuildPacksView(out RecordingSettingsSource fake3);
        VoicePacksPageState s3 = fake3.ViewState;
        s3.TuningArea = 0;
        s3.TuningLayer = 1;
        s3.TuningSelectedAction = "Eat";
        VoicePacksViewState v3 = fake3.BuildView();
        VoicePacksPageModel.CancelTuningTarget(s3, v3);
        Assert(s3.TuningSelectedAction.Length == 0 && s3.TuningLayer == 1,
            "in the action area the row editor is its own visible step");
        v3 = fake3.BuildView();
        VoicePacksPageModel.CancelTuningTarget(s3, v3);
        Assert(s3.TuningLayer == 0, "then the context step");
        v3 = fake3.BuildView();
        Assert(!VoicePacksPageModel.CanCancelTuningTarget(s3, v3), "and the branch rests at the default");
    }

    private static float ScopeTreeHeight(RecordingSettingsSource fake, Program.StubMetrics metrics)
    {
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Tuning");
        host.Session.BumpContentRevision();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        float height = snapshot.RectById.TryGetValue("scope-tree", out Rect rect) ? rect.height : 0f;
        host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        return height;
    }

    private static void F06FallbackAreaDropsTheTuningContext()
    {
        // With the pre-fix shape the fallback area was topped by the shared layer/domain rows and the
        // empty-xenotype reason band (F06). The fix gates that context to the action/mood areas, so the
        // empty-target state must change NOTHING about the fallback area's height, while the mood area
        // keeps its taller context; within the action/mood areas the XG1 row order is unchanged.
        var metrics = new Program.StubMetrics();
        var normal = new RecordingSettingsSource { RichData = true };
        normal.ViewState.TuningArea = 2;
        float fallbackNormal = ScopeTreeHeight(normal, metrics);

        var emptyXeno = new RecordingSettingsSource { RichData = true, EmptyXenotypeTarget = true };
        emptyXeno.ViewState.TuningArea = 2;
        float fallbackEmpty = ScopeTreeHeight(emptyXeno, metrics);
        Assert(Math.Abs(fallbackEmpty - fallbackNormal) < 0.01f,
            "the fallback area answers to its own single race target only: the layer/domain rows and the "
            + "empty-xenotype reason belong to the action/mood areas, so the two fallback heights must be "
            + "identical, got " + fallbackNormal + " vs " + fallbackEmpty);

        normal.ViewState.TuningArea = 1; // mood
        float moodNormal = ScopeTreeHeight(normal, metrics);
        emptyXeno.ViewState.TuningArea = 1;
        float moodEmpty = ScopeTreeHeight(emptyXeno, metrics);
        Assert(moodEmpty > moodNormal + 30f,
            "the action/mood areas DO present their layer/domain context (the reason band plus the layer "
            + "rows grow the mood card), got " + moodNormal + " vs " + moodEmpty);
        Console.WriteLine("[f06-gate] fallbackArea empty-vs-normal height " + fallbackEmpty + "/" + fallbackNormal
            + " (identical: layer/domain/reason gated out), mood " + moodEmpty + "/" + moodNormal
            + " (context kept)");
    }

    private static void F07MeasuredResetLineGrowsInsteadOfOverdrawing()
    {
        Dictionary<string, string> english = Program.ReadKeyedTable("English");
        var fits = new Dictionary<string, string>(english, StringComparer.Ordinal)
        {
            ["US.Tuning.ResetToPreset"] = "Reset"
        };
        var wrapping = new Dictionary<string, string>(english, StringComparer.Ordinal)
        {
            ["US.Tuning.ResetToPreset"] = new string('W', 120)
        };

        var reports = new List<UiOverflowReport>();
        var metrics = new Program.StubMetrics();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            float fitsHeight = ResetLineHeight(fits, metrics);
            float wrapsHeight = ResetLineHeight(wrapping, metrics);
            Assert(fitsHeight > 0f && wrapsHeight > fitsHeight,
                "the reset slot must grow the card when its content cannot fit inline, got "
                + fitsHeight + "px / " + wrapsHeight + "px");
            // Two multiplier lines move to their own line: exactly 2 * (RowHeight 28 + RowGap 2).
            Assert(Math.Abs(wrapsHeight - fitsHeight - 60f) < 0.01f,
                "measure and draw agree by construction: the wrapped shape adds exactly the two reset "
                + "lines it paints (expected +60px, got +" + (wrapsHeight - fitsHeight) + ")");
            Assert(reports.Count == 0,
                "the reset phrase is never overdrawn or clipped any more: " + Describe(reports));
            Console.WriteLine("[f07-reset-line] fits=" + fitsHeight + " wrapped=" + wrapsHeight
                + " delta=" + (wrapsHeight - fitsHeight) + " (exactly the two own-line rows)"
                + " overflowReports=" + reports.Count);
        }
        finally
        {
            UiFitAudit.Detach();
            Program.SetTranslatorResolver(null);
        }
    }

    private static float ResetLineHeight(Dictionary<string, string> table, Program.StubMetrics metrics)
    {
        Program.SetTranslatorResolver(table);
        var fake = new RecordingSettingsSource
        {
            RichData = true,
            TuningActionScopes = new[]
            {
                new ActionScopeRowView(
                    "Eat", "Eat", ActionScopeGroup.SystemOrEvent, SqueakActionScope.AnyOccurrence, SqueakAction.Eat,
                    hasOwnScope: true, effectiveScope: SqueakActionScope.AnyOccurrence,
                    hasOwnInterval: true, ownInterval: 1.2f, effectiveInterval: 1.2f, intervalSourceLayer: 1,
                    hasPresetAnchor: true, presetResetReady: true, resetPresetTarget: "Harness Baseline")
            }
        };
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics);
        host.Bindings.Invoke("set-tab", "Tuning");
        host.Bindings.Set("tuning-selected-action", "Eat");
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        float height = snapshot.RectById.TryGetValue("scope-tree", out Rect rect) ? rect.height : 0f;
        host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        return height;
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        return string.Join(" | ", reports.ConvertAll(
            r => "(" + (r.ElementPath.Length == 0 ? "unscoped" : r.ElementPath) + " " + r.Axis
            + " " + r.Needed + "/" + r.Available + ")"));
    }

    /// <summary>
    /// F01/US-UI1 window wiring: the settings main window stays Dialog with its absorption (its own
    /// choice), the dev/diagnostic TOOL windows sit on SubSuper so a click on the main window can
    /// never stand between them and their input (the recorded witness: same-layer click-to-front +
    /// absorb = the panel loses GetsInput), and BOTH confirmation dialogs sit on Super - the
    /// confirmation window outranks tool and main window alike. The order is the real Verse enum
    /// measured from the 1.6.4871 reference (GameUI &lt; Dialog &lt; SubSuper &lt; Super); which window
    /// the live stack ultimately serves remains the short-pass observation, the wiring is here.
    /// </summary>
    private static void WindowLayeringRelationships()
    {
        var stack = new WindowStack();
        typeof(Find).GetProperty("WindowStack")!.SetValue(null, stack);
        var assembly = typeof(UsKernelSettingsHost).Assembly;

        Type devType = assembly.GetType("UniversalSqueaker.UI.Dev.UsDevPanelWindow", throwOnError: true)!;
        var dev = (Window)Activator.CreateInstance(devType, nonPublic: true)!;
        Assert(dev.layer == WindowLayer.SubSuper,
            "the developer panel must sit above the Dialog settings window, got " + dev.layer);

        Type panelType = assembly.GetType("UniversalSqueaker.SqueakDiagnosticsPanel", throwOnError: true)!;
        var diag = (Window)Activator.CreateInstance(panelType)!;
        Assert(diag.layer == WindowLayer.SubSuper,
            "the diagnostics panel coexists on the same tool layer, got " + diag.layer);
        Type detailType = assembly.GetType("UniversalSqueaker.SqueakDiagnosticsDetailWindow", throwOnError: true)!;
        var detail = (Window)Activator.CreateInstance(detailType, new object[] { new Pawn() })!;
        Assert(detail.layer == WindowLayer.SubSuper, "and the detail windows with it, got " + detail.layer);

        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            bool ran = false;
            UsConfirmWindow? flow = UsConfirmWindow.Open("t", "m", "go", () => ran = true, stack);
            Assert(flow?.OpenedWindow != null, "the real confirmation window must have opened on the double");
            Assert(flow!.OpenedWindow!.layer == WindowLayer.Super,
                "the VF1 confirmation outranks the tools AND the main window, got " + flow.OpenedWindow!.layer);
            flow.DialogBindings.Invoke("confirm-no");
            Assert(flow.OpenedWindow == null && !ran, "and the answered cancel closed it, action dropped");

            var remix = new RemixConfirmationFlow(new WindowStack());
            Assert(remix.RequestRemix(() => ran = true), "the Remix flow opens its dialog on request");
            Assert(remix.OpenedWindow != null && remix.OpenedWindow.layer == WindowLayer.Super,
                "the Remix double confirmation sits on the same top layer as any other question, got "
                + (remix.OpenedWindow?.layer.ToString() ?? "no window"));
            remix.Abort();
            Assert(remix.OpenedWindow == null && !ran, "Abort answers as cancel - never a staged commit");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            typeof(Find).GetProperty("WindowStack")!.SetValue(null, new WindowStack());
        }

        Console.WriteLine("[f01-layers] tool=SubSuper(main=Dialog, absorbed) confirmations=Super; "
            + "settings main window untouched; dev-panel target registration unchanged");
    }

    private static void DiagnosticsTwoPressEscThroughTheNativeStackDouble()
    {
        var stack = new WindowStack();
        // The double's Find.WindowStack carries a SETTER the Krafs reference does not expose, so the
        // injection goes through reflection - the same door the focusedWindow reads already use.
        typeof(Find).GetProperty("WindowStack")!.SetValue(null, stack);

        Type panelType = typeof(UsKernelSettingsHost).Assembly
            .GetType("UniversalSqueaker.SqueakDiagnosticsPanel", throwOnError: true)!;
        var panel = (Window)Activator.CreateInstance(panelType)!;
        panel.windowRect = new Rect(100f, 100f, 460f, 120f);
        stack.Add(panel);

        Assert(panel.forceCatchAcceptAndCancelEventEvenIfUnfocused,
            "the migrated windows must set the native-eligibility Verse field themselves: with "
            + "closeOnCancel=false nothing else ever asks them for the key (frozen handoff §5)");
        Assert(!panel.closeOnCancel, "the two-press arm, not the native close, owns this window's Esc");

        // Press 1 through the stack's key dispatch (the stub is the FL double; see the header's limit):
        // arm only. The private timer field is the observable of WHICH branch ran.
        System.Reflection.FieldInfo panelArm = panelType.GetField("escArmedUntil",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert((float)panelArm.GetValue(panel)! < 0f, "the arm starts expired");
        (bool _, Event first) = PumpEscapeThrough(panel, stack);
        Assert(first.type == EventType.Used,
            "the answer must consume: the pre-migration override left the key standing and the game's "
            + "late second check could undo a second layer in the same pass");
        Assert(stack.IsOpen(panel) && (float)panelArm.GetValue(panel)! > 0f,
            "the first press only arms the exit");

        // Press 2 takes the CLOSE branch: the policy resets the arm to the expired sentinel BEFORE it
        // calls Close(). The teardown that follows is the double's known Verse.Map gap (the overlay's
        // session cascade the frozen stub deliberately does not ship - DiagnosticsPanelLaneTests keeps
        // the same boundary for the panel's close path), so the arm reset is the lane's honest witness
        // that the two-press decision fired; the completed cascade stays human evidence.
        Assert(CloseBranchEntered(panel, panelArm, stack),
            "the second press inside the arm window enters the close branch - the two-Esc policy is "
            + "preserved unchanged under the ladder-first order");

        // The detail window carries the same policy and the same eligibility opt-in. Its Title reads the
        // pinned pawn (a Verse.Entity type the frozen double deliberately omits), so this half is proven
        // at the shell's PUBLIC Cancel entry with the host still null - exactly the documented "no
        // library interaction open" precondition where the ladder declines and the consumer policy owns
        // the key. The full ladder-first stack ordering is the panel's proven half above.
        Type detailType = typeof(UsKernelSettingsHost).Assembly
            .GetType("UniversalSqueaker.SqueakDiagnosticsDetailWindow", throwOnError: true)!;
        var detail = (Window)Activator.CreateInstance(detailType, new object[] { new Pawn() })!;
        Assert(detail.forceCatchAcceptAndCancelEventEvenIfUnfocused && !detail.closeOnCancel,
            "the detail window's eligibility is set the same way");
        System.Reflection.FieldInfo detailArm = detailType.GetField("escArmedUntil",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Event d1 = Event.KeyboardEvent("dummy");
        d1.type = EventType.KeyDown;
        d1.keyCode = KeyCode.Escape;
        Event.current = d1;
        try { detail.OnCancelKeyPressed(); } finally { Event.current = null; }
        Assert(d1.type == EventType.Used && (float)detailArm.GetValue(detail)! > 0f, "detail: first press arms");
        Event d2 = Event.KeyboardEvent("dummy");
        d2.type = EventType.KeyDown;
        d2.keyCode = KeyCode.Escape;
        Event.current = d2;
        try { detail.OnCancelKeyPressed(); }
        catch (TypeLoadException) { }
        finally { Event.current = null; }
        Assert((float)detailArm.GetValue(detail)! < 0f, "detail: second press enters the close branch");

        // Restore the default double stack (fail-fast: a red lane ends the harness before anything
        // else reads this, but the lane must not leave a private stack standing for later steps).
        typeof(Find).GetProperty("WindowStack")!.SetValue(null, new WindowStack());
    }

    /// <summary>Second Escape press read at the policy's decision boundary: the close branch resets the
    /// arm to the expired sentinel, then calls Close(); the teardown cascade past that call reaches the
    /// double's deliberately missing Verse.Map, which is a stub limit, not a lane failure.</summary>
    private static bool CloseBranchEntered(Window window, System.Reflection.FieldInfo armField, WindowStack stack)
    {
        try
        {
            PumpEscapeThrough(window, stack);
        }
        catch (TypeLoadException)
        {
            // The overlay session cascade inside the close path needs Verse.Map, which the frozen double
            // omits. The close decision (the arm reset) has already been taken at this point.
        }

        return (float)armField.GetValue(window)! < 0f;
    }

    private static (bool _, Event ev) PumpEscapeThrough(Window window, WindowStack stack)
    {
        Event esc = Event.KeyboardEvent("dummy");
        esc.type = EventType.KeyDown;
        esc.keyCode = KeyCode.Escape;
        Event.current = esc;
        try
        {
            // The stub Window.WindowOnGUI runs the pre-contents key dispatch that routes through
            // WindowStack.Notify_PressedCancel - the real ordering, not a Widget.Draw stub claim.
            window.WindowOnGUI();
        }
        finally
        {
            Event.current = null;
        }

        return (false, esc);
    }
}
