using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

public static partial class SqueakDiagnosticsOverlay
{
    private static Pawn? recordingPawn;
    private static readonly List<SqueakResponseEvidence> captureRecords = new();
    private static SqueakResponseEvidence? captureInitial;
    private static SqueakResponseEvidence? captureMaintenance;
    private static long captureId, nextCaptureId, gameGeneration, captureGeneration, observed, overwritten, captureFailed;
    private static string captureSubject = "", captureDisplay = "", captureMap = "", stopReason = "", captureFailure = "", captureLogStatus = "Unavailable";
    private static long captureFieldsTruncated;
    private static double captureStarted, captureStopped;
    private static float captureLastRead;
    private static long captureReadFailures;
    private static string captureReadFailure = "";
    private static string copyStatus = "";
    private static volatile bool gameChangePending;
    internal static bool HasCapture => captureId != 0;
    internal static bool IsRecording(Pawn pawn) => ReferenceEquals(recordingPawn, pawn);
    internal static bool Recording => recordingPawn != null;
    internal static IReadOnlyList<SqueakResponseEvidence> CapturedRecords => captureRecords;
    internal static string CaptureIdentity => HasCapture ? "US.Diagnostics.Capture.Target".Translate(captureDisplay) : "";
    internal static bool CanStartCapture(Pawn? pawn) => sessionActive && !Recording && pawn != null && IsTrackableNow(pawn);

    internal static string CaptureStatus => !HasCapture ? "US.Diagnostics.Capture.Idle".Translate() + (copyStatus == "" ? "" : "\n" + copyStatus)
        : string.Format(CultureInfo.CurrentCulture,
        "US.Diagnostics.Capture.Status".Translate(),
        (Recording ? "US.Diagnostics.Capture.Recording" : HasCapture ? "US.Diagnostics.Capture.Stopped" : "US.Diagnostics.Capture.Idle").Translate(),
        HasCapture ? captureDisplay : "", captureRecords.Count, observed, overwritten, captureFailed)
        + " · " + "US.Diagnostics.Capture.Fields".Translate(captureFieldsTruncated)
        + (stopReason == "" ? "" : " · " + stopReason) + (copyStatus == "" ? "" : "\n" + copyStatus);

    internal static UI.UsDiagBarModel? CaptureBar => !HasCapture ? null : new UI.UsDiagBarModel(
        "US.Diagnostics.Capture.Target".Translate(captureDisplay),
        (Recording ? "US.Diagnostics.Capture.Recording" : "US.Diagnostics.Capture.Stopped").Translate(),
        "", "US.Diagnostics.Capture.Counts".Translate(captureRecords.Count, overwritten), UI.UsDiagDotTone.Pending);

    internal static UI.UsDiagBarModel? CaptureBarFor(string browsing, bool paused)
        => !HasCapture ? null : new UI.UsDiagBarModel(
            browsing.Length == 0 ? "US.Diagnostics.Capture.Target".Translate(captureDisplay)
                : "US.Diagnostics.Viewing".Translate() + " · " + browsing,
            (Recording ? "US.Diagnostics.Capture.Recording" : "US.Diagnostics.Capture.Stopped").Translate().ToString()
                + (paused ? " · " + "US.Diagnostics.Bar.Paused".Translate().ToString() : ""),
            "US.Diagnostics.Capture.Counts".Translate(captureRecords.Count, overwritten),
            "US.Diagnostics.Capture.Target".Translate(captureDisplay), UI.UsDiagDotTone.Pending);

    internal static void StartCapture(Pawn? pawn)
    {
        if (!CanStartCapture(pawn)) return;
        ClearCapture();
        recordingPawn = pawn;
        captureId = ++nextCaptureId; captureGeneration = gameGeneration;
        captureSubject = SqueakerRuntime.SubjectKey(pawn!);
        captureDisplay = captureSubject;
        captureMap = cachedMap!.uniqueID.ToString(CultureInfo.InvariantCulture);
        captureStarted = Time.realtimeSinceStartup;
        try
        {
            string label = pawn!.LabelShort;
            captureDisplay = (label.Length > 256 ? label.Substring(0, 256) : label) + " (" + captureSubject + ")";
            captureInitial = SqueakerRuntime.DiagnosticEvidence(pawn!);
            captureLastRead = Time.realtimeSinceStartup;
            captureLogStatus = pawn!.GetComp<CompSqueaker>().DiagnosticLogStatus;
        }
        catch (Exception error) when (SqueakLog.IsOrdinary(error)) { CaptureFailure(error); }
        model.BumpRevision();
    }

