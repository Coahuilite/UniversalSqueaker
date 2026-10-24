using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UniversalSqueaker.UI;
using UniversalSqueaker.Runtime;
namespace UniversalSqueaker.KernelHostTests;
/// <summary>Current production write/fold/read-model counterexamples replacing retired multiplier API shapes.</summary>
internal static class TuningMultiplierLaneTests
{
    internal static void RunAll()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        var runtime = typeof(GameSettings).Assembly.GetType("UniversalSqueaker.GameRuntime", true)!;
        runtime.GetMethod("InitializeMainThread", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
        var settings = new GameSettings();
        var fold = typeof(VoicePacksPageModel).GetMethod("BuildActionScopes", BindingFlags.NonPublic | BindingFlags.Static)!;
        IReadOnlyList<ActionScopeRowView> Rows(int layer, string race, string child)
            => (IReadOnlyList<ActionScopeRowView>)fold.Invoke(null, new object[] {settings, layer, race, child})!;
        var initial = Rows(0,"","");
        Require(initial.Count == 15 && initial.Select(x=>x.ActionKey).Distinct().Count()==15, "all fifteen canonical IDs appear exactly once");
        Require(initial.Single(x=>x.ActionKey==CoreActions.Work).EffectiveScope==BehaviorScope.ActiveCommand, "actual Work scope default comes from canonical contract");
        Require(settings.TryReplaceBehaviorOverride("Pawn","","","",CoreActions.Work,new BehaviorValues{Volume=0,HardIntervalTicks=0},()=>true,out _), "sparse profile edit committed");
        Require(settings.TryReplaceBehaviorOverride("Pawn","","RaceA","",CoreActions.Work,new BehaviorValues{Pitch=2},()=>true,out _), "independent race field committed");
        var global = Rows(0,"","").Single(x=>x.ActionKey==CoreActions.Work);
        var race = Rows(1,"RaceA","").Single(x=>x.ActionKey==CoreActions.Work);
        Require(global.Explanation.Contains("volume=player:Pawn::profile") && !global.Explanation.Contains("pitch=player:Pawn::RaceA:"), "profile never borrows a race supplier");
        Require(race.Explanation.Contains("volume=player:Pawn::profile") && race.Explanation.Contains("pitch=player:Pawn::RaceA:"), "UI preserves per-field origins from the actual runtime fold");
        var preset = new UsBehaviorPresetDef { defName="ResetTarget", label="Current template", races=new List<PresetRaceEntry>{new(){raceDefName="RaceA",actions=new(){new(CoreActions.Work,new BehaviorValues{Pitch=1.5})}}}};
        SqueakGameDefs.BaselineByName = key => key==preset.defName ? preset : null;
        try
        {
            settings.actionTuning.Single(x=>x.actionKey==CoreActions.Work && x.raceDefName=="RaceA").sourcePresetDefName=preset.defName;
            Require(Rows(1,"RaceA","").Single(x=>x.ActionKey==CoreActions.Work).PresetResetReady, "valid exact template is a reset target");
            preset.races[0].actions[0].Values.Pitch=double.NaN;
            Require(!Rows(1,"RaceA","").Single(x=>x.ActionKey==CoreActions.Work).PresetResetReady, "invalid current template remains visible as unavailable, without a read-model exception");
        }
        finally { SqueakGameDefs.ResetToGameDefaults(); Program.SetTranslatorResolver(null); }
        Console.WriteLine("R2 production settings/canonical fold/UI origins and invalid-template read model PASS");
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}