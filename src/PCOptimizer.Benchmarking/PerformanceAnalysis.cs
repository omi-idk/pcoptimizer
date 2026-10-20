using System.Globalization;
using System.Text;

namespace PCOptimizer.Benchmarking;

public sealed record PerformanceFinding(string Finding, string Evidence, string Hypothesis, string Confidence,
    string MissingInformation, string Recommendation, string Risk, string Experiment);
public sealed record SensorAssociation(SensorMetric Metric, string Device, string Source, int MatchedFrames,
    int MatchedSpikes, int HighSpikes, int HighNormalFrames, int NormalFrames, int DistinctSpikeObservations);
public sealed record PerformanceReport(int Frames, int TimedFrames, int Spikes, double SpikeThresholdMs,
    SensorAssociation[] Associations, PerformanceFinding[] Findings, string[] Limitations);

public static class PerformanceAnalysis
{
    public const double MaximumDistanceSeconds = .5;
    public const double MaximumReadSeconds = .25;

    public static bool IsUsable(SensorObservation observation, SensorReading reading, long frequency) =>
        reading.Availability == SensorAvailability.Available
        && (observation.EndQpc - observation.StartQpc) / (double)frequency <= MaximumReadSeconds
        && reading.AggregationSeconds is not > 1;

    public static PerformanceReport Calculate(BenchmarkSession session, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        BenchmarkSessionStore.Validate(session);
        var summary = BenchmarkStatistics.Calculate(session.Frames);
        var threshold = Math.Max(50, 2 * summary.MedianMs);
        var timed = session.Frames.Where(f => f.CpuStartQpc.HasValue).ToArray();
        var associations = new List<SensorAssociation>();
        var findings = new List<PerformanceFinding>();
        var limits = new List<string>
        {
            "Coincidencia temporal observacional; no demuestra causalidad, cuello de botella ni beneficio de un ajuste.",
            "Anclaje: inicio CPU del frame, no instante de presentación, pantalla ni entrada. El intervalo entre presents y el inicio CPU no son el mismo evento; el desfase limita la atribución de cada spike. NVML puede promediar una ventana interna no conocida.",
            "Sensores sin interpolación: lectura ≤250 ms y centro a ≤500 ms. Agregaciones conocidas >1 s se omiten. Huecos, lecturas lentas y valores ausentes no se rellenan.",
            "CPU total y memoria son del sistema; cada GPU se conserva separada y no se presupone que renderice este juego. Temperatura sola no demuestra throttling."
        };
        if (new[] { session.Context.Hardware, session.Context.GameVersion, session.Context.DriverVersion, session.Context.WindowsVersion, session.Context.Settings, session.Context.Scenario }.Any(string.IsNullOrWhiteSpace))
            limits.Add("Contexto incompleto: registra hardware, juego, driver, Windows, ajustes y escenario para reproducir el experimento.");
        if (session.Telemetry is not { } trace || timed.Length < 2 || trace.TargetProcessId is { } targetPid && targetPid != session.Stream.ProcessId)
        {
            limits.Add("No hay sensores del mismo experimento/PID o timestamps CPUStartQPC suficientes. No se reconstruye el tiempo sumando intervalos.");
        }
        else
        {
            limits.AddRange(trace.Limitations);
            var previous = 0L;
            foreach (var frame in timed)
            {
                token.ThrowIfCancellationRequested();
                if (frame.CpuStartQpc < previous) throw new InvalidDataException("El stream contiene timestamps QPC decrecientes.");
                previous = frame.CpuStartQpc!.Value;
            }
            var lanes = trace.Observations.SelectMany(o => o.Readings.Select(r => (Observation: o, Reading: r)))
                .GroupBy(x => (x.Reading.Metric, x.Reading.Device, x.Reading.Source));
            foreach (var lane in lanes)
            {
                token.ThrowIfCancellationRequested();
                var samples = lane.ToArray();
                // Search is O(frames * log(samples)); never compare every frame with every observation.
                var centers = samples.Select(x => x.Observation.StartQpc + (x.Observation.EndQpc - x.Observation.StartQpc) / 2).ToArray();
                int matched = 0, spikes = 0, high = 0, highNormal = 0, normal = 0;
                var independent = new HashSet<int>();
                foreach (var frame in timed)
                {
                    token.ThrowIfCancellationRequested();
                    var qpc = frame.CpuStartQpc!.Value;
                    if (qpc < trace.StartedQpc || qpc > trace.EndedQpc || samples.Length == 0) continue;
                    var index = Array.BinarySearch(centers, qpc);
                    if (index < 0)
                    {
                        index = ~index;
                        if (index == samples.Length || index > 0 && qpc - centers[index - 1] <= centers[index] - qpc) index--;
                    }
                    if (Math.Abs((double)qpc - centers[index]) / trace.QpcFrequency > MaximumDistanceSeconds
                        || !IsUsable(samples[index].Observation, samples[index].Reading, trace.QpcFrequency)) continue;
                    matched++;
                    var elevated = lane.Key.Metric is SensorMetric.CpuTotalPercent or SensorMetric.MemoryUsedPercent or SensorMetric.GpuUtilizationPercent
                        && samples[index].Reading.Value >= 90;
                    if (frame.IntervalMs > threshold)
                    {
                        spikes++;
                        if (elevated) { high++; independent.Add(index); }
                    }
                    else { normal++; if (elevated) highNormal++; }
                }
                associations.Add(new(lane.Key.Metric, lane.Key.Device, lane.Key.Source, matched, spikes, high, highNormal, normal, independent.Count));
                // Low confidence deliberately: sampled system/vendor telemetry is not process-causal evidence.
                if (timed.Length >= session.Frames.Length * .8 && matched >= session.Frames.Length * .8
                    && spikes >= 3 && spikes >= summary.Spikes * .8 && normal >= 10 && independent.Count >= 3
                    && high / (double)spikes >= .8 && highNormal / (double)normal <= .6
                    && high / (double)spikes - highNormal / (double)normal >= .2)
                {
                    findings.Add(new($"Coincidencia de spikes y {lane.Key.Metric} ≥90% · {lane.Key.Device}",
                        $"{matched}/{session.Frames.Length} frames cubiertos; {high}/{spikes} spikes con carga alta, frente a {highNormal}/{normal} frames normales. {independent.Count} observaciones distintas.",
                        "La actividad de este recurso podría acompañar las irregularidades; también puede ser efecto de otra carga o de la escena.",
                        "Baja · asociación exploratoria", "Atribución al proceso/hilo, colas GPU, clocks efectivos y repetición controlada del escenario.",
                        "Repetir la captura y cambiar una sola variable pertinente; revisar aplicaciones opcionales si la carga es global.",
                        "Cambiar calidad o cerrar aplicaciones puede afectar imagen, captura, audio y trabajo sin mejorar el problema.",
                        "3–5 repeticiones independientes A/B alternadas con escena, límite FPS y contexto constantes; contrastar p99 y 1% low. Mejora no concluyente sin esa validación."));
                }
            }
        }
        if (findings.Count == 0)
            findings.Add(new("Sin atribución concluyente de los spikes", $"{summary.Spikes} spikes >{threshold.ToString("F2", CultureInfo.InvariantCulture)} ms; {timed.Length}/{session.Frames.Length} frames con QPC; {associations.Count} series.",
                "Los datos actuales no permiten señalar un componente como causa.", "Insuficiente",
                "Cobertura temporal, sensores relevantes y repeticiones de una escena controlada.", "Conservar la configuración y completar mediciones antes de aplicar ajustes.",
                "Modificar varias opciones sin evidencia puede introducir regresiones y perder trazabilidad.", "Repetir la misma escena y revisar cobertura, contexto y cada serie. Ausencia de hallazgo no descarta un problema."));
        return new(session.Frames.Length, timed.Length, summary.Spikes, threshold, associations.ToArray(), findings.ToArray(), limits.Distinct().ToArray());
    }

