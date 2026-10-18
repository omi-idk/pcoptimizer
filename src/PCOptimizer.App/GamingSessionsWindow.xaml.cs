using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using PCOptimizer.Core;

namespace PCOptimizer.App;

public partial class GamingSessionsWindow : Window
{
    private readonly GamingPowerSessionEngine engine;
    private readonly IPowerSessionPlatform platform;
    private readonly Func<bool> acquire;
    private readonly Action release;
    private readonly Func<GamingPowerSession, bool, Task<bool>> prepareRecovery;
    private readonly CancellationTokenSource lifetime = new();
    private bool busy;
    private bool visualPreview;
    private Guid? monitored;
    private CancellationTokenSource? watchCancellation;
    public bool ControlsActiveSession => monitored.HasValue || busy;
    internal GamingSessionsWindow(GamingPowerSessionEngine engine, IPowerSessionPlatform platform, Func<bool> acquire,
        Action release, Func<GamingPowerSession, bool, Task<bool>> prepareRecovery)
    {
        InitializeComponent(); this.engine = engine; this.platform = platform; this.acquire = acquire;
        this.release = release; this.prepareRecovery = prepareRecovery;
        Loaded += async (_, _) => { if (!visualPreview) await RunAsync(RefreshAsync); };
        Closing += (_, e) =>
        {
            if (ControlsActiveSession) { e.Cancel = true; Status.Text = "Restaura la sesión activa antes de cerrar. Si hay otra operación, espera a que termine."; }
            else { watchCancellation?.Cancel(); lifetime.Cancel(); }
        };
        Closed += (_, _) => lifetime.Dispose();
    }
    internal void ConfigureDesignPreview(GameIdentity game, PowerPlan plan, GamingPowerSession session)
    {
        visualPreview = true;
        Games.ItemsSource = new[] { game }; Games.SelectedIndex = 0;
        Plans.ItemsSource = new[] { plan }; Plans.SelectedIndex = 0;
        Pending.ItemsSource = new[] { session }; Pending.SelectedIndex = 0;
        Status.Text = "VISTA DE DISEÑO · Datos ilustrativos; no se consultó ni cambió el equipo.";
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy || !acquire()) { Status.Text = "Espera a que termine la operación o benchmark en la ventana principal."; return; }
        busy = true; ControlsPanel.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) { Status.Text = "No se completó: " + ex.Message + " Revisa las sesiones pendientes antes de iniciar otra."; ReloadPending(); }
        finally { busy = false; ControlsPanel.IsEnabled = true; release(); }
    }
    private void ReloadPending()
    {
        try { Pending.ItemsSource = engine.ListPending(); Pending.SelectedIndex = 0; }
        catch (Exception ex) { Status.Text = "Registros inaccesibles o corruptos; se conservaron: " + ex.Message; }
    }
    private async Task RefreshAsync()
    {
        var plans = await platform.PlansAsync(); Plans.ItemsSource = plans.Where(p => !p.IsActive).ToArray(); Plans.SelectedIndex = 0;
        var games = await Task.Run(() =>
        {
            var list = new List<GameIdentity>();
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.Id != Environment.ProcessId && !p.HasExited && p.MainWindowHandle != IntPtr.Zero)
                            list.Add(new(p.Id, new DateTimeOffset(p.StartTime.ToUniversalTime()), $"{p.ProcessName} · PID {p.Id}"));
                    }
                    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
                }
            }
            return list.OrderBy(g => g.Name).ToArray();
        });
        Games.ItemsSource = games; Games.SelectedIndex = 0; ReloadPending();
        Status.Text = plans.Count(p => !p.IsActive) == 0
            ? "Windows no ofrece un plan alternativo. No se crean esquemas ni se fuerzan opciones no soportadas; puedes consultar y recuperar sesiones pendientes."
            : "Selecciona un juego abierto o espera una ruta concreta. No hay cambios hasta confirmar; no se fuerza acceso a procesos protegidos.";
    }
    private async void Refresh(object sender, RoutedEventArgs e) => await RunAsync(RefreshAsync);
    private async void StartSession(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (Games.SelectedItem is not GameIdentity game || Plans.SelectedItem is not PowerPlan plan)
            throw new InvalidOperationException("Selecciona proceso y plan.");
        if (watchCancellation is not null) throw new InvalidOperationException("Detén primero la espera de ejecutable.");
        await BeginSessionAsync(game, plan, true);
    });
    private async Task BeginSessionAsync(GameIdentity game, PowerPlan plan, bool review)
    {
        if (monitored.HasValue) throw new InvalidOperationException("Ya hay una sesión supervisada.");
        if (review && MessageBox.Show(this, $"Juego: {game.Name}\nPlan temporal: {plan.Name}\n\nSe guardará el plan original antes de escribir. Al salir se intentará restaurarlo y puede pedir permisos/punto de Windows. Un conflicto externo detiene la restauración. No hay garantía de FPS. ¿Preparar?",
            "Revisar sesión", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        var session = await engine.PrepareAsync(game, plan.Id);
        if (!await prepareRecovery(session, false))
        { engine.CancelPrepared(session.Id); ReloadPending(); return; }
        session = await engine.ApplyAsync(session.Id);
        monitored = session.Id; ReloadPending();
        Status.Text = "Plan temporal verificado. Supervisando salida del juego; puedes restaurar manualmente antes. No se modifica el juego.";
        _ = MonitorAsync(session);
    }
    private void StopWatching(object sender, RoutedEventArgs e)
    {
        watchCancellation?.Cancel();
        Status.Text = "Se solicitó detener la espera. No se cambia el plan por detener una espera.";
    }
    private void WatchExecutable(object sender, RoutedEventArgs e)
    {
        if (busy || monitored.HasValue || watchCancellation is not null) { Status.Text = "Termina primero la sesión o espera actual."; return; }
        if (Plans.SelectedItem is not PowerPlan plan) { Status.Text = "Selecciona un plan temporal existente."; return; }
        var dialog = new OpenFileDialog { Filter = "Ejecutable del juego (*.exe)|*.exe", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        var path = Path.GetFullPath(dialog.FileName);
        if (MessageBox.Show(this, $"Esperar: {path}\nPlan temporal: {plan.Name}\n\nNo se iniciará el ejecutable. Al detectar esa ruta se preparará el respaldo y el cambio de plan; puede aparecer UAC. La espera solo dura mientras esta ventana esté abierta y no se reactiva automáticamente después de reiniciar. ¿Activar una vez?",
            "Detección voluntaria", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); watchCancellation = cancellation;
        Status.Text = "Esperando inicio: " + Path.GetFileName(path) + ". No se ha cambiado el plan.";
        _ = WaitForExecutableAsync(path, plan, cancellation);
    }
    private async Task WaitForExecutableAsync(string path, PowerPlan plan, CancellationTokenSource cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                GameIdentity? found = null;
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
                {
                    using (process)
                    {
                        try
                        {
                            var actual = WindowsProcessPath.Read(process.Id);
                            if (actual is not null && string.Equals(Path.GetFullPath(actual), path, StringComparison.OrdinalIgnoreCase) && !process.HasExited)
                                found = new(process.Id, new DateTimeOffset(process.StartTime.ToUniversalTime()), $"{process.ProcessName} · PID {process.Id}");
                        }
                        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or ArgumentException) { }
                    }
                }
                if (found is not null && !busy)
                {
                    var entered = false;
                    await RunAsync(async () =>
                    {
                        cancellation.Token.ThrowIfCancellationRequested(); entered = true;
                        await BeginSessionAsync(found, plan, false);
                    });
                    if (entered) return; // One detection, never repeat permission prompts after cancellation/failure.
                }
                await Task.Delay(2000, cancellation.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Status.Text = "Espera detenida: " + ex.Message; }
        finally
        {
            if (watchCancellation == cancellation) watchCancellation = null;
            cancellation.Dispose();
        }
    }
    private async Task MonitorAsync(GamingPowerSession session)
    {
        try
        {
            // Low-rate polling avoids a service and handles inaccessible/reused PIDs conservatively.
            while (!lifetime.IsCancellationRequested && monitored == session.Id && await platform.IsGameRunningAsync(session.Game))
                await Task.Delay(2000, lifetime.Token);
            while (!lifetime.IsCancellationRequested && monitored == session.Id)
            {
                if (busy) { await Task.Delay(2000, lifetime.Token); continue; }
                var entered = false;
                await RunAsync(async () => { entered = true; await RestoreAsync(session.Id); });
                if (entered) break; // Failure/cancellation remains explicit and recoverable; never loop UAC prompts.
                await Task.Delay(2000, lifetime.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Status.Text = "La supervisión se detuvo: " + ex.Message + ". Usa restauración manual."; }
    }
    private async Task RestoreAsync(Guid id)
    {
        var session = engine.Read(id);
        if (await engine.NeedsRestoreWriteAsync(id) && !await prepareRecovery(session, true))
        { Status.Text = "Restauración cancelada; el registro sigue pendiente."; return; }
        await engine.RestoreAsync(id);
        if (monitored == id) monitored = null;
        ReloadPending(); Status.Text = "Plan original verificado; sesión restaurada. No se tocó la memoria del juego.";
    }
    private async void AcceptExternal(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (Pending.SelectedItem is not GamingPowerSession selected) return;
        if (MessageBox.Show(this, "Esta acción solo cierra un registro en conflicto y conserva el plan actual. No restaura el anterior ni cambia opciones. ¿Reconocer el cambio externo y cerrar el registro?",
            "Conservar cambio externo", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        await engine.AcceptExternalPlanAsync(selected.Id);
        if (monitored == selected.Id) monitored = null;
        ReloadPending(); Status.Text = "Cambio externo reconocido y conservado. No se escribió ningún plan.";
    });
    private void LeavePending(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (MessageBox.Show(this, "El plan actual se conservará y NO se restaurará al salir del juego. El registro permanecerá pendiente para recuperarlo desde esta ventana. ¿Detener la supervisión y dejar pendiente?",
            "Sesión sin restaurar", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        monitored = null; Status.Text = "Supervisión detenida por decisión del usuario. El plan no se restauró; el registro sigue pendiente.";
    }
    private async void RestoreSelected(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (Pending.SelectedItem is not GamingPowerSession selected) return;
        if (MessageBox.Show(this, $"Restaurar {selected.Game.Name}\nPlan original: {selected.PreviousPlan}\nSe comprobará el plan actual y se rechazarán conflictos. ¿Continuar?",
            "Revisar restauración", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) await RestoreAsync(selected.Id);
    });
}
