namespace UniversalSqueaker;

public enum BehaviorScope { Disabled, AnyOccurrence, ActiveCommand }

[System.Flags]
public enum ActionScopeSupport { None = 0, AnyOccurrence = 1, ActiveCommand = 2 }
