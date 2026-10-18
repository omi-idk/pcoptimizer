using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace PCOptimizer.App;

internal sealed record InstalledDriver(string Name, string Provider, string Version, string Date, string Inf, bool Signed)
{
    public string SourceName => Provider.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "NVIDIA" :
        Provider.Contains("Advanced Micro", StringComparison.OrdinalIgnoreCase) || Provider.Contains("AMD", StringComparison.OrdinalIgnoreCase) ? "AMD" :
        Provider.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "Intel" : "Windows Update / fabricante del equipo";
    public string SourceUrl => SourceName switch
    {
        "NVIDIA" => "https://www.nvidia.com/Download/index.aspx",
        "AMD" => "https://www.amd.com/en/support/download/drivers.html",
        "Intel" => "https://www.intel.com/content/www/us/en/download-center/home.html",
        _ => "https://support.microsoft.com/windows/update-drivers-through-device-manager-in-windows"
    };
}
internal sealed record WindowsDriverOffer(string Title, string HardwareId, string Manufacturer, string Provider, string Class, string DriverDate);
internal sealed record DiskHealth(string Model, string Health, string Operational, long Size,
    long? Temperature, long? Wear, long? PowerOnHours, long? ReadErrors, long? WriteErrors, string? ReliabilityError);
internal sealed record SoundDevice(string Name, string Manufacturer, string Status);
internal sealed record LoadSnapshot(double? CpuPercent, double? AvailableMemoryMb, double? TotalMemoryMb, double? Largest3DEnginePercent);

