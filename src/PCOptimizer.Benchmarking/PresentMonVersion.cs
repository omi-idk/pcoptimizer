namespace PCOptimizer.Benchmarking;

public static class PresentMonVersion
{
    public const string Version = "2.6.0";
    public const string Sha256 = "B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF";
    public const string DownloadUrl = "https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe";
    public static IReadOnlyList<string> CaptureArguments(int processId, int durationSeconds, string sessionName, string outputPath)
    {
        if (processId <= 0 || durationSeconds is < 5 or > 600 || !Guid.TryParseExact(sessionName, "N", out _))
            throw new ArgumentException("PID, duración (5–600 s) o sesión de captura inválidos.");
        if (!Path.IsPathFullyQualified(outputPath)) throw new ArgumentException("La salida debe ser absoluta.");
        return ["--process_id", processId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--output_file", outputPath, "--session_name", sessionName, "--timed",
            durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "--terminate_after_timed",
            "--terminate_on_proc_exit", "--no_console_stats", "--track_frame_type"];
    }
}
