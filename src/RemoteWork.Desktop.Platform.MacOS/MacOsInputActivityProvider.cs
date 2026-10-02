using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Models.Activity;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

/// <summary>
/// Uses CGEventTap (Quartz Event Services) to count keyboard and mouse interaction events on macOS.
/// Privacy by Design:
/// - Counts only: keyboard key-down events, mouse clicks (left/right/middle), and scroll wheel interactions.
/// - Never collects or stores: typed text, key values, passwords, scan codes, or mouse X/Y coordinates.
/// - Mouse movement is not counted as individual events.
/// Permission:
/// - Requires macOS Accessibility permission (AXIsProcessTrusted).
/// - If permission is missing, logs a diagnostic warning and degrades safely without crashing.
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
    private readonly ILogger<MacOsInputActivityProvider>? _logger;

    private int _keyboardCount;
    private int _mouseCount;
    private volatile bool _started;
    private volatile bool _stopping;
    private Thread? _hookThread;
    private IntPtr _runLoop;
    private IntPtr _eventTap;

    // Prevent GC of delegate
    private CGEventTapCallBack? _callback;

    public MacOsInputActivityProvider(ILogger<MacOsInputActivityProvider>? logger = null)
    {
        _logger = logger;
    }

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

        // Wait briefly for the hook thread to set up
        var waited = 0;
        while (!_started && !_stopping && waited < 3000)
        {
            Thread.Sleep(50);
            waited += 50;
        }

        if (!_started && !_stopping)
        {
            _logger?.LogWarning("Timed out initializing macOS CGEventTap. Input counting will remain disabled.");
        }
    }

    public void Stop()
    {
        if (!_started && _hookThread is null)
            return;

        _stopping = true;

        if (_runLoop != IntPtr.Zero)
        {
            try
            {
                CFRunLoopStop(_runLoop);
            }
            catch
            {
                // Already stopped
            }
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

    public IReadOnlyList<MouseClickSample> DrainMouseSamples()
    {
        // No coordinates collected or stored under privacy policy
        return Array.Empty<MouseClickSample>();
    }

    private void RunLoopThread()
    {
        try
        {
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
                _stopping = true;
                _logger?.LogWarning("Failed to create macOS CGEventTap. Accessibility permission may not be granted. Input counting will be inactive until permission is granted.");
                return;
            }

            var source = CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
            if (source == IntPtr.Zero)
            {
                _stopping = true;
                _logger?.LogWarning("Failed to create CFMachPortRunLoopSource for CGEventTap.");
                return;
            }

            _runLoop = CFRunLoopGetCurrent();
            CFRunLoopAddSource(_runLoop, source, _kCFRunLoopCommonModes);
            CGEventTapEnable(_eventTap, true);

            _started = true;

            CFRunLoopRun(); // Blocks until CFRunLoopStop is called

            // Cleanup resources
            CGEventTapEnable(_eventTap, false);
            CFRelease(source);
            CFRelease(_eventTap);
            _eventTap = IntPtr.Zero;
            _runLoop = IntPtr.Zero;
        }
        catch (Exception ex)
        {
            _stopping = true;
            _logger?.LogError(ex, "Unexpected exception in macOS CGEventTap RunLoop thread.");
        }

        _started = false;
    }

    private IntPtr EventCallback(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo)
    {
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

        // Return the event untouched; never block user input
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