using System;
using System.Runtime.CompilerServices;
using System.Text;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>Checked frames must report actual widget recovery, not only a surviving Host.
/// FL 0.8 closes runtime tables: this helper uses bounded public node observations and
/// IsTripped/TryGetTripLog, retaining observed recoveries for the test session. It fails on
/// a truncated observation rather than treating an incomplete scan as clean. Deliberate
/// recovery cases opt out explicitly and retain their positive-control assertions.</summary>
internal static class UsTripGuard
{
    /// <summary>
    /// Fails the calling lane when the session remembers a tripped element. A trip is remembered for the
    /// whole session, so this covers every checked frame the lane has drawn so far.
    /// <paramref name="deliberateTrips"/> is the explicit switch for lanes that test recovery itself.
    /// <paramref name="caller"/> names the lane in the failure text, so no call site has to spell it.
    /// </summary>
    internal static void ExpectNoTrips(
        UiSession session,
        string? lane = null,
        bool deliberateTrips = false,
        [CallerMemberName] string caller = "")
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (deliberateTrips) return;
        if (Fl08PublicObservation.Recoveries(session).Length == 0) return;

        throw new Exception(Describe(session, lane ?? caller));
    }

    /// <summary>
    /// True when the session remembers at least one tripped element. The positive-control surface: a lane
    /// asserts this BEFORE asserting that <see cref="ExpectNoTrips"/> reports it, so the guard is never
    /// both the thing under test and the thing measuring.
    /// </summary>
    internal static bool AnyTripped(UiSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        return Fl08PublicObservation.Recoveries(session).Length > 0;
    }

    /// <summary>The failure text: which lane, which elements, what each one threw, and what it means.</summary>
    internal static string Describe(UiSession session, string lane)
    {
        var text = new StringBuilder();
        text.Append(lane).Append(": ").Append(Fl08PublicObservation.Recoveries(session).Length)
            .Append(" element(s) in this session were replaced by a recovery band, so the page was not fully drawn. ")
            .Append("A trip means the element was replaced by its recovery band; in a harness that usually "
                + "means the runtime stub is missing a game member the product called, so the product path "
                + "under test never ran and this lane's assertions are not about the page. The trip is "
                + "remembered until the session is disposed, so this says the lane never had a silent "
                + "recovery. If the trip is deliberate, pass deliberateTrips: true - and keep passing it for "
                + "the rest of the lane.");

        foreach (string diagnostic in Fl08PublicObservation.Recoveries(session))
            text.Append(Environment.NewLine).Append("  ").Append(diagnostic);
        return text.ToString();
    }

    /// <summary>
    /// One checked frame: draw it, then assert the page actually drew. This is the shape every lane that
    /// drives a real frame should use, because the failure it catches is silent by construction.
    /// </summary>
    internal static void DrawChecked(
        this UiHost host,
        Rect viewport,
        bool deliberateTrips = false,
        [CallerMemberName] string caller = "")
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        Fl08PublicObservation.Draw(host, viewport);
        ExpectNoTrips(host.Session, caller, deliberateTrips);
    }
}
