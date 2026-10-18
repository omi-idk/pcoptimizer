using System.Text.Json.Serialization;

namespace PCOptimizer.Core;

public enum GamingSessionState { Prepared, Applying, Active, Restoring, Restored, Canceled, ApplyFailed, RestoreFailed, Conflict }
public sealed record GameIdentity(int ProcessId, DateTimeOffset StartedUtc, string Name);
public sealed record GamingPowerSession(int SchemaVersion, Guid Id, DateTimeOffset CreatedUtc, GameIdentity Game,
    Guid PreviousPlan, Guid TemporaryPlan, GamingSessionState State, string? Error = null)
{
    public bool ApplyAttempted { get; init; }
    [JsonIgnore]
    public string StateLabel => State switch
    {
        GamingSessionState.Prepared => "Preparada", GamingSessionState.Applying => "Aplicando plan",
        GamingSessionState.Active => "En curso", GamingSessionState.Restoring => "Restaurando",
        GamingSessionState.Restored => "Restaurada", GamingSessionState.Canceled => "Cerrada sin restauración pendiente",
        GamingSessionState.ApplyFailed => "Aplicación incompleta", GamingSessionState.RestoreFailed => "Restauración incompleta",
        GamingSessionState.Conflict => "Conflicto externo", _ => "Estado no reconocido"
    };
    [JsonIgnore]
    public string DisplayName => $"{CreatedUtc.ToLocalTime():g} · {Game.Name} · {StateLabel}";
}
public interface IPowerSessionPlatform
{
    Task<IReadOnlyList<PowerPlan>> PlansAsync();
    Task<bool> IsGameRunningAsync(GameIdentity game);
    Task ActivateAsync(Guid plan);
}

