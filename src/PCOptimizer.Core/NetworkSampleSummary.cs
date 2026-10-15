namespace PCOptimizer.Core;

public sealed record NetworkSampleSummary(int Sent, int Received, double? AverageMs,
    double? MinimumMs, double? MaximumMs, double? JitterMs)
{
    public double LossPercent => Sent == 0 ? 0 : 100.0 * (Sent - Received) / Sent;
    public static NetworkSampleSummary From(IReadOnlyList<long?> samples)
    {
        if (samples.Count == 0 || samples.Any(v => v is < 0)) throw new ArgumentException("Muestras inválidas.", nameof(samples));
        var received = samples.Where(v => v.HasValue).Select(v => (double)v!.Value).ToArray();
        var differences = samples.Zip(samples.Skip(1)).Where(pair => pair.First.HasValue && pair.Second.HasValue)
            .Select(pair => Math.Abs((double)pair.Second!.Value - pair.First!.Value)).ToArray();
        return new(samples.Count, received.Length, received.Length == 0 ? null : received.Average(),
            received.Length == 0 ? null : received.Min(), received.Length == 0 ? null : received.Max(),
            differences.Length == 0 ? null : differences.Average());
    }
}
