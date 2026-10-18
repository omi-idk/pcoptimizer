using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class ResourceDiagnosticsTests
{
    private static ResourceCapture Capture(Func<int, ResourceSample> sample) => new(DateTimeOffset.UtcNow, Enumerable.Range(0, 20).Select(sample).ToArray(), []);
    private static ResourceSample Sample(int i) => new(i, [new("0", 10), new("1", 15)], 8000, 16000, 50, 0);
    [Fact]
    public void PerLogicalCpuSaturationDoesNotRequireHighTotalCpu()
    {
        var c = Capture(i => Sample(i) with { LogicalProcessors = [new("0", 95), new("1", 1)] });
        var f = ResourceDiagnostics.Analyze(c);
        Assert.Contains(f, x => x.Title.Contains("lógico 0") && x.Confidence == DiagnosticConfidence.Moderate);
        Assert.Contains("no de un hilo del juego", f[0].Evidence);
    }
    [Fact]
    public void ShortOrSparseCaptureDoesNotDiagnose()
    {
        var c = Capture(Sample);
        Assert.Single(ResourceDiagnostics.Analyze(c with { Samples = c.Samples.Take(5).ToArray() }));
        Assert.Equal(DiagnosticConfidence.Insufficient, ResourceDiagnostics.Analyze(c with { Samples = c.Samples.Select((s, i) => s with { ElapsedSeconds = i * 6 }).ToArray() })[0].Confidence);
    }
    [Fact]
    public void MissingCountersDoNotMeanNoPressure()
    {
        var c = Capture(i => new(i, [], null, null, null, null));
        Assert.Contains(ResourceDiagnostics.Analyze(c), f => f.Title.Contains("Cobertura") && f.Confidence == DiagnosticConfidence.Insufficient);
        Assert.DoesNotContain(ResourceDiagnostics.Analyze(c), f => f.Title.StartsWith("Sin presión"));
    }
    [Fact]
    public void MemoryHypothesisRequiresConcurrentLowAvailableHighCommitAndPageInputs()
    {
        var c = Capture(i => Sample(i) with { AvailableMemoryMb = 300, CommitPercent = 95, PagesInputPerSecond = 20 });
        Assert.Contains(ResourceDiagnostics.Analyze(c), f => f.Title.Contains("Presión de memoria"));
        Assert.DoesNotContain(ResourceDiagnostics.Analyze(Capture(i => Sample(i) with { AvailableMemoryMb = 300 })), f => f.Title.Contains("Presión de memoria"));
    }
    [Fact]
    public void MigratingOrBriefCpuLoadIsNotLabeledSustained()
    {
        var c = Capture(i => Sample(i) with { LogicalProcessors = [new("0", i % 2 == 0 ? 95 : 0), new("1", i % 2 == 1 ? 95 : 0)] });
        Assert.DoesNotContain(ResourceDiagnostics.Analyze(c), f => f.Title.StartsWith("Carga sostenida"));
    }
    [Fact]
    public void MalformedValuesAndDuplicatesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => ResourceDiagnostics.Analyze(Capture(i => Sample(i) with { ElapsedSeconds = double.NaN })));
        Assert.Throws<InvalidDataException>(() => ResourceDiagnostics.Analyze(Capture(i => Sample(i) with { LogicalProcessors = [new("0", 10), new("0", 20)] })));
        Assert.Throws<InvalidDataException>(() => ResourceDiagnostics.Analyze(Capture(i => Sample(i) with { AvailableMemoryMb = 20000 })));
    }
    [Fact]
    public void KnownProfileDoesNotInferVramFromGpuName()
    {
        var ram = Enumerable.Range(0, 4).Select(i => new MemoryModuleDetails(i.ToString(), 8L * 1024 * 1024 * 1024, 3200, 3200)).ToArray();
        var hardware = new ProfileHardware("AMD Ryzen 7 5800X3D 8-Core Processor", "ASUSTeK ROG STRIX B550-F GAMING WIFI II", "NVIDIA GeForce RTX 5060 Ti", null, ram, true);
        var checks = KnownGamingProfile.Evaluate(hardware);
        Assert.Equal(ProfileCheckState.Unknown, checks.Single(c => c.Name == "VRAM dedicada").State);
        Assert.Equal(ProfileCheckState.Match, checks.Single(c => c.Name == "CPU").State);
        Assert.Equal(ProfileCheckState.Match, checks.Single(c => c.Name == "Placa").State);
        Assert.Equal(ProfileCheckState.Match, KnownGamingProfile.Evaluate(hardware with { DedicatedVramBytes = 16L * 1024 * 1024 * 1024 }).Single(c => c.Name == "VRAM dedicada").State);
        Assert.Equal(ProfileCheckState.Mismatch, KnownGamingProfile.Evaluate(hardware with { DedicatedVramBytes = 8L * 1024 * 1024 * 1024 }).Single(c => c.Name == "VRAM dedicada").State);
        TweakProfiles.Validate(KnownGamingProfile.BaseTweaks);
        Assert.DoesNotContain("hags", KnownGamingProfile.BaseTweaks.TweakIds);
    }
    [Fact]
    public void NetworkRateRejectsCounterResets()
    {
        Assert.Null(NetworkCounterRate.Calculate(100, 1, 100, 200, 2).ReceiveBytesPerSecond);
        Assert.Null(NetworkCounterRate.Calculate(0, long.MaxValue, 0, long.MaxValue, double.Epsilon).ReceiveBytesPerSecond);
        var rate = NetworkCounterRate.Calculate(100, 2100, 100, 1100, 2);
        Assert.Equal(1000, rate.ReceiveBytesPerSecond); Assert.Equal(500, rate.SendBytesPerSecond);
    }
}
