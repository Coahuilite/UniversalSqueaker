using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

internal static class ProjectionFrameExtensions
{
    // These unchanged projection cases paint once, then dispose their Host; interactive cases own Page.
    internal static void DrawChecked(this UiHost host, UiLayoutRect view)
    {
        using var execution = new VerseUiExecution();
        Event.current = new Event { type = EventType.Repaint, mousePosition = new Vector2(-1000, -1000) };
        host.DrawFrame(view, execution.CaptureFrame(), execution);
        var observation = host.Observe(new UiObservationBudget(512, 32));
        Program.Check(!observation.Truncated, "projection recovery observation complete");
        foreach (var node in observation.Nodes)
        foreach (var fact in node.Facts)
            if (fact.Name == "identity" && host.Session.GetNodeByElementId(fact.Value) is UiNode actual)
                Program.Check(!host.Session.IsTripped(actual), "projection actual widget not recovered " + fact.Value);
    }
}
