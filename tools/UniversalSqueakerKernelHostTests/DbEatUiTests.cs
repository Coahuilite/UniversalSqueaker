using System;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

internal static class DbEatUiTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("DB Eat UI: " + message); }
    internal static void CheckBindings(UiHost host, RecordingSettingsSource source)
    {
        int revision = host.Session.ContentRevision;
        host.Bindings.Set("eat-phase", "Execution");
        host.Bindings.Set("eat-nutrition", "NonNutritionGiving");
        host.Bindings.Set("eat-drug", "OnlyDrugs");
        Check(source.EatWrites["phase"] == "Execution" && source.EatWrites["nutritionScope"] == "NonNutritionGiving"
            && source.EatWrites["drugScope"] == "OnlyDrugs", "three typed values route independently");
        Check(host.Session.ContentRevision == revision + 3, "one shared revision bump per edit");
        host.Bindings.Set("eat-nutrition", "");
        Check(source.EatWrites["nutritionScope"] == "" && source.EatWrites["drugScope"] == "OnlyDrugs", "inherit clears only its own field");
    }
    internal static void Run()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach (float width in new[] { 760f, 480f })
                {
                    var source = new RecordingSettingsSource { RichData = true };
                    using var host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
                    host.Bindings.Invoke("go-page", "Output");
                    var snapshot = host.MeasureAndArrange(new UiLayoutPoint(width, 900));
                    foreach (string axis in new[] { "eat", "eat-nutrition", "eat-drug" })
                    {
                        Check(snapshot.RectById.TryGetValue("basic-" + axis + "-choice", out var bounds) && bounds.Width > 100 && bounds.Height >= 26,
                            language + "/" + width + " arranges usable independent dropdown " + axis);
                        Console.WriteLine("DB Eat UI " + language + "/" + width + " basic-" + axis + "-choice=" + bounds);
                    }
                    CheckBindings(host, source);
                    host.DrawChecked(new UiLayoutRect(0, 0, width, 900));
                }
            }
            finally { Program.SetTranslatorResolver(null); }
        }
        // Actual production facade writes the selected existing layer, not a disconnected global boolean.
        var settings = new GameSettings();
        var actual = new UsKernelSettingsSource(settings);
        actual.ViewState.TuningLayer = 0; actual.SetEatParameter("phase", "Execution");
        actual.ViewState.TuningLayer = 1; actual.ViewState.TuningRaceDefName = "Ratkin"; actual.SetEatParameter("drugScope", "OnlyDrugs");
        actual.ViewState.TuningLayer = 2; actual.ViewState.TuningXenotypeDefName = "LegalXeno"; actual.SetEatParameter("nutritionScope", "NonNutritionGiving");
        Check(settings.actionTuning.Count == 3 && settings.actionTuning.All(x => x.actionKey == "us.core/Eat")
            && settings.actionTuning.Single(x => x.raceDefName == "").hasEatPhase
            && settings.actionTuning.Single(x => x.raceDefName == "Ratkin" && x.xenotypeDefName == "").hasDrugScope
            && settings.actionTuning.Single(x => x.xenotypeDefName == "LegalXeno").hasNutritionScope, "actual source writes three exact sparse layers");
        actual.SetEatParameter("nutritionScope", "");
        Check(!settings.actionTuning.Single(x => x.xenotypeDefName == "LegalXeno").hasNutritionScope
            && settings.actionTuning.Single(x => x.raceDefName == "Ratkin" && x.xenotypeDefName == "").hasDrugScope,
            "actual inheritance operation preserves neighboring layer");
        Check(settings.TryReplaceBehaviorOverride("Pawn", "", "Ratkin", "", "us.core/Eat",
            new BehaviorValues { Scope = BehaviorScope.Disabled }, () => true, out _), "public settings API accepts full ActionId row");
        var fold = typeof(VoicePacksPageModel).GetMethod("BuildActionScopes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var scopes = (System.Collections.Generic.IReadOnlyList<ActionScopeRowView>)fold.Invoke(null, new object[] { settings, 1, "Ratkin", "" })!;
        Check(scopes.Single(x => x.ActionKey == CoreActions.Eat).HasOwnScope && scopes.Single(x => x.ActionKey == CoreActions.Eat).Scope == BehaviorScope.Disabled,
            "full ActionId persisted scope is visible through existing built-in row projection");
        Console.WriteLine("DB Eat UI production source/Host (platform doubles) PASS");
    }
}
