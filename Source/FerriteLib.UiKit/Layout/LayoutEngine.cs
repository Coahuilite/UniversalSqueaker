using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit;

/// <summary>
/// Two-pass synchronous layout engine. Measure walks the manifest and builds the rect list;
/// Draw reuses those rects. The measured rect cache is invalidated when the context or view
/// width changes, and Draw re-measures automatically if it was called without Measure.
/// </summary>
public sealed class LayoutEngine
{
    private sealed class MeasuredElement
    {
        internal UiElementSpec Spec = UiElementSpec.Empty;
        internal IWidget Widget = null!;
        internal Rect Rect;
    }

    private readonly LayoutManifest manifest;
    private readonly List<MeasuredElement> measured = new();

    private WidgetContext? measuredContext;
    private float measuredViewWidth = -1f;
    private float measuredContentHeight = 1f;

    public LayoutEngine(LayoutManifest manifest)
    {
        this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <summary>Measures all visible roots and returns the total content height (always at least 1).</summary>
    public float Measure(WidgetContext ctx, float viewWidth)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        float width = NormalizeViewWidth(viewWidth);
        ctx.ViewWidth = width;

        measured.Clear();
        float y = 0f;

        foreach (UiElementSpec root in manifest.Roots)
        {
            if (IsHidden(root)) continue;

            IWidget widget = WidgetRegistry.Resolve(manifest.Source, root.Kind);
            widget.Configure(root);

            float height = ResolveHeight(root, widget, ctx, width);
            height = NormalizeHeight(height);

            var element = new MeasuredElement
            {
                Spec = root,
                Widget = widget,
                Rect = new Rect(0f, y, width, height)
            };
            measured.Add(element);
            y += height;
        }

        measuredContext = ctx;
        measuredViewWidth = width;
        measuredContentHeight = Math.Max(1f, y);
        return measuredContentHeight;
    }

    /// <summary>Draws all visible roots using the last measured rects. Emits commands through <paramref name="emit"/>.</summary>
    public void Draw(Rect viewRect, WidgetContext ctx, Action<UiCommand> emit)
    {
        Draw(viewRect, ctx, emit, processEvents: true);
    }

    /// <summary>
    /// Draws all visible roots using the last measured rects. When <paramref name="processEvents"/> is
    /// false, the caller owns the <see cref="UiInteract"/> frame and must call
    /// <see cref="UiInteract.ProcessEvents"/> after all page chrome has been drawn.
    /// </summary>
    public void Draw(Rect viewRect, WidgetContext ctx, Action<UiCommand> emit, bool processEvents)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));

        if (processEvents)
        {
            UiInteract.BeginFrame();
        }

        try
        {
            if (!HasUsableCache(ctx, viewRect.width))
                Measure(ctx, viewRect.width);

            foreach (MeasuredElement element in measured)
            {
                Rect rect = new(
                    viewRect.x + element.Rect.x,
                    viewRect.y + element.Rect.y,
                    element.Rect.width,
                    element.Rect.height);
                element.Widget.Draw(rect, ctx, emit);
            }

            if (processEvents)
            {
                UiInteract.ProcessEvents();
            }
        }
        finally
        {
            if (processEvents)
            {
                UiInteract.EndFrame();
            }
        }
    }

    /// <summary>Clamps <see cref="UiPageState.ScrollPosition"/> against the last measured content height.</summary>
    public void ClampScroll(UiPageState state, float viewHeight)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        float contentHeight = measuredContext != null ? measuredContentHeight : 1f;
        float view = NormalizeViewHeight(viewHeight);
        float maxY = Math.Max(0f, contentHeight - view);

        float current = float.IsNaN(state.ScrollPosition.y) || state.ScrollPosition.y < 0f
            ? 0f
            : state.ScrollPosition.y;
        float clamped = Math.Min(current, maxY);

        state.ScrollPosition = new Vector2(state.ScrollPosition.x, clamped);
    }

    private bool HasUsableCache(WidgetContext ctx, float viewWidth)
    {
        return measuredContext != null
            && ReferenceEquals(measuredContext, ctx)
            && measuredViewWidth == viewWidth;
    }

    private static float ResolveHeight(UiElementSpec spec, IWidget widget, WidgetContext ctx, float width)
    {
        if (spec.TryGetAttribute("Height", out string raw))
        {
            string value = raw.Trim();
            if (value.Length == 0)
                return NormalizeHeight(widget.Measure(ctx));

            if (string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
                return NormalizeHeight(widget.Measure(ctx));

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedHeight))
                return NormalizeHeight(fixedHeight);

            throw new FormatException(
                $"Widget id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Height '{raw}'; expected a number or Auto.");
        }

        return NormalizeHeight(widget.Measure(ctx));
    }

    private static bool IsHidden(UiElementSpec spec)
    {
        if (!spec.TryGetAttribute("Hidden", out string raw)) return false;
        string value = raw.Trim();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static float NormalizeHeight(float height)
    {
        if (float.IsNaN(height) || float.IsInfinity(height) || height < 0f) return 0f;
        return height;
    }

    private static float NormalizeViewWidth(float viewWidth)
    {
        if (float.IsNaN(viewWidth) || float.IsInfinity(viewWidth) || viewWidth < 1f) return 1f;
        return viewWidth;
    }

    private static float NormalizeViewHeight(float viewHeight)
    {
        if (float.IsNaN(viewHeight) || viewHeight < 0f) return 0f;
        return viewHeight;
    }
}
