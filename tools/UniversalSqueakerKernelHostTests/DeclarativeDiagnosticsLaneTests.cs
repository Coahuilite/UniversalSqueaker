using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// T3-2 lane: the Overview diagnostics card that was dissolved out of the US-owned us/diagnostics
/// composite and rebuilt as a manifest subtree over the library own atoms.
///
/// <para>
/// WHY THIS IS A STRUCTURAL CHANGE, NOT AN APPEARANCE ONE (the four rulers). The card middle row is a
/// three-way choice over the typed SqueakDevLoggingMode, and it used to be written through a VALUE binding.
/// No atom in the vocabulary writes a typed N-way choice that way: input/mode-row reads and writes a STRING
/// value binding and takes its option values as manifest LITERALS (Value1..8), so adopting it would push a
/// machine-token parse into the host and put tokens in the manifest; us/mode-row is hard-typed to
/// SqueakVoicePackMode over a four-entry static table. The shape that does exist is S4-3b: three
/// input/button atoms, each carrying its own PayloadKey (G2 string contract) and its own SelectedKey (B1
/// per-element answer), over ONE string action whose enum parse stays on the host. Structure and the write
/// channel change; the flat surface and the square toggle are the page existing rules following along.
/// </para>
///
/// <para>
/// MUTATION LEDGER:
///  - RetiredKindIsGone - re-adding the Registrar line reddens the kind-set pin in UiSourceInvariantTests
///    (registrar != manifest) and this step own registry read.
///  - TheCardIsADeclaredSection - restoring the composite manifest element reddens the declared shape
///    assertion; REMOVING one of the four declared us/diagnostics/* HelpKeys reddens the claim assertion
///    below (the catalog item would be claimed by nothing).
///  - EachLoggingButtonRoutesItsMode - giving two buttons the same PayloadKey, or binding them to another
///    action, reddens the per-button routing assertion (each press must reach the setter with the mode the
///    button NAMES).
///  - TheLocalizeRowIsTheDeclaredToggle - pointing the toggle at another binding reddens the write-through
///    assertion.
/// </para>
///
/// <para>
/// NOT CLAIMED HERE: the player-visible look. The flat surface, the three-buttons-instead-of-a-segmented-row
/// shape, the square toggle and the row rhythm are UNVERIFIED VISIBLE DELTAS, listed as such in the S4/S6
/// real-screen list in TODO.md and MEMORY.md - this lane proves the declarations, the routing and the
/// geometry, never the appearance.
/// </para>
/// </summary>
internal static class DeclarativeDiagnosticsLaneTests
{
    private const string Scope = "coahuilite.universalsqueaker";
    private const string RetiredKind = "us/diagnostics";
    private const float PageWidth = 1024f;
    private const float PageHeight = 900f;

    /// <summary>The three logging buttons: element id, the SelectedKey it must answer for, the PayloadKey it
    /// must carry, the item HelpKey it must declare and the mode its payload names. The PAIRING is the
    /// contract - two buttons sharing a key paint together, and a button whose payload names another mode
    /// writes the wrong one while the page still looks right.</summary>
    private static readonly (string Id, string Selected, string Payload, string Help, SqueakDevLoggingMode Mode)[] Options =
    {
        ("diagnostics-logging-auto", "dev-logging-auto", "dev-logging-auto-value", "us/diagnostics/logging-auto", SqueakDevLoggingMode.Auto),
        ("diagnostics-logging-enabled", "dev-logging-enabled", "dev-logging-enabled-value", "us/diagnostics/logging-enabled", SqueakDevLoggingMode.Enabled),
        ("diagnostics-logging-disabled", "dev-logging-disabled", "dev-logging-disabled-value", "us/diagnostics/logging-disabled", SqueakDevLoggingMode.Disabled),
    };

    /// <summary>Every us/diagnostics item key the catalog owns. The claim lane in the UI-logic project reads
    /// manifest HelpKeys as claim sources, so these four must stay DECLARED on the card elements; a missing
    /// one leaves a catalog item claimed by nothing.</summary>
    private static readonly string[] ItemHelpKeys =
    {
        "us/diagnostics/logging-auto", "us/diagnostics/logging-enabled",
        "us/diagnostics/logging-disabled", "us/diagnostics/localize-debug"
    };

