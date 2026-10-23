namespace UniversalSqueaker;

/// <summary>Optional Pawn qualifications, independent of the fixed accepted-feedback contract.</summary>
public static class SelectPolicy
{
    public const string PlayerControlled = "us.core/PlayerControlled";
    public const string Awake = "us.core/Awake";
    public const string NotDowned = "us.core/NotDowned";
    public static BehaviorValues OfficialDefaults => new() {
        SelectRequirePlayerControlled = true, SelectRequireAwake = true, SelectRequireNotDowned = true };
    public static bool HasParameters(BehaviorValues value) => value.SelectRequirePlayerControlled.HasValue
        || value.SelectRequireAwake.HasValue || value.SelectRequireNotDowned.HasValue;
}
