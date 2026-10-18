using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PCOptimizer.App;

internal static class WindowsProcessPath
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref uint size);
    public static string? Read(int processId)
    {
        if (processId <= 0) return null;
        // QUERY_LIMITED_INFORMATION only. No PROCESS_VM_READ/WRITE, module enumeration or remote threads.
        using var process = OpenProcess(0x1000, false, (uint)processId);
        if (process.IsInvalid) return null;
        var text = new StringBuilder(32768); uint count = (uint)text.Capacity;
        return QueryFullProcessImageName(process, 0, text, ref count) ? text.ToString() : null;
    }
}
