namespace PCOptimizer.Core;

public sealed record LogicalProcessorLoad(string Name, double Percent);
public sealed record ResourceSample(double ElapsedSeconds, LogicalProcessorLoad[] LogicalProcessors,
    double? AvailableMemoryMb, double? TotalMemoryMb, double? CommitPercent, double? PagesInputPerSecond);
public sealed record ResourceCapture(DateTimeOffset StartedUtc, ResourceSample[] Samples, string[] Limitations);
public enum DiagnosticConfidence { Insufficient, Low, Moderate }
public sealed record ResourceFinding(string Title, DiagnosticConfidence Confidence, string Evidence,
    string MissingInformation, string SuggestedAction, string Risk, string Validation);

// These are observations of sustained system-wide pressure, not attribution to the game or causal bottlenecks.
public static class ResourceDiagnostics
{
    public static IReadOnlyList<ResourceFinding> Analyze(ResourceCapture capture)
    {
        if (capture.Samples is null || capture.Samples.Length > 120 || capture.Limitations is null)
            throw new InvalidDataException("Captura de recursos inválida.");
        double previous = -1;
        foreach (var s in capture.Samples)
        {
            if (s is null || !double.IsFinite(s.ElapsedSeconds) || s.ElapsedSeconds <= previous || s.ElapsedSeconds > 600
                || s.LogicalProcessors is null || s.LogicalProcessors.Length > 1024)
                throw new InvalidDataException("Orden temporal o muestra de recursos inválidos.");
            previous = s.ElapsedSeconds;
            foreach (var p in s.LogicalProcessors)
                if (p is null || string.IsNullOrWhiteSpace(p.Name) || !double.IsFinite(p.Percent) || p.Percent is < 0 or > 100)
                    throw new InvalidDataException("Carga de procesador lógico inválida.");
            if (s.LogicalProcessors.Select(p => p.Name).Distinct().Count() != s.LogicalProcessors.Length)
                throw new InvalidDataException("Procesadores lógicos duplicados.");
            foreach (var n in new[] { s.AvailableMemoryMb, s.TotalMemoryMb, s.CommitPercent, s.PagesInputPerSecond })
                if (n.HasValue && (!double.IsFinite(n.Value) || n.Value < 0)) throw new InvalidDataException("Métrica de memoria inválida.");
            if (s.CommitPercent > 100 || s.TotalMemoryMb is <= 0 || s.AvailableMemoryMb > s.TotalMemoryMb)
                throw new InvalidDataException("Métrica de memoria fuera de rango.");
        }
        var samples = capture.Samples;
        if (samples.Length < 10 || samples[^1].ElapsedSeconds - samples[0].ElapsedSeconds < 8
            || samples.Zip(samples.Skip(1)).Any(pair => pair.Second.ElapsedSeconds - pair.First.ElapsedSeconds > 5))
            return [new("Evidencia temporal insuficiente", DiagnosticConfidence.Insufficient,
                $"{samples.Length} muestras. Se requieren al menos 10, ocho segundos y sin huecos mayores de cinco segundos.",
                "Serie continua durante la escena del juego.", "Repite el muestreo con el juego en la misma escena.", "El muestreo también consume recursos.", "Compara con la misma escena sin el muestreador.")];
        var findings = new List<ResourceFinding>();
        var processors = samples.SelectMany(s => s.LogicalProcessors.Select(p => p.Name)).Distinct().ToArray();
        var coveredProcessors = 0;
        foreach (var name in processors)
        {
            var values = samples.Select(s => s.LogicalProcessors.SingleOrDefault(p => p.Name == name)).Where(p => p is not null).ToArray();
            if (values.Length < Math.Ceiling(samples.Length * .8)) continue;
            coveredProcessors++;
            var saturated = values.Count(p => p!.Percent >= 90);
            if (saturated >= Math.Ceiling(values.Length * .8))
                findings.Add(new($"Carga sostenida en procesador lógico {name}", DiagnosticConfidence.Moderate,
                    $"{saturated}/{values.Length} lecturas ≥90%. Es carga total de ese procesador lógico, no de un hilo del juego.",
                    "Frametimes sincronizados, actividad por proceso/hilo y topología física. Las cargas pueden migrar entre procesadores.",
                    "Comprueba qué procesos consumen CPU y repite una escena reproducible. No cambies afinidades automáticamente.",
                    "Cerrar procesos puede perder datos; no se cierra ninguno.", "Comprueba si los spikes coinciden temporalmente con la carga usando una traza de referencia."));
        }
        var memory = samples.Where(s => s.TotalMemoryMb is > 0 && s.AvailableMemoryMb.HasValue && s.CommitPercent.HasValue && s.PagesInputPerSecond.HasValue).ToArray();
        if (memory.Length >= Math.Ceiling(samples.Length * .8))
        {
            var pressure = memory.Count(s => s.AvailableMemoryMb / s.TotalMemoryMb < .05 && s.CommitPercent >= 90 && s.PagesInputPerSecond > 5);
            if (pressure >= Math.Ceiling(memory.Length * .8))
                findings.Add(new("Presión de memoria con lecturas de páginas", DiagnosticConfidence.Moderate,
                    $"{pressure}/{memory.Length} lecturas con memoria disponible <5%, compromiso ≥90% y Pages Input/sec >5.",
                    "Origen de las páginas (archivos mapeados o paginación), proceso y correlación con frametimes.",
                    "Revisa consumo por aplicación y configuración administrada del archivo de paginación. No vacíes cachés ni standby.",
                    "Desactivar paginación puede provocar cierres por falta de memoria.", "Repite la escena y contrasta commit y actividad de disco en Administrador de tareas/WPA."));
        }
        if (findings.Count == 0 && coveredProcessors == 0 && memory.Length < Math.Ceiling(samples.Length * .8))
            findings.Add(new("Cobertura de contadores insuficiente", DiagnosticConfidence.Insufficient,
                "La serie tiene duración suficiente, pero no hay contadores utilizables en al menos el 80% de las lecturas.",
                "CPU por procesador lógico y métricas completas de memoria.", "Comprueba disponibilidad de contadores y repite la consulta.", "No se cambia la configuración.", "Contrasta con los contadores de Windows; no se concluye ausencia de presión."));
        else if (findings.Count == 0)
            findings.Add(new("Sin presión sostenida detectada con estas reglas", DiagnosticConfidence.Low,
                $"{samples.Length} muestras durante {samples[^1].ElapsedSeconds - samples[0].ElapsedSeconds:N1} s. No se detectó el patrón de CPU o memoria definido.",
                "GPU, VRAM, temperaturas, límites de potencia, frametimes e interrupciones más breves que el muestreo.",
                "Captura frametimes y repite la misma escena; este resultado no descarta un límite de CPU/GPU ni stuttering.",
                "No se recomienda ningún cambio del sistema a partir de este resultado.", "Contrasta con una captura PresentMon y telemetría sincronizada antes de atribuir causas."));
        findings.Add(new("Límites de la observación", DiagnosticConfidence.Insufficient,
            string.Join(" · ", capture.Limitations.DefaultIfEmpty("Contadores de Windows, agregados del sistema.")),
            "Sin temperatura, GPU/VRAM ni correlación temporal con una captura de frames en esta consulta.",
            "Usa los resultados como hipótesis, no como porcentaje de cuello de botella.", "No se escriben registro, afinidades, prioridades ni memoria del juego.", "Valida con el escenario y una herramienta independiente."));
        return findings;
    }
}

public sealed record ProcessorDetails(string Name, int Cores, int LogicalProcessors, long? L2CacheKb, long? L3CacheKb, int? MaximumClockMhz);
public sealed record MemoryModuleDetails(string Locator, long CapacityBytes, int? ConfiguredClock, int? RatedClock);
public sealed record DisplayDetails(string Name, int? Width, int? Height, int? RefreshRate);
public sealed record HardwareDetails(ProcessorDetails[] Processors, MemoryModuleDetails[] MemoryModules, DisplayDetails[] Displays, string[] Limitations);
