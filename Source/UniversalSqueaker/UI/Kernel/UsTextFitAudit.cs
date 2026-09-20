using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// One window's text-fit audit scope, over that window's OWN host subscription
/// (<see cref="UiHost.Diagnostics"/>) instead of the process-wide legacy channel.
///
/// Layout can promise a rect but cannot promise that a translated string fits it, and no build-time
/// harness can know: real glyph advances only exist inside the game's font engine. This turns that
/// unknown into evidence. While the window is open and detailed logging is effective, every label its
/// host draws is measured against the rect it was given, and each distinct failure is written as one
/// <c>usdiag evt=ui.text.overflow</c> record carrying the element path, the failing axis, the font and
/// the need/have pixel pair.
///
/// The overflowing text itself is deliberately not logged: labels embed third-party pack and Def names,
/// and the element path already identifies the exact code site.
/// </summary>
/// <remarks>
/// <para>
/// <b>What moving to the per-host subscription buys, and what it does not.</b> The gain is <b>routing</b>
/// and <b>ruler isolation</b>: a finding is delivered to the subscription of the host whose scope is
/// open, and it is measured with THAT host's own <see cref="ITextMetrics"/> - so two visible windows no
/// longer share one sink (which silently replaced one window's record with another's) or one ruler (which
/// let one window's text adapter decide another window's overflow verdict). It is <b>not</b> "subscribing
/// removes every interference": <see cref="UiFitAudit.Enabled"/> is still one process-wide switch that
/// gates whether anything is measured at all, and <see cref="UiFitAudit.Detach"/>'s global teardown still
/// governs the legacy path. This class therefore owns that switch with a reference count - the switch is
/// global, so the consumer is the layer that has to keep it correct - and the boundary is asserted by
/// <c>UsAuditRoutingLaneTests</c> rather than described here.
/// </para>
/// <para>
/// <b>The channel is buffered, so the consumer drains it.</b> The legacy sink was push-based; a
/// subscription is a bounded ring, so this scope writes out the events it has not written yet once per
/// pass (and once more when the window closes). The ring's own dedup table is what keeps a repeating
/// finding to one line, exactly as the process-wide dedup set did, and the per-pass drain is what keeps a
/// frame's findings from being pushed out of the default 32-event ring before they are read.
/// </para>
/// <para>
/// <b>Policy stays with the window.</b> Whether to audit at all is the caller's dev-logging decision; this
/// type is only the mechanism, which is also what makes it lane-testable without the game.
/// </para>
/// </remarks>
public sealed class UsTextFitAudit : IDisposable
{
    /// <summary>
    /// Open audited windows, so the one process-wide detection switch follows "any window is diagnosing".
    /// <para>
    /// <b>The three invariants this counter must hold</b> (it is the easiest thing in this file to get
    /// wrong, and each failure is silent):
    /// <list type="number">
    /// <item>the count reaches EXACTLY zero when the last window closes, and the switch goes off there and
    /// only there - a leak leaves measurement on for the rest of the process, paying the audit's cost for a
    /// window that is gone;</item>
    /// <item>closing one of two open windows must NOT switch detection off - that is the "early decrement"
    /// failure, and its symptom is the other window silently reporting nothing;</item>
    /// <item><see cref="Dispose"/> is IDEMPOTENT, because the call sites are a window's <c>PreClose</c> plus
    /// the <c>using</c> scope a lane or a future caller may hold - a second dispose that decremented again
    /// would turn the switch off under the other window.</item>
    /// </list>
    /// Invariant 3 is enforced by <see cref="disposed"/>; 1 and 2 are asserted by
    /// <c>UsAuditRoutingLaneTests</c>, which closes one of two scopes and then disposes the same scope twice
    /// before closing the last one.
    /// </para>
    /// </summary>
    private static int openWindows;

    private readonly UiDiagnosticSubscription subscription;
    private long loggedThrough;
    private bool disposed;

    private UsTextFitAudit(UiDiagnosticSubscription subscription)
    {
        this.subscription = subscription;
    }

    /// <summary>
    /// Opens the audit over one host's own subscription. The subscription is created on first access and
    /// released by the host/session, so this scope never disposes it: closing a window must not silence a
    /// sibling window, and the shell already owns the host's lifetime.
    /// </summary>
    public static UsTextFitAudit Open(UiHost host)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        UiDiagnosticSubscription subscription = host.Diagnostics;
        subscription.Clear();

        // The detection switch is process-wide (UiFitAudit.Check returns immediately while it is false).
        // Reference-counting it is what keeps "any window still open is still diagnosed" true when the
        // first of two windows closes - the failure a single bool would reintroduce the moment the
        // diagnostics panel and the settings window are open together.
        if (openWindows == 0)
        {
            UiFitAudit.Enabled = true;
        }

        openWindows++;
        return new UsTextFitAudit(subscription);
    }

    /// <summary>
    /// Writes out the findings this host published since the last call. Called once per pass (the ring is
    /// bounded, so a window that only drained at close could lose findings) and once more on dispose.
    /// </summary>
    public void Publish()
    {
        if (disposed || !subscription.IsActive) return;

        IReadOnlyList<UiDiagnosticEvent> events = subscription.Snapshot();
        for (int i = 0; i < events.Count; i++)
        {
            UiDiagnosticEvent found = events[i];
            if (found.Sequence <= loggedThrough) continue;
            loggedThrough = found.Sequence;
            if (found.Kind == UiDiagnosticKind.Fit && found.Overflow.HasValue)
            {
                Report(found.Overflow.Value);
            }
        }
    }

    /// <summary>Final drain, then releases this window's hold on the process-wide detection switch.</summary>
    public void Dispose()
    {
        if (disposed) return;
        Publish();
        disposed = true;

        openWindows--;
        if (openWindows <= 0)
        {
            openWindows = 0;
            UiFitAudit.Enabled = false;
        }
    }

    private static void Report(UiOverflowReport report)
    {
        SqueakLog.LabelOverflow(
            report.ElementPath,
            report.Axis == UiOverflowAxis.Width ? "width" : "height",
            FontName(report.Font),
            report.Needed,
            report.Available);
    }

    private static string FontName(UiFont font)
    {
        return font switch
        {
            UiFont.Tiny => "tiny",
            UiFont.Medium => "medium",
            _ => "small"
        };
    }
}