    internal static void StopCapture(string reason = "User")
    {
        if (recordingPawn == null) return;
        Pawn stopped = recordingPawn;
        recordingPawn = null;
        captureStopped = Time.realtimeSinceStartup; stopReason = reason;
        model.BumpRevision();
        try { captureLogStatus = stopped.GetComp<CompSqueaker>()?.DiagnosticLogStatus ?? "Unavailable"; }
        catch (Exception error) when (SqueakLog.IsOrdinary(error))
        {
            captureLogStatus = "Unavailable";
            SqueakLog.DiagnosticsUiFailed("CaptureLogStatus", error, captureSubject);
        }
    }

    internal static void ClearCapture()
    {
        if (Recording) return;
        captureRecords.Clear(); captureInitial = captureMaintenance = null; captureId = 0;
        captureSubject = captureDisplay = captureMap = stopReason = captureFailure = copyStatus = "";
        captureFieldsTruncated = 0;
        captureLogStatus = "Unavailable";
        captureLastRead = 0; captureReadFailures = 0; captureReadFailure = "";
        captureStarted = captureStopped = 0; observed = overwritten = captureFailed = 0;
        model.BumpRevision();
    }

    internal static void RecordSettled(Pawn pawn)
    {
        if (!IsRecording(pawn)) return;
        observed++;
        try
        {
            var evidence = SqueakerRuntime.DiagnosticEvidence(pawn);
            if (captureRecords.Count == 64) { captureRecords.RemoveAt(0); overwritten++; }
            captureRecords.Add(evidence);
            captureFieldsTruncated += evidence.Truncated;
            model.BumpRevision();
        }
        catch (Exception error) when (SqueakLog.IsOrdinary(error)) { CaptureFailure(error); }
    }

    // One replaceable detached maintenance snapshot, not another history ring or a fake decision.
    // Called only after a real review/slot transition/stop, before a released adapter disappears.
    internal static void RecordMaintenance(Pawn pawn, PawnResponseAdapter adapter)
    {
        // Game construction may release old adapters on a LongEvent worker. The pending
        // Game-change path clears that capture on the UI thread; never touch its model here.
        if (!UnityData.IsInMainThread || !IsRecording(pawn)) return;
        try
        {
            captureMaintenance = adapter.CaptureDiagnosticEvidence();
            captureFieldsTruncated += captureMaintenance.Truncated;
            model.BumpRevision();
        }
        catch (Exception error) when (SqueakLog.IsOrdinary(error)) { CaptureFailure(error); }
    }

    private static void CaptureFailure(Exception error)
    {
        captureFailed++; captureFailure = error.GetType().Name;
        model.BumpRevision();
        SqueakLog.DiagnosticsUiFailed("Capture", error, captureSubject);
    }

    internal static void NotifyTargetUnavailable(Pawn pawn)
    {
        if (IsRecording(pawn)) StopCapture("TargetUnavailable");
    }

