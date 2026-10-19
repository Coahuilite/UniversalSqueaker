using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniversalSqueaker.Kernel;

internal static partial class Program
{
    private sealed class S2Rolls : ISelectionRollSource
    {
        public readonly List<SelectionRollPurpose> Purposes = new();
        private readonly Queue<double> values;
        public S2Rolls(params double[] values) { this.values = new Queue<double>(values); }
        public double Next01() => throw new Exception("Selection purpose missing");
        public double Next01(SelectionRollPurpose purpose)
        {
            Purposes.Add(purpose);
            if (values.Count == 0) throw new Exception("Unexpected selection roll " + purpose);
            return values.Dequeue();
        }
        public void Complete() => Check(values.Count == 0, "S2 all scripted rolls consumed");
    }
    private sealed class S2Gate : ISoundGate
    {
        public readonly HashSet<string> Denied = new(StringComparer.Ordinal);
        public bool Playable(string sound, SelectionContext context) => !Denied.Contains(sound);
    }
    private static void S2Resources()
    {
        var sources = new AudioSourceIndex();
        for (int i = 0; i < 15; i++)
        {
            sources.ObserveOriginal("Ratkin" + i, "Pack.Actual");
            sources.ObserveFinal("Ratkin" + i, i == 1 ? null : "pack.actual");
        }
        sources.ObserveOriginal("Ratkin0", "other.pack");
        sources.ObserveFinal("Ratkin0", "other.pack");
        Check(!sources.TryBind("Ratkin0", true, out _, out var conflict) && conflict == "DuplicateFinalDeclaration", "S2 duplicate Work isolated");
        for (int i = 1; i < 15; i++)
            Check(sources.TryBind("Ratkin" + i, true, out var id, out _) && id.PackageId == "pack.actual", "S2 other fourteen / legal root replacement " + i);
        Check(!sources.TryBind("Ratkin2", false, out _, out var unsafePool) && unsafePool == "UnsafeNativePool", "S2 unsafe pool rejected");
        sources.ObserveOriginal("Deleted", "a");
        Check(!sources.TryBind("Deleted", true, out _, out var deleted) && deleted == "DeletedOrUnconstructed", "S2 delete never resurrects");
        sources.ObserveFinal("PatchedNew", null);
        Check(!sources.TryBind("PatchedNew", true, out _, out _), "S2 patch-new cannot invent owner");
        sources.ObserveOriginal("Foreign", "a");
        sources.ObserveFinal("Foreign", "b");
        Check(!sources.TryBind("Foreign", true, out _, out _), "S2 contradictory replacement source rejected");
        Check(new AudioId("Actual.Provider", "Work").ToString() == "actual.provider/Work", "S2 canonical owner is distinct from author entry");
        Check(AudioId.TryParse("actual.provider/Work", out _) && !AudioId.TryParse("Work", out _), "S2 missing binding cannot use bare name");
        string first = S2RouteTrace();
        string second = S2RouteTrace();
        Check(first == second, "S2 two deterministic route replays");
        string expected = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "us-s2-golden.txt")).Replace("\r", "");
        Check(first == expected, "S2 independent fixed route golden (not old corpus replacement)");
        Console.Write(first);
    }
    private static VoicePackEntry S2Entry(string pack, AudioDomain domain, string sound, string? fallback = null)
    {
        return new VoicePackEntry(pack, domain, 1, new Dictionary<string, IReadOnlyList<ActionSoundSet>>
        {
            ["Work"] = new[] { new ActionSoundSet(new[] { sound }, null, 1) }
        }, fallback == null ? null : new Dictionary<string, string> { ["Work"] = fallback });
    }
    private static string S2RouteTrace()
    {
        var race = AudioDomains.RaceOnly("Ratkin");
        var xeno = new AudioDomain(race.Race, new XenotypeKey("X"));
        var bench = AudioDomain.NonPawn("bench", "A");
        var armor = AudioDomain.NonPawn("armor", "A");
        var entries = new[]
        {
            S2Entry("author.a/Pack", race, "actual.provider/Race", "core/RaceFallback"),
            S2Entry("author.b/Pack", xeno, "other.provider/Xeno", "core/XenoFallback"),
            S2Entry("bench.author/Pack", bench, "bench.audio/Work"),
            S2Entry("armor.author/Pack", armor, "armor.audio/Work")
        };
        var fallback = new BuiltInFallbackTable(new[] { new FallbackProfile(race.Race, 1, new Dictionary<string, string> { ["Work"] = "core/Work" }) });
        var pool = new SqueakPoolRegistry(entries, fallback);
        var text = new StringBuilder();
        void Case(string label, AudioDomain domain, SelectionMode mode, S2Gate gate, params double[] draws)
        {
            var random = new S2Rolls(draws);
            var result = pool.Select(new SelectionContext(domain, "Work", AgeBucket.Adult, true), mode, gate, random);
            random.Complete();
            Check(result.IsNone || result.Resource.HasValue, "S2 selected resource has complete identity");
            if (!result.IsNone) Check(result.SelectedContext?.Domain == domain, "S2 selected context retained");
            text.Append("tick=1000 ").Append(label).Append(" ").Append(result.SoundKey ?? "None").Append(" ")
                .Append(result.PoolStableKey ?? "-").Append(" ").Append(result.Tier?.ToString() ?? "-")
                .Append(" rolls=").Append(string.Join(",", random.Purposes)).Append(" receipt=")
                .Append(result.IsNone ? "NotSubmitted" : "Dispatched").Append('\n');
        }
        Case("Fallback", xeno, SelectionMode.Fallback, new S2Gate());
        Case("Off", xeno, SelectionMode.Off, new S2Gate());
        Case("Disabled", xeno, SelectionMode.Disabled, new S2Gate());
        Case("Remix", xeno, SelectionMode.Remix, new S2Gate(), .5);
        var denied = new S2Gate();
        denied.Denied.UnionWith(new[] { "actual.provider/Race", "other.provider/Xeno" });
        Case("ExactFallback", xeno, SelectionMode.Fallback, denied);
        denied.Denied.Add("core/XenoFallback");
        Case("NoRaceFallbackLeak", xeno, SelectionMode.Fallback, denied);
        Case("Bench", bench, SelectionMode.Fallback, new S2Gate());
        Case("Armor", armor, SelectionMode.Fallback, new S2Gate());
        Check(bench != armor && bench.Parent == bench, "S2 exact NonPawn namespaces have no Pawn parent");
        return text.ToString();
    }
}
