using System.Text.Json;

namespace PCOptimizer.Core;

public sealed record TweakDefinition(string Id, string Name, string Category, string Description,
    string Risks, string ReferenceTargets, string Source)
{
    public string Status => "Requiere evaluación · Sin ejecutor conectado";
    public string CurrentState => "Estado de tu equipo: no consultado";
}

public static class TweakCatalog
{
    public static IReadOnlyList<TweakDefinition> Items { get; } = Load();

    private static IReadOnlyList<TweakDefinition> Load()
    {
        using var stream = typeof(TweakCatalog).Assembly.GetManifestResourceStream("PCOptimizer.Core.Data.tweak-catalog.json")
            ?? throw new InvalidDataException("No se encontró el catálogo de ajustes.");
        var items = JsonSerializer.Deserialize<TweakDefinition[]>(stream)
            ?? throw new InvalidDataException("El catálogo de ajustes está vacío.");
        if (items.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length
            || items.Any(p => string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Risks)
                || !Uri.TryCreate(p.Source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidDataException("El catálogo de evaluación contiene entradas inválidas.");
        return Array.AsReadOnly(items);
    }
}
