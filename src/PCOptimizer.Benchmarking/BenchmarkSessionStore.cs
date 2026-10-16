using System.Text.Json;

namespace PCOptimizer.Benchmarking;

public sealed class BenchmarkSessionStore(string directory) : IBenchmarkSessionStore
{
    private const long MaximumBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { MaxDepth = 16 };
    private string FilePath(Guid id) => Path.Combine(directory, id.ToString("N") + ".json");

    public async Task SaveAsync(BenchmarkSession session, CancellationToken cancellationToken)
    {
        Validate(session);
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                await JsonSerializer.SerializeAsync(file, session, Options, cancellationToken);
                if (file.Length > MaximumBytes) throw new InvalidDataException("Sesión mayor de 64 MiB.");
                await file.FlushAsync(cancellationToken);
                file.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // A session is immutable. Never overwrite an earlier measurement, even with the same ID.
            File.Move(temporary, FilePath(session.Id), overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<BenchmarkSession> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(FilePath(id), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (file.Length > MaximumBytes) throw new InvalidDataException("Sesión mayor de 64 MiB.");
        var session = await JsonSerializer.DeserializeAsync<BenchmarkSession>(file, Options, cancellationToken)
            ?? throw new InvalidDataException("Sesión vacía.");
        Validate(session);
        if (session.Id != id) throw new InvalidDataException("El archivo no coincide con la sesión.");
        return session;
    }

    public Task<IReadOnlyList<SessionReference>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(directory)) return Task.FromResult<IReadOnlyList<SessionReference>>([]);
        var files = new DirectoryInfo(directory).EnumerateFiles("*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(200).ToArray();
        var list = new List<SessionReference>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "N", out var id))
                throw new InvalidDataException("Archivo de sesión no reconocido; se conservó sin eliminar.");
            list.Add(new(id, file.LastWriteTimeUtc));
        }
        return Task.FromResult<IReadOnlyList<SessionReference>>(list);
    }

    public static void Validate(BenchmarkSession session)
    {
        if (session.SchemaVersion != 1 || session.Id == Guid.Empty || session.Stream is null || session.Context is null
            || session.Frames is null || session.MissingIntervals < 0 || session.MissingIntervals > BenchmarkStatistics.MaximumFrames
            || session.SourceSha256 is null || session.SourceSha256.Length != 64 || session.SourceSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Esquema o procedencia de sesión inválidos.");
        if (session.Stream.ProcessId <= 0 || string.IsNullOrWhiteSpace(session.Stream.Application) || session.Stream.Application.Length > 260
            || string.IsNullOrWhiteSpace(session.Stream.SwapChain) || session.Stream.SwapChain.Length > 80)
            throw new InvalidDataException("Identidad de sesión inválida.");
        foreach (var value in new[] { session.Context.Hardware, session.Context.GameVersion, session.Context.DriverVersion,
            session.Context.WindowsVersion, session.Context.Settings, session.Context.Scenario })
            if (value is null || value.Length > 2000) throw new InvalidDataException("Contexto de sesión inválido.");
        _ = BenchmarkStatistics.Calculate(session.Frames);
    }
}
