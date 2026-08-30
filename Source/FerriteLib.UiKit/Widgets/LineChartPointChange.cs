using UnityEngine;

namespace FerriteLib.UiKit;

/// <summary>Payload emitted by <c>chart/line</c> when a control point changes.</summary>
public readonly struct LineChartPointChange
{
    public int Index { get; }

    public Vector2 Point { get; }

    public LineChartPointChange(int index, Vector2 point)
    {
        Index = index;
        Point = point;
    }
}
