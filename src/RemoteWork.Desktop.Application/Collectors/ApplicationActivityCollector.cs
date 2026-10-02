using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application.Collectors;

/// <summary>
/// Collects and aggregates application focus changes into discrete <see cref="ApplicationActivity"/> spans.
/// Compares consecutive samples to prevent redundant record generation while the same application remains active.
/// Calculates accurate wall-clock durations (avoiding cumulative timer drift).
/// Strictly adheres to privacy constraints: zero detailed content, zero window titles, zero browser URLs.
/// </summary>
public sealed class ApplicationActivityCollector : IApplicationActivityCollector
{
    private readonly IApplicationActivityProvider _provider;
    private readonly ILogger<ApplicationActivityCollector> _logger;
    private readonly object _lock = new();

    private ActiveApplicationInfo? _currentApp;
    private DateTimeOffset? _currentAppStartedAt;

    public ApplicationActivityCollector(
        IApplicationActivityProvider provider,
        ILogger<ApplicationActivityCollector> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public ApplicationActivity? Sample(string deviceId, string? sessionId, DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.UtcNow;
        ActiveApplicationInfo? newApp = null;

        try
        {
            newApp = _provider.GetActiveApplication();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query active application from platform provider.");
            newApp = null; // Treat provider failure as no active application
        }

        lock (_lock)
        {
            // Case 1: Same application remains active -> No redundant record generated
            if (IsSameApplication(_currentApp, newApp))
            {
                if (_currentApp is not null)
                {
                    _logger.LogDebug("Active application unchanged: {AppName} (PID: {Pid})", _currentApp.ApplicationName, _currentApp.ProcessId);
                }
                return null;
            }

            // Case 2: Transition occurred
            _logger.LogInformation(
                "Active application transition detected: from '{FromApp}' ({FromPid}) to '{ToApp}' ({ToPid})",
                _currentApp?.ApplicationName ?? "None",
                _currentApp?.ProcessId ?? 0,
                newApp?.ApplicationName ?? "None",
                newApp?.ProcessId ?? 0);

            ApplicationActivity? completedActivity = null;

            if (_currentApp is not null && _currentAppStartedAt is not null)
            {
                var duration = currentTime - _currentAppStartedAt.Value;
                if (duration < TimeSpan.Zero)
                {
                    duration = TimeSpan.Zero;
                }

                completedActivity = CreateActivity(deviceId, sessionId, _currentApp, _currentAppStartedAt.Value, duration);
            }

            // Set new active application state
            if (newApp is not null)
            {
                _currentApp = newApp;
                _currentAppStartedAt = currentTime;
            }
            else
            {
                _currentApp = null;
                _currentAppStartedAt = null;
            }

            return completedActivity;
        }
    }

    public ApplicationActivity? Flush(string deviceId, string? sessionId, DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (_currentApp is null || _currentAppStartedAt is null)
            {
                return null;
            }

            var duration = currentTime - _currentAppStartedAt.Value;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            var completedActivity = CreateActivity(deviceId, sessionId, _currentApp, _currentAppStartedAt.Value, duration);

            _currentApp = null;
            _currentAppStartedAt = null;

            return completedActivity;
        }
    }

    private static ApplicationActivity CreateActivity(
        string deviceId,
        string? sessionId,
        ActiveApplicationInfo app,
        DateTimeOffset startedAt,
        TimeSpan duration)
    {
        var appName = SanitizeMetadata(app.ApplicationName, app.ProcessName, "Unknown Application");
        var procName = SanitizeMetadata(app.ProcessName, app.ApplicationName, "Unknown Process");

        return new ApplicationActivity
        {
            ActivityId = Guid.NewGuid().ToString("D"),
            DeviceId = deviceId,
            SessionId = sessionId,
            Timestamp = startedAt,
            ApplicationName = appName,
            ProcessName = procName,
            ProcessId = app.ProcessId > 0 ? app.ProcessId : 0,
            WindowTitle = null, // Privacy: deliberately omitted to prevent leaking documents, URLs, or queries
            Duration = duration
        };
    }

    private static bool IsSameApplication(ActiveApplicationInfo? a, ActiveApplicationInfo? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        if (a.ProcessId > 0 && b.ProcessId > 0 && a.ProcessId == b.ProcessId)
        {
            return true;
        }

        return string.Equals(a.ApplicationName, b.ApplicationName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeMetadata(string? primary, string? fallback, string defaultName)
    {
        if (!string.IsNullOrWhiteSpace(primary))
            return primary.Trim();

        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback.Trim();

        return defaultName;
    }
}
