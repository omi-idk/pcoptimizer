using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal static class WindowsCommands
{
    public static string PowerCfg => Path.Combine(Environment.SystemDirectory, "powercfg.exe");

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    public static Task<string> ReadAsync(string executable, params string[] arguments) => ReadCancelableAsync(executable, CancellationToken.None, arguments);

    public static async Task<string> ReadCancelableAsync(string executable, CancellationToken cancellationToken, params string[] arguments)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = executable.Equals(PowerCfg, StringComparison.OrdinalIgnoreCase)
            ? Encoding.GetEncoding((int)GetOEMCP()) : Encoding.UTF8;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = encoding, StandardErrorEncoding = encoding
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar la consulta de Windows.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("La consulta de Windows tardó demasiado. Puedes volver a intentarlo.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"La consulta terminó con código {process.ExitCode}: {(await error).Trim()}");
        return await output;
    }

    public static async Task<IReadOnlyList<PowerPlan>> GetPlansAsync()
    {
        var plans = PowerPlanParser.Parse(await ReadAsync(PowerCfg, "/list"));
        if (plans.Count == 0 || plans.Count(p => p.IsActive) != 1)
            throw new InvalidOperationException("Windows no devolvió una lista válida de planes de energía.");
        return plans;
    }

    public static async Task ActivateAsync(Guid id)
    {
        // Only this fixed Windows command elevates; the UI remains unelevated.
        using var process = Process.Start(new ProcessStartInfo(PowerCfg, $"/setactive {id:D}")
        {
            UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("No se pudo iniciar el cambio de plan.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Windows rechazó el cambio de plan (código {process.ExitCode}).");
    }

    public static async Task<Inventory> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        // Fixed read-only CIM queries. No downloaded scripts or game process access.
        const string script = """
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
            $cpu = @(Get-CimInstance Win32_Processor)
            $gpu = @(Get-CimInstance Win32_VideoController)
            $system = Get-CimInstance Win32_ComputerSystem
            $os = Get-CimInstance Win32_OperatingSystem
            $disks = @(Get-CimInstance Win32_DiskDrive)
            $board = @(Get-CimInstance Win32_BaseBoard)
            $bios = @(Get-CimInstance Win32_BIOS)
            [pscustomobject]@{
                Cpu = ($cpu.Name -join ' / ')
                Gpu = ($gpu.Name -join ' / ')
                MemoryBytes = [long]$system.TotalPhysicalMemory
                Os = $os.Caption + ' · ' + $os.Version
                Motherboard = (($board | ForEach-Object { $_.Manufacturer + ' ' + $_.Product }) -join ' / ')
                Bios = ($bios.SMBIOSBIOSVersion -join ' / ')
                Disks = @($disks | ForEach-Object {
                    [pscustomobject]@{ Model = $_.Model; Size = [long]$_.Size }
                })
            } | ConvertTo-Json -Depth 4 -Compress
            """;
        var executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var json = await ReadCancelableAsync(executable, cancellationToken, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script);
        return JsonSerializer.Deserialize<Inventory>(json.Trim().TrimStart('\uFEFF'))
            ?? throw new InvalidOperationException("Windows devolvió un inventario vacío.");
    }
}

internal sealed record Inventory(string Cpu, string Gpu, long MemoryBytes, string Os, DiskInfo[] Disks, string Motherboard, string Bios);
internal sealed record DiskInfo(string Model, long Size);
internal sealed record PowerChange(Guid Previous, Guid Target, DateTimeOffset Date, string State);

internal sealed class PowerHistory
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCOptimizer", "power-history.json");

    public List<PowerChange> Read()
    {
        if (!File.Exists(path)) return [];
        // A corrupt history blocks changes rather than silently losing the recovery data.
        return LocalJsonFile.Read<List<PowerChange>>(path)
            ?? throw new InvalidDataException("No se pudo leer el historial. Se conservó el archivo original.");
    }

    public void Save(List<PowerChange> changes)
    {
        LocalJsonFile.Write(path, changes);
    }
}
