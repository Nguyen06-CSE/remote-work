using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

/// <summary>
/// Uses GetForegroundWindow + GetWindowThreadProcessId + Process.GetProcessById to detect active window.
/// API: user32.dll GetForegroundWindow, GetWindowThreadProcessId, GetWindowText.
/// Permission: None required on Windows.
/// Limitation: Some UWP/Store apps may show a generic process name.
/// </summary>
public sealed class WindowsActiveApplicationProvider : IActiveApplicationProvider
{
    public ActiveApplicationInfo? GetActiveApplication()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
                return null;

            using var process = Process.GetProcessById((int)pid);

            var titleLen = GetWindowTextLength(hwnd);
            var title = string.Empty;
            if (titleLen > 0)
            {
                var sb = new StringBuilder(titleLen + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                title = sb.ToString();
            }

            return new ActiveApplicationInfo
            {
                ApplicationName = process.MainWindowTitle.Length > 0 ? process.MainWindowTitle : process.ProcessName,
                ProcessName = process.ProcessName,
                ProcessId = (int)pid,
                WindowTitle = title,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null; // ponytail: graceful degradation
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
