namespace PCOptimizer.Benchmarking;

public static class BenchmarkStatistics
{
    public const int MaximumFrames = 200_000;
    public static BenchmarkSummary Calculate(IReadOnlyList<FrameSample> frames)
    {
        if (frames.Count is < 2 or > MaximumFrames) throw new InvalidDataException("Se requieren entre 2 y 200000 intervalos válidos por sesión.");
        foreach (var frame in frames)
        {
            if (frame is null || !double.IsFinite(frame.IntervalMs) || frame.IntervalMs < 0.000001 || frame.IntervalMs > 3_600_000)
                throw new InvalidDataException("Intervalo de presentación inválido.");
            foreach (var busy in new[] { frame.CpuBusyMs, frame.GpuBusyMs })
                if (busy is { } n && (!double.IsFinite(n) || n < 0 || n > 3_600_000))
                    throw new InvalidDataException("Tiempo Busy inválido.");
            if (frame.CpuStartQpc is <= 0) throw new InvalidDataException("Timestamp QPC inválido.");
            if (frame.FrameType?.Length > 80) throw new InvalidDataException("Tipo de frame demasiado largo.");
        }
        var sorted = frames.Select(f => f.IntervalMs).Order().ToArray();
        var sum = sorted.Sum();
        var mean = sum / sorted.Length;
        var median = Quantile(sorted, .5);
        double Low(double fraction) => 1000 / sorted.TakeLast((int)Math.Ceiling(sorted.Length * fraction)).Average();
        var cpu = frames.Where(f => f.CpuBusyMs.HasValue).Select(f => f.CpuBusyMs!.Value).ToArray();
        var gpu = frames.Where(f => f.GpuBusyMs.HasValue).Select(f => f.GpuBusyMs!.Value).ToArray();
        var displayKnown = frames.All(f => f.Displayed.HasValue);
        return new(frames.Count, sum / 1000, 1000 / mean,
            Quantile(sorted.Select(ms => 1000 / ms).Order().ToArray(), .5), Low(.01), Low(.001), median,
            Quantile(sorted, .95), Quantile(sorted, .99), Math.Sqrt(sorted.Average(x => (x - mean) * (x - mean))),
            frames.Count(f => f.IntervalMs > Math.Max(50, 2 * median)),
            cpu.Length > 0 ? cpu.Average() : null, gpu.Length > 0 ? gpu.Average() : null, cpu.Length, gpu.Length,
            displayKnown ? frames.Count(f => f.Displayed == true) : null,
            displayKnown ? frames.Count(f => f.Displayed == false) : null,
            frames.Count(f => !string.IsNullOrWhiteSpace(f.FrameType)));
    }

    // Hyndman/Fan type 7: linear interpolation at (n - 1) * p, including endpoints.
    internal static double Quantile(double[] sorted, double p)
    {
        var position = (sorted.Length - 1) * p;
        var lo = (int)Math.Floor(position);
        var hi = (int)Math.Ceiling(position);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (position - lo);
    }

    internal static bool ContextMatches(BenchmarkSession baseline, BenchmarkSession candidate) =>
        baseline.Context == candidate.Context && new[] { baseline.Context.Scenario, baseline.Context.GameVersion,
            baseline.Context.DriverVersion, baseline.Context.Settings, baseline.Context.Hardware, baseline.Context.WindowsVersion }
            .All(value => !string.IsNullOrWhiteSpace(value))
        && string.Equals(baseline.Stream.Application, candidate.Stream.Application, StringComparison.OrdinalIgnoreCase);

    public static string Compare(BenchmarkSession baseline, BenchmarkSession candidate)
    {
        if (baseline.Id == candidate.Id || (baseline.SourceSha256 == candidate.SourceSha256 && baseline.Stream == candidate.Stream)) throw new InvalidOperationException("Selecciona dos sesiones distintas.");
        if (!ContextMatches(baseline, candidate))
            return "No comparable: faltan datos de contexto o difieren hardware, juego, driver, Windows, configuración o escenario. No se atribuye una mejora.";
        var a = Calculate(baseline.Frames);
        var b = Calculate(candidate.Frames);
        return $"Comparación exploratoria B frente a A: FPS presentados {100 * (b.AverageFps / a.AverageFps - 1):+0.0;-0.0;0.0}%; " +
            $"1% low {100 * (b.Low1Fps / a.Low1Fps - 1):+0.0;-0.0;0.0}%; p99 {100 * (b.P99Ms / a.P99Ms - 1):+0.0;-0.0;0.0}%. " +
            "Una pareja no demuestra causalidad ni significancia. Realiza 3–5 repeticiones alternadas por configuración, cambia una variable y conserva la escena. Sin intervalo de confianza de repeticiones.";
    }
}
