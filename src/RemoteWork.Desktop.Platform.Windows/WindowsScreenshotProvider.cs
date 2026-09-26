using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

/// <summary>
/// PoC screenshot using GDI BitBlt and manual BMP file writing. No external dependencies.
/// API: user32.dll (GetDC, GetSystemMetrics), gdi32.dll (CreateCompatibleDC, BitBlt, CreateDIBSection).
/// Permission: None required on Windows.
/// Output: BMP file at the specified path.
/// Limitation: Primary monitor only. BMP format (uncompressed).
/// </summary>
public sealed class WindowsScreenshotProvider : IScreenshotProvider
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint BI_RGB = 0;

    public ScreenshotResult CaptureScreen(string outputPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new ScreenshotResult { Success = false, ErrorMessage = "Not running on Windows" };

        try
        {
            var width = GetSystemMetrics(SM_CXSCREEN);
            var height = GetSystemMetrics(SM_CYSCREEN);

            if (width <= 0 || height <= 0)
                return new ScreenshotResult { Success = false, ErrorMessage = "Could not determine screen dimensions" };

            var screenDc = GetDC(IntPtr.Zero);
            var memDc = CreateCompatibleDC(screenDc);

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB
                }
            };

            var hBitmap = CreateDIBSection(memDc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
            var oldBitmap = SelectObject(memDc, hBitmap);

            BitBlt(memDc, 0, 0, width, height, screenDc, 0, 0, SRCCOPY);

            SelectObject(memDc, oldBitmap);

            // Read pixel data
            var stride = width * 4;
            var pixelDataSize = stride * height;
            var pixels = new byte[pixelDataSize];
            Marshal.Copy(bits, pixels, 0, pixelDataSize);

            // Write BMP file
            WriteBmp(outputPath, width, height, pixels);

            // Cleanup GDI
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);

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

    private static void WriteBmp(string path, int width, int height, byte[] pixels)
    {
        var stride = width * 4;
        var pixelDataSize = stride * height;
        var headerSize = 14; // BITMAPFILEHEADER
        var infoSize = 40;   // BITMAPINFOHEADER
        var fileSize = headerSize + infoSize + pixelDataSize;

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        // BITMAPFILEHEADER
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write((ushort)0); // reserved1
        bw.Write((ushort)0); // reserved2
        bw.Write(headerSize + infoSize); // pixel data offset

        // BITMAPINFOHEADER (bottom-up for standard BMP)
        bw.Write(infoSize);
        bw.Write(width);
        bw.Write(height); // positive = bottom-up
        bw.Write((ushort)1); // planes
        bw.Write((ushort)32); // bits per pixel
        bw.Write(0); // compression (BI_RGB)
        bw.Write(pixelDataSize);
        bw.Write(0); // X pixels per meter
        bw.Write(0); // Y pixels per meter
        bw.Write(0); // colors used
        bw.Write(0); // important colors

        // Pixel data — flip vertically for bottom-up BMP since we captured top-down
        for (var y = height - 1; y >= 0; y--)
        {
            bw.Write(pixels, y * stride, stride);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int x, int y, int w, int h, IntPtr hdcSrc, int srcX, int srcY, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);
}
