using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

/// <summary>
/// Retrieves the currently active foreground application on macOS.
/// Primary mechanism: WindowServer query via CoreGraphics CGWindowListCopyWindowInfo (Z-order layer 0).
/// This provides instantaneous, real-time foreground window owner information without requiring a Cocoa NSRunLoop.
/// Secondary fallback: NSWorkspace.sharedWorkspace.frontmostApplication via AppKit / Objective-C runtime.
/// Dynamically loads AppKit.framework via dlopen to ensure NSWorkspace is registered in .NET console hosts.
/// Permission: None required for application name and PID. Window title is strictly omitted per privacy constraints.
/// </summary>
public sealed class MacOsActiveApplicationProvider : IActiveApplicationProvider
{
    private const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const int RtldLazy = 1;

    private static readonly object _initLock = new();
    private static bool _staticInitialized;
    private static bool _staticInitSuccess;
    private static string? _staticInitError;
    private static IntPtr _appKitHandle = IntPtr.Zero;

    private readonly ILogger<MacOsActiveApplicationProvider>? _logger;
    private readonly Func<string, int, IntPtr>? _dlopenFunc;
    private readonly Func<string, IntPtr>? _getClassFunc;

    public MacOsActiveApplicationProvider(ILogger<MacOsActiveApplicationProvider>? logger = null)
        : this(logger, null, null)
    {
    }

    /// <summary>
    /// Test seam constructor allowing injected library loader and class resolver delegates.
    /// </summary>
    internal MacOsActiveApplicationProvider(
        ILogger<MacOsActiveApplicationProvider>? logger,
        Func<string, int, IntPtr>? dlopenFunc,
        Func<string, IntPtr>? getClassFunc)
    {
        _logger = logger;
        _dlopenFunc = dlopenFunc;
        _getClassFunc = getClassFunc;
    }

    public ActiveApplicationInfo? GetActiveApplication()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return null;

        if (!EnsureAppKitInitialized())
            return null;

        if (_getClassFunc is not null)
        {
            var testClass = _getClassFunc("NSWorkspace");
            if (testClass == IntPtr.Zero)
            {
                _logger?.LogWarning("Objective-C class lookup for 'NSWorkspace' failed via injected resolver.");
                return null;
            }
        }

        // 1. Primary mechanism: Query WindowServer directly via CoreGraphics
        var windowInfo = GetActiveViaWindowList();
        if (windowInfo is not null)
            return windowInfo;

