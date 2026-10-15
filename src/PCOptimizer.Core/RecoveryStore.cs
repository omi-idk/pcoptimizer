using System.Text.Json.Serialization;

namespace PCOptimizer.Core;

public enum RecoveryActionKind { PowerPlan, InstallSoftware, RemoveOptionalApp }
public sealed record RecoveryAction(Guid Id, RecoveryActionKind Kind, string Name, string TargetId,
    string PreviousValue, string TargetValue, string State = "Planificada");
public sealed record WindowsRestorePoint(int SequenceNumber, string Description, DateTimeOffset VerifiedAtUtc);
public sealed record RecoverySession(int SchemaVersion, Guid Id, DateTimeOffset CreatedAtUtc,
    string PointState, WindowsRestorePoint? Point, IReadOnlyList<RecoveryAction> Actions)
{
    [JsonIgnore] public string DisplayName => $"{CreatedAtUtc.ToLocalTime():g} · {PointState} · {Actions.Count} acciones";
    [JsonIgnore] public string PointDescription => Point is null ? "Sin punto de Windows confirmado" : $"{Point.Description} · Secuencia {Point.SequenceNumber}";
}

public sealed class RecoveryStore(string directory)
{
    private string FilePath(Guid id) => Path.Combine(directory, id.ToString("N") + ".json");

    public RecoverySession Begin(IReadOnlyList<RecoveryAction> actions)
    {
        var session = new RecoverySession(1, Guid.NewGuid(), DateTimeOffset.UtcNow,
            "Punto pendiente", null, actions.ToArray());
        Validate(session);
        Save(session);
        return session;
    }

    public RecoverySession Read(Guid id)
    {
        var session = LocalJsonFile.Read<RecoverySession>(FilePath(id))
            ?? throw new InvalidDataException("El respaldo no contiene un registro válido.");
        Validate(session);
        if (session.Id != id) throw new InvalidDataException("El identificador del respaldo no coincide.");
        return session;
    }

    public IReadOnlyList<RecoverySession> List()
    {
        if (!Directory.Exists(directory)) return [];
        var sessions = new List<RecoverySession>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id))
                throw new InvalidDataException("Hay un archivo de respaldo no reconocido; se conservó sin cambios.");
            sessions.Add(Read(id));
        }
        return sessions.OrderByDescending(p => p.CreatedAtUtc).ToArray();
    }

    public RecoverySession SetPoint(Guid id, WindowsRestorePoint? point, string state)
    {
        var session = Read(id) with { Point = point, PointState = state };
        Validate(session);
        Save(session);
        return session;
    }

    public RecoverySession SetActionState(Guid id, Guid actionId, string state)
    {
        var session = Read(id);
        if (!session.Actions.Any(a => a.Id == actionId)) throw new InvalidOperationException("La acción no pertenece a este respaldo.");
        session = session with { Actions = session.Actions.Select(a => a.Id == actionId ? a with { State = state } : a).ToArray() };
        Save(session);
        return session;
    }

    public void SetPendingActionsState(Guid id, string state)
    {
        var session = Read(id);
        Save(session with { Actions = session.Actions.Select(a => a.State == "Planificada" ? a with { State = state } : a).ToArray() });
    }

    private static void Validate(RecoverySession session)
    {
        if (session.SchemaVersion != 1 || session.Id == Guid.Empty || session.CreatedAtUtc == default
            || session.Actions is null || session.Actions.Any(a => a is null || a.Id == Guid.Empty)
            || session.Actions.Select(a => a.Id).Distinct().Count() != session.Actions.Count)
            throw new InvalidDataException("El formato del respaldo no es válido o no está soportado.");
        if (session.Point is { } point && (point.SequenceNumber <= 0
            || point.Description != "PCOptimizer-" + session.Id.ToString("N") || point.VerifiedAtUtc == default))
            throw new InvalidDataException("El punto de restauración no corresponde a este respaldo.");
    }

    private void Save(RecoverySession session)
    {
        LocalJsonFile.Write(FilePath(session.Id), session);
    }
}
