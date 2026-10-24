using UniversalSqueaker;

namespace Example.Worker;

public static class QuietWorkOverride
{
    // false: not committed; prior memory/file remain.
    // true: already committed. A Committed:RuntimePublishFailed diagnostic
    // schedules publication repair; do not retry the disk transaction as a rollback.
    public static bool TryCommit(SqueakerMod mod, out string diagnostic)
        => mod.TryCommitBehaviorOverride("Pawn", "", "Human", "", CoreActions.Work,
            new BehaviorValues { Volume = 0 }, out diagnostic);

    // Ordinary-edit alternative: true means row accepted and saving queued, not disk Committed.
    // Run on the native owner thread; the same 0.35s save owner handles the physical write later.
    public static bool TryQueue(GameSettings settings, out string diagnostic)
    {
        bool replaced = settings.TryReplaceBehaviorOverride("Pawn", "", "Human", "", CoreActions.Work,
            new BehaviorValues { Volume = 0 }, () => { settings.QueuePersistence(); return true; }, out diagnostic);
        if (replaced) settings.ApplyToRuntime();
        return replaced;
    }
}
