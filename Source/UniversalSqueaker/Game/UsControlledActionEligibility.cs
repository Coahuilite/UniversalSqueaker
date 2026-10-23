namespace UniversalSqueaker;

/// <summary>Current folded options only. Definition admission is owned by the catalog, not a Comp table.</summary>
internal static class UsControlledActionEligibility
{
    internal static bool Allows(bool enabled, BehaviorScope scope, bool activeCommand, bool playerControlled)
    {
        if (!enabled || scope == BehaviorScope.Disabled)
        {
            return false;
        }
        return scope != BehaviorScope.ActiveCommand || activeCommand && playerControlled;
    }
}
