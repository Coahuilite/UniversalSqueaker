using System.Threading;

namespace UniversalSqueaker;

/// <summary>
/// The definition-ready PUBLISHER of the browse directory (OVT-001): holds exactly one immutable
/// plain snapshot, published once from Mod's ExecuteWhenFinished entry after the defs are loaded
/// (the same lifecycle the sound catalog already uses: Volatile.Read/Write over an immutable
/// snapshot). Deliberately VERSE-FREE - it only holds the plain UsDefinitionSnapshot - so any
/// consumer (the UI projection, the harness lanes) reads and even publishes plain data without
/// loading a single game type; the Verse reading lives in VerseDefinitionCollector alone.
///
/// Window close/reopen reuses this snapshot; no GUI pass re-reflects or re-scans. The snapshot is
/// a BROWSING fact set: it is never merged into SqueakXenotypeCatalog.RaceDefNames, never feeds
/// VoicePackCompAttach.Apply, and grants no sound. Empty before publication is a legal state
/// (early window open): the board then shows the honest zero directory, never a crash.
/// </summary>
public static class RaceDefinitionCatalog
{
    private static UsDefinitionSnapshot current = UsDefinitionSnapshot.Empty;

    /// <summary>The published snapshot; never null.</summary>
    public static UsDefinitionSnapshot Current => Volatile.Read(ref current);

    /// <summary>Atomic publication (the collector's result, or a harness fixture's plain data).</summary>
    public static void Publish(UsDefinitionSnapshot snapshot)
    {
        Volatile.Write(ref current, snapshot ?? UsDefinitionSnapshot.Empty);
    }

    /// <summary>The startup entry Mod calls once after defs are ready: collect (the only Verse read)
    /// and publish. Kept separate from CollectAndPublish's caller so the Verse type is touched from
    /// exactly one lifecycle point.</summary>
    public static void CollectAndPublish()
    {
        Publish(VerseDefinitionCollector.Collect());
    }
}
