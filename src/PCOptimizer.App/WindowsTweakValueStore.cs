using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal sealed class WindowsTweakValueStore : IBatchTweakValueStore
{
    public RegistryValueSnapshot Read(RegistryTweakTarget target)
    {
        CheckTarget(target);
        if (target.Path.StartsWith(@"SOFTWARE\Policies\", StringComparison.OrdinalIgnoreCase)) CheckPolicyEligibility();
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
        else if (value.Kind == "String" && uint.TryParse(value.Data, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && (number <= 32767 || target.ClearBits is not null || target.Id == "numlock"))
            key.SetValue(target.Name, value.Data, RegistryValueKind.String);
        else throw new InvalidOperationException("Tipo de escritura no permitido.");
        key.Flush();
    }

    private static RegistryKey OpenRoot(RegistryTweakTarget target) => RegistryKey.OpenBaseKey(
        target.Machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    private static void CheckPolicyEligibility()
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
            if (managed is { SubKeyCount: > 0 }) throw new InvalidOperationException("Se detectó administración o unión a una organización. No se modificarán políticas de Windows.");
        }
    }

    private static void WriteMachine(RegistryTweakTarget target, RegistryValueSnapshot expected, RegistryValueSnapshot value)
    {
        var owner = ExecutableTweaks.Items.First(t => t.Targets.Contains(target));
        WriteElevatedBatch([new TweakValueChange(owner.Id, target.Id, expected, value)]);
    }

    public void WriteMachineBatch(IReadOnlyList<TweakValueChange> changes) => WriteElevatedBatch(changes);

    private static void WriteElevatedBatch(IReadOnlyList<TweakValueChange> changes)
    {
        foreach (var change in changes)
        {
            var target = ExecutableTweaks.Target(change.TweakId, change.TargetId);
            CheckTarget(target);
            if (target.Path.StartsWith(@"SOFTWARE\Policies\", StringComparison.OrdinalIgnoreCase)) CheckPolicyEligibility();
        }
        var script = MachineTweakScript.Build(changes);
        var arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        if (arguments.Length > 30000) throw new InvalidOperationException("Divide el lote elevado en menos ajustes; no se inició ninguna escritura del equipo.");
        var executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(executable, arguments)
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden })
            ?? throw new InvalidOperationException("No se pudo solicitar autorización de Windows.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(process.ExitCode switch
        {
            4 => "Un valor cambió durante la autorización. Se detuvo el lote; revisa el respaldo antes de reintentar.",
            5 => "Se detectó pertenencia a dominio. No se modificarán políticas administradas.",
            _ => "Windows no completó el lote del equipo. Se conservó el respaldo; revisa los valores y solicita reversión si es necesario."
        });
    }

    private static void CheckTarget(RegistryTweakTarget target)
    {
        if (!ExecutableTweaks.Items.SelectMany(t => t.Targets).Contains(target))
            throw new InvalidOperationException("Destino de registro no autorizado.");
        var minimum = ExecutableTweaks.Items.Where(t => t.Targets.Contains(target)).Max(t => t.MinimumWindowsBuild);
        if (minimum > 0 && Environment.OSVersion.Version.Build < minimum)
            throw new InvalidOperationException($"Este ajuste requiere Windows 11 compilación {minimum} o posterior.");
    }
}
