using System;
using System.Runtime.CompilerServices;
using System.Text;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The rule a page-drawing lane owes its own coverage claim: a lane that drew a page must not have recorded
/// a tripped element. "The frame survived" is not "the element drew".
/// <para>
/// Why this is a guard rather than a convention (measured 2026-09-12). US's diagnostics detail called
/// <c>rect.ContractedBy(8f)</c>, a member of the game's <c>Verse.GenUI</c> the harness's Verse stub did
/// not declare, so the call threw <c>TypeLoadException</c> at JIT time inside the harness only. The
/// session guard did exactly its job - that element became a recovery band and the frame continued - and
/// every US lane asserted no more than <c>Session.IsActive</c>, so the panel's gate chain never drew a
/// single row in the harness while the lanes stayed green. In the game the same code draws, which is why
/// nothing looked wrong until the drawn text was actually read back.
/// </para>
/// <para>
/// So a lane that drives a real frame asserts this: <see cref="DrawChecked"/> draws and then checks, and
/// a lane that trips on purpose passes <c>deliberateTrips: true</c> so the choice is visible in the call
/// instead of being an omission.
/// </para>
/// <para>
/// The trip set is session-scoped, not frame-scoped: <c>UiSession.TrippedNodes</c> is cleared only when the
/// session is disposed (measured 2026-09-12 - a lane that plants a trip, clears the source and draws a clean
/// frame still reports the old trip). The claim this guard makes is therefore "this lane never had a silent
/// recovery", which is strictly stronger, and a lane that plants a trip keeps passing
/// <c>deliberateTrips: true</c> for the rest of that lane.
/// </para>
/// </summary>
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
        if (session.TrippedNodes.Count == 0) return;

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
        return session.TrippedNodes.Count > 0;
    }

    /// <summary>The failure text: which lane, which elements, what each one threw, and what it means.</summary>
    internal static string Describe(UiSession session, string lane)
    {
        var text = new StringBuilder();
        text.Append(lane).Append(": ").Append(session.TrippedNodes.Count)
            .Append(" element(s) in this session were replaced by a recovery band, so the page was not fully drawn. ")
            .Append("A trip means the element was replaced by its recovery band; in a harness that usually "
                + "means the runtime stub is missing a game member the product called, so the product path "
                + "under test never ran and this lane's assertions are not about the page. The trip is "
                + "remembered until the session is disposed, so this says the lane never had a silent "
                + "recovery. If the trip is deliberate, pass deliberateTrips: true - and keep passing it for "
                + "the rest of the lane.");

        foreach (UiNode node in session.TrippedNodes)
        {
            text.Append(Environment.NewLine).Append("  ").Append(node.Path);
            if (session.TryGetTripLog(node, out string logged))
            {
                text.Append(" :: ").Append(logged);
            }
        }

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
        host.DrawFrame(viewport);
        ExpectNoTrips(host.Session, caller, deliberateTrips);
    }
}
