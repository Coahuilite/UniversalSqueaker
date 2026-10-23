using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker;

public partial class GameSettings
{
    /// <summary>Replace only response fields of one exact player action/domain. Other fields and source intent remain unchanged.</summary>
    public void ReplaceResponseTuning(string actionKey, string? raceDefName, string? xenotypeDefName,
        IEnumerable<ResponseTuningValue> responses, IEnumerable<ResponseParameterValue> parameters)
    {
        string race = raceDefName ?? "";
        string xeno = xenotypeDefName ?? "";
        if (!Authoring.AuthorXml.ActionId(actionKey)
            || race != race.Trim() || xeno != xeno.Trim() || !IsValidTuningDomain(race, xeno))
        {
            throw new ArgumentException("Invalid exact response tuning identity");
        }
        var nextResponses = responses.Select(x => x.Copy()).ToList();
        var nextParameters = parameters.Select(x => x.Copy()).ToList();
        if (nextResponses.GroupBy(x => x.key, StringComparer.Ordinal).Any(x => x.Count() != 1)
            || nextParameters.GroupBy(x => x.key, StringComparer.Ordinal).Any(x => x.Count() != 1)
            || nextResponses.Any(x => string.IsNullOrWhiteSpace(x.key) || x.key != x.key.Trim() || !x.hasEnabled && !x.hasParticipation)
            || nextParameters.Any(x => string.IsNullOrWhiteSpace(x.key) || x.key != x.key.Trim()
                || double.IsNaN(x.value) || double.IsInfinity(x.value)))
        {
            throw new ArgumentException("Invalid response tuning fields");
        }
        var matching = actionTuning.Where(x => SameActionTuningIdentity(x, actionKey, race, xeno)).ToArray();
        var target = matching.LastOrDefault();
        foreach (var row in matching)
        {
            row.responses.Clear();
            row.responseParameters.Clear();
        }
        if (nextResponses.Count > 0 || nextParameters.Count > 0)
        {
            if (target == null)
            {
                target = new ActionTuningRecord { actionKey = actionKey, raceDefName = race, xenotypeDefName = xeno };
                actionTuning.Add(target);
            }
            target.responses = nextResponses;
            target.responseParameters = nextParameters;
        }
        actionTuning.RemoveAll(x => SameActionTuningIdentity(x, actionKey, race, xeno) && !CarriesAnyActionTuningField(x));
        PublishBehaviorConfiguration();
        QueuePersistence();
    }
}
