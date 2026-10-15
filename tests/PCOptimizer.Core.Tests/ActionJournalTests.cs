using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class ActionJournalTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-tests-" + Guid.NewGuid());
    private string JournalPath => Path.Combine(directory, "software-history.json");

    [Fact]
    public void PendingOperationSurvivesReloadAndCompletionPreservesEarlierEntries()
    {
        var journal = new ActionJournal(JournalPath);
        var first = journal.Begin(SoftwareActionKind.InstallSoftware, "Valve.Steam");
        var second = journal.Begin(SoftwareActionKind.RemoveOptionalApp, "Clipchamp.Clipchamp");
        var reloaded = new ActionJournal(JournalPath);
        Assert.All(reloaded.Read(), entry => Assert.Equal("Pendiente de verificar", entry.State));
        reloaded.Complete(second, "Verificado");
        var entries = new ActionJournal(JournalPath).Read();
        Assert.Equal(first, entries[0].Id);
        Assert.Equal("Pendiente de verificar", entries[0].State);
        Assert.Equal("Verificado", entries[1].State);
        Assert.Equal(SoftwareActionKind.RemoveOptionalApp, entries[1].Kind);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[null]")]
    public void CorruptHistoryBlocksNewOperationsAndIsNotOverwritten(string contents)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(JournalPath, contents);
        Assert.ThrowsAny<Exception>(() => new ActionJournal(JournalPath).Begin(SoftwareActionKind.InstallSoftware, "Valve.Steam"));
        Assert.Equal(contents, File.ReadAllText(JournalPath));
    }

    [Fact]
    public void UnknownOperationCannotChangeAnExistingRecord()
    {
        var journal = new ActionJournal(JournalPath);
        journal.Begin(SoftwareActionKind.InstallSoftware, "Valve.Steam");
        var original = File.ReadAllText(JournalPath);
        Assert.Throws<InvalidOperationException>(() => journal.Complete(Guid.NewGuid(), "Verificado"));
        Assert.Equal(original, File.ReadAllText(JournalPath));
    }

    [Fact]
    public void UnwritableJournalPreventsRecordingAnOperation()
    {
        Directory.CreateDirectory(directory);
        // A file in the place of the parent directory fails deterministically on all platforms.
        var blocker = Path.Combine(directory, "blocked");
        File.WriteAllText(blocker, "keep");
        var journal = new ActionJournal(Path.Combine(blocker, "history.json"));
        Assert.ThrowsAny<IOException>(() => journal.Begin(SoftwareActionKind.InstallSoftware, "Valve.Steam"));
        Assert.Equal("keep", File.ReadAllText(blocker));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
