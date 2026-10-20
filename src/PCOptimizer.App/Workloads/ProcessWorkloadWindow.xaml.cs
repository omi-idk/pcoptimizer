using System.Windows;
using Microsoft.Win32;
using PCOptimizer.Core;

namespace PCOptimizer.App.Workloads;

public partial class ProcessWorkloadWindow : Window
{
    private readonly ProcessWorkloadViewModel model;
    internal bool IsBusy => model.IsBusy;
    internal event EventHandler? BusyChanged;
    public SavedTweakProfile? SelectedPlan { get; private set; }
    internal ProcessWorkloadWindow(IProcessWorkloadProbe probe, Func<bool>? acquire = null, Action? release = null)
    {
        InitializeComponent(); DataContext = model = new(probe, acquire, release);
        model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(model.IsBusy)) BusyChanged?.Invoke(this, EventArgs.Empty); };
        Closing += (_, e) => { if (model.IsBusy) { e.Cancel = true; model.Cancel(); } };
    }
    internal void ConfigureDesignPreview(ProcessWorkloadCapture capture) => model.SetCapture(capture, null, null);
    private async void Capture(object sender, RoutedEventArgs e) => await model.RunAsync(model.CaptureAsync);
    private void Cancel(object sender, RoutedEventArgs e) => model.Cancel();
    private void SelectPlan(object sender, RoutedEventArgs e) { SelectedPlan = model.Plan.Tweaks; DialogResult = true; }
    private async void Save(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Captura de procesos (*.json)|*.json", FileName = "procesos.json", DefaultExt = ".json", AddExtension = true };
        if (dialog.ShowDialog(this) == true) await model.RunAsync(t => model.SaveAsync(dialog.FileName, t));
    }
    private async void Load(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Captura de procesos (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) await model.RunAsync(t => model.LoadAsync(dialog.FileName, t));
    }
    private async void Export(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Informe JSON (*.json)|*.json", FileName = "informe-procesos.json", DefaultExt = ".json", AddExtension = true };
        if (dialog.ShowDialog(this) == true) await model.RunAsync(t => model.ExportReportAsync(dialog.FileName, t));
    }
}