    public static int RunAll()
    {
        Step("the dissolved us/diagnostics kind is retired from the registry and the manifest", RetiredKindIsGone);
        Step("the diagnostics card is a declared Section over atoms with its four help claims", TheCardIsADeclaredSection);
        Step("each logging button carries its own payload and routes the mode it names", EachLoggingButtonRoutesItsMode);
        Step("the localize row is the page declared toggle over its own binding", TheLocalizeRowIsTheDeclaredToggle);
        Console.WriteLine("DeclarativeDiagnosticsLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1: the retirement is real
    // ---------------------------------------------------------------------------------------------

    private static void RetiredKindIsGone()
    {
        UsKernelWidgetRegistrar.EnsureRegistered();
        IReadOnlyCollection<string> kinds = UiWidgetRegistry.KnownKinds(Scope);
        Assert(!kinds.Contains(RetiredKind),
            "the dissolved composite '" + RetiredKind + "' must not be a registered kind any more: a"
            + " registration that outlives its manifest line is the half-retired state this batch prevents");

        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });
        var live = new HashSet<string>(StringComparer.Ordinal);
        CollectKinds(host.Manifest.Roots, live);
        Assert(!live.Contains(RetiredKind),
            "the live manifest must not contain the retired kind '" + RetiredKind + "' anywhere");
    }
    // ---------------------------------------------------------------------------------------------
    // Step 2: the live manifest is declarative, and it keeps every help claim
    // ---------------------------------------------------------------------------------------------

    private static void TheCardIsADeclaredSection()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        UiElementSpec? card = FindById(host.Manifest.Roots, "diagnostics");
        Assert(card != null, "the shipped manifest must carry the card diagnostics");
        Assert(card!.Kind == "Section",
            "diagnostics must be the engine Section container - the composite kind that used to own this"
            + " card is retired - got " + card.Kind);
        Assert(card.TryGetAttribute("Tab", out string tab) && tab == "Overview",
            "diagnostics must stay gated by the Overview workspace, got '" + tab + "'");

        UiElementSpec? header = FindById(card.Children, "diagnostics-header");
        Assert(header != null && header.Kind == "us/section-header",
            "the card title must be the us/section-header surface, got "
            + (header == null ? "(missing)" : header.Kind));
        Assert(header!.TryGetAttribute("HelpKey", out string headerHelp) && headerHelp == "us/diagnostics",
            "the header must keep the card help topic us/diagnostics, got '" + headerHelp + "'");

        foreach (var option in Options)
        {
            UiElementSpec? button = FindById(card.Children, option.Id);
            Assert(button != null, "the manifest must declare " + option.Id);
            Assert(button!.Kind == "input/button",
                option.Id + " must be the input/button atom, got " + button.Kind);
            Assert(button.TryGetAttribute("SelectedKey", out string selected) && selected == option.Selected,
                option.Id + " must answer for ITSELF: expected SelectedKey='" + option.Selected
                + "', got '" + selected + "'. Two buttons sharing one selected key paint together.");
            Assert(button.TryGetAttribute("PayloadKey", out string payload) && payload == option.Payload,
                option.Id + " must carry ITS OWN payload: expected PayloadKey='" + option.Payload
                + "', got '" + payload + "'");
            Assert(button.TryGetAttribute("ActionBind", out string action) && action == "set-dev-logging",
                option.Id + " must fire the one string action set-dev-logging, got '" + action + "'");
            Assert(button.TryGetAttribute("HelpKey", out string help) && help == option.Help,
                option.Id + " must declare its own help item '" + option.Help + "', got '" + help + "'");
        }

        // The four catalog items must stay CLAIMED. Removing any one of these declarations leaves a catalog
        // item claimed by nothing - the UI-logic claim lane reads manifest HelpKeys, so this is the negative
        // control that keeps the claim source alive after the composite C# literals are gone.
        var declaredHelp = new HashSet<string>(StringComparer.Ordinal);
        CollectAttribute(host.Manifest.Roots, "HelpKey", declaredHelp);
        foreach (string key in ItemHelpKeys)
        {
            Assert(declaredHelp.Contains(key),
                "the card must still declare the help claim '" + key + "': the catalog owns that item, and a"
                + " claim that moved out of the retired widget C# must land in the manifest, not vanish");
        }

