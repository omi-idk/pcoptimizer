using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PCOptimizer.Benchmarking;

namespace PCOptimizer.App.Benchmarking;

// Both panels use the same QPC range; gaps are left blank. No sensor interpolation or generated FPS.
public sealed class CorrelatedChart : FrameworkElement
{
    public static readonly DependencyProperty FramesProperty = DependencyProperty.Register(nameof(Frames), typeof(FrameSample[]), typeof(CorrelatedChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TraceProperty = DependencyProperty.Register(nameof(Trace), typeof(PerformanceTrace), typeof(CorrelatedChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public FrameSample[]? Frames { get => (FrameSample[]?)GetValue(FramesProperty); set => SetValue(FramesProperty, value); }
    public PerformanceTrace? Trace { get => (PerformanceTrace?)GetValue(TraceProperty); set => SetValue(TraceProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 180 || ActualHeight < 150) return;
        var muted = new SolidColorBrush(Color.FromRgb(154, 168, 188));
        var blue = new SolidColorBrush(Color.FromRgb(125, 179, 255));
        void Label(string text, double x, double y, Brush? brush = null) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, brush ?? muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
        if (Trace is not { } trace || Frames is not { Length: > 1 } frames)
        { Label("Sin sensores sincronizados de esta captura", 12, 16); return; }
        var width = ActualWidth - 90;
        var top = new Rect(65, 20, width, (ActualHeight - 100) * .45);
        var bottom = new Rect(65, top.Bottom + 30, width, (ActualHeight - 100) * .55);
        double X(long qpc) => top.Left + top.Width * (qpc - trace.StartedQpc) / (trace.EndedQpc - (double)trace.StartedQpc);
        var background = new SolidColorBrush(Color.FromRgb(15, 22, 34));
        dc.DrawRectangle(background, null, top); dc.DrawRectangle(background, null, bottom);
        var timed = frames.Where(f => f.CpuStartQpc >= trace.StartedQpc && f.CpuStartQpc <= trace.EndedQpc).ToArray();
        if (timed.Length > 0)
        {
            var max = timed.Max(f => f.IntervalMs);
            var columns = new double[Math.Max(1, (int)top.Width + 1)];
            foreach (var frame in timed)
            {
                var index = Math.Clamp((int)(X(frame.CpuStartQpc!.Value) - top.Left), 0, columns.Length - 1);
                columns[index] = Math.Max(columns[index], frame.IntervalMs);
            }
            var pen = new Pen(blue, 1);
            for (var i = 0; i < columns.Length; i++) if (columns[i] > 0)
                dc.DrawLine(pen, new(top.Left + i, top.Bottom), new(top.Left + i, top.Bottom - top.Height * columns[i] / max));
            Label($"{max:N1} ms", 0, top.Top);
        }
        Label("100%", 8, bottom.Top); Label("0%", 18, bottom.Bottom - 12);
        Label("0 s", top.Left, bottom.Bottom + 8);
        Label($"{(trace.EndedQpc - trace.StartedQpc) / (double)trace.QpcFrequency:N1} s QPC", Math.Max(top.Left, top.Right - 90), bottom.Bottom + 8);
        var colors = new[] { Colors.MediumAquamarine, Colors.Orange, Colors.Orchid, Colors.SkyBlue, Colors.Khaki, Colors.Salmon };
        var lanes = trace.Observations.SelectMany(o => o.Readings.Select(r => (Observation: o, Reading: r)))
            .Where(x => x.Reading.Metric is SensorMetric.CpuTotalPercent or SensorMetric.MemoryUsedPercent or SensorMetric.GpuUtilizationPercent)
            .GroupBy(x => (x.Reading.Metric, x.Reading.Device, x.Reading.Source)).ToArray();
        int laneIndex = 0;
        foreach (var lane in lanes)
        {
            var brush = new SolidColorBrush(colors[laneIndex++ % colors.Length]);
            foreach (var point in lane)
            {
                var o = point.Observation;
                if (point.Reading.Value is not { } value || !PerformanceAnalysis.IsUsable(o, point.Reading, trace.QpcFrequency)) continue;
                dc.DrawEllipse(brush, null, new(X(o.StartQpc + (o.EndQpc - o.StartQpc) / 2), bottom.Bottom - value / 100 * bottom.Height), 2, 2);
            }
            // Full device identity and coverage remain available in the report below the chart.
            if (laneIndex <= 3) Label(lane.Key.Metric.ToString(), top.Left + (laneIndex - 1) * Math.Min(175, top.Width / 3), ActualHeight - 13, brush);
        }
    }
}
