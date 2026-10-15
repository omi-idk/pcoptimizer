using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal static class WindowsRecoveryService
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    public static async Task<WindowsRestorePoint> CreatePointAsync(Guid sessionId)
    {
        var pipeName = "PCOptimizer-Recovery-" + Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var cancelRead = new CancellationTokenSource();
        var description = "PCOptimizer-" + sessionId.ToString("N");
        // Only generated identifiers enter this fixed script. The elevated process never writes
        // to a user-controlled path. A same-user pipe and client PID check bind its response.
        // No frequency override, execution-policy bypass or disabling of system protection.
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
            try {
                Microsoft.PowerShell.Management\Checkpoint-Computer -Description '{{description}}' -RestorePointType MODIFY_SETTINGS -WarningAction Stop -ErrorAction Stop
                $points = @(Microsoft.PowerShell.Management\Get-ComputerRestorePoint -ErrorAction Stop | Where-Object { $_.Description -eq '{{description}}' })
                if ($points.Count -ne 1) { exit 3 }
                $result = [pscustomobject]@{
                    SequenceNumber = [int]$points[0].SequenceNumber
                    Description = '{{description}}'
                    VerifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
                }
                $client = [IO.Pipes.NamedPipeClientStream]::new('.', '{{pipeName}}', [IO.Pipes.PipeDirection]::Out)
                try {
                    $client.Connect(10000)
                    $bytes = [Text.Encoding]::UTF8.GetBytes(($result | ConvertTo-Json -Compress))
                    $client.Write($bytes, 0, $bytes.Length)
                    $client.Flush()
                } finally { $client.Dispose() }
                exit 0
            } catch { exit 2 }
            """;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(executable,
            $"-NoLogo -NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("No se pudo iniciar la creación del punto.");
        var resultTask = ReadResultAsync(pipe, (uint)process.Id, cancelRead.Token);
        try
        {
            // Do not terminate a system recovery operation on an arbitrary timeout.
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Windows no confirmó un nuevo punto. Revisa protección, espacio, permisos y límite de 24 horas. La confirmación requiere elevar con la misma cuenta de Windows que ejecuta la app.");
            var json = await resultTask.WaitAsync(TimeSpan.FromSeconds(10));
            var point = JsonSerializer.Deserialize<WindowsRestorePoint>(json, new JsonSerializerOptions { MaxDepth = 8 })
                ?? throw new InvalidDataException("La confirmación del punto está vacía.");
            if (point.Description != description || point.SequenceNumber <= 0 || point.VerifiedAtUtc == default)
                throw new InvalidDataException("La confirmación no corresponde al punto solicitado.");
            return point;
        }
        finally
        {
            cancelRead.Cancel();
            try { await resultTask; }
            catch (Exception) { /* The primary operation reports the failure; always observe/cancel pipe I/O. */ }
        }
    }

    private static async Task<string> ReadResultAsync(NamedPipeServerStream pipe, uint expectedProcessId, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token);
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var clientId) || clientId != expectedProcessId)
            throw new InvalidDataException("La confirmación no procede del proceso de recuperación solicitado.");
        using var output = new MemoryStream();
        var buffer = new byte[1024];
        int count;
        while ((count = await pipe.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > 8192) throw new InvalidDataException("La confirmación excede el tamaño permitido.");
            output.Write(buffer, 0, count);
        }
        return new UTF8Encoding(false, true).GetString(output.ToArray());
    }

    public static void OpenProtection()
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "SystemPropertiesProtection.exe")) { UseShellExecute = true });
    }

    public static void OpenRestoreWizard()
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "rstrui.exe")) { UseShellExecute = true });
    }
}
