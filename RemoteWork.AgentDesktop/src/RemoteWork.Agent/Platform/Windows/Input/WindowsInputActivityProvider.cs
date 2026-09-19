using System.Runtime.InteropServices;
using RemoteWork.Agent.Core.Interfaces;

namespace RemoteWork.Agent.Platform.Windows.Input;

public sealed class WindowsInputActivityProvider
    : IInputActivityProvider
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MOUSEWHEEL = 0x020A;

    private const uint LLKHF_INJECTED = 0x00000010;

    private readonly object _lock = new();

    private int _keyboardCount;
    private int _mouseCount;

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    private LowLevelKeyboardProc? _keyboardProc;
    private LowLevelMouseProc? _mouseProc;

    private bool _started;

    public void Start()
    {
        if (_started)
            return;

        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        _keyboardHook = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _keyboardProc,
            GetModuleHandle(null),
            0);

        if (_keyboardHook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Failed to install keyboard hook.");
        }

        _mouseHook = SetWindowsHookEx(
            WH_MOUSE_LL,
            _mouseProc,
            GetModuleHandle(null),
            0);

        if (_mouseHook == IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;

            throw new InvalidOperationException(
                "Failed to install mouse hook.");
        }

        _started = true;
    }

    public void Stop()
    {
        if (!_started)
            return;

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        _started = false;
    }

    public int GetKeyboardCount()
    {
        lock (_lock)
        {
            var result = _keyboardCount;
            _keyboardCount = 0;
            return result;
        }
    }

    public int GetMouseCount()
    {
        lock (_lock)
        {
            var result = _mouseCount;
            _mouseCount = 0;
            return result;
        }
    }

    private IntPtr KeyboardHookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0 &&
            (wParam == (IntPtr)WM_KEYDOWN ||
             wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Do not count injected keyboard input.
            if ((data.flags & LLKHF_INJECTED) == 0)
            {
                lock (_lock)
                {
                    _keyboardCount++;
                }
            }
        }

        return CallNextHookEx(
            IntPtr.Zero,
            nCode,
            wParam,
            lParam);
    }

    private IntPtr MouseHookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();

            if (message == WM_LBUTTONDOWN ||
                message == WM_RBUTTONDOWN ||
                message == WM_MBUTTONDOWN ||
                message == WM_MOUSEWHEEL)
            {
                lock (_lock)
                {
                    _mouseCount++;
                }
            }
        }

        return CallNextHookEx(
            IntPtr.Zero,
            nCode,
            wParam,
            lParam);
    }

    public void Dispose()
    {
        Stop();
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    private delegate IntPtr LowLevelMouseProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        Delegate lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(
        IntPtr hhk);

    [DllImport(
        "user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Auto,
        SetLastError = true)]
    private static extern IntPtr GetModuleHandle(
        string? lpModuleName);
}