using System.Diagnostics;
using System.Runtime.InteropServices;
using PCOptimizer.Benchmarking;

namespace PCOptimizer.App.Benchmarking;

internal sealed record PerformanceCaptureResult(string CsvPath, PerformanceTrace Trace);
internal interface IPerformanceCaptureService
{
    Task<PerformanceCaptureResult> CaptureAsync(Func<CancellationToken, Task<string>> capture, CancellationToken token);
}

// Opt-in and bounded to one capture. No timers or providers remain active in the background afterwards.
internal sealed class PerformanceCaptureService(IGpuTelemetryProvider gpu, IProgress<SensorObservation>? progress = null, TimeSpan? samplingInterval = null) : IPerformanceCaptureService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    public async Task<PerformanceCaptureResult> CaptureAsync(Func<CancellationToken, Task<string>> capture, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (samplingInterval is { } interval && (interval.TotalMilliseconds < 500 || interval.TotalSeconds > 10))
            throw new ArgumentOutOfRangeException(nameof(samplingInterval));
        var start = Stopwatch.GetTimestamp();
        var utc = DateTimeOffset.UtcNow;
        using var self = Process.GetCurrentProcess();
        var cpu = self.TotalProcessorTime;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        stop.CancelAfter(TimeSpan.FromSeconds(680));
        var observations = new List<SensorObservation>();
        var limitations = new HashSet<string>
        {
            $"Muestreo solicitado cada {(samplingInterval ?? TimeSpan.FromMilliseconds(500)).TotalMilliseconds:N0} ms, sin recuperar ticks perdidos; consultas lentas reducen cobertura.",
            "Sin sensores CPU térmicos, clocks efectivos, I/O ni atribución por proceso. CPU total GetSystemTimes puede estar limitada a un grupo en equipos >64 procesadores.",
            "Sin telemetría AMD/Intel en este incremento; no disponer de NVML no significa GPU ausente o inactiva.",
            "Coste CPU del proceso completo registrado como observación; falta ensayo externo A/B para cuantificar el overhead y el coste de PresentMon."
        };
        var sampling = SampleLoopAsync(observations, limitations, stop.Token);
        string output;
        try { output = await capture(token); }
        finally
        {
            stop.Cancel();
            await sampling; // Observe completion before freezing the trace, including cancellation/failure paths.
        }
        var end = Stopwatch.GetTimestamp();
        self.Refresh();
        var trace = new PerformanceTrace(1, Guid.NewGuid(), Stopwatch.Frequency, start, end, utc,
            observations.ToArray(), limitations.Take(100).ToArray(), Math.Max(0, (self.TotalProcessorTime - cpu).TotalMilliseconds));
        PerformanceTrace.Validate(trace);
        return new(output, trace);
    }

    private async Task SampleLoopAsync(List<SensorObservation> observations, HashSet<string> limits, CancellationToken token)
    {
        var cadence = samplingInterval ?? TimeSpan.FromMilliseconds(500);
        void Record(SensorObservation observation) { observations.Add(observation); progress?.Report(observation); }
        long? previousTotal = null, previousIdle = null;
        var previousCpuQpc = Stopwatch.GetTimestamp();
        try
        {
            while (!token.IsCancellationRequested && observations.Count < 9000)
            {
                var start = Stopwatch.GetTimestamp();
                var readings = new List<SensorReading>();
                var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
                var memoryOk = GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0 && memory.AvailablePhysical <= memory.TotalPhysical;
                readings.Add(new(SensorMetric.MemoryUsedPercent, "Sistema", "GlobalMemoryStatusEx", "%",
                    memoryOk ? 100.0 * (memory.TotalPhysical - memory.AvailablePhysical) / memory.TotalPhysical : null,
                    memoryOk ? SensorAvailability.Available : SensorAvailability.Unavailable,
                    "Uso físico; no demuestra presión, paginación ni agotamiento del commit."));
                var cpuOk = GetSystemTimes(out var idle, out var kernel, out var user);
                var now = Stopwatch.GetTimestamp();
                var total = cpuOk ? kernel + user : 0;
                double? load = cpuOk && previousTotal is { } p && previousIdle is { } pi && total > p && idle >= pi && idle - pi <= total - p
                    ? 100.0 * (1 - (idle - pi) / (double)(total - p)) : null;
                readings.Add(new(SensorMetric.CpuTotalPercent, "Sistema", "GetSystemTimes", "%", load,
                    load.HasValue ? SensorAvailability.Available : SensorAvailability.Unavailable,
                    $"Promedio global desde lectura anterior ({(now - previousCpuQpc) / (double)Stopwatch.Frequency:F3} s); primera lectura no disponible. No identifica el hilo del juego.")
                { AggregationSeconds = (now - previousCpuQpc) / (double)Stopwatch.Frequency });
                previousTotal = cpuOk ? total : null; previousIdle = cpuOk ? idle : null; previousCpuQpc = now;
                Record(new(start, Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow, readings.ToArray()));

                start = Stopwatch.GetTimestamp();
                var result = await gpu.ReadAsync(token).ConfigureAwait(false);
                var end = Stopwatch.GetTimestamp();
                if (result.Devices.Length > 0 && (result.ReadStartQpc is not { } actualStart || result.ReadEndQpc is not { } actualEnd
                    || actualStart < start || actualEnd > end || actualEnd < actualStart))
                {
                    limits.Add("Se omitió una respuesta NVML sin ventana QPC propia de esta consulta; una lectura nativa anterior no se reetiqueta como nueva.");
                    await Task.Delay(cadence, token).ConfigureAwait(false);
                    continue;
                }
                if (result.Devices.Length == 0) limits.Add(result.Status);
                var gpuReadings = new List<SensorReading>();
                for (var i = 0; i < result.Devices.Length; i++)
                {
                    var device = result.Devices[i];
                    var name = $"NVML {i} · {device.Name}";
                    void Add(SensorMetric metric, double? value) => gpuReadings.Add(new(metric, name, "NVIDIA NVML · driver instalado", PerformanceTrace.UnitFor(metric), value,
                        value.HasValue ? SensorAvailability.Available : SensorAvailability.Unavailable, "Lectura del dispositivo; ventana interna del proveedor no conocida. Sin asociación automática al PID del juego."));
                    Add(SensorMetric.GpuUtilizationPercent, device.Utilization);
                    Add(SensorMetric.GpuTemperatureC, device.TemperatureC);
                    Add(SensorMetric.GpuClockMhz, device.GraphicsClockMhz);
                    Add(SensorMetric.GpuPowerWatts, device.PowerWatts);
                    Add(SensorMetric.GpuVramUsedMiB, device.VramUsed / (double?)(1024 * 1024));
                }
                if (gpuReadings.Count > 0) Record(new(result.ReadStartQpc!.Value, result.ReadEndQpc!.Value, DateTimeOffset.UtcNow, gpuReadings.ToArray()));
                await Task.Delay(cadence, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ExternalException)
        { limits.Add("Muestreo detenido; las lecturas previas se conservaron: " + ex.Message); }
    }
}