    /// <summary>Detached saved facts for full reading, including the original capture identity when browsing another Pawn.</summary>
    internal static string TechnicalCaptureText()
    {
        var text = new StringBuilder();
        text.AppendLine(CaptureStatus);
        text.Append("capture_id=").Append(captureId).Append(" game_generation=").Append(HasCapture ? captureGeneration : gameGeneration)
            .Append(" map=").Append(HasCapture ? captureMap : "NotCaptured").AppendLine();
        text.Append("subject=").Append(HasCapture ? captureSubject : "NotCaptured").Append(" stop_reason=").Append(stopReason).AppendLine();
        text.Append("started_realtime_seconds=").Append(HasCapture ? captureStarted.ToString("R", CultureInfo.InvariantCulture) : "NotCaptured")
            .Append(" stopped_realtime_seconds=").Append(HasCapture && !Recording ? captureStopped.ToString("R", CultureInfo.InvariantCulture) : "NotCaptured").AppendLine();
        text.Append("capture_last_success_read_realtime_seconds=").Append(HasCapture && captureLastRead > 0 ? captureLastRead.ToString("R", CultureInfo.InvariantCulture) : "NotCaptured")
            .Append(" read_failures=").Append(captureReadFailures).Append(" read_failure=").Append(captureReadFailure).AppendLine();
        text.Append("fields_truncated_total=").Append(captureFieldsTruncated).Append(" capture_failure=").Append(captureFailure)
            .Append(" log_status=").Append(captureLogStatus).AppendLine();
        text.AppendLine("history_scope=SettledCallbacksForOneSubjectWhileRecording;NotAllGameActions");
        if (captureMaintenance != null) text.AppendLine("\n[LatestMaintenance;NotDecisionHistory]").AppendLine(captureMaintenance.Text());
        for (int i = captureRecords.Count - 1; i >= 0; i--)
            text.Append("\n[Captured ").Append(captureRecords[i].Sequence).AppendLine("]").AppendLine(captureRecords[i].Text());
        if (captureInitial != null) text.AppendLine("\n[PreRecording]").AppendLine(captureInitial.Text());
        return text.ToString();
    }

    private static void RecordReadStatus(CachedPawn entry)
    {
        if (!IsRecording(entry.Pawn)) return;
        captureLastRead = entry.LastSuccessRealtime;
        captureReadFailure = entry.ReadFailure;
        captureReadFailures = entry.ReadFailures;
    }

    // Game construction/load can precede the UI thread boundary. Root owns window teardown.
    internal static void NotifyGameChanged() => gameChangePending = true;

    private static void ApplyGameChange()
    {
        if (!gameChangePending) return;
        gameChangePending = false;
        StopCapture("GameReplaced"); CloseSession(); ClearCapture(); gameGeneration++;
    }

