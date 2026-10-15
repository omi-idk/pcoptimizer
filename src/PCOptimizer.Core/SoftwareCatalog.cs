using System.Text.Json;
using System.Text.RegularExpressions;

namespace PCOptimizer.Core;

public sealed record SoftwarePackage(string Id, string Name, string Description, string Website,
    string Category, string Source, string Notes = "")
{
    public string PackageId => Source == "msstore" ? Id["msstore:".Length..] : Id;
    public string SourceLabel => Source switch { "winget" => "WinGet", "msstore" => "Microsoft Store", _ => "Descarga oficial" };
    public bool OpensWebsite => Source == "website";
}
public sealed record OptionalWindowsApp(string Id, string Name, string Consequence);

public static class SoftwareCatalog
{
    public static IReadOnlyList<SoftwarePackage> Packages { get; } = Load();

    private static IReadOnlyList<SoftwarePackage> Load()
    {
        using var stream = typeof(SoftwareCatalog).Assembly.GetManifestResourceStream("PCOptimizer.Core.Data.software-catalog.json")
            ?? throw new InvalidDataException("No se encontró el catálogo integrado.");
        var packages = JsonSerializer.Deserialize<SoftwarePackage[]>(stream)
            ?? throw new InvalidDataException("El catálogo está vacío.");
        var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages)
        {
            if (!identifiers.Add(package.Id) || string.IsNullOrWhiteSpace(package.Name) || string.IsNullOrWhiteSpace(package.Category)
                || !Uri.TryCreate(package.Website, UriKind.Absolute, out var website) || website.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(website.UserInfo))
                throw new InvalidDataException("El catálogo contiene una entrada inválida.");
            var validSource = package.Source switch
            {
                "winget" => Regex.IsMatch(package.Id, @"^[A-Za-z0-9][A-Za-z0-9.+_-]*\.[A-Za-z0-9.+_-]+$"),
                "msstore" => Regex.IsMatch(package.Id, @"^msstore:[A-Z0-9]{12}$"),
                "website" => Regex.IsMatch(package.Id, @"^[A-Za-z0-9:.+_-]+$"),
                _ => false
            };
            if (!validSource) throw new InvalidDataException("El catálogo contiene una fuente o identificador inválido.");
        }
        return Array.AsReadOnly(packages);
    }

    public static IReadOnlyList<OptionalWindowsApp> OptionalApps { get; } = Array.AsReadOnly<OptionalWindowsApp>([
        new("Microsoft.BingNews", "Noticias de Microsoft", "Quita esta aplicación de noticias para tu usuario; no desactiva Widgets ni sus servicios."),
        new("Microsoft.BingWeather", "El Tiempo de Microsoft", "Quita esta aplicación meteorológica para tu usuario; no elimina el clima de todos los componentes de Windows."),
        new("Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire Collection", "Quita la colección de solitarios. Puede perder preferencias o datos locales no sincronizados."),
        new("Clipchamp.Clipchamp", "Microsoft Clipchamp", "Quita este editor de vídeo. Guarda y exporta tus proyectos antes; puede perder datos locales.")
    ]);

    public static SoftwarePackage GetPackage(string id) => Packages.SingleOrDefault(p => p.Id == id)
        ?? throw new ArgumentException("El paquete no pertenece al catálogo permitido.", nameof(id));

    public static OptionalWindowsApp GetOptionalApp(string id) => OptionalApps.SingleOrDefault(p => p.Id == id)
        ?? throw new ArgumentException("La aplicación no pertenece a la lista de eliminación permitida.", nameof(id));

    public static IReadOnlyList<string> InstallArguments(string id)
    {
        var package = GetPackage(id);
        if (package.OpensWebsite) throw new InvalidOperationException("Esta aplicación se descarga desde su sitio oficial; no tiene instalación automática aprobada.");
        // Keep agreement prompts and checksum checks; never silently upgrade an existing install.
        var args = new List<string> { "install", "--id", package.PackageId, "--exact", "--source", package.Source, "--no-upgrade" };
        if (package.Source == "winget") args.Add("--interactive");
        return args.AsReadOnly();
    }
}
