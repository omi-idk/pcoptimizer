using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace PCOptimizer.App.Monitoring;

public sealed class LiveSensorChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(LivePoint[]), typeof(LiveSensorChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public LivePoint[]? Points { get => (LivePoint[]?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); if (ActualWidth < 160 || ActualHeight < 100) return;
        var plot = new Rect(65, 15, ActualWidth - 90, ActualHeight - 50); var muted = new SolidColorBrush(Color.FromRgb(154, 168, 188));
        void Label(string text, double x, double y) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 22, 34)), null, plot);
        var points = Points ?? [];
        if (points.Length == 0 || points.All(p => p.Value is null)) { Label("N/D · sin lecturas utilizables", plot.Left + 12, plot.Top + 18); return; }
        var first = points[0].Seconds; var end = points[^1].Seconds; var duration = Math.Max(.001, end - first);
        var max = Math.Max(1, points.Max(p => p.Value ?? 0) * 1.05); var brush = new SolidColorBrush(Color.FromRgb(107, 211, 171));
        foreach (var point in points) if (point.Value is { } value) dc.DrawEllipse(brush, null, new(plot.Left + (point.Seconds - first) / duration * plot.Width, plot.Bottom - value / max * plot.Height), 2.5, 2.5);
        Label($"{max:N1}", 0, plot.Top); Label("0", 20, plot.Bottom - 12); Label($"{first:N1} s", plot.Left, plot.Bottom + 10); Label($"{end:N1} s", plot.Right - 75, plot.Bottom + 10);
    }
}