    internal static void CopyFeedback(Pawn? displayed)
    {
        try
        {
            // Read cached / settled data only. In particular, no snapshot refresh is requested here.
            SqueakResponseEvidence? current = displayed != null && TryGetEntry(displayed, out var entry) ? entry.Snapshot.Evidence : null;
            var text = new StringBuilder("usdiag-feedback/1\n");
            int headerTruncated = 0;
            bool viewReadFailed = false;
            void Header(string key, object? value)
            {
                string scalar = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "Unavailable";
                if (scalar.Length > 256) { scalar = scalar.Substring(0, 256); headerTruncated++; text.Append("truncated_header=").Append(key).Append('\n'); }
                text.Append(key).Append('=').Append(scalar).Append('\n');
            }
            void Identity(string key, Assembly assembly)
            {
                Header(key + ".informational", SqueakResponseEvidence.Safe(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion));
                Header(key + ".assembly", assembly.GetName().Version);
                Header(key + ".mvid", assembly.ManifestModule.ModuleVersionId);
                Header(key + ".configuration", SqueakResponseEvidence.Safe(assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration));
            }
            Identity("us", typeof(SqueakerMod).Assembly); Identity("fl_loaded", typeof(UiHost).Assembly);
            Header("game_version", "Unavailable"); Header("game_assembly", typeof(Pawn).Assembly.GetName().Version);
            Header("biotech", ModsConfig.BiotechActive); Header("fl_compiler_pair", "NotComparedByFeedback");
            Header("capture_id", captureId); Header("game_generation", HasCapture ? captureGeneration : gameGeneration);
            Header("map", HasCapture ? captureMap : "NotCaptured"); Header("subject", HasCapture ? captureSubject : displayed == null ? "NotCaptured" : SqueakerRuntime.SubjectKey(displayed));
            Header("state", Recording ? "Recording" : HasCapture ? "Stopped" : "NotRecording"); Header("stop_reason", stopReason);
            Header("started_realtime_seconds", HasCapture ? captureStarted : (double?)null);
            Header("stopped_realtime_seconds", HasCapture && !Recording ? captureStopped : (double?)null);
            Header("copied_realtime_seconds", Time.realtimeSinceStartup);
            Header("observed", observed); Header("retained", captureRecords.Count); Header("overwritten", overwritten);
            Header("first_retained_sequence", captureRecords.Count == 0 ? (long?)null : captureRecords[0].Sequence);
            Header("last_retained_sequence", captureRecords.Count == 0 ? (long?)null : captureRecords[captureRecords.Count - 1].Sequence);
            Header("fields_truncated_total", captureFieldsTruncated);
            Header("capture_failed", captureFailed); Header("last_capture_failure_type", captureFailure);
            Header("capture_last_success_read_realtime_seconds", captureLastRead);
            Header("capture_read_failures", captureReadFailures); Header("capture_read_failure_type", captureReadFailure);
            Header("history_scope", "SettledCallbacksForOneSubjectWhileRecording;NotAllGameActions");
            Header("maintenance_scope", "LatestMainThreadReviewSlotTransitionOrStopWhileRecording;NotDecisionHistory");
            Header("logging_mode", SqueakLog.Mode); Header("logging_effective", SqueakLog.ShouldEmitDev);
            Header("log_status", recordingPawn?.GetComp<CompSqueaker>()?.DiagnosticLogStatus ?? captureLogStatus);
            if (displayed != null && TryGetEntry(displayed, out var cached))
            {
                viewReadFailed = cached.ReadFailure != "";
                Header("view_subject", SqueakerRuntime.SubjectKey(displayed)); Header("view_has_snapshot", cached.HasSnapshot);
                Header("view_last_success_realtime_seconds", cached.HasSnapshot ? cached.LastSuccessRealtime : (float?)null);
                Header("view_read_failures", cached.ReadFailures); Header("view_last_failure_type", cached.ReadFailure);
            }
            Header("redaction", "Allowlist;NoNamesPathsRawLogsConfigurationOrExceptionText");
            int omitted = 0;
            void Block(string heading, SqueakResponseEvidence? evidence)
            {
                string block = "\n[" + heading + "]\n" + (evidence?.Text() ?? "stage=NotCaptured") + "\n";
                if (Encoding.UTF8.GetByteCount(text.ToString()) + Encoding.UTF8.GetByteCount(block) > 64 * 1024 - 1024) { omitted++; return; }
                text.Append(block);
            }
            if (HasCapture)
            {
                bool sameTarget = displayed != null && SqueakerRuntime.SubjectKey(displayed) == captureSubject;
                Header("current_target_snapshot", sameTarget && current != null ? "CachedViewCurrent" : "NotCaptured");
                if (sameTarget) Block("CachedViewCurrent", current);
                Block("LatestMaintenance;NotDecisionHistory", captureMaintenance);
                for (int i = captureRecords.Count - 1; i >= 0; i--) Block("latest " + captureRecords[i].Sequence, captureRecords[i]);
                Block("PreRecording", captureInitial);
            }
            else Block("CachedCurrent", current);
            Header("report_blocks_omitted", omitted);
            Header("header_fields_truncated", headerTruncated);
            Header("completeness", overwritten != 0 || captureFailed != 0 || omitted != 0 || headerTruncated != 0
                || captureFieldsTruncated != 0 || captureReadFailure != "" || viewReadFailed ? "Incomplete" : "BoundedScopeOnly;SeeFieldsTruncatedAndNotCaptured");
            SqueakDiagnosticsClipboard.Write(text.ToString());
            copyStatus = "US.Diagnostics.Capture.Copied".Translate();
        }
        catch (Exception error) when (SqueakLog.IsOrdinary(error))
        {
            copyStatus = "US.Diagnostics.Capture.CopyFailed".Translate(error.GetType().Name);
            SqueakLog.DiagnosticsUiFailed("FeedbackCopy", error);
        }
        model.BumpRevision();
    }
}