        // 2. Fallback mechanism: Query NSWorkspace frontmostApplication via Objective-C
        return GetActiveViaWorkspace();
    }

    private ActiveApplicationInfo? GetActiveViaWindowList()
    {
        try
        {
            // kCGWindowListOptionOnScreenOnly (1) | kCGWindowListExcludeDesktopElements (16) = 17
            var list = CGWindowListCopyWindowInfo(17, 0);
            if (list == IntPtr.Zero)
                return null;

            try
            {
                var count = CFArrayGetCount(list);
                for (int i = 0; i < count; i++)
                {
                    var dict = CFArrayGetValueAtIndex(list, i);
                    int dictCount = CFDictionaryGetCount(dict);
                    if (dictCount <= 0) continue;

                    var keys = new IntPtr[dictCount];
                    var vals = new IntPtr[dictCount];
                    CFDictionaryGetKeysAndValues(dict, keys, vals);

                    int layer = -1;
                    int pid = 0;
                    string appName = string.Empty;

                    for (int k = 0; k < dictCount; k++)
                    {
                        var key = CFStringToString(keys[k]);
                        if (key == "kCGWindowLayer")
                        {
                            CFNumberGetValue(vals[k], 9 /* kCFNumberIntType */, out layer);
                        }
                        else if (key == "kCGWindowOwnerPID")
                        {
                            CFNumberGetValue(vals[k], 9, out pid);
                        }
                        else if (key == "kCGWindowOwnerName")
                        {
                            appName = CFStringToString(vals[k]);
                        }
                    }

                    // Layer 0 is the normal application window level (kCGNormalWindowLevel = 0)
                    if (layer == 0 && pid > 0 && !string.IsNullOrWhiteSpace(appName))
                    {
                        var processName = appName;
                        try
                        {
                            using var process = Process.GetProcessById(pid);
                            processName = process.ProcessName;
                        }
                        catch
                        {
                            // Ignore process lookup failure, fallback to appName
                        }

                        if (string.IsNullOrWhiteSpace(processName))
                            processName = appName;

                        _logger?.LogTrace("Active application retrieved via WindowServer: {AppName} ({ProcessName}, PID: {Pid})", appName, processName, pid);

                        return new ActiveApplicationInfo
                        {
                            ApplicationName = appName,
                            ProcessName = processName,
                            ProcessId = pid,
                            WindowTitle = string.Empty, // Strictly omitted: zero document titles, URLs, or queries per privacy policy
                            Timestamp = DateTimeOffset.UtcNow
                        };
                    }
                }

                return null;
            }
            finally
            {
                CFRelease(list);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "WindowServer query encountered an exception. Falling back to NSWorkspace.");
            return null;
        }
    }

    private ActiveApplicationInfo? GetActiveViaWorkspace()
    {
        DrainRunLoop();

        try
        {
            var getClass = _getClassFunc ?? objc_getClass;
            var nsWorkspace = getClass("NSWorkspace");
            if (nsWorkspace == IntPtr.Zero)
            {
                _logger?.LogWarning("Objective-C class lookup for 'NSWorkspace' failed despite AppKit being loaded.");
                return null;
            }

            var sharedSel = sel_registerName("sharedWorkspace");
            var frontmostSel = sel_registerName("frontmostApplication");
            var nameSel = sel_registerName("localizedName");
            var pidSel = sel_registerName("processIdentifier");

            var workspace = objc_msgSend_ptr(nsWorkspace, sharedSel);
            if (workspace == IntPtr.Zero)
            {
                _logger?.LogDebug("NSWorkspace.sharedWorkspace returned null (no workspace available).");
                return null;
            }

            var app = objc_msgSend_ptr(workspace, frontmostSel);
            if (app == IntPtr.Zero)
            {
                _logger?.LogDebug("NSWorkspace.frontmostApplication returned null (no foreground application).");
                return null;
            }

            var namePtr = objc_msgSend_ptr(app, nameSel);
            var appName = namePtr != IntPtr.Zero ? NSStringToString(namePtr) : string.Empty;
            var pid = objc_msgSend_int(app, pidSel);

            var processName = appName;
            if (pid > 0)
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    processName = process.ProcessName;
                }
                catch
                {
                    // Fallback to appName if process lookup fails
                }
            }

            if (string.IsNullOrWhiteSpace(appName))
                appName = processName;

            if (string.IsNullOrWhiteSpace(processName))
                processName = appName;

            if (string.IsNullOrWhiteSpace(appName) && pid <= 0)
            {
                _logger?.LogDebug("Frontmost application returned no valid name or PID.");
                return null;
            }

            _logger?.LogTrace("Active application retrieved via NSWorkspace: {AppName} ({ProcessName}, PID: {Pid})", appName, processName, pid);

            return new ActiveApplicationInfo
            {
                ApplicationName = appName,
                ProcessName = processName,
                ProcessId = pid,
                WindowTitle = string.Empty, // Strictly omitted: zero document titles, URLs, or queries per privacy policy
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error retrieving active application from macOS Objective-C runtime.");
            return null;
        }
    }

    private static void DrainRunLoop()
    {
        try
        {
            var nsRunLoopClass = objc_getClass("NSRunLoop");
            var nsDateClass = objc_getClass("NSDate");

            if (nsRunLoopClass == IntPtr.Zero || nsDateClass == IntPtr.Zero)
                return;

            var currentRunLoopSel = sel_registerName("currentRunLoop");
            var dateWithTimeIntervalSel = sel_registerName("dateWithTimeIntervalSinceNow:");
            var runUntilDateSel = sel_registerName("runUntilDate:");

            var runLoop = objc_msgSend_ptr(nsRunLoopClass, currentRunLoopSel);
            if (runLoop == IntPtr.Zero)
                return;

            var date = objc_msgSend_ptr_double(nsDateClass, dateWithTimeIntervalSel, 0.01);
            if (date == IntPtr.Zero)
                return;

            objc_msgSend_void_ptr(runLoop, runUntilDateSel, date);
        }
        catch
        {
            // Graceful degradation: continue even if run loop drain encounters an unexpected failure
        }
    }

    private bool EnsureAppKitInitialized()
    {
        if (_dlopenFunc is not null)
        {
            var handle = _dlopenFunc(AppKitPath, RtldLazy);
            if (handle == IntPtr.Zero)
            {
                _logger?.LogError("Failed to initialize AppKit via injected loader.");
                return false;
            }
            return true;
        }

        if (_staticInitialized)
            return _staticInitSuccess;

        lock (_initLock)
        {
            if (_staticInitialized)
                return _staticInitSuccess;

            try
            {
                _appKitHandle = dlopen(AppKitPath, RtldLazy);
                if (_appKitHandle == IntPtr.Zero)
                {
                    var errPtr = dlerror();
                    var errMsg = errPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errPtr) : "Unknown dlopen error";
                    _staticInitError = errMsg;
                    _staticInitSuccess = false;
                    _logger?.LogError("Failed to dynamically load AppKit from '{Path}': {Error}", AppKitPath, errMsg);
                }
                else
                {
                    _staticInitSuccess = true;
                    _logger?.LogInformation("Successfully initialized AppKit framework for macOS active application tracking.");
                }
            }
            catch (Exception ex)
            {
                _staticInitError = ex.Message;
                _staticInitSuccess = false;
                _logger?.LogError(ex, "Exception dynamically loading AppKit framework from '{Path}'.", AppKitPath);
            }
            finally
            {
                _staticInitialized = true;
            }

            return _staticInitSuccess;
        }
    }

    private static string NSStringToString(IntPtr nsString)
    {
        var utf8Sel = sel_registerName("UTF8String");
        var ptr = objc_msgSend_ptr(nsString, utf8Sel);
        return ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) ?? string.Empty : string.Empty;
    }

    private static string CFStringToString(IntPtr cfStr)
    {
        if (cfStr == IntPtr.Zero) return string.Empty;
        var len = CFStringGetLength(cfStr);
        var buffer = new byte[len * 4 + 1];
        if (CFStringGetCString(cfStr, buffer, buffer.Length, 0x08000100 /* kCFStringEncodingUTF8 */))
        {
            return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        }
        return string.Empty;
    }

    [DllImport("libdl.dylib", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("libdl.dylib", EntryPoint = "dlerror")]
    private static extern IntPtr dlerror();

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr_double(IntPtr receiver, IntPtr selector, double val);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_ptr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern int objc_msgSend_int(IntPtr receiver, IntPtr selector);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern int CFArrayGetCount(IntPtr theArray);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, int idx);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern int CFDictionaryGetCount(IntPtr theDict);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFDictionaryGetKeysAndValues(IntPtr theDict, IntPtr[] keys, IntPtr[] values);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, long bufferSize, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern long CFStringGetLength(IntPtr theString);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern bool CFNumberGetValue(IntPtr number, int theType, out int value);
}
