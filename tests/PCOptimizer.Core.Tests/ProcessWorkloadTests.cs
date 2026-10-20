using PCOptimizer.Core;
using Xunit;
namespace PCOptimizer.Core.Tests;

public sealed class ProcessWorkloadTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
    private static readonly GameIdentity Browser = new(12, Time, "chrome");
    private static readonly WorkloadContext Context = new(null, null, false, true);
    private static ProcessWorkloadCapture Capture(string name = "chrome") => new(1, Guid.NewGuid(), Time, 100, 5000, 1000, 8,
        [new(Browser with { Name = name }, 1000, 10000000, 1000, 1000)],
        [new(Browser with { Name = name }, 3000, 20000000, 5000, 3000)], 2, 3, []);
    [Fact]
    public void CpuDistinguishesCapacityFromSingleCoreAndIoUsesBytes()
    {
        var row = Assert.Single(ProcessWorkloads.Analyze(Capture(), Context));
        Assert.Equal(50, row.CpuCorePercent); Assert.Equal(6.25, row.CpuCapacityPercent);
        Assert.Equal(2000, row.ReadBytesPerSecond); Assert.Equal(1000, row.WriteBytesPerSecond);
        Assert.Contains("E/S incluye", row.Evidence); Assert.Contains("A/B", row.Recommendation);
    }
    [Fact]
    public void ReusedPidIsTwoUnavailableIdentities()
    {
        var c = Capture(); c = c with { After = [c.After[0] with { Identity = Browser with { StartedUtc = Time.AddSeconds(1) } }] };
        var rows = ProcessWorkloads.Analyze(c, Context);
        Assert.Equal(2, rows.Length); Assert.All(rows, r => Assert.Null(r.CpuCapacityPercent));
    }
    [Fact]
    public void NewAndExitedProcessesNeverBecomeZeroCpu()
    {
        Assert.Null(Assert.Single(ProcessWorkloads.Analyze(Capture() with { Before = [] }, Context)).CpuCapacityPercent);
        Assert.Null(Assert.Single(ProcessWorkloads.Analyze(Capture() with { After = [] }, Context)).CpuCapacityPercent);
    }
    [Fact]
    public void GameAndCollectorAreProtectedByCreationTimeNotName()
    {
        Assert.Equal(WorkloadRole.SelectedGame, Assert.Single(ProcessWorkloads.Analyze(Capture(), Context with { Game = Browser })).Role);
        Assert.Equal(WorkloadRole.Collector, Assert.Single(ProcessWorkloads.Analyze(Capture(), Context with { Collector = Browser })).Role);
        Assert.Equal(WorkloadRole.Browser, Assert.Single(ProcessWorkloads.Analyze(Capture(), Context with { Game = Browser with { StartedUtc = Time.AddSeconds(1) } })).Role);
    }
    [Fact]
    public void StreamingAndVoiceArePreservedEvenUnderLoad()
    {
        Assert.Contains("Conserva", Assert.Single(ProcessWorkloads.Analyze(Capture("obs64"), Context with { Streaming = true })).Recommendation);
        Assert.Contains("conservar", Assert.Single(ProcessWorkloads.Analyze(Capture("discord"), Context)).Recommendation);
        Assert.Contains("A/B", Assert.Single(ProcessWorkloads.Analyze(Capture("obs64"), Context)).Recommendation);
    }
    [Theory]
    [InlineData("MsMpEng")]
    [InlineData("svchost")]
    [InlineData("audiodg")]
    [InlineData("cod25-cod")]
    [InlineData("unknown-app")]
    [InlineData("vgc")]
    public void UnknownAndSystemProcessesNeverReceiveCloseAdvice(string name)
    {
        var row = Assert.Single(ProcessWorkloads.Analyze(Capture(name), Context));
        Assert.Equal(WorkloadRole.ObservationOnly, row.Role); Assert.StartsWith("Solo observación", row.Recommendation);
    }
    [Fact]
    public void ResetsMissingIoAndImpossibleCpuAreUnavailable()
    {
        var c = Capture(); c = c with { After = [c.After[0] with { ReadBytes = 0, WriteBytes = null, CpuTime100Ns = 9000000000 }] };
        var row = Assert.Single(ProcessWorkloads.Analyze(c, Context));
        Assert.Null(row.CpuCapacityPercent); Assert.Null(row.CpuCorePercent); Assert.Null(row.ReadBytesPerSecond); Assert.Null(row.WriteBytesPerSecond);
        c = c with { After = [c.After[0] with { CpuTime100Ns = 0 }] };
        Assert.Null(Assert.Single(ProcessWorkloads.Analyze(c, Context)).CpuCapacityPercent);
    }
    [Fact]
    public void DuplicateIdsAndOverlappingOrInvalidCountersAreRejected()
    {
        var c = Capture();
        foreach (var broken in new[]{c with{Before=[c.Before[0],c.Before[0]]},c with{LogicalProcessors=0},c with{QpcFrequency=0},
            c with{EndedQpc=long.MaxValue},c with{After=[c.After[0] with{Qpc=c.Before[0].Qpc}]},c with{After=[c.After[0] with{CpuTime100Ns=-1}]}})
            Assert.Throws<InvalidDataException>(() => ProcessWorkloads.Validate(broken));
    }
    [Fact]
    public void CancellationDoesNotReturnPartialAnalysis()
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Throws<OperationCanceledException>(() => ProcessWorkloads.Analyze(Capture(), Context, token.Token));
    }
    [Fact]
    public void CaptureAndContextRoundTripAndReportsExplainUnits()
    {
        var experiment = new WorkloadExperiment(1, Capture(), Context with { Game = Browser }, GamingGoal.Balanced, true);
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var path = Path.Combine(directory, "workload.json");
        try
        {
            WorkloadExperiment.Validate(experiment); LocalJsonFile.Write(path, experiment);
            var reopened = LocalJsonFile.Read<WorkloadExperiment>(path)!; WorkloadExperiment.Validate(reopened);
            Assert.Equal(experiment.Context, reopened.Context);
            Assert.Equal(ProcessWorkloads.Analyze(experiment.Capture, experiment.Context)[0], ProcessWorkloads.Analyze(reopened.Capture, reopened.Context)[0]);
            Assert.Contains("GetProcessTimes", ProcessWorkloads.Report(reopened.Capture, reopened.Context));
            Assert.Throws<InvalidDataException>(() => WorkloadExperiment.Validate(experiment with { Context = Context with { Game = Browser with { StartedUtc = Time.AddDays(1) } } }));
            Assert.Throws<InvalidDataException>(() => WorkloadExperiment.Validate(experiment with { Goal = GamingGoal.Streaming }));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void OnlyExplicitCompetitiveWithoutClipsSelectsCaptureChange()
    {
        foreach (var goal in Enum.GetValues<GamingGoal>()) foreach (var uses in new[] { true, false })
        {
            var plan = GamingGoals.Plan(goal, uses); TweakProfiles.Validate(plan.Tweaks);
            Assert.Equal(goal == GamingGoal.Competitive && !uses, plan.Tweaks.TweakIds.Contains("game-capture"));
            Assert.DoesNotContain("mouse-acceleration", plan.Tweaks.TweakIds);
            Assert.DoesNotContain("background-apps", plan.Tweaks.TweakIds);
            Assert.Contains("A/B", plan.Validation);
        }
        Assert.Throws<InvalidDataException>(() => GamingGoals.Plan((GamingGoal)100, false));
    }
}
