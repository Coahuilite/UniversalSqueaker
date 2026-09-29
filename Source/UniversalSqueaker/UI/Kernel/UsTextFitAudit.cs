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
/// <para>
/// <b>The geometry instrument is a SEPARATE, explicit developer control (R3-B).</b> It used to be switched on
/// as a side effect of opening this scope, which made "detailed logging" mean "start capturing every rect" -
/// the coupling the R3-B contract forbids. Nothing here enables it on its own now: a caller turns it on per
/// host (<see cref="SetGeometryCapture"/>), toggles the outline (<see cref="SetGeometryOverlay"/>) and asks
/// for one report (<see cref="RequestGeometryReport"/>), and the report is produced on the next pass and then
/// cleared. The refusal path is a STATUS (<see cref="DevGeometryStatus"/>) rather than an exception or an
/// empty report: a development build on a release carrier is a legitimate state, and the one loud line still
/// comes from <c>SqueakLog.LayoutTrace</c>.
/// </para>
/// </remarks>
public sealed class UsTextFitAudit : IDisposable
{
    /// <summary>What the development geometry instrument is doing for one host (R3-B's developer status).</summary>
    public enum DevGeometryStatus
    {
        /// <summary>No audit scope is open for this host, so the developer controls have nothing to drive.</summary>
        ScopeMissing,

        /// <summary>The instrument is off. The default.</summary>
        Off,

        /// <summary>The instrument is capturing the arranged pass.</summary>
        Active,

        /// <summary>Capturing AND painting the outline around each sampled draw rect.</summary>
        Overlay,

        /// <summary>The carrier payload has no instrument (a release carrier). Enabling is refused.</summary>
        Unavailable
    }

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

    /// <summary>
    /// The open scopes, by host, so a UI command that names a host can reach that window's OWN scope (R3-B:
    /// the geometry controls are per host - enabling the settings window's instrument must not touch the
    /// diagnostics panel's). Bounded by the number of open US windows and always released in
    /// <see cref="Dispose"/>, which is idempotent, so an entry cannot outlive its window.
    /// </summary>
    private static readonly List<KeyValuePair<UiHost, UsTextFitAudit>> openScopes = new();

    private readonly UiDiagnosticSubscription subscription;
    private long loggedThrough;
    private bool disposed;

    /// <summary>
    /// Whether this scope also runs the text-fit audit, which is the caller's logging policy restated (R3-B
    /// fix 4). It gates the process-wide detection switch and the ring drain, NOT the geometry commands.
    /// </summary>
    private bool auditsFit;

    /// <summary>The host this scope audits. Held so a per-host command can find this scope and no other.</summary>
    private readonly UiHost host;

    /// <summary>
    /// A report asked for and not yet produced. Set by <see cref="RequestGeometryReport"/> and cleared by the
    /// call that produces it, so one request is one report.
    /// <para>
    /// Deliberately NOT inside the <c>US_DEV</c> block, and neither is <see cref="geometryEnabled"/>: the
    /// developer controls and their state exist in BOTH configurations so the page always has something
    /// truthful to say, and so the Release build compiles without pretending these members are absent. Only
    /// the carrier CALLS (<c>GeometryEnabled</c> / <c>GeometryOverlay</c> / <c>DumpGeometry</c> /
    /// <c>TryGetGeometrySnapshot</c>) are conditional, because only they are refused on a release payload.
    /// </para>
    /// </summary>
    private bool reportPending;

    /// <summary>
    /// True while this host's sampling is on. It exists in both builds because it describes a REQUEST the
    /// consumer made, not the carrier's ability to honour it: an explicit enable is the only writer, and the
    /// status reader never writes it (R3-B fix 3 - reading status must not start capture).
    /// </summary>
    private bool geometryEnabled;

    /// <summary>
    /// Whether the carrier payload has the instrument, answered once per scope. Cached because the answer is a
    /// property of the loaded DLL, not of a frame. The cached answer never turns sampling on: the caller that
    /// wants sampling does that itself.
    /// </summary>
#if US_DEV
    private bool instrumentAvailable;

