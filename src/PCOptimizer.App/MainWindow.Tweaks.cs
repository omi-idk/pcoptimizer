using System.IO;
using System.Windows;
using System.Windows.Controls;
using PCOptimizer.Core;

namespace PCOptimizer.App;

public partial class MainWindow
{
    private readonly WindowsTweakValueStore tweakValues = new();
    private readonly TweakEngine tweakEngine = new(new WindowsTweakValueStore(), Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCOptimizer", "Tweaks"));

    private void ChooseTweakProfile(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || ((FrameworkElement)sender).Tag is not string id) return;
        var selected = id == "clear" ? [] : ExecutableTweaks.Profile(id);
        foreach (var choice in model.ExecutableChoices) choice.IsSelected = selected.Contains(choice.Id);
        model.SelectedProfile = id switch
        {
            "minimal" => "Mínimo · activar Modo Juego",
            "recommended" => "Recomendado · Modo Juego y extensiones visibles",
            "gaming" => "Gaming · Modo Juego, sin capturas y sin aceleración del puntero",
            "intensive" => "Intensivo · Gaming y menos efectos del escritorio",
            _ => "Personalizado · sin ajustes seleccionados"
        };
        model.Status = "Perfil seleccionado. Aún no se modificó Windows: revisa los ajustes y pulsa Revisar y aplicar.";
    }

    private async void AnalyzeTweaks(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        model.IsIdle = false;
        try { await ReadTweaksAsync(); model.Status = "Valores actuales del registro leídos. Esto no mide FPS ni confirma el efecto después de reiniciar."; }
        finally { model.IsIdle = true; }
    }

    private async Task ReadTweaksAsync()
    {
        foreach (var choice in model.ExecutableChoices)
        {
            try
            {
                var snapshot = await Task.Run(() => tweakEngine.Preview([choice.Id]));
                choice.State = snapshot.Changes.All(c => c.Before == c.After) ? "VALORES SOLICITADOS PRESENTES" : "DISPONIBLE PARA APLICAR";
                choice.Values = string.Join("\n", snapshot.Changes.Select(c => $"{c.TargetId}: {c.Before.Display}"));
            }
            catch (Exception ex) { choice.State = "NO DISPONIBLE"; choice.Values = ex.Message; }
        }
        RefreshTweakOperations();
    }

    private void RefreshTweakOperations(Guid? prefer = null)
    {
        var previous = prefer ?? model.SelectedTweakOperation?.Id;
        model.TweakOperations.Clear();
        try
        {
            foreach (var operation in tweakEngine.List().Where(o => o.UndoOf is null && o.State is not ("Preparado" or "Cancelado antes de escribir"))) model.TweakOperations.Add(operation);
            model.SelectedTweakOperation = model.TweakOperations.FirstOrDefault(o => o.Id == previous) ?? model.TweakOperations.FirstOrDefault();
        }
        catch (Exception ex) { model.SelectedTweakOperation = null; model.Status = "No se pudo leer el respaldo de ajustes; se conservaron los archivos: " + ex.Message; }
    }

    private async void ApplySelectedTweaks(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        var ids = model.ExecutableChoices.Where(t => t.IsSelected).Select(t => t.Id).ToArray();
        if (ids.Length == 0) { model.Status = "Elige un perfil o selecciona al menos un ajuste."; return; }
        await ReviewAndRunTweaksAsync(() => tweakEngine.Preview(ids), undo: false);
    }

