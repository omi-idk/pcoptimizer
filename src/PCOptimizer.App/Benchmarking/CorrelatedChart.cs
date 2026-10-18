using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PCOptimizer.Benchmarking;

namespace PCOptimizer.App.Benchmarking;

// Both panels use the same QPC range; gaps are left blank. No sensor interpolation or generated FPS.
public sealed record SensorSeriesOption(SensorMetric Metric, string Device, string Source, string Unit)
{
    public override string ToString() => $"{Metric switch
    {
        SensorMetric.CpuTotalPercent => "CPU global", SensorMetric.MemoryUsedPercent => "Memoria física usada",
        SensorMetric.GpuUtilizationPercent => "Carga GPU", SensorMetric.GpuTemperatureC => "Temperatura GPU",
        SensorMetric.GpuClockMhz => "Reloj GPU", SensorMetric.GpuPowerWatts => "Potencia GPU",
        SensorMetric.GpuVramUsedMiB => "VRAM usada", _ => Metric.ToString()
    }} · {Device} · {Unit}";
}

public sealed class CorrelatedChart : FrameworkElement
{
    public static readonly DependencyProperty FramesProperty = DependencyProperty.Register(nameof(Frames), typeof(FrameSample[]), typeof(CorrelatedChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TraceProperty = DependencyProperty.Register(nameof(Trace), typeof(PerformanceTrace), typeof(CorrelatedChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(SensorSeriesOption), typeof(CorrelatedChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public SensorSeriesOption? Series { get => (SensorSeriesOption?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
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
        Label("0", 18, bottom.Bottom - 12);
        Label("0 s", top.Left, bottom.Bottom + 8);
        Label($"{(trace.EndedQpc - trace.StartedQpc) / (double)trace.QpcFrequency:N1} s QPC", Math.Max(top.Left, top.Right - 90), bottom.Bottom + 8);
        if (Series is not { } series) { Label("Sin serie seleccionada", bottom.Left + 10, bottom.Top + 10); return; }
        var points = trace.Observations.SelectMany(o => o.Readings.Select(r => (Observation: o, Reading: r)))
            .Where(x => x.Reading.Metric == series.Metric && x.Reading.Device == series.Device && x.Reading.Source == series.Source
                && PerformanceAnalysis.IsUsable(x.Observation, x.Reading, trace.QpcFrequency)).ToArray();
        var percent = series.Unit == "%";
        var maximum = percent ? 100 : Math.Max(1, points.Select(p => p.Reading.Value ?? 0).DefaultIfEmpty().Max() * 1.05);
        Label($"{maximum:N0} {series.Unit}", 0, bottom.Top);
        var brush = new SolidColorBrush(Colors.MediumAquamarine);
        foreach (var point in points)
        {
            var o = point.Observation;
            if (point.Reading.Value is not { } value) continue;
            dc.DrawEllipse(brush, null, new(X(o.StartQpc + (o.EndQpc - o.StartQpc) / 2), bottom.Bottom - value / maximum * bottom.Height), 2, 2);
        }
        Label("Lecturas puntuales · sin interpolación", bottom.Left, ActualHeight - 13, brush);
    }
}
