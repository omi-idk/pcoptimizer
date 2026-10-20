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
        return Read(process);
    }
    internal static string? Read(SafeProcessHandle process)
    {
        if (process.IsInvalid) return null;
        for (var capacity = 512; capacity <= 32768; capacity *= 2)
        {
            var text = new StringBuilder(capacity); uint count = (uint)capacity;
            if (QueryFullProcessImageName(process, 0, text, ref count)) return text.ToString();
            if (Marshal.GetLastWin32Error() != 122) return null; // Only retry ERROR_INSUFFICIENT_BUFFER.
        }
        return null;
    }
}
