using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>B1: layered interaction routing and LayoutEngine integration.</summary>
internal static class InteractionTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyLayerHit();
        VerifyTopActionBeatsRow();
        VerifySameLayerOrder();
        VerifyProtectWins();
        VerifyNoHit();
        VerifyHoverWithoutClickDoesNotFire();
        VerifyCleanup();
        VerifyScrollTransform();
        VerifyToPageSpace();
        VerifyTryGetCurrentScrollView();
        VerifyLayoutEngineIntegration();
        return failures;
    }

    private static void VerifyLayerHit()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int background = 0;
        int top = 0;
        UiInteract.Button(new Rect(0f, 0f, 200f, 200f), UiLayer.Background, () => background++);
        UiInteract.Button(new Rect(50f, 50f, 50f, 50f), UiLayer.TopAction, () => top++);

        SetMouse(60f, 60f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();

        Check(background == 0 && top == 1, "overlapping TopAction wins over Background");
        UiInteract.EndFrame();
    }

    private static void VerifyTopActionBeatsRow()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int row = 0;
        int help = 0;
        UiInteract.Row(new Rect(0f, 0f, 200f, 200f), () => row++);
        UiInteract.Button(new Rect(150f, 0f, 50f, 50f), UiLayer.TopAction, () => help++);

        SetMouse(160f, 20f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();

        Check(row == 0 && help == 1, "TopAction help beats overlapping Row");
        UiInteract.EndFrame();
    }

    private static void VerifySameLayerOrder()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int first = 0;
        int second = 0;
        UiInteract.Button(new Rect(0f, 0f, 100f, 100f), UiLayer.Content, () => first++);
        UiInteract.Button(new Rect(10f, 10f, 80f, 80f), UiLayer.Content, () => second++);

        SetMouse(50f, 50f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();

        Check(first == 0 && second == 1, "same-layer later registration wins");
        UiInteract.EndFrame();
    }

    private static void VerifyProtectWins()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int row = 0;
        UiInteract.Row(new Rect(0f, 0f, 200f, 200f), () => row++);
        UiInteract.Protect(new Rect(50f, 50f, 100f, 100f));

        SetMouse(75f, 75f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();

        Check(row == 0, "protected native rect suppresses overlapping Row");
        UiInteract.EndFrame();
    }

    private static void VerifyNoHit()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int clicked = 0;
        UiInteract.Button(new Rect(0f, 0f, 50f, 50f), UiLayer.TopAction, () => clicked++);

        SetMouse(200f, 200f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();

        Check(clicked == 0, "no callback when mouse is outside every rect");
        UiInteract.EndFrame();
    }

    private static void VerifyHoverWithoutClickDoesNotFire()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int clicked = 0;
        UiInteract.Button(new Rect(0f, 0f, 100f, 100f), UiLayer.TopAction, () => clicked++);

        SetMouse(50f, 50f);
        UiInteract.DebugClick = false;
        UiInteract.ProcessEvents();

        Check(clicked == 0, "hover without click does not trigger button");
        UiInteract.EndFrame();
    }

    private static void VerifyCleanup()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int clicked = 0;
        UiInteract.Button(new Rect(0f, 0f, 100f, 100f), UiLayer.TopAction, () => clicked++);
        UiInteract.EndFrame();

        UiInteract.BeginFrame();
        SetMouse(50f, 50f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();
        Check(clicked == 0, "BeginFrame clears previous frame registrations");

        UiInteract.EndFrame();
        UiInteract.BeginFrame();
        SetMouse(50f, 50f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();
        Check(clicked == 0, "EndFrame leaves no registrations for a later frame");
        UiInteract.EndFrame();
    }

    private static void VerifyScrollTransform()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        int clicked = 0;
        UiInteract.PushScrollView(new Rect(100f, 50f, 200f, 300f), new Vector2(0f, 10f));
        UiInteract.Button(new Rect(0f, 0f, 50f, 50f), UiLayer.Content, () => clicked++);
        UiInteract.PopScrollView();

        // Content-local (0,0) maps to page-local (100, 40) when scrolled down by 10.
        SetMouse(105f, 45f);
        UiInteract.DebugClick = true;
        UiInteract.ProcessEvents();

        Check(clicked == 1, "scroll transform maps content-local rect to page-local hit test");
        UiInteract.EndFrame();
    }

    private static void VerifyToPageSpace()
    {
        ResetDebug();
        UiInteract.BeginFrame();
        UiInteract.PushScrollView(new Rect(100f, 50f, 200f, 300f), new Vector2(10f, 20f));

        Rect page = UiInteract.ToPageSpace(new Rect(0f, 0f, 50f, 50f));
        Check(Math.Abs(page.x - 90f) < 0.001f && Math.Abs(page.y - 30f) < 0.001f,
            "ToPageSpace applies outRect offset and scroll delta while a scroll transform is active");

        UiInteract.PopScrollView();
        UiInteract.EndFrame();
    }

    private static void VerifyTryGetCurrentScrollView()
    {
        ResetDebug();
        UiInteract.BeginFrame();

        bool before = UiInteract.TryGetCurrentScrollView(out Rect beforeRect, out Vector2 beforeScroll);
        Check(!before, "TryGetCurrentScrollView returns false outside a scroll view");

        var outRect = new Rect(100f, 50f, 200f, 300f);
        var scroll = new Vector2(10f, 20f);
        UiInteract.PushScrollView(outRect, scroll);
        bool inside = UiInteract.TryGetCurrentScrollView(out Rect actualRect, out Vector2 actualScroll);
        Check(inside
                && Math.Abs(actualRect.x - outRect.x) < 0.001f
                && Math.Abs(actualRect.y - outRect.y) < 0.001f
                && Math.Abs(actualRect.width - outRect.width) < 0.001f
                && Math.Abs(actualRect.height - outRect.height) < 0.001f
                && Math.Abs(actualScroll.x - scroll.x) < 0.001f
                && Math.Abs(actualScroll.y - scroll.y) < 0.001f,
            "TryGetCurrentScrollView returns the active outRect and scroll position");

        UiInteract.PopScrollView();
        UiInteract.EndFrame();
    }

    private static void VerifyLayoutEngineIntegration()
    {
        ResetDebug();
        WidgetRegistry.Clear();
        int clicked = 0;
        WidgetRegistry.Register(WidgetRegistry.CoreScope, "interaction/registering",
            () => new RegisteringButtonWidget(() => clicked++));

        LayoutManifest manifest = LayoutManifest.Parse(
            "<UiPage Schema=\"1\" Source=\"engine\">"
            + "<Widget id=\"a\" Kind=\"interaction/registering\" Height=\"40\" />"
            + "</UiPage>");
        LayoutEngine engine = new(manifest);
        WidgetContext ctx = new("engine", null, new StubMetrics(), new UiPageState());

        SetMouse(10f, 10f);
        UiInteract.DebugClick = true;
        engine.Draw(new Rect(0f, 0f, 200f, 100f), ctx, _ => { });

        Check(clicked == 1, "LayoutEngine.Draw dispatches registered button through ProcessEvents");
    }

    private static void ResetDebug()
    {
        UiInteract.DebugMousePositionEnabled = false;
        UiInteract.DebugMousePosition = default;
        UiInteract.DebugClick = false;
        UiInteract.DebugMouseDown = false;
        UiInteract.DebugMouseDrag = false;
        UiInteract.DebugMouseUp = false;
        UiInteract.DebugEnter = false;
        UiInteract.DebugFocusLost = false;
        UiInteract.SliderOverride = null;
        UiInteract.TextFieldOverride = null;
        UiValueStore.ResetFrame();
    }

    private static void SetMouse(float x, float y)
    {
        UiInteract.DebugMousePositionEnabled = true;
        UiInteract.DebugMousePosition = new Vector2(x, y);
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private sealed class RegisteringButtonWidget : IWidget
    {
        private readonly Action callback;
        private UiElementSpec spec = UiElementSpec.Empty;

        public RegisteringButtonWidget(Action callback)
        {
            this.callback = callback;
        }

        public string Kind => "interaction/registering";

        public void Configure(UiElementSpec spec)
        {
            this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public float Measure(WidgetContext ctx)
        {
            return 40f;
        }

        public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
        {
            UiInteract.Button(rect, UiLayer.TopAction, callback);
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return 24f;
        }
    }
}
