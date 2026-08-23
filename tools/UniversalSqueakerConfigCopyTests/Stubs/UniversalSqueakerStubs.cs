using System;
using System.Collections.Generic;

namespace UniversalSqueaker;

/// <summary>Characterization stub: the store reads the permanent packageId from the mod class.</summary>
public class UniversalSqueakerMod
{
    public const string PackageId = "coahuilite.universalsqueaker";
}

/// <summary>
/// Characterization stub: the store's only SqueakLog touchpoint is FallbackProfileStoreFailed
/// (failure diagnostics on the corrupt/rebuild path). The harness captures those calls to assert
/// that corrupt copies log and recover instead of throwing.
/// </summary>
public static class SqueakLog
{
    public static readonly List<string> StoreFailures = new();

    public static void FallbackProfileStoreFailed(string race, Exception ex)
    {
        StoreFailures.Add(race);
    }
}
