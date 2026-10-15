using System.Windows;
using System.Security.Principal;

namespace PCOptimizer.App;

public partial class App : Application
{
    private Mutex? instanceLock;

    protected override void OnStartup(StartupEventArgs e)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            MessageBox.Show("Abre PCOptimizer como usuario normal. Solo las acciones que lo necesitan pedirán permisos de administrador.", "PCOptimizer");
            Shutdown();
            return;
        }
        if (identity.User is null)
        {
            MessageBox.Show("No se pudo identificar la cuenta de Windows. No se iniciaron operaciones.", "PCOptimizer");
            Shutdown();
            return;
        }
        instanceLock = new Mutex(true, @"Global\PCOptimizer.Desktop." + identity.User.Value, out var created);
        if (!created)
        {
            MessageBox.Show("PCOptimizer ya está abierto para esta cuenta de Windows.", "PCOptimizer");
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instanceLock?.Dispose();
        base.OnExit(e);
    }
}
