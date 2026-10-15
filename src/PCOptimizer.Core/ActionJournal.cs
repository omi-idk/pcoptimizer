namespace PCOptimizer.Core;

public enum SoftwareActionKind { InstallSoftware, RemoveOptionalApp, OpenDownloadPage }
public sealed record SoftwareAction(Guid Id, SoftwareActionKind Kind, string TargetId,
    string Name, DateTimeOffset Date, string State);

public sealed class ActionJournal(string path)
{
    public IReadOnlyList<SoftwareAction> Read()
    {
        if (!File.Exists(path)) return [];
        var entries = LocalJsonFile.Read<List<SoftwareAction>>(path)
            ?? throw new InvalidDataException("El historial no contiene un registro válido; se conservó el archivo.");
        if (entries.Any(e => e is null || e.Id == Guid.Empty) || entries.Select(e => e.Id).Distinct().Count() != entries.Count)
            throw new InvalidDataException("El historial contiene entradas inválidas; se conservó el archivo.");
        return entries.AsReadOnly();
    }

    public Guid Begin(SoftwareActionKind kind, string targetId)
    {
        var name = kind switch
        {
            SoftwareActionKind.InstallSoftware => SoftwareCatalog.GetPackage(targetId).Name,
            SoftwareActionKind.OpenDownloadPage => SoftwareCatalog.GetPackage(targetId).Name,
            SoftwareActionKind.RemoveOptionalApp => SoftwareCatalog.GetOptionalApp(targetId).Name,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var entries = Read().ToList();
        var entry = new SoftwareAction(Guid.NewGuid(), kind, targetId, name, DateTimeOffset.UtcNow, "Pendiente de verificar");
        entries.Add(entry);
        Save(entries);
        return entry.Id;
    }

    public void Complete(Guid id, string state)
    {
        var entries = Read().ToList();
        var index = entries.FindIndex(e => e.Id == id);
        if (index < 0) throw new InvalidOperationException("No se encontró la operación en el historial.");
        entries[index] = entries[index] with { State = state };
        Save(entries);
    }

    private void Save(List<SoftwareAction> entries)
    {
        LocalJsonFile.Write(path, entries);
    }
}
