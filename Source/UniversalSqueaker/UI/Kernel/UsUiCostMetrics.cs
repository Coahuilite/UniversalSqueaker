using System;
using System.Diagnostics;
using System.Globalization;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Window-local counters for the existing explicit-Enabled, bounded cost sample.
/// The original ruler and its environment identity pass through unchanged; this adds no reuse policy.</summary>
internal sealed class UsUiCostMetrics : ITextMetrics, IUiTextMetricsEnvironment
{
    private readonly ITextMetrics source;
    private readonly IUiTextMetricsEnvironment? environment;
    private bool active, fitEnabledAtStart;
    private Totals height, width, environmentCapture, modeDraw, selectionDraw;

    internal UsUiCostMetrics(ITextMetrics source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        environment = source as IUiTextMetricsEnvironment;
    }

    internal void Begin(bool fitEnabled)
    {
        fitEnabledAtStart = fitEnabled;
        active = true;
    }

    public float MeasureText(string text, UiFont font, float availableWidth)
    {
        if (!active) return source.MeasureText(text, font, availableWidth);
        long started = Stopwatch.GetTimestamp();
        try { return source.MeasureText(text, font, availableWidth); }
        finally { height.Add(Stopwatch.GetTimestamp() - started); }
    }

    public float MeasureWidth(string text, UiFont font)
    {
        if (!active) return source.MeasureWidth(text, font);
        long started = Stopwatch.GetTimestamp();
        try { return source.MeasureWidth(text, font); }
        finally { width.Add(Stopwatch.GetTimestamp() - started); }
    }

    public UiMeasureInputs? CaptureEnvironment(UiFont font)
    {
        if (!active) return environment?.CaptureEnvironment(font);
        long started = Stopwatch.GetTimestamp();
        try { return environment?.CaptureEnvironment(font); }
        finally { environmentCapture.Add(Stopwatch.GetTimestamp() - started); }
    }

    internal void Complete(int passes, bool partial)
    {
        if (!active) return;
        active = false;
        SqueakLog.LayoutTrace(string.Format(CultureInfo.InvariantCulture,
            "UI.cost metrics passes={0} partial={1} fit_enabled_at_start={2} "
            + "height_calls={3} height_total_ms={4:F3} height_max_ms={5:F3} "
            + "width_calls={6} width_total_ms={7:F3} width_max_ms={8:F3} "
            + "environment_calls={9} environment_total_ms={10:F3} environment_max_ms={11:F3}",
            passes, partial, fitEnabledAtStart,
            height.Calls, Milliseconds(height.Ticks), Milliseconds(height.Max),
            width.Calls, Milliseconds(width.Ticks), Milliseconds(width.Max),
            environmentCapture.Calls, Milliseconds(environmentCapture.Ticks), Milliseconds(environmentCapture.Max)));
        WriteDraw("us/mode-row", modeDraw);
        WriteDraw("us/selection-surface", selectionDraw);
    }

    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    private static void WriteDraw(string kind, Totals totals) => SqueakLog.LayoutTrace(string.Format(
        CultureInfo.InvariantCulture, "UI.cost us_draw kind={0} calls={1} total_ms={2:F3} max_ms={3:F3}",
        kind, totals.Calls, Milliseconds(totals.Ticks), Milliseconds(totals.Max)));

    internal static DrawScope ObserveDraw(UiWidgetContext context, bool modeRow)
    {
        return context.Metrics is UsUiCostMetrics metrics && metrics.active
            ? new DrawScope(metrics, modeRow) : default;
    }

    internal readonly struct DrawScope : IDisposable
    {
        private readonly UsUiCostMetrics? owner;
        private readonly bool modeRow;
        private readonly long started;
        internal DrawScope(UsUiCostMetrics owner, bool modeRow)
        { this.owner = owner; this.modeRow = modeRow; started = Stopwatch.GetTimestamp(); }
        public void Dispose()
        {
            if (owner == null || !owner.active) return;
            long ticks = Stopwatch.GetTimestamp() - started;
            if (modeRow) owner.modeDraw.Add(ticks);
            else owner.selectionDraw.Add(ticks);
        }
    }

    private struct Totals
    {
        internal long Calls, Ticks, Max;
        internal void Add(long ticks) { Calls++; Ticks += ticks; Max = Math.Max(Max, ticks); }
    }
}
