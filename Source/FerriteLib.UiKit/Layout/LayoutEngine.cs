using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit;

/// <summary>
/// Two-pass synchronous layout engine. Measure recursively walks the manifest and builds a flat
/// pre-order rect list that contains both containers and leaf widgets; Draw reuses those rects.
/// The measured rect cache is invalidated when the context or view width changes, and Draw
/// re-measures automatically if it was called without Measure.
/// </summary>
public sealed class LayoutEngine
{
    private enum ContainerKind
    {
        None,
        Block,
        Section,
        Column
    }

    private readonly struct Padding
    {
        internal readonly float Top;
        internal readonly float Right;
        internal readonly float Bottom;
        internal readonly float Left;

        internal Padding(float top, float right, float bottom, float left)
        {
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
        }

        internal static readonly Padding Zero = new(0f, 0f, 0f, 0f);
    }

    private sealed class MeasuredElement
    {
        internal UiElementSpec Spec = UiElementSpec.Empty;
        internal IWidget? Widget;
        internal ContainerKind ContainerKind = ContainerKind.None;
        internal Rect Rect;
    }

    private const float SectionTitleHeight = 22f;

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

            var elements = new List<MeasuredElement>();
            float height = MeasureElement(root, 0f, y, width, ctx, elements);
            measured.AddRange(elements);
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

