using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using PCOptimizer.Benchmarking;

namespace PCOptimizer.App.Benchmarking;

internal sealed class BenchmarkViewModel(IBenchmarkSessionStore store, IPresentMonCaptureService capture, IPerformanceCaptureService? performance = null) : INotifyPropertyChanged
{
    private ImportedCapture? imported;
    private PerformanceTrace? telemetry;
    public bool CaptureSensors { get; set; }
    public PerformanceReport? PerformanceReport { get; private set; }
    public PerformanceTrace? Telemetry => telemetry;
    public string PerformanceEvidence => PerformanceReport is not { } report ? "Activa sensores durante una captura nueva para analizar coincidencias. Las importaciones antiguas conservan sus métricas sin sincronización inventada." :
        string.Join("\n\n", report.Findings.Select(f => $"{f.Finding}\nEvidencia: {f.Evidence}\nHipótesis: {f.Hypothesis}\nConfianza: {f.Confidence}\nFalta: {f.MissingInformation}\nRecomendación: {f.Recommendation}\nRiesgo: {f.Risk}\nPrueba: {f.Experiment}"));
    public string PerformanceCoverage => PerformanceReport is not { } r ? "Sin experimento correlacionado." :
        $"{r.TimedFrames}/{r.Frames} frames con QPC · {r.Spikes} spikes >{r.SpikeThresholdMs:F2} ms · {r.Associations.Length} series. " +
        string.Join(" · ", r.Associations.Select(a => $"{a.Device}/{a.Metric}: {a.MatchedFrames}/{r.Frames} frames, {a.MatchedSpikes}/{r.Spikes} spikes cubiertos"));
    public string PerformanceLimits => PerformanceReport is { } r ? string.Join("\n", r.Limitations) : "No se modifica ninguna configuración durante las mediciones.";
    private CaptureStream? selectedStream;
    private BenchmarkSession? current;
    private BenchmarkSession? baseline;
    private readonly List<BenchmarkSession> repetitionsA = [];
    private readonly List<BenchmarkSession> repetitionsB = [];
    public string RepetitionCounts => $"Repeticiones independientes · A: {repetitionsA.Count} · B: {repetitionsB.Count}";
    private bool busy;
    private bool captureExpanded = true;
    public bool CaptureExpanded { get => captureExpanded; set { captureExpanded = value; Changed(); } }
    private string status = "Importa un CSV de PresentMon o configura una captura manual. No se modifica el juego ni Windows.";
    private string executable = "";
    private CancellationTokenSource? operation;
    public ObservableCollection<CaptureStream> Streams { get; } = [];
    public ObservableCollection<SessionReference> History { get; } = [];
    public ObservableCollection<CaptureTarget> Targets { get; } = [];
    public CaptureTarget? SelectedTarget { get; set; }
    public SessionReference? SelectedHistory { get; set; }
    public int DurationSeconds { get; set; } = 60;
    public string Hardware { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public string DriverVersion { get; set; } = "";
    public string WindowsVersion { get; set; } = Environment.OSVersion.VersionString;
    public string Settings { get; set; } = "";
    public string Scenario { get; set; } = "";
    public string Executable { get => executable; private set { executable = value; Changed(); } }
    public string Capability { get; private set; } = "Captura: selecciona el auxiliar oficial 2.6.0 x64. No viene incluido ni se descarga automáticamente.";
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(IsIdle)); } }
    public bool IsIdle => !IsBusy;
    public bool ContextReadOnly => current is not null;
    public string Status { get => status; private set { status = value; Changed(); } }
    public CaptureStream? SelectedStream
    {
        get => selectedStream;
        set
        {
            selectedStream = value; Changed();
            current = null; Summary = null; PerformanceReport = null;
            Frames = value is not null && imported is not null ? imported.Streams[value] : [];
            if (Frames.Length >= 2) Summary = BenchmarkStatistics.Calculate(Frames);
            NotifyResult();
        }
    }
    public FrameSample[] Frames { get; private set; } = [];
    public BenchmarkSummary? Summary { get; private set; }
    public string StreamLabel => SelectedStream?.ToString() ?? "Sin captura seleccionada";
    public string Average => Summary is { } s ? $"{s.AverageFps:N1} FPS" : "—";
    public string Low1 => Summary is { } s ? $"{s.Low1Fps:N1} FPS" : "—";
    public string Low01 => Summary is { } s ? $"{s.Low01Fps:N1} FPS" : "—";
    public string Percentiles => Summary is { } s ? $"p50 {s.MedianMs:N2} · p95 {s.P95Ms:N2} · p99 {s.P99Ms:N2} ms" : "—";
    public string Coverage => Summary is { } s ? $"{s.Samples:N0} intervalos · suma {s.ObservedIntervalSeconds:N2} s · Presentación; no mide FPS de pantalla ni latencia de entrada." +
        (s.Samples < 1000 ? " Muestra corta para 0.1% low." : "") : "Sin mediciones. Importa una captura o inicia una sesión manual.";
    public string Evidence => Summary is { } s ?
        $"{s.Samples:N0} intervalos · suma {s.ObservedIntervalSeconds:N2} s · FPS instantáneos medianos {s.MedianInstantaneousFps:N1}\n" +
        $"Desviación {s.StandardDeviationMs:N2} ms · {s.Spikes} spikes (>máx[50 ms, 2×mediana])\n" +
        $"CPU Busy: {s.AverageCpuBusyMs?.ToString("N2") ?? "N/D"} ms ({s.CpuBusySamples} muestras) · GPU Busy: {s.AverageGpuBusyMs?.ToString("N2") ?? "N/D"} ms ({s.GpuBusySamples} muestras)\n" +
        $"Filas mostradas: {s.DisplayedRows?.ToString() ?? "N/D"} · no mostradas: {s.NotDisplayedRows?.ToString() ?? "N/D"} · con FrameType: {s.FrameTypeRows}\n" +
        $"Intervalos iniciales/cero/NA omitidos en el archivo: {imported?.MissingIntervals ?? current?.MissingIntervals ?? 0}. No se eliminaron outliers. " +
        (s.Samples < 1000 ? "Muestra insuficiente para un 0.1% low estable. " : "") +
        "Son intervalos de presentación, no FPS de pantalla ni latencia de entrada. Frame Generation y múltiples cadenas requieren interpretar su procedencia." :
        "Aún no hay mediciones. Las métricas ausentes permanecerán como N/D.";
    public string Comparison { get; private set; } = "Carga o importa una sesión A y fíjala como referencia; luego carga B. Completa el mismo contexto para comparar.";

    public async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        using var source = new CancellationTokenSource(); operation = source;
        try { await action(source.Token); }
        catch (OperationCanceledException) { Status = "Operación cancelada. Las sesiones guardadas se conservan; una captura interrumpida puede dejar CSV parcial sin importar."; }
        catch (Exception ex) { Status = "No se completó: " + ex.Message; }
        finally { operation = null; IsBusy = false; }
    }
    public void Cancel() => operation?.Cancel();
    public async Task ImportAsync(string path, CancellationToken token)
    {
        Status = "Leyendo y validando PresentMon…";
        var result = await Task.Run(() => PresentMonCsv.ImportAsync(path, token), token);
        SetImport(result);
        Status = $"CSV verificado: {Streams.Count} streams. Selecciona uno; los procesos y swapchains permanecen separados. Completa el contexto y guarda la sesión.";
    }
    internal void SetImport(ImportedCapture result)
    {
        telemetry = null;
        PerformanceReport = null;
        imported = result;
        CaptureExpanded = false;
        Streams.Clear();
        foreach (var stream in result.Streams.Keys) Streams.Add(stream);
        SelectedStream = Streams.Count == 1 ? Streams[0] : null;
    }
    internal void SetDesignTelemetry(PerformanceTrace trace)
    {
        telemetry = trace;
        PerformanceReport = PerformanceAnalysis.Calculate(BuildSession());
        NotifyResult();
    }
    public async Task ConfigureExecutableAsync(string path, CancellationToken token)
    {
        var result = await capture.CheckAsync(path, token);
        Capability = result.Detail; Changed(nameof(Capability));
        Executable = result.State == CapabilityState.ValidationRequired ? path : "";
        Status = result.Detail;
    }
    public async Task CaptureAsync(CancellationToken token)
    {
        if (SelectedTarget is null || string.IsNullOrEmpty(Executable)) throw new InvalidOperationException("Selecciona PresentMon verificado y un proceso en ejecución.");
        Status = $"Capturando {SelectedTarget.Name} durante {DurationSeconds} s. Puedes detener la captura; no cierres la app hasta terminar.";
        var target = SelectedTarget;
        PerformanceTrace? capturedTrace = null;
        string output;
        if (CaptureSensors)
        {
            if (performance is null) throw new InvalidOperationException("Proveedor de telemetría no configurado.");
            var result = await performance.CaptureAsync(t => capture.CaptureAsync(Executable, target, DurationSeconds, t), token);
            output = result.CsvPath; capturedTrace = result.Trace with { TargetProcessId = target.Id, TargetStartUtc = target.StartTimeUtc };
        }
        else output = await capture.CaptureAsync(Executable, target, DurationSeconds, token);
        await ImportAsync(output, token.IsCancellationRequested ? CancellationToken.None : token);
        telemetry = capturedTrace;
        await AnalyzePerformanceAsync(token.IsCancellationRequested ? CancellationToken.None : token);
        if (capturedTrace is not null) Status = "Captura con sensores terminada. Revisa cobertura y hallazgos, completa el contexto y guarda para reabrir el experimento.";
        if (token.IsCancellationRequested) Status = "Captura detenida por el usuario. Se importaron los intervalos completados; no equivale a una captura de la duración solicitada.";
    }
    private BenchmarkSession BuildSession() => current ?? (imported is not null && SelectedStream is not null
        ? new(1, Guid.NewGuid(), DateTimeOffset.UtcNow, imported.Sha256, SelectedStream,
            new(Hardware.Trim(), GameVersion.Trim(), DriverVersion.Trim(), WindowsVersion.Trim(), Settings.Trim(), Scenario.Trim()), Frames, imported.MissingIntervals) { Telemetry = telemetry }
        : throw new InvalidOperationException("Selecciona un stream con datos antes de continuar."));
    public async Task SaveAsync(CancellationToken token)
    {
        if (current is not null) { Status = "Esta sesión ya está guardada. Importa una captura nueva para otra sesión."; return; }
        var session = BuildSession();
        await Task.Run(() => store.SaveAsync(session, token), token);
        current = session; Changed(nameof(ContextReadOnly));
        await RefreshHistoryAsync(token);
        Status = "Sesión guardada con procedencia SHA-256 y contexto. El contexto vacío impide comparar resultados como equivalentes.";
    }
    public async Task RefreshHistoryAsync(CancellationToken token)
    {
        var entries = await Task.Run(() => store.ListAsync(token), token);
        History.Clear(); foreach (var entry in entries) History.Add(entry);
    }
    public async Task LoadAsync(CancellationToken token)
    {
        if (SelectedHistory is null) throw new InvalidOperationException("Selecciona una sesión del historial.");
        var session = await Task.Run(() => store.LoadAsync(SelectedHistory.Id, token), token);
        var report = await Task.Run(() => PerformanceAnalysis.Calculate(session, token), token);
        token.ThrowIfCancellationRequested();
        SetImport(new(session.SourceSha256, new Dictionary<CaptureStream, FrameSample[]> { [session.Stream] = session.Frames }, session.MissingIntervals));
        current = session; telemetry = session.Telemetry; PerformanceReport = report;
        Hardware = session.Context.Hardware; GameVersion = session.Context.GameVersion; DriverVersion = session.Context.DriverVersion;
        WindowsVersion = session.Context.WindowsVersion; Settings = session.Context.Settings; Scenario = session.Context.Scenario;
        foreach (var name in new[] { nameof(Hardware), nameof(GameVersion), nameof(DriverVersion), nameof(WindowsVersion), nameof(Settings), nameof(Scenario) }) Changed(name);
        NotifyResult(); Status = "Sesión cargada; sus datos y contexto son inmutables. El historial muestra la fecha de guardado local.";
    }
    public async Task AnalyzePerformanceAsync(CancellationToken token)
    {
        var session = BuildSession();
        var result = await Task.Run(() => PerformanceAnalysis.Calculate(session, token), token);
        PerformanceReport = result;
        foreach (var name in new[] { nameof(Telemetry), nameof(PerformanceReport), nameof(PerformanceEvidence), nameof(PerformanceCoverage), nameof(PerformanceLimits) }) Changed(name);
    }
    public async Task ExportReportAsync(string path, CancellationToken token)
    {
        var session = BuildSession();
        var report = await Task.Run(() => PerformanceAnalysis.Calculate(session, token), token);
        var temp = Path.Combine(Path.GetDirectoryName(path)!, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temp, PerformanceAnalysis.ToMarkdown(session, report), token);
            token.ThrowIfCancellationRequested(); File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Status = "Informe exportado con evidencia, cobertura, contexto y límites. No demuestra causalidad ni mejora.";
    }
    public void SetBaseline()
    {
        baseline = BuildSession(); Comparison = $"Referencia A: {baseline.Stream}. Importa/carga B y conserva el contexto del escenario."; Changed(nameof(Comparison));
    }
    public void Compare()
    {
        if (baseline is null) throw new InvalidOperationException("Fija primero una referencia A.");
        Comparison = BenchmarkStatistics.Compare(baseline, BuildSession()); Changed(nameof(Comparison));
    }
    public void AddRepetition(bool groupA)
    {
        var session = BuildSession();
        if (repetitionsA.Concat(repetitionsB).Any(s => s.SourceSha256 == session.SourceSha256 && s.Stream == session.Stream))
            throw new InvalidOperationException("Este stream ya está en la comparación. Importa otra captura independiente.");
        var list = groupA ? repetitionsA : repetitionsB;
        if (list.Count >= 30) throw new InvalidOperationException("Máximo 30 repeticiones por grupo.");
        list.Add(session); Changed(nameof(RepetitionCounts));
        Comparison = "Repetición añadida con su contexto actual. Alterna A/B y cambia solo una variable. Añade al menos 3 por grupo."; Changed(nameof(Comparison));
    }
    public void ClearRepetitions()
    {
        repetitionsA.Clear(); repetitionsB.Clear(); Changed(nameof(RepetitionCounts));
        Comparison = "Comparación de repeticiones vaciada. Las sesiones guardadas se conservan."; Changed(nameof(Comparison));
    }
    public async Task CompareRepetitionsAsync(CancellationToken token)
    {
        var result = await Task.Run(() => RepeatedComparison.Calculate(repetitionsA, repetitionsB), token);
        Comparison = result.MeanFpsA is { } a && result.MeanFpsB is { } b
            ? $"Media por repetición A: {a:N2} FPS; B: {b:N2} FPS. B−A: {result.DifferenceFps:N2} FPS. " +
              (result.Lower95Fps is { } lo && result.Upper95Fps is { } hi ? $"IC 95% aproximado: [{lo:N2}, {hi:N2}] FPS. " : "") + result.Conclusion
            : result.Conclusion;
        Changed(nameof(Comparison));
    }
    public async Task ExportAsync(string path, bool json, CancellationToken token)
    {
        var session = BuildSession();
        // Write to a sibling temporary file: cancellation never truncates an existing export.
        var temp = Path.Combine(Path.GetDirectoryName(path)!, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                if (json) await BenchmarkExport.JsonAsync(file, session, token); else await BenchmarkExport.CsvAsync(file, session, token);
                await file.FlushAsync(token); file.Flush(true);
            }
            token.ThrowIfCancellationRequested(); File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Status = "Exportación terminada. JSON incluye contexto y definición; CSV contiene las muestras del stream seleccionado.";
    }
    private void NotifyResult()
    {
        foreach (var name in new[] { nameof(ContextReadOnly), nameof(Frames), nameof(Summary), nameof(StreamLabel), nameof(Average), nameof(Low1), nameof(Low01), nameof(Percentiles), nameof(Coverage), nameof(Evidence), nameof(Telemetry), nameof(PerformanceReport), nameof(PerformanceEvidence), nameof(PerformanceCoverage), nameof(PerformanceLimits) }) Changed(name);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
