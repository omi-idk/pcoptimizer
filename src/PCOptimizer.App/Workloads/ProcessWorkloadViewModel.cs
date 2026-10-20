using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using PCOptimizer.Core;

namespace PCOptimizer.App.Workloads;

public sealed record GoalOption(GamingGoal Value, string Label);
public sealed record WorkloadItem(ProcessWorkloadRow Data)
{
    public string Name => Data.Identity.Name;
    public int Pid => Data.Identity.ProcessId;
    public string Cpu => Data.CpuCapacityPercent is { } value ? $"{value:N2}%" : "N/D";
    public string Core => Data.CpuCorePercent is { } value ? $"{value:N2}%" : "N/D";
    public string Read => Data.ReadBytesPerSecond is { } value ? $"{value / 1024:N1}" : "N/D";
    public string Write => Data.WriteBytesPerSecond is { } value ? $"{value / 1024:N1}" : "N/D";
    public override string ToString() => $"{Name} · PID {Pid} · inicio {Data.Identity.StartedUtc.ToLocalTime():T}";
}
internal sealed class ProcessWorkloadViewModel(IProcessWorkloadProbe probe, Func<bool>? acquire = null, Action? release = null) : INotifyPropertyChanged
{
    public GoalOption[] Goals { get; } = [new(GamingGoal.Balanced, "Equilibrado"), new(GamingGoal.Competitive, "Competitivo"), new(GamingGoal.Streaming, "Gaming + streaming"), new(GamingGoal.Laptop, "Laptop gaming")];
    private ProcessWorkloadCapture? capture;
    private GameIdentity? collector;
    private CancellationTokenSource? operation;
    private WorkloadItem? selected;
    private GameIdentity? game;
    private GamingGoal goal = GamingGoal.Balanced;
    private bool voice = true, clips = true, busy;
    private string status = "Captura voluntaria de ~2–10 s. Se consultan metadatos accesibles; no se modifican procesos ni Windows.";
    public ObservableCollection<WorkloadItem> Rows { get; } = [];
    public ObservableCollection<GameIdentity> Games { get; } = [];
    public GameIdentity? Game { get => game; set { if (Equals(game, value)) return; game = value; Changed(); Refresh(); } }
    public GamingGoal Goal { get => goal; set { goal = value; Changed(); Refresh(); } }
    public bool KeepVoice { get => voice; set { voice = value; Changed(); Refresh(); } }
    public bool UsesClips { get => clips; set { clips = value; Changed(); Changed(nameof(Plan)); } }
    public GamingGoalPlan Plan => GamingGoals.Plan(Goal, UsesClips);
    public WorkloadItem? Selected { get => selected; set { selected = value; Changed(); Changed(nameof(Details)); } }
    public string Details => Selected is { } item ? $"{item}\n{item.Data.Evidence}\n{item.Data.Recommendation}" : "Elige una fila para ver evidencia, disponibilidad y recomendación.";
    public string Coverage => capture is { } c ? $"{c.Before.Length} / {c.After.Length} procesos legibles · no observados: {c.UnavailableBefore} / {c.UnavailableAfter} · {c.LogicalProcessors} procesadores disponibles · {c.StartedUtc.ToLocalTime():g}" : "Sin mediciones. N/D significa sin dos observaciones comparables; no consumo cero.";
    public string Status { get => status; private set { status = value; Changed(); } }
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(IsIdle)); } }
    public bool IsIdle => !IsBusy;
    public void Cancel() => operation?.Cancel();
    public async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        if (acquire is not null && !acquire()) { Status="Hay otra operación en curso. Espera a que termine antes de medir o gestionar capturas."; return; }
        IsBusy = true; using var cancel = new CancellationTokenSource(); operation = cancel;
        try { await action(cancel.Token); }
        catch (OperationCanceledException) { Status = "Cancelado; se conserva el resultado anterior. No se publicaron datos parciales."; }
        catch (Exception ex) { Status = "No se completó: " + ex.Message; }
        finally { operation = null; IsBusy = false; release?.Invoke(); }
    }
    public async Task CaptureAsync(CancellationToken token)
    {
        Status = "Leyendo contadores de procesos en dos instantáneas…";
        var result = await probe.CaptureAsync(token); token.ThrowIfCancellationRequested();
        ProcessWorkloads.Validate(result);
        using var self = Process.GetCurrentProcess();
        var own = result.After.Select(r => r.Identity).FirstOrDefault(x => x.ProcessId == self.Id && x.StartedUtc == self.StartTime.ToUniversalTime());
        SetCapture(result, own, null);
        Status = "Captura terminada. Selecciona voluntariamente el juego para distinguirlo de otras aplicaciones; esto no demuestra qué causa sus frametimes.";
    }
    internal void SetCapture(ProcessWorkloadCapture value, GameIdentity? own, GameIdentity? target)
    {
        capture = value; collector = own; game = target;
        Games.Clear(); foreach (var identity in value.After.Concat(value.Before).Select(r => r.Identity).DistinctBy(x => (x.ProcessId, x.StartedUtc)).OrderBy(x => x.Name)) Games.Add(identity);
        game = target is null ? null : Games.FirstOrDefault(x => ProcessWorkloads.SameProcess(x, target)); Changed(nameof(Game)); Refresh();
    }
    private WorkloadContext Context => new(Game, collector, Goal == GamingGoal.Streaming, KeepVoice);
    private void Refresh()
    {
        if (capture is not null)
        {
            var identity = Selected?.Data.Identity;
            var rows = ProcessWorkloads.Analyze(capture, Context);
            Rows.Clear(); foreach (var row in rows) Rows.Add(new(row));
            Selected = Rows.FirstOrDefault(x => identity is not null && ProcessWorkloads.SameProcess(x.Data.Identity, identity)) ?? Rows.FirstOrDefault();
        }
        Changed(nameof(Coverage)); Changed(nameof(Plan));
    }
    public WorkloadExperiment Experiment()
    {
        var value = new WorkloadExperiment(1, capture ?? throw new InvalidOperationException("Realiza una captura primero."), Context, Goal, UsesClips);
        WorkloadExperiment.Validate(value); return value;
    }
    public async Task SaveAsync(string path, CancellationToken token)
    {
        var value = Experiment();
        await Task.Run(() => { token.ThrowIfCancellationRequested(); LocalJsonFile.Write(path, value); }, token);
        Status = "Captura y contexto guardados. El archivo puede incluir nombres de procesos; revísalo antes de compartirlo.";
    }
    public async Task LoadAsync(string path, CancellationToken token)
    {
        var value = await Task.Run(() => { token.ThrowIfCancellationRequested(); var result = LocalJsonFile.Read<WorkloadExperiment>(path) ?? throw new InvalidDataException("Archivo vacío."); WorkloadExperiment.Validate(result); return result; }, token);
        token.ThrowIfCancellationRequested(); goal = value.Goal; voice = value.Context.KeepVoiceChat; clips = value.UsesGameBarCapture;
        SetCapture(value.Capture, value.Context.Collector, value.Context.Game);
        Changed(nameof(Goal)); Changed(nameof(KeepVoice)); Changed(nameof(UsesClips));
        Status = "Captura reabierta con su contexto original. No se consultaron ni modificaron los procesos actuales.";
    }
    public async Task ExportReportAsync(string path, CancellationToken token)
    {
        var value = Experiment(); var report = ProcessWorkloads.Report(value.Capture, value.Context); var plan = Plan;
        await Task.Run(() => { token.ThrowIfCancellationRequested(); LocalJsonFile.Write(path, new { Report = report, Goal = plan.Explanation, Validation = plan.Validation }); }, token);
        Status = "Informe JSON exportado con evidencia, contexto y límites.";
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
