using System.Text.Json;
using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class RecoveryStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-recovery-tests-" + Guid.NewGuid());
    private static RecoveryAction Action(string name = "Steam") => new(Guid.NewGuid(), RecoveryActionKind.InstallSoftware,
        name, "Valve.Steam", "No inventariado", "Solicitar instalación");

    [Fact]
    public void SavesPlannedActionsBeforeAnyWindowsPointExistsAndSurvivesReload()
    {
        var store = new RecoveryStore(directory);
        var session = store.Begin([Action()]);
        var reloaded = new RecoveryStore(directory).Read(session.Id);
        Assert.Null(reloaded.Point);
        Assert.Equal("Punto pendiente", reloaded.PointState);
        Assert.Equal("Planificada", Assert.Single(reloaded.Actions).State);
    }

    [Fact]
    public void LinksActualPointToItsSessionAndKeepsIndividualOutcomes()
    {
        var store = new RecoveryStore(directory);
        var actions = new[] { Action(), Action("Discord") };
        var session = store.Begin(actions);
        var point = new WindowsRestorePoint(42, "PCOptimizer-" + session.Id.ToString("N"), DateTimeOffset.UtcNow);
        store.SetPoint(session.Id, point, "Creación del punto confirmada");
        store.SetActionState(session.Id, actions[0].Id, "Instalación no verificada");
        store.SetPendingActionsState(session.Id, "No ejecutada");
        var reloaded = new RecoveryStore(directory).Read(session.Id);
        Assert.Equal(point, reloaded.Point);
        Assert.Equal("Instalación no verificada", reloaded.Actions[0].State);
        Assert.Equal("No ejecutada", reloaded.Actions[1].State);
    }

    [Fact]
    public void CannotAttachAPointCreatedForAnotherSession()
    {
        var store = new RecoveryStore(directory);
        var session = store.Begin([]);
        var point = new WindowsRestorePoint(42, "PCOptimizer-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        Assert.Throws<InvalidDataException>(() => store.SetPoint(session.Id, point, "Confirmado"));
        Assert.Null(store.Read(session.Id).Point);
    }

    [Fact]
    public void UnknownActionCannotOverwriteAnotherSessionsAction()
    {
        var store = new RecoveryStore(directory);
        var session = store.Begin([Action()]);
        Assert.Throws<InvalidOperationException>(() => store.SetActionState(session.Id, Guid.NewGuid(), "Aplicada"));
        Assert.Equal("Planificada", Assert.Single(store.Read(session.Id).Actions).State);
    }

    [Fact]
    public void KeepsPreviousPowerPlanSelectionForRecoveryWithoutClaimingAFullPlanExport()
    {
        var original = Guid.NewGuid();
        var requested = Guid.NewGuid();
        var action = new RecoveryAction(Guid.NewGuid(), RecoveryActionKind.PowerPlan, "Plan de energía",
            requested.ToString("D"), original.ToString("D"), requested.ToString("D"));
        var store = new RecoveryStore(directory);
        var session = store.Begin([action]);
        var saved = Assert.Single(store.Read(session.Id).Actions);
        Assert.Equal(original.ToString("D"), saved.PreviousValue);
        Assert.Equal(requested.ToString("D"), saved.TargetValue);
    }

    [Fact]
    public void CorruptRecordIsPreservedAndDoesNotBecomeAnEmptySuccessfulBackup()
    {
        var store = new RecoveryStore(directory);
        var session = store.Begin([]);
        var path = Path.Combine(directory, session.Id.ToString("N") + ".json");
        File.WriteAllText(path, "{broken");
        Assert.Throws<JsonException>(() => store.Read(session.Id));
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    [Fact]
    public void CannotRecordAChangeIfBackupDirectoryCannotBeCreated()
    {
        Directory.CreateDirectory(directory);
        var blocker = Path.Combine(directory, "blocked");
        File.WriteAllText(blocker, "keep");
        Assert.ThrowsAny<IOException>(() => new RecoveryStore(blocker).Begin([Action()]));
        Assert.Equal("keep", File.ReadAllText(blocker));
    }

    [Fact]
    public void RejectsDuplicateActionIdsBeforeSaving()
    {
        var action = Action();
        Assert.Throws<InvalidDataException>(() => new RecoveryStore(directory).Begin([action, action]));
        Assert.False(Directory.Exists(directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
