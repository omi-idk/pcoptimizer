using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PCOptimizer.App.Benchmarking;
using PCOptimizer.Benchmarking;
namespace PCOptimizer.App.Monitoring;

public partial class LiveMonitorWindow : Window
{
    private readonly LiveMonitorViewModel model;
    private readonly Action openBenchmarks;
    private bool closeRequested, closed;
    internal LiveMonitorWindow(Func<bool> acquire, Action release, Action benchmarks)
    {
        InitializeComponent(); openBenchmarks = benchmarks;
        DataContext = model = new(p => new PerformanceCaptureService(new NvidiaTelemetry(), p, TimeSpan.FromSeconds(1)), acquire, release);
        var ageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        ageTimer.Tick += (_, _) => model.RefreshAge();
        Closed += (_, _) => { closed = true; ageTimer.Stop(); };
        Closing += (_, e) => { if (model.IsRunning) { e.Cancel = true; closeRequested = true; model.Stop(); } };
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(model.IsRunning)) return;
            if (model.IsRunning) ageTimer.Start(); else ageTimer.Stop();
            if (!model.IsRunning && closeRequested) Dispatcher.BeginInvoke(() => { if (!closed) Close(); });
        };
    }
    internal void ConfigureDesignPreview(PerformanceTrace trace) { DesignBanner.Visibility = Visibility.Visible; model.SetDesignTrace(trace); }
    private async void Start(object sender, RoutedEventArgs e) => await model.StartAsync();
    private void Stop(object sender, RoutedEventArgs e) => model.Stop();
    private async void Benchmarks(object sender, RoutedEventArgs e) { await model.StopAsync(); openBenchmarks(); if (!closed) Close(); }
    private async void Export(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Sensores JSON (*.json)|*.json", FileName = "sensores-en-vivo.json", AddExtension = true, DefaultExt = ".json" };
        if (dialog.ShowDialog(this) != true) return;
        try { await model.ExportAsync(dialog.FileName, CancellationToken.None); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo exportar", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}
