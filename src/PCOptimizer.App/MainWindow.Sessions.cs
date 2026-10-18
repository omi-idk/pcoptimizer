using System.IO;
using System.Windows;
using PCOptimizer.Core;

namespace PCOptimizer.App;

public partial class MainWindow
{
    private readonly IPowerSessionPlatform sessionPlatform = new WindowsPowerSessionPlatform();
    private readonly GamingPowerSessionEngine sessionEngine = new(new WindowsPowerSessionPlatform(), Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCOptimizer", "GamingSessions"));
    private GamingSessionsWindow? sessionsWindow;
    private void NotifyPendingSessions()
    {
        try
        {
            var count = sessionEngine.ListPending().Count;
            if (count > 0) model.Status = $"Hay {count} sesión(es) gaming pendientes. Abre Energía → Sesiones gaming para revisar y recuperar. No se restaura nada sin comprobar el estado.";
        }
        catch (Exception ex) { model.Status = "No se pudieron leer sesiones gaming; los registros se conservaron: " + ex.Message; }
    }
    private void OpenGamingSessions(object sender, RoutedEventArgs e)
    {
        if (sessionsWindow is not null) { sessionsWindow.Activate(); return; }
        if (!model.IsIdle) return;
        sessionsWindow = new(sessionEngine, sessionPlatform,
            () => { if (!model.IsIdle) return false; model.IsIdle = false; return true; },
            () => model.IsIdle = true,
            async (session, restoring) =>
            {
                var before = restoring ? session.TemporaryPlan : session.PreviousPlan;
                var after = restoring ? session.PreviousPlan : session.TemporaryPlan;
                var recovery = await PrepareRecoveryAsync([new RecoveryAction(Guid.NewGuid(), RecoveryActionKind.PowerPlan,
                    $"Sesión {session.Id:N} · {session.Game.Name} · {(restoring ? "restaurar" : "aplicar")}", after.ToString("D"), before.ToString("D"), after.ToString("D"),
                    "Intención registrada; resultado y estado previo verificado en el diario GamingSessions de esta sesión")], reviewOwner: sessionsWindow);
                return recovery is not null;
            })
        { Owner = this };
        sessionsWindow.Closed += (_, _) => { sessionsWindow = null; NotifyPendingSessions(); };
        sessionsWindow.Show();
    }
}
