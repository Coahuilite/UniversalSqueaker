using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Popup coordinate contract: a dropdown trigger drawn inside a scrolled container stores its
/// popup anchor in the Host's final usable window space (the engine translates draw rects by the
/// scroll offset), and the popup pass draws and hit-tests rows in that same stored space. A
/// content-local row position must not hit while the popup is open under a scroll offset.
/// </summary>
internal static class KernelPopupTests
{
    private const string Scope = "popup-test";
    private const string HeightWidgetKind = "test/height";
    private static readonly string Xml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Scroll Id=\"scroll\" Height=\"200\">"
        + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
        + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
        + "<Widget Id=\"tail\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
        + "</Scroll>"
        + "</UiPage>";

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Scroll dropdown anchor/popup share Host window space", VerifyScrollDropdownWindowSpace);
        return failures;
    }

    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    private static void VerifyScrollDropdownWindowSpace()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightWidgetKind, () => new HeightWidget());

        UiLayoutManifest manifest = UiLayoutManifest.Parse(Xml);
        var bindings = new UiBindings();
        string current = "x";
        bindings.BindValue("dropdown", () => current, value => current = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new(Scope, manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

        // Content is 100 + 28 + 100 = 228 tall; viewport is 200, so the scroll offset clamps to 28.
        // Vertical overflow reserves the 16px scrollbar width, leaving a 284px trigger.
        host.Session.SetScrollPosition("scroll", new Vector2(0f, 20f));

        // The Host viewport starts at (20, 30). The trigger draws at content-local
        // (0, 100, 284, 28), so its window-space rect is (20, 30 + 100 - 20, 284, 28).
        // A non-zero viewport is required here: a zero origin would hide double-offset bugs.
        Rect triggerContentLocal = new(0f, 100f, 284f, 28f);
        Rect expectedAnchor = new(20f, 110f, 284f, 28f);

        try
        {
            // Frame 1: click the trigger (content-local rect) -> popup opens anchored in window space.
            UiNative.ButtonOverride = rect => SameRect(rect, triggerContentLocal);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));

            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Dropdown did not open from the scrolled trigger");
            }

            if (!host.Session.OpenPopupAnchor.HasValue || !SameRect(host.Session.OpenPopupAnchor.Value, expectedAnchor))
            {
                throw new Exception("Popup anchor is not the Host window-space trigger rect: "
                    + (host.Session.OpenPopupAnchor.HasValue ? host.Session.OpenPopupAnchor.Value.ToString() : "<null>")
                    + " (expected " + expectedAnchor + ")");
            }

            // Frame 2: click the popup row at its window-space position.
            // Row 1 = (20, 138 + 24, 284, 24) = (20, 162, 284, 24); selecting it writes "y".
            Rect windowRow1 = new(20f, 162f, 284f, 24f);
            UiNative.ButtonOverride = rect => SameRect(rect, windowRow1);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));

            if (host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Popup did not close after a window-space row selection");
            }

            if (!string.Equals(current, "y", StringComparison.Ordinal))
            {
                throw new Exception("Window-space row selection did not write the binding (current='" + current + "')");
            }

            // Frame 3: reopen the popup.
            UiNative.ButtonOverride = rect => SameRect(rect, triggerContentLocal);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));
            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Dropdown did not reopen");
            }

            // Frame 4: a click at the content-local row position (0, 152, 284, 24) must NOT hit:
            // the popup lives in window space under the scroll offset.
            Rect contentSpaceRow1 = new(0f, 152f, 284f, 24f);
            UiNative.ButtonOverride = rect => SameRect(rect, contentSpaceRow1);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));

            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Content-local row position wrongly hit the window-space popup");
            }

            // Frame 5: the window-space row still selects and closes.
            UiNative.ButtonOverride = rect => SameRect(rect, windowRow1);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));
            if (host.Session.IsPopupOpen("dropdown") || !string.Equals(current, "y", StringComparison.Ordinal))
            {
                throw new Exception("Window-space row hit failed after the content-space miss");
            }
        }
        finally
        {
            UiNative.ButtonOverride = null;
        }
    }

    private static bool SameRect(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) < 0.01f
            && Math.Abs(a.y - b.y) < 0.01f
            && Math.Abs(a.width - b.width) < 0.01f
            && Math.Abs(a.height - b.height) < 0.01f;
    }

    private sealed class HeightWidget : IUiWidget
    {
        public string Kind => HeightWidgetKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 10f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }
    }
}
