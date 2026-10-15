using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PCOptimizer.Core;

namespace PCOptimizer.App;

public partial class MainWindow : Window
{
    private sealed record SoftwareOutcome(string State, bool StopBatch = false);
    private readonly MainViewModel model = new();
    private readonly PowerHistory history = new();
    private readonly ActionJournal softwareHistory = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCOptimizer", "software-history.json"));
    private readonly RecoveryStore recoveryStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCOptimizer", "Recovery"));
    private string? recoveryListIssue;
    private Inventory? inventory;
    private string section = "Inicio";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        Loaded += async (_, _) => { RefreshRecoverySessions(); await RefreshAsync(); };
        Closing += (_, e) =>
        {
            if (model.IsIdle) return;
            e.Cancel = true;
            model.Status = "Espera a que termine la operación antes de cerrar.";
        };
        // Initialize selection after all named XAML elements exist.
        SelectFirstNavigation(this);
    }

    private static bool SelectFirstNavigation(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is RadioButton { Tag: "Inicio" } button) { button.IsChecked = true; return true; }
            if (SelectFirstNavigation(child)) return true;
        }
        return false;
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        section = (string)((RadioButton)sender).Tag;
        Render();
    }

    private void Render()
    {
        model.Title = section;
        model.Cards.Clear();
        EnergyPanel.Visibility = section == "Energía" ? Visibility.Visible : Visibility.Collapsed;
        InstallPanel.Visibility = section == "Instalación rápida" ? Visibility.Visible : Visibility.Collapsed;
        OptionalAppsPanel.Visibility = section == "Limpieza de apps" ? Visibility.Visible : Visibility.Collapsed;
        PersonalizationPanel.Visibility = section == "Personalización" ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPanel.Visibility = section == "Windows Update" ? Visibility.Visible : Visibility.Collapsed;
        OptimizationPanel.Visibility = section == "Optimización" ? Visibility.Visible : Visibility.Collapsed;
        RecoveryPanel.Visibility = section == "Respaldo y recuperación" ? Visibility.Visible : Visibility.Collapsed;
        switch (section)
        {
            case "Inicio":
                model.Subtitle = "Conoce tu equipo antes de cambiar su configuración.";
                if (inventory is null)
                    Add("DIAGNÓSTICO", "Información aún no disponible", "Actualiza el diagnóstico para consultar el hardware real de este equipo.");
                else
                {
                    Add("PROCESADOR", inventory.Cpu, "Modelo informado por Windows. La carga por núcleo y la temperatura se incorporarán en una versión posterior.");
                    Add("GRÁFICOS", inventory.Gpu, "Se muestran los adaptadores detectados; no implica cuál está usando un juego.");
                    Add("MEMORIA", $"{inventory.MemoryBytes / Math.Pow(1024, 3):N1} GiB", "Memoria física instalada informada por Windows.");
                    Add("SISTEMA", inventory.Os, "Las funciones disponibles dependen de la versión de Windows y del fabricante.");
                }
                break;
            case "Energía":
                model.Subtitle = "Elige entre los planes que Windows ofrece en este equipo.";
                Add("DISPONIBLE", "Control de planes de energía", "Un plan de alto rendimiento puede aumentar consumo, ruido y temperatura sin mejorar los FPS. No se modifican voltajes ni frecuencias.");
                Add("REVERSIBLE", "Se conserva el plan anterior", "Cada cambio guarda primero el plan original en el historial local. Windows solicitará elevación para aplicar el cambio. En algunos equipos solo está disponible Equilibrado.");
                break;
            case "Almacenamiento":
                model.Subtitle = "Inventario de unidades físicas. El diagnóstico de salud está pendiente.";
                if (inventory is null || inventory.Disks.Length == 0)
                    Add("SIN DATOS", "No hay unidades detectadas", "Actualiza el diagnóstico. Algunos dispositivos pueden no exponer su información.");
                else foreach (var disk in inventory.Disks)
                    Add("UNIDAD FÍSICA", disk.Model, $"Capacidad: {disk.Size / Math.Pow(1000, 3):N0} GB. Salud, desgaste y temperatura: no evaluados.");
                Add("EN DESARROLLO", "Salud SMART / NVMe", "No es posible deducir la vida restante a partir del modelo o la capacidad. La lectura de desgaste y errores aún no está implementada.");
                break;
            case "Instalación rápida":
                model.Subtitle = "236 aplicaciones por categoría. Busca, selecciona y revisa antes de instalar.";
                Add("SELECCIÓN MANUAL", "WinGet y Microsoft Store, identificados por aplicación", "Sin programas preseleccionados. Las selecciones se conservan al filtrar; revisa el lote antes de instalar. WinGet conserva comprobaciones de hash y acuerdos. Las fuentes no certifican compatibilidad anticheat. Las descripciones de referencia están en inglés; las condiciones añadidas, en español.");
                break;
            case "Personalización":
                model.Subtitle = "Barra de tareas de Windows 11: comprobar compatibilidad antes de cambiar.";
                Add("REQUIERE EVALUACIÓN", "Barra e iconos más pequeños", "El tamaño de los iconos y la altura de toda la barra no son lo mismo. La disponibilidad de opciones nativas depende de tu compilación. El botón abre Configuración; no aplica un cambio ni confirma que la opción exista.");
                Add("COMPATIBILIDAD PENDIENTE", "Cambiar la altura completa", "No hay un método implementado aquí para reducir la altura. TaskbarSi no se trata como una solución universal de Windows 11. Herramientas de terceros requieren evaluar versión, estabilidad, licencia y recuperación. No se instala ninguna ni se reinicia Explorer automáticamente.");
                break;
            case "Windows Update":
                model.Subtitle = "Accesos nativos disponibles. Los perfiles de políticas requieren evaluación y aún no se aplican.";
                Add("REQUIERE EVALUACIÓN", "Perfil de aplazamientos de la referencia", "WinUtil propone 365 días para funciones y 4 para actualizaciones de calidad, excluir drivers y notificar antes de instalar. No es nuestra recomendación universal: depende de edición, políticas y soporte de tu versión; aplazar puede retrasar correcciones. Perfil sin implementar.");
                Add("REQUIERE EVALUACIÓN", "Recuperar configuración anterior", "La futura restauración recuperará lo guardado antes del cambio y detectará conflictos. No debe borrar políticas de una organización ni asumir que un valor predeterminado era el anterior. Perfil sin implementar.");
                Add("AVANZADO · REQUIERE EVALUACIÓN", "Desactivar actualizaciones", "Puede impedir parches de seguridad y afectar mantenimiento o componentes que los necesitan. Se conserva visible por petición del usuario; no forma parte de perfiles automáticos y no está conectado a un ejecutor. No se detienen servicios ni tareas.");
                Add("DISPONIBLE", "Programar desde Windows", "Puedes abrir Windows Update, horas activas, opciones e historial. Windows determina qué opciones ofrece tu equipo. Abrir estas páginas no aplica políticas desde PCOptimizer.");
                break;
            case "Respaldo y recuperación":
                model.Subtitle = "Puntos de Windows y registro de qué intentó cambiar PCOptimizer.";
                Add("ANTES DE CAMBIAR", "Punto por operación o lote, activado por defecto", "La opción está activada al abrir la app y puede cambiarse para esta sesión. Se guarda primero la lista de acciones y, en energía, el plan activo anterior. Windows pedirá permisos para crear el punto. Si no se confirma uno nuevo, se pedirá decidir antes de continuar.");
                Add("LÍMITES DE WINDOWS", "La creación puede no estar disponible", "Requiere protección del sistema, permisos y espacio. Checkpoint-Computer normalmente limita los nuevos puntos a uno cada 24 horas. No se altera esa política. Un registro local no garantiza que el punto siga disponible en Windows.");
                Add("RECUPERAR", "El asistente de Windows realiza la restauración", "Busca la descripción PCOptimizer y secuencia mostradas. La app abre el asistente, no preselecciona ni restaura automáticamente. Puede revertir otros cambios posteriores al punto y reiniciar Windows. Guarda tu trabajo y revisa los programas afectados.");
                Add("ALCANCE DEL RESPALDO", "Tus archivos necesitan una copia aparte", "Los puntos no son una copia de archivos, partidas o proyectos. El historial distingue planificado, en ejecución y resultado; no vigila cambios de otros programas. Recuperar el plan guardado restaura solo su selección, no sus opciones internas ni datos de aplicaciones.");
                if (recoveryListIssue is not null) Add("REGISTROS LOCALES NO DISPONIBLES", "Se conservaron los archivos", recoveryListIssue);
                break;
            case "Limpieza de apps":
                model.Subtitle = "Aplicaciones opcionales para tu usuario. Tú decides cuáles utilizas.";
                Add("ALCANCE LIMITADO", "Desinstalación selectiva, sin cambios globales", "Analiza primero y selecciona cada aplicación. No se eliminan Store, Gaming Services, Xbox, componentes de seguridad, controladores ni paquetes de otros usuarios.");
                Add("RIESGO DE DATOS", "Reinstalar no recupera necesariamente tus datos", "Guarda tus proyectos y preferencias antes. No hay copia de datos ni restauración automática. Una reinstalación desde Microsoft Store depende de su disponibilidad y puede no recuperar datos locales. Quitar una app no garantiza más FPS ni impide que vuelva con futuras actualizaciones.");
                break;
            case "Optimización":
                model.Subtitle = "71 ajustes visibles para evaluar. No se aplican desde este catálogo.";
                Add("CONSERVADORA E INTENSIVA · PREVISTAS", "Cada ajuste necesita evidencia y recuperación", "Se conservan las opciones de referencia, incluidas las avanzadas. Todas indican Requiere evaluación y el estado de tu equipo aún no se ha consultado. No hay ejecutores de registro, servicios o timers conectados. Los accesos siguientes abren opciones nativas de Windows; no aplican perfiles.");
                break;
            case "Historial":
                model.Subtitle = "Registro local de energía, instalaciones y limpieza. No equivale a una copia de seguridad.";
                try
                {
                    var changes = history.Read();
                    foreach (var change in changes.AsEnumerable().Reverse())
                        Add(change.State, change.Date.ToLocalTime().ToString("g"), $"Plan anterior: {change.Previous:D}\nPlan solicitado: {change.Target:D}");
                }
                catch (Exception ex) { Add("HISTORIAL NO DISPONIBLE", "No se pudo leer el registro", ex.Message); }
                try
                {
                    foreach (var action in softwareHistory.Read().Reverse())
                        Add(action.State, action.Name, $"{action.Date.ToLocalTime():g} · {action.Kind switch { SoftwareActionKind.InstallSoftware => "Instalación", SoftwareActionKind.OpenDownloadPage => "Página de descarga", _ => "Desinstalación para tu usuario" }}\n{action.TargetId}\nLas instalaciones y desinstalaciones no tienen restauración automática.");
                }
                catch (Exception ex) { Add("HISTORIAL DE SOFTWARE NO DISPONIBLE", "Se conservó el registro original", ex.Message); }
                if (model.Cards.Count == 0) Add("SIN CAMBIOS", "Todavía no hay operaciones registradas", "Los diagnósticos solo consultan información.");
                break;
            default:
                model.Subtitle = "Funciones previstas. Esta sección todavía no ejecuta ajustes ni mediciones.";
                var card = section switch
                {
                    "Gaming" => new InfoCard("EN DESARROLLO", "Rendimiento por juego", "FPS, tiempos de cuadro y análisis de CPU/GPU requieren mediciones durante una partida. No se muestran porcentajes de cuello de botella inventados."),
                    "Red" => new InfoCard("EN DESARROLLO", "Encontrar la causa del lag", "Se medirán latencia, variación y pérdida de paquetes. No se aplican cambios de DNS, TCP ni del adaptador en esta versión."),
                    "Audio" => new InfoCard("EN DESARROLLO", "Loudness EQ según compatibilidad", "Puede hacer más audibles los sonidos suaves, pero alterar la percepción de distancia. Depende del dispositivo y del controlador; todavía no se detecta ni se activa."),
                    _ => new InfoCard("NO DISPONIBLE", "Sección desconocida", "Vuelve al inicio.")
                };
                model.Cards.Add(card);
                break;
        }
    }

    private void Add(string label, string value, string detail) => model.Cards.Add(new(label, value, detail));

    private async void Refresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        model.IsIdle = false;
        model.Status = "Consultando hardware y planes de energía…";
        var issues = new List<string>();
        try { inventory = await WindowsCommands.GetInventoryAsync(); }
        catch (Exception ex) { inventory = null; issues.Add("Inventario: " + ex.Message); }
        try { await RefreshPlansAsync(); }
        catch (Exception ex) { model.PowerPlans.Clear(); model.SelectedPlan = null; issues.Add("Energía: " + ex.Message); }
        model.IsIdle = true;
        Render();
        model.Status = issues.Count == 0 ? $"Diagnóstico actualizado a las {DateTime.Now:t}. No se aplicaron cambios." : string.Join(" · ", issues);
    }

    private async Task RefreshPlansAsync()
    {
        var plans = await WindowsCommands.GetPlansAsync();
        model.PowerPlans.Clear();
        foreach (var plan in plans) model.PowerPlans.Add(plan);
        model.SelectedPlan = plans.Single(p => p.IsActive);
    }

    private string RecoveryPreferenceText => model.CreateRestorePointBeforeChanges
        ? "Antes de la operación se intentará crear y confirmar un punto de Windows."
        : "Has desactivado para esta sesión la creación previa del punto de Windows. Se conservará el registro local, que no sustituye ese punto ni una copia de archivos.";

    private void RefreshRecoverySessions(Guid? prefer = null)
    {
        var selectedId = prefer ?? model.SelectedRecoverySession?.Id;
        model.RecoverySessions.Clear();
        try
        {
            foreach (var session in recoveryStore.List()) model.RecoverySessions.Add(session);
            model.SelectedRecoverySession = model.RecoverySessions.FirstOrDefault(s => s.Id == selectedId) ?? model.RecoverySessions.FirstOrDefault();
            recoveryListIssue = null;
        }
        catch (Exception ex)
        {
            model.SelectedRecoverySession = null;
            recoveryListIssue = ex.Message;
        }
    }

    private async Task<RecoverySession?> PrepareRecoveryAsync(IReadOnlyList<RecoveryAction> actions, bool manual = false)
    {
        // Save the intended changes before elevation or any application/system change.
        var session = recoveryStore.Begin(actions);
        RefreshRecoverySessions(session.Id);
        if (!manual && !model.CreateRestorePointBeforeChanges)
            return recoveryStore.SetPoint(session.Id, null, "Punto no solicitado por el usuario");

        model.Status = "Creando y comprobando el punto de Windows. Puede tardar varios minutos; revisa UAC.";
        WindowsRestorePoint? point = null;
        string? failure = null;
        try { point = await WindowsRecoveryService.CreatePointAsync(session.Id); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { failure = "Se canceló el permiso de Windows para crear el punto."; }
        catch (Exception ex) { failure = ex.Message; }
        if (point is not null)
        {
            session = recoveryStore.SetPoint(session.Id, point, "Creación del punto confirmada");
            RefreshRecoverySessions(session.Id);
            return session;
        }

        session = recoveryStore.SetPoint(session.Id, null, "Sin nuevo punto confirmado");
        RefreshRecoverySessions(session.Id);
        if (manual) { model.Status = failure ?? "No se confirmó un punto nuevo."; return null; }
        var decision = MessageBox.Show(this,
            $"{failure}\n\nNo se ha iniciado la operación. El registro local de acciones no sustituye un punto de restauración ni una copia de archivos.\n\n¿Continuar con la operación SIN un nuevo punto confirmado? Elige No para cancelar y revisar Protección del sistema.",
            "Punto de restauración no confirmado", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (decision != MessageBoxResult.Yes)
        {
            recoveryStore.SetPendingActionsState(session.Id, "No ejecutada: operación cancelada antes del cambio");
            model.Status = "Operación cancelada antes de aplicar cambios. No se confirmó un nuevo punto de Windows.";
            return null;
        }
        return recoveryStore.SetPoint(session.Id, null, "Sin nuevo punto; continuación autorizada");
    }

    private async void CreateRestorePoint(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        if (MessageBox.Show(this, "Se pedirá permiso para crear un punto de restauración de Windows. No es una copia de tus archivos. Windows puede impedirlo por protección desactivada, espacio o límite de 24 horas. ¿Crear ahora?",
            "Crear punto de restauración", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        model.IsIdle = false;
        try
        {
            var session = await PrepareRecoveryAsync([], manual: true);
            if (session?.Point is { } point) model.Status = $"Punto creado y consultado en Windows: {point.Description}, secuencia {point.SequenceNumber}.";
        }
        catch (Exception ex) { model.Status = "No se pudo completar el registro del punto: " + ex.Message; }
        finally { model.IsIdle = true; RefreshRecoverySessions(); Render(); }
    }

    private void RefreshRecovery(object sender, RoutedEventArgs e)
    {
        RefreshRecoverySessions();
        Render();
        model.Status = recoveryListIssue ?? "Registros locales actualizados. La disponibilidad actual de los puntos se comprueba en el asistente de Windows.";
    }

    private void OpenSystemProtection(object sender, RoutedEventArgs e)
    {
        try { WindowsRecoveryService.OpenProtection(); model.Status = "Se solicitó abrir Protección del sistema. Revisa allí la unidad, protección y espacio; no se activaron automáticamente."; }
        catch (Exception ex) { model.Status = "No se pudo abrir Protección del sistema: " + ex.Message; }
    }

    private void OpenRestoreWizard(object sender, RoutedEventArgs e)
    {
        try { WindowsRecoveryService.OpenRestoreWizard(); model.Status = "Se solicitó abrir el asistente. Elige allí el punto por su descripción y revisa los programas afectados. No se preseleccionó ni ejecutó una restauración."; }
        catch (Exception ex) { model.Status = "No se pudo abrir el asistente: " + ex.Message; }
    }

    private async void RestoreSavedPowerPlan(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || model.SelectedRecoverySession is not { } selected) return;
        model.IsIdle = false;
        try
        {
            var session = recoveryStore.Read(selected.Id);
            var action = session.Actions.FirstOrDefault(a => a.Kind == RecoveryActionKind.PowerPlan);
            if (action is null || !Guid.TryParse(action.PreviousValue, out var id))
            {
                model.Status = "Este registro no contiene una selección anterior de plan de energía.";
                return;
            }
            var plans = await WindowsCommands.GetPlansAsync();
            var plan = plans.SingleOrDefault(p => p.Id == id);
            if (plan is null) { model.Status = "El plan guardado ya no existe. Este registro no puede recrear sus opciones internas."; return; }
            model.IsIdle = true;
            await ChangePlanAsync(id, plan.Name);
        }
        catch (Exception ex) { model.Status = "No se pudo recuperar la selección de plan: " + ex.Message; }
        finally { model.IsIdle = true; }
    }

    private async void InstallSelected(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        var selected = model.InstallChoices.Where(p => p.IsSelected).ToArray();
        if (selected.Length == 0) { model.Status = "Selecciona al menos un programa."; return; }
        var review = new SelectionReviewWindow("Revisar instalación", selected,
            "Se abrirá la fuente indicada en cada ficha. Revisa acuerdos, permisos, opciones adicionales e inicio automático. No se aceptan licencias por ti ni se actualizan programas existentes. El lote se detiene ante errores o reinicios. No hay reversión automática de instalaciones. " + RecoveryPreferenceText) { Owner = this };
        if (review.ShowDialog() != true) return;
        await RunSoftwareBatchAsync(selected, SoftwareActionKind.InstallSoftware, async choice =>
        {
            if (SoftwareCatalog.GetPackage(choice.Id).OpensWebsite)
            {
                SoftwareServices.OpenOfficialWebsite(choice.Id);
                return new SoftwareOutcome("Se solicitó abrir el sitio oficial; no se descargó ni verificó una instalación");
            }
            var exitCode = await SoftwareServices.InstallAsync(choice.Id);
            var result = WingetResult.FromExitCode(exitCode);
            if (result.IsFailure) throw new InvalidOperationException(result.Description);
            return new SoftwareOutcome(result.Description, result.StopBatch);
        });
    }

    private void ClearInstallSelection(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        foreach (var choice in model.InstallChoices) choice.IsSelected = false;
    }

    private void OpenPackageWebsite(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || ((FrameworkElement)sender).DataContext is not SoftwareChoice choice) return;
        try
        {
            SoftwareServices.OpenOfficialWebsite(choice.Id);
            model.Status = $"Se solicitó abrir el sitio oficial de {choice.Name}. No se ha descargado ni instalado desde PCOptimizer.";
        }
        catch (Exception ex) { model.Status = "No se pudo abrir el sitio: " + ex.Message; }
    }

    private void OpenWindowsSettings(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || ((FrameworkElement)sender).Tag is not string key) return;
        try
        {
            SoftwareServices.OpenSettings(key);
            model.Status = "Se solicitó abrir Configuración de Windows. No se aplicó ni verificó un ajuste desde PCOptimizer.";
        }
        catch (Exception ex) { model.Status = "No se pudo abrir Configuración: " + ex.Message; }
    }

    private async void ScanOptionalApps(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        model.IsIdle = false;
        model.Status = "Buscando únicamente las aplicaciones opcionales del catálogo para tu usuario…";
        try
        {
            var installed = await SoftwareServices.GetOptionalAppsAsync();
            foreach (var choice in model.OptionalAppChoices)
            {
                choice.IsSelected = false;
                choice.CanSelect = installed.Contains(choice.Id);
                choice.Availability = choice.CanSelect ? "Detectada para tu usuario" : "No detectada para tu usuario";
            }
            model.Status = $"Análisis terminado: {installed.Count} aplicaciones del catálogo detectadas. No se modificó Windows.";
        }
        catch (Exception ex)
        {
            foreach (var choice in model.OptionalAppChoices)
            {
                choice.IsSelected = false;
                choice.CanSelect = false;
                choice.Availability = "No se pudo comprobar su estado";
            }
            model.Status = "El análisis falló: " + ex.Message;
        }
        finally { model.IsIdle = true; }
    }

    private async void RemoveSelectedApps(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        var selected = model.OptionalAppChoices.Where(p => p.IsSelected && p.CanSelect).ToArray();
        if (selected.Length == 0) { model.Status = "Analiza primero y selecciona al menos una aplicación detectada."; return; }
        var summary = string.Join("\n\n", selected.Select(p => $"• {p.Name} ({p.Id})\n{p.Detail}"));
        if (MessageBox.Show(this, $"{summary}\n\nSe desinstalarán solo para tu usuario. Guarda y cierra tus proyectos: pueden perderse datos locales. No se hará una copia de datos ni hay restauración automática. Reinstalar desde Store depende de disponibilidad y no recupera necesariamente los datos. No se garantiza una mejora de FPS.\n\n{RecoveryPreferenceText}\n\n¿Desinstalar estas aplicaciones?",
            "Revisar desinstalación", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        await RunSoftwareBatchAsync(selected, SoftwareActionKind.RemoveOptionalApp, async choice =>
        {
            await SoftwareServices.RemoveOptionalAppAsync(choice.Id);
            choice.CanSelect = false;
            choice.Availability = "Ausencia verificada para tu usuario";
            return new SoftwareOutcome("Ausencia verificada para el usuario actual");
        });
    }

    private async Task RunSoftwareBatchAsync(IReadOnlyList<SoftwareChoice> selected, SoftwareActionKind kind,
        Func<SoftwareChoice, Task<SoftwareOutcome>> action)
    {
        model.IsIdle = false;
        var completed = 0;
        Guid? operationId = null;
        RecoverySession? recovery = null;
        Guid? recoveryActionId = null;
        try
        {
            var actions = selected.Select(choice => new RecoveryAction(Guid.NewGuid(),
                kind == SoftwareActionKind.InstallSoftware ? RecoveryActionKind.InstallSoftware : RecoveryActionKind.RemoveOptionalApp,
                choice.Name, choice.Id,
                kind == SoftwareActionKind.InstallSoftware ? "Instalación y versión anteriores no inventariadas" : "Detectada en el análisis del usuario; se volverá a consultar",
                kind == SoftwareActionKind.InstallSoftware ? "Solicitar instalación sin actualizar existentes" : "Ausencia del paquete para el usuario actual")).ToArray();
            recovery = await PrepareRecoveryAsync(actions);
            if (recovery is null) return;
            for (var index = 0; index < selected.Count; index++)
            {
                var choice = selected[index];
                operationId = null;
                recoveryActionId = actions[index].Id;
                model.Status = $"Procesando {choice.Name} ({completed + 1}/{selected.Count}). Revisa cualquier ventana de autorización o instalación.";
                // A write failure prevents launching the operation; keep an auditable pending record on interruption.
                var actionKind = kind == SoftwareActionKind.InstallSoftware && SoftwareCatalog.GetPackage(choice.Id).OpensWebsite
                    ? SoftwareActionKind.OpenDownloadPage : kind;
                operationId = softwareHistory.Begin(actionKind, choice.Id);
                recoveryStore.SetActionState(recovery.Id, recoveryActionId.Value, "En ejecución; resultado aún no confirmado");
                var outcome = await action(choice);
                softwareHistory.Complete(operationId.Value, outcome.State);
                recoveryStore.SetActionState(recovery.Id, recoveryActionId.Value, outcome.State);
                choice.IsSelected = false;
                completed++;
                if (outcome.StopBatch)
                {
                    recoveryStore.SetPendingActionsState(recovery.Id, "No ejecutada: lote detenido por resultado anterior");
                    model.Status = $"Lote detenido tras {completed} solicitudes procesadas. {outcome.State}. No se lanzarán más instaladores. PCOptimizer no solicita reinicios automáticos.";
                    return;
                }
            }
            model.Status = kind == SoftwareActionKind.InstallSoftware
                ? $"Se procesaron {completed} solicitudes. El historial distingue resultados y omisiones; comprueba las instalaciones en Windows."
                : $"Ausencia verificada para tu usuario en {completed} aplicaciones. No hay restauración de datos automática.";
        }
        catch (Exception ex)
        {
            model.Status = $"Lote detenido tras {completed} operaciones completadas: {ex.Message} Comprueba el estado de la última operación antes de reintentar.";
            if (operationId is { } id)
            {
                try { softwareHistory.Complete(id, "Operación no completada; comprobar estado en Windows"); }
                catch (Exception journalError) { model.Status += " No se pudo actualizar el historial: " + journalError.Message; }
            }
            if (recovery is not null)
            {
                try
                {
                    if (recoveryActionId is { } actionId) recoveryStore.SetActionState(recovery.Id, actionId, "Resultado no confirmado; comprobar Windows antes de reintentar");
                    recoveryStore.SetPendingActionsState(recovery.Id, "No ejecutada: lote detenido");
                }
                catch (Exception recoveryError) { model.Status += " No se pudo actualizar el respaldo: " + recoveryError.Message; }
            }
        }
        finally { model.IsIdle = true; RefreshRecoverySessions(recovery?.Id); Render(); }
    }

    private async void ApplyPowerPlan(object sender, RoutedEventArgs e)
    {
        if (model.SelectedPlan is not { } selected) { model.Status = "Selecciona un plan disponible."; return; }
        await ChangePlanAsync(selected.Id, selected.Name);
    }

    private async void RestorePowerPlan(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        model.IsIdle = false;
        try
        {
            var last = history.Read().LastOrDefault(change => change.State != "Cancelado");
            if (last is null) { model.Status = "No hay cambios que restaurar."; return; }
            var plans = await WindowsCommands.GetPlansAsync();
            if (plans.Single(p => p.IsActive).Id != last.Target)
            {
                model.Status = "El plan actual no coincide con el último cambio. No se modificó; elige un plan manualmente.";
                return;
            }
            var previous = plans.SingleOrDefault(p => p.Id == last.Previous);
            if (previous is null) { model.Status = "El plan anterior ya no está disponible en Windows."; return; }
            model.IsIdle = true;
            await ChangePlanAsync(previous.Id, previous.Name);
        }
        catch (Exception ex) { model.Status = "No se pudo restaurar: " + ex.Message; }
        finally { model.IsIdle = true; }
    }

    private async Task ChangePlanAsync(Guid target, string name)
    {
        if (!model.IsIdle) return;
        if (MessageBox.Show(this, $"¿Activar «{name}»?\n\nPuede cambiar el consumo, la temperatura y el ruido. Se guardará la selección de plan anterior para restaurarla. Windows puede solicitar permisos de administrador.\n\n{RecoveryPreferenceText}",
            "Cambiar plan de energía", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        model.IsIdle = false;
        model.Status = "Aplicando el plan y comprobando el resultado…";
        List<PowerChange>? changes = null;
        RecoverySession? recovery = null;
        RecoveryAction? recoveryAction = null;
        try
        {
            var plans = await WindowsCommands.GetPlansAsync();
            var current = plans.Single(p => p.IsActive);
            if (current.Id == target) { model.Status = "Ese plan ya está activo."; return; }
            if (!plans.Any(p => p.Id == target)) throw new InvalidOperationException("El plan seleccionado ya no existe.");
            recoveryAction = new RecoveryAction(Guid.NewGuid(), RecoveryActionKind.PowerPlan,
                $"Seleccionar plan: {name}", target.ToString("D"), current.Id.ToString("D"), target.ToString("D"));
            recovery = await PrepareRecoveryAsync([recoveryAction]);
            if (recovery is null) return;
            var latestPlans = await WindowsCommands.GetPlansAsync();
            if (latestPlans.Single(p => p.IsActive).Id != current.Id || !latestPlans.Any(p => p.Id == target))
            {
                recoveryStore.SetActionState(recovery.Id, recoveryAction.Id, "No ejecutada: los planes cambiaron durante la preparación del respaldo");
                model.Status = "La configuración de energía cambió mientras se preparaba el respaldo. No se activó ningún plan; actualiza el diagnóstico y revisa de nuevo.";
                return;
            }
            changes = history.Read();
            changes.Add(new PowerChange(current.Id, target, DateTimeOffset.UtcNow, "Pendiente de verificar"));
            history.Save(changes); // Write recovery information before making any system change.
            recoveryStore.SetActionState(recovery.Id, recoveryAction.Id, "En ejecución; resultado aún no confirmado");
            await WindowsCommands.ActivateAsync(target);
            await RefreshPlansAsync();
            if (model.SelectedPlan?.Id != target) throw new InvalidOperationException("Windows no confirmó el plan solicitado. Consulta el historial antes de reintentar.");
            changes[^1] = changes[^1] with { State = "Aplicado y verificado" };
            history.Save(changes);
            recoveryStore.SetActionState(recovery.Id, recoveryAction.Id, "Plan activo aplicado y verificado");
            model.Status = "Plan aplicado y verificado. Puedes restaurar el plan anterior desde Energía.";
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            model.Status = "Se canceló la autorización de Windows. No se aplicó el cambio solicitado.";
            if (changes is not null)
            {
                changes[^1] = changes[^1] with { State = "Cancelado" };
                try { history.Save(changes); }
                catch (Exception saveError) { model.Status += " No se pudo actualizar el historial: " + saveError.Message; }
            }
            if (recovery is not null && recoveryAction is not null)
            {
                try { recoveryStore.SetActionState(recovery.Id, recoveryAction.Id, "Cancelado en UAC antes de activar el plan"); }
                catch (Exception saveError) { model.Status += " No se pudo actualizar el respaldo: " + saveError.Message; }
            }
        }
        catch (Exception ex)
        {
            model.Status = "No se pudo completar el cambio: " + ex.Message;
            if (recovery is not null && recoveryAction is not null)
            {
                try { recoveryStore.SetActionState(recovery.Id, recoveryAction.Id, "Resultado no confirmado; comprobar el plan activo"); }
                catch (Exception saveError) { model.Status += " No se pudo actualizar el respaldo: " + saveError.Message; }
            }
        }
        finally { model.IsIdle = true; RefreshRecoverySessions(recovery?.Id); Render(); }
    }
}
