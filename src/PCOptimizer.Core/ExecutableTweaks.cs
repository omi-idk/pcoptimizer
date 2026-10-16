namespace PCOptimizer.Core;

public sealed record RegistryValueSnapshot(bool Exists, string Kind, string Data)
{
    public static RegistryValueSnapshot Missing { get; } = new(false, "", "");
    public string Display => Exists ? $"{Kind}: {Data}" : "Valor ausente (Windows decide)";
}

public sealed record RegistryTweakTarget(string Id, string Path, string Name, RegistryValueSnapshot Desired, bool Machine = false, uint? ClearBits = null, uint? SetBits = null);
public enum TweakRisk { Preference, Standard, Advanced }
public sealed record ExecutableTweak(string Id, string Name, string Category, string Benefit,
    string Risk, string Activation, string Source, IReadOnlyList<RegistryTweakTarget> Targets)
{
    public string? ExclusiveGroup { get; init; }
    public int MinimumWindowsBuild { get; init; }
    public TweakRisk? RiskOverride { get; init; }
    public TweakRisk RiskLevel => RiskOverride ?? (Targets.Any(t => t.Machine) ? TweakRisk.Advanced :
        Category is "Gaming" or "Entrada" ? TweakRisk.Standard : TweakRisk.Preference);
}

