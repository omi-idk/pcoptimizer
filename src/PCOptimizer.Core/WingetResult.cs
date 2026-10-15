namespace PCOptimizer.Core;

public enum WingetResultKind
{
    Completed, AlreadyInstalled, NoApplicableUpdate, RebootRequired,
    RebootInitiated, RebootRequiredBeforeInstall, Failed
}

public sealed record WingetResult(WingetResultKind Kind, string Description)
{
    public bool IsFailure => Kind == WingetResultKind.Failed;
    public bool StopBatch => Kind is WingetResultKind.Failed or WingetResultKind.RebootRequired
        or WingetResultKind.RebootInitiated or WingetResultKind.RebootRequiredBeforeInstall;

    // HRESULT identities checked against Microsoft's AppInstallerErrors.h.
    // A successful exit is a command outcome, not independent installed-state verification.
    public static WingetResult FromExitCode(int code) => unchecked((uint)code) switch
    {
        0 => new(WingetResultKind.Completed, "WinGet finalizó sin error; instalación no verificada"),
        0x8A150061 or 0x8A15010D => new(WingetResultKind.AlreadyInstalled, "WinGet indica que ya está instalado; no se verificó su versión"),
        0x8A15002B => new(WingetResultKind.NoApplicableUpdate, "WinGet indica que no hay actualización aplicable; instalación no verificada"),
        3010 or 0x8A150109 => new(WingetResultKind.RebootRequired, "El instalador requiere reiniciar para finalizar; instalación pendiente de verificación"),
        1641 or 0x8A15010B => new(WingetResultKind.RebootInitiated, "El instalador informa que inició un reinicio; comprobar estado tras reiniciar"),
        0x8A15010A => new(WingetResultKind.RebootRequiredBeforeInstall, "Se requiere reiniciar antes de instalar; instalación no completada"),
        _ => new(WingetResultKind.Failed, $"WinGet terminó con código 0x{unchecked((uint)code):X8}. Revisa sus mensajes o registros; instalación no verificada")
    };
}
