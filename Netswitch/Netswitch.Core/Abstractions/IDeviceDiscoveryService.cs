using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

/// <summary>
/// Service for discovering and tracking network devices.
/// </summary>
public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Observes device events as they occur (connected, disconnected, updated).
    /// </summary>
    IAsyncEnumerable<DeviceEvent> ObserveDeviceEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all currently known devices.
    /// </summary>
    Task<IReadOnlyList<NetworkDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an immediate network scan for devices.
    /// </summary>
    Task ScanNetworkAsync(CancellationToken cancellationToken = default);
}
