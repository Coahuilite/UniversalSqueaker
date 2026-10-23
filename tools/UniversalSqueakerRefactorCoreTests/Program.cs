using System;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Responses;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++;
        Console.WriteLine("PASS " + label);
    }

    private sealed class Output : IResponseOutput, IResponseRandom
    {
        internal PlaybackResult Result;
        internal Action<PlaybackRequest>? Enter;
        internal int Calls;
        internal PlaybackRequest? LastRequest;
        public double Next(RandomPurpose purpose) => .1;
        public PlaybackReceipt Dispatch(PlaybackRequest request)
        {
            Calls++;
            LastRequest = request;
            Enter?.Invoke(request);
            return new PlaybackReceipt(Result, "SynchronousReceipt");
        }
    }

    private static void Main()
    {
        var catalog = new DefinitionCatalog(120, 120, true);
        var contract = new NotificationContract("example.worker/Finished", 120);
        Check(catalog.DeclareNotification(contract), "one C# declaration registers explicit notification contract");
        Check(!catalog.DeclareNotification(contract), "duplicate notification is rejected, not a second contract");
        Check(catalog.HasFact(contract.FactId), "notification fact is explicit and registered");
        var actions = Array.FindAll(catalog.Actions, x => x.Key == contract.ActionId);
        Check(actions.Length == 1 && actions[0].Condition.RequiredFacts().GetEnumerator().MoveNext(), "declared action retains its fixed notification condition");
        DecisionInput Input(long tick, string domain = "RaceA") => new()
        {
            Subject = "stable-subject", Domain = domain, Tick = tick, Actions = actions,
            Facts = catalog.Capture("stable-subject", new Dictionary<string, Truth> { [contract.FactId] = Truth.True }),
            AudioCatalog = new UniversalSqueaker.Kernel.AudioCatalog(new[] { new AudioCandidate("example/entry", "example/audio", contract.ActionId, domain, 1) })
        };
        foreach (var result in Enum.GetValues<PlaybackResult>())
        {
            var owner = new ResponseSession("stable-subject", 100, new StatePolicy(40, 20));
            var output = new Output { Result = result };
            DecisionOutcome reentry = default;
            output.Enter = _ => reentry = owner.Respond(Input(100), output, output);
            var outcome = owner.Respond(Input(100), output, output);
            Check(output.Calls == 1 && reentry.Reason == "TickOccupied", result + " reserves original owner before synchronous re-entry");
            Check(outcome.Receipt?.Result == result, result + " receipt remains exact");
            bool protectedResult = result == PlaybackResult.Dispatched || result == PlaybackResult.Indeterminate;
            Check((owner.State.ActionDeadline(contract.ActionId) == 220) == protectedResult, result + " hard settlement");
            Check((owner.State.ConfirmedTick == 100) == (result == PlaybackResult.Dispatched), result + " confirmation meaning");
            output.Enter = null;
            var changedDomain = owner.Respond(Input(101, "RaceB"), output, output);
            Check(protectedResult ? changedDomain.Reason == "HardDeadline" : output.Calls == 2, result + " mutable domain does not allocate another owner/deadline");
        }
        AllFifteen();
        DomainAndPublication();
        SaveOwner();
        AuthorExamples();
        Console.WriteLine("REFACTOR neutral production Core PASS " + checks);
    }

    private sealed class Gate : UniversalSqueaker.Kernel.ISoundGate
    {
        public bool Playable(string audio, UniversalSqueaker.Kernel.SelectionContext context) => true;
    }

    private static void AuthorExamples()
    {
        string directory = System.IO.Path.Combine(Environment.CurrentDirectory,"docs","examples","0.6");
        int declarations = 0;
        foreach (string file in System.IO.Directory.GetFiles(directory,"*.xml"))
        {
            var doc = new System.Xml.XmlDocument(); doc.Load(file);
            foreach (System.Xml.XmlElement def in doc.DocumentElement!.SelectNodes("*")!)
            {
                var node = def.SelectSingleNode("declaration");
                switch (def.Name)
                {
                    case "UniversalSqueaker.UsVoicePackDef":
                        var pack = new VoicePackDeclaration(); pack.LoadDataFromXmlCustom(node!);
                        Check(pack.Entries.Count > 0 && pack.Entries.TrueForAll(x=>AuthorXml.ActionId(x.Action)), "current public audio declaration " + System.IO.Path.GetFileName(file)); declarations++; break;
                    case "UniversalSqueaker.UsActionDef":
                        var action = new ActionDeclaration(); action.LoadDataFromXmlCustom(node!);
                        Check(AuthorXml.ActionId(action.Key), "current public action declaration " + action.Key); declarations++; break;
                    case "UniversalSqueaker.UsBehaviorSourceDef":
                        var source = new BehaviorSourceDeclaration(); source.LoadDataFromXmlCustom(node!);
                        Check(source.Sources.Count > 0, "current public source declaration " + System.IO.Path.GetFileName(file)); declarations++; break;
                    case "UniversalSqueaker.UsNotificationDef":
                        var notification = new NotificationContract(def["actionId"]!.InnerText,long.Parse(def["intervalTicks"]!.InnerText));
                        Check(AuthorXml.ActionId(notification.ActionId), "current XML notification declaration"); declarations++; break;
                    case "UniversalSqueaker.UsProviderDef": case "UniversalSqueaker.UsBehaviorPresetDef": break; // Native Def fields; preset custom values are checked by the settings fixture.
                    default: throw new Exception("Retired/unknown public Def example: " + def.Name);
                }
            }
        }
        Check(declarations >= 20,"public declaration inventory was actually traversed");
    }

    private static void AllFifteen()
    {
        var definitions = new DefinitionCatalog(0, 0, true);
        var policy = new UsDevelopmentPolicy(20, 0, 0, 0, 0, 1, 1, UsEquipQualification.PlayerCoreEquip);
        var entries = new List<AudioCandidate>();
        foreach (string action in CoreActions.All) entries.Add(new AudioCandidate("pack/" + action, "owner/audio", action, "RaceA", 1));
        definitions.Publish();
        var audio = new UniversalSqueaker.Kernel.AudioCatalog(entries);
        var configuration = new BehaviorConfiguration("", Array.Empty<BehaviorPlayerOverride>(), true, 1, 1);
        var owner = new ResponseSession("stable-subject", 100, policy.State);
        var output = new Output { Result = PlaybackResult.Dispatched };
        long tick = 100;
        foreach (var action in definitions.Actions)
        {
            Check(action.Scope == BuiltInPawnContracts.DefaultScope(action.Key)
                && BuiltInPawnContracts.NormalizeScope(action.Key, action.Scope) == action.Scope,
                "canonical scope default is the actual declared contract " + action.Key);
            var values = new Dictionary<string, Truth>(StringComparer.Ordinal);
            foreach (string fact in definitions.FactKeys) values[fact] = Truth.False;
            foreach (string fact in action.Condition.RequiredFacts()) values[fact] = Truth.True;
            values["us.core/PlayerControlled"] = values["us.core/Awake"] = values["us.core/NotDowned"] = values["us.core/PlayerEquipCommand"] = Truth.True;
            var frame = new ResponseFrame { Subject = owner.Subject, Domain = new BehaviorDomain("Pawn", "", "RaceA"), Tick = tick,
                StateOpportunity = true, ActiveCommand = true, Facts = new FactSnapshot(values),
                AvailableFacts = new HashSet<string>(definitions.FactKeys, StringComparer.Ordinal), SoundGate = new Gate() };
            var outcome = owner.Respond(new RuntimeView(definitions, configuration, audio), frame, policy, output, output);
            Check(outcome.Action == action.Key && outcome.Receipt?.Result == PlaybackResult.Dispatched, "full neutral fold/choice/settlement " + action.Key);
            Check(output.LastRequest?.SubjectRole == action.SubjectRole && output.LastRequest?.Audio.Action == action.Key,
                "exact request role/audio identity " + action.Key);
            Check(owner.EffectiveActions.Count == 15, "bounded current-subject fold contains all fifteen, not a persistent Pawn/action/domain matrix " + action.Key);
            tick = owner.State.NextStateTick;
        }
        var relaxed = new BehaviorValues { SelectRequirePlayerControlled = false, SelectRequireAwake = false, SelectRequireNotDowned = false, HardIntervalTicks = 0, Volume = 0 };
        var configured = new BehaviorConfiguration("", new[] { new BehaviorPlayerOverride("Pawn", "", "RaceA", "", CoreActions.Select, relaxed) }, true, 1, 1);
        var select = Array.Find(definitions.Actions, x => x.Key == CoreActions.Select)!;
        var effective = configured.Resolve(new BehaviorDomain("Pawn", "", "RaceA"), select.Key, BehaviorContract.Defaults(select, policy), definitions.BehaviorSources,
            _ => true, select.Condition.RequiredFacts());
        var read = effective.Read();
        Check(read.Volume == 0 && read.HardIntervalTicks == 0 && read.SelectRequireAwake == false, "sparse false/zero survive new configuration fold");
        Check(BehaviorContract.EffectiveCondition(select, read).Evaluate(new FactSnapshot(new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.Unknown })) == Truth.Unknown,
            "relaxed optional Pawn qualifications cannot cancel fixed Select acceptance/feedback fact");
        var layered = new BehaviorConfiguration("", new[] {
            new BehaviorPlayerOverride("Pawn", "", "", "", CoreActions.Select, new BehaviorValues { SelectRequirePlayerControlled = false }),
            new BehaviorPlayerOverride("Pawn", "", "RaceA", "", CoreActions.Select, new BehaviorValues { Volume = 0, SelectRequireAwake = false })
        }, true, 1, 1);
        var profile = layered.ResolveProfile("Pawn", "", CoreActions.Select, SelectPolicy.OfficialDefaults, definitions.BehaviorSources, _ => true).Read();
        Check(profile.SelectRequirePlayerControlled == false && profile.SelectRequireAwake == true && !profile.Volume.HasValue,
            "service profile query folds only profile intent, without a synthetic Pawn domain borrowing a race row");
        var left = new BehaviorDomain("NonPawn", "left.mod", "Machine");
        var right = new BehaviorDomain("NonPawn", "right.mod", "Machine");
        Check(left.Identity != right.Identity && !left.Equals(right), "behavior and audio share the same exact namespaced domain identity");
        bool immutable = false;
        try { definitions.AddAction(new ActionDeclaration { Key = "external/TooLate" }); }
        catch (InvalidOperationException) { immutable = true; }
        Check(immutable && !definitions.HasAction("external/TooLate"), "published definition revision cannot be mutated by a late declaration");
    }

    private static void SaveOwner()
    {
        var save = new SettingsSaveOwner();
        int writes = 0;
        save.Edited(0); save.Edited(.1);
        Check(!save.Flush(.44, () => writes++) && writes == 0, "ordinary physical save waits for last edit + 0.35 realtime");
        Check(save.Flush(.46, () => writes++) && writes == 1 && !save.Dirty, "ordinary edits coalesce into one actual completed write");
        save.Edited(1);
        Check(!save.Flush(1.36, () => { writes++; throw new Exception("disk"); }) && save.State == SettingsSaveState.Failed && save.Dirty, "failed physical save remains dirty");
        Check(!save.Flush(2, () => writes++, true) && writes == 2, "debounce/framework force does not spin a failed generation");
        save.Flush(2, () => { writes++; throw new Exception("close disk"); }, true, true);
        save.Flush(2, () => writes++, true, true);
        Check(writes == 3 && save.Dirty, "closing retries failed generation once, not again on following framework close");
        save.Retry(); save.Flush(3, () => writes++, true);
        Check(writes == 4 && !save.Dirty, "explicit retry confirms same dirty generation");
        var atomic = new SettingsSaveOwner();
        int persisted = 0, published = 0;
        Check(!atomic.CommitAtomic(0, () => false, out _) && atomic.PhysicalSaveCount == 0, "rejected atomic candidate does not claim disk commit");
        Check(atomic.CommitAtomic(0, () => { persisted++; return true; }, out _), "atomic result is committed only after actual persistence returns");
        Check(!atomic.PublishCommitted(() => { published++; throw new Exception("publish"); }, out string failure)
            && failure.StartsWith("Committed:", StringComparison.Ordinal) && atomic.PublicationPending && persisted == 1, "publish failure preserves committed disk result and marks repair");
        Check(atomic.RepairPublication(() => published++, out _) && !atomic.PublicationPending && persisted == 1 && published == 2,
            "publication repair does not repeat successful disk transaction");
        atomic.Edited(1);
        atomic.Flush(1.4, () => atomic.Edited(1.4), true);
        Check(atomic.Dirty && !atomic.Flush(1.5, () => persisted++), "edit during synchronous save retains its own generation/debounce deadline");
    }
    private static void DomainAndPublication()
    {
        var exact = new BehaviorDomain("Pawn", "", "RaceA", "X");
        var otherRace = new BehaviorDomain("Pawn", "", "RaceB", "X");
        var values = new BehaviorValues { Enabled = false, Volume = 0 };
        var rows = new[] { new BehaviorPlayerOverride("Pawn", "", "RaceA", "X", CoreActions.Work, values) };
        var configuration = new BehaviorConfiguration("", rows, true, 1, 1, revision: 7);
        Check(configuration.Id == 7 && new BehaviorConfiguration("", rows, true, 1, 1).Id == 0,
            "configuration publisher supplies revision identity; neutral core has no process-global revision counter");
        Check(configuration.HasDomain(exact.Identity) && !configuration.HasDomain(otherRace.Identity),
            "current configuration declares only its exact race/child domain, not a global xenotype permission");
        var selected = new[] { new UniversalSqueaker.Kernel.AudioPackSelection(exact.Identity, new[] { "pack/A" }) };
        var selection = new BehaviorConfiguration("", Array.Empty<BehaviorPlayerOverride>(), true, 1, 1, selection: selected);
        Check(selection.HasDomain(exact.Identity) && !selection.HasDomain(otherRace.Identity), "exact audio selection membership never crosses race");
        var source = new BehaviorSource { Id = "author/Source", Domain = exact, Contributions = new()
            { new() { Action = CoreActions.Work, Values = new BehaviorValues { Volume = .8 } } } };
        var directory = new BehaviorSourceDirectory(new[] { source });
        Check(directory.HasDomain(exact.Identity) && !directory.HasDomain(otherRace.Identity), "definition-stage source domain membership is exact");
        var state = new BehaviorSourceState();
        Check(state.Bind(exact, source.Id, directory), "complete source action identity is admitted");
        var captured = new BehaviorConfiguration(state.ToXml(), Array.Empty<BehaviorPlayerOverride>(), true, 1, 1);
        Check(captured.HasDomain(exact.Identity) && !captured.HasDomain(otherRace.Identity), "durable last-good source domain keeps precise membership when content is absent");
        source.Contributions[0].Action = "Work";
        Check(!new BehaviorSourceState().Bind(exact, source.Id, new BehaviorSourceDirectory(new[] { source })), "short action spelling is not admitted as another source identity");
        source.Contributions[0].Action = CoreActions.Work; source.Contributions[0].Values.Participation["us.core/Age"] = false;
        Check(!new BehaviorSourceState().Bind(exact, source.Id, new BehaviorSourceDirectory(new[] { source })), "author source cannot acquire player-only participation permission");
    }
}
