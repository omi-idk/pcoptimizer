using PCOptimizer.Benchmarking;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class PerformanceIntelligenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-performance-" + Guid.NewGuid().ToString("N"));
    private static BenchmarkSession Experiment(bool correlated = true)
    {
        var frames = Enumerable.Range(0, 120).Select(i => new FrameSample(i is 10 or 30 or 50 or 70 or 90 ? 80 : 10, null, null, null, null)
        { CpuStartQpc = 1_001_000 + i * 1000 }).ToArray();
        var observations = frames.Select(f => new SensorObservation(f.CpuStartQpc!.Value - 1, f.CpuStartQpc.Value + 1, DateTimeOffset.UtcNow,
            [new(SensorMetric.GpuUtilizationPercent, "GPU 0", "Test provider", "%", !correlated || f.IntervalMs > 50 ? 95 : 30, SensorAvailability.Available, "Synthetic fixture; not a hardware measurement.")])).ToArray();
        var trace = new PerformanceTrace(1, Guid.NewGuid(), 1000, 1_000_000, 1_121_000, DateTimeOffset.UtcNow, observations, [], 12);
        return new(1, Guid.NewGuid(), DateTimeOffset.UtcNow, new string('A', 64), new("fixture.exe", 123, "0x1"),
            new("test hardware", "test game", "driver", "Windows", "1080p FG off", "controlled fixture"), frames, 0)
        { Telemetry = trace };
    }
    [Fact]
    public void RepeatedCoincidenceProducesAuditableLowConfidenceHypothesis()
    {
        var report = PerformanceAnalysis.Calculate(Experiment());
        Assert.Equal(5, report.Spikes); Assert.Equal(120, report.TimedFrames);
        var association = Assert.Single(report.Associations);
        Assert.Equal(120, association.MatchedFrames); Assert.Equal(5, association.HighSpikes);
        Assert.Equal(0, association.HighNormalFrames); Assert.Equal(5, association.DistinctSpikeObservations);
        var finding = Assert.Single(report.Findings);
        Assert.StartsWith("Coincidencia", finding.Finding); Assert.StartsWith("Baja", finding.Confidence);
        Assert.Contains("3–5", finding.Experiment); Assert.Contains("causalidad", string.Join(" ", report.Limitations));
    }
    [Fact]
    public void UniformHighLoadDoesNotExplainSpikes()
    {
        var report = PerformanceAnalysis.Calculate(Experiment(correlated: false));
        Assert.Equal("Insuficiente", Assert.Single(report.Findings).Confidence);
        Assert.Equal(115, Assert.Single(report.Associations).HighNormalFrames);
    }
    [Fact]
    public void MissingTraceAndLegacyFramesNeverGetInventedTime()
    {
        var session = Experiment();
        var withoutTrace = PerformanceAnalysis.Calculate(session with { Telemetry = null });
        Assert.Empty(withoutTrace.Associations);
        var legacy = PerformanceAnalysis.Calculate(session with { Frames = session.Frames.Select(f => f with { CpuStartQpc = null }).ToArray() });
        Assert.Equal(0, legacy.TimedFrames); Assert.Empty(legacy.Associations);
        Assert.Equal("Insuficiente", Assert.Single(legacy.Findings).Confidence);
    }
    [Fact]
    public void UnavailableIsNotZeroAndSlowReadingsCannotSupportFinding()
    {
        var session = Experiment();
        var absent = session.Telemetry! with
        {
            Observations = session.Telemetry!.Observations.Select(o => o with
            { Readings = o.Readings.Select(r => r with { Value = null, Availability = SensorAvailability.Unavailable }).ToArray() }).ToArray()
        };
        Assert.Equal(0, Assert.Single(PerformanceAnalysis.Calculate(session with { Telemetry = absent }).Associations).MatchedFrames);
        var slow = session.Telemetry with { Observations = session.Telemetry.Observations.Select(o => o with { EndQpc = o.StartQpc + 300 }).ToArray() };
        Assert.Equal(0, Assert.Single(PerformanceAnalysis.Calculate(session with { Telemetry = slow }).Associations).MatchedFrames);
        var averaged = session.Telemetry with { Observations = session.Telemetry.Observations.Select(o => o with
            { Readings = [o.Readings[0] with { AggregationSeconds = 8 }] }).ToArray() };
        Assert.Equal(0, Assert.Single(PerformanceAnalysis.Calculate(session with { Telemetry = averaged }).Associations).MatchedFrames);
    }
    [Fact]
    public void GapsDoNotInterpolateOrExtrapolateAndInsufficientCoverageCannotDiagnose()
    {
        var session = Experiment();
        var sparse = session.Telemetry! with { Observations = session.Telemetry!.Observations.Where((_, i) => i % 10 == 0).ToArray() };
        var report = PerformanceAnalysis.Calculate(session with { Telemetry = sparse });
        Assert.Equal(12, Assert.Single(report.Associations).MatchedFrames);
        Assert.Equal("Insuficiente", Assert.Single(report.Findings).Confidence);
        var outside = session with { Frames = session.Frames.Select(f => f with { CpuStartQpc = f.CpuStartQpc + 999999 }).ToArray() };
        Assert.Equal(0, Assert.Single(PerformanceAnalysis.Calculate(outside).Associations).MatchedFrames);
    }
    [Fact]
    public void UnavailableNeighborAndWrongPidCannotBorrowMeasurements()
    {
        var session = Experiment();
        var observations = session.Telemetry!.Observations;
        // A valid neighbor 400 ms away must not replace the nearest unavailable sample.
        var frame = session.Frames[10] with { CpuStartQpc = observations[10].StartQpc + 100 };
        var unavailable = observations[10] with { Readings = [observations[10].Readings[0] with { Value = null, Availability = SensorAvailability.Unavailable }] };
        var near = observations[10] with { StartQpc = unavailable.EndQpc + 300, EndQpc = unavailable.EndQpc + 302 };
        var trace = session.Telemetry with { Observations = [unavailable, near] };
        var report = PerformanceAnalysis.Calculate(session with { Frames = [frame, frame with { CpuStartQpc = frame.CpuStartQpc + 1 }], Telemetry = trace });
        Assert.Equal(0, Assert.Single(report.Associations).MatchedFrames);
        report = PerformanceAnalysis.Calculate(session with { Telemetry = session.Telemetry with { TargetProcessId = 999, TargetStartUtc = DateTimeOffset.UtcNow } });
        Assert.Empty(report.Associations);
    }
    [Fact]
    public void ManyFramesFromOneSensorReadAreNotIndependentEvidence()
    {
        var session = Experiment();
        var frames = session.Frames.Select((f, i) => f with { CpuStartQpc = 1_010_000 + i }).ToArray();
        var trace = session.Telemetry! with
        {
            Observations = [new(1_010_000, 1_010_001, DateTimeOffset.UtcNow,
            [new(SensorMetric.GpuUtilizationPercent, "GPU 0", "Test provider", "%", 95, SensorAvailability.Available, "fixture")])]
        };
        var report = PerformanceAnalysis.Calculate(session with { Frames = frames, Telemetry = trace });
        Assert.Equal(1, Assert.Single(report.Associations).DistinctSpikeObservations);
        Assert.Equal("Insuficiente", Assert.Single(report.Findings).Confidence);
    }
    [Fact]
    public void MultipleGpusAreKeptSeparateAndTemperatureIsNotThrottling()
    {
        var session = Experiment();
        var trace = session.Telemetry! with
        {
            Observations = session.Telemetry!.Observations.Select(o => o with
            {
                Readings =
            [o.Readings[0], o.Readings[0] with { Device = "GPU 1", Value = 10 },
                new(SensorMetric.GpuTemperatureC, "GPU 0", "Test provider", "°C", 95, SensorAvailability.Available, "fixture")]
            }).ToArray()
        };
        var report = PerformanceAnalysis.Calculate(session with { Telemetry = trace });
        Assert.Equal(3, report.Associations.Length);
        Assert.Contains("GPU 0", Assert.Single(report.Findings).Finding);
        Assert.DoesNotContain("throttling", report.Findings[0].Finding);
    }
    [Fact]
    public void InvalidOrAmbiguousTimeIsRejected()
    {
        var session = Experiment();
        Assert.Throws<InvalidDataException>(() => PerformanceAnalysis.Calculate(session with { Frames = session.Frames.Reverse().ToArray() }));
        Assert.Throws<InvalidDataException>(() => PerformanceTrace.Validate(session.Telemetry! with { QpcFrequency = 0 }));
        Assert.Throws<InvalidDataException>(() => PerformanceTrace.Validate(session.Telemetry! with { EndedQpc = long.MaxValue }));
        Assert.Throws<InvalidDataException>(() => PerformanceTrace.Validate(session.Telemetry! with { Observations = session.Telemetry!.Observations.Reverse().ToArray() }));
    }
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidSensorValuesAreRejected(double value)
    {
        var trace = Experiment().Telemetry!;
        var first = trace.Observations[0];
        Assert.Throws<InvalidDataException>(() => PerformanceTrace.Validate(trace with { Observations = [first with { Readings = [first.Readings[0] with { Value = value }] }] }));
    }
    [Fact]
    public void WrongUnitsDuplicateReadingsAndUnavailableValuesAreRejected()
    {
        var trace = Experiment().Telemetry!; var first = trace.Observations[0]; var reading = first.Readings[0];
        foreach (var readings in new[] { new[] { reading with { Unit = "MHz" } }, [reading, reading], [reading with { Availability = SensorAvailability.Unavailable }] })
            Assert.Throws<InvalidDataException>(() => PerformanceTrace.Validate(trace with { Observations = [first with { Readings = readings }] }));
    }
    [Fact]
    public async Task ExperimentRoundTripPreservesTelemetryAndRecomputesSameEvidence()
    {
        var store = new BenchmarkSessionStore(directory); var session = Experiment();
        await store.SaveAsync(session, default);
        var reopened = await store.LoadAsync(session.Id, default);
        Assert.Equal(session.Telemetry!.CaptureId, reopened.Telemetry!.CaptureId);
        Assert.Equal(session.Frames[0].CpuStartQpc, reopened.Frames[0].CpuStartQpc);
        Assert.Equal(session.Telemetry.Observations.Length, reopened.Telemetry.Observations.Length);
        Assert.Equal(PerformanceAnalysis.Calculate(session).Findings[0], PerformanceAnalysis.Calculate(reopened).Findings[0]);
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(session, default));
    }
    [Fact]
    public async Task CanceledSavePreservesExistingSessionAndLeavesNoTemporaryFile()
    {
        var store = new BenchmarkSessionStore(directory); var session = Experiment();
        await store.SaveAsync(session, default);
        var before = await File.ReadAllBytesAsync(Path.Combine(directory, session.Id.ToString("N") + ".json"));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(session with { Id = Guid.NewGuid() }, cancel.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(directory, session.Id.ToString("N") + ".json")));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        Assert.Throws<OperationCanceledException>(() => PerformanceAnalysis.Calculate(session, cancel.Token));
    }
    [Fact]
    public async Task ParserPreservesExactQpcTicksBeyondDoubleIntegerPrecision()
    {
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "source.csv");
        await File.WriteAllTextAsync(path, "Application,ProcessID,SwapChainAddress,MsBetweenPresents,CPUStartQPC\ngame.exe,1,0x1,10,9007199254740993\ngame.exe,1,0x1,20,9007199254740994\n");
        var result = await PresentMonCsv.ImportAsync(path, default);
        Assert.Equal(9007199254740993, result.Streams.Single().Value[0].CpuStartQpc);
        await File.WriteAllTextAsync(path, "Application,ProcessID,SwapChainAddress,MsBetweenPresents,CPUStartQPC\ngame.exe,1,0x1,10,1.5\ngame.exe,1,0x1,20,10\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => PresentMonCsv.ImportAsync(path, default));
    }
    [Fact]
    public async Task ExportsIncludeRawClockSensorsCoverageAndLimits()
    {
        var session = Experiment();
        using var stream = new MemoryStream();
        await BenchmarkExport.JsonAsync(stream, session, default);
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("QpcFrequency", json); Assert.Contains("CpuStartQpc", json); Assert.Contains("Associations", json);
        var markdown = PerformanceAnalysis.ToMarkdown(session, PerformanceAnalysis.Calculate(session));
        Assert.Contains(session.SourceSha256, markdown); Assert.Contains("Confianza", markdown); Assert.Contains("Límites", markdown);
        using var csv = new MemoryStream(); await BenchmarkExport.CsvAsync(csv, session, default);
        Assert.Contains("CPUStartQPC", System.Text.Encoding.UTF8.GetString(csv.ToArray()));
        Assert.Contains("--qpc_time", PresentMonVersion.CaptureArguments(1, 5, Guid.NewGuid().ToString("N"), Path.GetFullPath("capture.csv")));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
