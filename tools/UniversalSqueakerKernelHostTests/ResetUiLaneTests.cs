using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

using FerriteLib.UiKit.Kernel;

using UniversalSqueaker;
using UniversalSqueaker.UI;

using Verse;


namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// US-RESET1 UI integration (INTERACTION-CONVERGENCE-TASKS §4.3/§5-6, integration contract lines 16-19):
/// the restore entries driven through their REAL UI surface. What this lane owns and the backend
/// harness does not: the ceremony (the real UsConfirmWindow shell - cancel drops the staged action with
/// zero writes, confirm runs the effect key exactly once and the clock moves only for Applied), the
/// direct entries (distance defaults, one action row - no window at all), the scope-tree's area
/// confirmation NAMING the current layer/domain, the XG1 target offering no area entry and the command
/// refusing one, and the draft integration (the live field's existing commit contract still writes a
/// valid draft exactly once; the question opening neither re-commits nor discards the open edit; a
/// cancel executes nothing; the confirmed restore ENDS the affected edit so no later Enter or focus
/// pass can push the old value over the restored one). The real publish/persistence counts, settings
/// instance identity, idempotence and the fallback file bytes stay the dedicated reset backend
/// harness's boundary (120 checks); the real native focus/IME behaviour is the named human pass.
/// </summary>
internal static class ResetUiLaneTests
{
    private const float PageWidth = 1280f;
    private const float PageHeight = 720f;
    private static readonly Rect Viewport = new(0f, 0f, PageWidth, PageHeight);

    private static readonly FieldInfo ButtonOverrideField = RequireField("ButtonOverride");
    private static readonly FieldInfo TextFieldOverrideField = RequireField("TextFieldOverride");

    internal static void RunAll()
    {
        DeclarativeCeremonyRealWindow();
        DirectDistanceRestore();
        TuningAreaCeremonyAndXg1Refusal();
        RowRestoreDirectAndGuarded();
        WindowReachabilityRealButtons();
        ScopedDraftBoundaries();
        StagedIdentityDriftRefusal();
        DraftOcclusionCancelAndConfirm();
        Console.WriteLine("[us-reset] ceremony through the REAL question window (cancel zero writes; confirm one effect, one bump; "
            + "NoChange silent); distance defaults direct without a window; the area entry names the current layer/domain and the "
            + "XG1 target offers none / refuses the command; the row restore is direct and guarded; the live "
            + "field's commit contract stands, occlusion neither commits nor discards, cancel executes "
            + "nothing, and the confirmed restore ends the affected edit; review1: the long EN/ZH body "
            + "GROWS the shell and the absurd body CAPS at the screen with the buttons answered by the "
            + "REAL dialog button press, the draft end is scoped to Applied + own fields (unrelated and "
            + "NoChange/Rejected edits survive), and a drifted staged identity restores nothing");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("ResetUi lane: " + message);
    }

    private static FieldInfo RequireField(string name)
    {
        FieldInfo? field = typeof(UiNative).GetField(name,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) throw new Exception("the carrier's " + name + " seam moved");
        return field;
    }

    private static void SetButton(Func<Rect, bool>? value) => ButtonOverrideField.SetValue(null, value);
    private static void SetText(Func<Rect, string, string>? value) => TextFieldOverrideField.SetValue(null, value);

    private static bool RectMatches(Rect a, Rect b)
        => Math.Abs(a.x - b.x) < 0.5f && Math.Abs(a.y - b.y) < 0.5f
        && Math.Abs(a.width - b.width) < 0.5f && Math.Abs(a.height - b.height) < 0.5f;

    /// <summary>UsConfirmWindow seam hygiene on the stub's shared stack, and a self-healed start: a
    /// question left open by another lane is CANCELLED before this lane begins, so the "one question
    /// at a time" rule can never silently absorb a press here. The stub's Find.WindowStack is a
    /// get-only live instance - the widget hands its question to exactly that stack.</summary>
    private static void FreshStack()
    {
        if (UsConfirmWindow.Active != null)
        {
            UsConfirmWindow.Active.DialogBindings.Invoke("confirm-no");
        }
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        UiLayoutSnapshot snap = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.DrawWithPointer(host, Viewport, new Vector2(PageWidth / 2f, 140f));
        host.DrawChecked(Viewport);
        return snap;
    }

