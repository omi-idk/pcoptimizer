using System.Diagnostics;
using System.Windows;
using PCOptimizer.Core;

namespace PCOptimizer.App;

public partial class MainWindow
{
    private readonly IGpuTelemetryProvider gpuProbe = new NvidiaTelemetry();
    private readonly IResourceProbe resourceProbe = new WindowsResourceProbe();
    private CancellationTokenSource? resourceCancellation;
    private ResourceCapture? latestResourceCapture;

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
        catch (OperationCanceledException)
        {
            model.Status = "Consulta cancelada. No se cambió la configuración y se conserva el resultado anterior.";
        }
        catch (Exception ex)
        {
            diagnosticCards[name] = [new("NO SE PUDO LEER", "Consulta sin resultado", ex.Message)];
            model.Status = "La consulta no pudo completarse: " + ex.Message;
        }
        finally { model.IsIdle = true; Render(); }
    }

    private async void InspectNetwork(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        using var cancellation = new CancellationTokenSource(); resourceCancellation = cancellation;
        try
        {
            await RunDiagnosisAsync("Red", async () =>
            {
                var adapters = await WindowsNetworkProbe.AdaptersAsync(cancellation.Token);
                var cards = adapters.Select(a => new InfoCard("ADAPTADOR · OBSERVACIÓN", a.Name,
                    $"Tipo: {a.Type}\nVelocidad de enlace informada: {(a.LinkBitsPerSecond is { } link ? $"{link / 1e6:N0} Mbit/s" : "N/D")} (no velocidad de Internet)\n" +
                    $"Tráfico recibido: {a.Rate.ReceiveBytesPerSecond?.ToString("N0") ?? "N/D"} B/s · enviado: {a.Rate.SendBytesPerSecond?.ToString("N0") ?? "N/D"} B/s\n" +
                    $"Errores acumulados entrada/salida: {a.InputErrors?.ToString() ?? "N/D"} / {a.OutputErrors?.ToString() ?? "N/D"}\nDNS configurados: {a.DnsServers}\nPuertas: {a.Gateways}\n{a.Rate.Detail}\n{a.Error}")).ToList();
                cards.Add(new("ALCANCE", "No se cambió la configuración", "No se modifican DNS, TCP, IPv6, offloads ni controlador. El tráfico se observa durante dos segundos. Bufferbloat requiere comparar latencia bajo carga controlada; esta consulta no lo diagnostica."));
                return cards;
            });
        }
        finally { resourceCancellation = null; }
    }
    private async void ResolveDns(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        using var cancellation = new CancellationTokenSource(); resourceCancellation = cancellation;
        try
        {
            await RunDiagnosisAsync("Red", async () => (await WindowsNetworkProbe.DnsAsync(cancellation.Token))
                .Select(d => new InfoCard("RESOLUCIÓN DNS · SISTEMA", d.Host,
                    $"Tiempo de resolución observado: {d.ElapsedMs?.ToString("N1") ?? "N/D"} ms · {d.AddressCount} direcciones\n{d.Error}\nPuede incluir caché local. No compara servidores DNS, no mide ping del juego ni velocidad de descarga. Se consultaron estos dominios al pulsar el botón.")).ToArray());
        }
        finally { resourceCancellation = null; }
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

    private async void ScanWindowsDriverOffers(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        using var cancellation = new CancellationTokenSource(); resourceCancellation = cancellation;
        try
        {
            await RunDiagnosisAsync("Drivers", async () =>
            {
        var offers = await WindowsDiagnostics.GetWindowsDriverOffersAsync(cancellation.Token);
        var cards = offers.Select(d => new InfoCard("OFRECIDO POR WINDOWS UPDATE · NO INSTALADO", d.Title,
            $"Fabricante: {d.Manufacturer} · proveedor: {d.Provider}\nClase: {d.Class} · fecha declarada: {d.DriverDate}\nHardware ID: {d.HardwareId}\nWindows Update lo considera aplicable con su configuración actual. No demuestra que sea el último del fabricante ni el recomendado para tu juego; no se descarga ni instala el controlador.")).ToList();
        if (offers.Length == 0) cards.Add(new("SIN OFERTAS", "Windows Update no devolvió drivers pendientes", "No demuestra que todos sean los últimos. Depende del catálogo, políticas, conexión y compatibilidad del equipo."));
        cards.Add(new("ALCANCE DE LA CONSULTA", "Último y recomendado siguen separados", "La búsqueda usa el agente oficial y puede contactar el servicio de actualización configurado. No cambia políticas, no aprueba instalaciones ni se compara una fecha con una versión. El inventario instalado y las fuentes oficiales siguen disponibles. Timeout de consulta: 45 segundos."));
        return cards;
            });
        }
        finally { resourceCancellation = null; }
    }

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

    private async void EvaluateWarzone(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        using var cancellation = new CancellationTokenSource(); resourceCancellation = cancellation;
        try
        {
            await RunDiagnosisAsync("Gaming", async () =>
            {
                var data = await resourceProbe.HardwareAsync(cancellation.Token);
                var machine = await WindowsCommands.GetInventoryAsync(cancellation.Token);
                var gpuData = await gpuProbe.ReadAsync(cancellation.Token);
                var matching = gpuData.Devices.Where(g => g.Name.Contains("RTX 5060 Ti", StringComparison.OrdinalIgnoreCase)).ToArray();
                long? vram = matching.Length == 1 && matching[0].VramTotal is { } total && total <= long.MaxValue ? (long)total : null;
                var checks = KnownGamingProfile.Evaluate(new(machine.Cpu, machine.Motherboard, machine.Gpu, vram,
                    data.MemoryModules, OperatingSystem.IsWindowsVersionAtLeast(10,0,22000) && System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X64, data.Security));
                var cards = checks.Select(c => new InfoCard(c.State switch { ProfileCheckState.Match => "COINCIDE", ProfileCheckState.Mismatch => "DIFIERE", _ => "POR COMPROBAR" },
                    c.Name, $"Esperado: {c.Expected}\nDetectado: {c.Observed}")).ToList();
                cards.Add(new("PERFIL EXPERIMENTAL", KnownGamingProfile.Name, string.Join("\n\n", KnownGamingProfile.CalibrationChecklist)));
                return cards;
            });
        }
        finally { resourceCancellation = null; }
    }
    private void OpenProcessWorkloads(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        var window = new Workloads.ProcessWorkloadWindow(new WindowsProcessWorkloadProbe(),
            () => { if (!model.IsIdle) return false; model.IsIdle = false; return true; },
            () => model.IsIdle = true) { Owner = this };
        if (window.ShowDialog() == true && window.SelectedPlan is { } profile)
        {
            if (!model.IsIdle)
            {
                model.Status = "Otra operación está en curso; espera y vuelve a seleccionar el objetivo.";
                return;
            }
            model.LoadProfile(profile);
            SelectNavigation(this, "Optimización");
            model.Status = "Objetivo seleccionado para revisión: no se aplicó ningún cambio. Comprueba los valores, riesgos, respaldo y punto de restauración antes de aplicar.";
        }
    }
    private void SelectWarzoneBase(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        model.LoadProfile(KnownGamingProfile.BaseTweaks);
        SelectNavigation(this, "Optimización");
        model.Status = "Base Warzone seleccionada: solo Modo Juego. No se aplicó nada. Evalúa primero tu hardware y revisa el ajuste; la calibración requiere mediciones.";
    }

    private void CancelResources(object sender, RoutedEventArgs e) => resourceCancellation?.Cancel();

    private async void InspectHardware(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        using var cancellation = new CancellationTokenSource(); resourceCancellation = cancellation;
        try
        {
            await RunDiagnosisAsync("Gaming", async () =>
            {
                var data = await resourceProbe.HardwareAsync(cancellation.Token);
                var cards = new List<InfoCard>();
                string N(long? n) => n is > 0 ? n.Value.ToString("N0") : "No informado";
                foreach (var cpu in data.Processors)
                    cards.Add(new("CPU · CIM", cpu.Name, $"{cpu.Cores} núcleos · {cpu.LogicalProcessors} procesadores lógicos\nL2: {N(cpu.L2CacheKb)} KiB · L3: {N(cpu.L3CacheKb)} KiB\nFrecuencia máxima declarada: {N(cpu.MaximumClockMhz)} MHz. No es frecuencia efectiva ni prueba de throttling."));
                foreach (var ram in data.MemoryModules)
                    cards.Add(new("MÓDULO RAM · SMBIOS", ram.Locator, $"{ram.CapacityBytes / Math.Pow(1024,3):N1} GiB\nConfiguredClockSpeed: {N(ram.ConfiguredClock)} · Speed: {N(ram.RatedClock)} (valores SMBIOS). No confirma canales, DOCP/XMP ni estabilidad."));
                foreach (var display in data.Displays)
                    cards.Add(new("MODO DE VÍDEO · CIM", display.Name, $"{N(display.Width)} × {N(display.Height)} · {N(display.RefreshRate)} Hz informados. No equivale a FPS del juego ni confirma HDR/VRR."));
                if (data.Security is { } security)
                {
                    string Flag(bool? value) => value.HasValue ? (value.Value ? "Sí" : "No") : "No informado";
                    cards.Add(new("SEGURIDAD · SOLO LECTURA", "TPM, Secure Boot y aislamiento",
                        $"TPM: {security.TpmSpecification ?? "No informado"} (versión declarada)\nSecure Boot confirmado por API: {Flag(security.SecureBoot)}\n" +
                        $"Estado VBS: {security.VbsStatus switch {0 => "No habilitado",1 => "Habilitado, sin ejecutar",2 => "En ejecución",_ => "No informado"}} · HVCI en ejecución: {Flag(security.HvciRunning)}\n" +
                        $"{string.Join("\n",security.Limitations)}\nNo informado no significa desactivado. No se cambia seguridad ni se infiere compatibilidad anticheat."));
                }
                var gpuData = await gpuProbe.ReadAsync(cancellation.Token);
                foreach (var gpu in gpuData.Devices)
                    cards.Add(new("GPU · NVML · SOLO LECTURA", gpu.Name,
                        $"VRAM dedicada: {(gpu.VramTotal is { } total ? $"{total / Math.Pow(1024,3):N1} GiB" : "N/D")} · usada: {(gpu.VramUsed is { } used ? $"{used / Math.Pow(1024,3):N1} GiB" : "N/D")}\n" +
                        $"Uso: {gpu.Utilization?.ToString() ?? "N/D"}% · temperatura: {gpu.TemperatureC?.ToString() ?? "N/D"} °C\nReloj gráfico: {gpu.GraphicsClockMhz?.ToString() ?? "N/D"} MHz · consumo: {gpu.PowerWatts?.ToString("N1") ?? "N/D"} W\n" +
                        $"Máscara de eventos de reloj NVML: {gpu.ClockEventReasons?.ToString("X") ?? "N/D"}. Sin interpretación causal automática.\n{string.Join("; ", gpu.Unavailable)}\nObservado: {gpu.ObservedUtc.ToLocalTime():g}"));
                cards.Add(new("COBERTURA", "Datos medidos y limitaciones", string.Join("\n", data.Limitations) + "\n" + gpuData.Status));
                return cards;
            });
        }
        finally { resourceCancellation = null; }
    }

    private async void CaptureResources(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        using var cancellation = new CancellationTokenSource(); resourceCancellation = cancellation;
        try
        {
            await RunDiagnosisAsync("Gaming", async () =>
            {
                var capture = await resourceProbe.CaptureAsync(cancellation.Token);
                var findings = await Task.Run(() => ResourceDiagnostics.Analyze(capture), cancellation.Token);
                latestResourceCapture = capture;
                return PresentResourceFindings(findings);
            });
        }
        finally { resourceCancellation = null; }
    }

    internal void ConfigureResourceDesignPreview(ResourceCapture capture) => diagnosticCards["Gaming"] = PresentResourceFindings(ResourceDiagnostics.Analyze(capture));
    private static InfoCard[] PresentResourceFindings(IEnumerable<ResourceFinding> findings)
    {
        return findings.Select(f => new InfoCard($"EVIDENCIA · {f.Confidence switch { DiagnosticConfidence.Moderate => "MODERADA", DiagnosticConfidence.Low => "BAJA", _ => "INSUFICIENTE" }}", f.Title,
                    $"Observación: {f.Evidence}\nFalta: {f.MissingInformation}\nAcción sugerida: {f.SuggestedAction}\nRiesgo: {f.Risk}\nValidación: {f.Validation}")).ToArray();
    }

    private void ExportResources(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || latestResourceCapture is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Captura de recursos JSON (*.json)|*.json", DefaultExt = ".json", AddExtension = true, FileName = "recursos.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { LocalJsonFile.Write(dialog.FileName, latestResourceCapture); model.Status = "Captura de recursos exportada con tiempos relativos y limitaciones."; }
        catch (Exception ex) { model.Status = "No se exportó la captura: " + ex.Message; }
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