    private async void UndoSelectedTweaks(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || model.SelectedTweakOperation is not { } selected) return;
        await ReviewAndRunTweaksAsync(() => tweakEngine.PreviewUndo(selected.Id), undo: true);
    }

    private async void ApplyUpdatePolicy(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || ((FrameworkElement)sender).Tag is not string id) return;
        if (id is not ("update-feature-delay" or "update-driver-exclusion")) return;
        await ReviewAndRunTweaksAsync(() => tweakEngine.Preview([id]), undo: false);
    }

    private async void ApplyPersonalization(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle || ((FrameworkElement)sender).Tag is not string id) return;
        if (id is not ("taskbar-left" or "dark-theme" or "task-view")) return;
        await ReviewAndRunTweaksAsync(() => tweakEngine.Preview([id]), undo: false);
    }

    private async Task ReviewAndRunTweaksAsync(Func<TweakOperation> preview, bool undo)
    {
        model.IsIdle = false;
        TweakOperation? operation = null;
        RecoverySession? recovery = null;
        try
        {
            // Preserve corrupt/inaccessible records instead of silently replacing recovery history.
            _ = tweakEngine.List();
            operation = await Task.Run(preview);
            if (operation.Changes.All(c => c.Before == c.After))
            { model.Status = "Los valores solicitados ya están presentes. No se escribió el registro."; return; }
            var choices = operation.Changes.GroupBy(c => c.TweakId).Select(group =>
            {
                var definition = ExecutableTweaks.Get(group.Key);
                var values = string.Join("\n", group.Select(c =>
                {
                    var target = ExecutableTweaks.Target(c.TweakId, c.TargetId);
                    return $"{(target.Machine ? "HKLM" : "HKCU")}\\{target.Path}\\{target.Name}\n{c.Before.Display} → {c.After.Display}";
                }));
                return new SoftwareChoice(definition.Id, definition.Name, definition.Benefit + "\n\n" + values, notes: $"{definition.Risk}\n{definition.Activation}");
            }).ToArray();
            var review = new SelectionReviewWindow(undo ? "Revisar reversión" : "Revisar ajustes de Windows", choices,
                "Se escribirán exclusivamente estos valores. Se guardan valores y tipos anteriores; si un valor no existía, la reversión lo eliminará sin borrar la clave. Los ajustes del equipo requieren UAC. No se reinicia Windows automáticamente. La verificación confirma el registro, no los FPS ni la activación efectiva de la función. " + RecoveryPreferenceText, "ajustes") { Owner = this };
            if (review.ShowDialog() != true) { model.Status = "Operación cancelada. No se modificó el registro."; return; }
            tweakEngine.SavePrepared(operation);
            var actions = operation.Changes.Select(c => new RecoveryAction(Guid.NewGuid(), RecoveryActionKind.RegistryTweak,
                (undo ? "Revertir: " : "Aplicar: ") + ExecutableTweaks.Get(c.TweakId).Name,
                $"{operation.Id:N}/{c.TweakId}/{c.TargetId}", c.Before.Display, c.After.Display)).ToArray();
            recovery = await PrepareRecoveryAsync(actions);
            if (recovery is null) { tweakEngine.CancelPrepared(operation.Id); return; }
            foreach (var action in actions) recoveryStore.SetActionState(recovery.Id, action.Id, "En ejecución; consultar respaldo de valores");
            model.Status = "Aplicando ajustes y verificando los valores del registro… Revisa UAC si seleccionaste ajustes del equipo.";
            var result = await Task.Run(() => tweakEngine.Apply(operation.Id));
            foreach (var action in actions) recoveryStore.SetActionState(recovery.Id, action.Id, "Valor de registro verificado; revisar activación/reinicio indicado en la ficha");
            RefreshTweakOperations(result.Id);
            model.Status = undo
                ? "Valores anteriores restaurados y leídos de nuevo. Revisa si el ajuste requiere cerrar sesión o reiniciar."
                : "Valores de registro aplicados y verificados. Revisa las indicaciones de activación; no se ha reiniciado Windows.";
        }
        catch (Exception ex)
        {
            model.Status = "Operación detenida: " + ex.Message + " Si hubo escrituras, el respaldo permite revisarlas y solicitar reversión.";
            if (recovery is not null)
            {
                try
                {
                    foreach (var action in recovery.Actions)
                        recoveryStore.SetActionState(recovery.Id, action.Id, "Lote detenido; consultar estado por valor en el respaldo de ajustes");
                }
                catch (Exception journalError) { model.Status += " No se pudo actualizar el registro: " + journalError.Message; }
            }
        }
        finally
        {
            var finalStatus = model.Status;
            await ReadTweaksAsync();
            RefreshRecoverySessions(recovery?.Id);
            if (operation is not null) RefreshTweakOperations(operation.Id);
            model.Status = finalStatus;
            model.IsIdle = true;
        }
    }

    private void GoToSection(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string target) SelectNavigation(this, target);
    }

    private static bool SelectNavigation(DependencyObject parent, string target)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is RadioButton button && Equals(button.Tag, target)) { button.IsChecked = true; return true; }
            if (SelectNavigation(child, target)) return true;
        }
        return false;
    }
}
