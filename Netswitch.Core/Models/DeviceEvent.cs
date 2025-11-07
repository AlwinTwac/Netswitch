namespace Netswitch.Core.Models;

/// <summary>
/// Represents an event that occurred with a network device.
/// </summary>
public sealed record DeviceEvent(
    DeviceEventType EventType,
    NetworkDevice Device,
    DateTimeOffset OccurredAt,
    string Message);

/// <summary>
/// Types of device events.
/// </summary>
public enum DeviceEventType
{
    DeviceConnected,
    DeviceDisconnected,
    DeviceUpdated,
    HighLatency,
    HighUsage
}
