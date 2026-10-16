using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PCOptimizer.Benchmarking;

public static class BenchmarkExport
{
    public static async Task JsonAsync(Stream output, BenchmarkSession session, CancellationToken cancellationToken)
    {
        BenchmarkSessionStore.Validate(session);
        await JsonSerializer.SerializeAsync(output, new { Session = session, Summary = BenchmarkStatistics.Calculate(session.Frames),
            MetricDefinition = "Present intervals; low = 1000 / mean slowest ceil(N * fraction); quantile type 7. Not displayed FPS or input latency." },
            new JsonSerializerOptions { WriteIndented = true, MaxDepth = 16 }, cancellationToken);
    }

    public static async Task CsvAsync(Stream output, BenchmarkSession session, CancellationToken cancellationToken)
    {
        BenchmarkSessionStore.Validate(session);
        using var writer = new StreamWriter(output, new UTF8Encoding(false), 65536, leaveOpen: true);
        await writer.WriteLineAsync("Sample,ElapsedIntervalMs,MsBetweenPresents,MsCPUBusy,MsGPUBusy,Displayed,FrameType".AsMemory(), cancellationToken);
        double elapsed = 0;
        for (var i = 0; i < session.Frames.Length; i++)
        {
            var f = session.Frames[i]; elapsed += f.IntervalMs;
            string N(double? n) => n?.ToString("R", CultureInfo.InvariantCulture) ?? "";
            await writer.WriteLineAsync($"{i + 1},{N(elapsed)},{N(f.IntervalMs)},{N(f.CpuBusyMs)},{N(f.GpuBusyMs)},{(f.Displayed is null ? "" : f.Displayed.Value ? "1" : "0")},{Escape(f.FrameType ?? "")}".AsMemory(), cancellationToken);
        }
        await writer.FlushAsync(cancellationToken);
    }

    // Text opened by spreadsheet programs must not become formulas.
    private static string Escape(string text)
    {
        if (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') || text.TrimStart().StartsWith('-')
            || text.TrimStart().StartsWith('@') || text.StartsWith('\t') || text.StartsWith('\r')) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
