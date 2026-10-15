using System.Diagnostics;
using System.IO;
using System.Text.Json;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal static class SoftwareServices
{
    public static void OpenOfficialWebsite(string id)
    {
        var package = SoftwareCatalog.GetPackage(id);
        using var process = Process.Start(new ProcessStartInfo(package.Website) { UseShellExecute = true });
    }

    public static void OpenSettings(string key)
    {
        var uri = WindowsSettingsCatalog.GetUri(key);
        using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
    private static string PowerShell => Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private static async Task<string> ResolveWingetAsync()
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [Text.Encoding]::UTF8
            $env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
            $package = @(Get-AppxPackage -Name Microsoft.DesktopAppInstaller -ErrorAction Stop | Where-Object {
                $_.PackageFamilyName -eq 'Microsoft.DesktopAppInstaller_8wekyb3d8bbwe'
            } | Sort-Object Version -Descending | Select-Object -First 1)
            if ($package.Count -ne 1) { throw 'Instala o actualiza App Installer desde Microsoft Store.' }
            $path = [IO.Path]::Combine($package[0].InstallLocation, 'winget.exe')
            $signature = Get-AuthenticodeSignature -LiteralPath $path -ErrorAction Stop
            if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
                $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
                throw 'No se pudo verificar una firma válida de Microsoft para WinGet. No se ejecutará.'
            }
            ConvertTo-Json -InputObject $path -Compress
            """;
        var json = await WindowsCommands.ReadAsync(PowerShell, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script);
        var path = JsonSerializer.Deserialize<string>(json.Trim().TrimStart('\uFEFF'));
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
            throw new InvalidDataException("WinGet no devolvió una ruta válida del paquete verificado.");
        return path;
    }

    public static async Task<int> InstallAsync(string id)
    {
        var arguments = SoftwareCatalog.InstallArguments(id);
        // Launch the registered executable directly; no shell script, silent EULA acceptance or elevation of the UI.
        var start = new ProcessStartInfo(await ResolveWingetAsync()) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo abrir WinGet.");
        await process.WaitForExitAsync(); // Interactive prompts have no arbitrary timeout.
        return process.ExitCode;
    }

    public static async Task<IReadOnlySet<string>> GetOptionalAppsAsync()
    {
        // Identifiers come exclusively from the compiled allowlist; no user-supplied PowerShell.
        var names = string.Join(",", SoftwareCatalog.OptionalApps.Select(p => $"'{p.Id}'"));
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $found = @(foreach ($name in @({{names}})) {
                if (@(Get-AppxPackage -Name $name -ErrorAction Stop).Count -gt 0) { $name }
            })
            ConvertTo-Json -InputObject $found -Compress
            """;
        var json = await WindowsCommands.ReadAsync(PowerShell, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script);
        var installed = JsonSerializer.Deserialize<string[]>(json.Trim().TrimStart('\uFEFF'))
            ?? throw new InvalidDataException("Windows devolvió una lista de aplicaciones inválida.");
        var allowed = SoftwareCatalog.OptionalApps.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (installed.Any(p => !allowed.Contains(p))) throw new InvalidDataException("Windows devolvió un identificador inesperado.");
        return installed.ToHashSet(StringComparer.Ordinal);
    }

    public static async Task RemoveOptionalAppAsync(string id)
    {
        var app = SoftwareCatalog.GetOptionalApp(id);
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $packages = @(Get-AppxPackage -Name '{{app.Id}}' -ErrorAction Stop)
            foreach ($package in $packages) {
                Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
            }
            if (@(Get-AppxPackage -Name '{{app.Id}}' -ErrorAction Stop).Count -ne 0) {
                throw 'Windows no confirmó la eliminación de la aplicación.'
            }
            'ABSENT'
            """;
        var result = await WindowsCommands.ReadAsync(PowerShell, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script);
        if (result.Trim().TrimStart('\uFEFF') != "ABSENT") throw new InvalidOperationException("No se pudo verificar la eliminación.");
    }
}