        UiElementSpec? localizeLabel = FindById(card.Children, "diagnostics-localize-label");
        Assert(localizeLabel != null && localizeLabel.Kind == "text/wrapped",
            "the localize row must carry a text/wrapped label, got "
            + (localizeLabel == null ? "(missing)" : localizeLabel.Kind));
        UiElementSpec? toggle = FindById(card.Children, "diagnostics-localize-check");
        Assert(toggle != null && toggle.Kind == "us/square-toggle",
            "the localize control must be the us/square-toggle surface, got "
            + (toggle == null ? "(missing)" : toggle.Kind));
        Assert(toggle!.TryGetAttribute("Bind", out string bind) && bind == "localize-debug-menu",
            "the toggle must write localize-debug-menu, got '" + bind + "'");
        Assert(toggle.TryGetAttribute("SelectedKey", out string toggleSelected) && toggleSelected == "localize-debug-menu",
            "the toggle must name the SAME bool in SelectedKey, got '" + toggleSelected + "'");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: every button routes the mode it NAMES
    // ---------------------------------------------------------------------------------------------

    private static void EachLoggingButtonRoutesItsMode()
    {
        var source = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            UiLayoutSnapshot snapshot = Arrange(host);
            // The recorded UiNative rects live in the content SCROLL's local space while RectById is in page
            // space (the documented cross-space trap). Translate the row before comparing x positions, or the
            // filter below silently compares two different coordinate systems.
            Rect row = Local(snapshot, "diagnostics-logging-row");
            float cellWidth = (row.width - 4f * 2f) / 3f;

            var recorded = new List<Rect>();
            DrawWithButtons(host, rect => { recorded.Add(rect); return false; });
            List<Rect> buttons = recorded
                .Where(r => Math.Abs(r.width - cellWidth) <= 0.51f && r.xMax <= row.xMax + 0.51f)
                .OrderBy(r => r.x)
                .ToList();
            Assert(buttons.Count == 3,
                "the logging row must draw exactly three declared buttons in the row own shape, got "
                + buttons.Count + " [" + string.Join(" ", buttons.Select(Describe)) + "]");

            // T17/L3: the presses below are matched by RECT IDENTITY and the three buttons are told apart by
            // their INDEX in x order. If the layout ever degenerated so that two of them drew the SAME rect,
            // index 0 would match the first press by accident and the routing assertion would pass for the
            // wrong button - the "several rows measured to one box" family this project already recorded.
            // Strictly increasing, mutually distinct x is the precondition that makes the index meaningful.
            for (int i = 1; i < buttons.Count; i++)
            {
                Assert(buttons[i].x > buttons[i - 1].x,
                    "the three logging buttons must draw at strictly increasing x, or index order cannot"
                    + " identify them: button " + i + " at " + Describe(buttons[i]) + " follows "
                    + Describe(buttons[i - 1]));
            }

            for (int index = 0; index < buttons.Count; index++)
            {
                source.LastDevLoggingMode = null;
                DrawWithButtons(host, rect => RectMatches(rect, buttons[index]));
                SqueakDevLoggingMode? received = source.LastDevLoggingMode;
                Assert(received.HasValue,
                    "pressing logging button " + index + " must reach the business setter at all");
                Assert(received!.Value == Options[index].Mode,
                    "pressing logging button " + index + " must select the mode that button NAMES ("
                    + Options[index].Mode + "), got " + received.Value
                    + " - each button carries its own PayloadKey for exactly this reason");
            }

            var trueKeys = new List<string>();
            foreach (var option in Options)
            {
                if (host.Bindings.Get<bool>(option.Selected)) trueKeys.Add(option.Selected);
            }

            Assert(trueKeys.Count == 1,
                "exactly ONE logging button may answer selected, got " + trueKeys.Count + " ["
                + string.Join(",", trueKeys) + "]");
            Assert(host.Bindings.Get<string>(Options[1].Payload) == nameof(SqueakDevLoggingMode.Enabled),
                "the highlighted button must be the mode the model holds (the fixture holds Enabled)");
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 4: the localize row writes through its own binding
    // ---------------------------------------------------------------------------------------------

    private static void TheLocalizeRowIsTheDeclaredToggle()
    {
        var source = new RecordingSettingsSource { RichData = true };
        using UiHost host = UsKernelSettingsHost.Create(source, new Program.StubMetrics());
        host.Bindings.Invoke("set-tab", "Overview");

        // The declared binding is the write channel (the atom writes the inverse of the bool it read), and
        // the fixture rich view holds true, so the write below must land as false.
        source.LastLocalizeDebugActions = null;
        host.Bindings.Set("localize-debug-menu", false);
        Assert(source.LastLocalizeDebugActions == false,
            "writing the localize binding must reach the business setter, got "
            + (source.LastLocalizeDebugActions?.ToString() ?? "(null)"));

        // And the drawn control is the toggle: pressing its band writes through the same binding. The band
        // MUST be translated into the space the recorded rects live in - the first cut compared the page-space
        // RectById rect against a content-local drawn rect and matched nothing, which is the cross-space trap
        // this suite already paid for once (and a press lane that matches nothing asserts nothing).
        UiLayoutSnapshot snapshot = Arrange(host);
        Rect band = Local(snapshot, "diagnostics-localize-check");
        source.LastLocalizeDebugActions = null;
        DrawWithButtons(host, rect => RectMatches(rect, band));
        Assert(source.LastLocalizeDebugActions.HasValue,
            "pressing the drawn toggle band must write through its binding (the band is the hit surface)");
    }

    // ---------------------------------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------------------------------

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        return host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
    }

