using System;
using System.Collections.Generic;

namespace UniversalSqueaker;

public partial class UniversalSqueakerSettings
{
    // Add-only Config field. Existing imported player records remain player-owned under their original meaning.
    public string behaviorSourcesXml = "";
    // Independent positive activity. Invalid external/Config input is rejected by the opportunity owner.
    public double activity = 1;

    internal bool TryWriteBehaviorCandidate(string path, string candidate, Action<string> serialize,
        out string failure, Action? beforeCommit = null)
    {
        string previous = behaviorSourcesXml;
        try
        {
            behaviorSourcesXml = candidate;
            return UsAtomicConfigFile.TryWrite(path, serialize, out failure, beforeCommit);
        }
        finally
        {
            behaviorSourcesXml = previous;
        }
    }

    public bool TryChangeBehaviorSource(BehaviorDomain domain, string? sourceId, BehaviorSourceDirectory directory,
        Func<string, bool> persistCandidate, out string failure)
    {
        failure = "";
        if (IsPersistenceBlockedByMigrationFailure)
        {
            failure = "SettingsMigrationBlocked";
            return false;
        }
        try
        {
            var candidate = BehaviorSourceState.FromXml(behaviorSourcesXml);
            if (sourceId == null)
            {
                candidate.Forget(domain);
            }
            else if (!candidate.Bind(domain, sourceId, directory))
            {
                failure = "InvalidBehaviorSource";
                return false;
            }
            return CommitBehaviorState(candidate, persistCandidate, out failure);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ":" + error.Message;
            return false;
        }
    }

    public bool TryRefreshBehaviorSources(BehaviorSourceDirectory directory, Func<string, bool> persistCandidate, out string failure)
    {
        failure = "";
        if (IsPersistenceBlockedByMigrationFailure)
        {
            failure = "SettingsMigrationBlocked";
            return false;
        }
        try
        {
            var candidate = BehaviorSourceState.FromXml(behaviorSourcesXml);
            candidate.Refresh(directory);
            if (candidate.ToXml() == BehaviorSourceState.FromXml(behaviorSourcesXml).ToXml())
            {
                return true;
            }
            return CommitBehaviorState(candidate, persistCandidate, out failure);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ":" + error.Message;
            return false;
        }
    }

    private bool CommitBehaviorState(BehaviorSourceState candidate, Func<string, bool> persistCandidate, out string failure)
    {
        failure = "";
        string xml = candidate.ToXml();
        if (BehaviorSourceState.FromXml(xml).ToXml() != xml)
        {
            throw new InvalidOperationException("Unstable source serialization");
        }
        if (!persistCandidate(xml))
        {
            failure = "BehaviorSourceSaveFailed";
            return false;
        }
        behaviorSourcesXml = xml;
        return true;
    }

    public BehaviorValues ResolveBehaviorSource(BehaviorDomain domain, string action, BehaviorSourceDirectory directory,
        Func<string, bool> implementationAvailable, out bool paused, Action<string>? reportPause = null)
    {
        try
        {
            var players = new List<BehaviorValues>();
            foreach (int layer in new[] { 0, 1, 2 })
            {
                foreach (var record in actionTuning)
                {
                    if (record.actionKey != action || !record.IsValidLayer(out int actual) || actual != layer)
                    {
                        continue;
                    }
                    if (layer > 0 && (domain.Family != "Pawn" || record.raceDefName != domain.Target))
                    {
                        continue;
                    }
                    if (layer == 2 && (domain.Child == "" || record.xenotypeDefName != domain.Child))
                    {
                        continue;
                    }
                    // No conversion of old probability/interval multipliers into chance or hard ticks.
                    var player = new BehaviorValues
                    {
                        Scope = record.hasScope ? record.scope : null,
                        Enabled = record.hasScope ? record.scope != SqueakActionScope.Disabled : null
                    };
                    foreach (var response in record.responses)
                    {
                        if (response.hasEnabled)
                        {
                            player.ResponseEnabled.Add(response.key, response.enabled);
                            if (response.key == "us.core/Age")
                            {
                                player.AgeEnabled = response.enabled;
                            }
                            if (response.key == "us.core/Mood")
                            {
                                player.MoodEnabled = response.enabled;
                            }
                        }
                        if (response.hasParticipation)
                        {
                            player.Participation.Add(response.key, response.apply);
                        }
                    }
                    foreach (var parameter in record.responseParameters)
                    {
                        player.ResponseParameters.Add(parameter.key, parameter.value);
                    }
                    players.Add(player);
                }
            }
            return BehaviorSourceState.FromXml(behaviorSourcesXml).Resolve(domain, action, directory, players, implementationAvailable, out paused, reportPause);
        }
        catch (Exception)
        {
            // Preserve unreadable Config bytes for recovery, and pause only the new behavior path.
            paused = true;
            reportPause?.Invoke("UnreadableBehaviorConfig");
            return new BehaviorValues();
        }
    }
}
