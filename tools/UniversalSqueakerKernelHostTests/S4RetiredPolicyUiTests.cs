using System;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

internal static class S4RetiredPolicyUiTests
{
    internal static void Run()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            var table = Program.ReadKeyedTable(language);
            Program.SetTranslatorResolver(table);
            try
            {
                foreach (float width in new[] { 1024f, 736f, 480f, 320f })
                {
                    var source = new RecordingSettingsSource { RichData = true };
                    using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
                    host.Bindings.Invoke("set-tab", "Tuning");
                    var snapshot = host.MeasureAndArrange(new Vector2(width, 720));
                    host.DrawChecked(new Rect(0, 0, width, 720));
                    foreach (string retired in new[] { "basic-cooldown-row", "basic-population-row", "timing-interval-row",
                        "timing-interval-slider", "timing-multiplier-row", "timing-restore" })
                    {
                        Require(!snapshot.RectById.ContainsKey(retired), "retired policy control remains arranged: " + retired);
                    }
                    Require(snapshot.RectById.TryGetValue("kernel-policy-note", out var note) && note.height > 0,
                        "selection status must be visible and measured");
                    Require(host.Bindings.TryGet("kernel-policy-note", out string text) && text == table["US.Kernel.PolicyRequired"],
                        "the current selection requirement must resolve through the actual language table");
                    Require(snapshot.RectById.ContainsKey("basic-talking-check") && snapshot.RectById.ContainsKey("basic-baby-check")
                        && snapshot.RectById.ContainsKey("basic-eat-check"), "effective permission and occurrence controls must remain present");
                    host.Bindings.Set("tuning-area", 1);
                    var mood = host.MeasureAndArrange(new Vector2(width, 720));
                    host.DrawChecked(new Rect(0, 0, width, 720));
                    Require(mood.RectById.TryGetValue("scope-tree", out var tree) && tree.height > 0 && tree.width > 0,
                        "retired editor notice and navigation must retain a measured body");
                    Console.WriteLine("[S4-policy-ui] " + language + " width=" + width + " notice=" + note.height + " retired-editor=" + tree.height);
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }
    }

    private static void Require(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
