using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using Verse;

using FerriteLib.UiKit.Kernel;

using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// VF1 r5 (PM entry review): the single confirmation window is a NEW host with a real lifecycle -
/// catalog creation, AbsorbInputAroundWindow so the background cannot change the selection while
/// the question is up, DT1 audit attach on HostAttached, and symmetric cleanup on every teardown.
/// The lane drives the PRODUCTION shell (the Remix lane's windowRect + WindowOnGUI precedent) and
/// answers through the flow's own command table - the same table the real buttons fire - then pins
/// the three answers: confirm runs the staged action EXACTLY ONCE, cancel runs nothing, and a
/// second question DROPS the first staged action.
/// </summary>
internal static class ConfirmWindowLaneTests
{
    public static void RunAll()
    {
        Step("confirm: the real shell opens, absorbs input, attaches its audit, and answers once", ConfirmPass);
        Step("cancel: the staged action never runs and the teardown is symmetric", CancelPass);
        Step("reopen: the second question drops the first staged action", ReopenPass);
        Console.WriteLine("ConfirmWindowLaneTests ALL PASS");
    }

    private static void ConfirmPass()
    {
        var stack = new WindowStack();
        int calls = 0;
        UsConfirmWindow? flow = UsConfirmWindow.Open("Delete table", "Delete RaceZ?", "Confirm", () => calls++, stack);
        Assert(flow != null, "Open returned a flow over the injected stack");

        UiWindowHost? shell = flow!.OpenedWindow;
        Assert(shell != null, "the catalog opened the confirmation window");
        Assert(shell!.absorbInputAroundWindow,
            "the confirmation absorbs input around itself: the background cannot change the selection mid-question");

        shell.windowRect = new Rect(200f, 200f, 420f, 180f);
        shell.WindowOnGUI(); // the first pass creates the page host and announces it (HostAttached)

        Assert(flow.DialogAudit != null,
            "DT1: HostAttached opened the dialog's OWN fit-audit scope");
        Assert(DevPanelDialogHostSet(expected: true),
            "DT1: the developer panel target names a live dialog host while the question is up");

        Assert(flow.DialogBindings.CanExecute("confirm-yes") && flow.DialogBindings.CanExecute("confirm-no"),
            "the dialog's two answer commands are registered on its own table");
        flow.DialogBindings.Invoke("confirm-yes");
        Assert(calls == 1, "confirm runs the staged action exactly once, got " + calls);
        Assert(flow.OpenedWindow == null, "the window closed after the answer");
        Assert(flow.DialogAudit == null, "the dialog's audit scope was disposed on teardown");
        Assert(DevPanelDialogHostSet(expected: false),
            "DT1: the developer panel target was cleared on teardown");

        flow.DialogBindings.Invoke("confirm-yes");
        Assert(calls == 1, "a second confirm answer never re-runs the action, got " + calls);
    }

    private static void CancelPass()
    {
        var stack = new WindowStack();
        int calls = 0;
        UsConfirmWindow? flow = UsConfirmWindow.Open("T", "M", "Yes", () => calls++, stack);
        flow!.OpenedWindow!.windowRect = new Rect(200f, 200f, 420f, 180f);
        flow.OpenedWindow!.WindowOnGUI();
        Assert(flow.DialogAudit != null, "the cancel pass attached the audit before answering");
        flow.DialogBindings.Invoke("confirm-no");
        Assert(calls == 0, "cancel never runs the staged action, got " + calls);
        Assert(flow.OpenedWindow == null && flow.DialogAudit == null,
            "cancel closes the window and disposes the audit scope");
    }

    private static void ReopenPass()
    {
        var stack = new WindowStack();
        int first = 0;
        int second = 0;
        UsConfirmWindow? a = UsConfirmWindow.Open("A", "a", "A", () => first++, stack);
        a!.OpenedWindow!.WindowOnGUI();
        UsConfirmWindow? b = UsConfirmWindow.Open("B", "b", "B", () => second++, stack);
        Assert(first == 0, "opening the second question did not run the first action");
        b!.OpenedWindow!.WindowOnGUI();
        b.DialogBindings.Invoke("confirm-yes");
        Assert(second == 1 && first == 0,
            "only the SECOND staged action survives the reopen, got first=" + first + " second=" + second);
    }

    /// <summary>UsDevPanelTargets is internal to the product assembly; the lane reads its DialogHost
    /// through the same reflection seam the DX1 shell step uses.</summary>
    private static bool DevPanelDialogHostSet(bool expected)
    {
        Type targets = typeof(UsConfirmWindow).Assembly
            .GetType("UniversalSqueaker.UI.Dev.UsDevPanelTargets", throwOnError: true)!;
        PropertyInfo? prop = targets.GetProperty("DialogHost",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);
        object? value = prop!.GetValue(null);
        return expected ? value != null : value == null;
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ConfirmWindowLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
