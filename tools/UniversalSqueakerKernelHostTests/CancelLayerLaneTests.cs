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
        HiddenManualIsNoLayerVisibleManualCollapsesOnce();
        RootHandoffNeedsNoEmptyLayerAfterVisibleRelease();
        ExplicitTargetsExpireAndRealClicksClimb();
        TuningLadderIsScopedToTheVisibleBranch();
        DefaultGlobalContextReturnsOnceWithRealClick();
        PureLadderStaticsRoundTrip();
        F06FallbackAreaDropsTheTuningContext();
        F07MeasuredResetLineGrowsInsteadOfOverdrawing();
        WindowLayeringRelationships();
        KeyboardCooperationWitness();
        DiagnosticsTwoPressEscThroughTheNativeStackDouble();
        Console.WriteLine("[us-cancel] packs return = domain one press; the RESULT layer answers only for "
            + "VISIBLE manual cards (hidden manual retained, the climb reaches help); tuning rows"
            + " visible-branch-scoped over a context layer on the wrapper container;"
            + " default-Global page returns context ONCE (real-click subject); explicit targets expire"
            + " off-play; dev panel eligible only while its own page holds a layer - idle Esc goes to the"
            + " settings ladder; two-press Esc armed + close-branch through the stack double");
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
        host.Bindings.Set("help-open", true);   // the layer BELOW the result, for the climb to find last
        ArrangeAndDraw(host);

        // US-PACK1 (§4.1 real chain): the composed filter narrows the result to exactly the us.sang
        // card with its enabled xenotype row (the chip drops the disabled race row and the second
        // pack); the author condition is the second filter that must survive every return.
        host.Bindings.Invoke("set-domain-filter", new UsDomainFilterWrite(SqueakDomainFilterKind.EnabledOnly, true));
        host.Bindings.Set("pack-filter", "AuthorA");
        ArrangeAndDraw(host);
        Assert(fake.BuildView().PackCards.Count == 1
                && fake.BuildView().PackCards[0].Key == "us.sang",
            "setup: the author condition leaves exactly the us.sang card (the chip's write is asserted "
            + "at the boundary below; the fake's projection mirrors the author filter, not the chip)");

        // The card opens through the SAME funnel write the header button invokes - the item-scoped
        // toggle-pack-card action - and the manual set is what the result layer later collapses.
        host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
        ArrangeAndDraw(host);
        Assert(fake.ViewState.PackCardsExpanded.Contains("us.sang"), "setup: the manual expansion recorded");
        Assert(!fake.BuildView().PackCards[0].AutoExpanded,
            "no keyword is up, so the open card is the player's own - not the query's");

        // The subject is established by a press on the domain row's hit through the carrier's hit seam
        // (the declarative row control is pressed the way the retired DeclarativePacks lane pressed its
        // rows: the seam answers true for exactly that node, the engine records the subject and fires
        // the item action). The domain CancelBind lives on the page-level Repeat above the rows - a
        // Cancel inside a template is item-qualified and could not name the page command - so the walk
        // climbs from this subject to it, which is what press 1 below proves by answering.
        Rect viewport = new(0f, 0f, PageWidth, PageHeight);
        string hitId = "pack-card-row-hit#us.sang|human|sanguophage";
        int fired = Program.PressDeclarativeButton(host, viewport, hitId);
        Assert(fired == 1, "the domain row's hit must answer exactly one press, got " + fired);
        UiNode? subject = host.Session.LastInteractionNode;
        Assert(subject != null && string.Equals(subject.ElementId, hitId, StringComparison.Ordinal),
            "the press must record the row's own hit node as the walk's subject, got '"
            + (subject?.ElementId ?? "null") + "'");

        (bool answered, Event first) = PressEscape(host);
        Assert(answered && first.type == EventType.Used,
            "press 1 answers the DOMAIN layer and the shell consumes the key");
        Assert(fake.ViewState.DomainSelectionCanceled && fake.BuildView().SelectedDomain == null,
            "press 1 exits the operation domain into the explicit cancelled state - no re-pick");
        Assert(fake.ViewState.HelpPanelOpen && fake.ViewState.PackCardsExpanded.Contains("us.sang")
                && fake.BuildView().PackCards.Count == 1 && fake.BuildView().PackCards[0].Expanded,
            "one press, ONE layer: the card stays open and the help stays up (§4.1 退出选择不关闭包)");
        Assert(fake.ViewState.RaceFilter.Length == 0
                && string.Equals(fake.ViewState.PackFilter.Author, "AuthorA", StringComparison.Ordinal)
                && fake.LastDomainFilterKind == SqueakDomainFilterKind.EnabledOnly && fake.LastDomainFilterFlag == true,
            "the filter conditions survive the return (§4.1) - the author and the Enabled chip are exactly "
            + "what they were before the presses; the return touched neither");

        (bool answered2, Event second) = PressEscape(host);
        Assert(answered2 && second.type == EventType.Used
                && fake.ViewState.PackCardsExpanded.Count == 0
                && fake.BuildView().PackCards.Count == 1 && !fake.BuildView().PackCards[0].Expanded,
            "press 2: the domain layer declines (already cancelled) and the climb answers at the RESULT "
            + "layer - the manually opened cards collapse, the card itself stays in the result, the "
            + "filters stay, the help stays");
        Assert(fake.ViewState.HelpPanelOpen && fake.BuildView().PackCards.Count == 1,
            "the collapse touched neither the help layer below nor the surviving-card set");

        (bool answered3, Event third) = PressEscape(host);
        Assert(answered3 && third.type == EventType.Used && !fake.ViewState.HelpPanelOpen,
            "press 3 climbs to the page-root help layer and closes the panel");

        (bool answered4, Event fourth) = PressEscape(host);
        Assert(!answered4 && fourth.type != EventType.Used,
            "press 4: every declared layer declined, so nothing consumed the key - only the root closes, "
            + "and that is Verse's job on this press");
    }
    /// <summary>
    /// ESC1 aggregate closure (§4.1, integration contract line 7): after the active branch is cancelled,
    /// the release of the selection/interaction is OBSERVABLE (the domain highlight leaves and no
    /// consecutive projection re-picks it), and the NEXT press reaches Verse unconsumed - the handoff
    /// press. Because the branch release already completed the root arrival, the root needs NO extra
    /// layer that only raises the key count, and the help layer's own close must not masquerade as this
    /// proof. Re-clicking rebuilds the branch, which then answers its own Esc again. The row highlight's
    /// actual pixels are the human pass; the observable state answer here is the projection's own
    /// SelectedDomain, which is exactly what the selection surface reads.
    /// </summary>
    private static void RootHandoffNeedsNoEmptyLayerAfterVisibleRelease()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Packs");
        Rect viewport = new(0f, 0f, PageWidth, PageHeight);
        ArrangeAndDraw(host);

        // (1) Real clicks establish the subject and the branch: open the card, then press its row.
        Assert(Program.PressDeclarativeButton(host, viewport, "pack-card-row-expand#us.sang") == 1,
            "the Contents press must open the card");
        ArrangeAndDraw(host);
        Assert(Program.PressDeclarativeButton(host, viewport, "pack-card-row-hit#us.sang|human|sanguophage") == 1,
            "the row press must select the domain");
        VoicePacksViewState armed = fake.BuildView();
        Assert(armed.SelectedDomain.HasValue && VoicePacksPageModel.IsSelectedDomainVisible(armed),
            "setup: the selection is on screen - the branch is real, not a latent state");
        Assert(host.Session.LastInteractionNode != null,
            "the row press recorded its subject - the walk starts from the interaction");

        // (2) Press 1 answers the domain layer WITH the visible release, and the release survives
        // consecutive projections (a full arrange pass plus two rebuilds) - nothing re-picks.
        (bool domain, Event de) = PressEscape(host);
        Assert(domain && de.type == EventType.Used, "press 1 answers the visible domain layer");
        Assert(fake.ViewState.DomainSelectionCanceled, "the cancel is the explicit state, not a blanking");
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        VoicePacksViewState released = fake.BuildView();
        Assert(!released.SelectedDomain.HasValue && !VoicePacksPageModel.IsSelectedDomainVisible(released),
            "the selection release is observable across consecutive projections - the return does not re-select");
        Assert(fake.BuildView().SelectedDomain == null, "and a third rebuild still shows no domain");

        // (3) Press 2 answers the result layer - the card visibly collapses.
        (bool result, Event pe) = PressEscape(host);
        Assert(result && pe.type == EventType.Used, "press 2 answers the result layer");
        ArrangeAndDraw(host);
        Assert(fake.ViewState.PackCardsExpanded.Count == 0 && !fake.BuildView().PackCards[0].Expanded,
            "the manual card visibly collapsed - the branch is fully released");

        // (4) Press 3: every layer declines, so the key reaches Verse UNCONSUMED. That press is the
        // handoff; no empty root layer sits between the release and Verse's close.
        (bool handoff, Event he) = PressEscape(host);
        Assert(!handoff && he.type != EventType.Used,
            "the next press reaches Verse unconsumed - only the root closes, and the root adds no layer");

        // (5) Re-click rebuilds the branch and it answers again.
        Assert(Program.PressDeclarativeButton(host, viewport, "pack-card-row-expand#us.sang") == 1,
            "a fresh Contents press rebuilds the card");
        ArrangeAndDraw(host);
        Assert(Program.PressDeclarativeButton(host, viewport, "pack-card-row-hit#us.sang|human|sanguophage") == 1,
            "the row is pressable again");
        Assert(!fake.ViewState.DomainSelectionCanceled && fake.BuildView().SelectedDomain.HasValue,
            "the re-selection cleared the mark - the branch is live");
        (bool rebuilt, Event be) = PressEscape(host);
        Assert(rebuilt && be.type == EventType.Used && fake.ViewState.DomainSelectionCanceled,
            "and the rebuilt branch answers its own Esc");
        PressEscape(host); // collapse the card again so only help can answer below
        ArrangeAndDraw(host);

        // (6) Help answers with its OWN visible effect and cannot masquerade as the root-release proof:
        // that proof was already paid at press 1. Open help through the real footer switch, then show
        // the ladder answers help and the NEXT press again reaches Verse.
        UiLayoutSnapshot snap = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.DrawWithPointer(host, viewport, new Vector2(PageWidth / 2f, 140f));
        if (!snap.RectById.TryGetValue("help-toggle", out Rect toggle))
        {
            throw new Exception("CancelLayer lane: the footer help switch did not materialise a rect");
        }
        Vector2 press = new(toggle.x + toggle.width / 2f, toggle.y + toggle.height / 2f);
        Program.DrawWithEvent(host, viewport, EventType.MouseDown, press);
        Program.DrawWithEvent(host, viewport, EventType.MouseUp, press);
        ArrangeAndDraw(host);
        Assert(fake.ViewState.HelpPanelOpen, "setup: the real footer click opened the help panel");
        (bool help, Event he2) = PressEscape(host);
        Assert(help && !fake.ViewState.HelpPanelOpen,
            "the help layer answers with its own visible close - a separate layer, not the root release");
        (bool final, Event fe) = PressEscape(host);
        Assert(!final && fe.type != EventType.Used,
            "and only after every layer declined does the press reach Verse - no fabricated empty layer");
    }

    private static void ReSelectAndWorkspaceSwitchRebuildTheBranch()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Packs");
        string raceRowKey = fake.BuildView().Races[0].RaceDefName;
        Assert(fake.BuildView().SelectedDomain.HasValue,
            "first entry keeps the accepted initial default (the projection may auto-select)");

        // The subject is the domain row's hit element - it exists once a card is open. The domain
        // CancelBind lives on the page-level Repeat above the rows, so the walk climbs from this
        // subject to it (the sibling lane presses the same node through the seam).
        ArrangeAndDraw(host);   // materialises the headers, which registers their toggle-pack-card
        host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
        ArrangeAndDraw(host);
        Assert(host.Session.SetCancelTarget("pack-card-row-hit#us.sang|human|sanguophage"),
            "the materialised domain row must be a walkable subject");
        (bool answered, _) = PressEscape(host);
        Assert(answered && fake.ViewState.DomainSelectionCanceled, "setup: the return was taken");
        Assert(fake.ViewState.PackCardsExpanded.Contains("us.sang"),
            "the domain return left the manual expansion alone (each layer owns its own state)");

        host.Bindings.Invoke("select-domain", raceRowKey);
        Assert(!fake.ViewState.DomainSelectionCanceled,
            "a fresh selection replaces the cancel mark (the user chose again, the ladder is not stuck)");
        Assert(fake.BuildView().SelectedDomain.HasValue,
            "after re-selection the card shows the chosen domain");

        PressEscape(host);
        Assert(fake.ViewState.DomainSelectionCanceled, "setup: cancelled again");
        PressEscape(host);
        Assert(fake.ViewState.PackCardsExpanded.Count == 0, "setup: the result layer collapsed too");

        host.Bindings.Invoke("set-tab", "Tuning");
        Assert(!fake.ViewState.DomainSelectionCanceled && !fake.ViewState.FallbackTableCanceled,
            "switching workspace RE-ESTABLISHES the active branch (§4.1): last page's return marks do not "
            + "carry over, so each page opens with its first-entry default again");
        host.Bindings.Invoke("set-tab", "Packs");
        Assert(fake.BuildView().SelectedDomain.HasValue,
            "the rebuilt branch auto-selects once more (cancel was this branch's, not the page's)");
        Assert(fake.ViewState.PackCardsExpanded.Count == 0,
            "and the collapse is NOT silently re-opened by the rebuild - expansion is only ever the "
            + "player's own gesture or the query's");
    }

    /// <summary>
    /// US-PACK1 review-1 correction 2, on the REAL host: a manual expansion that the active conditions
    /// HIDE must never be an executable Esc layer. The subject is a row of a VISIBLE auto-expanded card;
    /// press 1 clears the domain; press 2 must climb PAST the result layer (the only manual key, sang2,
    /// is hidden by the author condition) and answer at the help layer while the hidden manual key is
    /// RETAINED; press 3 then declines everything to the root. Clearing the conditions brings the
    /// player's sang2 expansion back - and only then is the result layer real: it answers once and
    /// collapses exactly that visible card. Reproduces the PM boundary witness (host-run.log case 3).
    /// </summary>
    private static void HiddenManualIsNoLayerVisibleManualCollapsesOnce()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        Rect viewport = new(0f, 0f, PageWidth, PageHeight);
        host.Bindings.Invoke("set-tab", "Packs");
        ArrangeAndDraw(host);
        host.Bindings.Set("help-open", true);
        ArrangeAndDraw(host);

        // The player opens sang2; the author condition then hides it while the keyword auto-opens sang.
        host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang2", "toggle-pack-card"), "");
        host.Bindings.Set("pack-filter", "AuthorA");
        host.Bindings.Set("search-text", "sanguophage");
        ArrangeAndDraw(host);
        Assert(fake.ViewState.PackCardsExpanded.Contains("us.sang2"), "setup: sang2 is the manual key");
        Assert(fake.BuildView().PackCards.Count == 1
                && fake.BuildView().PackCards[0].Key == "us.sang"
                && fake.BuildView().PackCards[0].AutoExpanded
                && !fake.BuildView().PackCards[0].ManualExpanded,
            "setup: the author condition hides sang2 and the keyword auto-opens sang (visible, not manual)");

        int fired = Program.PressDeclarativeButton(host, viewport, "pack-card-row-hit#us.sang|human|sanguophage");
        Assert(fired == 1, "the visible auto-expanded row must answer the press, got " + fired);

        (bool answered1, Event first) = PressEscape(host);
        Assert(answered1 && first.type == EventType.Used && fake.ViewState.DomainSelectionCanceled,
            "press 1 clears the visible domain");

        (bool answered2, Event second) = PressEscape(host);
        Assert(answered2 && second.type == EventType.Used && !fake.ViewState.HelpPanelOpen,
            "press 2 climbs to the HELP layer: the result layer declined because its only manual key is hidden");
        Assert(fake.ViewState.PackCardsExpanded.Contains("us.sang2"),
            "the hidden manual expansion is RETAINED, not silently consumed");
        Assert(fake.BuildView().PackCards[0].Expanded,
            "and the visible auto card stayed exactly as the query left it");

        (bool answered3, Event third) = PressEscape(host);
        Assert(!answered3 && third.type != EventType.Used,
            "press 3: help closed, no visible manual card, domain declined - the key reaches the root unconsumed");

        // The retained state returns with the condition - and only now is the result layer real.
        host.Bindings.Set("pack-filter", "");
        host.Bindings.Set("search-text", "");
        ArrangeAndDraw(host);
        PackCardView back = fake.BuildView().PackCards[1];
        Assert(back.Key == "us.sang2" && back.ManualExpanded && back.Expanded,
            "clearing the conditions restored the player's own sang2 expansion");
        fired = Program.PressDeclarativeButton(host, viewport, "pack-card-row-hit#us.sang2|human|sanguophage");
        Assert(fired == 1, "the restored card's row answers a press");
        host.Bindings.Invoke("select-domain", "human|sanguophage");   // re-establish the domain layer
        ArrangeAndDraw(host);
        (bool answered4, _) = PressEscape(host);
        Assert(answered4 && fake.ViewState.DomainSelectionCanceled,
            "press 4: the domain layer answers first (one press, one layer)");
        (bool answered5, Event fifth) = PressEscape(host);
        Assert(answered5 && fifth.type == EventType.Used
                && fake.ViewState.PackCardsExpanded.Count == 0,
            "press 5: the VISIBLE manual card now IS the layer - one press, collapsed exactly once");
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
        // The CONTEXT layer is a real PARENT of that node, not a state stitched into the row command
        // (review1 observation 1): the wrapper container publishes cancel-tuning-context one step up.
        Assert(subject != null && subject.Parent != null
                && string.Equals(subject.Parent.CancelBindingKey, "cancel-tuning-context", StringComparison.Ordinal),
            "the container wrapping the composite must publish the context CancelBind on the parent chain");

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
                && fake.ViewState.TuningSelectedAction.Length == 0 && !fake.ViewState.TuningContextActive,
            "press 3: the ROW layer declines and the climb answers at the parent CONTEXT layer - the "
            + "branch rests at the page default, folding the stale row target in as one context exit");
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

        // PATH B: in the ACTION area the row editor IS visible, so it IS its own layer-1 step; the
        // context is the parent's single press after that, and after the context exits ANY tuning write
        // re-arms it (the branch is active again while the player operates).
        host.Bindings.Invoke("set-tuning-layer", 1);
        host.Bindings.Set("tuning-selected-action", "Eat");
        ArrangeAndDraw(host);
        Assert(fake.ViewState.TuningContextActive, "PATH B setup: the writes re-armed the context");
        (bool q1, _) = PressEscape(host);
        Assert(q1 && fake.ViewState.TuningSelectedAction.Length == 0 && fake.ViewState.TuningLayer == 1
                && fake.ViewState.TuningContextActive,
            "press 1 in the action area closes the visible row editor only - context untouched");
        (bool q2, _) = PressEscape(host);
        Assert(q2 && fake.ViewState.TuningLayer == 0 && fake.ViewState.TuningArea == 0
                && !fake.ViewState.TuningContextActive,
            "press 2 = the parent context layer");
        host.Bindings.Invoke("set-tuning-layer", 1);
        Assert(fake.ViewState.TuningContextActive,
            "operation after the exit re-arms the branch - the context is activity, not a one-shot");
        (bool q3, Event third) = PressEscape(host);
        Assert(q3 && fake.ViewState.TuningLayer == 0, "and it answers the new session too");
        (bool q4, Event fourth2) = PressEscape(host);
        Assert(!q4 && fourth2.type != EventType.Used, "then the ladder declines - the root keeps the next press");

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

        Assert(host.Session.SetCancelTarget("packs-results") && host.Session.CancelTargetNode != null,
            "setup: the explicit target - the packs RESULT Section, the node carrying cancel-pack-results -"
            + " is held while its branch is on the page");
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
        // ESC1 closure (defect A): a selection is a layer only while its row is ON SCREEN. Open the
        // card that carries the row and select through that row's own identity.
        VoicePacksViewState v0 = fake.BuildView();
        PackCardView firstCard = v0.PackCards[0];
        PackCardDomainRowView domainRow = firstCard.Rows[0];
        state.PackCardsExpanded.Add(firstCard.Key);
        VoicePacksPageModel.SelectDomain(state, domainRow.Scope, domainRow.RaceDefName, domainRow.TargetDefName);
        VoicePacksViewState view = fake.BuildView();

        Assert(VoicePacksPageModel.CanCancelDomainSelection(state, view),
            "with a VISIBLE domain row and no mark, the packs layer can answer");
        VoicePacksPageModel.CancelDomainSelection(state);
        VoicePacksViewState cancelled = fake.BuildView();
        Assert(state.DomainSelectionCanceled
                && !VoicePacksPageModel.CanCancelDomainSelection(state, cancelled),
            "after the return the veto is honest: TryInvokeCommand would climb past");
        VoicePacksPageModel.SelectDomain(state, domainRow.Scope, domainRow.RaceDefName, domainRow.TargetDefName);
        VoicePacksViewState rearmed = fake.BuildView();
        Assert(!state.DomainSelectionCanceled && VoicePacksPageModel.CanCancelDomainSelection(state, rearmed),
            "a new selection re-arms the layer");
        // The rejection clause: collapse the card. The selection stays retained as latent state, but
        // the layer must decline while nothing on screen shows it - consuming the key there is the
        // invisible Esc the probe witnessed.
        state.PackCardsExpanded.Remove(firstCard.Key);
        VoicePacksViewState hidden = fake.BuildView();
        Assert(hidden.SelectedDomain.HasValue
                && !VoicePacksPageModel.CanCancelDomainSelection(state, hidden),
            "a selection whose row is collapsed away is retained, never an invisible Esc layer");

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
        // The row layer's steps are done; a THIRD row press changes nothing and vetoes (the stale action
        // is NOT an invisible step, PM 10) - the context is a SEPARATE parent layer with its own gate.
        v2 = fake2.BuildView();
        Assert(!VoicePacksPageModel.CanCancelTuningTarget(s2, v2),
            "the row layer has nothing visible left - it vetoes rather than swallowing the press");
        VoicePacksPageModel.CancelTuningTarget(s2, v2);   // must be a no-op
        Assert(s2.TuningLayer == 1 && s2.TuningArea == 2 && s2.TuningSelectedAction == "Eat",
            "a vetoed row layer changes no state - repose belongs to the context layer");
        Assert(VoicePacksPageModel.CanCancelTuningContext(s2),
            "the context layer is active (business state), independent of the non-zero layer/area");
        VoicePacksPageModel.CancelTuningContext(s2);
        Assert(s2.TuningLayer == 0 && s2.TuningArea == 0 && s2.TuningSelectedAction.Length == 0
                && !s2.TuningContextActive,
            "the context exit reposes the branch and folds the stale action - the single parent press");
        v2 = fake2.BuildView();
        Assert(!VoicePacksPageModel.CanCancelTuningTarget(s2, v2) && !VoicePacksPageModel.CanCancelTuningContext(s2),
            "then both layers veto - the root keeps the next press");

        // The same statics on the ACTION area: the row target IS visible and IS its own step.
        BuildPacksView(out RecordingSettingsSource fake3);
        VoicePacksPageState s3 = fake3.ViewState;
        s3.TuningArea = 0;
        s3.TuningLayer = 1;
        s3.TuningSelectedAction = "Eat";
        VoicePacksViewState v3 = fake3.BuildView();
        VoicePacksPageModel.CancelTuningTarget(s3, v3);
        Assert(s3.TuningSelectedAction.Length == 0 && s3.TuningLayer == 1 && s3.TuningContextActive,
            "in the action area the row editor is its own visible step; the context stays active");
        v3 = fake3.BuildView();
        Assert(!VoicePacksPageModel.CanCancelTuningTarget(s3, v3) && VoicePacksPageModel.CanCancelTuningContext(s3),
            "the row layer now vetoes and the parent context layer answers");
        VoicePacksPageModel.CancelTuningContext(s3);
        Assert(s3.TuningLayer == 0 && !s3.TuningContextActive, "the context exit rests the branch");
        VoicePacksPageModel.SetTuningLayer(s3, 1);   // operating again re-arms
        Assert(s3.TuningContextActive, "re-arm: any tuning write makes the branch active again");
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
    /// Review1 observation 1's required path: NO manual SetCancelTarget, NO help layer. A real click on
    /// a composite control (captured through the carrier's own button seam, then pressed for real through
    /// MouseDown/MouseUp events) establishes the subject; the untouched default Global/Actions page must
    /// still answer exactly ONE context return before the root may take the next press, and that exit
    /// changes no settings and selects no domain.
    /// </summary>
    private static void DefaultGlobalContextReturnsOnceWithRealClick()
    {
        BuildPacksView(out RecordingSettingsSource fake);
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.RevisionSource = () => host.Session.ContentRevision;
        host.Bindings.Invoke("set-tab", "Tuning");
        ArrangeAndDraw(host);

        var viewport = new Rect(0f, 0f, PageWidth, PageHeight);
        List<Rect> captured = CaptureCompositeButtons(host, viewport, "scope-tree");
        List<Rect> row = FirstWideRow(captured);
        Assert(row.Count == 3, "the layer segment must have drawn three wide buttons, got ["
            + string.Join(", ", captured.ConvertAll(r => (int)r.width + "px@" + (int)r.y)) + "]");

        Assert(!fake.ViewState.HelpPanelOpen, "this path runs with the help layer closed (PM: no help fallback)");
        Assert(fake.ViewState.TuningLayer == 0 && fake.ViewState.TuningArea == 0
                && fake.ViewState.TuningSelectedAction.Length == 0 && fake.ViewState.TuningContextActive,
            "setup: the untouched default Global/Actions branch, context active from entry");

        // The seam's rects are content-local inside content-scroll; a real event carries window-space
        // coordinates (same conversion the XG1 lane uses). The default-Global branch sits at the top of
        // the column: pin scroll zero, then transform - and assert the point is genuinely inside the
        // viewport, so "a real press recorded the subject" cannot pass on an off-screen event.
        Rect globalButton = row[0];
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        UiLayoutSnapshot pressSnap = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Assert(pressSnap.Viewports.TryGetValue("content-scroll", out Rect scrollViewport),
            "the tuning column must publish its scroll viewport for a real pointer event");
        Vector2 press = new(globalButton.x + globalButton.width / 2f + scrollViewport.x,
            globalButton.y + globalButton.height / 2f + scrollViewport.y);
        Assert(press.x > scrollViewport.x && press.x < scrollViewport.xMax
                && press.y > scrollViewport.y && press.y < scrollViewport.yMax,
            "the layer button must be inside the viewport for the honest real press, got " + press);
        Program.DrawWithEvent(host, viewport, EventType.MouseDown, press);
        Program.DrawWithEvent(host, viewport, EventType.MouseUp, press);
        Assert(host.Session.LastInteractionNode != null,
            "the real press must record its subject - no SetCancelTarget was used");

        (bool answered, Event used) = PressEscape(host);
        Assert(answered && used.type == EventType.Used,
            "the default-Global page answers ONE context return before the root (review1 observation 1)");
        Assert(!fake.ViewState.TuningContextActive && fake.ViewState.TuningLayer == 0
                && fake.ViewState.TuningArea == 0 && fake.ViewState.TuningRaceDefName.Length == 0,
            "the exit reposes the branch without changing settings or auto-selecting a domain");
        (bool again, Event second) = PressEscape(host);
        Assert(!again && second.type != EventType.Used,
            "with rows and context both declined and no help open, the key reaches the root unconsumed");
        Assert(fake.LastActionScope == null && fake.LastPackScope == null && fake.LastMood == null,
            "and the whole path touched no persisted business value");
    }

    private static List<Rect> CaptureCompositeButtons(UiHost host, Rect viewport, string elementId)
    {
        var rects = new List<Rect>();
        System.Reflection.FieldInfo? seam = typeof(UiNative).GetField("ButtonOverride",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        Assert(seam != null, "the carrier's ButtonOverride seam moved - the capture must fail, not fake");
        try
        {
            seam!.SetValue(null, new Func<Rect, bool>(rect =>
            {
                UiNode node = host.Session.ActiveNode;
                if (node != null && string.Equals(node.ElementId, elementId, StringComparison.Ordinal))
                {
                    rects.Add(rect);
                }

                return false; // observe only: nothing fires while capturing
            }));
            Program.DrawWithPointer(host, viewport, new Vector2(-4000f, -4000f));
        }
        finally
        {
            seam!.SetValue(null, null);
        }

        return rects;
    }

    private static List<Rect> FirstWideRow(List<Rect> rects)
    {
        foreach (Rect candidate in rects)
        {
            var row = new List<Rect>();
            foreach (Rect other in rects)
            {
                if (Math.Abs(other.y - candidate.y) < 1f && other.width >= 100f) row.Add(other);
            }

            if (row.Count == 3) return row;
        }

        return new List<Rect>();
    }

    /// <summary>
    /// Review1 observation 2 + the focus-timing observation's reachability witness. Everything runs
    /// through the window's OWN native pass order: the panel's eligibility recompute happens at the top
    /// of its WindowOnGUI, Verse's pre-content dispatch reads it in the SAME pass, and no lane call ever
    /// refreshes the field by hand. Cases: (a) a menu opened by the previous frame is RECEIVABLE on the
    /// very next Escape - the exact stale-BeforeDraw failure the observation named; (b) once the menu is
    /// gone the next pass clears eligibility and Escape/Accept reach the settings window under; (c) a
    /// confirmation on Super with absorption blocks the windows below while it is up. The stub walks its
    /// list bottom-up while the real WindowStack walks top-down; the two readings agree on every case
    /// asserted here (the disagreement case - focused main, active tool menu - is named in the print for
    /// the human short pass). Main-window stand-ins carry the settings window's real public fields.
    /// </summary>
    private static void KeyboardCooperationWitness()
    {
        var stack = new WindowStack();
        typeof(Find).GetProperty("WindowStack")!.SetValue(null, stack);
        try
        {
            var main = new KeyReceiverStandIn
            {
                layer = WindowLayer.Dialog,
                absorbInputAroundWindow = true,
                closeOnAccept = true,
                closeOnCancel = true,
                windowRect = new Rect(200f, 100f, 600f, 400f),
            };
            stack.Add(main); // back: the settings window
            Type devType = typeof(UsKernelSettingsHost).Assembly
                .GetType("UniversalSqueaker.UI.Dev.UsDevPanelWindow", throwOnError: true)!;
            var panel = (Window)Activator.CreateInstance(devType, nonPublic: true)!;
            panel.windowRect = new Rect(60f, 60f, 360f, 240f);
            Assert(panel.layer == WindowLayer.SubSuper && !panel.closeOnCancel && !panel.closeOnAccept
                    && !panel.forceCatchAcceptAndCancelEventEvenIfUnfocused,
                "the panel starts idle: above the settings window, claiming no key unconditionally");
            stack.Add(panel); // front: the tool

            PumpKeyPass(panel, EventType.Repaint, KeyCode.None);
            var hostField = typeof(UiWindowHost).GetField("host",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(hostField != null, "the shell's host field moved");
            var panelHost = (UiHost?)hostField!.GetValue(panel);
            Assert(panelHost != null, "a real panel pass must have built its own host");

            // (a) The observation's exact sequence: last pass left the fields FALSE (panel idle), a click
            // then opened the menu, the VERY NEXT event is Escape - with no manual refresh in between.
            panelHost!.Session.OpenPopup("coop-target-menu", new Rect(70f, 70f, 120f, 24f));
            Assert(!panel.forceCatchAcceptAndCancelEventEvenIfUnfocused,
                "nothing has recomputed between the open and this press - the field is still last pass's false");
            SetFocused(stack, panel);
            PumpKeyPass(panel, EventType.KeyDown, KeyCode.Escape);
            Assert(panel.forceCatchAcceptAndCancelEventEvenIfUnfocused,
                "the panel's own pre-content recompute claimed eligibility inside the SAME pass Verse read");
            Assert(panelHost.Session.OpenPopupId == null && main.CancelAnswers == 0 && stack.IsOpen(main)
                    && stack.IsOpen(panel),
                "that pass's dispatch closed exactly the panel's own menu; the settings window never moved");

            // (b1) Menu-open routing: the menu was opened AFTER the last pass boundary, so the next
            // event's pass-top recompute finds it live and the tool receives that event. The sealed
            // Accept has nothing to answer (no edit) - and the observable is ROUTING: the settings
            // window below was never asked. (A synthetic menu has no owning element, so the double
            // also reclaims it at this pass's boundary; the "Accept answers only an edit" ladder rule
            // itself is FL-IC2's own lane, not re-proven here.)
            panelHost.Session.OpenPopup("coop-target-menu", new Rect(70f, 70f, 120f, 24f));
            SetFocused(stack, panel);
            PumpKeyPass(panel, EventType.KeyDown, KeyCode.Return);
            Assert(main.AcceptAnswers == 0 && stack.IsOpen(main),
                "an eligible tool takes Enter into its editless Accept door: the settings window "
                + "underneath is never reached - one key, one receiver");
            // (b1') Cancel on the same live footing closes exactly the tool's own menu layer.
            panelHost.Session.OpenPopup("coop-target-menu", new Rect(70f, 70f, 120f, 24f));
            PumpKeyPass(panel, EventType.KeyDown, KeyCode.Escape);
            Assert(panelHost.Session.OpenPopupId == null && main.CancelAnswers == 0 && stack.IsOpen(main),
                "and Cancel answers exactly one menu layer inside the tool, never the window below");

            // (b2) idle: the next pass clears the claim and Enter reaches the settings window, where
            // Verse's own closeOnAccept convention answers (stock semantics, not a lane invention).
            PumpKeyPass(panel, EventType.Repaint, KeyCode.None);
            Assert(!panel.forceCatchAcceptAndCancelEventEvenIfUnfocused,
                "a closed menu releases the claim within the next pass - no stale true");
            SetFocused(stack, main);
            PumpKeyPass(panel, EventType.KeyDown, KeyCode.Return);
            Assert(main.AcceptAnswers == 1 && !stack.IsOpen(main) && stack.IsOpen(panel),
                "Enter reached the settings window and Verse's stock accept-close took it; the idle tool "
                + "never blocks the key and survives its window's exit");

            // (c) the confirmation outranks both while it is up: its absorption blocks the walk, and it
            // alone answers.
            var main3 = new KeyReceiverStandIn
            {
                layer = WindowLayer.Dialog,
                absorbInputAroundWindow = true,
                closeOnAccept = true,
                closeOnCancel = true,
                windowRect = new Rect(200f, 100f, 600f, 400f),
            };
            stack.Add(main3);
            Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
            UsConfirmWindow? flow = UsConfirmWindow.Open("t", "m", "go", () => { }, stack);
            Assert(flow?.OpenedWindow != null && ((Window)flow.OpenedWindow!).layer == WindowLayer.Super,
                "the confirmation sits on Super above tool and main window");
            SetFocused(stack, main3);
            PumpKeyPass(panel, EventType.KeyDown, KeyCode.Escape);
            Assert(main3.CancelAnswers == 0 && !panel.forceCatchAcceptAndCancelEventEvenIfUnfocused
                    && stack.IsOpen(main3) && (flow!.OpenedWindow == null || !flow.OpenedWindow.IsOpen),
                "the open question absorbed the key: exactly the dialog answered it (as Cancel), and "
                + "neither the tool nor the settings window moved");
            flow!.DialogBindings.Invoke("confirm-no"); // idempotent teardown of the lane's dialog
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            typeof(Find).GetProperty("WindowStack")!.SetValue(null, new WindowStack());
        }

        Console.WriteLine("[f01-keys] native pre-content recompute: menu-open survives the immediate-Escape "
            + "order; idle clears BOTH keys to the settings window; confirmation absorbs first; the "
            + "focused-main-with-active-panel ORDER differs between the double's walk and the real stack - "
            + "named for the human short pass");
    }

    /// <summary>Stand-in carrying the settings window's real public fields (the actual class needs a
    /// Verse.Mod the double omits); UiSourceInvariantTests pins the mirrored field block in source.</summary>
    private sealed class KeyReceiverStandIn : Window
    {
        public int CancelAnswers;
        public int AcceptAnswers;

        public override void DoWindowContents(Rect inRect)
        {
        }

        public override void OnCancelKeyPressed()
        {
            CancelAnswers++;
            base.OnCancelKeyPressed();
        }

        public override void OnAcceptKeyPressed()
        {
            AcceptAnswers++;
            base.OnAcceptKeyPressed();
        }
    }

    private static void PumpKeyPass(Window window, EventType type, KeyCode key)
    {
        Event e = Event.KeyboardEvent("dummy");
        e.type = type;
        e.keyCode = key;
        e.mousePosition = new Vector2(-4000f, -4000f);
        Event.current = e;
        try
        {
            window.WindowOnGUI();
        }
        finally
        {
            Event.current = null;
        }
    }

    /// <summary>The reference assembly keeps WindowStack.focusedWindow private (the double made it
    /// public only so FL's own lanes can observe it through reflection too): the witness sets it the
    /// same way, never a compile-time shortcut the real shape would refuse.</summary>
    private static void SetFocused(WindowStack stack, Window window)
    {
        System.Reflection.FieldInfo field = typeof(WindowStack).GetField("focusedWindow",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(stack, window);
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
