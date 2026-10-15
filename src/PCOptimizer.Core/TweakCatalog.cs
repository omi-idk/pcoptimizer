using System.Text.Json;

namespace PCOptimizer.Core;

public sealed record TweakDefinition(string Id, string Name, string Category, string Description,
    string Risks, string ReferenceTargets, string Source)
{
    public string? ExecutableId => Id switch
    {
        "WPFToggleGameMode" => "game-mode", "GameCapture" => "game-capture",
        "WPFToggleMouseAcceleration" => "mouse-acceleration", "HAGS" => "hags",
        "WPFMultiplaneOverlay" => "mpo", "WPFToggleTaskbarAlignment" => "taskbar-left",
        "WPFToggleTaskView" => "task-view", "WPFToggleHiddenFiles" => "hidden-files",
        "WPFToggleShowExt" => "file-extensions", "WPFToggleDarkMode" => "dark-theme",
        _ => null
    };
    public string Status => ExecutableId is null ? "Requiere evaluación · Sin ejecutor conectado" : "Disponible en los ajustes aplicables";
    public string CurrentState => ExecutableId is null ? "Estado de tu equipo: no consultado" : "El estado se consulta en la ficha aplicable, fuera del catálogo de referencia.";
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