                if (element.ContainerKind != ContainerKind.None)
                {
                    DrawContainer(element, rect, ctx);
                }
                else
                {
                    element.Widget!.Draw(rect, ctx, emit);
                }
            }

            if (processEvents)
            {
                UiInteract.DrawPopups();
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

    /// <summary>
    /// Returns the vertical position of a measured element by its XML id, if present in the last
    /// measure pass. Used for scroll-to-section navigation.
    /// </summary>
    public bool TryGetElementY(string id, out float y)
    {
        if (id != null)
        {
            foreach (MeasuredElement element in measured)
            {
                if (string.Equals(element.Spec.Id, id, StringComparison.Ordinal))
                {
                    y = element.Rect.y;
                    return true;
                }
            }
        }

        y = 0f;
        return false;
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

    private float MeasureElement(
        UiElementSpec spec,
        float x,
        float y,
        float width,
        WidgetContext ctx,
        List<MeasuredElement> output)
    {
        ContainerKind kind = GetContainerKind(spec);
        if (kind == ContainerKind.None)
        {
            IWidget widget = WidgetRegistry.Resolve(manifest.Source, spec.Kind);
            widget.Configure(spec);

            float height = ResolveLeafHeight(spec, widget, ctx, width);
            height = NormalizeHeight(height);

            output.Add(new MeasuredElement
            {
                Spec = spec,
                Widget = widget,
                ContainerKind = ContainerKind.None,
                Rect = new Rect(x, y, width, height)
            });
            return height;
        }

        var childOutput = new List<MeasuredElement>();
        float naturalHeight = MeasureContainer(spec, kind, x, y, width, ctx, childOutput);
        float containerHeight = ResolveContainerHeight(spec, naturalHeight);

        output.Add(new MeasuredElement
        {
            Spec = spec,
            Widget = null,
            ContainerKind = kind,
            Rect = new Rect(x, y, width, containerHeight)
        });
        output.AddRange(childOutput);
        return containerHeight;
    }

    private float MeasureContainer(
        UiElementSpec spec,
        ContainerKind kind,
        float x,
        float y,
        float width,
        WidgetContext ctx,
        List<MeasuredElement> childOutput)
    {
        Padding padding = ParsePadding(spec);
        float gap = ReadGap(spec);
        float titleHeight = HasTitle(spec) ? SectionTitleHeight : 0f;
        float innerX = x + padding.Left;
        float innerY = y + padding.Top + titleHeight;
        float innerWidth = Math.Max(1f, width - padding.Left - padding.Right);

        if (kind == ContainerKind.Column)
        {
            var visible = new List<UiElementSpec>();
            foreach (UiElementSpec child in spec.Children)
            {
                if (!IsHidden(child)) visible.Add(child);
            }

            float[] childWidths = ResolveColumnWidths(visible, innerWidth);
            float cursorX = innerX;
            float maxChildHeight = 0f;

            for (int i = 0; i < visible.Count; i++)
            {
                var elements = new List<MeasuredElement>();
                float childHeight = MeasureElement(visible[i], cursorX, innerY, childWidths[i], ctx, elements);
                childOutput.AddRange(elements);
                maxChildHeight = Math.Max(maxChildHeight, childHeight);
                cursorX += childWidths[i] + gap;
            }

            return padding.Top + titleHeight + maxChildHeight + padding.Bottom;
        }

        float cursorY = innerY;
        bool firstVisible = true;

        foreach (UiElementSpec child in spec.Children)
        {
            if (IsHidden(child)) continue;

            if (!firstVisible) cursorY += gap;

            var elements = new List<MeasuredElement>();
            float childHeight = MeasureElement(child, innerX, cursorY, innerWidth, ctx, elements);
            childOutput.AddRange(elements);
            cursorY += childHeight;
            firstVisible = false;
        }

        float contentHeight = cursorY - innerY;
        return padding.Top + titleHeight + contentHeight + padding.Bottom;
    }

    private static float[] ResolveColumnWidths(IReadOnlyList<UiElementSpec> children, float innerWidth)
    {
        var widths = new float[children.Count];
        float fixedSum = 0f;
        int autoCount = 0;

        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].TryGetAttribute("Width", out string raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedWidth)
                && fixedWidth > 0f)
            {
                widths[i] = fixedWidth;
                fixedSum += fixedWidth;
            }
            else
            {
                widths[i] = 0f;
                autoCount++;
            }
        }

        float remaining = innerWidth - fixedSum;
        float autoWidth = autoCount > 0 ? Math.Max(0f, remaining / autoCount) : 0f;

        for (int i = 0; i < widths.Length; i++)
        {
            if (widths[i] == 0f) widths[i] = autoWidth;
        }

        return widths;
    }

    private void DrawContainer(MeasuredElement element, Rect rect, WidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        if (element.ContainerKind == ContainerKind.Column)
        {
            SurfaceFrame.Draw(rect, SurfaceFrame.SurfaceKind.Base);
        }
        else
        {
            SurfaceFrame.Draw(rect, SurfaceFrame.SurfaceKind.Panel);
        }

        if (HasTitle(element.Spec))
        {
            Padding padding = ParsePadding(element.Spec);
            float innerWidth = Math.Max(1f, rect.width - padding.Left - padding.Right);
            var headerRect = new Rect(rect.x + padding.Left, rect.y + padding.Top, innerWidth, SectionTitleHeight);
            UiPanel.DrawHeader(headerRect, ReadTitle(element.Spec));
        }
    }

    private static string ReadTitle(UiElementSpec spec)
    {
        return spec.TryGetAttribute("Title", out string raw) ? raw : "";
    }

    private static bool HasTitle(UiElementSpec spec)
    {
        return ReadTitle(spec).Trim().Length > 0;
    }

    private static Padding ParsePadding(UiElementSpec spec)
    {
        if (!spec.TryGetAttribute("Padding", out string raw))
            return Padding.Zero;

        string[] parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 1 && parts.Length != 2 && parts.Length != 4)
            throw new FormatException(
                $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Padding '{raw}'; expected 1, 2, or 4 numbers.");

        var values = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                throw new FormatException(
                    $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Padding value '{parts[i].Trim()}'.");
        }

        return parts.Length switch
        {
            1 => new Padding(values[0], values[0], values[0], values[0]),
            2 => new Padding(values[0], values[1], values[0], values[1]),
            _ => new Padding(values[0], values[1], values[2], values[3]),
        };
    }

    private static float ReadGap(UiElementSpec spec)
    {
        if (!spec.TryGetAttribute("Gap", out string raw) || raw.Trim().Length == 0)
            return 0f;

        if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float gap) && gap >= 0f)
            return gap;

        throw new FormatException(
            $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Gap '{raw}'; expected a non-negative number.");
    }

    private static ContainerKind GetContainerKind(UiElementSpec spec)
    {
        if (string.Equals(spec.Kind, "Block", StringComparison.Ordinal)) return ContainerKind.Block;
        if (string.Equals(spec.Kind, "Section", StringComparison.Ordinal)) return ContainerKind.Section;
        if (string.Equals(spec.Kind, "Column", StringComparison.Ordinal)) return ContainerKind.Column;
        return ContainerKind.None;
    }

    private static float ResolveLeafHeight(UiElementSpec spec, IWidget widget, WidgetContext ctx, float width)
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

    private static float ResolveContainerHeight(UiElementSpec spec, float naturalHeight)
    {
        if (spec.TryGetAttribute("Height", out string raw))
        {
            string value = raw.Trim();
            if (value.Length == 0 || string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
                return naturalHeight;

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedHeight))
                return NormalizeHeight(fixedHeight);

            throw new FormatException(
                $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Height '{raw}'; expected a number or Auto.");
        }

        return naturalHeight;
    }

    private bool HasUsableCache(WidgetContext ctx, float viewWidth)
    {
        return measuredContext != null
            && ReferenceEquals(measuredContext, ctx)
            && measuredViewWidth == viewWidth;
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
