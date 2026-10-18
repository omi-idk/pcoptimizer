using System.Diagnostics;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal sealed class WindowsPowerSessionPlatform : IPowerSessionPlatform
{
    public Task<IReadOnlyList<PowerPlan>> PlansAsync() => WindowsCommands.GetPlansAsync();
    public Task ActivateAsync(Guid plan) => WindowsCommands.ActivateAsync(plan);
    public Task<bool> IsGameRunningAsync(GameIdentity game)
    {
        try
        {
            using var process = Process.GetProcessById(game.ProcessId);
            return Task.FromResult(!process.HasExited && process.StartTime.ToUniversalTime() == game.StartedUtc.UtcDateTime);
        }
        catch (System.ComponentModel.Win32Exception ex)
        { throw new UnauthorizedAccessException("No se pudo comprobar la identidad del proceso; no se interpretará como salida ni se cambiará energía automáticamente.", ex); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Task.FromResult(false); }
    }
}
