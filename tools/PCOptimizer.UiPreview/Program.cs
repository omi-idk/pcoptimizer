using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        window.Show();
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
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(directory, name + ".png"));
                    encoder.Save(output);
                    Console.WriteLine($"Rendered {name}: {window.ActualWidth}x{window.ActualHeight}");
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { window.Close(); app.Shutdown(); Dispatcher.ExitAllFrames(); }
        });
        Dispatcher.Run();
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
