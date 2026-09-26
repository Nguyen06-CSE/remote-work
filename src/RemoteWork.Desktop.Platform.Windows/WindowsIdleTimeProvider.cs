using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

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
            throw new InvalidOperationException("Unable to retrieve Windows last input information.");
        }

        var tickCount = Environment.TickCount64;
        var idleMilliseconds = tickCount - info.dwTime;

        return TimeSpan.FromMilliseconds(Math.Max(0, idleMilliseconds));
    }
}
