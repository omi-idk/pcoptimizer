namespace PCOptimizer.Benchmarking;

public enum SensorMetric { CpuTotalPercent, MemoryUsedPercent, GpuUtilizationPercent, GpuTemperatureC, GpuClockMhz, GpuPowerWatts, GpuVramUsedMiB }
public enum SensorAvailability { Available, Unavailable }
public sealed record SensorReading(SensorMetric Metric, string Device, string Source, string Unit,
    double? Value, SensorAvailability Availability, string Detail)
{
    // Null means provider aggregation window is unknown, not instantaneous.
    public double? AggregationSeconds { get; init; }
}
public sealed record SensorObservation(long StartQpc, long EndQpc, DateTimeOffset ObservedUtc, SensorReading[] Readings);

// QPC is meaningful only in this capture on the same Windows boot. UTC is context, never the join key.
public sealed record PerformanceTrace(int SchemaVersion, Guid CaptureId, long QpcFrequency,
    long StartedQpc, long EndedQpc, DateTimeOffset StartedUtc, SensorObservation[] Observations,
    string[] Limitations, double CollectorCpuMs)
{
    public int? TargetProcessId { get; init; }
    public DateTimeOffset? TargetStartUtc { get; init; }

    public static string UnitFor(SensorMetric metric) => metric switch
    {
        SensorMetric.CpuTotalPercent or SensorMetric.MemoryUsedPercent or SensorMetric.GpuUtilizationPercent => "%",
        SensorMetric.GpuTemperatureC => "°C",
        SensorMetric.GpuClockMhz => "MHz",
        SensorMetric.GpuPowerWatts => "W",
        SensorMetric.GpuVramUsedMiB => "MiB",
        _ => throw new InvalidDataException("Métrica desconocida.")
    };

    public static void Validate(PerformanceTrace trace)
    {
        if (trace.TargetProcessId is <= 0 || trace.TargetProcessId.HasValue != trace.TargetStartUtc.HasValue
            || trace.SchemaVersion != 1 || trace.CaptureId == Guid.Empty || trace.QpcFrequency is <= 0 or > 10_000_000_000
            || trace.StartedQpc <= 0 || trace.EndedQpc <= trace.StartedQpc
            || (trace.EndedQpc - trace.StartedQpc) / (double)trace.QpcFrequency > 700
            || trace.Observations is null || trace.Observations.Length > 10000
            || trace.Limitations is null || trace.Limitations.Length > 100
            || trace.Limitations.Any(x => x is null || x.Length > 2000)
            || !double.IsFinite(trace.CollectorCpuMs) || trace.CollectorCpuMs < 0)
            throw new InvalidDataException("Traza de rendimiento inválida o fuera de límites.");
        long previous = trace.StartedQpc;
        var series = new HashSet<(SensorMetric, string, string)>();
        foreach (var observation in trace.Observations)
        {
            if (observation is null || observation.StartQpc < previous || observation.EndQpc < observation.StartQpc
                || observation.EndQpc > trace.EndedQpc || observation.Readings is null || observation.Readings.Length > 128)
                throw new InvalidDataException("Ventana temporal de sensor inválida.");
            previous = observation.EndQpc;
            var keys = new HashSet<(SensorMetric, string, string)>();
            foreach (var reading in observation.Readings)
            {
                if (reading is null || !Enum.IsDefined(reading.Metric) || !Enum.IsDefined(reading.Availability)
                    || string.IsNullOrWhiteSpace(reading.Device) || reading.Device.Length > 300
                    || string.IsNullOrWhiteSpace(reading.Source) || reading.Source.Length > 300
                    || reading.Unit != UnitFor(reading.Metric) || reading.Detail is null || reading.Detail.Length > 2000
                    || reading.AggregationSeconds is { } window && (!double.IsFinite(window) || window < 0 || window > 700)
                    || !keys.Add((reading.Metric, reading.Device, reading.Source)))
                    throw new InvalidDataException("Identidad, unidad o estado de sensor inválidos.");
                series.Add((reading.Metric, reading.Device, reading.Source));
                if (series.Count > 128) throw new InvalidDataException("Demasiadas series de sensores.");
                var max = reading.Metric switch
                {
                    SensorMetric.CpuTotalPercent or SensorMetric.MemoryUsedPercent or SensorMetric.GpuUtilizationPercent => 100,
                    SensorMetric.GpuTemperatureC => 150,
                    _ => 1e12
                };
                if (reading.Availability == SensorAvailability.Available
                    ? reading.Value is not { } value || !double.IsFinite(value) || value < 0 || value > max
                    : reading.Value is not null)
                    throw new InvalidDataException("Valor de sensor inválido; ausencia no equivale a cero.");
            }
        }
    }
}
