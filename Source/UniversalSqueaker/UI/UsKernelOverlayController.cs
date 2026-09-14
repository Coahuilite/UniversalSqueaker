using System;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Lifecycle controller for the camera-indicator overlay Host (second UiKit host). It owns at most
/// one <see cref="UiHost"/> at a time: created lazily on the first enabled frame, disposed as soon
/// as the toggle is off, the map goes away, the controller is disposed, or a draw fails. Each
/// show/hide round-trip gets a fresh Host/session. It is deliberately NOT a generic HUD manager —
/// its only surface is the camera readout row at the date-bar rect.
///
/// Creation/draw failures trip a permanent fallback flag for this game session: the legacy
/// pure-Verse patch takes over and the kernel Host is disposed, so the two paths are never drawn
/// in parallel (the single draw dispatcher in
/// <see cref="Patch_GlobalControlsUtility_CameraIndicator"/> guarantees mutual exclusion).
/// </summary>
public sealed class UsKernelOverlayController : IDisposable
{
    // 26, not the settings row height: this row is drawn into the vanilla global-controls column, next to
    // the game's own controls, and it has to keep the rhythm they are drawn at - a floating HUD row that
    // followed the settings window's density axis would change height when a settings document changes,
    // which is neither its surface nor its axis. The patch that inserts the row uses the same number; if
    // the two ever drift, the overlay sits a pixel off the vanilla stack it is part of.
    private const float RowHeight = 26f;

    private readonly IUsKernelOverlaySource source;
    private UiHost? host;
    private bool creationFailed;
    private bool disposed;

    public UsKernelOverlayController(IUsKernelOverlaySource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>True when the kernel overlay Host exists and its session is active.</summary>
    public bool IsActive => host != null && host.Session.IsActive;

    /// <summary>True once creation or draw failed; the legacy fallback takes over for the session.</summary>
    public bool CreationFailed => creationFailed;

    /// <summary>
    /// Per-frame gate: disposes the Host when the indicator is off, the map is gone, or the
    /// controller was disposed; creates the Host lazily on the first enabled frame. Safe to call
    /// from a layout-period postfix (Root.Update) and from the draw dispatcher.
    /// </summary>
    public void Maintain()
    {
        if (disposed || !source.IndicatorEnabled || !source.MapAvailable)
        {
            Teardown();
            return;
        }

        if (host == null && !creationFailed)
        {
            EnsureHost();
        }
    }

    /// <summary>
    /// Draws one overlay frame at the date-bar rect supplied by the caller (the position already
    /// proven by the legacy patch; no new offsets are invented). Returns true only when the kernel
    /// overlay drew; false when disabled, map-gone, a Layout event, or a failure tripped the
    /// permanent fallback. On a draw failure the Host is disposed and never retried this session.
    /// </summary>
    public bool TryDraw(float leftX, float width, ref float curBaseY)
    {
        Maintain();
        if (!IsActive) return false;
        if (UiNative.IsLayoutEvent()) return false;

        try
        {
            // Only commit the row cursor when the kernel overlay actually drew. If DrawFrame throws
            // and the caller falls back to the legacy pure-Verse readout, curBaseY must remain
            // untouched so the fallback does not double-reserve the row height.
            float rowY = curBaseY - RowHeight;
            host!.DrawFrame(new Rect(leftX, rowY, width, RowHeight));
            curBaseY = rowY;
            return true;
        }
        catch (Exception ex)
        {
            creationFailed = true;
            Teardown();
            Log.Warning(
                "[UniversalSqueaker] Camera indicator kernel overlay draw failed; legacy camera indicator will be used: "
                + SqueakLogText.SanitizeExceptionMessage(ex.Message));
            return false;
        }
    }

    private void EnsureHost()
    {
        try
        {
            host = UsKernelOverlayHost.Create(source);
        }
        catch (Exception ex)
        {
            creationFailed = true;
            Log.Warning(
                "[UniversalSqueaker] Camera indicator kernel overlay creation failed; legacy camera indicator will be used: "
                + SqueakLogText.SanitizeExceptionMessage(ex.Message));
        }
    }

    private void Teardown()
    {
        host?.Dispose();
        host = null;
    }

    public void Dispose()
    {
        disposed = true;
        Teardown();
    }
}