    /// <summary>True once <see cref="InstrumentAvailable"/> has asked the carrier, so it asks only once.</summary>
    private bool instrumentProbed;
#endif

    /// <summary>
    /// The one-shot report the last explicit emission produced: the structured snapshot AND the matching text
    /// dump, read from ONE capture (R3-B fix 5 - never a second read that could land on a later pass). Null
    /// until a report has been produced. This is what the consumer's diagnostics surface exposes.
    /// </summary>
    private UiDevGeometrySnapshot? lastReportSnapshot = null;

    private string lastReportDump = "";

#if US_DEV
    /// <summary>
    /// The pass this request is waiting on. Latched on the FIRST consume call, so one request cannot be
    /// answered by a capture it waited for and then a newer one as well.
    /// <para>
    /// US_DEV ONLY. In a release build no pass can ever be reported (the carrier has no capture), so this
    /// state would be written and never read - the assigned-but-unread warning PM measured. Putting it where
    /// it is actually used is the fix; suppressing the warning would have hidden a member that had no business
    /// existing in that build.
    /// </para>
    /// </summary>
    private int reportRequestPass = -1;
#endif

    // Explicit char[] selects the overload present on net472 (TrimEnd(char) is unavailable there).
    private static readonly char[] LineEndings = { '\r' };

    /// <summary>One refusal line per process: a dev build on a release carrier is a legitimate state.</summary>
    private static bool geometryRefused;

    private UsTextFitAudit(UiHost host, UiDiagnosticSubscription subscription)
    {
        this.host = host;
        this.subscription = subscription;
    }

    /// <summary>
    /// Opens the audit over one host's own subscription. The subscription is created on first access and
    /// released by the host/session, so this scope never disposes it: closing a window must not silence a
    /// sibling window, and the shell already owns the host's lifetime.
    /// <para>
    /// <b>The scope and the fit-audit switch are two different things (R3-B fix 4).</b> A window opens this
    /// scope in every logging mode, because the developer geometry commands need a scope to target; the
    /// process-wide fit detection switch follows the CALLER's logging policy through
    /// <paramref name="auditFit"/>. With <paramref name="auditFit"/> false the scope is a pure geometry
    /// handle: nothing is measured, <see cref="UiFitAudit.Enabled"/> is not touched, and a sibling window that
    /// IS auditing keeps its own hold. That is how geometry became independent of detailed logging without
    /// changing the fit/recovery policy at all.
    /// </para>
    /// </summary>
    /// <param name="auditFit">True when this window also wants the text-fit audit, which is exactly
    /// <c>SqueakLog.ShouldEmitDev</c> at both US call sites - the policy is unchanged.</param>
    public static UsTextFitAudit Open(UiHost host, bool auditFit)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        UiDiagnosticSubscription subscription = host.Diagnostics;

        var scope = new UsTextFitAudit(host, subscription);
        scope.auditsFit = auditFit;
        if (auditFit)
        {
            // The ring is drained only by an auditing window, so only an auditing window clears it: a
            // geometry-only scope that cleared the ring would throw away a sibling's unread findings.
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
        }

