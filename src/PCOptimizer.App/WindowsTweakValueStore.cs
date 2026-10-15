using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal sealed class WindowsTweakValueStore : ITweakValueStore
{
    public RegistryValueSnapshot Read(RegistryTweakTarget target)
    {
        CheckTarget(target);
        if (target.Id.StartsWith("updates-", StringComparison.Ordinal)) CheckUpdateEligibility();
        using var root = OpenRoot(target);
        using var key = root.OpenSubKey(target.Path, writable: false);
        if (key is null || !key.GetValueNames().Contains(target.Name, StringComparer.OrdinalIgnoreCase))
            return RegistryValueSnapshot.Missing;
        return key.GetValueKind(target.Name) switch
        {
            RegistryValueKind.DWord => new(true, "DWord", unchecked((uint)(int)key.GetValue(target.Name)!).ToString(CultureInfo.InvariantCulture)),
            RegistryValueKind.String => new(true, "String", (string)key.GetValue(target.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!),
            _ => throw new InvalidOperationException("Tipo de registro no compatible. Se conservó el valor sin modificar.")
        };
    }

    public void Write(RegistryTweakTarget target, RegistryValueSnapshot expected, RegistryValueSnapshot value)
    {
        CheckTarget(target);
        if (target.Machine)
        {
            WriteMachine(target, expected, value);
            return;
        }
        if (Read(target) != expected) throw new InvalidOperationException("El valor cambió antes de escribir. Se detuvo el lote.");
        if (!value.Exists)
        {
            // Restore an absent value without deleting keys or neighboring values.
            using var existing = Registry.CurrentUser.OpenSubKey(target.Path, writable: true);
            existing?.DeleteValue(target.Name, throwOnMissingValue: false);
            existing?.Flush();
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(target.Path, writable: true)
            ?? throw new InvalidOperationException("No se pudo abrir la preferencia del usuario.");
        if (value.Kind == "DWord")
            key.SetValue(target.Name, unchecked((int)uint.Parse(value.Data, CultureInfo.InvariantCulture)), RegistryValueKind.DWord);
        else if (value.Kind == "String" && uint.TryParse(value.Data, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number <= 32767)
            key.SetValue(target.Name, value.Data, RegistryValueKind.String);
        else throw new InvalidOperationException("Tipo de escritura no permitido.");
        key.Flush();
    }

    private static RegistryKey OpenRoot(RegistryTweakTarget target) => RegistryKey.OpenBaseKey(
        target.Machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    private static void CheckUpdateEligibility()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var version = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var edition = version?.GetValue("EditionID") as string;
        if (edition is null || !(edition.StartsWith("Professional", StringComparison.Ordinal)
            || edition.StartsWith("Enterprise", StringComparison.Ordinal) || edition.StartsWith("Education", StringComparison.Ordinal)))
            throw new InvalidOperationException("Estas políticas requieren una edición compatible Pro, Enterprise o Education; no se aplicarán en Home o edición desconocida.");
        foreach (var path in new[] { @"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts", @"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo" })
        {
            using var managed = root.OpenSubKey(path);
            if (managed is { SubKeyCount: > 0 }) throw new InvalidOperationException("Se detectó administración o unión a una organización. No se modificarán políticas de actualización.");
        }
    }

    private static void WriteMachine(RegistryTweakTarget target, RegistryValueSnapshot expected, RegistryValueSnapshot value)
    {
        // Path/name come only from the compiled allowlist. Only a typed DWORD or value deletion
        // reaches the elevated script; no user paths, script bodies, registry imports or result files.
        if (value.Exists && value.Kind != "DWord") throw new InvalidOperationException("Solo DWORD permitido para este ajuste del equipo.");
        if (expected.Exists && expected.Kind != "DWord") throw new InvalidOperationException("Tipo anterior no permitido para este ajuste del equipo.");
        var check = expected.Exists
            ? $"if ($null -eq $prior -or $prior.GetValueKind('{target.Name}') -ne [Microsoft.Win32.RegistryValueKind]::DWord -or $prior.GetValue('{target.Name}') -ne [BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]{uint.Parse(expected.Data, CultureInfo.InvariantCulture)}), 0)) {{ exit 4 }}"
            : $"if ($null -ne $prior -and $prior.GetValueNames() -contains '{target.Name}') {{ exit 4 }}";
        var statement = value.Exists
            ? $"$key.SetValue('{target.Name}', [int][BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]{uint.Parse(value.Data, CultureInfo.InvariantCulture)}), 0), [Microsoft.Win32.RegistryValueKind]::DWord)"
            : $"if ($null -ne $key) {{ $key.DeleteValue('{target.Name}', $false) }}";
        var open = value.Exists ? $"$root.CreateSubKey('{target.Path}', $true)" : $"$root.OpenSubKey('{target.Path}', $true)";
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
            try {
                {{(target.Id.StartsWith("updates-", StringComparison.Ordinal) ? "if ((Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).PartOfDomain) { exit 5 }" : "")}}
                $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
                try {
                    $prior = $root.OpenSubKey('{{target.Path}}', $false)
                    try { {{check}} } finally { if ($null -ne $prior) { $prior.Dispose() } }
                    $key = {{open}}
                    try {
                        {{statement}}
                        if ($null -ne $key) { $key.Flush() }
                    } finally { if ($null -ne $key) { $key.Dispose() } }
                } finally { $root.Dispose() }
                exit 0
            } catch { exit 2 }
            """;
        var executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(executable,
            "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden })
            ?? throw new InvalidOperationException("No se pudo solicitar autorización de Windows.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Windows no completó la escritura del ajuste del equipo. Se conserva el respaldo para revisar y revertir.");
    }

    private static void CheckTarget(RegistryTweakTarget target)
    {
        if (!ExecutableTweaks.Items.SelectMany(t => t.Targets).Contains(target))
            throw new InvalidOperationException("Destino de registro no autorizado.");
    }
}
