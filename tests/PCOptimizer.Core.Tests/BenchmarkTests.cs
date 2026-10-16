using System.Text;
using PCOptimizer.Benchmarking;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class BenchmarkTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-bench-test-" + Guid.NewGuid().ToString("N"));
    private static FrameSample F(double n) => new(n, null, null, null, null);
    private static BenchmarkSession Session(params FrameSample[] frames) => new(1, Guid.NewGuid(), DateTimeOffset.UtcNow,
        new string('A', 64), new("game.exe", 123, "0x1"), new("CPU/GPU/RAM", "game 1.0", "driver 1.0", "Windows 11", "1080p FG off", "route 60s; variable: power plan"), frames, 0);
    private async Task<string> Csv(string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid() + ".csv");
        await File.WriteAllTextAsync(path, text); return path;
    }
    [Fact]
    public void MetricsUseTimeWeightedRateAndExplicitTailMean()
    {
        var frames = Enumerable.Repeat(F(10), 99).Append(F(100)).ToArray();
        var s = BenchmarkStatistics.Calculate(frames);
        Assert.Equal(100000.0 / 1090, s.AverageFps, 9);
        Assert.Equal(100, s.MedianInstantaneousFps);
        Assert.Equal(10, s.Low1Fps);
        Assert.Equal(10, s.Low01Fps);
        Assert.Equal(10.9, s.P99Ms, 8); // type 7 differs from slowest-tail mean.
        Assert.Equal(1, s.Spikes);
        Assert.Null(s.DisplayedRows);
        Assert.Null(s.AverageGpuBusyMs);
    }
    [Fact]
    public void TailUsesCeilingAndMedianFpsIsNotReciprocalOfMedianMs()
    {
        var s = BenchmarkStatistics.Calculate([F(10), F(20)]);
        Assert.Equal(75, s.MedianInstantaneousFps);
        Assert.Equal(15, s.MedianMs);
        Assert.Equal(50, s.Low1Fps);
        Assert.Equal(1000 / 15.0, s.AverageFps, 8);
    }
    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-1)] [InlineData(0)] [InlineData(1e-320)] [InlineData(3600001)]
    public void InvalidIntervalsNeverProduceFps(double interval) => Assert.Throws<InvalidDataException>(() => BenchmarkStatistics.Calculate([F(10), F(interval)]));
    [Fact]
    public async Task CsvSeparatesApplicationsPidsAndSwapchainsAndParsesQuotedNames()
    {
        var path = await Csv("Application,ProcessID,SwapChainAddress,MsBetweenPresents,Dropped,MsGPUBusy\n\"game, one.exe\",7,0x1,10,0,4\n\"game, one.exe\",7,0x1,20,1,NA\nother.exe,8,0x1,5,0,1\nother.exe,8,0x1,5,0,1\nother.exe,8,0x2,8,0,1\nother.exe,8,0x2,8,0,1\n");
        var imported = await PresentMonCsv.ImportAsync(path, default);
        Assert.Equal(3, imported.Streams.Count);
        Assert.Equal(64, imported.Sha256.Length);
        var s = BenchmarkStatistics.Calculate(imported.Streams[new("game, one.exe", 7, "0x1")]);
        Assert.Equal(1, s.DisplayedRows); Assert.Equal(1, s.NotDisplayedRows);
        Assert.Equal(4, s.AverageGpuBusyMs); Assert.Equal(1, s.GpuBusySamples);
    }
    [Theory]
    [InlineData("NaN")] [InlineData("Infinity")] [InlineData("-4")] [InlineData("oops")]
    public async Task CsvRejectsInvalidNumbers(string value)
    {
        var path = await Csv($"Application,ProcessID,SwapChainAddress,MsBetweenPresents\ngame.exe,1,0x1,{value}\ngame.exe,1,0x1,10\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => PresentMonCsv.ImportAsync(path, default));
    }
    [Fact]
    public async Task MissingIntervalsAreDisclosedAndNeverTurnedIntoZeroFps()
    {
        var path = await Csv("Application,ProcessID,SwapChainAddress,MsBetweenPresents\ngame.exe,1,0x1,0\ngame.exe,1,0x1,NA\ngame.exe,1,0x1,10\ngame.exe,1,0x1,20\n");
        var imported = await PresentMonCsv.ImportAsync(path, default);
        Assert.Equal(2, imported.MissingIntervals); Assert.Equal(2, imported.Streams.Single().Value.Length);
    }
    [Theory]
    [InlineData("Application,ProcessID,SwapChainAddress,MsCPUBusy\ngame.exe,1,0x1,10\n")]
    [InlineData("Application,ProcessID,SwapChainAddress,MsBetweenPresents,ProcessID\ngame.exe,1,0x1,10,1\n")]
    [InlineData("Application,ProcessID,SwapChainAddress,MsBetweenPresents\ngame.exe,1,0x1\n")]
    public async Task CsvRejectsAmbiguousOrIncompleteSchemas(string csv)
    {
        var path = await Csv(csv);
        await Assert.ThrowsAnyAsync<Exception>(() => PresentMonCsv.ImportAsync(path, default));
    }
    [Fact]
    public async Task CanceledImportDoesNotReadCapture()
    {
        var path = await Csv("Application,ProcessID,SwapChainAddress,MsBetweenPresents\ngame.exe,1,0x1,10\ngame.exe,1,0x1,10\n");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PresentMonCsv.ImportAsync(path, cts.Token));
    }
    [Fact]
    public async Task SessionsAreImmutableAndStatisticsAreRecomputed()
    {
        var store = new BenchmarkSessionStore(directory); var session = Session(F(10), F(20));
        await store.SaveAsync(session, default);
        var loaded = await store.LoadAsync(session.Id, default);
        Assert.Equal(session.Frames, loaded.Frames);
        Assert.Equal(1000 / 15.0, BenchmarkStatistics.Calculate(loaded.Frames).AverageFps, 8);
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(session, default));
        Assert.Single(await store.ListAsync(default));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }
    [Fact]
    public async Task CorruptSessionIsPreserved()
    {
        Directory.CreateDirectory(directory); var id = Guid.NewGuid(); var path = Path.Combine(directory, id.ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "{broken");
        await Assert.ThrowsAnyAsync<Exception>(() => new BenchmarkSessionStore(directory).LoadAsync(id, default));
        Assert.Equal("{broken", await File.ReadAllTextAsync(path));
    }
    [Fact]
    public void SessionValidationRejectsWrongSchemaNullsAndIdentity()
    {
        var s = Session(F(10), F(20));
        Assert.Throws<InvalidDataException>(() => BenchmarkSessionStore.Validate(s with { SchemaVersion = 2 }));
        Assert.Throws<InvalidDataException>(() => BenchmarkSessionStore.Validate(s with { Stream = s.Stream with { ProcessId = 0 } }));
        Assert.Throws<InvalidDataException>(() => BenchmarkSessionStore.Validate(s with { Context = null! }));
    }
    [Fact]
    public async Task CsvExportNeutralizesSpreadsheetFormulasAndKeepsNumericPrecision()
    {
        using var output = new MemoryStream();
        await BenchmarkExport.CsvAsync(output, Session(F(12.34567) with { FrameType = "=HYPERLINK(\"bad\")" }, F(20)), default);
        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("12.34567", text); Assert.Contains("\"'=HYPERLINK", text);
    }
    [Fact]
    public void CompareRefusesMissingContextAndNeverClaimsCausality()
    {
        var a = Session(F(10), F(20)); var b = Session(F(8), F(18)) with { SourceSha256 = new string('B', 64) };
        Assert.Contains("exploratoria", BenchmarkStatistics.Compare(a, b));
        Assert.Contains("no demuestra causalidad", BenchmarkStatistics.Compare(a, b));
        Assert.StartsWith("No comparable", BenchmarkStatistics.Compare(a, b with { Context = b.Context with { DriverVersion = "" } }));
        Assert.Throws<InvalidOperationException>(() => BenchmarkStatistics.Compare(a, a));
    }
    [Fact]
    public void CaptureArgumentsAreBoundedPidSpecificAndNeverStopAnotherSession()
    {
        var args = PresentMonVersion.CaptureArguments(123, 60, Guid.NewGuid().ToString("N"), Path.GetFullPath("sample.csv"));
        Assert.Contains("--process_id", args); Assert.DoesNotContain("--restart_as_admin", args);
        Assert.DoesNotContain("--stop_existing_session", args);
        Assert.Throws<ArgumentException>(() => PresentMonVersion.CaptureArguments(1, 0, "PresentMon", "relative.csv"));
    }
    private static BenchmarkSession Repetition(double fps, char hash)
    {
        var count = (int)fps;
        return Session(Enumerable.Repeat(F(1000.0 / fps), count).ToArray()) with { SourceSha256 = new string(hash, 64) };
    }
    [Fact]
    public void RepeatedComparisonUsesIndependentRunMeansAndWelchUncertainty()
    {
        var a = new[] { Repetition(99, 'A'), Repetition(100, 'B'), Repetition(101, 'C') };
        var b = new[] { Repetition(119, 'D'), Repetition(120, 'E'), Repetition(121, 'F') };
        var r = RepeatedComparison.Calculate(a, b);
        Assert.Equal(20, r.DifferenceFps!.Value, 8);
        Assert.InRange(r.Lower95Fps!.Value, 17.7, 17.8);
        Assert.InRange(r.Upper95Fps!.Value, 22.2, 22.3);
        Assert.Contains("positiva", r.Conclusion);
    }
    [Fact]
    public void RepeatedComparisonRefusesPseudoreplicationAndInsufficientRuns()
    {
        var a = Repetition(100, 'A'); var b = Repetition(120, 'B');
        Assert.Null(RepeatedComparison.Calculate([a], [b]).DifferenceFps);
        Assert.Throws<InvalidOperationException>(() => RepeatedComparison.Calculate([a,a,a], [b,b,b]));
    }
    [Fact]
    public void RepeatedComparisonCanBeInconclusiveOrShowRegression()
    {
        var a = new[] { Repetition(99, 'A'), Repetition(100, 'B'), Repetition(101, 'C') };
        var same = new[] { Repetition(99, 'D'), Repetition(100, 'E'), Repetition(101, 'F') };
        Assert.Contains("Inconcluso", RepeatedComparison.Calculate(a, same).Conclusion);
        var slower = new[] { Repetition(79, 'D'), Repetition(80, 'E'), Repetition(81, 'F') };
        Assert.Contains("Regresión", RepeatedComparison.Calculate(a, slower).Conclusion);
        var flatA = new[] { Repetition(100,'A'), Repetition(100,'B'), Repetition(100,'C') };
        var flatB = new[] { Repetition(120,'D'), Repetition(120,'E'), Repetition(120,'F') };
        Assert.Null(RepeatedComparison.Calculate(flatA, flatB).Lower95Fps);
    }
    [Fact]
    public async Task CaptureStopCompletesCleanupAndReturnsPartialCaptureControl()
    {
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = false;
        var wait = CaptureLifetime.WaitAsync(async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); },
            () => { cleanup = true; return Task.CompletedTask; }, TimeSpan.FromSeconds(30), cts.Token);
        await started.Task; cts.Cancel();
        Assert.Equal(CaptureCompletion.Stopped, await wait);
        Assert.True(cleanup);
    }
    [Fact]
    public async Task CaptureTimeoutAndNormalExitAreDistinct()
    {
        var cleanup = 0;
        var timed = await CaptureLifetime.WaitAsync(token => Task.Delay(Timeout.Infinite, token),
            () => { cleanup++; return Task.CompletedTask; }, TimeSpan.FromMilliseconds(20), default);
        Assert.Equal(CaptureCompletion.TimedOut, timed); Assert.Equal(1, cleanup);
        var normal = await CaptureLifetime.WaitAsync(_ => Task.CompletedTask,
            () => { cleanup++; return Task.CompletedTask; }, TimeSpan.FromSeconds(1), default);
        Assert.Equal(CaptureCompletion.Finished, normal); Assert.Equal(1, cleanup);
    }
    [Fact]
    public async Task CaptureCleanupFailureIsReported()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAsync<IOException>(() => CaptureLifetime.WaitAsync(token => Task.Delay(Timeout.Infinite, token),
            () => throw new IOException("stop failed"), TimeSpan.FromSeconds(1), cts.Token));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
