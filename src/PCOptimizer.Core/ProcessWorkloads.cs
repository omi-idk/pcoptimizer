using System.Text;

namespace PCOptimizer.Core;

public sealed record ProcessCounter(GameIdentity Identity, long Qpc, long CpuTime100Ns, ulong? ReadBytes, ulong? WriteBytes);
public sealed record ProcessWorkloadCapture(int SchemaVersion, Guid Id, DateTimeOffset StartedUtc,
    long StartedQpc, long EndedQpc, long QpcFrequency, int LogicalProcessors,
    ProcessCounter[] Before, ProcessCounter[] After, int UnavailableBefore, int UnavailableAfter, string[] Limitations);
public enum WorkloadRole { ObservationOnly, SelectedGame, Collector, Browser, Recording, Communication }
public sealed record WorkloadContext(GameIdentity? Game, GameIdentity? Collector, bool Streaming, bool KeepVoiceChat);
public sealed record ProcessWorkloadRow(GameIdentity Identity, double? CpuCapacityPercent, double? CpuCorePercent,
    double? ReadBytesPerSecond, double? WriteBytesPerSecond, WorkloadRole Role, string Evidence, string Recommendation);

public static class ProcessWorkloads
{
    public const int MaximumProcesses = 4096;
    public static bool SameProcess(GameIdentity a, GameIdentity b) => a.ProcessId == b.ProcessId && a.StartedUtc == b.StartedUtc;

