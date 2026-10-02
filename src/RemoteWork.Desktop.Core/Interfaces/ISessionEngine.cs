using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface ISessionEngine
{
    /// <summary>
    /// Starts a new session associated with the specified device ID.
    /// </summary>
    /// <param name="deviceId">The stable device ID.</param>
    /// <returns>The created <see cref="SessionInfo"/> in Active state.</returns>
    SessionInfo StartSession(string deviceId);

    /// <summary>
    /// Ends the current active session, recording the end time and calculating total duration.
    /// </summary>
    /// <returns>The ended <see cref="SessionInfo"/>, or null if no session was active.</returns>
    SessionInfo? EndSession();

    /// <summary>
    /// Gets the current session, or null if no session is active.
    /// </summary>
    /// <returns>The active <see cref="SessionInfo"/>, or null.</returns>
    SessionInfo? GetCurrentSession();
}
