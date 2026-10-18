using System.Text.RegularExpressions;

namespace PCOptimizer.Core;

public enum ProfileCheckState { Match, Mismatch, Unknown }
public sealed record ProfileHardware(string Cpu, string Motherboard, string Gpu, long? DedicatedVramBytes,
    MemoryModuleDetails[] MemoryModules, bool Windows11X64);
public sealed record ProfileCheck(string Name, ProfileCheckState State, string Expected, string Observed);

public static partial class KnownGamingProfile
{
    public const string Name = "Warzone · 5800X3D / RTX 5060 Ti 16 GB · experimental";
    public static SavedTweakProfile BaseTweaks => new(1, Name, ["game-mode"]);
    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex Separators();
    private static string Normalize(string value) => Separators().Replace(value.ToUpperInvariant(), "");
    private static ProfileCheck Text(string name, string expected, string actual, string token) => new(name,
        string.IsNullOrWhiteSpace(actual) ? ProfileCheckState.Unknown : Normalize(actual).Contains(token, StringComparison.Ordinal)
            ? ProfileCheckState.Match : ProfileCheckState.Mismatch, expected, string.IsNullOrWhiteSpace(actual) ? "No informado" : actual);
    public static IReadOnlyList<ProfileCheck> Evaluate(ProfileHardware hardware)
    {
        if (hardware.MemoryModules is null) throw new InvalidDataException("Falta el inventario de memoria.");
        var modules = hardware.MemoryModules;
        var result = new List<ProfileCheck>
        {
            Text("CPU", "AMD Ryzen 7 5800X3D", hardware.Cpu, "RYZEN75800X3D"),
            Text("Placa", "ASUS ROG Strix B550-F Gaming WiFi II", hardware.Motherboard, "ROGSTRIXB550FGAMINGWIFIII"),
            Text("GPU", "NVIDIA GeForce RTX 5060 Ti", hardware.Gpu, "NVIDIAGEFORCERTX5060TI"),
            new("VRAM dedicada", hardware.DedicatedVramBytes is null ? ProfileCheckState.Unknown :
                hardware.DedicatedVramBytes >= 15.5 * 1024 * 1024 * 1024 && hardware.DedicatedVramBytes <= 16.5 * 1024 * 1024 * 1024 ? ProfileCheckState.Match : ProfileCheckState.Mismatch,
                "Clase 16 GiB dedicados (se admite reserva de memoria informada por el driver)", hardware.DedicatedVramBytes is { } n ? $"{n / Math.Pow(1024,3):N1} GiB" : "No verificado; no se usa AdapterRAM de 32 bits ni se deduce del nombre."),
            new("RAM instalada", modules.Length == 0 ? ProfileCheckState.Unknown : modules.Length == 4 && modules.All(m => m.CapacityBytes == 8L * 1024 * 1024 * 1024)
                ? ProfileCheckState.Match : ProfileCheckState.Mismatch, "4 módulos de 8 GiB", $"{modules.Length} módulos; {modules.Sum(m => m.CapacityBytes) / Math.Pow(1024,3):N1} GiB"),
            new("Velocidad RAM declarada", modules.Length == 0 || modules.Any(m => m.ConfiguredClock is null) ? ProfileCheckState.Unknown :
                modules.All(m => m.ConfiguredClock == 3200) ? ProfileCheckState.Match : ProfileCheckState.Mismatch,
                "ConfiguredClockSpeed 3200 en los cuatro módulos; comprobar DDR4/DOCP manualmente", string.Join(", ", modules.Select(m => m.ConfiguredClock?.ToString() ?? "No informado"))),
            new("Windows", hardware.Windows11X64 ? ProfileCheckState.Match : ProfileCheckState.Mismatch, "Windows 11 x64", hardware.Windows11X64 ? "Windows 11 x64 detectado" : "No coincide"),
            new("Refrigeración y mando", ProfileCheckState.Unknown, "DeepCool AK500 Digital / GameSir G7 Pro", "Requiere confirmación del usuario; no se presume a partir de CPU o USB genérico.")
        };
        return result;
    }
    public static IReadOnlyList<string> CalibrationChecklist { get; } =
    [
        "Registrar versión actual de Warzone, Windows y drivers NVIDIA/chipset AMD. Último no equivale automáticamente a recomendado.",
        "Confirmar resolución, Hz, límite FPS, VSync, DLSS/Frame Generation y Reflex disponibles en esa versión. Hz no son FPS.",
        "Mantener la misma escena/ruta y calentamiento; capturar 3–5 repeticiones A/B alternadas con una sola variable.",
        "Comparar FPS presentados, lows y p99; observar CPU lógica, temperaturas, GPU Busy y VRAM cuando estén disponibles.",
        "HAGS se prueba aparte, con compatibilidad y reinicio confirmados; no se incluye en la base del perfil.",
        "Compatibilidad RICOCHET pendiente de validación real. No modificar memoria, archivos protegidos, tráfico ni componentes anticheat.",
        "La base solo selecciona Modo Juego. No altera ratón, mando, archivos de Warzone, BIOS, voltajes ni captura de OBS. Se revisa y respalda antes de aplicar."
    ];
}
