using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Widgets;

/// <summary>
/// Neutral normalized line chart with draggable control points. Points use normalized 0..1
/// coordinates, either from the <c>Points</c> literal or from <c>Bind</c> view state. Dragging a
/// control point emits a neutral <see cref="UiCommand"/> named by <c>EmitName</c> (default
/// "PointChanged") with a <see cref="LineChartPointChange"/> payload.
/// </summary>
public sealed class LineChartWidget : IWidget
{
    public const string Kind = "chart/line";

    private const string BindAttribute = "Bind";
    private const string PointsAttribute = "Points";
    private const string HeightAttribute = "Height";
    private const string EditableAttribute = "Editable";
    private const string EmitNameAttribute = "EmitName";
    private const string DefaultEmitName = "PointChanged";
    private const float DefaultHeight = 120f;
    private const float PlotPadding = 8f;
    private const float PointSize = 5f;
    private const float HitRadius = 6f;
    private const float Epsilon = 0.0001f;

    private UiElementSpec _spec = UiElementSpec.Empty;

    string IWidget.Kind => Kind;

    public void Configure(UiElementSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public float Measure(WidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        return ReadHeight();
    }

    public void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (emit == null) throw new ArgumentNullException(nameof(emit));
        if (rect.width <= 1f || rect.height <= 1f) return;

        string emitName = Read(EmitNameAttribute);
        if (emitName.Length == 0) emitName = DefaultEmitName;
        bool editable = ReadBool(EditableAttribute, true);

        List<Vector2> points = ResolvePoints(ctx);

        string scope = _spec.Id.Length > 0 ? _spec.Id : Kind;
        var id = new UiControlId(scope, "chart");
        UiValueState state = UiValueStore.GetOrCreate(id);

        Rect plotRect = new(
            rect.x + PlotPadding,
            rect.y + PlotPadding,
            Math.Max(1f, rect.width - PlotPadding * 2f),
            Math.Max(1f, rect.height - PlotPadding * 2f));

        SurfaceFrame.DrawBorder(plotRect);
        DrawGrid(plotRect);

        if (points.Count >= 2)
            DrawPolyline(plotRect, points);

        DrawPoints(plotRect, points);

        if (editable)
        {
            UiInteract.Protect(rect);
            HandleDrag(plotRect, points, id, state, emitName, emit);
        }
    }

    internal static List<Vector2> ParsePoints(string raw)
    {
        var result = new List<Vector2>();
        if (raw == null) return result;

        string[] pointParts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string pointPart in pointParts)
        {
            string[] xy = pointPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length != 2)
                throw new FormatException($"Invalid chart point '{pointPart.Trim()}'; expected \"x,y\".");

            if (!float.TryParse(xy[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(xy[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                throw new FormatException($"Invalid chart point '{pointPart.Trim()}'; expected numeric x,y.");
            }

            result.Add(new Vector2(Clamp01(x), Clamp01(y)));
        }

        return result;
    }

    private List<Vector2> ResolvePoints(WidgetContext ctx)
    {
        if (_spec.TryGetAttribute(BindAttribute, out string key)
            && key.Length > 0
            && ctx.TryGetViewValue(key, out object? value)
            && value != null)
        {
            switch (value)
            {
                case IEnumerable<Vector2> vectors:
                    return ToList(vectors);
                case IEnumerable<object?> objects:
                    var converted = new List<Vector2>();
                    foreach (object? item in objects)
                    {
                        switch (item)
                        {
                            case Vector2 vector:
                                converted.Add(new Vector2(Clamp01(vector.x), Clamp01(vector.y)));
                                break;
                            case string text:
                                converted.AddRange(ParsePoints(text));
                                break;
                        }
                    }

                    return converted;
                case string text:
                    return ParsePoints(text);
            }
        }

        if (_spec.TryGetAttribute(PointsAttribute, out string literal) && literal.Trim().Length > 0)
            return ParsePoints(literal);

        return new List<Vector2>();
    }

    private static List<Vector2> ToList(IEnumerable<Vector2> source)
    {
        var result = new List<Vector2>();
        foreach (Vector2 point in source)
            result.Add(new Vector2(Clamp01(point.x), Clamp01(point.y)));
        return result;
    }

    private void HandleDrag(
        Rect plotRect,
        List<Vector2> points,
        UiControlId id,
        UiValueState state,
        string emitName,
        Action<UiCommand> emit)
    {
        Vector2 pointer = UiInteract.PointerPosition();

        if (UiInteract.IsPointerDown())
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 pixel = ToPixel(plotRect, points[i]);
                if (Distance(pointer, pixel) <= HitRadius)
                {
                    state.Dragging = true;
                    state.Cursor = i;
                    break;
                }
            }
        }

        if (state.Dragging && state.Cursor >= 0 && state.Cursor < points.Count)
        {
            if (UiInteract.IsPointerDown() || UiInteract.IsPointerDragging())
            {
                Vector2 previous = points[state.Cursor];
                Vector2 updated = ToNormalized(plotRect, pointer);
                if (Math.Abs(previous.x - updated.x) > Epsilon || Math.Abs(previous.y - updated.y) > Epsilon)
                {
                    points[state.Cursor] = updated;
                    emit(new UiCommand(emitName, new LineChartPointChange(state.Cursor, updated)));
                }
            }
        }

        if (UiInteract.IsPointerUp())
        {
            state.Dragging = false;
        }
    }