        openScopes.Add(new KeyValuePair<UiHost, UsTextFitAudit>(host, scope));
        // NOTE (R3-B): the geometry instrument is deliberately NOT enabled here. It is a separate developer
        // decision, made per host through SetGeometryCapture, so detailed logging no longer implies rect
        // capture. See the type remarks.
        return scope;
    }

    /// <summary>
    /// Writes out the findings this host published since the last call. Called once per pass (the ring is
    /// bounded, so a window that only drained at close could lose findings) and once more on dispose.
    /// <para>
    /// <b>No geometry here (R3-B fix 6).</b> The explicit report is the geometry emission path now: a
    /// business press must not spend a whole dump on a developer instrument merely because capture is on. The
    /// fit/recovery half is untouched.
    /// </para>
    /// </summary>
    public void Publish()
    {
        if (disposed || !subscription.IsActive) return;

        if (!auditsFit)
        {
            // A geometry-only scope does not audit text, and must not consume the ring another window reads.
            return;
        }

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

    /// <summary>
    /// Final drain, then releases this window's hold on the process-wide detection switch.
    /// <para>
    /// <b>Coherent in BOTH builds (R3-B fix 7).</b> The scope registry is populated in both configurations, so
    /// its removal is not conditional: a release build must not leave a closed window findable by a developer
    /// command. Only the carrier call that switches sampling off is conditional.
    /// </para>
    /// </summary>
    public void Dispose()
    {
        if (disposed) return;
        Publish();
        disposed = true;

        DisableGeometry();
        // This window's entry goes with it, so a later per-host command cannot find a closed window's scope
        // and drive an instrument nobody is drawing.
        for (int i = openScopes.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(openScopes[i].Value, this)) openScopes.RemoveAt(i);
        }

        if (auditsFit)
        {
            auditsFit = false;
            openWindows--;
            if (openWindows <= 0)
            {
                openWindows = 0;
                UiFitAudit.Enabled = false;
            }
        }
    }

    /// <summary>
    /// What the development geometry instrument is doing for <paramref name="host"/> right now (R3-B's
    /// developer status). Safe to call on every kind of payload: a release carrier refuses ENABLING and the
    /// probe below answers <see cref="DevGeometryStatus.Unavailable"/> rather than throwing or reporting an
    /// empty capture as success.
    /// <para>
    /// <b>NON-MUTATING (R3-B fix 3).</b> Reading the status must not start sampling: this method only READS
    /// the consumer's own request flag and the cached capability answer. Only
    /// <see cref="SetGeometryCapture"/> writes <c>geometryEnabled</c>, because only an explicit enable may
    /// turn the instrument on. Before this fix the read ran the enable probe, so the readout said Off while
    /// sampling had already started.
    /// </para>
    /// <para>
    /// This method is compiled in BOTH configurations on purpose: the whole point of the status is that a
    /// shipped build can say "the tool is not here".
    /// </para>
    /// </summary>
    public static DevGeometryStatus GetDevGeometryStatus(UiHost? host)
    {
        UsTextFitAudit? scope = FindScope(host);
        if (scope == null) return DevGeometryStatus.ScopeMissing;
        if (!scope.geometryEnabled) return scope.InstrumentAvailable()
            ? DevGeometryStatus.Off
            : DevGeometryStatus.Unavailable;
        return scope.GeometryOverlayOn() ? DevGeometryStatus.Overlay : DevGeometryStatus.Active;
    }

    /// <summary>
    /// Turns THIS host's instrument on or off, explicitly and per host (R3-B). Returns whether the request
    /// was honoured: <c>true</c> on a development carrier, and <c>false</c> when the payload has no
    /// instrument, in which case the caller shows the developer-feature-unavailable status instead of a
    /// "clean" report. The refusal is reported once per process on the existing <c>ltrace</c> channel.
    /// <para>
    /// Nothing enables this implicitly: detailed logging no longer implies rect capture, and neither does
    /// reading the status. The default is off.
    /// </para>
    /// </summary>
    public static bool SetGeometryCapture(UiHost? host, bool enabled)
    {
        UsTextFitAudit? scope = FindScope(host);
        if (scope == null) return false;
        if (!enabled)
        {
            scope.DisableGeometry();
            return true;
        }

        if (!scope.InstrumentAvailable())
        {
            // Refused, or the payload cannot hold the instrument: say it once, leave no half-state, and do
            // not let a request ride on a later successful enable (fix 7).
            scope.DisableGeometry();
            return false;
        }

#if US_DEV
        scope.subscription.GeometryEnabled = true;
#endif
        scope.geometryEnabled = true;
        return true;
    }

    /// <summary>
    /// Turns the outline painter on or off for THIS host. It requires the capture, so the carrier refuses it
    /// while the instrument is off; that is reported as "not honoured" rather than thrown at the caller.
    /// </summary>
    public static bool SetGeometryOverlay(UiHost? host, bool enabled)
    {
        UsTextFitAudit? scope = FindScope(host);
        if (scope == null) return false;
        if (!scope.geometryEnabled || !scope.InstrumentAvailable()) return false;

#if US_DEV
        try
        {
            scope.subscription.GeometryOverlay = enabled;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            ReportGeometryUnavailable(ex);
            return false;
        }
#else
        return false;
#endif
    }

    /// <summary>
    /// Asks for ONE geometry report (R3-B). The request is consumed by <see cref="PublishGeometryReport"/> on
    /// the next completed pass, which is a real draw pass the caller owns - never a frame invented here - so
    /// one request produces one report. A request made while capture is off or unavailable is refused and
    /// cleared instead of being left pending for a later enable to satisfy (fix 7).
    /// </summary>
    public static bool RequestGeometryReport(UiHost? host)
    {
        UsTextFitAudit? scope = FindScope(host);
        if (scope == null) return false;
        if (!scope.geometryEnabled || !scope.InstrumentAvailable())
        {
            // Refuse now, clear anything stale, and keep the honest status: the source shows "unavailable",
            // not a request that silently fires after some later enable.
            scope.reportPending = false;
#if US_DEV
            scope.reportRequestPass = -1;
#endif
            return false;
        }

        scope.reportPending = true;
#if US_DEV
        scope.reportRequestPass = -1;
#endif
        return true;
    }

    /// <summary>
    /// Whether a report has been asked for and not yet produced. Read by a lane to assert the deferral (the
    /// request survives the click and is cleared by the pass that reports), and by a host that wants to show
    /// "waiting" rather than pretending nothing was requested.
    /// </summary>
    public bool IsGeometryReportPending => reportPending;

    /// <summary>
    /// The pass the last explicit report described, or -1 when none has been produced. This is the PUBLIC join
    /// between the report's two renderings: both are retained from ONE capture (see
    /// <see cref="EmitGeometryReport"/>), so a pass number that matches the text dump's own line is what makes
    /// "same capture, two renderings" checkable from outside.
    /// </summary>
    public int LastGeometryReportPass => lastReportSnapshot?.Pass ?? -1;

    /// <summary>How many arranged elements the last explicit report's STRUCTURED rendering carries.</summary>
    public int LastGeometryReportNodeCount => lastReportSnapshot?.Nodes.Count ?? 0;

    /// <summary>How many sampled hit queries the last explicit report's structured rendering carries.</summary>
    public int LastGeometryReportInputCount => lastReportSnapshot?.Inputs.Count ?? 0;

    /// <summary>True when the last explicit report has BOTH renderings retained from the same capture.</summary>
    public bool HasGeometryReport => lastReportSnapshot != null && lastReportDump.Length > 0;

    /// <summary>
    /// The structured rendering of the last explicit report, retained from the SAME capture as
    /// <see cref="LatestGeometryReportText"/> (R3-B fix 5).
    /// <para>
    /// <b>internal as a conservatism, not because the carrier surface is unreviewed.</b>
    /// <c>UiDevGeometrySnapshot</c> IS a reviewed carrier type: it is classified <c>public-unstable</c> in
    /// <c>ferritelib/docs/api-tiers.md</c>, so naming it in a public US signature would be legitimate. US keeps
    /// it internal because the useful facts are republished against US's OWN documented surface
    /// (<see cref="LastGeometryReportPass"/>, <see cref="LastGeometryReportNodeCount"/>,
    /// <see cref="LastGeometryReportInputCount"/>, <see cref="LatestGeometryReportText"/> plus the settings
    /// source's own snapshot accessor), and a public US signature over a public-unstable carrier type would add
    /// a second surface to keep in step for no consumer gain yet. Revisit if a consumer needs the graph.
    /// </para>
    /// </summary>
    internal UiDevGeometrySnapshot? LatestGeometryReport => lastReportSnapshot;

    /// <summary>
    /// The text rendering of the last explicit report. The STRUCTURED half is deliberately not returned from
    /// here for the reason above: the useful facts are republished against US's own documented surface, and US
    /// does not add a second public surface over a public-unstable carrier type without a consumer asking. This
    /// text plus <see cref="LastGeometryReportPass"/> and the two counts are the public evidence that both
    /// renderings came from the same pass.
    /// </summary>
    public string LatestGeometryReportText => lastReportDump;

    /// <summary>
    /// The one-shot report half, called once per pass by the window that owns this scope. It reads the SAME
    /// capture the consumer's snapshot accessor reads - one <c>TryGetGeometrySnapshot</c> plus one
    /// <c>DumpGeometry</c> from the same pass - and writes the text to the existing <c>ltrace</c> channel;
    /// there is no second collector and no export service.
    /// <para>
    /// <b>The lifecycle, stated because it is the contract.</b> request -&gt; the first pass AFTER the request
    /// writes the report and clears the request -&gt; a second call writes nothing. A request made while the
    /// instrument is off (or on a carrier without one) is refused at request time (see
    /// <see cref="RequestGeometryReport"/>), so nothing is left pending for a later enable to satisfy.
    /// </para>
    /// </summary>
    /// <returns>The pass the report described, or -1 when this call wrote nothing (no request, or the pass the
    /// request is waiting on has not completed yet). The return value is what makes "one report per request"
    /// assertable without reading the log.</returns>
    public static int PublishGeometryReport(UiHost? host)
    {
        UsTextFitAudit? scope = FindScope(host);
        if (scope == null || scope.disposed || !scope.reportPending) return -1;

        return scope.ConsumeGeometryReport(scope.CurrentCapturePass());
    }

    /// <summary>
    /// The consumer half. A release payload has no capture and no pass to wait for, so the request is retired
    /// with the unavailable sentence; that is the same answer the source shows, just once.
    /// </summary>
    private int ConsumeGeometryReport(int pendingPass)
    {
#if !US_DEV
        reportPending = false;
        ReportGeometryUnavailable(null);
        return -1;
#else
        if (!geometryEnabled || !InstrumentAvailable())
        {
            reportPending = false;
            reportRequestPass = -1;
            ReportGeometryUnavailable(null);
            return -1;
        }

        // A capture that existed BEFORE the request is not the answer: latch the pass the request is waiting
        // on, and report the first capture that is NEWER than it.
        if (reportRequestPass < 0)
        {
            if (pendingPass < 0) return -1;
            reportRequestPass = pendingPass;
        }

        if (pendingPass <= reportRequestPass) return -1;

        reportPending = false;
        reportRequestPass = -1;
        return EmitGeometryReport() ? pendingPass : -1;
#endif
    }

