using System.Text.Json;
using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class TweakEngineTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-tweaks-" + Guid.NewGuid());
    private sealed class Values : ITweakValueStore
    {
        public Dictionary<string, RegistryValueSnapshot> Data { get; } = [];
        public int Writes { get; private set; }
        public int FailAt { get; set; } = int.MaxValue;
        public bool IgnoreWrites { get; set; }
        public Action? BeforeWrite { get; set; }
        public RegistryValueSnapshot Read(RegistryTweakTarget target) => Data.GetValueOrDefault(target.Id, RegistryValueSnapshot.Missing);
        public void Write(RegistryTweakTarget target, RegistryValueSnapshot expected, RegistryValueSnapshot value)
        {
            BeforeWrite?.Invoke();
            if (Read(target) != expected) throw new IOException("Concurrent mutation");
            if (++Writes == FailAt) throw new IOException("Simulated write failure");
            if (!IgnoreWrites) Data[target.Id] = value;
        }
    }

    [Fact]
    public void PreviewDoesNotWriteAndOriginalValuesSurviveNewEngineInstance()
    {
        var values = new Values();
        values.Data["game-mode-auto"] = new(true, "DWord", "0");
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["game-mode"]);
        Assert.False(Directory.Exists(directory));
        Assert.Equal(0, values.Writes);
        engine.SavePrepared(operation);
        var reloaded = new TweakEngine(values, directory).Read(operation.Id);
        Assert.Equal(new(true, "DWord", "0"), reloaded.Changes[0].Before);
        Assert.Equal(RegistryValueSnapshot.Missing, reloaded.Changes[1].Before);
    }

    [Fact]
    public void ApplyAndUndoRestoreExactValuesIncludingPreviouslyMissingValues()
    {
        var values = new Values();
        values.Data["game-mode-auto"] = new(true, "DWord", "0");
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["game-mode"]);
        engine.SavePrepared(operation);
        var applied = engine.Apply(operation.Id);
        Assert.Equal("Valores de registro verificados", applied.State);
        var undo = engine.PreviewUndo(operation.Id);
        engine.SavePrepared(undo);
        engine.Apply(undo.Id);
        Assert.Equal(new(true, "DWord", "0"), values.Data["game-mode-auto"]);
        Assert.Equal(RegistryValueSnapshot.Missing, values.Data["game-mode-allow"]);
    }

    [Fact]
    public void ConflictInLastValuePreventsAllWrites()
    {
        var values = new Values();
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["game-mode"]);
        engine.SavePrepared(operation);
        values.Data["game-mode-allow"] = new(true, "DWord", "0");
        Assert.Throws<InvalidOperationException>(() => engine.Apply(operation.Id));
        Assert.Equal(0, values.Writes);
    }

    [Fact]
    public void ConflictDuringAuthorizationIsCheckedByValueStore()
    {
        var values = new Values();
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["game-mode"]);
        engine.SavePrepared(operation);
        values.BeforeWrite = () => values.Data["game-mode-auto"] = new(true, "DWord", "2");
        Assert.Throws<IOException>(() => engine.Apply(operation.Id));
        Assert.Equal(0, values.Writes);
        Assert.Equal("Lote incompleto; revisar o revertir", engine.Read(operation.Id).State);
    }

    [Fact]
    public void PartialFailurePreservesWriteAheadBackupAndCanUndoOnlyTouchedValues()
    {
        var values = new Values { FailAt = 2 };
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["game-mode"]);
        engine.SavePrepared(operation);
        Assert.Throws<IOException>(() => engine.Apply(operation.Id));
        Assert.Equal("Lote incompleto; revisar o revertir", engine.Read(operation.Id).State);
        var undo = engine.PreviewUndo(operation.Id);
        Assert.Single(undo.Changes);
        engine.SavePrepared(undo);
        values.FailAt = int.MaxValue;
        engine.Apply(undo.Id);
        Assert.Equal(RegistryValueSnapshot.Missing, values.Read(ExecutableTweaks.Get("game-mode").Targets[0]));
    }

    [Fact]
    public void ReadBackMismatchCannotBecomeSuccess()
    {
        var values = new Values { IgnoreWrites = true };
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["file-extensions"]);
        engine.SavePrepared(operation);
        Assert.Throws<IOException>(() => engine.Apply(operation.Id));
        Assert.Equal("Lote incompleto; revisar o revertir", engine.Read(operation.Id).State);
    }

    [Fact]
    public void MatchingValuesAreNotWrittenOrLaterUndoneAsIfWeChangedThem()
    {
        var values = new Values();
        values.Data["game-mode-auto"] = new(true, "DWord", "1");
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["game-mode"]);
        engine.SavePrepared(operation);
        engine.Apply(operation.Id);
        Assert.Equal(1, values.Writes);
        values.Data["game-mode-auto"] = new(true, "DWord", "0");
        var undo = engine.PreviewUndo(operation.Id);
        Assert.Equal("game-mode-allow", Assert.Single(undo.Changes).TargetId);
        engine.SavePrepared(undo);
        engine.Apply(undo.Id);
        Assert.Equal("0", values.Data["game-mode-auto"].Data);
    }

    [Fact]
    public void UndoRefusesExternalChangesWithoutOverwritingThem()
    {
        var values = new Values();
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["file-extensions"]);
        engine.SavePrepared(operation);
        engine.Apply(operation.Id);
        values.Data["file-extensions"] = new(true, "DWord", "2");
        var writes = values.Writes;
        Assert.Throws<InvalidOperationException>(() => engine.PreviewUndo(operation.Id));
        Assert.Equal(writes, values.Writes);
    }

    [Fact]
    public void TamperedDesiredValueCannotBeApplied()
    {
        var values = new Values();
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["game-mode"]);
        engine.SavePrepared(operation);
        var hostile = operation with { Changes = new[] { operation.Changes[0] with { After = new(true, "DWord", "99") } } };
        LocalJsonFile.Write(Path.Combine(directory, operation.Id.ToString("N") + ".json"), hostile);
        Assert.Throws<InvalidDataException>(() => engine.Apply(operation.Id));
        Assert.Equal(0, values.Writes);
    }

    [Fact]
    public void CorruptBackupIsPreservedAndBlocksHistoryRead()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{broken");
        Assert.Throws<JsonException>(() => new TweakEngine(new Values(), directory).List());
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    [Fact]
    public void UnsupportedRegistryTypeIsNotSilentlyReplaced()
    {
        var values = new Values();
        values.Data["file-extensions"] = new(true, "Binary", "anything");
        Assert.Throws<InvalidDataException>(() => new TweakEngine(values, directory).Preview(["file-extensions"]));
        Assert.Equal(0, values.Writes);
    }

    [Fact]
    public void CannotWriteIfOriginalBackupCannotBeSaved()
    {
        Directory.CreateDirectory(directory);
        var blocked = Path.Combine(directory, "file");
        File.WriteAllText(blocked, "original");
        var values = new Values();
        var engine = new TweakEngine(values, blocked);
        var operation = engine.Preview(["game-mode"]);
        Assert.ThrowsAny<IOException>(() => engine.SavePrepared(operation));
        Assert.Equal(0, values.Writes);
    }

    [Fact]
    public void AutomaticProfilesExcludeMachineWideExperimentsAndSecurityChanges()
    {
        foreach (var id in new[] { "minimal", "recommended", "gaming", "intensive" })
        {
            var profile = ExecutableTweaks.Profile(id);
            Assert.NotEmpty(profile);
            Assert.All(profile.SelectMany(t => ExecutableTweaks.Get(t).Targets), t => Assert.False(t.Machine));
        }
        Assert.Throws<InvalidDataException>(() => ExecutableTweaks.Get("disable-defender"));
    }

    [Fact]
    public void CanceledOperationCannotUndoValuesWrittenLaterByAnotherApp()
    {
        var values = new Values();
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["file-extensions"]);
        engine.SavePrepared(operation);
        engine.CancelPrepared(operation.Id);
        values.Data["file-extensions"] = new(true, "DWord", "0");
        Assert.Throws<InvalidOperationException>(() => engine.PreviewUndo(operation.Id));
        Assert.Equal(0, values.Writes);
    }

    [Fact]
    public void MouseProfileRestoresOriginalThresholdsAndRejectsMalformedStrings()
    {
        var values = new Values();
        values.Data["mouse-speed"] = new(true, "String", "1");
        values.Data["mouse-threshold1"] = new(true, "String", "6");
        values.Data["mouse-threshold2"] = new(true, "String", "10");
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["mouse-acceleration"]);
        engine.SavePrepared(operation);
        engine.Apply(operation.Id);
        var undo = engine.PreviewUndo(operation.Id);
        engine.SavePrepared(undo);
        engine.Apply(undo.Id);
        Assert.Equal("6", values.Data["mouse-threshold1"].Data);
        Assert.Equal("10", values.Data["mouse-threshold2"].Data);
        values.Data["mouse-speed"] = new(true, "String", "expression; malicious");
        Assert.Throws<InvalidDataException>(() => engine.Preview(["mouse-acceleration"]));
    }

    [Fact]
    public void CatalogKeepsExecutableTargetsClosedAndOriginalSnapshotsTyped()
    {
        Assert.Equal(14, ExecutableTweaks.Items.Count);
        Assert.All(ExecutableTweaks.Items, tweak =>
        {
            Assert.StartsWith("https://", tweak.Source);
            Assert.All(tweak.Targets, target => Assert.Equal(target, ExecutableTweaks.Target(tweak.Id, target.Id)));
        });
        var values = new Values();
        values.Data["window-animation"] = new(true, "String", "1");
        var engine = new TweakEngine(values, directory);
        var operation = engine.Preview(["window-animation"]);
        engine.SavePrepared(operation);
        engine.Apply(operation.Id);
        var undo = engine.PreviewUndo(operation.Id);
        engine.SavePrepared(undo);
        engine.Apply(undo.Id);
        Assert.Equal(new(true, "String", "1"), values.Data["window-animation"]);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
