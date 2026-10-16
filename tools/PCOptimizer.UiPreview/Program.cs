using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Text;
using PCOptimizer.App;
using PCOptimizer.Core;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Specify the screenshot output directory.");
        var directory = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(directory);
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PCOptimizer.App;component/Themes/DesignSystem.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PCOptimizer.App;component/Themes/SharedTemplates.xaml", UriKind.Relative) });
        var window = new MainWindow();
        window.ConfigureDesignPreview();
        _ = window.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await ValidateElevatedScriptSyntax();
                foreach (var (name, section, width, height) in new[]
                {
                    ("overview", "Inicio", 1400, 880), ("tweaks", "Optimización", 1400, 880),
                    ("tweaks-compact", "Optimización", 1120, 740), ("applications", "Instalación rápida", 1400, 880),
                    ("recovery", "Respaldo y recuperación", 1400, 880)
                })
                {
                    window.Width = width;
                    window.Height = height;
                    window.ShowDesignSection(section);
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    await Task.Delay(150);
                    // Keep the design window unshown: hosted runner HWNDs are clipped to a
                    // small virtual display. Render its real WPF content without creating a native surface.
                    var visual = (FrameworkElement)window.Content;
                    visual.DataContext = window.DataContext;
                    visual.SetValue(TextElement.ForegroundProperty, window.Foreground);
                    visual.SetValue(TextElement.FontFamilyProperty, window.FontFamily);
                    visual.SetValue(TextElement.FontSizeProperty, window.FontSize);
                    window.Content = null; // Remove the HWND layout clip while preserving real resources and bindings.
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if (VisualTreeHelper.GetParent(visual) is not null)
                        throw new InvalidOperationException("The design content was not detached from its native window.");
                    visual.Clip = null;
                    visual.Visibility = Visibility.Visible;
                    visual.InvalidateMeasure();
                    visual.InvalidateArrange();
                    visual.Width = width;
                    visual.Height = height;
                    visual.Measure(new Size(width, height));
                    visual.Arrange(new Rect(0, 0, width, height));
                    visual.UpdateLayout();
                    visual.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(directory, name + ".png"));
                    encoder.Save(output);
                    if (section == "Optimización") VerifyTweakColumns(visual);
                    Console.WriteLine($"Rendered {name}: {width}x{height}");
                    window.Content = visual;
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { window.Close(); app.Shutdown(); Dispatcher.ExitAllFrames(); }
        });
        Dispatcher.Run();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T value) yield return value;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void VerifyTweakColumns(FrameworkElement root)
    {
        var list = Descendants<ListBox>(root).Single(l => System.Windows.Automation.AutomationProperties.GetName(l) == "Ajustes disponibles");
        if (list.ItemContainerGenerator.ContainerFromIndex(0) is not ListBoxItem row || list.Items[0] is not ExecutableTweakChoice choice)
            throw new InvalidOperationException("The first tweak row was not rendered.");
        foreach (var text in new[] { choice.Category, choice.State })
        {
            var block = Descendants<TextBlock>(row).First(t => t.Text == text);
            var bounds = block.TransformToAncestor(list).TransformBounds(new Rect(block.RenderSize));
            if (bounds.Width < 1 || bounds.Right > list.ActualWidth + 1)
                throw new InvalidOperationException($"The {text} column is clipped: {bounds.Right} > {list.ActualWidth}.");
        }
    }

    private static async Task ValidateElevatedScriptSyntax()
    {
        var machine = ExecutableTweaks.Items.Where(t => t.Targets.Any(v => v.Machine))
            .GroupBy(t => t.ExclusiveGroup ?? t.Id).Select(g => g.First()).ToArray();
        foreach (var restore in new[] { false, true })
        {
            var requests = machine.SelectMany(t => t.Targets.Select(v => new TweakValueChange(t.Id, v.Id,
                new(true, "DWord", "4294967295"), restore ? RegistryValueSnapshot.Missing : v.Desired))).ToArray();
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(MachineTweakScript.Build(requests)));
            var parser = $$"""
                [Console]::OutputEncoding = [Text.Encoding]::UTF8
                $tokens = $null
                $errors = $null
                $text = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{{encoded}}'))
                [void][System.Management.Automation.Language.Parser]::ParseInput($text, [ref]$tokens, [ref]$errors)
                if ($errors.Count -ne 0) { [Console]::Error.WriteLine(($errors.Message -join '; ')); exit 1 }
                'PARSE_OK'
                """;
            var result = await WindowsCommands.ReadAsync(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", parser);
            if (result.Trim().TrimStart('\uFEFF') != "PARSE_OK") throw new InvalidDataException("Elevated script did not parse cleanly.");
        }
        Console.WriteLine($"Parsed apply/restore batch scripts without executing them. {ExecutableTweaks.Items.Count} executable tweaks; {SoftwareCatalog.OptionalApps.Count} optional apps.");
    }
}
