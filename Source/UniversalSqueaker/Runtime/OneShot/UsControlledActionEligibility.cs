namespace UniversalSqueaker;

/// <summary>New author wrappers do not require a second complete legacy Comp table.</summary>
internal static class UsControlledActionEligibility
{
    internal static bool Allows(bool legacyConfigured, bool authorAction, bool permission,
        bool enabled, SqueakActionScope scope, bool activeCommand, bool playerControlled)
    {
        if ((!legacyConfigured && !authorAction) || !permission || !enabled || scope == SqueakActionScope.Disabled)
        {
            return false;
        }
        return scope != SqueakActionScope.ActiveCommand || activeCommand && playerControlled;
    }
}
