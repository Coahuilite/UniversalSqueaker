using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The FL-20 seam lane: the text-fit audit is routed per HOST, and measured with that host's own ruler.
/// <para>
/// What this asserts, and what it deliberately does not. The claim is <b>routing and ruler isolation</b>:
/// a finding published while host A's scope is open lands in A's subscription, carries A's identity, and
/// was measured with A's <see cref="ITextMetrics"/> - so B's verdict is not decided by A's adapter. It is
/// <b>not</b> "subscribing removes every interference": <see cref="UiFitAudit.Enabled"/> is still one
/// process-wide switch, and the last clause below asserts that on purpose - with the switch off, a
/// subscribed host receives nothing at all. That is the external review's X-27 boundary, and it is a lane
/// assertion here rather than a slogan in a comment.
/// </para>
/// <para>
/// The ruler signal is a flood ruler for A and the calibrated stub for B: A's page must report findings
/// and B's must report none (the shipped page draws clean under the stub - the width/language sweep is the
/// standing evidence for that). If B ever reported A's findings, B would be non-zero and this reddens.
/// </para>
/// </summary>
internal static class UsAuditRoutingLaneTests
{
    private static readonly Vector2 Viewport = new(1024f, 720f);

    public static int RunAll()
    {
        Step("per-host audit routing + ruler isolation + the shared detection switch", TheAuditIsRoutedPerHost);
#if US_DEV
        Step("repeated clicks emit no geometry; each explicit report is its own pass",
            RepeatedClicksAreSeparateEvidence);
        Step("one report request produces one report AND one retained snapshot", TheReportIsOneShot);
#endif
        Console.WriteLine("UsAuditRoutingLaneTests ALL PASS");
        return 0;
    }

#if US_DEV
    /// <summary>
    /// R3-B's one-shot lifecycle, driven the way the page drives it: a REAL bound command asks for a report,
    /// real <see cref="UiHost.DrawFrame"/> passes supply the captures, and the pending request is read back
    /// through the same source the window calls.
    /// <para>
    /// MUTATION LEDGER. <b>TheRequestIsNotConsumedByTheClickAndNotUntilAPassCompletes</b> is the proof that the
    /// request is deferred rather than served from the click's own pass: a straight
    /// <c>request -&gt; report</c> (no latch) reddens it. <b>OneRequestOneReport</b> is the proof that the
    /// request is CLEARED: a consume that forgot to clear reddens it on the second pass. Both are asserted
    /// through returned pass numbers, not by counting log lines, so a message that changed shape cannot make
    /// them pass for the wrong reason.
    /// </para>
    /// </summary>
    private static void TheReportIsOneShot()
    {
        var observer = typeof(Verse.Log).GetField("MessageObserver")
            ?? throw new InvalidOperationException("The logging stub needs its scoped message observer.");
        object? previousObserver = observer.GetValue(null);
        SqueakDevLoggingMode previousMode = SqueakLog.Mode;
        var lines = new List<string>();
        var fake = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        fake.AttachHost(host);
        UsKernelWidgetRegistrar.EnsureRegistered();

        try
        {
            observer.SetValue(null, (Action<string>)lines.Add);
            SqueakLog.Configure(SqueakDevLoggingMode.Enabled);
            // `auditFit: true` because this lane exercises the fit path; the scope's existence no longer
            // depends on the logging policy (R3-B fix 4).
            using UsTextFitAudit audit = UsTextFitAudit.Open(host, auditFit: true);
            var viewport = new Rect(0f, 0f, 1024f, 720f);

            // The instrument is OFF until the real command asks for it, and reading status does not change
            // that (R3-B fix 3).
            Assert(!host.Diagnostics.GeometryEnabled,
                "the developer command, not the scope, is what enables capture (R3-B / B2)");
            Assert(UsTextFitAudit.GetDevGeometryStatus(host) == UsTextFitAudit.DevGeometryStatus.Off
                && !host.Diagnostics.GeometryEnabled,
                "reading the status must not enable capture (R3-B fix 3)");
            host.Bindings.Invoke("set-tab", "Overview");
            // A report requested BEFORE capture is on must be refused and not left pending, so a later enable
            // cannot satisfy a stale click (R3-B fix 7).
            host.Bindings.Invoke("request-layout-report");
            Assert(!fake.LayoutReportPending && !audit.IsGeometryReportPending,
                "a report request made while capture is off must be refused, not left pending");
            host.Bindings.Set("layout-capture", true);
            Assert(fake.LayoutCaptureOn && host.Diagnostics.GeometryEnabled,
                "the page's own layout-capture binding must reach this host's instrument");

            // One real pass, so a capture exists, and the pass number it produced.
            host.DrawFrame(viewport);
            Assert(host.Diagnostics.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? first) && first != null,
                "a real drawn pass must produce a capture for the instrument to report");
            int capturePass = first!.Pass;

            // THE REQUEST, from the real bound command the manifest dispatches.
            host.Bindings.Invoke("request-layout-report");
            Assert(fake.LayoutReportRequests == 1, "the report command must reach the source once");
            Assert(audit.IsGeometryReportPending,
                "the request must be PENDING after the click: it is not served from the click's own pass");
            Assert(UsTextFitAudit.PublishGeometryReport(host) < 0 && audit.IsGeometryReportPending,
                "and it must not be consumed while the pass it is waiting on is the one that already existed"
                + " - a report served from the pre-click capture would describe the wrong frame");

            // A real pass AFTER the request is what completes it.
            host.DrawFrame(viewport);
            int reported = UsTextFitAudit.PublishGeometryReport(host);
            Assert(reported > capturePass,
                "the report must describe a pass NEWER than the one that existed when it was requested"
                + " (requested after pass " + capturePass + ", reported pass " + reported + ")");
            Assert(!audit.IsGeometryReportPending, "and the request must be CLEARED in the same step");
            Assert(lines.Exists(line => line.Contains("geometry [ferritelib.geometry]")),
                "the report must go to the existing layout-trace channel, got " + lines.Count + " line(s)");

            // ONE request is ONE report: another pass and another consume publish nothing.
            int published = lines.Count;
            host.DrawFrame(viewport);
            Assert(UsTextFitAudit.PublishGeometryReport(host) < 0,
                "a second pass must not produce a report: the request was cleared by the first");
            Assert(lines.Count == published,
                "and nothing new may be written either (" + published + " -> " + lines.Count + " lines)");

            // FIX 5: the SAME capture is retained as data AND as text, reachable through the consumer's own
            // accessor. A report that only kept one rendering could not be checked against the other, and a
            // second read would have been free to describe a later pass.
            UiDevGeometrySnapshot? retained = fake.LayoutReportSnapshot;
            Assert(retained != null, "the explicit report must retain the STRUCTURED rendering, not only text");
            Assert(retained!.Pass == reported,
                "and the structured snapshot must describe the pass the report claims (" + retained.Pass
                + " vs " + reported + "), so the two renderings are one capture");
            Assert(audit.HasGeometryReport && audit.LatestGeometryReportText.Length > 0,
                "with the matching TEXT rendering retained beside it");
            Assert(audit.LastGeometryReportPass == reported
                && audit.LastGeometryReportNodeCount == retained.Nodes.Count
                && audit.LastGeometryReportInputCount == retained.Inputs.Count,
                "and the text rendering's own identity must agree with the same snapshot (pass "
                + audit.LastGeometryReportPass + ", nodes " + audit.LastGeometryReportNodeCount + ", inputs "
                + audit.LastGeometryReportInputCount + ")");
            Console.WriteLine("  ok: one request -> one report on pass " + reported
                + " (nodes " + retained.Nodes.Count + ", inputs " + retained.Inputs.Count
                + "), nothing on the next pass");
        }
        finally
        {
            SqueakLog.Configure(previousMode);
            observer.SetValue(null, previousObserver);
        }
    }
