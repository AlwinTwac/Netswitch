namespace Netswitch.Core.Models;

/// <summary>
/// Represents the most recent router latency measurement.
/// </summary>
public sealed record LatencySnapshot(
    TimeSpan RoundTripTime,
    LatencyQuality Quality,
    DateTimeOffset CapturedAt);
