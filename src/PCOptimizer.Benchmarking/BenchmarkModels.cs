namespace PCOptimizer.Benchmarking;

public sealed record CaptureStream(string Application, int ProcessId, string SwapChain)
{
    public override string ToString() => $"{Application} · PID {ProcessId} · {SwapChain}";
}
public sealed record FrameSample(double IntervalMs, double? CpuBusyMs, double? GpuBusyMs,
    bool? Displayed, string? FrameType);
public sealed record ImportedCapture(string Sha256, IReadOnlyDictionary<CaptureStream, FrameSample[]> Streams, int MissingIntervals);
public sealed record BenchmarkContext(string Hardware, string GameVersion, string DriverVersion,
    string WindowsVersion, string Settings, string Scenario);
public sealed record BenchmarkSession(int SchemaVersion, Guid Id, DateTimeOffset ImportedAtUtc,
    string SourceSha256, CaptureStream Stream, BenchmarkContext Context, FrameSample[] Frames, int MissingIntervals);
public sealed record BenchmarkSummary(int Samples, double ObservedIntervalSeconds, double AverageFps,
    double MedianInstantaneousFps, double Low1Fps, double Low01Fps,
    double MedianMs, double P95Ms, double P99Ms, double StandardDeviationMs, int Spikes,
    double? AverageCpuBusyMs, double? AverageGpuBusyMs, int CpuBusySamples, int GpuBusySamples,
    int? DisplayedRows, int? NotDisplayedRows, int FrameTypeRows);
public sealed record SessionReference(Guid Id, DateTimeOffset SavedAtUtc)
{
    public override string ToString() => $"{SavedAtUtc.ToLocalTime():g} · {Id.ToString("N")[..8]}";
}
public enum CapabilityState { Available, Unsupported, PermissionRequired, ValidationRequired }
public sealed record BenchmarkCapability(CapabilityState State, string Detail);

public interface IBenchmarkSessionStore
{
    Task SaveAsync(BenchmarkSession session, CancellationToken cancellationToken);
    Task<BenchmarkSession> LoadAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionReference>> ListAsync(CancellationToken cancellationToken);
}
