using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace PCOptimizer.Benchmarking;

// PresentMon 1.x and 2.6 default CSV containing MsBetweenPresents. No FPS guesses from CPU Busy.
public static class PresentMonCsv
{
    public const long MaximumFileBytes = 64 * 1024 * 1024;
    public static async Task<ImportedCapture> ImportAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (file.Length > MaximumFileBytes) throw new InvalidDataException("CSV mayor de 64 MiB. Divide la captura por sesión.");
        using var bytes = new MemoryStream();
        var buffer = new byte[65536];
        int read;
        while ((read = await file.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (bytes.Length + read > MaximumFileBytes) throw new InvalidDataException("CSV mayor de 64 MiB.");
            await bytes.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var hash = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length)));
        bytes.Position = 0;
        using var parser = new TextFieldParser(bytes, Encoding.UTF8, true, true) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("CSV vacío.");
        if (header.Length > 256 || header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != header.Length)
            throw new InvalidDataException("Cabeceras duplicadas o excesivas.");
        var columns = header.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "Application", "ProcessID", "SwapChainAddress", "MsBetweenPresents" })
            if (!columns.ContainsKey(required)) throw new InvalidDataException($"Falta {required}. Exporta PresentMon 2.6.0 con métricas predeterminadas o 1.x; no se deducen FPS de CPU Busy.");
        var groups = new Dictionary<CaptureStream, List<FrameSample>>();
        var missing = 0;
        var count = 0;
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++count > BenchmarkStatistics.MaximumFrames) throw new InvalidDataException("La captura supera 200000 filas.");
            var row = parser.ReadFields()!;
            if (row.Length != header.Length || row.Any(s => s.Length > 4096)) throw new InvalidDataException($"Fila {count + 1}: columnas incompletas o texto excesivo.");
            string Field(string name) => columns.TryGetValue(name, out var index) ? row[index] : "";
            if (!int.TryParse(Field("ProcessID"), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0
                || Field("Application").Length is 0 or > 260 || Field("SwapChainAddress").Length is 0 or > 80)
                throw new InvalidDataException($"Fila {count + 1}: identidad de proceso o swapchain inválida.");
            var stream = new CaptureStream(Field("Application"), pid, Field("SwapChainAddress"));
            if (!groups.TryGetValue(stream, out var frames))
            {
                if (groups.Count >= 128) throw new InvalidDataException("Demasiados streams en la captura.");
                groups[stream] = frames = [];
            }
            var interval = OptionalNumber(Field("MsBetweenPresents"));
            if (interval is null or 0) { missing++; continue; }
            bool? displayed = null;
            if (columns.ContainsKey("Dropped")) displayed = Field("Dropped") switch
                { "0" => true, "1" => false, "" or "NA" => null, _ => throw new InvalidDataException("Dropped debe ser 0, 1 o NA.") };
            else if (columns.ContainsKey("DisplayedTime")) displayed = Field("DisplayedTime") == "" ? null : OptionalNumber(Field("DisplayedTime")) is > 0;
            frames.Add(new(interval.Value, OptionalNumber(Field("MsCPUBusy")), OptionalNumber(Field("MsGPUBusy")), displayed,
                string.IsNullOrEmpty(Field("FrameType")) ? null : Field("FrameType")));
        }
        if (groups.Values.All(g => g.Count < 2)) throw new InvalidDataException("No hay un stream con al menos dos intervalos válidos.");
        foreach (var frames in groups.Values.Where(g => g.Count >= 2)) _ = BenchmarkStatistics.Calculate(frames);
        return new(hash, groups.Where(p => p.Value.Count >= 2).ToDictionary(p => p.Key, p => p.Value.ToArray()), missing);
    }

    private static double? OptionalNumber(string text)
    {
        if (text is "" or "NA" or "N/A") return null;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < 0 || n > 3_600_000)
            throw new InvalidDataException("El CSV contiene una métrica no finita, negativa o fuera de rango.");
        return n;
    }
}