#if US_DEV
    /// <summary>
    /// The explicit report's emission: ONE capture, TWO renderings (R3-B fix 5). The snapshot and the text
    /// dump are read together from the same completed pass and both are retained, so a consumer can inspect
    /// the structured half and the human half of the same frame. A second read of either would be free to
    /// land on a later pass, which is exactly what B1 forbids.
    /// </summary>
    private bool EmitGeometryReport()
    {
        if (!subscription.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? snapshot) || snapshot == null)
        {
            ReportGeometryUnavailable(null);
            return false;
        }

        string dump = subscription.DumpGeometry();
        if (dump.Length == 0)
        {
            // Instrumented but this pass produced no capture: say so rather than publishing an empty report.
            ReportGeometryUnavailable(null);
            return false;
        }

        lastReportSnapshot = snapshot;
        lastReportDump = dump;
        WriteDump(dump);
        return true;
    }
#endif

    /// <summary>
    /// The pass number of the most recent capture, or -1 when there is none. Compiled in both builds because
    /// <see cref="PublishGeometryReport"/> asks for it before it knows whether the carrier can answer: on a
    /// release payload it is simply always -1, since <c>TryGetGeometrySnapshot</c> is documented to answer
    /// false rather than throw there.
    /// </summary>
    private int CurrentCapturePass()
    {
        return subscription.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? snapshot) && snapshot != null
            ? snapshot.Pass
            : -1;
    }

    /// <summary>The scope auditing <paramref name="host"/>, or null. Host identity, not a window type.</summary>
    private static UsTextFitAudit? FindScope(UiHost? host)
    {
        if (host == null) return null;
        for (int i = 0; i < openScopes.Count; i++)
        {
            if (ReferenceEquals(openScopes[i].Key, host)) return openScopes[i].Value;
        }

        return null;
    }

    /// <summary>
    /// The open scope auditing <paramref name="host"/>, for the same-assembly consumer that owns the window.
    /// internal for the API-tier reason on <see cref="LatestGeometryReport"/>: the structured report is
    /// reachable through US's own surface, but not through a PUBLIC one that would name an unlisted carrier
    /// type.
    /// </summary>
    internal static UsTextFitAudit? FindOpenScope(UiHost? host)
    {
        return FindScope(host);
    }

    /// <summary>
    /// Whether the carrier payload has the instrument, answered once per scope and CACHED (R3-B fix 3). The
    /// answer is a property of the loaded DLL, not of a frame.
    /// <para>
    /// <b>Reading is not enabling.</b> Unlike the probe this replaced, this method never turns sampling on:
    /// it asks the carrier for the capability and records the answer. Only <see cref="SetGeometryCapture"/>
    /// writes the request flag, so "Off" can never be reported while sampling is already running.
    /// </para>
    /// <para>
    /// The refusal is reported once per process, and the cached answer is what makes re-enable correct: a
    /// later explicit enable reads the cache instead of re-probing, so it succeeds on a Dev carrier even
    /// after a status read already asked the question.
    /// </para>
    /// </summary>
    private bool InstrumentAvailable()
    {
#if US_DEV
        if (instrumentProbed) return instrumentAvailable;
        instrumentProbed = true;
        try
        {
            // The carrier's own refusal is the probe: the getter answers false on a release payload, so only
            // the SETTER's documented throw distinguishes "off" from "cannot".
            subscription.GeometryEnabled = true;
            subscription.GeometryEnabled = false;
            instrumentAvailable = true;
        }
        catch (InvalidOperationException ex)
        {
            instrumentAvailable = false;
            ReportGeometryUnavailable(ex);
        }

        return instrumentAvailable;
#else
        ReportGeometryUnavailable(null);
        return false;
#endif
    }

    /// <summary>Whether THIS host is painting the captured-rect outline, read without mutating anything.</summary>
    private bool GeometryOverlayOn()
    {
#if US_DEV
        return geometryEnabled && subscription.GeometryOverlay;
#else
        return false;
#endif
    }

    /// <summary>Turns the instrument back off; only the scope that turned it on does this.</summary>
    private void DisableGeometry()
    {
        geometryEnabled = false;
        reportPending = false;
#if US_DEV
        reportRequestPass = -1;
        try
        {
            subscription.GeometryEnabled = false;
        }
        catch (InvalidOperationException)
        {
            // The payload lost the instrument under us (a release carrier was swapped in). Nothing to undo.
        }
#endif
    }

    /// <summary>
    /// One refusal line per process, carrying the carrier's own reason when there is one. The developer needs
    /// to know that the tool is missing rather than that the layout was clean - that distinction is the whole
    /// of R3-B's B3.
    /// </summary>
    private static void ReportGeometryUnavailable(InvalidOperationException? refusal)
    {
        if (geometryRefused) return;
        geometryRefused = true;
        SqueakLog.LayoutTrace(refusal == null
            ? "geometry unavailable: this payload has no layout instrument (a release library package)"
            : "geometry unavailable: " + refusal.Message);
    }

    /// <summary>Writes one capture to the existing layout-trace channel, one line per record.</summary>
    private static void WriteDump(string dump)
    {
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
