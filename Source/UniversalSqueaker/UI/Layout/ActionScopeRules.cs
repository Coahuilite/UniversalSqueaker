using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Pure classification rules for the built-in action scope editor.
///
/// Actions are split into two UI groups:
/// - <see cref="Autonomous"/>: background/self-initiated behaviors.
/// - <see cref="Operable"/>: player-commandable actions (default <see cref="SqueakActionScope.ActiveCommand"/>
///   or actions that support <see cref="SqueakActionScope.ActiveCommand"/>).
///
/// Baby actions share the runtime eligibility rule; the default editor excludes them.
/// </summary>
public enum ActionScopeGroup
{
    Autonomous,
    Operable
}

public static class ActionScopeRules
{
    public static ActionScopeGroup GroupFor(SqueakActionDefinition definition)
    {
        bool supportsCommand = (definition.SupportedScopes & SqueakActionScopeSupport.ActiveCommand) != 0;
        bool defaultsToCommand = definition.DefaultScope == SqueakActionScope.ActiveCommand;
        return supportsCommand || defaultsToCommand ? ActionScopeGroup.Operable : ActionScopeGroup.Autonomous;
    }

    public static bool IsHiddenByDefault(SqueakAction action) => !SqueakActionEligibility.IsEligible(action, false);
}
