using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Core.Configuration;

namespace RemoteWork.Desktop.Infrastructure.Configuration;

public sealed class DesktopConfigurationValidator : IValidateOptions<DesktopConfiguration>
{
    public ValidateOptionsResult Validate(string? name, DesktopConfiguration options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApplicationVersion))
        {
            failures.Add("ApplicationVersion must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.BackendBaseUrl) ||
            !Uri.TryCreate(options.BackendBaseUrl, UriKind.Absolute, out var uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"BackendBaseUrl '{options.BackendBaseUrl}' must be a valid HTTP or HTTPS absolute URL.");
        }

        if (options.HeartbeatIntervalSeconds <= 0)
        {
            failures.Add($"HeartbeatIntervalSeconds ({options.HeartbeatIntervalSeconds}) must be greater than 0.");
        }

        if (options.ActivitySamplingIntervalSeconds <= 0)
        {
            failures.Add($"ActivitySamplingIntervalSeconds ({options.ActivitySamplingIntervalSeconds}) must be greater than 0.");
        }

        if (options.ActivityBatchIntervalSeconds < options.ActivitySamplingIntervalSeconds)
        {
            failures.Add($"ActivityBatchIntervalSeconds ({options.ActivityBatchIntervalSeconds}) must be greater than or equal to ActivitySamplingIntervalSeconds ({options.ActivitySamplingIntervalSeconds}).");
        }

        if (options.ScreenshotDefaults.Enabled && options.ScreenshotDefaults.IntervalSeconds <= 0)
        {
            failures.Add($"ScreenshotDefaults.IntervalSeconds ({options.ScreenshotDefaults.IntervalSeconds}) must be greater than 0 when screenshots are enabled.");
        }

        if (options.ScreenshotDefaults.Quality is < 1 or > 100)
        {
            failures.Add($"ScreenshotDefaults.Quality ({options.ScreenshotDefaults.Quality}) must be between 1 and 100.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}
