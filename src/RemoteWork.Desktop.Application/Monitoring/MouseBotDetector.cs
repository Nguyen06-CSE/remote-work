using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Options;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models.Activity;

namespace RemoteWork.Desktop.Application.Monitoring;

public sealed class MouseBotDetector : IMouseBotDetector
{
    private readonly TrackingOptions _options;
    private readonly ILogger<MouseBotDetector> _logger;

    public MouseBotDetector(
        IOptions<TrackingOptions> options,
        ILogger<MouseBotDetector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public BotDetectionResult Analyze(IReadOnlyList<MouseClickSample> samples)
    {
        var now = DateTimeOffset.UtcNow;

        if (!_options.MouseBotDetectionEnabled || samples.Count < _options.PeriodicityMinSamples)
        {
            return new BotDetectionResult
            {
                IsSuspicious = false,
                Timestamp = now
            };
        }

        var isSuspicious = AnalyzePeriodicity(samples)
                        || AnalyzeFixedArea(samples)
                        || AnalyzeCyclicMultiZone(samples);

        if (isSuspicious)
        {
            // Privacy constraint: Logger CHỈ ghi nhận cờ, KHÔNG in tọa độ hay score
            _logger.LogWarning("Suspicious mouse activity pattern detected.");
        }

        return new BotDetectionResult
        {
            IsSuspicious = isSuspicious,
            Timestamp = now
        };
    }

    /// <summary>
    /// Thuật toán A: Phân tích hệ số biến thiên khoảng cách thời gian (CV = stddev / mean)
    /// </summary>
    private bool AnalyzePeriodicity(IReadOnlyList<MouseClickSample> samples)
    {
        if (samples.Count < _options.PeriodicityMinSamples)
            return false;

        var intervals = new double[samples.Count - 1];
        for (var i = 0; i < samples.Count - 1; i++)
        {
            intervals[i] = samples[i + 1].TimestampMs - samples[i].TimestampMs;
        }

        var mean = intervals.Average();
        if (mean < _options.PeriodicityMinIntervalMs || mean > _options.PeriodicityMaxIntervalMs)
            return false;

        var variance = intervals.Sum(x => Math.Pow(x - mean, 2)) / (intervals.Length - 1);
        var stdDev = Math.Sqrt(variance);
        var cv = stdDev / mean;

        return cv < _options.PeriodicityThresholdCv;
    }

    /// <summary>
    /// Thuật toán B: Phân tích diện tích Bounding Box trong vùng nhỏ kéo dài
    /// </summary>
    private bool AnalyzeFixedArea(IReadOnlyList<MouseClickSample> samples)
    {
        if (samples.Count < _options.PeriodicityMinSamples)
            return false;

        var duration = samples[^1].TimestampMs - samples[0].TimestampMs;
        if (duration < _options.FixedAreaMinDurationMs)
            return false;

        var minX = int.MaxValue;
        var maxX = int.MinValue;
        var minY = int.MaxValue;
        var maxY = int.MinValue;

        for (var i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.X < minX) minX = s.X;
            if (s.X > maxX) maxX = s.X;
            if (s.Y < minY) minY = s.Y;
            if (s.Y > maxY) maxY = s.Y;
        }

        var width = maxX - minX;
        var height = maxY - minY;
        var bboxArea = (long)width * height;

        return bboxArea <= _options.FixedAreaMaxPixels;
    }

    /// <summary>
    /// Thuật toán C: Phân tích chuỗi lặp lại qua các cụm ô lưới (Grid-based Cluster Cycles)
    /// </summary>
    private bool AnalyzeCyclicMultiZone(IReadOnlyList<MouseClickSample> samples)
    {
        if (samples.Count < _options.PeriodicityMinSamples)
            return false;

        var duration = samples[^1].TimestampMs - samples[0].TimestampMs;
        if (duration < 120_000) // Tối thiểu 2 phút quan sát
            return false;

        const int cellSize = 100;
        var clusterMap = new Dictionary<(int, int), int>();
        var sequence = new int[samples.Count];

        for (var i = 0; i < samples.Count; i++)
        {
            var key = (samples[i].X / cellSize, samples[i].Y / cellSize);
            if (!clusterMap.TryGetValue(key, out var clusterId))
            {
                clusterId = clusterMap.Count;
                clusterMap[key] = clusterId;
            }
            sequence[i] = clusterId;
        }

        var uniqueClusters = clusterMap.Count;
        if (uniqueClusters < _options.MultiAreaMinClusters || uniqueClusters > _options.MultiAreaMaxClusters)
            return false;

        return HasRepeatingCycle(sequence, minRepeat: 3);
    }

    private static bool HasRepeatingCycle(int[] sequence, int minRepeat)
    {
        var n = sequence.Length;
        var maxPeriod = n / minRepeat;

        for (var p = 2; p <= maxPeriod; p++)
        {
            var isPeriodic = true;
            for (var i = 0; i < p * (minRepeat - 1); i++)
            {
                if (sequence[i] != sequence[i + p])
                {
                    isPeriodic = false;
                    break;
                }
            }

            if (isPeriodic)
                return true;
        }

        return false;
    }
}