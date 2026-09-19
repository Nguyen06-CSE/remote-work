using System.Runtime.InteropServices;

namespace RemoteWork.Agent.Platform.Windows.Input;

public sealed class WindowsInputActivityProvider
{
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool GetLastInputInfo(
        ref LASTINPUTINFO plii);

    public TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref info))
        {
            throw new InvalidOperationException(
                "Unable to retrieve Windows last input information.");
        }

        var tickCount = Environment.TickCount64;

        var idleMilliseconds =
            tickCount - info.dwTime;

        return TimeSpan.FromMilliseconds(
            Math.Max(0, idleMilliseconds));
    }
}