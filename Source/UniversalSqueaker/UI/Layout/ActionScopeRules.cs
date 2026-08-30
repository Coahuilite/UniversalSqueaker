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
/// Biotech defensive baby actions (Crying/Giggling) are hidden from the editor by default; they still
/// remain runtime playable through the existing patches, this is purely a UI exposure policy.
/// </summary>
public enum ActionScopeGroup
{
    Autonomous,
    Operable
}

public static class ActionScopeRules
{
    /// <summary>
    /// Set to true by maintainers who want Biotech defensive baby actions to reappear in the
    /// Action Scope editor. Default false keeps the UI focused on gameplay-relevant behaviors.
    /// </summary>
    public const bool ShowBiotechDefensiveActions = false;

    public static ActionScopeGroup GroupFor(SqueakActionDefinition definition)
    {
        bool supportsCommand = (definition.SupportedScopes & SqueakActionScopeSupport.ActiveCommand) != 0;
        bool defaultsToCommand = definition.DefaultScope == SqueakActionScope.ActiveCommand;
        return supportsCommand || defaultsToCommand ? ActionScopeGroup.Operable : ActionScopeGroup.Autonomous;
    }

    public static bool IsHiddenByDefault(SqueakAction action)
    {
        return !ShowBiotechDefensiveActions
            && (action == SqueakAction.Crying || action == SqueakAction.Giggling);
    }
}