// Caller serializes UI operations. Every attempted system write is preceded by a durable journal state.
public sealed class GamingPowerSessionEngine(IPowerSessionPlatform platform, string directory)
{
    private string FilePath(Guid id) => Path.Combine(directory, id.ToString("N") + ".json");
    public IReadOnlyList<GamingPowerSession> ListPending() => List().Where(s => s.State is not (GamingSessionState.Restored or GamingSessionState.Canceled)).ToArray();
    public IReadOnlyList<GamingPowerSession> List()
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.json").Select(path =>
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)) throw new InvalidDataException("Registro de sesión no reconocido; se conservó.");
            return Read(id);
        }).OrderByDescending(s => s.CreatedUtc).ToArray();
    }
    public GamingPowerSession Read(Guid id)
    {
        var session = LocalJsonFile.Read<GamingPowerSession>(FilePath(id)) ?? throw new InvalidDataException("Sesión vacía.");
        Validate(session);
        if (session.Id != id) throw new InvalidDataException("Identidad de sesión incorrecta.");
        return session;
    }
    private GamingPowerSession Save(GamingPowerSession session)
    {
        Validate(session); LocalJsonFile.Write(FilePath(session.Id), session); return session;
    }
    private static void Validate(GamingPowerSession s)
    {
        if (s.SchemaVersion != 1 || s.Id == Guid.Empty || s.Game is null || s.Game.ProcessId <= 0 || string.IsNullOrWhiteSpace(s.Game.Name)
            || s.Game.Name.Length > 260 || s.Game.StartedUtc == default || s.PreviousPlan == Guid.Empty || s.TemporaryPlan == Guid.Empty
            || s.PreviousPlan == s.TemporaryPlan || !Enum.IsDefined(s.State) || s.Error?.Length > 4096)
            throw new InvalidDataException("Esquema de sesión inválido.");
    }
    private static PowerPlan Active(IReadOnlyList<PowerPlan> plans) => plans.Count(p => p.IsActive) == 1
        ? plans.Single(p => p.IsActive) : throw new InvalidDataException("No se pudo identificar un único plan activo.");
    public async Task<GamingPowerSession> PrepareAsync(GameIdentity game, Guid temporaryPlan)
    {
        if (ListPending().Count != 0) throw new InvalidOperationException("Hay una sesión pendiente. Recupérala antes de iniciar otra.");
        if (!await platform.IsGameRunningAsync(game)) throw new InvalidOperationException("El juego terminó o se reutilizó su PID.");
        var plans = await platform.PlansAsync(); var previous = Active(plans).Id;
        if (!plans.Any(p => p.Id == temporaryPlan) || previous == temporaryPlan) throw new InvalidOperationException("Elige otro plan existente; no se crean planes ni se alteran sus opciones internas.");
        return Save(new(1, Guid.NewGuid(), DateTimeOffset.UtcNow, game, previous, temporaryPlan, GamingSessionState.Prepared));
    }
    public GamingPowerSession CancelPrepared(Guid id)
    {
        var session = Read(id);
        if (session.State != GamingSessionState.Prepared) throw new InvalidOperationException("La sesión ya inició cambios.");
        return Save(session with { State = GamingSessionState.Canceled });
    }
    public async Task<GamingPowerSession> ApplyAsync(Guid id)
    {
        var session = Read(id);
        if (session.State != GamingSessionState.Prepared) throw new InvalidOperationException("La sesión no está preparada.");
        if (!await platform.IsGameRunningAsync(session.Game)) throw new InvalidOperationException("El juego terminó antes de aplicar.");
        var plans = await platform.PlansAsync();
        if (Active(plans).Id != session.PreviousPlan || !plans.Any(p => p.Id == session.TemporaryPlan))
            throw new InvalidOperationException("Los planes cambiaron externamente. No se aplicó la sesión.");
        session = Save(session with { State = GamingSessionState.Applying, ApplyAttempted = true });
        try
        {
            await platform.ActivateAsync(session.TemporaryPlan);
            if (Active(await platform.PlansAsync()).Id != session.TemporaryPlan) throw new InvalidOperationException("No se verificó el plan temporal.");
            return Save(session with { State = GamingSessionState.Active });
        }
        catch (Exception ex)
        {
            Save(session with { State = GamingSessionState.ApplyFailed, Error = ex.Message[..Math.Min(ex.Message.Length, 4096)] }); throw;
        }
    }
    public async Task<bool> NeedsRestoreWriteAsync(Guid id)
    {
        var session = Read(id);
        if (session.State is GamingSessionState.Restored or GamingSessionState.Canceled) return false;
        var plans = await platform.PlansAsync(); var current = Active(plans).Id;
        if (current == session.PreviousPlan) return false;
        if (current != session.TemporaryPlan)
        {
            Save(session with { State = GamingSessionState.Conflict, Error = "Otro programa cambió el plan activo." });
            throw new InvalidOperationException("Conflicto: el plan ya no coincide con el temporal. No se sobrescribió el cambio externo.");
        }
        if (!session.ApplyAttempted) throw new InvalidOperationException("La sesión no inició escrituras; no se revertirá el cambio de otro programa.");
        if (!plans.Any(p => p.Id == session.PreviousPlan)) throw new InvalidOperationException("El plan original fue eliminado. No se recrean ajustes de energía.");
        return true;
    }
    public async Task<GamingPowerSession> AcceptExternalPlanAsync(Guid id)
    {
        var session = Read(id);
        if (session.State != GamingSessionState.Conflict) throw new InvalidOperationException("Solo se puede reconocer un conflicto externo ya detectado.");
        var current = Active(await platform.PlansAsync()).Id;
        if (current == session.TemporaryPlan) throw new InvalidOperationException("El plan temporal sigue activo. Restaura la sesión o déjala pendiente.");
        return Save(session with { State = GamingSessionState.Canceled, Error = "Cambio externo conservado por decisión explícita del usuario; no se escribió ningún plan." });
    }
    public async Task<GamingPowerSession> RestoreAsync(Guid id)
    {
        var session = Read(id);
        if (session.State is GamingSessionState.Restored or GamingSessionState.Canceled) return session;
        if (!await NeedsRestoreWriteAsync(id)) return Save(session with { State = GamingSessionState.Restored, Error = null });
        Save(session with { State = GamingSessionState.Restoring });
        try
        {
            await platform.ActivateAsync(session.PreviousPlan);
            if (Active(await platform.PlansAsync()).Id != session.PreviousPlan) throw new InvalidOperationException("No se verificó la restauración.");
            return Save(session with { State = GamingSessionState.Restored, Error = null });
        }
        catch (Exception ex)
        {
            Save(session with { State = GamingSessionState.RestoreFailed, Error = ex.Message[..Math.Min(ex.Message.Length, 4096)] }); throw;
        }
    }
}
