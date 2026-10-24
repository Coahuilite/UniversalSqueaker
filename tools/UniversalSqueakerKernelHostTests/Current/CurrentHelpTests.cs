using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
using Verse;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

internal static class CurrentHelpTests
{
    private const string General = "us/global-volume/slider", Specific = "us/mode-row/remix";
    private enum Target { First, Second }
    private sealed class Probe : IUiWidget
    {
        internal bool PublishGeneral = true;
        internal string OptionHelpRef = "";
        internal bool Dropdown;
        internal Target Selected;
        internal UiLayoutRect Trigger;
        internal readonly UiChoice<Target>[] Options = {
            new("Same label", Target.First), new("Same label", Target.Second, Specific)
        };
        public string Kind => "test/us-fixed-help";
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindingAccess bindings, string elementPath) { }
        public float Measure(UiWidgetContext context) => 100;
        public void Draw(UiLayoutRect rect, UiWidgetContext context)
        {
            if (PublishGeneral) UsKernelDraw.HelpHover(rect, context, General);
            UsKernelDraw.HelpHover(rect, context, OptionHelpRef);
            if (Dropdown)
            {
                Trigger = new UiLayoutRect(rect.X + 10, rect.Y + 10, 160, 28);
                UsKernelDraw.Dropdown(Trigger, "typed-popup", context, Selected, Options, value => Selected = value);
            }
        }
    }
    internal static void Run()
    {
        var probe = new Probe();
        UiWidgetRegistry.Define(new UiWidgetDefinition(Program.Scope, probe.Kind, () => probe,
            new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"))));
        UsKernelWidgetRegistrar.EnsureRegistered();
        var bindings = new UiBindings(); bindings.BindReadOnly("help-section-ref", () => "us/mode-row");
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\"><Column Id=\"root\"><Widget Id=\"probe\" Kind=\"test/us-fixed-help\"/><Widget Id=\"help\" Kind=\"us/help-panel\"/></Column></UiPage>");
        using var page = new Program.Page(new UiHost(Program.Scope, manifest, bindings, UsTheme.Surface(), Program.Metrics, new Program.StubTranslation()));
        page.Host.Session.HelpGracePasses = 15;
        var viewport = new UiLayoutRect(0, 0, 700, 900);
        var inside = new UiLayoutPoint(20, 20); var outside = new UiLayoutPoint(-1000, -1000);
        page.Draw(viewport, EventType.Repaint, inside);
        Program.Check(page.Host.Session.CurrentHelpRef == General, "empty choice ref does not erase general help");
        Program.Check(page.Host.Session.CurrentHelpSource == page.Host.Session.GetNodeByElementId("probe")!.Id, "CurrentHelpSource is the actual publisher node");
        probe.OptionHelpRef = null!; page.Draw(viewport, EventType.Repaint, inside);
        Program.Check(page.Host.Session.CurrentHelpRef == General, "omitted option ref does not publish or throw");
        probe.OptionHelpRef = Specific; page.Draw(viewport, EventType.Repaint, inside);
        Program.Check(page.Host.Session.CurrentHelpRef == Specific, "later explicit option publication replaces general");
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            var table = Program.ReadKeyedTable(language); Program.SetTranslatorResolver(table);
            // Parallel native recording tables share one index; clear them together.
            foreach (string field in new[] { "LabelTexts", "LabelRects", "LabelColors" })
                ((IList)typeof(Widgets).GetField(field)!.GetValue(null)!).Clear();
            page.Draw(viewport, EventType.Repaint, inside);
            var texts = ((IEnumerable)typeof(Widgets).GetField("LabelTexts")!.GetValue(null)!).Cast<string>().ToArray();
            Program.Check(texts.Contains(table["US.Help.ModeRow.Remix.Text"]), language + " actual unified panel draws translated explicit topic with no sink");
            Program.Check(!bindings.IsWritable("HoveredOptionHelpOutputBind"), "panel has no optional help output registration");
        }
        Program.SetTranslatorResolver(null);
        probe.PublishGeneral = false; probe.OptionHelpRef = "";
        page.Draw(viewport, EventType.Repaint, outside); // Original first-gap reset.
        for (int pass = 1; pass <= 15; pass++)
        {
            page.Draw(viewport, EventType.Repaint, outside);
            Program.Check(page.Host.Session.CurrentHelpRef == Specific && page.Host.Session.CurrentHelpSource == page.Host.Session.GetNodeByElementId("probe")!.Id,
                "same topic/source retained pass " + pass);
        }
        page.Draw(viewport, EventType.Repaint, outside);
        Program.Check(string.IsNullOrEmpty(page.Host.Session.CurrentHelpRef), "expiry after first gap plus15 retained passes");
        probe.Dropdown = true; probe.PublishGeneral = true;
        page.Draw(viewport, EventType.Repaint, inside);
        page.Draw(viewport, EventType.MouseDown, probe.Trigger.Center);
        page.Draw(viewport, EventType.MouseUp, probe.Trigger.Center);
        Program.Check(page.Host.Session.IsPopupOpen("typed-popup"), "actual US typed dropdown opens through native press/release");
        var popup = UiPopup.RectFor(page.Host.Session.OpenPopupAnchor!.Value, 2, viewport);
        var first = new UiLayoutPoint(popup.X + 12, popup.Y + 12);
        page.Draw(viewport, EventType.Repaint, first);
        Program.Check(page.Host.Session.CurrentHelpRef == General, "empty typed option ref never guesses label, value or index");
        var second = new UiLayoutPoint(popup.X + 12, popup.Y + UiPopup.OptionHeight + 12);
        page.Draw(viewport, EventType.Repaint, second);
        Program.Check(page.Host.Session.CurrentHelpRef == Specific, "typed option keeps explicit topic through popup");
        page.Draw(viewport, EventType.MouseDown, second); page.Draw(viewport, EventType.MouseUp, second);
        Program.Check(probe.Selected == Target.Second && !page.Host.Session.IsPopupOpen("typed-popup"), "equal labels do not collapse actual typed targets");
    }
}