    /// <summary>The manifest element whose viewport defines the coordinate space the recorded UiNative
    /// rects live in. Named rather than repeated as a literal, so the two places that must agree (this
    /// helper and the arrange) cannot drift apart silently.</summary>
    private const string ScrollViewportId = "content-scroll";

    /// <summary>An arranged element's rect in the coordinate space the recorded UiNative rects live in: the
    /// content scroll's local space (page rect minus the scroll viewport origin). Comparing the two spaces
    /// directly is the documented trap - the rects look plausible and match nothing.
    /// <para>
    /// PRECONDITION, stated because a future edit can break it silently: the element must be a DIRECT
    /// descendant of the scroll's content - no further scoped container (a nested Clip, another Scroll, a
    /// Group with its own origin) may sit between ScrollViewportId and the target. The engine subtracts
    /// exactly one scroll position and ToDrawRect does NOT subtract scrollPosition, so an extra scoped parent
    /// would need its own translation while this helper kept returning a plausible rect that matches nothing.
    /// </para>
    /// </summary>
    private static Rect Local(UiLayoutSnapshot snapshot, string id)
    {
        Assert(snapshot.RectById.TryGetValue(id, out Rect page),
            "the arranged snapshot must carry '" + id + "'");
        Assert(snapshot.Viewports.TryGetValue(ScrollViewportId, out Rect viewport),
            "the arranged snapshot must carry the '" + ScrollViewportId + "' viewport: the recorded UiNative"
            + " rects live in that space, and without it this helper has no translation to apply");
        return new Rect(page.x - viewport.x, page.y - viewport.y, page.width, page.height);
    }

    private static void DrawWithButtons(UiHost host, Func<Rect, bool> click)
    {
        FieldInfo? field = typeof(UiNative).GetField("ButtonOverride",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert(field != null && field.FieldType == typeof(Func<Rect, bool>),
            "REFLECTION BLOCKER: UiNative.ButtonOverride is not the expected seam on net472");
        try
        {
            field!.SetValue(null, click);
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        }
        finally
        {
            field!.SetValue(null, null);
        }
    }

    private static bool RectMatches(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) <= 0.01f && Math.Abs(a.y - b.y) <= 0.01f
            && Math.Abs(a.width - b.width) <= 0.01f && Math.Abs(a.height - b.height) <= 0.01f;
    }

    private static string Describe(Rect r)
    {
        return "(" + r.x.ToString("0.##") + "," + r.y.ToString("0.##") + " "
            + r.width.ToString("0.##") + "x" + r.height.ToString("0.##") + ")";
    }

    private static UiElementSpec? FindById(IReadOnlyList<UiElementSpec> elements, string id)
    {
        foreach (UiElementSpec element in elements)
        {
            if (string.Equals(element.Id, id, StringComparison.Ordinal)) return element;
            UiElementSpec? nested = FindById(element.Children, id);
            if (nested != null) return nested;
        }

        return null;
    }

    private static void CollectKinds(IReadOnlyList<UiElementSpec> elements, HashSet<string> kinds)
    {
        foreach (UiElementSpec element in elements)
        {
            kinds.Add(element.Kind);
            CollectKinds(element.Children, kinds);
        }
    }

    private static void CollectAttribute(IReadOnlyList<UiElementSpec> elements, string name, HashSet<string> values)
    {
        foreach (UiElementSpec element in elements)
        {
            if (element.TryGetAttribute(name, out string value) && value.Length > 0) values.Add(value);
            CollectAttribute(element.Children, name, values);
        }
    }


    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
