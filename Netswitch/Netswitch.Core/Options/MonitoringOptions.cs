namespace Netswitch.Core.Options;

/// <summary>
/// Configurable intervals for background monitoring services.
/// </summary>
public sealed class MonitoringOptions
{
    public TimeSpan NetworkPollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan LatencyPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan UsageAggregationInterval { get; set; } = TimeSpan.FromSeconds(10);

    public int PingTimeoutMilliseconds { get; set; } = 2000;
}

/// <summary>
/// Configuration for the built-in speed test service.
/// </summary>
public sealed class SpeedTestOptions
{
    /// <summary>
    /// URL of a file to download for measuring downstream throughput.
    /// </summary>
    public string DownloadUrl { get; set; } = "https://speed.cloudflare.com/__down?bytes=5000000";

    /// <summary>
    /// Number of bytes expected from the download target. Used for validation only.
    /// </summary>
    public int ExpectedDownloadBytes { get; set; } = 5_000_000;

    /// <summary>
    /// Endpoint that accepts POST requests with arbitrary binary payload for upstream measurement.
    /// </summary>
    public string UploadUrl { get; set; } = "https://httpbin.org/post";

    /// <summary>
    /// Size of the payload in bytes to upload when running the speed test.
    /// </summary>
    public int UploadBytes { get; set; } = 1_000_000;

    /// <summary>
    /// Host to target for latency measurement as part of the speed test.
    /// </summary>
    public string LatencyHost { get; set; } = "8.8.8.8";

    public int TimeoutSeconds { get; set; } = 60;
    
    public int LatencyTimeoutMilliseconds { get; set; } = 5000;
}
