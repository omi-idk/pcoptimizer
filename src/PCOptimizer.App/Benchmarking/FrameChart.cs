using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PCOptimizer.Benchmarking;

namespace PCOptimizer.App.Benchmarking;

public sealed class FrameChart : FrameworkElement
{
    public static readonly DependencyProperty FramesProperty = DependencyProperty.Register(nameof(Frames), typeof(FrameSample[]), typeof(FrameChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HistogramProperty = DependencyProperty.Register(nameof(Histogram), typeof(bool), typeof(FrameChart), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public FrameSample[]? Frames { get => (FrameSample[]?)GetValue(FramesProperty); set => SetValue(FramesProperty, value); }
    public bool Histogram { get => (bool)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 100 || ActualHeight < 80) return;
        var plot = new Rect(52, 12, ActualWidth - 64, ActualHeight - 42);
        var muted = new SolidColorBrush(Color.FromRgb(154, 168, 188));
        var ink = new SolidColorBrush(Color.FromRgb(125, 179, 255));
        void Label(string text, double x, double y) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 22, 34)), null, plot);
        if (Frames is not { Length: > 1 } frames) { Label("Sin muestras", plot.X + 12, plot.Y + 20); return; }
        if (Histogram)
        {
            const int count = 20;
            var maximum = frames.Max(f => 1000 / f.IntervalMs);
            var bins = new int[count];
            foreach (var frame in frames) bins[Math.Min(count - 1, (int)(1000 / frame.IntervalMs / maximum * count))]++;
            var top = bins.Max();
            for (var i = 0; i < count; i++)
            {
                var height = plot.Height * bins[i] / top;
                dc.DrawRectangle(ink, null, new(plot.X + plot.Width * i / count, plot.Bottom - height, Math.Max(1, plot.Width / count - 2), height));
            }
            Label(top.ToString("N0"), 0, plot.Top); Label("0 FPS", plot.Left, plot.Bottom + 8);
            Label($"{maximum:N0} FPS", Math.Max(plot.Left, plot.Right - 75), plot.Bottom + 8);
        }
        else
        {
            var maximum = frames.Max(f => f.IntervalMs);
            var duration = frames.Sum(f => f.IntervalMs);
            // Max per horizontal pixel preserves isolated spikes without allocating a visual per frame.
            var columns = new double[Math.Max(1, (int)plot.Width)];
            double elapsed = 0;
            foreach (var frame in frames)
            {
                var column = Math.Min(columns.Length - 1, (int)(elapsed / duration * columns.Length));
                columns[column] = Math.Max(columns[column], frame.IntervalMs); elapsed += frame.IntervalMs;
            }
            var pen = new Pen(ink, 1);
            for (var i = 0; i < columns.Length; i++)
                if (columns[i] > 0) dc.DrawLine(pen, new(plot.Left + i, plot.Bottom), new(plot.Left + i, plot.Bottom - plot.Height * columns[i] / maximum));
            Label($"{maximum:N1} ms", 0, plot.Top); Label("0 s", plot.Left, plot.Bottom + 8);
            Label($"{duration / 1000:N1} s", Math.Max(plot.Left, plot.Right - 65), plot.Bottom + 8);
        }
    }
}
