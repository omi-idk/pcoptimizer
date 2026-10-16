namespace PCOptimizer.Benchmarking;

public enum CaptureCompletion { Finished, Stopped, TimedOut }

public static class CaptureLifetime
{
    // Keep process mechanics in the provider, but test cancellation/cleanup without launching a game or ETW.
    public static async Task<CaptureCompletion> WaitAsync(Func<CancellationToken, Task> waitForExit,
        Func<Task> stopOwnedCapture, TimeSpan maximumDuration, CancellationToken cancellationToken)
    {
        if (maximumDuration <= TimeSpan.Zero || maximumDuration > TimeSpan.FromMinutes(11))
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(maximumDuration);
        var completion = CaptureCompletion.TimedOut;
        try
        {
            try
            {
                await waitForExit(deadline.Token);
                completion = CaptureCompletion.Finished;
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                completion = cancellationToken.IsCancellationRequested ? CaptureCompletion.Stopped : CaptureCompletion.TimedOut;
            }
        }
        finally
        {
            if (completion != CaptureCompletion.Finished) await stopOwnedCapture();
        }
        return completion;
    }
}
