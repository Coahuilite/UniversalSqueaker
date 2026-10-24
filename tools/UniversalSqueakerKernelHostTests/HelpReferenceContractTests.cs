using System;
using UnityEngine;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

internal static class HelpReferenceContractTests
{
    private const string General = "us/global-volume/slider", Specific = "us/mode-row/remix";
    private sealed class Probe : IUiWidget
    {
        public string Kind => "test/help-reference";
        internal bool PublishGeneral = true;
        internal string OptionHelpRef = "";
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindings bindings, string elementPath) { }
        public float Measure(UiWidgetContext context) => 80;
        public void Draw(Rect rect, UiWidgetContext context)
        {
            if (PublishGeneral) UsKernelDraw.HelpHover(rect, context, General);
            // A row without dedicated help must not erase its control's general explanation.
            UsKernelDraw.HelpHover(rect, context, OptionHelpRef);
        }
    }
    internal static void Run()
    {
        var probe = new Probe();
        UiWidgetRegistry.Register(UsKernelWidgetRegistrar.Scope, probe.Kind, () => probe, new[] {"Id","Kind"});
        var page = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\"><Widget Id=\"probe\" Kind=\"test/help-reference\" /></UiPage>");
        using var host = new UiHost(UsKernelWidgetRegistrar.Scope, page, new UiBindings(), UsTheme.Surface(), new Program.StubMetrics(), new Program.StubTranslation());
        host.Session.HoverGraceFrames = 15;
        var viewport = new Rect(0,0,300,150); var inside = new Vector2(20,20);var outside = new Vector2(-1000,-1000);
        Program.DrawWithPointer(host, viewport, inside);
        if(host.Session.IsTripped(host.Session.GetNodeByElementId("probe")!) || host.Session.HoverClaim != General)
            throw new Exception("empty option reference erased the actual production helper's general help: '"+host.Session.HoverClaim+"'");
        probe.OptionHelpRef = null!;
        Program.DrawWithPointer(host, viewport, inside);
        if(host.Session.IsTripped(host.Session.GetNodeByElementId("probe")!) || host.Session.HoverClaim != General)
            throw new Exception("missing option reference threw or erased general help");
        probe.OptionHelpRef = Specific;
        Program.DrawWithPointer(host, viewport, inside);
        if(host.Session.HoverClaim != Specific)throw new Exception("explicit topic did not replace general reference");
        probe.PublishGeneral = false;probe.OptionHelpRef = "";
        Program.DrawWithPointer(host, viewport, outside); // Fixed fc010bd clears the next pass for fresh publication.
        for(int gap=1;gap<=15;gap++)
        {
            Program.DrawWithPointer(host, viewport, outside);
            if(host.Session.HoverClaim != Specific)throw new Exception("actual completed help grace lost topic at gap="+gap);
        }
        Program.DrawWithPointer(host, viewport, outside);
        if(host.Session.HoverClaim != null)throw new Exception("completed fixed grace did not release the topic");
        Console.WriteLine("A002 actual HelpHover empty/general/explicit reference + 15 completed grace passes PASS");
    }
}
