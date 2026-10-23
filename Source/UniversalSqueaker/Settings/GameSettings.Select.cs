using System;
using System.Linq;

namespace UniversalSqueaker;

public partial class GameSettings
{
    public BehaviorValues ReadSelectParameters(string race = "", string child = "")
        => ReadParameters(CoreActions.Select, SelectPolicy.OfficialDefaults, race, child);

    public BehaviorValues ReadSelectParameterOverrides(string race = "", string child = "")
    {
        var values = new BehaviorValues();
        foreach (var row in actionTuning.Where(x => x.serviceFamily == "Pawn" && x.raceDefName == race
            && x.xenotypeDefName == child && x.actionKey == "us.core/Select"))
            values.Overlay(ProjectBehaviorPlayerValues(row));
        return values;
    }

    internal void SetSelectParameter(string key, bool? value, string race = "", string child = "")
    {
        if (child != "" && race == "") throw new ArgumentException("Child requires parent");
        var values = ReadSelectParameterOverrides(race, child);
        switch (key)
        {
            case "playerControlled": values.SelectRequirePlayerControlled = value; break;
            case "awake": values.SelectRequireAwake = value; break;
            case "notDowned": values.SelectRequireNotDowned = value; break;
            default: throw new ArgumentException("Unknown Select qualification", nameof(key));
        }
        if (!TryChangeBehaviorOverride("Pawn", "", race, child, "us.core/Select", values,
            () => { QueuePersistence(); return true; }, out string failure)) throw new InvalidOperationException(failure);
        PublishBehaviorConfiguration();
    }
}
