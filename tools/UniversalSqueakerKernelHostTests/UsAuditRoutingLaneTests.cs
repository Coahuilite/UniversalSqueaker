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
        Console.WriteLine("UsAuditRoutingLaneTests ALL PASS");
        return 0;
    }

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
            using (UsTextFitAudit floodedAudit = UsTextFitAudit.Open(flooded))
            using (UsTextFitAudit calibratedAudit = UsTextFitAudit.Open(calibrated))
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