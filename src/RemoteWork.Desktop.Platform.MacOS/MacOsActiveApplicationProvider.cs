using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

/// <summary>
/// Uses NSWorkspace.sharedWorkspace.frontmostApplication via Objective-C runtime to get the active app.
/// API: libobjc.dylib objc_msgSend, NSWorkspace, NSRunningApplication.
/// Permission: None required for app name/PID. Window title requires Accessibility permission.
/// Limitation: Window title may be empty without Accessibility permission. Unlike Windows,
/// macOS NSRunningApplication gives the app-level info, not per-window info.
/// </summary>
public sealed class MacOsActiveApplicationProvider : IActiveApplicationProvider
{
    public ActiveApplicationInfo? GetActiveApplication()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return null;

        try
        {
            var nsWorkspace = objc_getClass("NSWorkspace");
            var sharedSel = sel_registerName("sharedWorkspace");
            var frontmostSel = sel_registerName("frontmostApplication");
            var nameSel = sel_registerName("localizedName");
            var pidSel = sel_registerName("processIdentifier");

            var workspace = objc_msgSend_ptr(nsWorkspace, sharedSel);
            if (workspace == IntPtr.Zero)
                return null;

            var app = objc_msgSend_ptr(workspace, frontmostSel);
            if (app == IntPtr.Zero)
                return null;

            var namePtr = objc_msgSend_ptr(app, nameSel);
            var appName = namePtr != IntPtr.Zero ? NSStringToString(namePtr) : string.Empty;
            var pid = objc_msgSend_int(app, pidSel);

            // Process name from the running application's bundle
            var processName = appName;
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(pid);
                processName = process.ProcessName;
            }
            catch
            {
                // use appName as fallback
            }

            return new ActiveApplicationInfo
            {
                ApplicationName = appName,
                ProcessName = processName,
                ProcessId = pid,
                WindowTitle = string.Empty, // ponytail: window title needs CGWindowList + Accessibility, skip for PoC
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null; // ponytail: graceful degradation
        }
    }

    private static string NSStringToString(IntPtr nsString)
    {
        var utf8Sel = sel_registerName("UTF8String");
        var ptr = objc_msgSend_ptr(nsString, utf8Sel);
        return ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) ?? string.Empty : string.Empty;
    }

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern int objc_msgSend_int(IntPtr receiver, IntPtr selector);
}
