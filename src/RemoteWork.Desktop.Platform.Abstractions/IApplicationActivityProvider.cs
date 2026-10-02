namespace RemoteWork.Desktop.Platform.Abstractions;

/// <summary>
/// Platform-specific provider for querying the currently active / foreground application.
/// </summary>
public interface IApplicationActivityProvider
{
    /// <summary>
    /// Gets a snapshot of the currently focused foreground application, or null if no application has focus.
    /// </summary>
    ActiveApplicationInfo? GetActiveApplication();
}
