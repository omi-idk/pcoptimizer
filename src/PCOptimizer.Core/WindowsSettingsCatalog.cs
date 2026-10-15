namespace PCOptimizer.Core;

public static class WindowsSettingsCatalog
{
    public static string GetUri(string key) => key switch
    {
        "Taskbar" => "ms-settings:taskbar",
        "Updates" => "ms-settings:windowsupdate",
        "UpdateHours" => "ms-settings:windowsupdate-activehours",
        "UpdateOptions" => "ms-settings:windowsupdate-options",
        "UpdateHistory" => "ms-settings:windowsupdate-history",
        "GameMode" => "ms-settings:gaming-gamemode",
        "Captures" => "ms-settings:gaming-gamedvr",
        "Startup" => "ms-settings:startupapps",
        _ => throw new ArgumentException("La página de configuración no pertenece a la lista permitida.", nameof(key))
    };
}
