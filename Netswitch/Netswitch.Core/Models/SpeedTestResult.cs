namespace Netswitch.Core.Models;

/// <summary>
/// Represents the results of a bandwidth speed test.
/// </summary>
public sealed record SpeedTestResult(
    double DownloadMbps,
    double UploadMbps,
    TimeSpan Latency,
    DateTimeOffset CompletedAt);
