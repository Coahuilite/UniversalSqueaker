using System;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

internal static class DcSelectUiTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("DC Select UI: " + message); }
    internal static void Run()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" }) {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try {
                foreach (float width in new[] { 760f, 480f }) {
                    var source = new RecordingSettingsSource { RichData = true };
                    using var host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
                    host.Bindings.Invoke("set-tab", "Tuning");
                    var layout = host.MeasureAndArrange(new Vector2(width, 1000));
                    foreach (string name in new[] { "player", "awake", "not-downed" }) {
                        Check(layout.RectById.TryGetValue("basic-select-" + name + "-choice", out var rect) && rect.width >= 100 && rect.height >= 26,
                            language + "/" + width + " usable qualification control " + name);
                        Console.WriteLine("DC Select UI " + language + "/" + width + " " + name + "=" + rect);
                    }
                    int revision = host.Session.ContentRevision;
                    host.Bindings.Set("select-player-controlled", "False"); host.Bindings.Set("select-awake", "True"); host.Bindings.Set("select-not-downed", "False");
                    Check(source.SelectWrites["playerControlled"] == "False" && source.SelectWrites["awake"] == "True"
                        && source.SelectWrites["notDowned"] == "False" && host.Session.ContentRevision == revision + 3,
                        "three independent writes share the existing revision clock");
                    host.Bindings.Set("select-awake", "");
                    Check(host.Bindings.Get<string>("select-awake") == "" && host.Bindings.Get<string>("select-player-controlled") == "False"
                        && source.SelectRequireNotDowned == false, "Inherit clears only the selected qualification");
                    host.DrawChecked(new Rect(0, 0, width, 1000));
                }
            } finally { Program.SetTranslatorResolver(null); }
        }
        var settings = new UniversalSqueakerSettings(); var actual = new UsKernelSettingsSource(settings);
        actual.ViewState.TuningLayer = 0; actual.SetSelectParameter("playerControlled", "False");
        actual.ViewState.TuningLayer = 1; actual.ViewState.TuningRaceDefName = "SelectRace"; actual.SetSelectParameter("awake", "False");
        actual.ViewState.TuningLayer = 2; actual.ViewState.TuningXenotypeDefName = "LegalXeno"; actual.SetSelectParameter("notDowned", "False");
        Check(settings.actionTuning.Count == 3 && settings.actionTuning.All(x => x.actionKey == "us.core/Select")
            && settings.ReadSelectParameterOverrides().SelectRequirePlayerControlled == false
            && settings.ReadSelectParameterOverrides("SelectRace").SelectRequireAwake == false
            && settings.ReadSelectParameterOverrides("SelectRace", "LegalXeno").SelectRequireNotDowned == false,
            "actual production facade writes exact service/Race/Xeno presence and false values");
        actual.SetSelectParameter("notDowned", "");
        Check(!settings.ReadSelectParameterOverrides("SelectRace", "LegalXeno").SelectRequireNotDowned.HasValue
            && settings.ReadSelectParameterOverrides("SelectRace").SelectRequireAwake == false, "actual source inheritance preserves neighboring layer");
        actual.ViewState.TuningLayer = 1; actual.ViewState.TuningRaceDefName = "";
        int count = settings.actionTuning.Count; actual.SetSelectParameter("playerControlled", "True");
        Check(settings.actionTuning.Count == count && settings.ReadSelectParameterOverrides().SelectRequirePlayerControlled == false,
            "incomplete Race target does not silently write the Pawn profile");
        Console.WriteLine("DC Select UI production source/Host with explicit platform doubles PASS");
    }
}
