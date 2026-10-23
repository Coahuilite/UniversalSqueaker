using System;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using UniversalSqueaker.Kernel;
using UniversalSqueaker.Responses;
namespace UniversalSqueaker.KernelTests;
public static partial class UnitTests
{
    private static void ActionKeyMapping(ref int failures)
    {
        Check(CoreActions.All.Count == 15 && CoreActions.All.Distinct(StringComparer.Ordinal).Count() == 15
            && CoreActions.All.SequenceEqual(BuiltInActionKeys.All), "one complete official action inventory, no enum/short-key authority", ref failures);
        Check(CoreActions.All.All(AuthorXml.ActionId) && !BuiltInActionKeys.Contains("Call") && !BuiltInActionKeys.Contains("us.core/call"),
            "complete IDs are ordinal and short/wrong-case spellings are not official aliases", ref failures);
        Check(!BuiltInActionKeys.Contains("us.core/Crying") && !BuiltInActionKeys.Contains("us.core/Giggling")
            && typeof(DefinitionCatalog).Assembly.GetType("UniversalSqueaker.ActionKey") == null,
            "retired official packages and the old short-key/enum bridge remain absent", ref failures);
    }
    private static void VerseEventBindingContract(ref int failures)
    {
        var definitions = new DefinitionCatalog(120, 120, true);
        foreach (var plan in definitions.Actions)
            Check(plan.Condition.RequiredFacts().Contains(BuiltInPawnContracts.FactKey(plan.Key)) && plan.SubjectRole != "Subject",
                "actual fixed contract/role for " + plan.Key, ref failures);
        Check(BuiltInPawnContracts.CandidateOrder.Count(x => BuiltInPawnContracts.IsEvent(x)) == 8,
            "official eight events and seven current-state contracts are distinct, no inferred unknown event", ref failures);
    }
    private static void TriggerInvocationRules(ref int failures)
    {
        var select = new DefinitionCatalog(120, 120, true).Actions.Single(x => x.Key == CoreActions.Select);
        var fixedFacts = select.Condition.RequiredFacts().ToArray();
        Check(fixedFacts.SequenceEqual(new[] { "us.core/SelectEvent" }) && select.SkipTalking && !select.RequiresVocal,
            "Select fixed acceptance/feedback/final-subject fact has no Responsive/talking/vocal hidden gate", ref failures);
        var relaxed = new BehaviorValues { SelectRequirePlayerControlled = false, SelectRequireAwake = false, SelectRequireNotDowned = false };
        var condition = BehaviorContract.EffectiveCondition(select, relaxed);
        Check(condition.Evaluate(new FactSnapshot(new Dictionary<string, Truth> { ["us.core/SelectEvent"] = Truth.Unknown })) == Truth.Unknown,
            "relaxed explicit Pawn qualifications cannot manufacture accepted feedback", ref failures);
        Check(SelectPolicy.OfficialDefaults.SelectRequirePlayerControlled == true && SelectPolicy.OfficialDefaults.SelectRequireAwake == true
            && SelectPolicy.OfficialDefaults.SelectRequireNotDowned == true, "three official Pawn qualification defaults stay independent and true", ref failures);
    }
    private static void LayeredTuning(ref int failures)
    {
        var domain = new BehaviorDomain("Pawn", "", "RaceA", "X");
        var rows = new[] {
            new BehaviorPlayerOverride("Pawn", "", "", "", CoreActions.Call, new BehaviorValues { Scope = BehaviorScope.Disabled, Volume = 0, HardIntervalTicks = 300 }),
            new BehaviorPlayerOverride("Pawn", "", "RaceA", "", CoreActions.Call, new BehaviorValues { Pitch = 2 }),
            new BehaviorPlayerOverride("Pawn", "", "RaceA", "X", CoreActions.Call, new BehaviorValues { Scope = BehaviorScope.AnyOccurrence }),
            new BehaviorPlayerOverride("Pawn", "", "RaceA", "X", CoreActions.Call, new BehaviorValues()) };
        var empty = new BehaviorSourceDirectory(Array.Empty<BehaviorSource>());
        var configuration = new BehaviorConfiguration("", rows, true, 1, 1);
        var value = configuration.Resolve(domain, CoreActions.Call, new BehaviorValues { Enabled = true }, empty, _ => true).Read();
        Check(value.Scope == BehaviorScope.AnyOccurrence && value.Volume == 0 && value.HardIntervalTicks == 300 && value.Pitch == 2,
            "same sparse authority folds profile/race/child field-wise; empty row is identity and zero remains present", ref failures);
        Check(configuration.Resolve(new BehaviorDomain("Pawn", "", "Other", "X"), CoreActions.Call, new BehaviorValues(), empty, _ => true).Read().Pitch == null,
            "same child name in another race cannot borrow the local response field", ref failures);
        var source = new BehaviorValues { Volume = .8 }; var copied = source.Copy(); source.Volume = 9;
        Check(copied.Volume == .8, "captured sparse values do not retain mutable author/player dictionaries", ref failures);
    }
    private static void XenoDoubleKeyDomains(ref int failures)
    {
        var a = new BehaviorDomain("Pawn", "", "RaceA", "X"); var b = new BehaviorDomain("Pawn", "", "RaceB", "X");
        var config = new BehaviorConfiguration("", new[] {
            new BehaviorPlayerOverride("Pawn", "", "RaceA", "X", CoreActions.Call, new BehaviorValues { Volume = 2 }),
            new BehaviorPlayerOverride("Pawn", "", "RaceB", "X", CoreActions.Call, new BehaviorValues { Volume = 3 }) }, true, 1, 1);
        var empty = new BehaviorSourceDirectory(Array.Empty<BehaviorSource>());
        Check(config.Resolve(a, CoreActions.Call, new BehaviorValues(), empty, _ => true).Read().Volume == 2
            && config.Resolve(b, CoreActions.Call, new BehaviorValues(), empty, _ => true).Read().Volume == 3,
            "exact race/child domains are independent in the actual current fold", ref failures);
        Check(config.HasDomain(a.Identity) && config.HasDomain(b.Identity) && !config.HasDomain(new BehaviorDomain("Pawn", "", "Human", "X").Identity),
            "domain membership is exact; native canonical/DLC lookup stays in the game adapter, not a second pure resolver", ref failures);
        Check(AudioDomains.Collect(new[] { ("RaceA", "X"), ("RaceB", "X"), ("RaceA", "X") }).Count == 2,
            "domain collection dedupes exact pairs without merging same-named children", ref failures);
        Check(new BehaviorDomain("NonPawn", "armor", "A").Identity != new BehaviorDomain("NonPawn", "bench", "A").Identity,
            "non-Pawn targets remain namespace-qualified", ref failures);
    }
    private sealed class ReceiptDriver : IResponseOutput, IResponseRandom
    {
        internal PlaybackResult Result; internal int Calls;
        public PlaybackReceipt Dispatch(PlaybackRequest request) { Calls++; return new PlaybackReceipt(Result, "fixture"); }
        public double Next(RandomPurpose purpose) => .1;
    }
    private static void TimingModelRules(ref int failures)
    {
        const string key = "fixture/Clock";
        DecisionInput Input(long tick) => new() { Subject = "stable", Domain = "RaceA", Tick = tick,
            Actions = new[] { new ActionPlan(key, 1, new Condition("all"), 300) },
            AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("fixture/entry", "owner/audio", key, "RaceA", 1) }) };
        foreach (PlaybackResult result in Enum.GetValues(typeof(PlaybackResult)))
        {
            var state = new DecisionState(1000); var driver = new ReceiptDriver { Result = result }; var decision = new ResponseDecision();
            var first = decision.Run(state, new StatePolicy(216, 20), Input(1000), driver, driver);
            Check(first.Receipt?.Result == result && decision.Run(state, new StatePolicy(216, 20), Input(1000), driver, driver).Reason == "TickOccupied"
                && driver.Calls == 1, "single tick reservation survives receipt " + result, ref failures);
            bool protects = result == PlaybackResult.Dispatched || result == PlaybackResult.Indeterminate;
            Check((state.ActionDeadline(key) == 1300 && state.SharedDeadline == 1216) == protects
                && (state.ConfirmedTick == 1000) == (result == PlaybackResult.Dispatched), "hard boundary/confirmed receipt meanings " + result, ref failures);
            if (protects)
            {
                Check(decision.Run(state, new StatePolicy(216, 20), Input(1299), driver, driver).Reason == "HardDeadline" && driver.Calls == 1,
                    "action deadline is closed one tick before exact boundary", ref failures);
                Check(decision.Run(state, new StatePolicy(216, 20), Input(1300), driver, driver).Receipt?.Result == result && driver.Calls == 2,
                    "exact action deadline reopens without another clock", ref failures);
            }
        }
        var independent = new DecisionState(1000); var output = new ReceiptDriver { Result = PlaybackResult.Dispatched }; var engine = new ResponseDecision();
        engine.Run(independent, new StatePolicy(216, 20), Input(1000), output, output);
        var bypass = Input(1001); bypass.Actions = new[] { new ActionPlan("fixture/Other", 2, new Condition("all"), 0, checkShared: false, writeShared: true) };
        bypass.AudioCatalog = new AudioCatalog(new[] { new AudioCandidate("fixture/other", "owner/audio", "fixture/Other", "RaceA", 1) });
        Check(engine.Run(independent, new StatePolicy(216, 20), bypass, output, output).Receipt?.Result == PlaybackResult.Dispatched && independent.SharedDeadline == 1217,
            "explicit ignore-read still writes the single shared clock", ref failures);
        Check(typeof(ResponseSession).Assembly.GetType("UniversalSqueaker.SqueakTimingModel") == null,
            "retired realtime/time-speed/population multiplier model is absent, not a second timing authority", ref failures);
    }
}
