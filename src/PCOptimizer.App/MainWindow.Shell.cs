using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using PCOptimizer.Core;

namespace PCOptimizer.App;

public partial class MainWindow
{
    private bool visualPreview;
    private void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void MaximizeWindow(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void FocusSearch(object sender, ExecutedRoutedEventArgs e)
    {
        if (section == "Instalación rápida") { SoftwareSearchBox.Focus(); SoftwareSearchBox.SelectAll(); }
        else if (section == "Optimización") { TweakSearchBox.Focus(); TweakSearchBox.SelectAll(); }
    }
    private void OpenTweakCategory(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string category) return;
        SelectNavigation(this, "Optimización");
        model.ExecutableCategory = category;
    }
    private void SelectVisibleTweaks(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        model.SelectVisibleStandard();
        model.Status = "Seleccionados los ajustes estándar compatibles del filtro actual. Los avanzados requieren selección individual.";
    }
    private void OpenTweakSource(object sender, RoutedEventArgs e)
    {
        if (model.SelectedExecutableTweak is not { } choice) return;
        try
        {
            var source = ExecutableTweaks.Get(choice.Id).Source;
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Fuente fuera de las direcciones HTTPS permitidas.");
            using var process = Process.Start(new ProcessStartInfo(source) { UseShellExecute = true });
        }
        catch (Exception ex) { model.Status = "No se pudo abrir la fuente: " + ex.Message; }
    }

    private void SaveTweakProfile(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        var ids = model.ExecutableChoices.Where(t => t.IsSelected).Select(t => t.Id).ToArray();
        if (ids.Length == 0) { model.Status = "Selecciona ajustes antes de guardar un perfil."; return; }
        var dialog = new SaveFileDialog { Filter = "Perfil de PCOptimizer (*.pcoprofile.json)|*.pcoprofile.json", DefaultExt = ".pcoprofile.json", FileName = "mi-perfil.pcoprofile.json", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            TweakProfiles.Save(dialog.FileName, new SavedTweakProfile(1, "Perfil personalizado", ids));
            model.Status = "Perfil guardado. Contiene identificadores de ajustes; no incluye respaldos ni valores privados del equipo.";
        }
        catch (Exception ex) { model.Status = "No se pudo guardar el perfil: " + ex.Message; }
    }
    private void LoadTweakProfile(object sender, RoutedEventArgs e)
    {
        if (!model.IsIdle) return;
        var dialog = new OpenFileDialog { Filter = "Perfil de PCOptimizer (*.pcoprofile.json)|*.pcoprofile.json", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        try { model.LoadProfile(TweakProfiles.Read(dialog.FileName)); model.Status = model.ProfileNote; }
        catch (Exception ex) { model.Status = "No se cargó el perfil: " + ex.Message; }
    }

    // Called only by the separate, non-shipping design renderer. No diagnostics or mutations run.
    internal void ConfigureDesignPreview()
    {
        visualPreview = true;
        IsHitTestVisible = false;
        PreviewLabel.Visibility = Visibility.Visible;
        inventory = new Inventory("AMD Ryzen 7 7800X3D", "NVIDIA GeForce RTX 4070 SUPER", 32L * 1024 * 1024 * 1024,
            "Windows 11 Pro · datos ilustrativos", [], "MSI MAG B650 TOMAHAWK WIFI", "Ejemplo de diseño");
        foreach (var choice in model.ExecutableChoices)
        {
            try
            {
                var changes = choice.Definition.Targets.Select(target =>
                {
                    var before = (target.ClearBits is not null || target.SetBits is not null) ? new RegistryValueSnapshot(true, "String", "510") : RegistryValueSnapshot.Missing;
                    return new TweakValueChange(choice.Id, target.Id, before, ExecutableTweaks.DesiredFor(target, before));
                }).ToArray();
                choice.SetSnapshot(new TweakOperation(1, Guid.NewGuid(), DateTimeOffset.UtcNow, null, changes, "Vista previa"));
            }
            catch (Exception ex) { choice.SetUnavailable(ex.Message); }
        }
        model.IsIdle = true;
        model.Status = "Vista previa de diseño con datos ilustrativos. No se consultó ni modificó Windows.";
        Render();
    }
    internal void ShowDesignSection(string name)
    {
        SelectNavigation(this, name);
        if (name == "Optimización") model.SelectProfile("gaming");
    }
}