    public static string ToMarkdown(BenchmarkSession session, PerformanceReport report)
    {
        static string Safe(string value)
        {
            var clean = value.Replace("<", "&lt;").Replace(">", "&gt;").Replace("\r", " ").Replace("\n", " ");
            foreach (var character in new[] { "\\", "`", "*", "_", "[", "]", "!", "#", "|" }) clean = clean.Replace(character, "\\" + character);
            return clean;
        }
        var text = new StringBuilder("# PCOptimizer · informe de rendimiento\n\n");
        text.AppendLine($"Experimento: {session.Id} · SHA-256 CSV: {session.SourceSha256}\n");
        text.AppendLine($"Proceso/stream: {Safe(session.Stream.ToString())}\n");
        foreach (var (label, value) in new[] { ("Hardware", session.Context.Hardware), ("Juego", session.Context.GameVersion),
            ("Driver", session.Context.DriverVersion), ("Windows", session.Context.WindowsVersion), ("Ajustes", session.Context.Settings), ("Escenario", session.Context.Scenario) })
            text.AppendLine($"{label}: {Safe(value)}\n");
        text.AppendLine($"Frames: {report.Frames}; con QPC: {report.TimedFrames}; spikes: {report.Spikes}; umbral: {report.SpikeThresholdMs:F2} ms.\n");
        if (session.Telemetry is { } trace)
            text.AppendLine($"Captura: {trace.CaptureId}; UTC inicial: {trace.StartedUtc:O}; frecuencia QPC: {trace.QpcFrequency} Hz; rango [{trace.StartedQpc}, {trace.EndedQpc}]; CPU del proceso colector: {trace.CollectorCpuMs:F2} ms (incluye GUI y PresentMon no está incluido; no es overhead causal).\n");
        foreach (var finding in report.Findings)
        {
            text.AppendLine($"## {Safe(finding.Finding)}\n");
            foreach (var (label, value) in new[] { ("Evidencia", finding.Evidence), ("Hipótesis", finding.Hypothesis), ("Confianza", finding.Confidence),
                ("Falta", finding.MissingInformation), ("Recomendación", finding.Recommendation), ("Riesgo", finding.Risk), ("Experimento", finding.Experiment) })
                text.AppendLine($"{label}: {Safe(value)}\n");
        }
        text.AppendLine("## Cobertura por serie\n");
        foreach (var lane in report.Associations)
            text.AppendLine($"- {lane.Metric} · {Safe(lane.Device)} · {Safe(lane.Source)}: {lane.MatchedFrames}/{report.Frames} frames, {lane.MatchedSpikes}/{report.Spikes} spikes.\n");
        text.AppendLine("## Límites\n");
        foreach (var limit in report.Limitations) text.AppendLine($"- {Safe(limit)}");
        return text.ToString();
    }
}
