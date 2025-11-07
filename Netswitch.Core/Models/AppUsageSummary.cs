namespace Netswitch.Core.Models;

/// <summary>
/// Represents a network application's usage metrics for a given interval.
/// </summary>
public sealed record AppUsageSummary(
    string ApplicationName,
    long BytesSent,
    long BytesReceived,
    DateTimeOffset CapturedAt)
{
    public long TotalBytes => BytesSent + BytesReceived;
}
