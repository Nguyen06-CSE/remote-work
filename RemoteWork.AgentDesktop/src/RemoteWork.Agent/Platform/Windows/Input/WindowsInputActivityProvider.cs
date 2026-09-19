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
    private readonly ManualResetEventSlim _hookReady = new(false);

    private int _keyboardCount;
    private int _mouseCount;

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    // QUAN TRỌNG: giữ reference để GC không thu hồi
    private LowLevelKeyboardProc? _keyboardProc;
    private LowLevelMouseProc? _mouseProc;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private volatile bool _started;
    private volatile bool _stopping;

    public void Start()
    {
        if (_started)
            return;

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Input hooks require Windows.");
        }

        _hookReady.Reset();
        _stopping = false;

        _hookThread = new Thread(HookThreadProc)
        {
            Name = "RemoteWork.InputHook",
            IsBackground = true
        };

        // Low-level hooks yêu cầu STA thread + message pump
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        // Chờ hook được cài xong (tối đa 5s)
        if (!_hookReady.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException(
                "Timed out installing input hooks.");
        }

        if (!_started)
        {
            throw new InvalidOperationException(
                "Failed to install input hooks.");
        }
    }

    public void Stop()
    {
        if (!_started && _hookThread is null)
            return;

        _stopping = true;

        // Post WM_QUIT vào hook thread để thoát message loop
        if (_hookThreadId != 0)
        {
            PostThreadMessage(
                _hookThreadId,
                0x0012, // WM_QUIT
                IntPtr.Zero,
                IntPtr.Zero);
        }

        _hookThread?.Join(TimeSpan.FromSeconds(3));

        _hookThread = null;
        _hookThreadId = 0;
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

    private void HookThreadProc()
    {
        _hookThreadId = GetCurrentThreadId();

        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        // hMod = IntPtr.Zero vì LL hooks không cần module handle
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

        if (_keyboardHook == IntPtr.Zero ||
            _mouseHook == IntPtr.Zero)
        {
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

        // MESSAGE PUMP — bắt buộc cho low-level hooks
        while (!_stopping &&
               GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // Cleanup
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

    private IntPtr KeyboardHookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();

            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data =
                    Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                if ((data.flags & LLKHF_INJECTED) == 0)
                {
                    lock (_lock)
                    {
                        _keyboardCount++;
                        //Console.WriteLine($"[HOOK] Key down: vk={data.vkCode}, total={_keyboardCount}");
                    }
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();

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

    // ---------- P/Invoke với delegate type cụ thể ----------

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr LowLevelMouseProc(
        int nCode, IntPtr wParam, IntPtr lParam);

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

    [DllImport("user32.dll", SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelMouseProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(
        out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(
        uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}