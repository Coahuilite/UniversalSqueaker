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
#if US_DEV
    // Explicit char[] selects the overload present on net472 (TrimEnd(char) is unavailable there).
    private static readonly char[] LineEndings = { '\r' };
    /// <summary>The last sampled pass printed; equal clicks in different passes remain separate evidence.</summary>
    private string lastGeometryHeader = "";

    /// <summary>True once this scope turned the geometry instrument on, and therefore the one that turns it off.</summary>
    private bool geometryEnabled;

    /// <summary>One refusal line per process: a dev build on a release carrier is a legitimate state.</summary>
    private static bool geometryRefused;
#endif

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
        var scope = new UsTextFitAudit(subscription);
#if US_DEV
        scope.EnableGeometry();
#endif
        return scope;
    }

    /// <summary>
    /// Writes out the findings this host published since the last call. Called once per pass (the ring is
    /// bounded, so a window that only drained at close could lose findings) and once more on dispose.
    /// </summary>
    public void Publish()
    {
        if (disposed || !subscription.IsActive) return;

#if US_DEV
        PublishGeometry();
#endif

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

#if US_DEV
        DisableGeometry();
#endif

        openWindows--;
        if (openWindows <= 0)
        {
            openWindows = 0;
            UiFitAudit.Enabled = false;
        }
    }

#if US_DEV
    /// <summary>
    /// Opts this window's host subscription in to the carrier's development-only geometry instrument
    /// (<c>UiDiagnosticSubscription.GeometryEnabled</c>): every node's arranged / draw / window rect, its
    /// height mode and resolved height, plus - for every press-shaped hit query - the pointer, the queried
    /// rect and the funnel's own verdict (disabled / covered / hit / miss).
    /// <para>
    /// <b>One place.</b> This scope is the only US code that touches the instrument: it is the same
    /// per-window diagnostic scope the text-fit audit already uses, so nothing is instrumented inside a
    /// widget and there is no second switch to keep in sync. The developer switch is the existing one - the
    /// audit only opens while detailed logging is effective (<c>SqueakLog.ShouldEmitDev</c>), which the
    /// Diagnostics workspace already toggles in game.
    /// </para>
    /// <para>
    /// <b>Fail closed, and say so once.</b> The instrument is compiled into a development payload and out of
    /// a release one, and the setter THROWS on the latter rather than answering with silence. A US
    /// development build on a release carrier is a legitimate state - it is what the shared sibling path
    /// holds most of the time - so the refusal is reported once per process on the out-of-protocol
    /// <c>ltrace</c> channel and the window keeps drawing.
    /// </para>
    /// <para>
    /// <b>To exercise it, explicitly select a Dev carrier with FerriteLibArtifactPath.</b>
    /// The default compatibility input can remain Release. See docs/build-and-debug.md.
    /// </para>
    /// </summary>
    private void EnableGeometry()
    {
        try
        {
            subscription.GeometryEnabled = true;
            geometryEnabled = true;
        }
        catch (InvalidOperationException ex)
        {
            if (geometryRefused) return;
            geometryRefused = true;
            SqueakLog.LayoutTrace("geometry unavailable: " + ex.Message);
        }
    }

    /// <summary>Turns the instrument back off; only the scope that turned it on does this.</summary>
    private void DisableGeometry()
    {
        if (!geometryEnabled) return;
        geometryEnabled = false;
        try
        {
            subscription.GeometryEnabled = false;
        }
        catch (InvalidOperationException)
        {
            // The payload lost the instrument under us (a release carrier was swapped in). Nothing to undo.
        }
    }

    /// <summary>
    /// Prints the full instrument dump once per sampled pass. Identical clicks in later passes are
    /// separate evidence; repeated Publish calls in the same pass do not duplicate the block.
    /// </summary>
    private void PublishGeometry()
    {
        if (!geometryEnabled) return;

        string dump = subscription.DumpGeometry();
        if (dump.Length == 0) return;

        string presses = PressLines(dump);
        int headerEnd = dump.IndexOf('\n');
        string header = headerEnd < 0 ? dump : dump.Substring(0, headerEnd);
        if (presses.Length == 0 || string.Equals(header, lastGeometryHeader, StringComparison.Ordinal)) return;
        lastGeometryHeader = header;

        int start = 0;
        while (start < dump.Length)
        {
            int end = dump.IndexOf('\n', start);
            if (end < 0) end = dump.Length;
            string line = dump.Substring(start, end - start).TrimEnd(LineEndings);
            if (line.Length > 0) SqueakLog.LayoutTrace("geometry " + line);
            start = end + 1;
        }
    }

    /// <summary>The dump's own hit-query lines: the half that only a press produces.</summary>
    private static string PressLines(string dump)
    {
        var text = new System.Text.StringBuilder();
        int start = 0;
        while (start < dump.Length)
        {
            int end = dump.IndexOf('\n', start);
            if (end < 0) end = dump.Length;
            string line = dump.Substring(start, end - start).TrimEnd(LineEndings);
            if (line.StartsWith("input ", StringComparison.Ordinal)) text.Append(line).Append('\n');
            start = end + 1;
        }

        return text.ToString();
    }
#endif

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
