namespace RemoteWork.Desktop.Application.Options;

/// <summary>
/// Configuration options for the offline-first sync engine.
/// </summary>
public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>
    /// Interval in seconds between background sync queue checks when online.
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum number of items to sync in a single batch.
    /// </summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Initial delay in seconds before the first retry attempt.
    /// </summary>
    public int InitialRetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Maximum retry interval in seconds (capping exponential backoff).
    /// </summary>
    public int MaxRetryIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Exponential multiplier for subsequent retry delays.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Maximum number of retry attempts before marking as permanent failure.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 10;
}
