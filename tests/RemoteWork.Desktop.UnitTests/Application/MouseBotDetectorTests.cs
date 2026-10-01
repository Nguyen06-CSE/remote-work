using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Monitoring;
using RemoteWork.Desktop.Application.Options;
using RemoteWork.Desktop.Core.Models.Activity;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public class MouseBotDetectorTests
{
    private static MouseBotDetector CreateDetector(TrackingOptions? options = null)
    {
        var opt = Options.Create(options ?? new TrackingOptions());
        return new MouseBotDetector(opt, NullLogger<MouseBotDetector>.Instance);
    }

    [Fact]
    public void Analyze_WhenDisabled_Should_Return_NotSuspicious()
    {
        var detector = CreateDetector(new TrackingOptions { MouseBotDetectionEnabled = false });

        var samples = new List<MouseClickSample>();
        for (var i = 0; i < 50; i++)
        {
            samples.Add(new MouseClickSample(i * 10000, 100, 100));
        }

        var result = detector.Analyze(samples);

        Assert.False(result.IsSuspicious);
    }

    [Fact]
    public void Analyze_WhenTooFewSamples_Should_Return_NotSuspicious()
    {
        var detector = CreateDetector();

        var samples = new List<MouseClickSample>
        {
            new(0, 100, 100),
            new(10000, 100, 100)
        };

        var result = detector.Analyze(samples);

        Assert.False(result.IsSuspicious);
    }

    [Fact]
    public void Analyze_AlgorithmA_PeriodicClicks_Should_Be_Suspicious()
    {
        var detector = CreateDetector();

        // 40 clicks cách đều chính xác 10,000 ms (CV ≈ 0)
        var samples = new List<MouseClickSample>();
        for (var i = 0; i < 40; i++)
        {
            samples.Add(new MouseClickSample(i * 10000, i * 15, i * 20));
        }

        var result = detector.Analyze(samples);

        Assert.True(result.IsSuspicious);
    }

    [Fact]
    public void Analyze_AlgorithmA_HumanClicks_Should_Not_Be_Suspicious()
    {
        var detector = CreateDetector();

        // Clicks với khoảng cách ngẫu nhiên và biến thiên cao
        var random = new Random(42);
        var samples = new List<MouseClickSample>();
        long current = 0;

        for (var i = 0; i < 40; i++)
        {
            current += random.Next(1000, 15000);
            samples.Add(new MouseClickSample(current, random.Next(100, 1920), random.Next(100, 1080)));
        }

        var result = detector.Analyze(samples);

        Assert.False(result.IsSuspicious);
    }

    [Fact]
    public void Analyze_AlgorithmB_FixedAreaClicks_Should_Be_Suspicious()
    {
        // Tắt thuật toán A bằng cách cho khoảng thời gian biến thiên cao, nhưng giữ vùng click cực nhỏ
        var detector = CreateDetector(new TrackingOptions
        {
            PeriodicityThresholdCv = 0.01 // khắt khe để thuật toán A không bắt
        });

        var random = new Random(42);
        var samples = new List<MouseClickSample>();
        long current = 0;

        for (var i = 0; i < 40; i++)
        {
            current += random.Next(1500, 4000); // tổng duration > 60s
            // click trong hình vuông 30x30 px (diện tích 900 px² < 10,000 px²)
            samples.Add(new MouseClickSample(current, 500 + random.Next(0, 30), 500 + random.Next(0, 30)));
        }

        var result = detector.Analyze(samples);

        Assert.True(result.IsSuspicious);
    }

    [Fact]
    public void Analyze_AlgorithmC_CyclicMultiZone_Should_Be_Suspicious()
    {
        var detector = CreateDetector(new TrackingOptions
        {
            PeriodicityThresholdCv = 0.01 // tránh thuật toán A bắt
        });

        var random = new Random(42);
        var samples = new List<MouseClickSample>();
        long current = 0;

        // 3 cụm tọa độ khác nhau (Grid cells: (1,1), (5,5), (9,9))
        var clusters = new (int X, int Y)[]
        {
            (150, 150),
            (550, 550),
            (950, 950)
        };

        // Lặp chuỗi A-B-C ít nhất 4 lần kéo dài hơn 120s
        for (var cycle = 0; cycle < 14; cycle++)
        {
            for (var c = 0; c < clusters.Length; c++)
            {
                current += random.Next(3000, 6000);
                samples.Add(new MouseClickSample(current, clusters[c].X, clusters[c].Y));
            }
        }

        var result = detector.Analyze(samples);

        Assert.True(result.IsSuspicious);
    }

    [Fact]
    public void Analyze_FastGamingClicks_Should_Not_Be_Suspicious()
    {
        var detector = CreateDetector();

        // Gaming burst click: 50 click cách nhau 100ms (mean = 100ms < 5000ms threshold)
        var samples = new List<MouseClickSample>();
        for (var i = 0; i < 50; i++)
        {
            samples.Add(new MouseClickSample(i * 100, 500, 500));
        }

        var result = detector.Analyze(samples);

        Assert.False(result.IsSuspicious);
    }
}