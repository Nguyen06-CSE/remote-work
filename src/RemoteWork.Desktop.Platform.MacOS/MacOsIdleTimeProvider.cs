using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

/// <summary>
/// Uses IOKit HIDIdleTime to get actual user idle duration on macOS.
/// API: IOServiceGetMatchingService + IORegistryEntryCreateCFProperty("HIDIdleTime")
/// The HIDIdleTime value is in nanoseconds.
/// Permission: None required.
/// Limitation: Wraps at ~49 days (uint32 tick on some older systems), accurate to ~millisecond.
/// </summary>
public sealed class MacOsIdleTimeProvider : IIdleTimeProvider
{
    private const uint kIOMasterPortDefault = 0;
    private const uint kCFStringEncodingUTF8 = 0x08000100;
    private const int kCFNumberSInt64Type = 4;

    public TimeSpan GetIdleTime()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return TimeSpan.Zero;

        try
        {
            var matching = IOServiceMatching("IOHIDSystem");
            if (matching == IntPtr.Zero)
                return TimeSpan.Zero;

            var service = IOServiceGetMatchingService(kIOMasterPortDefault, matching);
            // matching is consumed by IOServiceGetMatchingService, no need to release
            if (service == 0)
                return TimeSpan.Zero;

            try
            {
                var key = CFStringCreateWithCString(IntPtr.Zero, "HIDIdleTime", kCFStringEncodingUTF8);
                if (key == IntPtr.Zero)
                    return TimeSpan.Zero;

                try
                {
                    var property = IORegistryEntryCreateCFProperty(service, key, IntPtr.Zero, 0);
                    if (property == IntPtr.Zero)
                        return TimeSpan.Zero;

                    try
                    {
                        if (CFNumberGetValue(property, kCFNumberSInt64Type, out var nanoseconds))
                        {
                            return TimeSpan.FromMilliseconds(nanoseconds / 1_000_000.0);
                        }
                    }
                    finally
                    {
                        CFRelease(property);
                    }
                }
                finally
                {
                    CFRelease(key);
                }
            }
            finally
            {
                IOObjectRelease(service);
            }
        }
        catch
        {
            // ponytail: graceful degradation — if IOKit fails, return zero
        }

        return TimeSpan.Zero;
    }

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    private static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    private static extern IntPtr IOServiceMatching(string name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    private static extern IntPtr IORegistryEntryCreateCFProperty(uint entry, IntPtr key, IntPtr allocator, uint options);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    private static extern int IOObjectRelease(uint obj);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string str, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern bool CFNumberGetValue(IntPtr number, int theType, out long value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr obj);
}
