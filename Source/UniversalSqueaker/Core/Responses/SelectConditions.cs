using System.Linq;

namespace UniversalSqueaker.Responses;

public static class SelectConditions
{
    public static bool UsesSelection(Condition condition) => condition.RequiredFacts().Contains("us.core/SelectEvent");
    public static Condition Restriction(BehaviorValues value) => new("all", "", new[] {
        value.SelectRequirePlayerControlled == true ? new Condition("ref", SelectPolicy.PlayerControlled) : new Condition("all"),
        value.SelectRequireAwake == true ? new Condition("ref", SelectPolicy.Awake) : new Condition("all"),
        value.SelectRequireNotDowned == true ? new Condition("ref", SelectPolicy.NotDowned) : new Condition("all") });
}
