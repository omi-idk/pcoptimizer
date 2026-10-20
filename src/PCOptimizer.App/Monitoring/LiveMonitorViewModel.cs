using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text.Json;
using PCOptimizer.App.Benchmarking;
using PCOptimizer.Benchmarking;

namespace PCOptimizer.App.Monitoring;

public sealed record LivePoint(double Seconds, double? Value);
internal sealed class LiveMonitorViewModel(Func<IProgress<SensorObservation>, IPerformanceCaptureService> factory,
    Func<bool> acquire, Action release) : INotifyPropertyChanged
{
    private CancellationTokenSource? cancellation;
    private Task? active;
    private long generation;
    private bool running;
    private string status = "Pulsa Iniciar para consultar CPU, RAM y sensores NVIDIA disponibles. Sin cambios del sistema ni sondeo al cerrar.";
    private SensorSeriesOption? selected;
    private readonly List<SensorObservation> observations = [];
    private long frequency = Stopwatch.Frequency, origin;
    public bool IsRunning { get => running; private set { running = value; Changed(); Changed(nameof(IsIdle)); } }
    public bool IsIdle => !IsRunning;
    public string Status { get => status; private set { status = value; Changed(); } }
    public SensorSeriesOption[] Series { get; private set; } = [];
    public SensorSeriesOption? Selected { get => selected; set { selected = value; Changed(); Refresh(); } }
    public LivePoint[] Points { get; private set; } = [];
    public string Value { get; private set; } = "N/D";
    public string Source { get; private set; } = "Cada dispositivo conserva su identidad de índice/nombre y fuente; no se asigna automáticamente al juego.";
    public PerformanceTrace? Trace { get; private set; }
    public Task StartAsync()
    {
        if (IsRunning) return active ?? Task.CompletedTask;
        return active = RunAsync();
    }
    private async Task RunAsync()
    {
        if (!acquire()) { Status = "Otra operación está en curso; espera antes de iniciar el monitor."; return; }
        using var cancel = new CancellationTokenSource(); cancellation = cancel;
        var current = ++generation;
        origin = Stopwatch.GetTimestamp(); frequency = Stopwatch.Frequency;
        observations.Clear(); Series = []; Selected = null; Trace = null;
        Changed(nameof(Series)); IsRunning = true; Status = "Monitor activo · intervalo solicitado 1 s; se detiene al pulsar Parar o después de 10 minutos. FPS se miden por separado con PresentMon.";
        var progress = new Progress<SensorObservation>(value =>
        {
            if (generation != current || cancel.IsCancellationRequested || !IsRunning) return;
            observations.Add(value); if (observations.Count > 240) observations.RemoveAt(0);
            RefreshSeries(); Refresh();
        });
        try
        {
            var result = await factory(progress).CaptureAsync(async token =>
            {
                try { await Task.Delay(TimeSpan.FromMinutes(10), token); } catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                return ""; // Sensor-only capture: no CSV, PresentMon helper or game process is started.
            }, cancel.Token);
            Trace = result.Trace; frequency = Trace.QpcFrequency; origin = Trace.StartedQpc;
            observations.Clear(); observations.AddRange(Trace.Observations.TakeLast(240)); RefreshSeries(); Refresh();
            Status = "Monitor detenido; la traza completa se conserva para exportar. " + string.Join(" ", Trace.Limitations);
        }
        catch (OperationCanceledException) { Status = "Monitor cancelado."; }
        catch (Exception ex) { Status = "No se completó el monitor: " + ex.Message; }
        finally { generation++; cancellation = null; IsRunning = false; release(); }
    }
    public void Stop() => cancellation?.Cancel();
    public async Task StopAsync() { Stop(); if (active is { } task) await task; }
    private void RefreshSeries()
    {
        var series = observations.SelectMany(o => o.Readings).Select(r => new SensorSeriesOption(r.Metric, r.Device, r.Source, r.Unit)).Distinct().ToArray();
        if (Series.SequenceEqual(series)) return;
        var previous = Selected; Series = series; Changed(nameof(Series));
        Selected = series.FirstOrDefault(s => s == previous) ?? series.FirstOrDefault();
    }
    private void Refresh()
    {
        var metric = Selected;
        var values = metric is null ? [] : observations.SelectMany(o => o.Readings
            .Where(r => r.Metric == metric.Metric && r.Device == metric.Device && r.Source == metric.Source).Select(r => (Observation: o, Reading: r))).TakeLast(120).ToArray();
        Points = values.Select(v => new LivePoint((v.Observation.StartQpc + (v.Observation.EndQpc - v.Observation.StartQpc) / 2 - origin) / (double)frequency,
            v.Reading.Availability == SensorAvailability.Available && (v.Observation.EndQpc - v.Observation.StartQpc) / (double)frequency <= .25 ? v.Reading.Value : null)).ToArray();
        var last = values.LastOrDefault();
        var stale = IsRunning && last.Observation is { } observed && (Stopwatch.GetTimestamp() - observed.EndQpc) / (double)frequency > 5;
        Value = stale ? "N/D · lectura antigua" : Points.LastOrDefault()?.Value is { } number ? $"{number:N1} {metric!.Unit}" : "N/D";
        Source = last.Reading is { } reading ? $"{reading.Source} · {reading.Device} · UTC {last.Observation.ObservedUtc:O}\n{reading.Detail}\n" +
            (reading.AggregationSeconds is { } seconds ? $"Ventana de agregación declarada: {seconds:F2} s." : "Ventana de agregación del proveedor desconocida.") : "Sin lecturas de la serie seleccionada.";
        Changed(nameof(Points)); Changed(nameof(Value)); Changed(nameof(Source)); Changed(nameof(Trace));
    }
    internal void RefreshAge() => Refresh();
    internal void SetDesignTrace(PerformanceTrace trace)
    { Trace = trace; frequency = trace.QpcFrequency; origin = trace.StartedQpc; observations.Clear(); observations.AddRange(trace.Observations); RefreshSeries(); Refresh(); }
    public async Task ExportAsync(string path, CancellationToken token)
    {
        var trace = Trace ?? throw new InvalidOperationException("Detén una captura completa antes de exportar."); PerformanceTrace.Validate(trace);
        var temp = Path.Combine(Path.GetDirectoryName(path)!, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            { await JsonSerializer.SerializeAsync(file, trace, new JsonSerializerOptions { WriteIndented = true, MaxDepth = 16 }, token); if (file.Length > PresentMonCsv.MaximumFileBytes) throw new InvalidDataException("Traza mayor de 64 MiB."); await file.FlushAsync(token); file.Flush(true); }
            token.ThrowIfCancellationRequested(); File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Status = "Traza de sensores exportada; no incluye FPS ni atribuye cuellos de botella. Revisa los datos antes de compartirlos.";
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
