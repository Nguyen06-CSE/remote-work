using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Models.Activity;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

/// <summary>
/// Uses Win32 Low-Level hooks (WH_KEYBOARD_LL, WH_MOUSE_LL) to count keyboard and mouse events on Windows.
/// Privacy by Design:
/// - Counts only: keyboard key-down events, mouse clicks (left/right/middle), and scroll wheel interactions.
/// - Never collects or stores: typed text, key values, passwords, scan codes, or mouse X/Y coordinates.
/// - Mouse movement (WM_MOUSEMOVE) is explicitly not counted.
/// Isolation & Safety:
/// - Hooks run on a dedicated STA background thread with an explicit Win32 message pump (GetMessage/DispatchMessage).
/// - Thread termination cleanly posts WM_QUIT and unhooks Win32 hooks.
/// - Graceful degradation: does not throw or crash if hooks cannot be installed.
/// </summary>
public sealed class WindowsInputActivityProvider : IInputActivityProvider
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
    private readonly ManualResetEventSlim _hookReady = new(false);
    private readonly ILogger<WindowsInputActivityProvider>? _logger;

    private int _keyboardCount;
    private int _mouseCount;

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    private LowLevelKeyboardProc? _keyboardProc;
    private LowLevelMouseProc? _mouseProc;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private volatile bool _started;
    private volatile bool _stopping;

    public WindowsInputActivityProvider(ILogger<WindowsInputActivityProvider>? logger = null)
    {
        _logger = logger;
    }

    public void Start()
    {
        if (_started)
            return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        _hookReady.Reset();
        _stopping = false;

        _hookThread = new Thread(HookThreadProc)
        {
            Name = "RemoteWork.InputHook.Windows",
            IsBackground = true
        };

        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        if (!_hookReady.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger?.LogWarning("Timed out waiting for Windows input hooks initialization. Input counting disabled.");
            return;
        }

        if (!_started)
        {
            _logger?.LogWarning("Failed to install Windows input hooks. Input counting disabled.");
        }
    }

    public void Stop()
    {
        if (!_started && _hookThread is null)
            return;

        _stopping = true;

        if (_hookThreadId != 0)
        {
            PostThreadMessage(_hookThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        }

        _hookThread?.Join(TimeSpan.FromSeconds(3));

        _hookThread = null;
        _hookThreadId = 0;
        _started = false;
    }

    public int GetKeyboardCount()
    {
        if (!_started)
            return 0;

        lock (_lock)
        {
            var result = _keyboardCount;
            _keyboardCount = 0;
            return result;
        }
    }

    public int GetMouseCount()
    {
        if (!_started)
            return 0;

        lock (_lock)
        {
            var result = _mouseCount;
            _mouseCount = 0;
            return result;
        }
    }

    public IReadOnlyList<MouseClickSample> DrainMouseSamples()
    {
        // No mouse coordinates stored under privacy policy
        return Array.Empty<MouseClickSample>();
    }

    private void HookThreadProc()
    {
        _hookThreadId = GetCurrentThreadId();

        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        _keyboardHook = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _keyboardProc,
            IntPtr.Zero,
            0);

        _mouseHook = SetWindowsHookEx(
            WH_MOUSE_LL,
            _mouseProc,
            IntPtr.Zero,
            0);

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            _logger?.LogWarning("Windows SetWindowsHookEx failed with error code {ErrorCode}. Input counting will be disabled.", Marshal.GetLastWin32Error());

            if (_keyboardHook != IntPtr.Zero)
                UnhookWindowsHookEx(_keyboardHook);

            if (_mouseHook != IntPtr.Zero)
                UnhookWindowsHookEx(_mouseHook);

            _keyboardHook = IntPtr.Zero;
            _mouseHook = IntPtr.Zero;

            _hookReady.Set();
            return;
        }

        _started = true;
        _hookReady.Set();

        while (!_stopping && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

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
        _hookReady.Set();
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();

            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                if ((data.flags & LLKHF_INJECTED) == 0)
                {
                    lock (_lock)
                    {
                        _keyboardCount++;
                    }
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();

            // Only count meaningful interactions: Left, Right, Middle, Wheel.
            // Mouse movement (WM_MOUSEMOVE = 0x0200) is ignored.
            // Coordinates are NEVER stored.
            if (msg == WM_LBUTTONDOWN ||
                msg == WM_RBUTTONDOWN ||
                msg == WM_MBUTTONDOWN ||
                msg == WM_MOUSEWHEEL)
            {
                lock (_lock)
                {
                    _mouseCount++;
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        _hookReady.Dispose();
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}