// This is the complete execution allowlist. The reference JSON catalog never enters this engine.
public static class ExecutableTweaks
{
    private static RegistryTweakTarget D(string id, string path, string name, uint value) =>
        new(id, path, name, new(true, "DWord", value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    private const string Explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string GameBar = @"Software\Microsoft\GameBar";
    public static IReadOnlyList<ExecutableTweak> Items { get; } = Array.AsReadOnly(new[]
    {
        new ExecutableTweak("game-mode", "Activar Modo Juego", "Gaming",
            "Solicita a Windows que use Modo Juego. El efecto depende del juego y de la carga en segundo plano; no garantiza FPS adicionales.",
            "Puede no mejorar tu juego. Son preferencias por usuario, no políticas; comprueba también el interruptor en Configuración de Windows.",
            "Cierra y vuelve a abrir el juego. Confirma Modo Juego en Configuración.",
            "https://support.xbox.com/help/games-apps/game-setup-and-play/use-game-mode-gaming-on-pc",
            new[] { D("game-mode-auto", GameBar, "AutoGameModeEnabled", 1), D("game-mode-allow", GameBar, "AllowAutoGameMode", 1) }),
        new ExecutableTweak("game-capture", "Desactivar grabación de Xbox Game Bar", "Gaming",
            "Desactiva las preferencias de captura de Game Bar para evitar grabaciones que no utilizas. Puede liberar recursos si había captura activa.",
            "Perderás clips y grabación con Game Bar. No desactiva OBS, herramientas de la GPU ni otros grabadores. No elimina Xbox ni Gaming Services.",
            "Cierra Game Bar y vuelve a iniciar sesión. Comprueba Capturas en Windows.",
            "https://support.xbox.com/help/games-apps/game-setup-and-play/adjust-capture-settings-windows-10",
            new[] { D("capture-enabled", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                D("capture-history", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0),
                D("capture-dvr", @"System\GameConfigStore", "GameDVR_Enabled", 0) }),
        new ExecutableTweak("transparency", "Desactivar transparencias", "Experiencia de Windows",
            "Reduce efectos de transparencia del escritorio. Es una preferencia visual; no se promete una mejora dentro del juego.",
            "Cambiará el aspecto de Inicio, barra de tareas y ventanas compatibles.",
            "Puede requerir cerrar y volver a iniciar sesión. No se reinicia Explorer automáticamente.",
            "https://github.com/Raphire/Win11Debloat/blob/4079b276e9e93aa8698a9f70f5aa228ba11f8265/Regfiles/Disable_Transparency.reg",
            new[] { D("transparency", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0) }),
        new ExecutableTweak("mouse-acceleration", "Desactivar aceleración del puntero de Windows", "Gaming",
            "Guarda umbrales y aceleración del puntero en cero. Puede dar un movimiento más predecible en juegos que usan el puntero de Windows; los juegos con Raw Input suelen ignorarlo.",
            "Cambiará la sensación del ratón en el escritorio. No reduce el ping ni la latencia física del dispositivo. Conserva tu sensibilidad preferida dentro de cada juego.",
            "Cierra y vuelve a iniciar sesión. Comprueba Mejorar la precisión del puntero en las propiedades de ratón de Windows.",
            "https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfoa",
            new[] { new RegistryTweakTarget("mouse-speed", @"Control Panel\Mouse", "MouseSpeed", new(true, "String", "0")),
                new RegistryTweakTarget("mouse-threshold1", @"Control Panel\Mouse", "MouseThreshold1", new(true, "String", "0")),
                new RegistryTweakTarget("mouse-threshold2", @"Control Panel\Mouse", "MouseThreshold2", new(true, "String", "0")) }),
        new ExecutableTweak("window-animation", "Desactivar animación al minimizar ventanas", "Experiencia de Windows",
            "Hace más inmediatas las transiciones al minimizar y restaurar ventanas. No cambia los FPS del juego.",
            "El escritorio pierde esa animación. No desactiva todas las animaciones de Windows.",
            "Cierra y vuelve a iniciar sesión para que todas las aplicaciones relean la preferencia.",
            "https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json",
            new[] { new RegistryTweakTarget("window-animation", @"Control Panel\Desktop\WindowMetrics", "MinAnimate", new(true, "String", "0")) }),
        new ExecutableTweak("file-extensions", "Mostrar extensiones de archivos", "Explorador",
            "Permite distinguir ejecutables, scripts y documentos por su extensión. Es una mejora de visibilidad, no de FPS.",
            "Cambiar la extensión al renombrar puede impedir que un archivo se abra correctamente.",
            "Puede ser necesario abrir otra ventana del Explorador o volver a iniciar sesión.",
            "https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json",
            new[] { D("file-extensions", Explorer, "HideFileExt", 0) }),
        new ExecutableTweak("hidden-files", "Mostrar archivos ocultos", "Explorador",
            "Facilita localizar configuraciones de juegos en carpetas ocultas. Mantiene protegidos los archivos del sistema.",
            "Verás archivos que normalmente se ocultan. No los elimines sin conocer su función; no mejora el rendimiento.",
            "Puede ser necesario abrir otra ventana del Explorador o volver a iniciar sesión.",
            "https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json",
            new[] { D("hidden-files", Explorer, "Hidden", 1) }),
        new ExecutableTweak("taskbar-left", "Alinear la barra de tareas a la izquierda", "Personalización",
            "Aplica la preferencia de alineación en Windows 11; no cambia la altura ni los FPS.",
            "La compatibilidad depende de la compilación y de si usas una barra modificada por terceros.",
            "Puede requerir volver a iniciar sesión.",
            "https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json",
            new[] { D("taskbar-left", Explorer, "TaskbarAl", 0) }),
        new ExecutableTweak("task-view", "Ocultar botón Vista de tareas", "Personalización",
            "Libera espacio en la barra de tareas; Windows + Tab sigue disponible. No cambia los FPS.",
            "Se oculta el acceso con el ratón en la barra.", "Puede requerir volver a iniciar sesión.",
            "https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json",
            new[] { D("task-view", Explorer, "ShowTaskViewButton", 0) }),
        new ExecutableTweak("hags", "Solicitar programación de GPU por hardware (HAGS)", "Avanzado · equipo",
            "Solicita HAGS en equipos y controladores compatibles. Necesita comparación antes/después: puede mejorar, no cambiar o empeorar tiempos de cuadro.",
            "Requiere administrador y reinicio. No detectamos todavía soporte de GPU/driver: compruébalo en Configuración de gráficos antes de seleccionar. Puede causar inestabilidad o conflictos con captura. Fuera de los perfiles automáticos.",
            "Reinicia manualmente y comprueba el interruptor en Configuración de gráficos. Guardar el valor no prueba que Windows haya activado HAGS.",
            "https://devblogs.microsoft.com/directx/hardware-accelerated-gpu-scheduling/",
            new[] { new RegistryTweakTarget("hags", @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", new(true, "DWord", "2"), true) }) with { ExclusiveGroup = "hags" },
        new ExecutableTweak("update-feature-delay", "Aplazar actualizaciones de funciones 30 días", "Windows Update · equipo",
            "En ediciones compatibles, solicita 30 días de aplazamiento de nuevas versiones de Windows. No cambia el aplazamiento de parches de calidad y seguridad.",
            "Requiere administrador y Windows Pro, Enterprise o Education compatible. Puede retrasar novedades y no extiende el soporte de una versión. Se rechaza en equipos de dominio, unidos a una organización o con inscripción MDM detectada.",
            "Reinicia manualmente y verifica las políticas en Windows Update. La existencia del valor no confirma por sí sola que Windows acepte la política.",
            "https://learn.microsoft.com/windows/deployment/update/waas-configure-wufb",
            new[] { new RegistryTweakTarget("updates-feature-enable", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferFeatureUpdates", new(true, "DWord", "1"), true),
                new RegistryTweakTarget("updates-feature-days", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferFeatureUpdatesPeriodInDays", new(true, "DWord", "30"), true) }),
        new ExecutableTweak("update-driver-exclusion", "Excluir drivers de las actualizaciones de calidad", "Windows Update · equipo",
            "Solicita separar los controladores incluidos en actualizaciones de calidad, para que puedas revisar las versiones del fabricante.",
            "Requiere administrador y edición compatible. Tendrás que revisar manualmente drivers y sus correcciones de seguridad. No bloquea todos los mecanismos por los que Windows puede instalar un driver.",
            "Reinicia y comprueba Windows Update. No descarga un controlador alternativo ni decide cuál es el recomendado.",
            "https://learn.microsoft.com/windows/deployment/update/waas-wu-settings",
            new[] { new RegistryTweakTarget("updates-exclude-drivers", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", new(true, "DWord", "1"), true) }),
        new ExecutableTweak("mpo", "Desactivar Multiplane Overlay (MPO)", "Avanzado · equipo",
            "Prueba de diagnóstico para parpadeos o problemas de presentación. No es una mejora general de FPS; NVIDIA publicó este workaround para síntomas específicos.",
            "Requiere administrador y reinicio. Puede aumentar consumo o empeorar presentación y reproducción de vídeo. Úsalo solo para comparar un problema reproducible; fuera de perfiles automáticos.",
            "Reinicia manualmente y reproduce el problema. El registro solo confirma el valor, no la solución del síntoma.",
            "https://nvidia.custhelp.com/app/answers/detail/a_id/5157",
            new[] { new RegistryTweakTarget("mpo", @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", new(true, "DWord", "5"), true) }),
        new ExecutableTweak("dark-theme", "Usar tema oscuro", "Personalización",
            "Selecciona modo oscuro para Windows y aplicaciones compatibles. Preferencia visual sin mejora de rendimiento atribuida.",
            "Algunas aplicaciones administran su propio tema y no seguirán esta selección.", "Puede requerir reabrir las aplicaciones.",
            "https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json",
            new[] { D("dark-apps", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0),
                D("dark-system", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) })
    }.Concat(AdditionalExecutableTweaks.Items).ToArray());
    public static ExecutableTweak Get(string id) => Items.SingleOrDefault(t => t.Id == id)
        ?? throw new InvalidDataException("Ajuste fuera de la lista de ejecución permitida.");
    public static RegistryTweakTarget Target(string tweakId, string targetId) => Get(tweakId).Targets.SingleOrDefault(t => t.Id == targetId)
        ?? throw new InvalidDataException("Valor fuera de la lista de ejecución permitida.");
    public static IReadOnlyList<string> Profile(string id) => id switch
    {
        "minimal" => new[] { "game-mode", "file-extensions", "windows-tips", "settings-suggestions" },
        "recommended" => new[] { "game-mode", "file-extensions", "windows-tips", "settings-suggestions", "ad-id", "tailored-experiences", "silent-apps", "start-suggestions", "lockscreen-suggestions" },
        "gaming" => new[] { "game-mode", "game-capture", "mouse-acceleration", "sticky-hotkey", "filter-hotkey", "toggle-hotkey", "restart-apps" },
        "intensive" => new[] { "game-mode", "game-capture", "mouse-acceleration", "sticky-hotkey", "filter-hotkey", "toggle-hotkey", "restart-apps", "transparency", "window-animation", "taskbar-animation", "aero-peek", "windows-tips", "settings-suggestions", "silent-apps", "start-suggestions", "lockscreen-suggestions" },
        _ => throw new ArgumentException("Perfil desconocido.", nameof(id))
    };

    public static RegistryValueSnapshot DesiredFor(RegistryTweakTarget target, RegistryValueSnapshot before)
    {
        if (target.ClearBits is null && target.SetBits is null) return target.Desired;
        if (!before.Exists || before.Kind != "String" || !uint.TryParse(before.Data,
            System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var original))
            throw new InvalidDataException("Windows no expone las banderas persistidas de esta opción. Revísala en sus opciones nativas; no se asumirá un valor por defecto.");
        return new(true, "String", ((original & ~(target.ClearBits ?? 0u)) | (target.SetBits ?? 0u)).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

public interface ITweakValueStore
{
    RegistryValueSnapshot Read(RegistryTweakTarget target);
    void Write(RegistryTweakTarget target, RegistryValueSnapshot expected, RegistryValueSnapshot value);
}

public interface IBatchTweakValueStore : ITweakValueStore
{
    void WriteMachineBatch(IReadOnlyList<TweakValueChange> changes);
}

public sealed record TweakValueChange(string TweakId, string TargetId, RegistryValueSnapshot Before,
    RegistryValueSnapshot After, string State = "Preparado");
public sealed record TweakOperation(int SchemaVersion, Guid Id, DateTimeOffset CreatedAtUtc,
    Guid? UndoOf, IReadOnlyList<TweakValueChange> Changes, string State)
{
    public string DisplayName => $"{CreatedAtUtc.ToLocalTime():g} · {(UndoOf is null ? "Aplicar" : "Revertir")} · {State}";
}

// Durable, per-value recovery; no arbitrary registry path, hive, script, or executable is loaded from disk.
public sealed class TweakEngine(ITweakValueStore values, string directory)
{
    private string FilePath(Guid id) => Path.Combine(directory, id.ToString("N") + ".json");

    public TweakOperation Preview(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count) throw new InvalidOperationException("Selecciona ajustes distintos.");
        var definitions = ids.Select(ExecutableTweaks.Get).ToArray();
        if (definitions.Where(t => t.ExclusiveGroup is not null).GroupBy(t => t.ExclusiveGroup).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Hay ajustes incompatibles seleccionados para la misma función.");
        var changes = definitions.SelectMany(t => t.Targets.Select(v =>
        {
            var before = values.Read(v);
            return new TweakValueChange(t.Id, v.Id, before, ExecutableTweaks.DesiredFor(v, before));
        })).ToArray();
        var operation = new TweakOperation(1, Guid.NewGuid(), DateTimeOffset.UtcNow, null, changes, "Preparado");
        Validate(operation);
        return operation;
    }

    public void SavePrepared(TweakOperation operation)
    {
        Validate(operation);
        if (operation.State != "Preparado" || File.Exists(FilePath(operation.Id)))
            throw new InvalidOperationException("La operación ya se registró o no está preparada.");
        LocalJsonFile.Write(FilePath(operation.Id), operation);
    }

    public TweakOperation Read(Guid id)
    {
        var operation = LocalJsonFile.Read<TweakOperation>(FilePath(id))
            ?? throw new InvalidDataException("Respaldo de ajustes vacío.");
        Validate(operation);
        if (operation.Id != id) throw new InvalidDataException("El respaldo no coincide con la operación.");
        return operation;
    }

    public IReadOnlyList<TweakOperation> List()
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.json").Select(path =>
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id))
                throw new InvalidDataException("Hay un respaldo de ajustes no reconocido; no se descartó.");
            return Read(id);
        }).OrderByDescending(o => o.CreatedAtUtc).ToArray();
    }

    public TweakOperation PreviewUndo(Guid id)
    {
        var original = Read(id);
        if (original.UndoOf is not null) throw new InvalidOperationException("Selecciona una aplicación de ajustes, no una reversión.");
        if (original.State is "Preparado" or "Cancelado antes de escribir")
            throw new InvalidOperationException("Esta operación no inició escrituras. No se revertirán valores de otra operación.");
        // A failed or interrupted batch may have written some values. Restore only observed matching values.
        var changes = new List<TweakValueChange>();
        foreach (var change in original.Changes)
        {
            if (change.Before == change.After) continue;
            var current = values.Read(ExecutableTweaks.Target(change.TweakId, change.TargetId));
            if (current == change.Before) continue;
            if (current != change.After) throw new InvalidOperationException("Conflicto: un valor cambió fuera de esta operación. No se sobrescribirá. Revisa Windows y el respaldo.");
            changes.Add(change with { Before = current, After = change.Before, State = "Preparado" });
        }
        if (changes.Count == 0) throw new InvalidOperationException("Los valores ya coinciden con el respaldo anterior. No hay cambios que revertir.");
        var undo = new TweakOperation(1, Guid.NewGuid(), DateTimeOffset.UtcNow, id, changes, "Preparado");
        Validate(undo);
        return undo;
    }

    public void CancelPrepared(Guid id)
    {
        var operation = Read(id);
        if (operation.State != "Preparado") throw new InvalidOperationException("La operación ya inició escrituras; revisa sus valores antes de revertir.");
        Save(operation with { State = "Cancelado antes de escribir" });
    }

    public TweakOperation Apply(Guid id)
    {
        var operation = Read(id);
        if (operation.State != "Preparado") throw new InvalidOperationException("La operación no está preparada. No se reintentará un lote incompleto automáticamente.");
        // Check the entire batch before the first mutation; recheck each value immediately before writing.
        foreach (var change in operation.Changes) CheckUnchanged(change);
        operation = operation with { State = "En ejecución" };
        Save(operation);
        try
        {
            var machineIndices = operation.Changes.Select((change, index) => (change, index))
                .Where(pair => pair.change.Before != pair.change.After && ExecutableTweaks.Target(pair.change.TweakId, pair.change.TargetId).Machine)
                .Select(pair => pair.index).ToArray();
            var machineBatchStarted = false;
            for (var index = 0; index < operation.Changes.Count; index++)
            {
                if (machineBatchStarted && machineIndices.Contains(index)) continue;
                var change = operation.Changes[index];
                CheckUnchanged(change);
                if (change.Before == change.After)
                {
                    operation = Update(operation, index, "Valor ya presente; sin escritura");
                    Save(operation);
                    continue;
                }
                var target = ExecutableTweaks.Target(change.TweakId, change.TargetId);
                if (target.Machine && values is IBatchTweakValueStore batch)
                {
                    if (machineBatchStarted) continue;
                    machineBatchStarted = true;
                    foreach (var machineIndex in machineIndices)
                    {
                        CheckUnchanged(operation.Changes[machineIndex]);
                        operation = Update(operation, machineIndex, "Lote elevado preparado; comprobar valor si se interrumpe");
                    }
                    Save(operation); // All originals are durable before the single UAC request.
                    batch.WriteMachineBatch(machineIndices.Select(i => operation.Changes[i]).ToArray());
                    foreach (var machineIndex in machineIndices)
                    {
                        var machineChange = operation.Changes[machineIndex];
                        if (values.Read(ExecutableTweaks.Target(machineChange.TweakId, machineChange.TargetId)) != machineChange.After)
                            throw new IOException("El registro no confirmó un valor del lote elevado.");
                        operation = Update(operation, machineIndex, "Valor de registro verificado");
                        Save(operation);
                    }
                    continue;
                }
                operation = Update(operation, index, "Escritura iniciada; comprobar valor si se interrumpe");
                Save(operation); // Recovery data must be durable before each write.
                values.Write(target, change.Before, change.After);
                if (values.Read(target) != change.After) throw new IOException("El registro no confirmó el valor solicitado.");
                operation = Update(operation, index, "Valor de registro verificado");
                Save(operation);
            }
            operation = operation with { State = "Valores de registro verificados" };
            Save(operation);
            return operation;
        }
        catch
        {
            // Preserve the write-ahead record if an outcome cannot be saved. Do not hide the primary failure.
            try { Save(operation with { State = "Lote incompleto; revisar o revertir" }); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private void CheckUnchanged(TweakValueChange change)
    {
        if (values.Read(ExecutableTweaks.Target(change.TweakId, change.TargetId)) != change.Before)
            throw new InvalidOperationException("La configuración cambió desde la revisión. No se sobrescribirá; vuelve a analizar.");
    }
    private static TweakOperation Update(TweakOperation operation, int index, string state) => operation with
    {
        Changes = operation.Changes.Select((c, i) => i == index ? c with { State = state } : c).ToArray()
    };
    private void Save(TweakOperation operation) => LocalJsonFile.Write(FilePath(operation.Id), operation);

    private void Validate(TweakOperation operation)
    {
        if (operation.SchemaVersion != 1 || operation.Id == Guid.Empty || operation.CreatedAtUtc == default
            || operation.Changes is null || operation.Changes.Count is < 1 or > 256
            || operation.Changes.Any(c => c is null) || operation.Changes.Select(c => c.TargetId).Distinct().Count() != operation.Changes.Count)
            throw new InvalidDataException("Formato de respaldo de ajustes no admitido.");
        var source = operation.UndoOf is { } sourceId
            ? ReadOriginal(sourceId, operation.Id) : null;
        foreach (var change in operation.Changes)
        {
            var target = ExecutableTweaks.Target(change.TweakId, change.TargetId);
            ValidateValue(change.Before, target);
            ValidateValue(change.After, target);
            if (source is null && change.After != ExecutableTweaks.DesiredFor(target, change.Before))
                throw new InvalidDataException("El valor solicitado no pertenece al catálogo ejecutable.");
            if (source is not null && !source.Changes.Any(c => c.TweakId == change.TweakId && c.TargetId == change.TargetId
                && c.Before == change.After && c.After == change.Before))
                throw new InvalidDataException("La reversión no coincide con el respaldo original.");
        }
    }
    private TweakOperation ReadOriginal(Guid sourceId, Guid undoId)
    {
        if (sourceId == undoId) throw new InvalidDataException("Referencia circular en el respaldo.");
        var source = LocalJsonFile.Read<TweakOperation>(FilePath(sourceId))
            ?? throw new InvalidDataException("Falta el respaldo original.");
        if (source.UndoOf is not null || source.Id != sourceId)
            throw new InvalidDataException("El original no es una operación de aplicación.");
        Validate(source);
        return source;
    }
    private static void ValidateValue(RegistryValueSnapshot value, RegistryTweakTarget target)
    {
        if (value is null || (!value.Exists && value != RegistryValueSnapshot.Missing)
            || (value.Exists && (value.Kind != target.Desired.Kind || (value.Kind == "DWord"
                ? !uint.TryParse(value.Data, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _)
                : !ValidString(value, target)))))
            throw new InvalidDataException("Tipo o contenido del valor no compatible; no se modificará.");
    }
    private static bool ValidString(RegistryValueSnapshot value, RegistryTweakTarget target) =>
        target.ClearBits is not null || target.SetBits is not null || target.Id == "numlock"
            ? uint.TryParse(value.Data, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _)
            : target.Id.StartsWith("mouse-", StringComparison.Ordinal)
            ? uint.TryParse(value.Data, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number <= 32767
            : value.Data is "0" or "1";
}