    public static void Validate(ProcessWorkloadCapture capture)
    {
        if (capture.SchemaVersion != 1 || capture.Id == Guid.Empty || capture.StartedQpc <= 0 || capture.EndedQpc <= capture.StartedQpc
            || capture.QpcFrequency is <= 0 or > 10_000_000_000 || capture.LogicalProcessors is < 1 or > 4096
            || (capture.EndedQpc - capture.StartedQpc) / (double)capture.QpcFrequency > 60
            || capture.UnavailableBefore < 0 || capture.UnavailableAfter < 0 || capture.Before is null || capture.After is null
            || capture.Before.Length > MaximumProcesses || capture.After.Length > MaximumProcesses
            || capture.Limitations is null || capture.Limitations.Length > 100 || capture.Limitations.Any(x => x is null || x.Length > 2000))
            throw new InvalidDataException("Captura de procesos inválida o fuera de límites.");
        foreach (var snapshot in new[] { capture.Before, capture.After })
        {
            var pids = new HashSet<int>();
            foreach (var row in snapshot)
                if (row is null || row.Identity is null || row.Identity.ProcessId <= 0 || row.Identity.StartedUtc == default
                    || string.IsNullOrWhiteSpace(row.Identity.Name) || row.Identity.Name.Length > 260 || row.Identity.Name.Any(char.IsControl)
                    || !pids.Add(row.Identity.ProcessId) || row.Qpc < capture.StartedQpc || row.Qpc > capture.EndedQpc || row.CpuTime100Ns < 0)
                    throw new InvalidDataException("Identidad o contadores de proceso inválidos.");
        }
        if (capture.Before.Length > 0 && capture.After.Length > 0 && capture.Before.Max(x => x.Qpc) >= capture.After.Min(x => x.Qpc))
            throw new InvalidDataException("Las instantáneas de procesos se superponen o están invertidas.");
    }
    public static ProcessWorkloadRow[] Analyze(ProcessWorkloadCapture capture, WorkloadContext context, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); Validate(capture);
        var before = capture.Before.ToDictionary(x => x.Identity.ProcessId);
        var after = capture.After.ToDictionary(x => x.Identity.ProcessId);
        var result = new List<ProcessWorkloadRow>();
        foreach (var current in capture.After)
        {
            token.ThrowIfCancellationRequested();
            double? cpu = null, core = null, read = null, write = null;
            var reason = "Proceso nuevo o sin dos lecturas de la misma identidad; no se supone consumo cero.";
            if (before.TryGetValue(current.Identity.ProcessId, out var old) && SameProcess(old.Identity, current.Identity))
            {
                var seconds = (current.Qpc - old.Qpc) / (double)capture.QpcFrequency;
                if (seconds >= .25 && current.CpuTime100Ns >= old.CpuTime100Ns)
                {
                    core = (current.CpuTime100Ns - old.CpuTime100Ns) / 10000000.0 / seconds * 100;
                    cpu = core / capture.LogicalProcessors;
                    if (cpu > 105) { cpu = null; core = null; }
                    read = Rate(old.ReadBytes, current.ReadBytes, seconds);
                    write = Rate(old.WriteBytes, current.WriteBytes, seconds);
                    reason = $"Ventana propia {seconds:F2} s; CPU = Δtiempo usuario+kernel / ΔQPC / {capture.LogicalProcessors} procesadores disponibles. CPU en unidades de procesador lógico puede superar 100%; no identifica núcleos físicos ni hilos individuales. E/S incluye transferencias de archivos, red y dispositivos; no equivale a disco ni tráfico del juego.";
                    if (cpu is null) reason += " CPU no disponible por contadores/normalización incompatibles; no se recorta a 100%.";
                }
                else reason = "Contador reiniciado o intervalo insuficiente; consumo no disponible.";
            }
            var role = Role(current.Identity, context);
            result.Add(new(current.Identity, cpu, core, read, write, role, reason, Advice(role, cpu, context)));
        }
        foreach (var old in capture.Before.Where(x => !after.TryGetValue(x.Identity.ProcessId, out var value) || !SameProcess(x.Identity, value.Identity)))
        {
            token.ThrowIfCancellationRequested();
            result.Add(new(old.Identity, null, null, null, null, Role(old.Identity, context), "Terminó, no se pudo observar al final o el PID cambió de identidad. No se calcula una tasa.", "Solo observación; no se realizan acciones sobre el proceso."));
        }
        return result.OrderByDescending(r => r.CpuCapacityPercent ?? -1).ThenBy(r => r.Identity.ProcessId).ToArray();
    }
    private static double? Rate(ulong? previous, ulong? current, double seconds) => previous is { } p && current is { } c && c >= p ? (c - p) / seconds : null;
    private static WorkloadRole Role(GameIdentity identity, WorkloadContext context)
    {
        if (context.Game is { } game && SameProcess(identity, game)) return WorkloadRole.SelectedGame;
        if (context.Collector is { } collector && SameProcess(identity, collector)) return WorkloadRole.Collector;
        // Labels are hints from image names, not proof of origin, trust or safe termination.
        return identity.Name.ToLowerInvariant() switch
        {
            "chrome" or "msedge" or "firefox" or "brave" => WorkloadRole.Browser,
            "obs64" or "obs32" or "streamlabs" => WorkloadRole.Recording,
            "discord" or "teams" => WorkloadRole.Communication,
            _ => WorkloadRole.ObservationOnly
        };
    }
    private static string Advice(WorkloadRole role, double? cpu, WorkloadContext context) => role switch
    {
        WorkloadRole.SelectedGame => "Juego elegido por PID e inicio; se conserva. No se cambia memoria, prioridad ni afinidad.",
        WorkloadRole.Collector => "Colector: su consumo se informa, pero esta lectura no mide overhead causal ni incluye auxiliares.",
        WorkloadRole.Recording when context.Streaming => "Grabación/transmisión necesaria según tu selección. Conserva la aplicación; revisa escena/codificador sin atribuirle stuttering automáticamente.",
        WorkloadRole.Communication when context.KeepVoiceChat => "Comunicación necesaria según tu selección; conservar. El nombre del ejecutable no verifica su identidad de producto.",
        WorkloadRole.Browser or WorkloadRole.Recording or WorkloadRole.Communication when cpu >= 5 => "Carga observada ≥5% de capacidad. Revisa voluntariamente si necesitas esta aplicación y prueba una sola variable A/B. No demuestra interferencia ni permite cerrarla desde aquí.",
        _ => "Solo observación. No se recomienda terminar procesos desconocidos, de Windows, seguridad, drivers ni anticheat."
    };
    public static string Report(ProcessWorkloadCapture capture, WorkloadContext context)
    {
        var rows = Analyze(capture, context);
        var text = new StringBuilder($"PCOptimizer · procesos · {capture.Id}\nUTC: {capture.StartedUtc:O}\nFuente: GetProcessTimes / GetProcessIoCounters / QPC\nProcesadores disponibles: {capture.LogicalProcessors}\nStreaming: {context.Streaming}; conservar voz: {context.KeepVoiceChat}\nNo observados o no legibles: {capture.UnavailableBefore} / {capture.UnavailableAfter}\n");
        text.AppendLine("Medición de dos instantáneas, sin atribución causal a frametimes. CPU total de capacidad y CPU en unidades de procesador lógico son escalas distintas. E/S no es exclusivamente disco. No hay cambios del sistema.");
        foreach (var row in rows)
            text.AppendLine($"\n{row.Identity.Name} · PID {row.Identity.ProcessId} · inicio {row.Identity.StartedUtc:O}\nCPU capacidad: {row.CpuCapacityPercent?.ToString("F2") ?? "N/D"}% · 1 lógico=100%: {row.CpuCorePercent?.ToString("F2") ?? "N/D"}%\nE/S lectura: {row.ReadBytesPerSecond?.ToString("F0") ?? "N/D"} B/s · escritura: {row.WriteBytesPerSecond?.ToString("F0") ?? "N/D"} B/s\n{row.Evidence}\n{row.Recommendation}");
        foreach (var limit in capture.Limitations) text.AppendLine(limit);
        return text.ToString();
    }
}
