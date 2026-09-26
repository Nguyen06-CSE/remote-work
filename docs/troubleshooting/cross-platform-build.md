# Troubleshooting Guide: Cross-Platform Build & Execution

## 1. Win32 P/Invoke Calls on Non-Windows Platforms

### Problem
Invoking Windows Win32 APIs (such as `user32.dll` or `kernel32.dll` P/Invoke functions) on macOS or Linux throws `DllNotFoundException` or `PlatformNotSupportedException`.

### Solution
- All Windows P/Invoke calls are strictly contained in `RemoteWork.Desktop.Platform.Windows`.
- The Host composition root in `RemoteWork.Desktop.Host/Program.cs` conditionally registers platform providers based on `RuntimeInformation.IsOSPlatform`:
  - `OSPlatform.Windows` registers `WindowsPlatformServices`.
  - `OSPlatform.OSX` registers `MacOsPlatformServices`.
  - `OSPlatform.Linux` registers `LinuxPlatformServices`.
- Providers check `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` before initiating thread hooks.

---

## 2. Solution Format Compatibility (.sln vs .slnx)

### Problem
.NET 10 SDK introduces the XML-based `.slnx` solution format by default. Older IDEs or tools may expect the traditional `.sln` format.

### Solution
The repository maintains both `RemoteWork.Desktop.sln` (traditional text format) and `RemoteWork.Desktop.slnx` (XML format), allowing seamless development across all IDEs (Visual Studio 2022, Visual Studio Code, JetBrains Rider, and `dotnet` CLI).
