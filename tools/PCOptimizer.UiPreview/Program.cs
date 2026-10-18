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
using PCOptimizer.Benchmarking;
using PCOptimizer.App.Benchmarking;

internal static class Program
{
    private static async Task ValidateBenchmarkIsolation(MainWindow window)
    {
        var view = (BenchmarkView)window.FindName("BenchmarkPanel");
        var module = (BenchmarkViewModel)view.DataContext;
        var shell = (MainViewModel)window.DataContext;
        var previousStatus = shell.Status;
        var isolated = false;
        await module.RunAsync(_ =>
        {
            isolated = !shell.IsIdle && view.IsEnabled;
            return Task.CompletedTask;
        });
        if (!isolated || !shell.IsIdle)
            throw new InvalidOperationException("Benchmark operations must block system changes and keep their own Stop control enabled.");
        shell.Status = previousStatus;
        var fake = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            await File.WriteAllTextAsync(fake, "not PresentMon");
            var service = new PresentMonCaptureService(Path.GetTempPath());
            var result = await service.CheckAsync(fake, CancellationToken.None);
            if (result.State != CapabilityState.Unsupported)
                throw new InvalidOperationException("An unrecognized PresentMon binary was accepted.");
            var rejected = false;
            try { await service.CaptureAsync(fake, new CaptureTarget(Environment.ProcessId, DateTime.UtcNow, "test"), 5, CancellationToken.None); }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Capture did not reject a wrong SHA-256 before execution.");
        }
        finally { File.Delete(fake); }
    }

    private sealed class MissingGpuProvider : IGpuTelemetryProvider
    {
        public int Reads { get; private set; }
        public Task<NvidiaProbeResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Reads++;
            return Task.FromResult(new NvidiaProbeResult([], "GPU fixture intentionally unavailable."));
        }
    }
    private static async Task ValidatePerformanceCapture()
    {
        var gpu = new MissingGpuProvider();
        var service = new PerformanceCaptureService(gpu);
        var result = await service.CaptureAsync(async token => { await Task.Delay(1100, token); return "fixture.csv"; }, CancellationToken.None);
        PerformanceTrace.Validate(result.Trace);
        if (result.Trace.Observations.Length < 2 || !result.Trace.Observations.SelectMany(o => o.Readings).Any(r => r.Metric == SensorMetric.MemoryUsedPercent && r.Value.HasValue))
            throw new InvalidOperationException("Read-only Windows memory sampling did not produce a bounded trace.");
        var reads = gpu.Reads;
        await Task.Delay(600);
        if (gpu.Reads != reads) throw new InvalidOperationException("Sampler remained active after capture.");
        using var cancel = new CancellationTokenSource(100);
        var canceled = false;
        try { await service.CaptureAsync(async token => { await Task.Delay(15000, token); return "unused.csv"; }, cancel.Token); }
        catch (OperationCanceledException) { canceled = true; }
        if (!canceled) throw new InvalidOperationException("Capture cancellation was not propagated.");
        reads = gpu.Reads;
        await Task.Delay(600);
        if (gpu.Reads != reads) throw new InvalidOperationException("Sampler remained active after cancellation.");
        Console.WriteLine("Read-only performance sampler: missing GPU, real Windows memory, stop and cancellation passed. No game or system modifications.");
    }
    private sealed class FixturePresentMon(string csvPath) : IPresentMonCaptureService
    {
        public Task<BenchmarkCapability> CheckAsync(string executable, CancellationToken token) => Task.FromResult(new BenchmarkCapability(CapabilityState.ValidationRequired, "Test fixture; no helper executed."));
        public async Task<string> CaptureAsync(string executable, CaptureTarget target, int seconds, CancellationToken token)
        {
            await Task.Delay(650, token);
            var qpc = System.Diagnostics.Stopwatch.GetTimestamp();
            await File.WriteAllTextAsync(csvPath, $"Application,ProcessID,SwapChainAddress,MsBetweenPresents,CPUStartQPC\nfixture.exe,{target.Id},0x1,10,{qpc - 1000}\nfixture.exe,{target.Id},0x1,20,{qpc}\n", token);
            return csvPath;
        }
    }
    private static async Task ValidatePerformanceWorkflow()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pcoptimizer-performance-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var csv = Path.Combine(folder, "fixture.csv");
            var store = new BenchmarkSessionStore(Path.Combine(folder, "sessions"));
            var capture = new FixturePresentMon(csv);
            var model = new BenchmarkViewModel(store, capture, new PerformanceCaptureService(new MissingGpuProvider()));
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            model.SelectedTarget = new(self.Id, self.StartTime.ToUniversalTime(), "fixture");
            model.CaptureSensors = true;
            await model.ConfigureExecutableAsync("test fixture", CancellationToken.None);
            await model.CaptureAsync(CancellationToken.None);
            if (model.Telemetry?.TargetProcessId != self.Id || model.PerformanceReport is null)
                throw new InvalidOperationException("Capture workflow did not attach and analyze its trace.");
            await model.SaveAsync(CancellationToken.None);
            var reopened = new BenchmarkViewModel(store, capture);
            await reopened.RefreshHistoryAsync(CancellationToken.None);
            reopened.SelectedHistory = reopened.History.Single();
            await reopened.LoadAsync(CancellationToken.None);
            if (reopened.Telemetry?.CaptureId != model.Telemetry.CaptureId || reopened.PerformanceReport?.Frames != 2)
                throw new InvalidOperationException("Reopening lost the trace or report.");
            var markdown = Path.Combine(folder, "report.md");
            await reopened.ExportReportAsync(markdown, CancellationToken.None);
            if (!File.ReadAllText(markdown).Contains("Sin atribución concluyente")) throw new InvalidOperationException("Report export is incomplete.");
            await reopened.ImportAsync(csv, CancellationToken.None);
            if (reopened.Telemetry is not null || reopened.PerformanceReport is not null)
                throw new InvalidOperationException("A manual import retained sensors from a different operation.");
            Console.WriteLine("Performance workflow: fixture CSV + read-only memory, save/reopen, report and stale-trace reset passed. No PresentMon helper/game executed.");
        }
        finally { Directory.Delete(folder, true); }
    }

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject child)
    {
        for (var current = VisualTreeHelper.GetParent(child); current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

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
        window.ConfigureResourceDesignPreview(new ResourceCapture(DateTimeOffset.UtcNow,
            Enumerable.Range(0,20).Select(i=>new ResourceSample(i,[new("0",95),new("1",10)],6000,32000,60,0)).ToArray(),
            ["Datos ilustrativos de la vista; no se midió ni modificó Windows."]));
        var previewFrames = Enumerable.Range(0, 3600).Select(i => new FrameSample(i % 377 == 0 ? 55 : 8 + Math.Sin(i * .07), 3, 6, true, "Application") { CpuStartQpc = 1000000 + i * 10000L }).ToArray();
        var previewTrace = new PerformanceTrace(1, Guid.NewGuid(), 1000000, 999999, 37000000, DateTimeOffset.UtcNow,
            Enumerable.Range(0,72).Select(i => new SensorObservation(1000000 + i * 500000, 1001000 + i * 500000, DateTimeOffset.UtcNow,
                [new(SensorMetric.GpuUtilizationPercent, "GPU ilustrativa", "Fixture de diseño", "%", 60 + 30 * Math.Sin(i * .3), SensorAvailability.Available, "Datos simulados de diseño; no medición."),
                 new(SensorMetric.MemoryUsedPercent, "Sistema ilustrativo", "Fixture de diseño", "%", 55, SensorAvailability.Available, "Datos simulados de diseño; no medición.")])).ToArray(),
            ["Datos simulados exclusivamente para revisar el diseño. No pertenecen al equipo del propietario."], 0);
        ((BenchmarkView)window.FindName("BenchmarkPanel")).ConfigureDesignPreview(new ImportedCapture(new string('A',64),
            new Dictionary<CaptureStream, FrameSample[]> { [new("juego-ilustrativo.exe",123,"0x1")] = previewFrames }, 1), previewTrace);
        _ = window.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await ValidateBenchmarkIsolation(window);
                await ValidatePerformanceCapture();
                await ValidatePerformanceWorkflow();
                await ValidateElevatedScriptSyntax();
                await ValidateReadOnlyProbes();
                foreach (var (name, section, width, height) in new[]
                {
                    ("overview", "Inicio", 1400, 880), ("tweaks", "Optimización", 1400, 880),
                    ("tweaks-compact", "Optimización", 1120, 740), ("applications", "Instalación rápida", 1400, 880),
                    ("resources", "Gaming", 1400, 880), ("network", "Red", 1400, 880), ("drivers", "Drivers", 1400, 880), ("energy", "Energía", 1400, 880), ("recovery", "Respaldo y recuperación", 1400, 880), ("benchmarks", "Benchmarks", 1400, 880), ("benchmarks-compact", "Benchmarks", 1120, 740), ("performance", "Benchmarks", 1400, 880), ("performance-compact", "Benchmarks", 1120, 740)
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
                    if (section == "Benchmarks")
                    {
                        var benchmark = (BenchmarkView)window.FindName("BenchmarkPanel");
                        var chart = Descendants<CorrelatedChart>(benchmark).Single();
                        var scroll = Descendants<ScrollViewer>(benchmark).First(v => IsAncestor(v, chart));
                        if (name.StartsWith("performance"))
                        {
                            var position = chart.TransformToAncestor(scroll).Transform(new Point());
                            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + position.Y - 140);
                        }
                        else scroll.ScrollToTop();
                    }
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
                RenderSessionsDesign(directory);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { window.Close(); app.Shutdown(); Dispatcher.ExitAllFrames(); }
        });
        Dispatcher.Run();
    }

    private sealed class DesignPowerPlatform : IPowerSessionPlatform
    {
        public Task<IReadOnlyList<PowerPlan>> PlansAsync() => Task.FromResult<IReadOnlyList<PowerPlan>>([]);
        public Task<bool> IsGameRunningAsync(GameIdentity game) => Task.FromResult(false);
        public Task ActivateAsync(Guid plan) => throw new InvalidOperationException("Design renderer cannot change power plans.");
    }
    private static void RenderSessionsDesign(string directory)
    {
        var platform = new DesignPowerPlatform();
        var game = new GameIdentity(123, DateTimeOffset.UtcNow, "juego-ilustrativo.exe · PID 123");
        var plan = new PowerPlan(Guid.NewGuid(), "Plan temporal de ejemplo", false);
        var session = new GamingPowerSession(1, Guid.NewGuid(), DateTimeOffset.UtcNow, game, Guid.NewGuid(), plan.Id, GamingSessionState.Active) {ApplyAttempted=true};
        var dialog = new GamingSessionsWindow(new GamingPowerSessionEngine(platform, Path.GetTempPath()), platform,
            () => false, () => {}, (_,_) => Task.FromResult(false));
        dialog.ConfigureDesignPreview(game,plan,session);
        var root=(FrameworkElement)dialog.Content; root.SetValue(TextElement.ForegroundProperty,dialog.Foreground);
        root.SetValue(TextElement.FontFamilyProperty,dialog.FontFamily); root.SetValue(TextElement.FontSizeProperty,13.0);
        dialog.Content=null;
        root.ClearValue(FrameworkElement.WidthProperty); root.ClearValue(FrameworkElement.HeightProperty);
        var host=new Border {Background=dialog.Background,Child=root,Width=850,Height=650};
        host.Measure(new Size(850,650)); host.Arrange(new Rect(0,0,850,650)); host.UpdateLayout();
        var texts=Descendants<TextBlock>(host).Select(t=>t.Text).ToArray();
        if (!texts.Contains(game.Name) || !texts.Contains(plan.Name) || texts.Any(t=>t.StartsWith("GameIdentity {") || t.StartsWith("PowerPlan {")))
            throw new InvalidOperationException("Gaming-session selectors did not render their display names.");
        if(root.ActualWidth+root.Margin.Left+root.Margin.Right>850.5 || root.ActualHeight+root.Margin.Top+root.Margin.Bottom>650.5)
            throw new InvalidOperationException("Gaming-session dialog content exceeds its viewport.");
        var bitmap=new RenderTargetBitmap(850,650,96,96,PixelFormats.Pbgra32);bitmap.Render(host);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var output=File.Create(Path.Combine(directory,"gaming-sessions.png"))) encoder.Save(output);
        dialog.Close(); Console.WriteLine("Rendered gaming sessions with illustrative data and a provider that rejects mutations.");
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

    private static async Task ValidateReadOnlyProbes()
    {
        if (NvidiaTelemetry.LayoutSizes != (24,8)) throw new InvalidOperationException("NVML ABI sizes are invalid.");
        var selfPath = WindowsProcessPath.Read(Environment.ProcessId);
        if (string.IsNullOrWhiteSpace(selfPath) || !Path.IsPathFullyQualified(selfPath))
            throw new InvalidOperationException("Limited-information process path lookup failed for the test process.");
        using (var self = System.Diagnostics.Process.GetCurrentProcess())
        {
            var identity = new GameIdentity(self.Id, new DateTimeOffset(self.StartTime.ToUniversalTime()), "Read-only smoke test");
            var platform = new WindowsPowerSessionPlatform();
            if (!await platform.IsGameRunningAsync(identity) || await platform.IsGameRunningAsync(identity with { StartedUtc = identity.StartedUtc.AddSeconds(1) }))
                throw new InvalidOperationException("Process identity validation did not distinguish its creation time.");
        }
        var gpu = await new NvidiaTelemetry().ReadAsync();
        Console.WriteLine($"Read-only NVML probe completed; {gpu.Devices.Length} exposed devices. This does not validate game/anticheat support.");
        foreach (var script in new[] {WindowsResourceProbe.HardwareScript,WindowsResourceProbe.CaptureScript, WindowsDiagnostics.WindowsDriverOffersScript})
        {
            var encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var parser=$"$t=$null; $e=$null; [void][System.Management.Automation.Language.Parser]::ParseInput([Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{encoded}')),[ref]$t,[ref]$e); if($e.Count){{throw ($e.Message -join '; ')}}";
            await WindowsCommands.ReadAsync(Path.Combine(Environment.SystemDirectory,"WindowsPowerShell","v1.0","powershell.exe"),"-NoProfile","-NonInteractive","-Command",parser);
        }
        using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var canceled=false;
        try { await WindowsCommands.ReadCancelableAsync(Path.Combine(Environment.SystemDirectory,"WindowsPowerShell","v1.0","powershell.exe"),cancel.Token,"-NoProfile","-NonInteractive","-Command","Start-Sleep -Seconds 15"); }
        catch(OperationCanceledException) {canceled=true;}
        if(!canceled) throw new InvalidOperationException("Read-only probe did not honor cancellation.");
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
