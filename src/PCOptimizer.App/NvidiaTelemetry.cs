using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PCOptimizer.App;

internal sealed record NvidiaGpuSnapshot(string Name, ulong? VramTotal, ulong? VramUsed, uint? Utilization,
    uint? TemperatureC, uint? GraphicsClockMhz, double? PowerWatts, ulong? ClockEventReasons, string[] Unavailable, DateTimeOffset ObservedUtc);
internal sealed record NvidiaProbeResult(NvidiaGpuSnapshot[] Devices, string Status)
{
    public long? ReadStartQpc { get; init; }
    public long? ReadEndQpc { get; init; }
}

// Read-only vendor API from the installed driver. No kernel driver is installed or redistributed.
internal interface IGpuTelemetryProvider
{
    Task<NvidiaProbeResult> ReadAsync(CancellationToken cancellationToken = default);
}
internal sealed class NvidiaTelemetry : IGpuTelemetryProvider
{
    [StructLayout(LayoutKind.Sequential)] private struct Memory { public ulong Total, Free, Used; }
    [StructLayout(LayoutKind.Sequential)] private struct Utilization { public uint Gpu, Memory; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NoArgs();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Count(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Device(uint index, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Name(IntPtr device, [Out] byte[] name, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadMemory(IntPtr device, out Memory memory);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadUtilization(IntPtr device, out Utilization utilization);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadUint(IntPtr device, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadSelector(IntPtr device, uint selector, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReadReasons(IntPtr device, out ulong reasons);

    internal static (int MemoryBytes, int UtilizationBytes) LayoutSizes => (Marshal.SizeOf<Memory>(), Marshal.SizeOf<Utilization>());
    private static readonly object gate = new();
    private static Task<NvidiaProbeResult>? activeRead;
    public async Task<NvidiaProbeResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<NvidiaProbeResult> task;
        lock (gate)
        {
            if (activeRead is null || activeRead.IsCompleted) activeRead = Task.Run(() =>
            {
                var start = Stopwatch.GetTimestamp();
                var result = Read();
                return result with { ReadStartQpc = start, ReadEndQpc = Stopwatch.GetTimestamp() };
            });
            task = activeRead;
        }
        try { return await task.WaitAsync(TimeSpan.FromSeconds(8), cancellationToken); }
        catch (TimeoutException) { return new([], "NVML no respondió en ocho segundos. No se inicia otra consulta nativa mientras la anterior siga activa."); }
    }
    private static NvidiaProbeResult Read()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) return new([], "NVML requiere Windows x64; no disponible.");
        var path = Path.Combine(Environment.SystemDirectory, "nvml.dll");
        if (!File.Exists(path)) return new([], "El controlador no expone nvml.dll en System32. No se instala ningún componente; GPU NVIDIA no medida por este proveedor.");
        IntPtr library = IntPtr.Zero; NoArgs? shutdown = null; var initialized = false;
        try
        {
            library = NativeLibrary.Load(path, typeof(NvidiaTelemetry).Assembly, DllImportSearchPath.System32);
            T? Function<T>(string name) where T : Delegate => NativeLibrary.TryGetExport(library, name, out var address) ? Marshal.GetDelegateForFunctionPointer<T>(address) : null;
            var init = Function<NoArgs>("nvmlInit_v2"); shutdown = Function<NoArgs>("nvmlShutdown");
            if (init is null || shutdown is null) return new([], "NVML no expone inicialización compatible.");
            var result = init(); if (result != 0) return new([], $"NVML no disponible: código {result}; puede depender de permisos o driver.");
            initialized = true;
            var count = Function<Count>("nvmlDeviceGetCount_v2"); var handle = Function<Device>("nvmlDeviceGetHandleByIndex_v2");
            if (count is null || handle is null || count(out var number) != 0 || number > 16) return new([], "No se pudo enumerar dispositivos NVML.");
            var name = Function<Name>("nvmlDeviceGetName"); var memory = Function<ReadMemory>("nvmlDeviceGetMemoryInfo");
            var utilization = Function<ReadUtilization>("nvmlDeviceGetUtilizationRates"); var power = Function<ReadUint>("nvmlDeviceGetPowerUsage");
            var temp = Function<ReadSelector>("nvmlDeviceGetTemperature"); var clock = Function<ReadSelector>("nvmlDeviceGetClockInfo");
            var reasons = Function<ReadReasons>("nvmlDeviceGetCurrentClocksThrottleReasons");
            var devices = new List<NvidiaGpuSnapshot>();
            for (uint i = 0; i < number; i++)
            {
                if (handle(i, out var device) != 0) continue;
                var missing = new List<string>(); var label = $"NVIDIA · índice NVML {i}"; var bytes = new byte[256];
                if (name is not null && name(device, bytes, (uint)bytes.Length) == 0) label = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                ulong? total = null, used = null; uint? load = null, temperature = null, graphics = null; double? watts = null; ulong? events = null;
                if (memory is not null && memory(device, out var m) == 0 && m.Total > 0 && m.Used <= m.Total) { total = m.Total; used = m.Used; }
                else missing.Add("VRAM no disponible");
                if (utilization is not null && utilization(device, out var u) == 0 && u.Gpu <= 100) load = u.Gpu; else missing.Add("Utilización no disponible");
                if (temp is not null && temp(device, 0, out var t) == 0 && t <= 150) temperature = t; else missing.Add("Temperatura no disponible");
                if (clock is not null && clock(device, 0, out var c) == 0 && c > 0) graphics = c; else missing.Add("Reloj gráfico no disponible");
                if (power is not null && power(device, out var p) == 0) watts = p / 1000.0; else missing.Add("Consumo no disponible");
                if (reasons is not null && reasons(device, out var r) == 0) events = r; else missing.Add("Motivos de limitación no disponibles");
                devices.Add(new(label, total, used, load, temperature, graphics, watts, events, missing.ToArray(), DateTimeOffset.UtcNow));
            }
            return new(devices.ToArray(), "Proveedor NVML del driver instalado; instantánea de GPU, no métricas del juego ni latencia. Soporte por modelo/driver variable.");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or IOException or UnauthorizedAccessException)
        { return new([], "NVML no disponible: " + ex.Message); }
        finally
        {
            if (initialized) shutdown?.Invoke();
            if (library != IntPtr.Zero) NativeLibrary.Free(library);
        }
    }
}
