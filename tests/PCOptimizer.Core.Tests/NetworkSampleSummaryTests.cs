using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class NetworkSampleSummaryTests
{
    [Fact]
    public void ReportsLossWithoutInventingLatencyForTimeouts()
    {
        var summary = NetworkSampleSummary.From([10, 20, null, 40]);
        Assert.Equal(4, summary.Sent);
        Assert.Equal(3, summary.Received);
        Assert.Equal(25, summary.LossPercent);
        Assert.Equal(70.0 / 3, summary.AverageMs);
        Assert.Equal(10, summary.MinimumMs);
        Assert.Equal(40, summary.MaximumMs);
        Assert.Equal(10, summary.JitterMs); // Never bridge the timeout with an invented consecutive pair.
    }

    [Fact]
    public void BlockedIcmpHasNoFakeZeroLatencyOrJitter()
    {
        var summary = NetworkSampleSummary.From([null, null]);
        Assert.Equal(100, summary.LossPercent);
        Assert.Null(summary.AverageMs);
        Assert.Null(summary.MinimumMs);
        Assert.Null(summary.MaximumMs);
        Assert.Null(summary.JitterMs);
    }

    [Fact]
    public void OneReplyCannotEstablishVariation()
    {
        var summary = NetworkSampleSummary.From([null, 5, null]);
        Assert.Equal(5, summary.AverageMs);
        Assert.Null(summary.JitterMs);
    }

    [Fact]
    public void RejectsEmptyAndInvalidSamples()
    {
        Assert.Throws<ArgumentException>(() => NetworkSampleSummary.From([]));
        Assert.Throws<ArgumentException>(() => NetworkSampleSummary.From([-1]));
    }
}
