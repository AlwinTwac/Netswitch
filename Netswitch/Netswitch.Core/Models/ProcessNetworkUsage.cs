namespace Netswitch.Core.Models;

/// <summary>
/// Represents network usage by a specific process/application.
/// </summary>
public sealed record ProcessNetworkUsage(
    string ProcessName,
    int ProcessId,
    long BytesSent,
    long BytesReceived,
    int ConnectionCount);
