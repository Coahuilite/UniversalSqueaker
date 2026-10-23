using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;
using UniversalSqueaker.Authoring;

internal static partial class Program
{
    private sealed class S2Backend : IResponseOutput, IResponseRandom
    {
        internal readonly PlaybackResult Result;
        internal readonly List<PlaybackRequest> Requests = new();
        internal readonly List<RandomPurpose> Rolls = new();
        internal S2Backend(PlaybackResult result)
        {
            Result = result;
        }
        public PlaybackReceipt Dispatch(PlaybackRequest request)
        {
            Requests.Add(request);
            return new PlaybackReceipt(Result, "InjectedS2Backend");
        }
        public double Next(RandomPurpose purpose)
        {
            Rolls.Add(purpose);
            return .25;
        }
    }

    private static void S2BoundRequests()
    {
        string Replay()
        {
            var sources = new AudioSourceIndex();
            sources.ObserveOriginal("US_RatkinExp_Work", "actual.audio");
            sources.ObserveFinal("US_RatkinExp_Work", "actual.audio");
            var pack = new VoicePackDeclaration { Race = "Ratkin" };
            pack.Entries.Add(new VoiceEntry { Key = "recorded", Action = "us.core/Work", Sound = "US_RatkinExp_Work" });
            var catalog = new DefinitionCatalog(120, 120);
        var audioInputs = new AudioInputBuilder(catalog);
            audioInputs.AddPack("entry.author", "VoicePack", pack, name => sources.TryBind(name, true, out var id, out _) ? id.ToString() : null);
            var transcript = new StringBuilder();
            foreach (var result in new[] { PlaybackResult.Dispatched, PlaybackResult.Rejected, PlaybackResult.Indeterminate, PlaybackResult.AlreadyActive })
            {
                var input = Input(1000);
                input.AudioCatalog = new AudioCatalog(audioInputs.Entries);
                var state = new DecisionState(1000);
                var backend = new S2Backend(result);
                var outcome = new ResponseDecision().Run(state, new StatePolicy(40, 20), input, backend, backend);
                Check(backend.Requests.Count == 1, "S2 injected backend invoked once");
                var request = backend.Requests.Single();
                Check(request.Audio.EntryKey == "entry.author/VoicePack/recorded", "S2 request retains referring entry identity");
                Check(request.Audio.Resource?.PackageId == "actual.audio", "S2 request retains actual resource owner");
                Check(request.Tick == 1000 && request.Domain == "Ratkin" && request.Action == "us.core/Work", "S2 request uses fixed actual facts/tick/action");
                Check(outcome.Receipt?.Result == result, "S2 observed injected receipt preserved");
                Check(backend.Rolls.Count == 0, "S2 no selection reroll for sole bound candidate");
                transcript.Append("tick=").Append(request.Tick).Append(" entry=").Append(request.Audio.EntryKey)
                    .Append(" audio=").Append(request.Audio.AudioId).Append(" calls=").Append(backend.Requests.Count)
                    .Append(" rolls=").Append(string.Join(",", backend.Rolls)).Append(" receipt=").Append(outcome.Receipt!.Value.Result)
                    .Append(" confirmed=").Append(state.ConfirmedTick?.ToString() ?? "-")
                    .Append(" shared=").Append(state.SharedDeadline).Append(" next=").Append(state.NextStateTick).Append('\n');
            }
            return transcript.ToString();
        }
        string first = Replay();
        Check(first == Replay(), "S2 complete bound request + four injected receipts double replay");
        string expected = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "us-s2-requests-golden.txt")).Replace("\r", "");
        Check(first == expected, "S2 fixed request/receipt transcript");
        Console.Write(first);
    }

    private static void S2WeightedAgeAndEgg()
    {
        var domain = AudioDomains.RaceOnly("Ratkin");
        var variants = new[]
        {
            new ActionSoundSet(new[] { "real.audio/AllAge" }, null, 1),
            new ActionSoundSet(new[] { "real.audio/ChildEgg" }, AgeBucket.Child, 1, true)
        };
        var actions = new Dictionary<string, IReadOnlyList<ActionSoundSet>> { ["us.core/Work"] = variants };
        var pool = new SqueakPoolRegistry(new[] { new VoicePackEntry("entry.author/P", domain, 1, actions) }, BuiltInFallbackTable.Empty);
        var noRoll = new S2Rolls();
        Check(pool.Select(new SelectionContext(domain, "us.core/Work", AgeBucket.Child, true, false), SelectionMode.Fallback, new S2Gate(), noRoll).IsNone,
            "S2 exact-age egg excluded does not unlock all-age");
        noRoll.Complete();
        var enabled = pool.Select(new SelectionContext(domain, "us.core/Work", AgeBucket.Child, true, true), SelectionMode.Fallback, new S2Gate(), new S2Rolls());
        Check(enabled.IsEgg && enabled.Resource?.DefName == "ChildEgg" && ReferenceEquals(enabled.SelectedVariant, variants[1]), "S2 exact-age egg and selected variant retained");
        variants[1] = new ActionSoundSet(Array.Empty<string>(), AgeBucket.Child, 1);
        Check(pool.Select(new SelectionContext(domain, "us.core/Work", AgeBucket.Child, true), SelectionMode.Fallback, new S2Gate(), new S2Rolls()).IsNone,
            "S2 unbound empty exact-age declaration does not unlock all-age");

        var weighted = new[]
        {
            new ActionSoundSet(new[] { "real.audio/Low" }, null, 1),
            new ActionSoundSet(new[] { "real.audio/HighA", "real.audio/HighB" }, null, 3)
        };
        var weightedActions = new Dictionary<string, IReadOnlyList<ActionSoundSet>> { ["us.core/Work"] = weighted };
        pool = new SqueakPoolRegistry(new[]
        {
            new VoicePackEntry("a", domain, 1, weightedActions),
            new VoicePackEntry("b", domain, 3, weightedActions)
        }, BuiltInFallbackTable.Empty);
        var rolls = new S2Rolls(.5, .5, .75);
        var choice = pool.Select(new SelectionContext(domain, "us.core/Work", AgeBucket.Adult, true), SelectionMode.Fallback, new S2Gate(), rolls);
        Check(choice.PoolStableKey == "b" && choice.Resource?.DefName == "HighB", "S2 pack/variant/sound weighted boundaries preserved");
        Check(rolls.Purposes.SequenceEqual(new[] { SelectionRollPurpose.Pack, SelectionRollPurpose.Variant, SelectionRollPurpose.Sound }), "S2 exact purpose-tagged random consumption");
        rolls.Complete();

        var race = AudioDomains.RaceOnly("Ratkin");
        var first = AudioDomain.NonPawn("A", "Ratkin");
        var second = AudioDomain.NonPawn("B", "Ratkin");
        var third = AudioDomain.NonPawn("A", "Bench");
        Check(first.Race.DefName == null && !first.IsRaceOnly && first.ObjectTarget == "Ratkin", "S2 NonPawn target never occupies RaceKey");
        var ordered = AudioDomains.CollectDomains(new[] { second, first, third, race, first });
        Check(ordered.SequenceEqual(new[] { race, third, first, second }), "S2 family/namespace/target deterministic deduplication and ordering");
        Check(AudioDomains.TryParseNonPawn(first.ToString(), out var restored) && restored == first, "S2 independent NonPawn identity serialization round trip");
        Check(first.ToString() == "NonPawn:A/Ratkin" && second.ToString() == "NonPawn:B/Ratkin", "S2 diagnostic identity preserves namespace");
        pool = new SqueakPoolRegistry(new[] { S2Entry("pawn", race, "pawn/Work"), S2Entry("nonpawn.a", first, "object.a/Work") },
            new BuiltInFallbackTable(new[] { new FallbackProfile(race.Race, 1, new Dictionary<string, string> { ["us.core/Work"] = "core/PawnFallback" }) }));
        Check(pool.Select(new SelectionContext(second, "us.core/Work", AgeBucket.Adult, true), SelectionMode.Fallback, new S2Gate(), new S2Rolls()).IsNone,
            "S2 NonPawn B/Ratkin cannot borrow Pawn Ratkin or NonPawn A/Ratkin or Pawn fallback");
        Check(pool.Select(new SelectionContext(first, "us.core/Work", AgeBucket.Adult, true), SelectionMode.Fallback, new S2Gate(), new S2Rolls()).Resource?.PackageId == "object.a",
            "S2 NonPawn A consumes only its own exact pool");
    }
}