    private static void DrawGrid(Rect plotRect)
    {
        for (int i = 1; i <= 3; i++)
        {
            float fx = plotRect.x + plotRect.width * i / 4f;
            VerseWidgets.DrawBoxSolid(new Rect(fx, plotRect.y, 1f, plotRect.height), Palette.Divider);

            float fy = plotRect.y + plotRect.height * i / 4f;
            VerseWidgets.DrawBoxSolid(new Rect(plotRect.x, fy, plotRect.width, 1f), Palette.Divider);
        }
    }

    private static void DrawPolyline(Rect plotRect, IReadOnlyList<Vector2> points)
    {
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = ToPixel(plotRect, points[i - 1]);
            Vector2 b = ToPixel(plotRect, points[i]);

            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float distance = (float)Math.Sqrt((double)(dx * dx + dy * dy));
            int steps = Math.Max(1, (int)Math.Ceiling(distance / 2f));

            for (int step = 0; step < steps; step++)
            {
                float t = steps == 1 ? 1f : step / (float)(steps - 1);
                float px = a.x + dx * t;
                float py = a.y + dy * t;
                VerseWidgets.DrawBoxSolid(new Rect(px, py, 1f, 1f), Palette.AccentGold);
            }
        }
    }

    private static void DrawPoints(Rect plotRect, IReadOnlyList<Vector2> points)
    {
        foreach (Vector2 point in points)
        {
            Vector2 pixel = ToPixel(plotRect, point);
            VerseWidgets.DrawBoxSolid(
                new Rect(pixel.x - PointSize * 0.5f, pixel.y - PointSize * 0.5f, PointSize, PointSize),
                Palette.AccentGold);
        }
    }

    private static Vector2 ToPixel(Rect plotRect, Vector2 normalized)
    {
        float x = plotRect.x + Clamp01(normalized.x) * plotRect.width;
        float y = plotRect.y + (1f - Clamp01(normalized.y)) * plotRect.height;
        return new Vector2(x, y);
    }

    private static Vector2 ToNormalized(Rect plotRect, Vector2 pixel)
    {
        float x = plotRect.width > 1f ? (pixel.x - plotRect.x) / plotRect.width : 0f;
        float y = plotRect.height > 1f ? (pixel.y - plotRect.y) / plotRect.height : 0f;
        return new Vector2(Clamp01(x), Clamp01(1f - y));
    }

    private static float Distance(Vector2 a, Vector2 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)Math.Sqrt((double)(dx * dx + dy * dy));
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    private float ReadHeight()
    {
        if (_spec.TryGetAttribute(HeightAttribute, out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }

    private string Read(string name)
    {
        return _spec.TryGetAttribute(name, out string value) ? value : "";
    }

    private bool ReadBool(string name, bool fallback)
    {
        if (!_spec.TryGetAttribute(name, out string raw)) return fallback;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.Ordinal);
    }
}
