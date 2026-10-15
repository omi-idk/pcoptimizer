using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public class WingetResultTests
{
    [Theory]
    [InlineData(0u, WingetResultKind.Completed, false)]
    [InlineData(0x8A150061u, WingetResultKind.AlreadyInstalled, false)]
    [InlineData(0x8A15010Du, WingetResultKind.AlreadyInstalled, false)]
    [InlineData(0x8A15002Bu, WingetResultKind.NoApplicableUpdate, false)]
    [InlineData(3010u, WingetResultKind.RebootRequired, true)]
    [InlineData(0x8A150109u, WingetResultKind.RebootRequired, true)]
    [InlineData(1641u, WingetResultKind.RebootInitiated, true)]
    [InlineData(0x8A15010Bu, WingetResultKind.RebootInitiated, true)]
    [InlineData(0x8A15010Au, WingetResultKind.RebootRequiredBeforeInstall, true)]
    public void SeparatesSkipsAndRebootsFromErrors(uint code, WingetResultKind expected, bool stopBatch)
    {
        var result = WingetResult.FromExitCode(unchecked((int)code));
        Assert.Equal(expected, result.Kind);
        Assert.Equal(stopBatch, result.StopBatch);
        Assert.False(result.IsFailure);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(1603)]
    [InlineData(1602)]
    public void UnknownOrFailedResultsStopTheBatchAndRetainTheCode(int code)
    {
        var result = WingetResult.FromExitCode(code);
        Assert.True(result.IsFailure);
        Assert.True(result.StopBatch);
        Assert.Contains(unchecked((uint)code).ToString("X8"), result.Description);
    }
}
