using System;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

internal static class Fl08ReadOnlyPatchTests
{
    public static void Run()
    {
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); Console.WriteLine("FL-A002 " + label); }
        using var host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
        var view = new Rect(0, 0, 1000, 1200);
        void Pump() => Program.DrawWithEvent(host, view, EventType.Repaint, Vector2.zero);
        Pump();
        var geometry = host.MeasureAndArrange(new Vector2(view.width, view.height));
        var rect = geometry.RectById["global-volume-slider"];
        var field = host.ElementProperties("global-volume-slider");
        var parent = host.ElementProperties("page-root");
        var metrics = field.ResolvedMetrics;
        var colours = field.ResolvedColours;
        Check(rect.width > 0 && rect.height > 0 && geometry.VisibleIds.Count > 0 && metrics.Count == 5 && colours.Count == 30,
            "ordinary existing US Host property/geometry reads remain available");
        float original = metrics["RowHeight"].Value;
        Console.WriteLine("FL-A002 geometry=" + rect.x + "," + rect.y + "," + rect.width + "," + rect.height + " rowHeight=" + original);
        field.Metric("RowHeight").Set(60); Pump();
        Check(metrics["RowHeight"].Value == 60 && metrics["RowHeight"].Origin == UiPropertyOrigin.Instance,
            "normal explicit override reads through the held resolved view");
        field.Metric("RowHeight").Set(64); parent.Metric("RowHeight").Set(52); Pump();
        Check(metrics["RowHeight"].Value == 64 && ReferenceEquals(metrics, field.ResolvedMetrics) && ReferenceEquals(colours, field.ResolvedColours),
            "normal update preserves view identity and local precedence over current ancestor");
        field.Metric("RowHeight").Remove(); parent.Metric("RowHeight").Set(56); Pump();
        Check(metrics["RowHeight"].Value == 56 && metrics["RowHeight"].Origin == UiPropertyOrigin.Ancestor,
            "remove reveals current ancestor56 rather than a view-creation snapshot");
        parent.Metric("RowHeight").Remove(); Pump();
        Check(metrics["RowHeight"].Value == original && host.MeasureAndArrange(new Vector2(view.width, view.height)).RectById["global-volume-slider"].height == rect.height,
            "ordinary declaration removal returns to original source and explicit widget geometry");
        Console.WriteLine("FL-A002 minimal fixed consumption PASS (actual US Host, platform doubles)");
    }
}
