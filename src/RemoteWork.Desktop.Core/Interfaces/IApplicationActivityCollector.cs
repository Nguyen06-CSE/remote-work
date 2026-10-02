using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

/// <summary>
/// State-tracking collector for monitoring active application changes.
/// Compares consecutive samples to detect when the active application transitions
/// (e.g. App A -> App B or App A -> None) and produces an <see cref="ApplicationActivity"/>
/// record with the exact wall-clock start time and duration of the completed application.
/// </summary>
public interface IApplicationActivityCollector
{
    /// <summary>
    /// Samples the currently active application.
    /// Returns a completed <see cref="ApplicationActivity"/> if an application transition occurred,
    /// or null if the same application remains active or no application was previously tracked.
    /// </summary>
    /// <param name="deviceId">The stable device ID.</param>
    /// <param name="sessionId">The current session ID, or null if unassigned.</param>
    /// <param name="now">Optional timestamp for testing wall-clock calculations.</param>
    /// <returns>A completed <see cref="ApplicationActivity"/> for the previous application, or null.</returns>
    ApplicationActivity? Sample(string deviceId, string? sessionId, DateTimeOffset? now = null);

    /// <summary>
    /// Flushes any active application span currently in progress (e.g. on session termination or graceful shutdown).
    /// </summary>
    /// <param name="deviceId">The stable device ID.</param>
    /// <param name="sessionId">The current session ID, or null if unassigned.</param>
    /// <param name="now">Optional timestamp for testing wall-clock calculations.</param>
    /// <returns>The finalized <see cref="ApplicationActivity"/>, or null if no application is active.</returns>
    ApplicationActivity? Flush(string deviceId, string? sessionId, DateTimeOffset? now = null);
}
