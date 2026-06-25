namespace Netswitch.Core.Models;

/// <summary>
/// Represents a network alert notification.
/// </summary>
public sealed record NetworkAlert(
    AlertSeverity Severity,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    AlertCategory Category);

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    High,
    Error,
    Critical
}

/// <summary>
/// Alert categories for filtering and grouping.
/// </summary>
public enum AlertCategory
{
    NetworkStatus,
    DeviceActivity,
    Performance,
    SpeedTest,
    Security
}
