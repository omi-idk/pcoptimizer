using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal interface IProcessWorkloadProbe
{
    Task<ProcessWorkloadCapture> CaptureAsync(CancellationToken token);
}
internal sealed class WindowsProcessWorkloadProbe : IProcessWorkloadProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(SafeProcessHandle process, out IoCounters counters);

    public Task<ProcessWorkloadCapture> CaptureAsync(CancellationToken token) => Task.Run(async () =>
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Se requiere Windows.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
        var start = Stopwatch.GetTimestamp(); var utc = DateTimeOffset.UtcNow;
        var before = Snapshot(deadline.Token);
        await Task.Delay(2000, deadline.Token);
        var after = Snapshot(deadline.Token);
        var result = new ProcessWorkloadCapture(1, Guid.NewGuid(), utc, start, Stopwatch.GetTimestamp(), Stopwatch.Frequency, Environment.ProcessorCount,
            before.Rows, after.Rows, before.Unavailable, after.Unavailable,
            ["Metadatos de procesos accesibles mediante QUERY_LIMITED_INFORMATION; procesos protegidos/no legibles se omiten y cuentan como cobertura ausente.",
                "No se leen módulos ni memoria privada, no hay inyección, cambios de prioridad/afinidad ni terminación de procesos. No se activa SeDebugPrivilege.",
                "CPU normalizada por Environment.ProcessorCount (capacidad disponible para PCOptimizer, no necesariamente toda la máquina). No mide GPU por proceso ni atribuye spikes.",
                "Los nombres son etiquetas de metadatos, no firmas ni prueba de que una aplicación pueda cerrarse sin consecuencias."]);
        ProcessWorkloads.Validate(result); return result;
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested && deadline.IsCancellationRequested)
        {throw new TimeoutException("La consulta de procesos superó 45 segundos. No se conserva una captura parcial.");}
    }, token);

    private static (ProcessCounter[] Rows, int Unavailable) Snapshot(CancellationToken token)
    {
        var rows = new List<ProcessCounter>(); var unavailable = 0;
        var processes = Process.GetProcesses();
        try
        {
            foreach (var process in processes)
            {
                token.ThrowIfCancellationRequested();
                if (rows.Count >= ProcessWorkloads.MaximumProcesses) { unavailable++; continue; }
                try
                {
                    var pid = process.Id;
                    if (pid <= 0) { unavailable++; continue; }
                    // Keep this one limited handle for all identity/counter reads, preventing PID reuse mixing.
                    using var handle = OpenProcess(0x1000, false, pid);
                    if (handle.IsInvalid) { unavailable++; continue; }
                    var qpc = Stopwatch.GetTimestamp();
                    if (!GetProcessTimes(handle, out var creation, out _, out var kernel, out var user)
                        || creation <= 0 || kernel < 0 || user < 0 || kernel > long.MaxValue - user)
                    { unavailable++; continue; }
                    var center = qpc + (Stopwatch.GetTimestamp() - qpc) / 2;
                    var ioOk = GetProcessIoCounters(handle, out var io);
                    var path = WindowsProcessPath.Read(handle);
                    var name = path is null ? $"PID {pid}" : Path.GetFileNameWithoutExtension(path);
                    rows.Add(new(new(pid, DateTime.FromFileTimeUtc(creation), name), center, kernel + user,
                        ioOk ? io.ReadBytes : null, ioOk ? io.WriteBytes : null));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
                { unavailable++; }
            }
        }
        finally { foreach (var process in processes) process.Dispose(); }
        return (rows.ToArray(), unavailable);
    }
}
