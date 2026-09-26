using System.Diagnostics;
using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

/// <summary>
/// PoC screenshot using macOS native screencapture CLI utility.
/// API: /usr/sbin/screencapture -x (suppress click sound) — wraps CoreGraphics internally.
/// Permission: Requires Screen Recording permission (System Preferences → Privacy → Screen Recording).
/// Output: PNG file at the specified path.
/// Limitation: Primary display. Requires Screen Recording permission or produces a blank/error image.
/// </summary>
// ponytail: screencapture CLI wrapper, switch to CGWindowListCreateImage P/Invoke if perf matters
public sealed class MacOsScreenshotProvider : IScreenshotProvider
{
    public ScreenshotResult CaptureScreen(string outputPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new ScreenshotResult { Success = false, ErrorMessage = "Not running on macOS" };

        try
        {
            // Ensure .png extension for screencapture
            if (!outputPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                outputPath = Path.ChangeExtension(outputPath, ".png");

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/sbin/screencapture",
                Arguments = $"-x \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return new ScreenshotResult { Success = false, ErrorMessage = "Failed to start screencapture" };

            process.WaitForExit(10_000);

            if (!File.Exists(outputPath))
                return new ScreenshotResult { Success = false, ErrorMessage = "screencapture did not produce a file — Screen Recording permission may be denied" };

            var fileInfo = new FileInfo(outputPath);
            if (fileInfo.Length == 0)
                return new ScreenshotResult { Success = false, ErrorMessage = "Screenshot file is empty — Screen Recording permission may be denied" };

            // Read PNG dimensions from header (bytes 16-23 contain width/height as big-endian uint32)
            var (width, height) = ReadPngDimensions(outputPath);

            return new ScreenshotResult
            {
                Success = true,
                FilePath = outputPath,
                Width = width,
                Height = height
            };
        }
        catch (Exception ex)
        {
            return new ScreenshotResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static (int width, int height) ReadPngDimensions(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var header = new byte[24];
            if (fs.Read(header, 0, 24) < 24)
                return (0, 0);

            // PNG signature check
            if (header[0] != 0x89 || header[1] != 0x50)
                return (0, 0);

            // Width at offset 16, Height at offset 20 (big-endian uint32)
            var width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            var height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return (width, height);
        }
        catch
        {
            return (0, 0);
        }
    }
}
