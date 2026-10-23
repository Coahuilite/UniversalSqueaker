namespace UniversalSqueaker;

// Official 0.6 package inventory; no platform references. Third-party contracts use independent keys.


/// <summary>The official complete IDs. Strings remain ordinal identities, never enum slots or short tails.</summary>
public static class CoreActions
{
    public const string Call = "us.core/Call", Eat = "us.core/Eat", Sleep = "us.core/Sleep",
        Wounded = "us.core/Wounded", Select = "us.core/Select", Move = "us.core/Move",
        Social = "us.core/Social", Joy = "us.core/Joy", Death = "us.core/Death",
        Draft = "us.core/Draft", Undraft = "us.core/Undraft", Attack = "us.core/Attack",
        Work = "us.core/Work", Equip = "us.core/Equip", MentalBreak = "us.core/MentalBreak";
    public static System.Collections.Generic.IReadOnlyList<string> All { get; } = System.Array.AsReadOnly(new[]
    { Call, Eat, Sleep, Wounded, Select, Move, Social, Joy, Death, Draft, Undraft, Attack, Work, Equip, MentalBreak });
}
