using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using Verse;

namespace Example.Worker;

// Call Register during definition initialization, before response subjects become active.
// This single C# declaration replaces the old unregistered-string notification prerequisite.
public static class FinishedNotification
{
    public static readonly NotificationContract Contract = new("example.worker/Finished", intervalTicks: 120);
    public static void Register() => SqueakerNotifications.Declare(Contract);
    // True means input processed, even when qualification/audio/output prevents playback.
    public static bool TryProcessWorkFinished(Pawn actualWorker) => SqueakerNotifications.TryProcess(actualWorker, Contract);
}
