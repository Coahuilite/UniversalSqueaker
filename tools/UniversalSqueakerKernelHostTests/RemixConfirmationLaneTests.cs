using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// SA1.3: the equal-weight-Remix double confirmation. The lane drives the REAL production wiring -
/// the settings host's "mode" write registered against a live <see cref="RemixConfirmationFlow"/>,
/// the flow's own dialog command table (the same IUiBindings the UiPageWindow buttons invoke
/// through), and the embedded dialog manifest parsed from the production assembly.
///
/// What it proves (the contract's key verifications):
///  1. ENTERING Remix from another mode writes NOTHING until the second step's explicit enable -
///     step one advances, Cancel/Abort close, and the recorded mode write stays empty across all of
///     them ("第二步前零模式写入/取消");
///  2. already being in Remix re-answers with a direct no-op write and leaving Remix commits
///     directly - neither opens the flow ("已处于混合不重复确认、离开正常");
///  3. a request arriving while the flow is open is swallowed, never stacked ("同一父窗口只一个
///     确认流");
///  4. on the REAL shell content path - the production UiPageWindow the flow's catalog opens, driven
///     through its own WindowOnGUI at the registered 460x240 outer size, so the page is measured,
///     fit-audited and clicked at the 420x164 content box the chrome actually leaves, with the resolved
///     EN/ZH tables - the step-two geometry is MEASURED: the left slot is Cancel where step one's
///     continue was, the right slot is enable, and the OLD coordinate of the first click, clicked
///     again through the real buttons, closes the flow without ever reaching the enabling button
///     ("同位置连续点击不启用", the r1 correction; the r4 yardstick correction retires the r3 step's
///     lane-built 460x240 host and key-echo StubTranslation);
///  5. GUARD (declaration facts, not behaviour): the manifest's declared order and emphasis attributes
///     (continue Muted / cancel Active in step one, cancel Muted / enable Danger in step two) and the
///     ActionBind names agreeing with the flow's registered commands in both directions.
///
/// MUTATION LEDGER (run at PM's scheduled build; the reverts are named at their clauses):
///  - commit the mode inside RequestRemix instead of the enable command → clause 1 reddens;
///  - drop the `Mode != Remix` guard in the setter → clause 2 reddens (leaving/already-in would
///    confirm);
///  - make RequestRemix stack a second flow while open → clause 3 reddens;
///  - swap the manifest so step two's LEFT slot is confirm-enable -> clause 5 reddens on the
///    declaration; clause 4 reddens on the measured shell geometry (r4 observation, clause 5 parked:
///    the step-two slot-order assert fires first; the old-point clause is downstream of it);
///  - rename one ActionBind without renaming the flow command -> clause 5 reddens.
///  - drop the equal-height body regions (the r4 row-stability fix) -> clause 5 reddens on the region
///    guard, and the ZH shell pass drifts the row 22px up (the measured defect the fix was cut for).
/// The r1 clause-4 evidence was the XML order alone; the shell-path clauses above are what the PM r2/r4
/// rulings asked for and the ledger names them as the behavioural proof.
/// </summary>
internal static class RemixConfirmationLaneTests
{
    private const string DialogManifestResource = "UniversalSqueaker.UI.Layout.RemixConfirm.Schema2.xml";
    private static readonly Vector2 PageBox = new Vector2(760f, 524f);

    public static int RunAll()
    {
        var metrics = new Program.StubMetrics();
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            EnteringRemixWritesNothingUntilTheSecondStepEnable(metrics);
            AlreadyRemixAndLeavingRemixNeverEnterTheFlow(metrics);
            ReentryWhileOpenIsSwallowedAndCancelAndAbortWriteNothing(metrics);
            TheDialogManifestDeclaresTheSlotSwapAndEmphasis();
            TheRealShellPageMakesTheOldPointHitCancelOnTheSecondClick();
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }

        Console.WriteLine("RemixConfirmationLaneTests ALL PASS");
        return 0;
    }

    // 1. The full commit sequence through the production write path.
    private static void EnteringRemixWritesNothingUntilTheSecondStepEnable(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = false }; // the fixture's view mode is Vanilla
        var flow = new RemixConfirmationFlow(new WindowStack());
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics, out _, flow);
        host.MeasureAndArrange(PageBox);

        fake.LastMode = null;
        host.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(fake.LastMode == null,
            "entering Remix must write NO mode at the boundary - the flow owns the request");
        Assert(flow.Step == 1, "the request opens the first step, got " + flow.Step);

        flow.DialogBindings.Invoke("confirm-continue");
        Assert(flow.Step == 2, "continue advances to the final step, got " + flow.Step);
        Assert(fake.LastMode == null, "step one still writes nothing");

        flow.DialogBindings.Invoke("confirm-enable");
        Assert(flow.Step == 0, "the enable command closes the flow, got " + flow.Step);
        Assert(fake.LastMode == SqueakVoicePackMode.Remix,
            "only the second step's explicit enable commits, got "
            + (fake.LastMode?.ToString() ?? "no write"));

        Console.WriteLine("[sa1-remix] enter->continue->enable commits once: LastMode=" + fake.LastMode);
    }

    // 2. Already-in-Remix and leaving-Remix bypass the flow entirely.
    private static void AlreadyRemixAndLeavingRemixNeverEnterTheFlow(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true }; // the fixture's view mode is Remix
        var flow = new RemixConfirmationFlow(new WindowStack());
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics, out _, flow);
        host.MeasureAndArrange(PageBox);

        fake.LastMode = null;
        host.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(flow.Step == 0, "already in Remix must not re-confirm, the flow stayed closed");
        Assert(fake.LastMode == SqueakVoicePackMode.Remix,
            "the already-in-Remix re-answer is the direct (no-op) typed write");

        fake.LastMode = null;
        host.Bindings.Set("mode", SqueakVoicePackMode.Fallback);
        Assert(flow.Step == 0, "leaving Remix must commit directly, no confirmation");
        Assert(fake.LastMode == SqueakVoicePackMode.Fallback,
            "leaving Remix writes the chosen mode at the boundary, got "
            + (fake.LastMode?.ToString() ?? "no write"));

        Console.WriteLine("[sa1-remix] already-in and leaving bypass the flow: Step stays 0");
    }

    // 3. One flow per parent: a re-request is swallowed; Cancel and the parent's Abort both write nothing.
    private static void ReentryWhileOpenIsSwallowedAndCancelAndAbortWriteNothing(Program.StubMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = false };
        var flow = new RemixConfirmationFlow(new WindowStack());
        using UiHost host = UsKernelSettingsHost.Create(fake, metrics, out _, flow);
        host.MeasureAndArrange(PageBox);

        fake.LastMode = null;
        host.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(flow.Step == 1, "the first request opens step one");
        host.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(flow.Step == 1, "a re-request while open is swallowed, never stacked, got " + flow.Step);
        flow.DialogBindings.Invoke("confirm-continue");
        host.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(flow.Step == 2, "the re-request does not disturb the open final step, got " + flow.Step);
        Assert(fake.LastMode == null, "nothing through any of the swallowed requests");

        flow.DialogBindings.Invoke("confirm-cancel");
        Assert(flow.Step == 0 && fake.LastMode == null, "Cancel closes without writing");

        host.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        flow.Abort(); // the parent window's PostClose path
        Assert(flow.Step == 0 && fake.LastMode == null, "the parent's close releases the flow without writing");

        host.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(flow.Step == 1, "after the release, a fresh request starts a fresh first step");
        flow.Abort();

        Console.WriteLine("[sa1-remix] one flow per parent; cancel/abort keep the old mode");
    }

    // 5. GUARD: the dialog manifest's DECLARATION - slot order, emphasis, step gating, command agreement.
    private static void TheDialogManifestDeclaresTheSlotSwapAndEmphasis()
    {
        string xml;
        using (Stream? stream = typeof(RemixConfirmationFlow).Assembly
                   .GetManifestResourceStream(DialogManifestResource))
        {
            Assert(stream != null, "the production assembly embeds the SA1.3 dialog manifest");
            using var reader = new StreamReader(stream!);
            xml = reader.ReadToEnd();
        }

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        UiElementSpec buttons = FindById(manifest.Roots, "confirm-buttons")
            ?? throw new InvalidOperationException("the dialog declares the confirm-buttons row");
        Assert(buttons.Kind == "Row", "the dialog's button band is a Row");

        var step1 = new List<UiElementSpec>();
        var step2 = new List<UiElementSpec>();
        var commands = new List<string>();
        foreach (UiElementSpec child in buttons.Children)
        {
            Assert(child.TryGetAttribute("VisibleKey", out string visibleKey),
                "every dialog button is gated by a step key: " + child.Id);
            Assert(child.TryGetAttribute("ActionBind", out string actionBind),
                "every dialog button binds a command: " + child.Id);
            (visibleKey == "confirm-step1" ? step1 : step2).Add(child);
            commands.Add(actionBind);
            Assert(child.TryGetAttribute("TextKey", out string textKey) && textKey.StartsWith("US.", StringComparison.Ordinal),
                "the dialog's captions are Keyed, never literals: " + child.Id);
        }

        Assert(step1.Count == 2 && step2.Count == 2,
            "each step shows exactly two buttons, got " + step1.Count + "/" + step2.Count);

        // The swap: step one's LEFT is the continue command and its RIGHT is Cancel; step two's LEFT is
        // Cancel and its RIGHT is the enabling command. A click at the left point therefore meets
        // Cancel in step two, and the right point's step-one click CLOSES the flow - no same-point
        // chain can land on enable.
        Assert(step1[0].Id == "confirm-continue"
                && step1[1].TryGetAttribute("ActionBind", out string right1) && right1 == "confirm-cancel"
                && step2[0].TryGetAttribute("ActionBind", out string left2) && left2 == "confirm-cancel"
                && step2[1].Id == "confirm-enable",
            "the two steps swap the slots (continue|cancel -> cancel|enable), got "
            + step1[0].Id + "|" + step1[1].Id + " -> " + step2[0].Id + "|" + step2[1].Id);

        // The emphasis ruling (r1 correction): step one highlights Cancel with the existing ACTIVE tone
        // (SR's gold), continue is Muted; step two mutes Cancel and carries the enabling button on Danger.
        Assert(step1[0].TryGetAttribute("Emphasis", out string muteContinue) && muteContinue == "Muted"
                && step1[1].TryGetAttribute("Tone", out string cancel1Tone) && cancel1Tone == "Active"
                && !step1[1].TryGetAttribute("Emphasis", out _),
            "step one: continue muted, cancel the prominent (Active) control");
        Assert(step2[0].TryGetAttribute("Emphasis", out string muteCancel) && muteCancel == "Muted"
                && step2[1].TryGetAttribute("Tone", out string dangerTone) && dangerTone == "Danger",
            "step two: cancel muted, enable carries the Danger emphasis");

        // The two bodies are gated by the same step keys as their buttons - one truth per step - and
        // BOTH live in regions of the SAME fixed height: the r4 shell measurement proved a freely
        // flowing body moves the button row between steps (22px on the ZH table), so row stability is
        // now a declared invariant, not an accident of one language's line count.
        UiElementSpec bodyFirst = FindById(manifest.Roots, "confirm-body-first")
            ?? throw new InvalidOperationException("the dialog declares its first-step body");
        UiElementSpec bodySecond = FindById(manifest.Roots, "confirm-body-second")
            ?? throw new InvalidOperationException("the dialog declares its second-step body");
        UiElementSpec regionFirst = FindById(manifest.Roots, "confirm-body-first-region")
            ?? throw new InvalidOperationException("the first-step body lives in a gated region");
        UiElementSpec regionSecond = FindById(manifest.Roots, "confirm-body-second-region")
            ?? throw new InvalidOperationException("the second-step body lives in a gated region");
        Assert(regionFirst.TryGetAttribute("VisibleKey", out string b1) && b1 == "confirm-step1"
                && regionSecond.TryGetAttribute("VisibleKey", out string b2) && b2 == "confirm-step2",
            "the body regions share the step gating with the buttons");
        Assert(regionFirst.TryGetAttribute("Height", out string h1)
                && regionSecond.TryGetAttribute("Height", out string h2) && h1 == h2,
            "both steps carry the SAME body region height - the button row cannot shift between steps");

        // 5. Manifest <-> command table agreement, both directions.
        var flow = new RemixConfirmationFlow(new WindowStack());
        foreach (string command in commands)
        {
            Assert(flow.DialogBindings.CanExecute(command),
                "the flow registers the dialog's command '" + command + "'");
        }

        foreach (string command in new[] { "confirm-continue", "confirm-cancel", "confirm-enable" })
        {
            Assert(commands.Contains(command),
                "the dialog binds the flow's command '" + command + "'");
        }

        Console.WriteLine("[sa1-remix] dialog slots swap continue|cancel -> cancel|enable; commands agree");
    }

    // 4. THE r2 proof: a REAL UiHost over the flow's dialog page - measured geometry, real button clicks.
    private static void TheRealShellPageMakesTheOldPointHitCancelOnTheSecondClick()
    {
        ShellPass("EN", "English");
        ShellPass("ZH", "ChineseSimplified");
    }

    private static void ShellPass(string label, string languageFolder)
    {
        var table = Program.ReadKeyedTable(languageFolder);
        Program.SetTranslatorResolver(table);
        var fake = new RecordingSettingsSource { RichData = false };
        var drawn = new DrawnTextMetrics();
        var flow = new RemixConfirmationFlow(new WindowStack(), drawn);
        using UiHost settings = UsKernelSettingsHost.Create(fake, new Program.StubMetrics(), out _, flow);
        settings.MeasureAndArrange(PageBox);
        var outer = new Rect(0f, 0f, 460f, 240f);
        var reports = new List<UiOverflowReport>();
        UiHost? page = null;
        UiWindowHost? shell = null;
        Rect viewport = default;

        // One honest pass of the PRODUCTION shell: windowRect set to the registered outer size, then
        // WindowOnGUI - the first pass creates the page host and announces it (both subscribers see it:
        // the flow wires its bump channel, the lane keeps the handle); a reopen builds a fresh window
        // and the same subscription captures the fresh host. The fit audit is armed for the pass, so
        // the shell's own draw is the measurement.
        void Drive(string phase)
        {
            shell = flow.OpenedWindow
                ?? throw new InvalidOperationException(label + " " + phase + ": the catalog opened no window");
            shell.HostAttached += host => page = host;
            reports.Clear();
            UiFitAudit.Attach(drawn, reports.Add);
            UiFitAudit.Enabled = true;
            try
            {
                shell.windowRect = outer;
                shell.WindowOnGUI();
            }
            finally
            {
                UiFitAudit.Detach();
                UiFitAudit.Enabled = false;
            }

            _ = page ?? throw new InvalidOperationException(label + " " + phase + ": shell never attached");
        }

        // A click the way the game delivers one: a window-space event (the page-local rect plus the
        // content origin the shell drew at) through the shell's own WindowOnGUI. Drawing the page host
        // directly would bypass the shell's draw guard, which absorbs the teardown of a close fired by
        // a command mid-pass - the state change lands either way, and the game never shows the player
        // the rest.
        void Click(Vector2 pagePoint)
        {
            var at = new Vector2(pagePoint.x + viewport.x, pagePoint.y + viewport.y);
            for (int i = 0; i < 2; i++)
            {
                Event e = Event.KeyboardEvent("dummy");
                e.type = i == 0 ? EventType.MouseDown : EventType.MouseUp;
                e.button = 0;
                e.mousePosition = at;
                Event.current = e;
                try { shell!.WindowOnGUI(); }
                finally { Event.current = null; }
            }

            GUIUtility.hotControl = 0;
        }

        fake.LastMode = null;
        settings.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Assert(flow.Step == 1, "the request opens step one");
        Drive("step one open");
        viewport = page!.Session.HostViewport;
        Assert(Mathf.Abs(viewport.width - 420f) < 0.51f && Mathf.Abs(viewport.height - 164f) < 0.51f,
            label + ": the real shell must leave the page 420x164 for the registered 460x240 outer window"
            + " (chrome: 20px sides, 56px title bar), got " + DescribeRect(viewport));
        Assert(reports.Count == 0, label + " step one: resolved text must fit the real page, got "
            + reports.Count + " fit findings: " + DescribeReports(reports));

        UiLayoutSnapshot first = page.MeasureAndArrange(viewport.size);
        Assert(first.RectById.TryGetValue("confirm-continue", out Rect continueRect),
            label + " step one: the real shell must arrange the continue button");
        Assert(first.RectById.TryGetValue("confirm-cancel-first", out Rect cancelFirstRect),
            label + " step one: the real shell must arrange the prominent cancel");
        Assert(!first.RectById.ContainsKey("confirm-enable")
                && !first.RectById.ContainsKey("confirm-cancel-second"),
            "step two's buttons must NOT be arranged while step one is showing");
        Assert(continueRect.x < cancelFirstRect.x,
            label + " step one: continue is the LEFT slot and cancel the right one");
        AssertFits(viewport, label, "step one", ("continue", continueRect), ("cancel-first", cancelFirstRect));
        Assert(drawn.Measured(table["US.Remix.Confirm1.Body"])
                && drawn.Measured(table["US.Remix.Confirm.Continue"])
                && drawn.Measured(table["US.Common.Cancel"]),
            label + ": the real shell must have MEASURED the resolved step-one body and both button labels");

        // The OLD coordinate: the centre of the left button the player has just pressed, clicked
        // THROUGH THE SHELL (window-space event, the shell's own pass).
        var oldPoint = Center(continueRect);
        Click(oldPoint);
        Assert(flow.Step == 2, "a real click on the left button advances to the final step, got " + flow.Step);
        Assert(fake.LastMode == null, "step one still writes nothing");

        Drive("step two");
        Assert(reports.Count == 0, label + " step two: resolved text must fit the real page, got "
            + reports.Count + " fit findings: " + DescribeReports(reports));
        UiLayoutSnapshot swap = page.MeasureAndArrange(viewport.size);
        Assert(swap.RectById.TryGetValue("confirm-cancel-second", out Rect cancelSecondRect),
            label + " step two: the real shell must arrange the swap cancel");
        Assert(swap.RectById.TryGetValue("confirm-enable", out Rect enableRect),
            label + " step two: the real shell must arrange the enabling button");
        Assert(!swap.RectById.ContainsKey("confirm-continue")
                && !swap.RectById.ContainsKey("confirm-cancel-first"),
            "the swap is a re-gating: step one's buttons are gone, not covered");
        Assert(cancelSecondRect.x < enableRect.x,
            label + " step two: cancel is the LEFT slot and enable the right one");
        AssertFits(viewport, label, "step two", ("cancel-second", cancelSecondRect), ("enable", enableRect));
        Assert(drawn.Measured(table["US.Remix.Confirm2.Body"])
                && drawn.Measured(table["US.Remix.Confirm.Enable"]),
            label + ": the real shell must have MEASURED the resolved step-two body and the enable label");

        // THE measured clause: the old point now sits on Cancel and NOT on enable.
        Assert(ContainsPoint(cancelSecondRect, oldPoint) && !ContainsPoint(enableRect, oldPoint),
            label + ": the same coordinate must meet Cancel in step two: point (" + Num(oldPoint.x) + ","
            + Num(oldPoint.y) + ") cancel " + DescribeRect(cancelSecondRect) + " enable "
            + DescribeRect(enableRect));

        // The second click AT THAT COORDINATE closes the flow without ever enabling - through the
        // shell pass, whose draw guard absorbs the mid-pass teardown exactly like the game does.
        Click(oldPoint);
        Assert(flow.Step == 0, "the same-position second click cancels the flow, got step " + flow.Step);
        Assert(fake.LastMode == null, "and it never commits");

        // The deliberate path stays alive on the same ruler: enter again (a fresh real shell), press the
        // left button, then the RIGHT one.
        UiHost firstPage = page;
        settings.Bindings.Set("mode", SqueakVoicePackMode.Remix);
        Drive("deliberate step one");
        Assert(!ReferenceEquals(page, firstPage), "the reopened dialog is a fresh window and host");
        viewport = page!.Session.HostViewport;
        UiLayoutSnapshot third = page.MeasureAndArrange(viewport.size);
        Click(Center(RectOf(third, "confirm-continue")));
        Assert(flow.Step == 2, "the fresh flow reaches the final step again");
        Drive("deliberate step two");
        UiLayoutSnapshot fourth = page.MeasureAndArrange(viewport.size);
        Click(Center(RectOf(fourth, "confirm-enable")));
        Assert(flow.Step == 0 && fake.LastMode == SqueakVoicePackMode.Remix,
            "the explicit enable click on the right slot commits once, got step " + flow.Step
            + " mode=" + (fake.LastMode?.ToString() ?? "no write"));

        Console.WriteLine("[sa1-remix-shell " + label + "] real 460x240 shell leaves page "
            + DescribeRect(viewport) + "; old point (" + Num(oldPoint.x) + "," + Num(oldPoint.y)
            + ") hits cancel in step two; deliberate right-slot click enables; zero fit findings");
    }

    private static void AssertFits(Rect viewport, string label, string phase, params (string, Rect)[] rects)
    {
        foreach ((string name, Rect rect) in rects)
        {
            Assert(rect.x >= -0.01f && rect.y >= -0.01f
                    && rect.xMax <= viewport.width + 0.01f && rect.yMax <= viewport.height + 0.01f,
                label + " " + phase + ": '" + name + "' must sit fully inside the real page "
                + DescribeRect(viewport) + ", got " + DescribeRect(rect));
        }
    }

    private static string DescribeReports(List<UiOverflowReport> reports)
    {
        var lines = new List<string>();
        foreach (UiOverflowReport report in reports) lines.Add(report.ToString() ?? "?");
        return string.Join(" | ", lines);
    }

    private sealed class DrawnTextMetrics : ITextMetrics
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

    private static Rect RectOf(UiLayoutSnapshot snapshot, string id)
    {
        return snapshot.RectById.TryGetValue(id, out Rect rect)
            ? rect
            : throw new InvalidOperationException("the dialog did not arrange '" + id + "'");
    }

    private static Vector2 Center(Rect rect)
    {
        return new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
    }

    private static bool ContainsPoint(Rect rect, Vector2 point)
    {
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
    }

    private static string Num(float value)
    {
        return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string DescribeRect(Rect rect)
    {
        return "(" + Num(rect.x) + "," + Num(rect.y) + " " + Num(rect.width) + "x" + Num(rect.height) + ")";
    }

    private static UiElementSpec? FindById(IEnumerable<UiElementSpec> elements, string id)
    {
        foreach (UiElementSpec element in elements)
        {
            if (string.Equals(element.Id, id, StringComparison.Ordinal)) return element;
            UiElementSpec? nested = FindById(element.Children, id);
            if (nested != null) return nested;
        }

        return null;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