    private static RecordingSettingsSource RichHost(out UiHost host, out UsWriteBindings writes)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        UiHost created = UsKernelSettingsHost.Create(fake, new Program.StubMetrics(), out writes);
        fake.RevisionSource = () => created.Session.ContentRevision;
        host = created;
        return fake;
    }
    /// <summary>The centre column's bring-into-view rule (XG1/RPT1): a band below the fold is drawn
    /// but its Button call is clip-refused at the CURRENT scroll, so a real press needs the element
    /// actually inside the viewport. Space model (measured here, the RPT1 ToContentLocal rule): the
    /// seam/drawn rects are CONTENT-LOCAL (scroll never shifts them), RectById is content-local plus
    /// the viewport origin (the page position at scroll 0), and a pointer event aims at
    /// local + viewport - scroll. The loop reads the element's own drawn rect and scrolls by the
    /// exact residual until the page position lands inside the clip.</summary>
    private static UiLayoutSnapshot ArrangeIntoView(UiHost host, string id)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            UiLayoutSnapshot snap = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
            Rect viewport = snap.Viewports["content-scroll"];
            Rect drawn = ProbeDrawnRect(host, id);
            Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
            float pageY, pageYMax;
            if (drawn.width > 0f)
            {
                pageY = drawn.y + viewport.y - scroll.y;
                pageYMax = drawn.yMax + viewport.y - scroll.y;
            }
            else
            {
                // Not a button (a number field, a caption): RectById already carries the viewport
                // origin at scroll 0, so the page position is simply it minus the scroll.
                Assert(snap.RectById.TryGetValue(id, out Rect band),
                    "ResetUi lane: '" + id + "' materialises no rect at all");
                pageY = band.y - scroll.y;
                pageYMax = band.yMax - scroll.y;
            }
            if (pageY >= viewport.y && pageYMax <= viewport.yMax)
            {
                Program.DrawWithPointer(host, Viewport, new Vector2(PageWidth / 2f, 140f));
                host.DrawChecked(Viewport);
                return snap;
            }
            Program.SetScrollPositionById(host.Session, "content-scroll",
                new Vector2(0f, Math.Max(0f, scroll.y + pageYMax - viewport.yMax + 40f)));
        }
        throw new Exception("ResetUi lane: '" + id + "' never came into the scroll viewport");
    }

    /// <summary>The element's drawn (content-local) button rect - the seam sees every Button call,
    /// clipped or not - or a zero rect when the element draws no button at all.</summary>
    private static Rect ProbeDrawnRect(UiHost host, string id)
    {
        Rect found = default;
        try
        {
            SetButton(rect =>
            {
                if (found.width <= 0f
                    && string.Equals(host.Session.ActiveNode.ElementId, id, StringComparison.Ordinal))
                {
                    found = rect;
                }
                return false;
            });
            host.DrawChecked(Viewport);
        }
        finally
        {
            SetButton(null);
        }
        return found;
    }

    /// <summary>RectById band (page position at scroll 0) -> the page point a pointer event must aim
    /// at under the CURRENT scroll.</summary>
    private static Rect ToPage(UiHost host, UiLayoutSnapshot snap, Rect band)
    {
        Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
        return new Rect(band.x - scroll.x, band.y - scroll.y, band.width, band.height);
    }

    /// <summary>RectById band -> the content-local rect the seam predicates compare against.</summary>
    private static Rect ToLocal(UiLayoutSnapshot snap, Rect band)
    {
        Rect viewport = snap.Viewports["content-scroll"];
        return new Rect(band.x - viewport.x, band.y - viewport.y, band.width, band.height);
    }

    /// <summary>Scroll the centre column until a CONTENT-LOCAL band (the seam space) is inside the
    /// clip at the current scroll (page = local + viewport - scroll).</summary>
    private static void ScrollLocalIntoView(UiHost host, UiLayoutSnapshot snap, Rect local)
    {
        Rect viewport = snap.Viewports["content-scroll"];
        Vector2 scroll = Program.ScrollPositionById(host.Session, "content-scroll");
        float pageYMax = local.yMax + viewport.y - scroll.y;
        if (pageYMax > viewport.yMax)
        {
            Program.SetScrollPositionById(host.Session, "content-scroll",
                new Vector2(0f, scroll.y + pageYMax - viewport.yMax + 40f));
        }
        Assert(local.y + viewport.y - Program.ScrollPositionById(host.Session, "content-scroll").y
                >= viewport.y,
            "ResetUi lane: the band's top must not scroll above the viewport");
    }

    // ------------------------------------------------------------------ declarative ceremony

    private static void DeclarativeCeremonyRealWindow()
    {
        FreshStack();
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        host.Bindings.Invoke("set-tab", "Overview");
        // Press the GLOBAL entry's button - the us/reset-entry composite, through the carrier's own
        // hit seam. The card sits at the bottom of the settings home, so it is brought into the
        // viewport first: a below-fold Button call is clip-refused (the XG1 rule). The press may
        // only open the question; nothing is written yet.
        ArrangeIntoView(host, "restore-center-button");
        Assert(Program.PressDeclarativeButton(host, Viewport, "restore-center-button") == 1,
            "the restore-center button must answer exactly one press");
        UsConfirmWindow? flow = UsConfirmWindow.Active;
        Assert(flow != null, "the press must open the REAL question window");
        Assert(fake.ResetAllCount == 0, "the question itself writes nothing");

        // Cancel through the dialog's own command table - the exact command the real button fires.
        flow!.DialogBindings.Invoke("confirm-no");
        Assert(UsConfirmWindow.Active == null, "cancel closes the window");
        Assert(fake.ResetAllCount == 0, "cancel drops the staged action: zero writes");
        int revAfterCancel = host.Session.ContentRevision;

        // Confirm: the staged action runs the EFFECT key once, Applied advances the clock once.
        Assert(Program.PressDeclarativeButton(host, Viewport, "restore-center-button") == 1,
            "the entry re-asks after an answered question");
        flow = UsConfirmWindow.Active;
        Assert(flow != null, "setup: the second press opened the window");
        flow!.DialogBindings.Invoke("confirm-yes");
        Assert(fake.ResetAllCount == 1 && UsConfirmWindow.Active == null,
            "confirm runs the staged effect exactly once and closes");
        Assert(host.Session.ContentRevision == revAfterCancel + 1,
            "Applied advances the session clock exactly once");

        // NoChange: the restore ran, the funnel wrote nothing, and the UI must not pretend - no bump,
        // no phantom rebuild. (The real funnel counts are the backend harness's boundary; this is the
        // UI's claim discipline.)
        fake.NextResetOutcome = SqueakResetOutcome.NoChange;
        Assert(Program.PressDeclarativeButton(host, Viewport, "restore-center-button") == 1,
            "setup: third ask");
        int revBefore = host.Session.ContentRevision;
        UsConfirmWindow.Active!.DialogBindings.Invoke("confirm-yes");
        Assert(fake.ResetAllCount == 2, "the effect key still ran once");
        Assert(host.Session.ContentRevision == revBefore,
            "NoChange must not advance the clock - the page did not change and no save may be claimed");

        // Rejected behaves the same (identity lost between ask and answer - the backend refuses).
        fake.NextResetOutcome = SqueakResetOutcome.Rejected;
        Assert(Program.PressDeclarativeButton(host, Viewport, "restore-center-button") == 1, "setup: fourth ask");
        UsConfirmWindow.Active!.DialogBindings.Invoke("confirm-yes");
        Assert(fake.ResetAllCount == 3 && host.Session.ContentRevision == revBefore,
            "Rejected: ran, wrote nothing, silent");
        fake.NextResetOutcome = SqueakResetOutcome.Applied;
        Program.SetTranslatorResolver(null);
    }

    // ------------------------------------------------------------------ direct distance entry

    private static void DirectDistanceRestore()
    {
        FreshStack();
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        host.Bindings.Invoke("set-tab", "Distance");
        ArrangeIntoView(host, "attenuation-reset-defaults");

        // The simple restore executes on the press - no window at all (§4.3: 距离等简单恢复直接执行).
        int rev = host.Session.ContentRevision;
        Assert(Program.PressDeclarativeButton(host, Viewport, "attenuation-reset-defaults") == 1,
            "the direct distance button must answer one press");
        Assert(fake.ResetDistanceCount == 1 && host.Session.ContentRevision == rev + 1,
            "one press, one restore, one bump");
        Assert(UsConfirmWindow.Active == null, "a simple restore must NOT open a question window");
        Assert(fake.ResetNormalAreaCount == 0,
            "the direct distance entry is ResetDistanceDefaults, never the distance+volume AREA restore");

        // The area entry (confirmation-gated) is a DIFFERENT scope: it asks, and its effect key is the
        // area command - the two entries cannot substitute for each other.
        Assert(Program.PressDeclarativeButton(host, Viewport, "attenuation-reset-area") == 1,
            "the area button answers one press");
        Assert(UsConfirmWindow.Active != null && fake.ResetNormalAreaCount == 0,
            "the area entry asks first and writes nothing until answered");
        UsConfirmWindow.Active!.DialogBindings.Invoke("confirm-yes");
        Assert(fake.ResetNormalAreaCount == 1 && fake.LastNormalResetArea == SqueakNormalResetArea.Distance,
            "the staged action runs the AREA restore with the Distance field set");
    }

    // ------------------------------------------------------------------ scope-tree area + row

    private static List<Rect> RecordScopeTreeButtons(UiHost host)
    {
        var rects = new List<Rect>();
        try
        {
            SetButton(rect =>
            {
                if (string.Equals(host.Session.ActiveNode.ElementId, "scope-tree", StringComparison.Ordinal))
                {
                    rects.Add(rect);
                }
                return false;
            });
            host.DrawChecked(Viewport);
        }
        finally
        {
            SetButton(null);
        }
        return rects;
    }

    private static void PressScopeTreeButton(UiHost host, Rect target)
    {
        int fired = 0;
        try
        {
            SetButton(rect =>
            {
                if (string.Equals(host.Session.ActiveNode.ElementId, "scope-tree", StringComparison.Ordinal)
                    && RectMatches(rect, target))
                {
                    fired++;
                    return true;
                }
                return false;
            });
            host.DrawChecked(Viewport);
        }
        finally
        {
            SetButton(null);
        }
        Assert(fired == 1, "the scope-tree press must answer exactly once, got " + fired);
    }

    private static void TuningAreaCeremonyAndXg1Refusal()
    {
        FreshStack();
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        fake.AttachHost(host); // the draft-cleanup seam is per-host; the lane needs the REAL host
        host.Bindings.Invoke("set-tab", "Tuning");
        UiLayoutSnapshot snapA = Arrange(host);
        Rect tree = ToLocal(snapA, snapA.RectById["scope-tree"]);
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            // The action area at the default Global layer: the header's 132x28 restore entry is the
            // topmost candidate (the measured scope bands below it are 96/108-wide and 24 tall).
            List<Rect> buttons = RecordScopeTreeButtons(host).FindAll(r => AreaButton(r, tree));
            Assert(buttons.Count >= 1,
                "a valid target must offer the header area-restore entry, got " + buttons.Count);
            buttons.Sort((a, b) => a.y.CompareTo(b.y));
            int validCount = buttons.Count;

            PressScopeTreeButton(host, buttons[0]);
            UsConfirmWindow? flow = UsConfirmWindow.Active;
            Assert(flow != null, "the area press asks through the real window");
            Assert(flow!.MessageText.Contains("Global"),
                "the question must NAME the current layer/domain (§4.3), got '" + flow.MessageText + "'");
            Assert(fake.ResetActionAreaCount == 0 && fake.ResetMoodAreaCount == 0,
                "the area question writes nothing before the answer");
            flow.DialogBindings.Invoke("confirm-yes");
            Assert(fake.ResetActionAreaCount == 1 && fake.ResetMoodAreaCount == 0,
                "the action-area ceremony stages ONLY the action-area restore");

            // The mood area carries its own entry and its own key.
            host.Bindings.Set("tuning-area", 1);
            Arrange(host);
            List<Rect> moodButtons = RecordScopeTreeButtons(host).FindAll(r => AreaButton(r, tree));
            moodButtons.Sort((a, b) => a.y.CompareTo(b.y));
            Assert(moodButtons.Count >= 1, "the mood header draws its own area entry, got " + moodButtons.Count);
            PressScopeTreeButton(host, moodButtons[0]);
            Assert(UsConfirmWindow.Active != null, "setup: the mood press asked");
            Assert(UsConfirmWindow.Active!.MessageText.Contains("Global"), "the mood question names the layer too");
            UsConfirmWindow.Active!.DialogBindings.Invoke("confirm-yes");
            Assert(fake.ResetMoodAreaCount == 1 && fake.ResetActionAreaCount == 1,
                "one press, one area: the mood restore never runs the action area's key");

            // XG1: the empty xenotype target must offer NO area entry and refuse the command outright -
            // never a silent fall back to a Global-area restore.
            fake.ViewState.TuningArea = 0;
            fake.ViewState.TuningLayer = 2;
            fake.ViewState.TuningRaceDefName = "human";
            fake.ViewState.TuningXenotypeDefName = "";
            Arrange(host);
            List<Rect> xg1 = RecordScopeTreeButtons(host).FindAll(r => AreaButton(r, tree));
            Assert(xg1.Count == validCount - 1,
                "the empty-target layer removes exactly the header's entry and keeps every measured "
                + "scope band below it (" + validCount + " -> " + xg1.Count + ")");
            Assert(!host.Bindings.TryInvokeCommand("reset-action-area")
                    && !host.Bindings.TryInvokeCommand("reset-mood-area"),
                "and both area commands refuse - CanExecute is the single answer");
            Assert(fake.ResetActionAreaCount == 1 && fake.ResetMoodAreaCount == 1,
                "the refusal wrote nothing");
            Assert(!fake.CanResetTuningArea(), "the facade agrees with the guards");

            // A race-only layer names the RACE, not Global: layer 1 with a race is a valid target.
            fake.ViewState.TuningLayer = 1;
            Arrange(host);
            List<Rect> race = RecordScopeTreeButtons(host).FindAll(r => AreaButton(r, tree));
            race.Sort((a, b) => a.y.CompareTo(b.y));
            Assert(race.Count >= 1, "setup: the race layer offers the entry again");
            PressScopeTreeButton(host, race[0]);
            Assert(UsConfirmWindow.Active!.MessageText.Contains("Human"),
                "the question names the CURRENT domain, got '" + UsConfirmWindow.Active!.MessageText + "'");
            UsConfirmWindow.Active!.DialogBindings.Invoke("confirm-no");
            Assert(fake.ResetActionAreaCount == 1, "cancel again wrote nothing");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    private static void RowRestoreDirectAndGuarded()
    {
        FreshStack();
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        fake.AttachHost(host);
        host.Bindings.Invoke("set-tab", "Tuning");
        Arrange(host);

        // No action selected: the row command refuses and no button exists (layer 0, so the only
        // 104-wide right-flush button when it appears is the row's own restore).
        Assert(!host.Bindings.TryInvokeCommand("reset-action-row"),
            "with no selected action the row restore refuses");
        UiLayoutSnapshot snapT = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Rect tree = ToLocal(snapT, snapT.RectById["scope-tree"]); // the seam space (content-local)
        List<Rect> before = RecordScopeTreeButtons(host);
        Assert(!before.Exists(r => RowButton(r, tree)), "no row button without a selection");

        host.Bindings.Set("tuning-selected-action", "Eat");
        snapT = Arrange(host);
        List<Rect> after = RecordScopeTreeButtons(host).FindAll(r => RowButton(r, tree));
        Assert(after.Count == 1,
            "the selected action's editor row must draw exactly one right-flush 104-wide row restore, got "
            + after.Count);
        // The editor sits under the long scope list. The seam rects are content-local and do NOT
        // move with scroll, so the lane scrolls until the button's PAGE position is inside the
        // clip, then presses the same recorded rect.
        ScrollLocalIntoView(host, snapT, after[0]);
        Arrange(host);
        int rev = host.Session.ContentRevision;
        PressScopeTreeButton(host, after[0]);
        Assert(fake.ResetActionRowCount == 1 && fake.LastResetActionKey == "Eat",
            "the row restore is DIRECT (no window) and carries the selected action's key");
        Assert(UsConfirmWindow.Active == null, "a simple row restore must not ask");
        // An invalid layer identity refuses the row too (never a Global fall-through).
        fake.ViewState.TuningLayer = 2;
        fake.ViewState.TuningXenotypeDefName = "";
        Assert(!host.Bindings.TryInvokeCommand("reset-action-row") && fake.ResetActionRowCount == 1,
            "the empty-target layer refuses the row restore as well");
    }

    // The reset entries' declared bands (132/104 wide, 28 tall) are unique among everything the tree
    // draws: the measured scope triggers are 96/108-wide and 24 tall, the mood steppers 20x22 and the
    // full-row band 1020-wide. The two headers sit at different insets, so the predicates key on the
    // declared size only.
    private static bool AreaButton(Rect r, Rect tree)
        => Math.Abs(r.width - 132f) <= 0.5f && Math.Abs(r.height - 28f) <= 0.5f;

    private static bool RowButton(Rect r, Rect tree)
        => Math.Abs(r.width - 104f) <= 0.5f && Math.Abs(r.height - 28f) <= 0.5f;

    // ------------------------------------------------------------------ draft integration

    private static void DraftOcclusionCancelAndConfirm()
    {
        FreshStack();
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        fake.AttachHost(host);
        host.Bindings.Invoke("set-tab", "Overview");
        UiLayoutSnapshot snap = ArrangeIntoView(host, "timing-restore");
        // Spaces: the pointer events aim at the PAGE position (RectById at scroll 0, shifted by the
        // scroll), the seam predicates compare against the CONTENT-LOCAL rect the atom draws with.
        Rect fieldPage = ToPage(host, snap, snap.RectById["timing-multiplier-field"]);
        Rect fieldLocal = ToLocal(snap, snap.RectById["timing-multiplier-field"]);
        Rect header = ToPage(host, snap, snap.RectById["timing-header"]); // no input: the outside point

        // (1) Focus the field through a real press - the edit opens with the value as its draft.
        // The stub's GUIUtility is process-static: an earlier lane's held control would swallow
        // this press, so the lane releases it first (the XG1 ReleasePointer rule).
        GUIUtility.hotControl = 0;
        Vector2 center = new(fieldPage.x + fieldPage.width / 2f, fieldPage.y + fieldPage.height / 2f);
        Program.DrawWithPointer(host, Viewport, center);
        Program.DrawWithEvent(host, Viewport, EventType.MouseDown, center);
        Assert(host.Session.ActiveEditNode != null,
            "setup: the real press must open the field's edit (edit="
            + (host.Session.ActiveEditNode?.ElementId ?? "null") + ")");
        Program.DrawWithEvent(host, Viewport, EventType.MouseUp, center);

        // The atom's funnel keeps its own buffer slot; the lane's observable channels are the
        // session's edit record and the fake's write recorder.
        fake.LastCooldownMultiplier = null;

        // (2) The live field's EXISTING commit contract: a valid draft writes once.
        try
        {
            SetText((rect, text) => RectMatches(rect, fieldLocal) ? "0.75" : text);
            host.DrawChecked(Viewport);
        }
        finally
        {
            SetText(null);
        }
        Assert(Math.Abs((fake.LastCooldownMultiplier ?? 0f) - 0.75f) < 0.001f,
            "the live field's existing commit contract must still write a valid draft");
        fake.LastCooldownMultiplier = null;

        // (3) The question opening must not re-commit or discard the edit: the temporary window only
        // covers the page; commits belong to the field's own events, which the ceremony never sends.
        Assert(Program.PressDeclarativeButton(host, Viewport, "timing-restore") == 1, "the entry asks");
        Assert(UsConfirmWindow.Active != null, "setup: the real window opened");
        Arrange(host);
        Assert(fake.LastCooldownMultiplier == null && host.Session.ActiveEditNode != null,
            "occlusion alone neither re-commits nor discards the open edit");

        // (4) Cancel: no restore, and the edit survives untouched (the player may keep typing).
        UsConfirmWindow.Active!.DialogBindings.Invoke("confirm-no");
        Assert(fake.ResetNormalAreaCount == 0, "cancel does not execute the restore");
        Assert(host.Session.ActiveEditNode != null && fake.LastCooldownMultiplier == null,
            "cancel leaves the open edit exactly where it was");

        // (5) Confirm: the restore runs once AND the affected edit ends. The discard applies at the
        // field's own next draw; afterwards there is no live edit left to push the old 0.75 over
        // the restored value (the contract's central clause - without EndOpenDraft the focused
        // field stays live and THIS assert reddens).
        Assert(Program.PressDeclarativeButton(host, Viewport, "timing-restore") == 1, "the entry re-asks");
        int rev = host.Session.ContentRevision;
        UsConfirmWindow.Active!.DialogBindings.Invoke("confirm-yes");
        Assert(fake.ResetNormalAreaCount == 1 && fake.LastNormalResetArea == SqueakNormalResetArea.Timing,
            "the confirmed ceremony runs the timing-area restore exactly once");
        Assert(host.Session.ContentRevision == rev + 1, "Applied bumps once");
        Arrange(host); // the field's next draw applies the marked discard
        Assert(host.Session.ActiveEditNode == null,
            "the old edit ended with the restore - it no longer exists to overwrite it");
        fake.LastCooldownMultiplier = null;
        host.TryHandleAccept(); // Enter with no open edit: the caller keeps Verse's meaning and the
        Arrange(host);          // field must not resurrect a write - the restored value stands.
        Assert(fake.LastCooldownMultiplier == null,
            "a follow-up Enter / focus pass must not re-write the restored field");

        // (6) The comparison point: the field's own channel still works after all of it, and an
        // ordinary outside press ends the edit - the restore never borrowed the field's write.
        Program.DrawWithPointer(host, Viewport, center);
        Program.DrawWithEvent(host, Viewport, EventType.MouseDown, center);
        Program.DrawWithEvent(host, Viewport, EventType.MouseUp, center);
        fake.LastCooldownMultiplier = null; // the edit-open pass may re-commit the current value
        try
        {
            SetText((rect, text) => RectMatches(rect, fieldLocal) ? "0.9" : text);
            host.DrawChecked(Viewport);
        }
        finally
        {
            SetText(null);
        }
        Assert(Math.Abs((fake.LastCooldownMultiplier ?? 0f) - 0.9f) < 0.001f,
            "a fresh valid draft still commits through the existing live contract");
        Vector2 outside = new(header.x + header.width / 2f, header.y + header.height / 2f);
        Program.DrawWithPointer(host, Viewport, outside);
        Program.DrawWithEvent(host, Viewport, EventType.MouseDown, outside);
        Program.DrawWithEvent(host, Viewport, EventType.MouseUp, outside);
        Arrange(host);
        Assert(host.Session.ActiveEditNode == null,
            "the ordinary outside press ends the edit (the existing focus contract, undisturbed)");
    }

    // ------------------------------------------------------- review1: reachability, scope, identity

    /// <summary>PM review1 item 1: the long restore questions must be reviewable and their buttons
    /// clickable at the REAL shell. The lane opens the production ceremony (widget press), reads the
    /// shell's own measured InitialSize, then builds a host over the SAME page manifest and the SAME
    /// DialogBindings (the SA1.3 wiring precedent), lays it out at the shell's content box, and
    /// ANSWERS THROUGH THE REAL ENGINE BUTTON (the carrier's hit seam on the dialog page) - a
    /// DialogBindings.Invoke is not what this clause proves. The screen clamp is exercised with an
    /// absurd body: the window caps at the screen, the message scrolls, the buttons stay laid out.</summary>
    private static void WindowReachabilityRealButtons()
    {
        FreshStack();
        int savedScreen = Verse.UI.screenHeight;
        Verse.UI.screenHeight = 768; // the named human-pass box
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        host.Bindings.Invoke("set-tab", "Overview");
        ArrangeIntoView(host, "restore-center-button");
        try
        {
            foreach (string language in new[] { "English", "ChineseSimplified" })
            {
                Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
                Assert(Program.PressDeclarativeButton(host, Viewport, "restore-center-button") == 1,
                    language + ": the entry asks");
                UsConfirmWindow flow = UsConfirmWindow.Active!;
                Assert(flow != null && flow.OpenedWindow != null, language + ": setup window");
                Vector2 size = flow!.OpenedWindow!.InitialSize;
                Assert(size.y > 180f && size.y <= 768f - 40f + 0.5f,
                    language + ": the long question must GROW the shell from its measured body and stay "
                    + "inside the screen, got " + size.y);
                AssertButtonsReachable(flow, size, language + " global question");
                // Answer through the REAL dialog button: the staged global restore runs once.
                int before = fake.ResetAllCount;
                PressDialogButton(flow, "confirm-yes");
                Assert(fake.ResetAllCount == before + 1 && UsConfirmWindow.Active == null,
                    language + ": the real confirm button ran the staged action and closed");

                // And the real cancel button answers without running anything.
                Assert(Program.PressDeclarativeButton(host, Viewport, "restore-center-button") == 1,
                    language + ": re-ask");
                before = fake.ResetAllCount;
                PressDialogButton(UsConfirmWindow.Active!, "confirm-no");
                Assert(fake.ResetAllCount == before && UsConfirmWindow.Active == null,
                    language + ": the real cancel closed with zero writes");
            }

            // The screen clamp: a body taller than the screen caps the window, and the Scroll
            // container keeps the buttons laid out anyway (reachability is structural, not lucky).
            Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
            string absurd = string.Join(" ", System.Linq.Enumerable.Repeat(
                "This restore touches every Universal Squeaker setting, every tuning override, every "
                + "pack selection and every preset anchor while keeping all fallback tables, mappings "
                + "and author content exactly as they are.", 12));
            bool ran = false;
            UsConfirmWindow big = UsConfirmWindow.Open("Restore", absurd, "Confirm", () => ran = true)!;
            Vector2 capped = big.OpenedWindow!.InitialSize;
            Assert(Math.Abs(capped.y - (768f - 40f)) < 0.5f,
                "an over-tall body must cap at the screen's usable height, got " + capped.y);
            AssertButtonsReachable(big, capped, "capped absurd body");
            PressDialogButton(big, "confirm-yes");
            Assert(ran, "the capped dialog's real confirm still answers");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
            Verse.UI.screenHeight = savedScreen;
            if (UsConfirmWindow.Active != null)
            {
                UsConfirmWindow.Active.DialogBindings.Invoke("confirm-no");
            }
        }
    }

    private static void AssertButtonsReachable(UsConfirmWindow flow, Vector2 windowSize, string where)
    {
        Vector2 content = new(windowSize.x - 2f * 20f, windowSize.y - 56f - 20f);
        UiLayoutManifest manifest = UiLayoutManifest.Parse(flow.PageXml);
        using UiHost dialog = new("coahuilite.universalsqueaker", manifest,
            (UiBindings)flow.DialogBindings, UsTheme.Surface(), new Program.StubMetrics(),
            DialogTranslation());
        UiLayoutSnapshot snap = dialog.MeasureAndArrange(content);
        Rect yes = snap.RectById["confirm-yes"];
        Rect no = snap.RectById["confirm-no"];
        Assert(yes.yMax <= content.y + 0.5f && no.yMax <= content.y + 0.5f && yes.y >= 0f,
            where + ": confirm/cancel must be laid out INSIDE the shell's content box (yes "
            + yes.y + ".." + yes.yMax + ", no " + no.y + ".." + no.yMax + ", content " + content.y + ")");
    }

    private static void PressDialogButton(UsConfirmWindow flow, string elementId)
    {
        Vector2 size = flow.OpenedWindow!.InitialSize;
        Vector2 content = new(size.x - 2f * 20f, size.y - 56f - 20f);
        UiLayoutManifest manifest = UiLayoutManifest.Parse(flow.PageXml);
        using UiHost dialog = new("coahuilite.universalsqueaker", manifest,
            (UiBindings)flow.DialogBindings, UsTheme.Surface(), new Program.StubMetrics(),
            DialogTranslation());
        dialog.MeasureAndArrange(content);
        int fired = Program.PressDeclarativeButton(dialog, new Rect(0f, 0f, content.x, content.y), elementId);
        Assert(fired == 1, "the dialog's real " + elementId + " button must answer exactly once, got " + fired);
    }

    /// <summary>PM review1 item 2: the draft end is scoped to the operation's own fields and to
    /// Applied. A volume edit survives a diagnostics restore (Applied!), survives a NoChange timing
    /// restore, and is ended by the distance-area restore that really writes its field.</summary>
    private static void ScopedDraftBoundaries()
    {
        FreshStack();
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        fake.AttachHost(host);
        host.Bindings.Invoke("set-tab", "Overview");
        UiLayoutSnapshot snap = ArrangeIntoView(host, "global-volume-number");
        Rect field = ToPage(host, snap, snap.RectById["global-volume-number"]);
        Vector2 center = new(field.x + field.width / 2f, field.y + field.height / 2f);
        GUIUtility.hotControl = 0;
        Program.DrawWithPointer(host, Viewport, center);
        Program.DrawWithEvent(host, Viewport, EventType.MouseDown, center);
        Program.DrawWithEvent(host, Viewport, EventType.MouseUp, center);
        Assert(host.Session.ActiveEditNode != null, "setup: the volume field's edit is open");

        // Unrelated target, Applied: the diagnostics restore writes its own fields and must leave the
        // volume edit exactly where it is (the PM probe's case, now through the real command).
        host.Bindings.Invoke("reset-normal-diagnostics");
        Assert(fake.ResetNormalAreaCount == 1 && host.Session.ActiveEditNode != null,
            "an Applied restore of an UNRELATED area keeps the open edit (review1 item 2)");

        // Same target, NoChange: the timing restore wrote nothing, so nothing is cleaned.
        fake.NextResetOutcome = SqueakResetOutcome.NoChange;
        host.Bindings.Invoke("reset-normal-timing");
        Assert(fake.ResetNormalAreaCount == 2 && host.Session.ActiveEditNode != null,
            "a NoChange restore never ends an edit - not even one whose fields it owns");
        fake.NextResetOutcome = SqueakResetOutcome.Rejected;
        host.Bindings.Invoke("reset-normal-timing");
        Assert(host.Session.ActiveEditNode != null, "Rejected likewise leaves the edit untouched");
        fake.NextResetOutcome = SqueakResetOutcome.Applied;

        // Same target, Applied: the distance-area restore writes the volume field, so the edit ends.
        host.Bindings.Invoke("reset-normal-distance");
        // The fake counts every call: diagnostics Applied, timing NoChange, timing Rejected, distance.
        Assert(fake.ResetNormalAreaCount == 4, "the distance-area restore ran");
        Arrange(host);
        Assert(host.Session.ActiveEditNode == null,
            "the affected field's edit ended with the Applied restore that owns it");
    }

    /// <summary>PM review1 item 3: the staged area answer acts on the identity the question NAMED.
    /// Open A (Global) -> page identity drifts to B (a different VALID layer/domain) -> confirm must
    /// restore NEITHER (refuse), and B's records stay; re-asking at the captured identity restores
    /// exactly that one. The modal absorb is not trusted as the guarantee.</summary>
    private static void StagedIdentityDriftRefusal()
    {
        FreshStack();
        UiHost host;
        RecordingSettingsSource fake = RichHost(out host, out _);
        fake.AttachHost(host);
        host.Bindings.Invoke("set-tab", "Tuning");
        UiLayoutSnapshot snapA = Arrange(host);
        Rect tree = ToLocal(snapA, snapA.RectById["scope-tree"]);
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            List<Rect> cands = RecordScopeTreeButtons(host).FindAll(r => AreaButton(r, tree));
            cands.Sort((a, b) => a.y.CompareTo(b.y));
            Assert(cands.Count >= 1, "setup: the Global action entry is offered");
            PressScopeTreeButton(host, cands[0]);
            UsConfirmWindow flow = UsConfirmWindow.Active!;
            Assert(flow != null && flow.MessageText.Contains("Global"), "setup: the question named Global");

            // Drift: the page now sits on a DIFFERENT, perfectly valid identity (Race layer / Test Race).
            fake.ViewState.TuningLayer = 1;
            fake.ViewState.TuningRaceDefName = "testrace";
            flow!.DialogBindings.Invoke("confirm-yes");
            Assert(fake.ResetActionAreaCount == 0,
                "confirm at a drifted identity restores NOTHING - not the asked target, not the new one");
            Assert(UsConfirmWindow.Active == null, "the refused answer still closes the question");

            // Back to the captured identity: the same ceremony now restores it.
            fake.ViewState.TuningLayer = 0;
            fake.ViewState.TuningRaceDefName = "";
            Arrange(host);
            cands = RecordScopeTreeButtons(host).FindAll(r => AreaButton(r, tree));
            cands.Sort((a, b) => a.y.CompareTo(b.y));
            PressScopeTreeButton(host, cands[0]);
            flow = UsConfirmWindow.Active!;
            Assert(flow != null, "setup: re-ask at the captured identity");
            flow!.DialogBindings.Invoke("confirm-yes");
            Assert(fake.ResetActionAreaCount == 1, "the identity that was asked is the identity restored");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>US ships no InternalsVisibleTo (the Mod.cs pin); the dialog page's translation seam
    /// is the internal UsKernelTranslation, reached exactly the way the PM boundary probe reaches
    /// it - reflection on the product assembly.</summary>
    private static IUiTranslation DialogTranslation()
        => (IUiTranslation)Activator.CreateInstance(
            typeof(UsConfirmWindow).Assembly.GetType("UniversalSqueaker.UI.UsKernelTranslation", true),
            true)!;
}
