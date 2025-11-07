namespace Netswitch.Core.Models;

/// <summary>
/// Represents a network device discovered on the local network.
/// </summary>
public sealed record NetworkDevice(
    string IpAddress,
    string? MacAddress,
    string? HostName,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    bool IsOnline,
    long BytesSent,
    long BytesReceived,
    int? LatencyMs);
