using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PCOptimizer.Benchmarking;

namespace PCOptimizer.App.Benchmarking;

public partial class BenchmarkView : UserControl
{
    private readonly BenchmarkViewModel model;
    public bool IsBusy => model.IsBusy;
    internal event EventHandler? BusyChanged;
    public BenchmarkView() : this(new BenchmarkSessionStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCOptimizer", "Benchmarks")),
        new PresentMonCaptureService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCOptimizer", "Captures")), new PerformanceCaptureService(new NvidiaTelemetry())) { }
    internal BenchmarkView(IBenchmarkSessionStore store, IPresentMonCaptureService capture, IPerformanceCaptureService? performance = null)
    {
        InitializeComponent();
        model = new(store, capture, performance); DataContext = model;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BenchmarkViewModel.IsBusy)) BusyChanged?.Invoke(this, EventArgs.Empty);
        };
    }
    internal void ConfigureDesignPreview(ImportedCapture capture, PerformanceTrace? trace = null)
    {
        model.SetImport(capture);
        if (trace is not null) model.SetDesignTelemetry(trace);
    }
    private async void ImportCsv(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PresentMon CSV (*.csv)|*.csv", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) await model.RunAsync(t => model.ImportAsync(dialog.FileName, t));
    }
    private async void SelectExecutable(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PresentMon 2.6.0 x64 (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) await model.RunAsync(t => model.ConfigureExecutableAsync(dialog.FileName, t));
    }
    private async void OpenPresentMon(object sender, RoutedEventArgs e) => await model.RunAsync(_ =>
    {
        using var process = Process.Start(new ProcessStartInfo(PresentMonVersion.DownloadUrl) { UseShellExecute = true });
        return Task.CompletedTask;
    });
    private async void RefreshProcesses(object sender, RoutedEventArgs e) => await model.RunAsync(async token =>
    {
        var targets = await Task.Run(() =>
        {
            var list = new List<CaptureTarget>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero && !process.HasExited)
                            list.Add(new(process.Id, process.StartTime.ToUniversalTime(), process.ProcessName));
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { /* Protected processes cannot be capture targets. */ }
                }
            }
            return list.OrderBy(p => p.Name).ToArray();
        }, token);
        model.Targets.Clear(); foreach (var target in targets) model.Targets.Add(target);
    });
    private async void StartCapture(object sender, RoutedEventArgs e) => await model.RunAsync(model.CaptureAsync);
    private async void AnalyzePerformance(object sender, RoutedEventArgs e) => await model.RunAsync(model.AnalyzePerformanceAsync);
    private async void ExportReport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Informe Markdown (*.md)|*.md", DefaultExt = ".md", AddExtension = true, FileName = "rendimiento.md" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) await model.RunAsync(t => model.ExportReportAsync(dialog.FileName, t));
    }
    private void Cancel(object sender, RoutedEventArgs e) => model.Cancel();
    private async void SaveSession(object sender, RoutedEventArgs e) => await model.RunAsync(model.SaveAsync);
    private async void RefreshHistory(object sender, RoutedEventArgs e) => await model.RunAsync(model.RefreshHistoryAsync);
    private async void LoadSession(object sender, RoutedEventArgs e) => await model.RunAsync(model.LoadAsync);
    private async void SetBaseline(object sender, RoutedEventArgs e) => await model.RunAsync(_ => { model.SetBaseline(); return Task.CompletedTask; });
    private async void CompareSessions(object sender, RoutedEventArgs e) => await model.RunAsync(_ => { model.Compare(); return Task.CompletedTask; });
    private async void AddRepetition(object sender, RoutedEventArgs e) => await model.RunAsync(_ => { model.AddRepetition(Equals(((FrameworkElement)sender).Tag, "A")); return Task.CompletedTask; });
    private async void CompareRepetitions(object sender, RoutedEventArgs e) => await model.RunAsync(model.CompareRepetitionsAsync);
    private async void ClearRepetitions(object sender, RoutedEventArgs e) => await model.RunAsync(_ => { model.ClearRepetitions(); return Task.CompletedTask; });
    private async void ExportSession(object sender, RoutedEventArgs e)
    {
        var json = Equals(((FrameworkElement)sender).Tag, "json");
        var dialog = new SaveFileDialog { Filter = json ? "Sesión JSON (*.json)|*.json" : "Muestras CSV (*.csv)|*.csv", DefaultExt = json ? ".json" : ".csv", AddExtension = true, FileName = json ? "benchmark.json" : "benchmark.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) await model.RunAsync(t => model.ExportAsync(dialog.FileName, json, t));
    }
}