internal static class WindowsDiagnostics
{
    private static async Task<T> QueryAsync<T>(string script, CancellationToken cancellationToken = default)
    {
        var prefix = "[Console]::OutputEncoding = [Text.Encoding]::UTF8; $ErrorActionPreference = 'Stop'; $env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules');\n";
        var path = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var json = await WindowsCommands.ReadCancelableAsync(path, cancellationToken, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", prefix + script);
        return JsonSerializer.Deserialize<T>(json.Trim().TrimStart('\uFEFF'))
            ?? throw new InvalidDataException("Windows no devolvió datos válidos.");
    }

    public static Task<InstalledDriver[]> GetDriversAsync() => QueryAsync<InstalledDriver[]>("""
        $drivers = @(Get-CimInstance Win32_PnPSignedDriver | Where-Object {
            $_.DeviceClass -in @('DISPLAY', 'NET', 'MEDIA', 'SYSTEM', 'HDC', 'SCSIADAPTER') -and $_.DeviceName
        } | Sort-Object DeviceName | ForEach-Object {
            [pscustomobject]@{
                Name = [string]$_.DeviceName
                Provider = [string]$_.DriverProviderName
                Version = [string]$_.DriverVersion
                Date = if ($_.DriverDate) { $_.DriverDate.ToString('yyyy-MM-dd') } else { 'No informada' }
                Inf = [string]$_.InfName
                Signed = [bool]$_.IsSigned
            }
        })
        ConvertTo-Json -InputObject $drivers -Depth 4 -Compress
        """);

    internal const string WindowsDriverOffersScript = """
        $session = New-Object -ComObject Microsoft.Update.Session
        $session.ClientApplicationID = 'PCOptimizer read-only driver inventory'
        $searcher = $session.CreateUpdateSearcher()
        $result = $searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'")
        $offers = @($result.Updates | ForEach-Object {
            [pscustomobject]@{Title=[string]$_.Title; HardwareId=[string]$_.DriverHardwareID;
                Manufacturer=[string]$_.DriverManufacturer; Provider=[string]$_.DriverProvider;
                Class=[string]$_.DriverClass;
                DriverDate=if ($_.DriverVerDate) {$_.DriverVerDate.ToString('yyyy-MM-dd')} else {'No informada'}}
        })
        ConvertTo-Json -InputObject $offers -Depth 4 -Compress
        """;
    public static Task<WindowsDriverOffer[]> GetWindowsDriverOffersAsync(CancellationToken cancellationToken = default) => QueryAsync<WindowsDriverOffer[]>(WindowsDriverOffersScript, cancellationToken);

    private const string DiskHealthScript = """
        $disks = @(Get-PhysicalDisk | ForEach-Object {
            $disk = $_
            $counter = $null
            $failure = $null
            try { $counter = $disk | Get-StorageReliabilityCounter -ErrorAction Stop }
            catch { $failure = $_.Exception.Message }
            [pscustomobject]@{
                Model = [string]$disk.FriendlyName
                Health = [string]$disk.HealthStatus
                Operational = [string]($disk.OperationalStatus -join ', ')
                Size = [long]$disk.Size
                Temperature = if ($counter -and $null -ne $counter.Temperature) { [long]$counter.Temperature } else { $null }
                Wear = if ($counter -and $null -ne $counter.Wear) { [long]$counter.Wear } else { $null }
                PowerOnHours = if ($counter -and $null -ne $counter.PowerOnHours) { [long]$counter.PowerOnHours } else { $null }
                ReadErrors = if ($counter -and $null -ne $counter.ReadErrorsTotal) { [long]$counter.ReadErrorsTotal } else { $null }
                WriteErrors = if ($counter -and $null -ne $counter.WriteErrorsTotal) { [long]$counter.WriteErrorsTotal } else { $null }
                ReliabilityError = $failure
            }
        })
        ConvertTo-Json -InputObject $disks -Depth 4 -Compress
        """;

    public static Task<DiskHealth[]> GetDiskHealthAsync() => QueryAsync<DiskHealth[]>(DiskHealthScript);
    public static Task<DiskHealth[]> GetDiskHealthElevatedAsync() => WindowsReadOnlyProbe.ReadStorageAsync(DiskHealthScript);

    public static Task<SoundDevice[]> GetSoundDevicesAsync() => QueryAsync<SoundDevice[]>("""
        $devices = @(Get-CimInstance Win32_SoundDevice | ForEach-Object {
            [pscustomobject]@{ Name = [string]$_.Name; Manufacturer = [string]$_.Manufacturer; Status = [string]$_.Status }
        })
        ConvertTo-Json -InputObject $devices -Compress
        """);

    public static Task<LoadSnapshot> GetLoadAsync() => QueryAsync<LoadSnapshot>("""
        $cpu = Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'" | Select-Object -First 1
        $memory = Get-CimInstance Win32_OperatingSystem
        $gpu = $null
        try {
            $engines = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -ErrorAction Stop | Where-Object { $_.Name -like '*engtype_3D*' })
            if ($engines.Count -gt 0) { $gpu = [double](($engines | Measure-Object UtilizationPercentage -Maximum).Maximum) }
        } catch { }
        [pscustomobject]@{
            CpuPercent = if ($cpu) { [double]$cpu.PercentProcessorTime } else { $null }
            AvailableMemoryMb = [double]$memory.FreePhysicalMemory / 1024
            TotalMemoryMb = [double]$memory.TotalVisibleMemorySize / 1024
            Largest3DEnginePercent = $gpu
        } | ConvertTo-Json -Compress
        """);

    public static IReadOnlyList<(string Name, string Address)> NetworkTargets()
    {
        var targets = new List<(string, string)>();
        var gateway = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().GatewayAddresses).Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(System.Net.IPAddress.Any));
        if (gateway is not null) targets.Add(("Puerta de enlace", gateway.ToString()));
        targets.Add(("Cloudflare · ICMP", "1.1.1.1"));
        targets.Add(("Google · ICMP", "8.8.8.8"));
        return targets;
    }

    public static async Task<IReadOnlyList<long?>> PingAsync(string address)
    {
        var samples = new List<long?>();
        using var ping = new Ping();
        for (var i = 0; i < 10; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(System.Net.IPAddress.Parse(address), 1000);
                samples.Add(reply.Status == IPStatus.Success ? reply.RoundtripTime : null);
            }
            catch (PingException) { samples.Add(null); }
            if (i < 9) await Task.Delay(200);
        }
        return samples;
    }

    public static void OpenSoundPanel()
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "control.exe")) { UseShellExecute = true };
        start.ArgumentList.Add(Path.Combine(Environment.SystemDirectory, "mmsys.cpl"));
        using var process = Process.Start(start);
    }
}
