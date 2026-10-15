using System.Text.Json;

namespace PCOptimizer.Core;

public static class LocalJsonFile
{
    public const long MaximumBytes = 8 * 1024 * 1024;

    public static T? Read<T>(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("El registro supera el límite de lectura. Se conservó el archivo; archívalo antes de continuar.");
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { MaxDepth = 32 });
    }

    public static void Write<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true, MaxDepth = 32 });
            if (bytes.LongLength > MaximumBytes) throw new InvalidDataException("El registro supera el límite de escritura. No se sobrescribió el anterior.");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
