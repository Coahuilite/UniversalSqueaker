using System;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

internal static class Fl08MigrationTests
{
    private static readonly Rect View = new(0, 0, 1000, 1200);
    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); Console.WriteLine("FL08 " + reason); }
    private static Vector2 Center(Rect rect) => new(rect.x + rect.width / 2, rect.y + rect.height / 2);
    private static void Event(UiHost host, EventType type, Vector2 point) => Program.DrawWithEvent(host, View, type, point);
    private static UiLayoutSnapshot Arrange(UiHost host) => host.MeasureAndArrange(new Vector2(View.width, View.height));
    private static void Click(UiHost host, Vector2 point) { Event(host, EventType.MouseDown, point); Event(host, EventType.MouseUp, point); }
    public static void Run()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try {
            int subscriptionsBefore = UiDiagnosticHub.SubscriptionCount;
            var source = new RecordingSettingsSource { RichData = true };
            using var host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
            host.Bindings.Invoke("set-tab", "Tuning");
            var snapshot = Arrange(host); host.DrawChecked(View);
            Vector2 egg = Center(snapshot.RectById["basic-egg-check"]);
            Event(host, EventType.MouseDown, egg);
            Check(source.LastEasterEggs == null, "actual checkbox primary press does not write");
            Event(host, EventType.MouseUp, egg);
            Check(source.LastEasterEggs == false, "actual checkbox valid primary release writes inverse once");

            host.Bindings.Invoke("set-tab", "Packs");
            snapshot = Arrange(host); host.DrawChecked(View);
            Vector2 trigger = Center(snapshot.RectById["packs-race-filter"]);
            Event(host, EventType.MouseDown, trigger);
            Check(host.Session.OpenPopupId == null, "actual dropdown waits for release");
            Event(host, EventType.MouseUp, trigger); host.DrawChecked(View);
            Check(Fl08PublicObservation.Popup(host.Session, out var popup), "actual dropdown publishes its popup through public trace");
            Vector2 first = new(popup.Rect.x + 12, popup.Rect.y + 12);
            Event(host, EventType.MouseDown, first);
            Check(source.RaceFilterWriteCount == 0, "same-value option does not request on press");
            Event(host, EventType.MouseUp, first);
            Check(source.RaceFilterWriteCount == 1 && source.LastRaceFilter == "", "same-value option explicitly requests exactly once on release");

            Click(host, trigger); host.DrawChecked(View);
            Check(host.Session.OpenPopupId != null, "reopen the actual dropdown before outside gesture");
            Rect nav = Arrange(host).RectById["nav"];
            Vector2 overview = new(nav.x + 24, nav.y + 24);
            Event(host, EventType.MouseDown, overview);
            Check(host.Session.OpenPopupId == null && host.Bindings.Get<string>(UiBindings.ActiveTabKey) == "Packs", "outside primary press closes dropdown without invoking underlying navigation");
            Event(host, EventType.MouseUp, overview);
            Check(host.Bindings.Get<string>(UiBindings.ActiveTabKey) == "Packs", "consumed outside gesture cannot invoke underlying command on release");
            Click(host, overview);
            Check(host.Bindings.Get<string>(UiBindings.ActiveTabKey) == "Overview", "fresh gesture reaches the same underlying navigation target");

            host.Bindings.Invoke("set-tab", "Packs"); snapshot = Arrange(host); host.DrawChecked(View);
            Vector2 search = Center(snapshot.RectById["packs-keyword"]);
            Click(host, search);
            Type(host, "local");
            Check(source.LastSearchText == "local" && source.SearchWriteCount == 1, "existing search explicitly Live=true keeps its immediate business write");
            UiNode node = host.Session.GetNodeByElementId("packs-keyword")!;
            var ctx = host.CreateContext(View.width).WithNode(node);
            var editor = UiNative.Editor<string>(ctx, "search-text", x => x, Parse);
            var retainedEditor = editor;
            int identity = host.Session.Identity;
            host.SetScrollPosition("content-scroll", new Vector2(0, 20));
            var settings = new UniversalSqueakerSettings();
            UsKernelSettingsHost.ApplySwatchLive(settings, host, UsTheme.ChromeTheme, "biotech");
            Check(editor.Draft == "local" && ReferenceEquals(node, host.Session.GetNodeByElementId("packs-keyword")), "theme switch retains actual node and editor draft");
            host.SetDisplayDemand(false);
            source.SetSearchText("external"); host.Invalidate();
            Check(host.Session.IsActive && !host.DisplayRequired && editor.Draft == "local", "reliably hidden retained view keeps local draft without disposing its owner");
            host.SetDisplayDemand(true); host.DrawChecked(View);
            Check(host.Session.Identity == identity && ReferenceEquals(retainedEditor, UiNative.Editor<string>(ctx, "search-text", x => x, Parse))
                && editor.Draft == "local" && editor.Conflict && source.SearchWriteCount == 2,
                "show synchronizes current external model, retains conflicting draft and makes no automatic request");
            Click(host, search);
            Check(host.TryHandleCancel(), "actual search editor accepts local Cancel"); host.DrawChecked(View);
            Check(editor.Draft == "external" && source.SearchWriteCount == 2, "Cancel follows latest actual model without a compensating write");
            source.SearchTransform = value => value.Trim().ToLowerInvariant();
            Click(host, search); Type(host, " LOCAL ");
            Check(source.LastSearchRequest == " LOCAL " && source.LastSearchText == "local" && source.SearchWriteCount == 3,
                "text request and normalized business model stay distinct");
            Check(host.TryHandleAccept(), "text confirmation ends the live edit"); host.DrawChecked(View);
            Check(editor.Draft == "local" && source.SearchWriteCount == 3, "normal normalization displays actual model without another request");
            source.SearchTransform = null;

            host.Bindings.Invoke("set-tab", "Overview"); snapshot = Arrange(host); host.DrawChecked(View);
            Vector2 number = Center(snapshot.RectById["global-volume-number"]);
            Click(host, number);
            Check(host.Session.ActiveEditNode?.ElementId == "global-volume-number", "actual numeric field owns the edit before input injection");
            Type(host, "73");
            Check(source.GlobalVolumeWriteCount == 1 && source.LastGlobalVolume.HasValue && Math.Abs(source.LastGlobalVolume.Value - .73f) < .001f,
                "existing numeric field explicitly Live=true sends the qualified candidate once");
            Check(host.TryHandleAccept(), "numeric confirmation is handled by the active editor"); host.DrawChecked(View);
            var numberNode = host.Session.GetNodeByElementId("global-volume-number")!;
            var numeric = new UiNumericEdit(0, 100, "0");
            var numberEditor = UiNative.Editor<float>(host.CreateContext(View.width).WithNode(numberNode), "global-volume-percent", numeric.Format, numeric.Parse);
            Check(source.GlobalVolumeWriteCount == 1 && numberEditor.Draft == "60", "normal business refusal returns to actual numeric model without replaying the request");
            _ = host.Diagnostics;
            Check(UiDiagnosticHub.SubscriptionCount == subscriptionsBefore + 1, "this actual Host owns one explicit diagnostic subscription");
            host.Dispose();
            Check(!host.InputState.IsActive && !host.Session.IsActive, "closing the actual production Host releases this presentation owner");
            Check(UiDiagnosticHub.SubscriptionCount == subscriptionsBefore, "close releases the Host subscription without leaking a retained view");
            Console.WriteLine("FL08 actual production Host migration PASS (platform doubles, real native-event protocol)");
        } finally { Program.SetTranslatorResolver(null); }
    }
    private static bool Parse(string text, out string value) { value = text; return true; }
    private static void Type(UiHost host, string text)
    {
        var field = typeof(UiNative).GetField("TextFieldOverride", BindingFlags.NonPublic | BindingFlags.Static)!;
        var target = host.Session.ActiveEditNode;
        if (target == null) throw new InvalidOperationException("No focused native editor for the test input");
        bool sent = false;
        field.SetValue(null, new Func<Rect, string, string>((rect, current) => {
            if (sent || !ReferenceEquals(host.Session.ActiveNode, target)) return current;
            sent = true; return text;
        }));
        try { host.DrawChecked(View); } finally { field.SetValue(null, null); }
    }
}
