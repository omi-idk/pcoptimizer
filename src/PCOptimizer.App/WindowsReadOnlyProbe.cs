using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace PCOptimizer.App;

// Used only for the compiled storage query, never for commands or scripts from UI/files.
internal static class WindowsReadOnlyProbe
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    internal static async Task<DiskHealth[]> ReadStorageAsync(string compiledQuery)
    {
        var pipeName = "PCOptimizer-Storage-" + Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
            try {
                $json = (& {
                    {{compiledQuery}}
                }) -join [Environment]::NewLine
                $client = [IO.Pipes.NamedPipeClientStream]::new('.', '{{pipeName}}', [IO.Pipes.PipeDirection]::Out)
                try {
                    $client.Connect(10000)
                    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
                    $client.Write($bytes, 0, $bytes.Length)
                    $client.Flush()
                } finally { $client.Dispose() }
                exit 0
            } catch { exit 2 }
            """;
        var path = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(path,
            "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden })
            ?? throw new InvalidOperationException("No se pudo iniciar la consulta de almacenamiento.");
        var read = ReadAsync(pipe, (uint)process.Id, cancellation.Token);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            if (process.ExitCode != 0) throw new InvalidOperationException("Windows no completó la consulta elevada. Usa la misma cuenta para UAC y revisa si el controlador expone contadores.");
            var json = await read.WaitAsync(TimeSpan.FromSeconds(10));
            return JsonSerializer.Deserialize<DiskHealth[]>(json, new JsonSerializerOptions { MaxDepth = 8 })
                ?? throw new InvalidDataException("La consulta no devolvió unidades válidas.");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true); // Read-only probe; never used for restore-point creation.
            throw new TimeoutException("La consulta de almacenamiento tardó demasiado; no cambió discos ni configuración.");
        }
        finally
        {
            cancellation.Cancel();
            try { await read; } catch (Exception) { /* Observe canceled/failed pipe I/O. */ }
        }
    }

    private static async Task<string> ReadAsync(NamedPipeServerStream pipe, uint expectedId, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token);
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var id) || id != expectedId)
            throw new InvalidDataException("La respuesta no proviene de la consulta solicitada.");
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = await pipe.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > 256 * 1024) throw new InvalidDataException("Respuesta de almacenamiento demasiado grande.");
            output.Write(buffer, 0, count);
        }
        return new UTF8Encoding(false, true).GetString(output.ToArray());
    }
}
