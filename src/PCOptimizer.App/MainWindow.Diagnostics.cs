using System.Diagnostics;
using System.Windows;
using PCOptimizer.Core;

namespace PCOptimizer.App;

public partial class MainWindow
{
    private readonly Dictionary<string, IReadOnlyList<InfoCard>> diagnosticCards = [];

    private void AddDiagnosticCards(string name)
    {
        if (diagnosticCards.TryGetValue(name, out var cards))
            foreach (var card in cards) model.Cards.Add(card);
    }

    private async Task RunDiagnosisAsync(string name, Func<Task<IReadOnlyList<InfoCard>>> query)
    {
        if (!model.IsIdle) return;
        model.IsIdle = false;
        model.Status = $"Consultando {name.ToLowerInvariant()}… No cierres la aplicación hasta que termine.";
        try
        {
            diagnosticCards[name] = await query();
            model.Status = $"Consulta de {name.ToLowerInvariant()} completada a las {DateTime.Now:t}. No se cambió la configuración.";
        }
        catch (Exception ex)
        {
            diagnosticCards[name] = [new("NO SE PUDO LEER", "Consulta sin resultado", ex.Message)];
            model.Status = "La consulta no pudo completarse: " + ex.Message;
        }
        finally { model.IsIdle = true; Render(); }
    }

    private async void MeasureNetwork(object sender, RoutedEventArgs e) => await RunDiagnosisAsync("Red", async () =>
    {
        var targets = WindowsDiagnostics.NetworkTargets();
        return await Task.WhenAll(targets.Select(async target =>
        {
            var stats = NetworkSampleSummary.From(await WindowsDiagnostics.PingAsync(target.Address));
            string Ms(double? value) => value is { } n ? $"{n:N1} ms" : "No disponible";
            return new InfoCard(target.Name.ToUpperInvariant(), Ms(stats.AverageMs),
                $"{target.Address} · ICMP\nRecibidos: {stats.Received}/{stats.Sent} · Sin respuesta: {stats.LossPercent:N0}%\nMín: {Ms(stats.MinimumMs)} · Máx: {Ms(stats.MaximumMs)}\nVariación entre respuestas consecutivas: {Ms(stats.JitterMs)}\nICMP puede estar limitado o bloqueado: no equivale a pérdida dentro de un juego ni mide la velocidad de DNS.");
        }));
    });

    private async void ScanDrivers(object sender, RoutedEventArgs e) => await RunDiagnosisAsync("Drivers", async () =>
    {
        var drivers = await WindowsDiagnostics.GetDriversAsync();
        model.Drivers.Clear();
        foreach (var driver in drivers) model.Drivers.Add(driver);
        return [new InfoCard("INVENTARIO REAL", $"{drivers.Length} controladores detectados",
            "Vídeo, red, audio, sistema y almacenamiento. La fecha no demuestra que un driver esté obsoleto. Los enlaces abren fuentes oficiales; no se descargan ni instalan drivers automáticamente.")];
    });

    private void OpenDriverSource(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || ((FrameworkElement)sender).DataContext is not InstalledDriver driver) return;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(driver.SourceUrl) { UseShellExecute = true });
            model.Status = "Se abrió la fuente oficial. Comprueba modelo exacto, versión de Windows y recomendaciones del fabricante del equipo.";
        }
        catch (Exception ex) { model.Status = "No se pudo abrir la fuente: " + ex.Message; }
    }

    private async void ScanStorage(object sender, RoutedEventArgs e)
    {
        var elevated = Equals(((FrameworkElement)sender).Tag, "elevated");
        await RunDiagnosisAsync("Almacenamiento", async () =>
        {
            var disks = elevated ? await WindowsDiagnostics.GetDiskHealthElevatedAsync() : await WindowsDiagnostics.GetDiskHealthAsync();
            if (disks.Length == 0) return [new InfoCard("SIN DATOS", "Windows no devolvió unidades", "El controlador puede no exponer la información con esta API.")];
            string Metric(long? value, string suffix = "") => value is null ? "No informado" : $"{value:N0}{suffix}";
            return disks.Select(d => new InfoCard("SALUD INFORMADA POR WINDOWS", d.Model,
                $"Estado: {d.Health} · {d.Operational}\nCapacidad: {d.Size / 1e9:N0} GB\nTemperatura: {Metric(d.Temperature, " °C")} · Desgaste: {Metric(d.Wear, "%")}\nHoras encendido: {Metric(d.PowerOnHours)}\nErrores de lectura/escritura reportados: {Metric(d.ReadErrors)} / {Metric(d.WriteErrors)}\n" +
                (d.ReliabilityError is not null ? "Contadores no disponibles: " + d.ReliabilityError : "Los contadores dependen del controlador. Cero puede ser un valor por defecto no soportado; contrasta con la herramienta del fabricante. No es una predicción de vida restante."))).ToArray();
        });
    }

    private async void ScanAudio(object sender, RoutedEventArgs e) => await RunDiagnosisAsync("Audio", async () =>
    {
        var devices = await WindowsDiagnostics.GetSoundDevicesAsync();
        return devices.Length == 0
            ? [new InfoCard("SIN DATOS", "No se detectaron dispositivos", "Comprueba Sonido y Administrador de dispositivos de Windows.")]
            : devices.Select(d => new InfoCard("DISPOSITIVO DE SONIDO", d.Name, $"{d.Manufacturer}\nEstado según Windows: {d.Status}\nLa detección no confirma soporte de Loudness Equalization.")).ToArray();
    });

    private void OpenSoundControl(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        try { WindowsDiagnostics.OpenSoundPanel(); model.Status = "En Reproducción, abre Propiedades del dispositivo y busca Mejoras. Solo se puede activar Loudness Equalization si el controlador lo ofrece."; }
        catch (Exception ex) { model.Status = "No se pudo abrir el panel de sonido: " + ex.Message; }
    }

    private async void MeasureLoad(object sender, RoutedEventArgs e) => await RunDiagnosisAsync("Gaming", async () =>
    {
        var load = await WindowsDiagnostics.GetLoadAsync();
        string Percent(double? n) => n is null ? "No disponible" : $"{n:N0}%";
        return [
            new InfoCard("CARGA ACTUAL · CPU", Percent(load.CpuPercent), "Carga total informada por el contador de Windows. Una lectura no detecta saturación por núcleo ni mide FPS."),
            new InfoCard("MEMORIA DISPONIBLE", load.AvailableMemoryMb is { } memory ? $"{memory / 1024:N1} GiB" : "No disponible", "Memoria física libre informada por Windows; no se vacía la RAM ni se modifica el archivo de paginación."),
            new InfoCard("MAYOR INSTANCIA DE MOTOR 3D", Percent(load.Largest3DEnginePercent), "Máximo del contador por instancia 3D; no es el uso total de la GPU. No se accede a la memoria de los juegos."),
            new InfoCard("ALCANCE", "No es un porcentaje de cuello de botella", "Para atribuir un límite a CPU/GPU se necesitan tiempos de cuadro y carga por núcleo durante el mismo escenario. Esta consulta muestra carga actual; no diagnostica por sí sola el rendimiento del juego.")
        ];
    });
}
