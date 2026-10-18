using System.IO;
using System.Text.Json;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal interface IResourceProbe
{
    Task<HardwareDetails> HardwareAsync(CancellationToken cancellationToken);
    Task<ResourceCapture> CaptureAsync(CancellationToken cancellationToken);
}
internal sealed class WindowsResourceProbe : IResourceProbe
{
    internal const string HardwareScript = """
        $cpu = @(Get-CimInstance Win32_Processor | ForEach-Object {
            [pscustomobject]@{Name=[string]$_.Name; Cores=[int]$_.NumberOfCores; LogicalProcessors=[int]$_.NumberOfLogicalProcessors;
                L2CacheKb=if ($_.L2CacheSize -gt 0) {[long]$_.L2CacheSize} else {$null};
                L3CacheKb=if ($_.L3CacheSize -gt 0) {[long]$_.L3CacheSize} else {$null};
                MaximumClockMhz=if ($_.MaxClockSpeed -gt 0) {[int]$_.MaxClockSpeed} else {$null}}
        })
        $ram = @(Get-CimInstance Win32_PhysicalMemory | ForEach-Object {
            [pscustomobject]@{Locator=[string]$_.DeviceLocator; CapacityBytes=[long]$_.Capacity;
                ConfiguredClock=if ($_.ConfiguredClockSpeed -gt 0) {[int]$_.ConfiguredClockSpeed} else {$null};
                RatedClock=if ($_.Speed -gt 0) {[int]$_.Speed} else {$null}}
        })
        $display = @(Get-CimInstance Win32_VideoController | ForEach-Object {
            [pscustomobject]@{Name=[string]$_.Name;
                Width=if ($_.CurrentHorizontalResolution -gt 0) {[int]$_.CurrentHorizontalResolution} else {$null};
                Height=if ($_.CurrentVerticalResolution -gt 0) {[int]$_.CurrentVerticalResolution} else {$null};
                RefreshRate=if ($_.CurrentRefreshRate -gt 1) {[int]$_.CurrentRefreshRate} else {$null}}
        })
        $securityIssues = [Collections.Generic.List[string]]::new()
        $tpm = $null; $secureBoot = $null; $vbs = $null; $hvci = $null
        try {
            $device = Get-CimInstance -Namespace 'root/CIMV2/Security/MicrosoftTpm' -ClassName Win32_Tpm -ErrorAction Stop | Select-Object -First 1
            if ($device) { $tpm = [string]$device.SpecVersion } else { $securityIssues.Add('El proveedor no devolvió TPM; no se certifica ausencia física.') }
        } catch { $securityIssues.Add('TPM no informado: ' + $_.Exception.Message) }
        try { $secureBoot = [bool](Confirm-SecureBootUEFI -ErrorAction Stop) }
        catch { $securityIssues.Add('Secure Boot no informado: ' + $_.Exception.Message) }
        try {
            $guard = Get-CimInstance -Namespace 'root/Microsoft/Windows/DeviceGuard' -ClassName Win32_DeviceGuard -ErrorAction Stop | Select-Object -First 1
            if ($guard) {
                $vbs = [int]$guard.VirtualizationBasedSecurityStatus
                if ($null -ne $guard.SecurityServicesRunning) { $hvci = [bool](@($guard.SecurityServicesRunning) -contains 2) }
            }
        } catch { $securityIssues.Add('VBS/HVCI no informados: ' + $_.Exception.Message) }
        [pscustomobject]@{Processors=$cpu; MemoryModules=$ram; Displays=$display;
            Security=[pscustomobject]@{TpmSpecification=$tpm; SecureBoot=$secureBoot; VbsStatus=$vbs; HvciRunning=$hvci; Limitations=$securityIssues.ToArray()};
            Limitations=@('CPU: datos CIM; frecuencia máxima informada, no frecuencia efectiva ni throttling.',
                'RAM: campos SMBIOS; no prueban canales, XMP/DOCP ni estabilidad.',
                'Vídeo: modo informado por Win32_VideoController; no identifica cada monitor, HDR, VRR ni VRAM física.')
        } | ConvertTo-Json -Depth 5 -Compress
        """;
    internal const string CaptureScript = """
        $samples = [Collections.Generic.List[object]]::new()
        $issues = [Collections.Generic.HashSet[string]]::new()
        $started = [DateTimeOffset]::UtcNow.ToString('o')
        $clock = [Diagnostics.Stopwatch]::StartNew()
        for ($i=0; $i -lt 20; $i++) {
            $cores = @()
            $memory = $null
            $os = $null
            try {
                $cores = @(Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -ErrorAction Stop |
                    Where-Object { $_.Name -ne '_Total' } | ForEach-Object {
                        [pscustomobject]@{Name=[string]$_.Name; Percent=[double]$_.PercentProcessorTime}
                    })
            } catch { [void]$issues.Add('CPU por procesador lógico no disponible: ' + $_.Exception.Message) }
            try {
                $memory = Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory -ErrorAction Stop
                $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
            } catch { [void]$issues.Add('Contadores de memoria no disponibles: ' + $_.Exception.Message) }
            $samples.Add([pscustomobject]@{
                ElapsedSeconds=$clock.Elapsed.TotalSeconds; LogicalProcessors=$cores;
                AvailableMemoryMb=if ($os) {[double]$os.FreePhysicalMemory/1024} else {$null};
                TotalMemoryMb=if ($os) {[double]$os.TotalVisibleMemorySize/1024} else {$null};
                CommitPercent=if ($memory) {[double]$memory.PercentCommittedBytesInUse} else {$null};
                PagesInputPerSecond=if ($memory) {[double]$memory.PagesInputPersec} else {$null}
            })
            if ($i -lt 19) { Start-Sleep -Milliseconds 500 }
        }
        [void]$issues.Add('Muestreo CIM del sistema: no se atribuye carga al juego ni se sincroniza con PresentMon.')
        [pscustomobject]@{StartedUtc=$started; Samples=$samples.ToArray(); Limitations=@($issues | ForEach-Object {$_})} |
            ConvertTo-Json -Depth 6 -Compress
        """;
    public Task<HardwareDetails> HardwareAsync(CancellationToken cancellationToken) => QueryAsync<HardwareDetails>(HardwareScript, cancellationToken);
    public Task<ResourceCapture> CaptureAsync(CancellationToken cancellationToken) => QueryAsync<ResourceCapture>(CaptureScript, cancellationToken);
    private static async Task<T> QueryAsync<T>(string script, CancellationToken token)
    {
        const string prefix = "[Console]::OutputEncoding=[Text.Encoding]::UTF8; $ErrorActionPreference='Stop'; $env:PSModulePath=[IO.Path]::Combine($PSHOME,'Modules');\n";
        var exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var json = await WindowsCommands.ReadCancelableAsync(exe, token, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", prefix + script);
        if (json.Length > 2 * 1024 * 1024) throw new InvalidDataException("Respuesta de recursos demasiado grande.");
        return JsonSerializer.Deserialize<T>(json.Trim().TrimStart('\uFEFF'), new JsonSerializerOptions { MaxDepth = 16 })
            ?? throw new InvalidDataException("Windows no devolvió datos de recursos.");
    }
}