#endif

#if US_DEV
    private static void RepeatedClicksAreSeparateEvidence()
    {
        var observer = typeof(Verse.Log).GetField("MessageObserver")
            ?? throw new InvalidOperationException("The logging stub needs its scoped message observer.");
        object? previousObserver = observer.GetValue(null);
        SqueakDevLoggingMode previousMode = SqueakLog.Mode;
        var lines = new List<string>();
        int commands = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("act", () => commands++);
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"audit-repeat\">"
            + "<Widget Id=\"button\" Kind=\"input/button\" Text=\"Press\" Height=\"30\" ActionBind=\"act\" /></UiPage>");
        using var host = new UiHost("audit-repeat", manifest, bindings, UsTheme.Surface(),
            new Program.StubMetrics(), new AuditTranslation());
        try
        {
            observer.SetValue(null, (Action<string>)lines.Add);
            SqueakLog.Configure(SqueakDevLoggingMode.Enabled);

            // R3-B, and it is the MUTATION PROOF for the decoupling: turning detailed logging on and OPENING
            // the audit scope must NOT start the geometry instrument. Before this slice the scope enabled it
            // on open, which is exactly what made "set the log level to Enabled" silently begin capturing
            // every rect. Re-coupling them reddens this assertion by name.
            using var audit = UsTextFitAudit.Open(host, auditFit: true);
            Assert(!host.Diagnostics.GeometryEnabled,
                "opening the audit must not enable geometry: detailed logging and rect capture are separate"
                + " developer decisions (R3-B / B2)");

            // It is enabled only by the explicit, per-host request.
            Assert(UsTextFitAudit.SetGeometryCapture(host, true),
                "the explicit per-host enable must be honoured on a Dev carrier");
            Assert(host.Diagnostics.GeometryEnabled, "this Dev integration lane requires a Dev carrier with geometry enabled");
            var viewport = new Rect(0f, 0f, 200f, 100f);
            Rect button = host.MeasureAndArrange(new Vector2(200f, 100f)).RectById["button"];

            // The geometry report is the EXPLICIT action now (R3-B fix 6): a business press emits nothing, so
            // each phase below has to ask. This helper is the same requirement the page's Report button makes.
            var reportedPasses = new List<int>();
            Func<int> reportNow = () =>
            {
                Assert(UsTextFitAudit.RequestGeometryReport(host), "the explicit report request must be honoured");
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    host.DrawFrame(viewport);
                    int pass = UsTextFitAudit.PublishGeometryReport(host);
                    if (pass >= 0)
                    {
                        int logged = lines.Count;
                        // Draining again in the same pass must not duplicate the block (the request is gone).
                        Assert(UsTextFitAudit.PublishGeometryReport(host) < 0 && lines.Count == logged,
                            "one report per request: a second drain in the same pass must write nothing");
                        reportedPasses.Add(pass);
                        return pass;
                    }
                }

                throw new InvalidOperationException("a report request must be satisfied within a bounded number of passes");
            };

            for (int click = 0; click < 2; click++)
            {
                foreach (EventType phase in new[] { EventType.MouseDown, EventType.MouseUp })
                {
                    Event raised = Event.KeyboardEvent("");
                    raised.type = phase;
                    raised.button = 0;
                    raised.mousePosition = button.center;
                    Event.current = raised;
                    // NO audit.Publish() geometry: a press must not spend a dump on the instrument.
                    int before = lines.Count;
                    host.DrawFrame(viewport);
                    audit.Publish();
                    Assert(lines.Count == before || !lines[lines.Count - 1].StartsWith("geometry "),
                        "a business press must not emit a geometry dump on its own (R3-B fix 6)");
                    reportNow();
                }
            }

            Assert(commands == 2, "both identical native clicks must execute the command");
            Assert(reportedPasses.Count == 4,
                "each of the four phases must produce exactly one explicit report, got " + reportedPasses.Count);
            Assert(new HashSet<int>(reportedPasses).Count == reportedPasses.Count,
                "and each must describe a DISTINCT pass (" + string.Join(",", reportedPasses) + "): identical"
                + " input text in different passes must never be deduplicated into one observation");

            // Consecutive unclaimed releases have identical input text but distinct pass identities. They can
            // occur when capture was lost; the emitter must preserve both observations, which is what the old
            // header dedup suppressed.
            for (int release = 0; release < 2; release++)
            {
                Event raised = Event.KeyboardEvent("");
                raised.type = EventType.MouseUp;
                raised.button = 0;
                raised.mousePosition = button.center;
                Event.current = raised;
                reportNow();
            }

            // One block per emitted report, and no more: the geometry count is the emitted count, not the
            // number of frames or presses.
            int blocks = lines.FindAll(line => line.Contains("geometry [ferritelib.geometry]")).Count;
            Assert(blocks >= reportedPasses.Count,
                "every emitted report must have written its block, got " + blocks + " for "
                + reportedPasses.Count + " reports");
            Assert(new HashSet<int>(reportedPasses).Count == 6,
                "six explicit reports must describe six distinct passes");
            Console.WriteLine("  ok: " + reportedPasses.Count + " explicit reports over distinct passes ("
                + string.Join(",", reportedPasses) + "); presses emit no geometry; one block per report");
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
            observer.SetValue(null, previousObserver);
            SqueakLog.Configure(previousMode);
        }
    }
    private sealed class AuditTranslation : IUiTranslation
    {
        public string Translate(string key) => key;
        public int TranslationRevision => 0;
    }
