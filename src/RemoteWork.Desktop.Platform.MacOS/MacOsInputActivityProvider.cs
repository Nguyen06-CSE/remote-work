using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

/// <summary>
/// Uses CGEventTap (Quartz Event Services) to count keyboard and mouse events on macOS.
/// API: CGEventTapCreate with kCGEventTapOptionListenOnly (passive, listen-only).
/// Permission: Requires Accessibility permission (System Preferences → Privacy → Accessibility).
/// If permission is not granted, CGEventTapCreate returns IntPtr.Zero and the provider stays inactive.
/// NO key codes, key values, or mouse positions are recorded — only event counts.
/// Limitation: Requires user to grant Accessibility permission manually.
/// </summary>
public sealed class MacOsInputActivityProvider : IInputActivityProvider
{
    private const int kCGSessionEventTap = 1;
    private const int kCGHeadInsertEventTap = 0;
    private const int kCGEventTapOptionListenOnly = 1;

    // CGEventType values
    private const ulong kCGEventKeyDown = 10;
    private const ulong kCGEventLeftMouseDown = 1;
    private const ulong kCGEventRightMouseDown = 3;
    private const ulong kCGEventOtherMouseDown = 25;
    private const ulong kCGEventScrollWheel = 22;

    private static readonly IntPtr _kCFRunLoopCommonModes;

    static MacOsInputActivityProvider()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                var lib = NativeLibrary.Load("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
                var ptr = NativeLibrary.GetExport(lib, "kCFRunLoopCommonModes");
                _kCFRunLoopCommonModes = Marshal.ReadIntPtr(ptr);
            }
            catch
            {
                _kCFRunLoopCommonModes = IntPtr.Zero;
            }
        }
    }

    private readonly object _lock = new();
    private int _keyboardCount;
    private int _mouseCount;
    private volatile bool _started;
    private volatile bool _stopping;
    private Thread? _hookThread;
    private IntPtr _runLoop;
    private IntPtr _eventTap;

    // prevent GC of delegate
    private CGEventTapCallBack? _callback;

    public void Start()
    {
        if (_started)
            return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        _stopping = false;

        _hookThread = new Thread(RunLoopThread)
        {
            Name = "RemoteWork.InputHook.MacOS",
            IsBackground = true
        };
        _hookThread.Start();

        // Wait briefly for the thread to set up
        var waited = 0;
        while (!_started && !_stopping && waited < 3000)
        {
            Thread.Sleep(50);
            waited += 50;
        }
    }

    public void Stop()
    {
        if (!_started && _hookThread is null)
            return;

        _stopping = true;

        if (_runLoop != IntPtr.Zero)
        {
            try { CFRunLoopStop(_runLoop); } catch { /* already stopped */ }
        }

        _hookThread?.Join(TimeSpan.FromSeconds(3));
        _hookThread = null;
        _started = false;
    }

    public int GetKeyboardCount()
    {
        if (!_started)
            return 0;

        lock (_lock)
        {
            var count = _keyboardCount;
            _keyboardCount = 0;
            return count;
        }
    }

    public int GetMouseCount()
    {
        if (!_started)
            return 0;

        lock (_lock)
        {
            var count = _mouseCount;
            _mouseCount = 0;
            return count;
        }
    }

    private void RunLoopThread()
    {
        try
        {
            // Event mask: keyboard down + mouse clicks + scroll
            var eventMask =
                (1UL << (int)kCGEventKeyDown) |
                (1UL << (int)kCGEventLeftMouseDown) |
                (1UL << (int)kCGEventRightMouseDown) |
                (1UL << (int)kCGEventOtherMouseDown) |
                (1UL << (int)kCGEventScrollWheel);

            _callback = EventCallback;

            _eventTap = CGEventTapCreate(
                kCGSessionEventTap,
                kCGHeadInsertEventTap,
                kCGEventTapOptionListenOnly,
                eventMask,
                _callback,
                IntPtr.Zero);

            if (_eventTap == IntPtr.Zero)
            {
                // No Accessibility permission — graceful degradation
                _stopping = true;
                return;
            }

            var source = CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
            if (source == IntPtr.Zero)
            {
                _stopping = true;
                return;
            }

            _runLoop = CFRunLoopGetCurrent();
            CFRunLoopAddSource(_runLoop, source, _kCFRunLoopCommonModes);
            CGEventTapEnable(_eventTap, true);

            _started = true;

            CFRunLoopRun(); // blocks until CFRunLoopStop

            // Cleanup
            CGEventTapEnable(_eventTap, false);
            CFRelease(source);
            CFRelease(_eventTap);
            _eventTap = IntPtr.Zero;
            _runLoop = IntPtr.Zero;
        }
        catch
        {
            // ponytail: graceful degradation — if event tap fails, stay inactive
            _stopping = true;
        }

        _started = false;
    }

    private IntPtr EventCallback(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo)
    {
        // Only count, never inspect key codes or mouse positions
        lock (_lock)
        {
            if (type == kCGEventKeyDown)
            {
                _keyboardCount++;
            }
            else if (type == kCGEventLeftMouseDown ||
                     type == kCGEventRightMouseDown ||
                     type == kCGEventOtherMouseDown ||
                     type == kCGEventScrollWheel)
            {
                _mouseCount++;
            }
        }

        return eventRef;
    }

    public void Dispose()
    {
        Stop();
    }

    private delegate IntPtr CGEventTapCallBack(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern IntPtr CGEventTapCreate(
        int tap, int place, int options, ulong eventsOfInterest,
        CGEventTapCallBack callback, IntPtr userInfo);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern void CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, long order);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopRun();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopStop(IntPtr rl);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr obj);
}
