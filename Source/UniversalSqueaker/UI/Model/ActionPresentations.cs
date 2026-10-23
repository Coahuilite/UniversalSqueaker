using System;
namespace UniversalSqueaker;

/// <summary>UI labels only. Scope capabilities and defaults belong to the canonical contracts.</summary>
public readonly struct ActionPresentation
{
    public readonly string Key, DisplayKey;
    internal ActionPresentation(string key, string label)
    { Key = key; DisplayKey = label; }
}

public static class ActionPresentations
{
    private static readonly ActionPresentation[] definitions =
    {
        new(CoreActions.Call, "US.Action.Call"),
        new(CoreActions.Eat, "US.Action.Eat"),
        new(CoreActions.Sleep, "US.Action.Sleep"),
        new(CoreActions.Wounded, "US.Action.Wounded"),
        new(CoreActions.Select, "US.Action.Select"),
        new(CoreActions.Move, "US.Action.Move"),
        new(CoreActions.Social, "US.Action.Social"),
        new(CoreActions.Joy, "US.Action.Joy"),
        new(CoreActions.Death, "US.Action.Death"),
        new(CoreActions.Draft, "US.Action.Draft"),
        new(CoreActions.Undraft, "US.Action.Undraft"),
        new(CoreActions.Attack, "US.Action.Attack"),
        new(CoreActions.Work, "US.Action.Work"),
        new(CoreActions.Equip, "US.Action.Equip"),
        new(CoreActions.MentalBreak, "US.Action.MentalBreak")
    };
    public static int Count => definitions.Length;
    public static bool IsKnown(string key) => Kernel.BuiltInActionKeys.Contains(key);
    public static ActionPresentation Get(string key)
    {
        foreach (var entry in definitions) if (entry.Key == key) return entry;
        throw new ArgumentException("NoOfficialPresentation:" + key, nameof(key));
    }
}
