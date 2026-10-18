using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class GamingPowerSessionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-session-" + Guid.NewGuid().ToString("N"));
    private readonly FakePlatform platform = new();
    private static GameIdentity Game => new(123, DateTimeOffset.UtcNow, "game.exe");
    private GamingPowerSessionEngine Engine => new(platform, directory);
    private sealed class FakePlatform : IPowerSessionPlatform
    {
        public Guid Original { get; } = Guid.NewGuid(); public Guid Temporary { get; } = Guid.NewGuid(); public Guid Other { get; } = Guid.NewGuid();
        public Guid Active; public bool Running = true; public bool FailAfterWrite; public bool OriginalExists = true; public int Writes;
        public FakePlatform() { Active = Original; }
        public Task<IReadOnlyList<PowerPlan>> PlansAsync() => Task.FromResult<IReadOnlyList<PowerPlan>>(new[] { new PowerPlan(Original, "Original", Active == Original), new PowerPlan(Temporary, "Temporal", Active == Temporary), new PowerPlan(Other, "Otro", Active == Other) }.Where(p => p.Id != Original || OriginalExists).ToArray());
        public Task<bool> IsGameRunningAsync(GameIdentity game) => Task.FromResult(Running);
        public Task ActivateAsync(Guid plan) { Writes++; Active = plan; if (FailAfterWrite) throw new IOException("Interrupted"); return Task.CompletedTask; }
    }
    [Fact]
    public async Task PrepareDoesNotWriteAndRestoreReturnsExactOriginal()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); Assert.Equal(0, platform.Writes);
        await e.ApplyAsync(s.Id); Assert.Equal(GamingSessionState.Active, e.Read(s.Id).State);
        await e.RestoreAsync(s.Id); Assert.Equal(platform.Original, platform.Active); Assert.Empty(e.ListPending());
    }
    [Fact]
    public async Task FailedApplyAfterNativeWriteIsRecoverableByNewEngine()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); platform.FailAfterWrite = true;
        await Assert.ThrowsAsync<IOException>(() => e.ApplyAsync(s.Id));
        Assert.Equal(GamingSessionState.ApplyFailed, e.Read(s.Id).State);
        platform.FailAfterWrite = false; await Engine.RestoreAsync(s.Id); Assert.Equal(platform.Original, platform.Active);
    }
    [Fact]
    public async Task ExternalPlanConflictIsPreserved()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); await e.ApplyAsync(s.Id); platform.Active = platform.Other;
        await Assert.ThrowsAsync<InvalidOperationException>(() => e.RestoreAsync(s.Id));
        Assert.Equal(platform.Other, platform.Active); Assert.Equal(1, platform.Writes); Assert.Equal(GamingSessionState.Conflict, e.Read(s.Id).State);
    }
    [Fact]
    public async Task PendingSessionBlocksAnotherAndReusedPidBlocksApply()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary);
        await Assert.ThrowsAsync<InvalidOperationException>(() => e.PrepareAsync(Game, platform.Temporary));
        platform.Running = false; await Assert.ThrowsAsync<InvalidOperationException>(() => e.ApplyAsync(s.Id)); Assert.Equal(0, platform.Writes);
        e.CancelPrepared(s.Id); Assert.Empty(e.ListPending());
    }
    [Fact]
    public async Task PreparedJournalNeverUndoesAnExternalMatchingPlan()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); platform.Active = platform.Temporary;
        await Assert.ThrowsAsync<InvalidOperationException>(() => e.RestoreAsync(s.Id)); Assert.Equal(0, platform.Writes);
    }
    [Fact]
    public async Task DeletedOriginalAndCorruptJournalDoNotTriggerWrites()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); await e.ApplyAsync(s.Id); platform.OriginalExists = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => e.RestoreAsync(s.Id)); Assert.Equal(1, platform.Writes);
        var path = Path.Combine(directory, s.Id.ToString("N") + ".json"); await File.WriteAllTextAsync(path, "{corrupt");
        Assert.ThrowsAny<Exception>(() => e.ListPending()); Assert.Equal("{corrupt", await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task ExternalConflictCanBeAcknowledgedWithoutWriting()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); await e.ApplyAsync(s.Id); platform.Active = platform.Other;
        await Assert.ThrowsAsync<InvalidOperationException>(() => e.NeedsRestoreWriteAsync(s.Id));
        await e.AcceptExternalPlanAsync(s.Id); Assert.Empty(e.ListPending()); Assert.Equal(1, platform.Writes); Assert.Equal(platform.Other, platform.Active);
    }
    [Fact]
    public async Task AlreadyRestoredPlanNeedsNoSystemWrite()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); await e.ApplyAsync(s.Id); platform.Active = platform.Original;
        Assert.False(await e.NeedsRestoreWriteAsync(s.Id)); await e.RestoreAsync(s.Id); Assert.Equal(1, platform.Writes);
    }
    [Fact]
    public async Task PreparedConflictDoesNotLoseTheNoWritesInvariant()
    {
        var e = Engine; var s = await e.PrepareAsync(Game, platform.Temporary); platform.Active = platform.Other;
        await Assert.ThrowsAsync<InvalidOperationException>(() => e.NeedsRestoreWriteAsync(s.Id));
        platform.Active = platform.Temporary;
        await Assert.ThrowsAsync<InvalidOperationException>(() => e.RestoreAsync(s.Id)); Assert.Equal(0, platform.Writes);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
