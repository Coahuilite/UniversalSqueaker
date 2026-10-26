using System.Diagnostics;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker;

/// <summary>Main-thread root serials survive owner/map resets; timestamps are sampled at acceptance.</summary>
internal static class UsInteractionObservation
{
    private static long serial;
    internal static long NewRoot() => checked(++serial);
    internal static InteractionOpportunity Accepted(string fact, long root)
        => new(fact, root, (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
}
