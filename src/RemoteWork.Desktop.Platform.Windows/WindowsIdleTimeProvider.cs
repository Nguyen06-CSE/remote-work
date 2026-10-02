using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

/// <summary>
/// Retrieves user idle duration on Windows using user32!GetLastInputInfo.
/// Measures time elapsed since the last hardware input event across the desktop session.
/// Handles 32-bit tick count wrapping safely.
/// Returns TimeSpan.Zero on error without throwing unhandled exceptions.
/// </summary>
public sealed class WindowsIdleTimeProvider : IIdleTimeProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public TimeSpan GetIdleTime()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TimeSpan.Zero;
        }

        var info = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        // Unsigned 32-bit subtraction correctly handles tick rollover every ~49.7 days
        var currentTick = (uint)Environment.TickCount;
        var idleMilliseconds = currentTick - info.dwTime;

        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }
}