#endif

    private static void TheAuditIsRoutedPerHost()
    {
        int subscriptionBaseline = UiDiagnosticHub.SubscriptionCount;
        var legacySink = new List<UiOverflowReport>();

        // The zero-finding half of the ruler clause needs the real strings: without a loaded table every
        // Keyed lookup returns its raw key, which is wider than the translation and can overflow a
        // single-line band on its own - a finding this lane would then blame on the ruler.
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));


        try
        {
            // Two hosts with two DIFFERENT rulers. A floods, B is the calibrated stub.
            using UiHost flooded = NewHost(new FloodMetrics());
            using UiHost calibrated = NewHost(new Program.StubMetrics());

            int floodedSession;
            int calibratedSession;
            using (UsTextFitAudit floodedAudit = UsTextFitAudit.Open(flooded, auditFit: true))
            using (UsTextFitAudit calibratedAudit = UsTextFitAudit.Open(calibrated, auditFit: true))
            {
                floodedSession = flooded.Session.Identity;
                calibratedSession = calibrated.Session.Identity;
                Assert(UiFitAudit.Enabled,
                    "opening an audit scope must switch the process-wide detection gate on (it is still the gate)");

                // The legacy process-wide channel must stay unused while subscriptions are live: attach a
                // sink and enable the audit exactly as the old consumer did, then draw both hosts.
                UiFitAudit.Attach(new Program.StubMetrics(), legacySink.Add);
                UiFitAudit.Enabled = true;

                flooded.DrawFrame(new Rect(0f, 0f, Viewport.x, Viewport.y));
                calibrated.DrawFrame(new Rect(0f, 0f, Viewport.x, Viewport.y));
                floodedAudit.Publish();
                calibratedAudit.Publish();

                IReadOnlyList<UiDiagnosticEvent> floodedEvents = flooded.Diagnostics.Snapshot();
                IReadOnlyList<UiDiagnosticEvent> calibratedEvents = calibrated.Diagnostics.Snapshot();
                int floodedFindings = CountFit(floodedEvents);
                int calibratedFindings = CountFit(calibratedEvents);

#if US_FRAME_DEBUG
                foreach (UiDiagnosticEvent dbg in floodedEvents)
                {
                    Console.WriteLine("[dbgE] " + dbg.Kind + " key=" + dbg.Key + " hasRecord=" + dbg.Overflow.HasValue);
                }
                foreach (UiDiagnosticEvent dbg in calibratedEvents)
                {
                    Console.WriteLine("[dbgE] B " + dbg.Kind + " key=" + dbg.Key + " hasRecord=" + dbg.Overflow.HasValue);
                }
#endif
                Assert(floodedFindings > 0,
                    "the flood ruler's host must report fit findings at all, or this lane asserts nothing");

                Assert(calibratedFindings == 0,
                    "host B must not inherit host A's ruler: B reported " + calibratedFindings
                    + " overflow finding(s) while the calibrated stub (the ruler B was created with) reports"
                    + " none on the shipped page [style-fallback records on B: " + StyleFallbacks(calibratedEvents)
                    + "]");
                Assert(legacySink.Count == 0,
                    "no path may consult the process-wide sink while a subscription is live; it received "
                    + legacySink.Count + " finding(s)");

                // Attribution: every event names the host that produced it, and no event names the other.
                foreach (UiDiagnosticEvent found in floodedEvents)
                {
                    Assert(found.Host == flooded.Source && found.SessionId == floodedSession,
                        "an event in A's subscription must be attributed to A: " + found);
                    Assert(found.Overflow.HasValue, "a fit event must carry the audit's own record: " + found);
                }

                foreach (UiDiagnosticEvent found in calibratedEvents)
                {
                    Assert(found.Host == calibrated.Source && found.SessionId == calibratedSession,
                        "an event in B's subscription must be attributed to B: " + found);
                }

                Assert(flooded.Diagnostics.CountOf(UiDiagnosticKind.Fit) == floodedFindings,
                    "the subscription's own count must agree with the snapshot (per-subscription ring)");

                // Ref-count invariants, asserted because every one of their failures is silent. The
                // process-wide switch is the only thing that still couples these windows, so the consumer
                // is what has to keep it correct.
                UiFitAudit.Enabled = true;
                floodedAudit.Dispose();
                Assert(UiFitAudit.Enabled,
                    "closing ONE of two audited windows must not switch detection off for the other "
                    + "(early decrement); the other window would silently stop reporting");
                floodedAudit.Dispose();
                Assert(UiFitAudit.Enabled,
                    "disposing an already-disposed scope must be a no-op; a second decrement is the same "
                    + "early-decrement failure reached through a different call site");
                floodedAudit.Publish();
                calibratedAudit.Publish();
            }

            // X-27, asserted: with both scopes closed the last close turned the shared switch off, so a
            // subscribed host now measures NOTHING even though its subscription is still attached and its
            // ring was just cleared. Routing is per host; the gate is not.
            flooded.Diagnostics.Clear();
            calibrated.Diagnostics.Clear();
            flooded.DrawFrame(new Rect(0f, 0f, Viewport.x, Viewport.y));
            Assert(flooded.Diagnostics.CountOf(UiDiagnosticKind.Fit) == 0,
                "the process-wide detection gate still governs: a subscribed host with the gate off must "
                + "receive nothing (this clause exists so the migration is never reported as 'subscribing "
                + "solves all cross-talk')");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Program.SetTranslatorResolver(null);
        }

        // Lifetime: both hosts are disposed, so neither subscription may outlive them.
        Assert(UiDiagnosticHub.SubscriptionCount == subscriptionBaseline,
            "disposing the hosts must release their subscriptions: " + UiDiagnosticHub.SubscriptionCount
            + " live, baseline " + subscriptionBaseline);
    }

    private static UiHost NewHost(ITextMetrics metrics)
    {
        var fake = new RecordingSettingsSource { RichData = true };
        return UsKernelSettingsHost.Create(fake, metrics);
    }

    /// <summary>
    /// The host's OVERFLOW findings - and the filter is the point, not a convenience. Both of the audit's
    /// channels publish under one kind (<c>fit.overflow</c> and <c>fit.style-fallback</c> both arrive as
    /// <see cref="UiDiagnosticKind.Fit"/>), and only the first carries an
    /// <see cref="UiDiagnosticEvent.Overflow"/> record. A bare kind count therefore conflates "this
    /// host's ruler decided a string does not fit its rect" - what this lane is about - with "some binding
    /// answered a fallback" - a different finding with a different owner and its own lanes. Measured while
    /// S4-3 re-cut this step: the calibrated host publishes exactly one style-fallback record and no
    /// overflow record, so the unfiltered count reported a ruler leak that was not there.
    /// </summary>
    private static int CountFit(IReadOnlyList<UiDiagnosticEvent> events)
    {
        int found = 0;
        foreach (UiDiagnosticEvent candidate in events)
        {
            if (candidate.Kind == UiDiagnosticKind.Fit && candidate.Overflow.HasValue) found++;
        }

        return found;
    }

    /// <summary>Fit-kind records that are NOT overflow findings, counted so the message above stays honest.</summary>
    private static int StyleFallbacks(IReadOnlyList<UiDiagnosticEvent> events)
    {
        int found = 0;
        foreach (UiDiagnosticEvent candidate in events)
        {
            if (candidate.Kind == UiDiagnosticKind.Fit && !candidate.Overflow.HasValue) found++;
        }

        return found;
    }

    /// <summary>
    /// A ruler that makes every string far too big for its rect. Deliberately a ruler and not a corrupted
    /// layout: the layout is measured with it too, which is exactly the point - the host whose ruler this is
    /// reports findings, and the host next to it does not.
    /// </summary>
    private sealed class FloodMetrics : ITextMetrics
    {
        private readonly ITextMetrics inner = new Program.StubMetrics();

        public float MeasureText(string text, UiFont font, float width)
        {
            return inner.MeasureText(text, font, width) * 3f + 120f;
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return inner.MeasureWidth(text, font) * 3f + 120f;
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
            throw new InvalidOperationException("UsAuditRoutingLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
