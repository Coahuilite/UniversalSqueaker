using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UniversalSqueaker;
using Verse;

namespace UniversalSqueaker.SettingsMigrationTests;

internal static partial class Program
{
    private static void BehaviorSourceConfigTransaction()
    {
        Scenario("S3-source-config-transaction-and-recovery");
        var settings = NewSettings();
        settings.actionTuning.Add(new ActionTuningRecord { actionKey = "us.core/Work", raceDefName = "Ratkin", sourcePresetDefName = "ImportedPlayer", hasProbabilityMultiplier = true, probabilityMultiplier = 0 });
        settings.globalCooldownMultiplier = .42f;
        settings.scaleCooldownWithTimeSpeed = false;
        var domain = new BehaviorDomain("Pawn", "", "Ratkin");
        var neighbor = new BehaviorDomain("Pawn", "", "Kiiro");
        var a = new BehaviorSource { Id = "author.a/Def", Domain = domain, Contributions = new List<BehaviorContribution>
            { new() { Action = "us.core/Work", Values = new BehaviorValues { Enabled = false, TalkingEnabled = false, HardIntervalTicks = 0, Volume = 0 }, RequiredFacts = new[] { "missing/Implementation" } } } };
        var b = a.Copy(); b.Id = "author.b/Def"; b.Contributions[0].Values.Enabled = true;
        var n = a.Copy(); n.Id = "author.n/Def"; n.Domain = neighbor;
        var directory = new BehaviorSourceDirectory(new[] { a, b, n });
        string folder = Path.Combine(Path.GetTempPath(), "us-s3-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "settings.xml");
        void Serialize(string staging) => SafeSaver.Save(staging, "SettingsBlock", () =>
        {
            var value = settings;
            Scribe_Deep.Look(ref value, "ModSettings");
        });
        bool Persist(string candidate)
        {
            bool saved = settings.TryWriteBehaviorCandidate(path, candidate, Serialize, out string failure);
            if (!saved) Console.Error.WriteLine("S3 persist failure: " + failure);
            return saved;
        }
        UniversalSqueakerSettings Read()
        {
            UniversalSqueakerSettings result = null!;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref result, "ModSettings");
            Scribe.loader.FinalizeLoading();
            return result;
        }
        try
        {
            Check(settings.TryChangeBehaviorSource(domain, a.Id, directory, Persist, out string bindFailure), "S3 actual settings operation adopts A and commits isolated Config: " + bindFailure, ref failures);
            Check(settings.TryChangeBehaviorSource(neighbor, n.Id, directory, Persist, out _), "S3 neighbor binding persists independently", ref failures);
            var reloaded = Read();
            Check(reloaded.behaviorSourcesXml == settings.behaviorSourcesXml && reloaded.actionTuning.Single().sourcePresetDefName == "ImportedPlayer"
                && reloaded.actionTuning.Single().probabilityMultiplier == 0 && reloaded.globalCooldownMultiplier == .42f && !reloaded.scaleCooldownWithTimeSpeed,
                "S3 same Scribe SettingsBlock/ModSettings reload preserves sources AND old player provenance/zero/clock/master", ref failures);
            string beforeMemory = settings.behaviorSourcesXml;
            string beforeFile = File.ReadAllText(path);
            bool FailedSerialize(string candidate) => settings.TryWriteBehaviorCandidate(path, candidate, stage =>
            {
                File.WriteAllText(stage, "partial new candidate");
                throw new IOException("injected serializer failure");
            }, out _);
            Check(!settings.TryChangeBehaviorSource(domain, b.Id, directory, FailedSerialize, out _)
                && settings.behaviorSourcesXml == beforeMemory && File.ReadAllText(path) == beforeFile,
                "S3 serializer truncation/failure cannot alter original memory OR original file", ref failures);
            bool FailedReplace(string candidate) => settings.TryWriteBehaviorCandidate(path, candidate, Serialize, out _, () => throw new IOException("injected before atomic replacement"));
            Check(!settings.TryChangeBehaviorSource(domain, b.Id, directory, FailedReplace, out _)
                && settings.behaviorSourcesXml == beforeMemory && File.ReadAllText(path) == beforeFile,
                "S3 failure after complete Scribe stage but before commit preserves original memory/file", ref failures);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Check(!settings.TryChangeBehaviorSource(domain, b.Id, directory, Persist, out _) && settings.behaviorSourcesXml == beforeMemory,
                    "S3 actual filesystem replace denial preserves original memory", ref failures);
            }
            Check(File.ReadAllText(path) == beforeFile, "S3 actual replace denial preserves original file", ref failures);
            var missing = new BehaviorSourceDirectory(new[] { b, n });
            Check(settings.TryRefreshBehaviorSources(missing, Persist, out _), "S3 missing A keeps binding while B exists", ref failures);
            var resolved = settings.ResolveBehaviorSource(domain, "us.core/Work", missing, _ => true, out bool paused);
            Check(resolved.Enabled == false && resolved.TalkingEnabled == false && resolved.HardIntervalTicks == 0 && resolved.Volume == 0 && !paused,
                "S3 runtime settings parser consumes last author false/zero without B takeover", ref failures);
            a.Contributions[0].Values.Volume = 2;
            var returned = new BehaviorSourceDirectory(new[] { a, b, n });
            string beforeRefresh = settings.behaviorSourcesXml;
            string beforeRefreshFile = File.ReadAllText(path);
            Check(!settings.TryRefreshBehaviorSources(returned, FailedReplace, out _)
                && settings.behaviorSourcesXml == beforeRefresh && File.ReadAllText(path) == beforeRefreshFile,
                "S3 refresh failure retains original durable record and file, not merely switch failure", ref failures);
            resolved = settings.ResolveBehaviorSource(domain, "us.core/Work", returned, _ => true, out paused);
            var recoveredAfterFailure = Read().ResolveBehaviorSource(domain, "us.core/Work", missing, _ => true, out _);
            Check(resolved.Volume == 2 && recoveredAfterFailure.Volume == 0,
                "S3 current valid definition is a live view; failed refresh cannot claim its new value is durably recoverable", ref failures);
            Check(settings.TryRefreshBehaviorSources(returned, Persist, out _), "S3 same identity refresh commits last good snapshot", ref failures);
            resolved = Read().ResolveBehaviorSource(domain, "us.core/Work", missing, _ => false, out paused);
            Check(resolved.Volume == 2 && paused, "S3 committed fresh author parameters restore after reread but missing implementation still pauses only dependency", ref failures);
            a.Contributions[0].Values.Volume = double.NaN;
            Check(settings.TryRefreshBehaviorSources(returned = new BehaviorSourceDirectory(new[] { a, b, n }), Persist, out _), "S3 invalid update processed without destroying prior good record", ref failures);
            resolved = Read().ResolveBehaviorSource(domain, "us.core/Work", missing, _ => true, out paused);
            Check(resolved.Volume == 2 && !paused, "S3 bad update cannot overwrite durable good snapshot", ref failures);
            Check(settings.TryChangeBehaviorSource(domain, null, returned, Persist, out _), "S3 actual public forget operation commits precise domain", ref failures);
            reloaded = Read();
            var state = BehaviorSourceState.FromXml(reloaded.behaviorSourcesXml);
            Check(state.Read(domain).Intent == BehaviorSourceIntent.Inherit && state.Read(domain).Snapshot == null
                && state.Read(neighbor).SourceId == n.Id && reloaded.actionTuning.Single().sourcePresetDefName == "ImportedPlayer",
                "S3 forget/reread preserves neighbor and imported player data", ref failures);
        }
        finally { Directory.Delete(folder, true); }
    }
}
