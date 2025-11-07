using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;

namespace Netswitch.UI.Controllers;

/// <summary>
/// Coordinates alert generation based on network events, device events, and system status.
/// </summary>
public sealed class AlertCoordinator : IAsyncDisposable
{
    private readonly IAlertService _alertService;
    private readonly INetworkHealthService _networkHealthService;
    private readonly ILatencyMonitor _latencyMonitor;
    private readonly IDeviceDiscoveryService _deviceDiscoveryService;
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private NetworkStatus? _lastNetworkStatus;
    private LatencySnapshot? _lastLatencySnapshot;

    public AlertCoordinator(
        IAlertService alertService,
        INetworkHealthService networkHealthService,
        ILatencyMonitor latencyMonitor,
        IDeviceDiscoveryService deviceDiscoveryService)
    {
        _alertService = alertService;
        _networkHealthService = networkHealthService;
        _latencyMonitor = latencyMonitor;
        _deviceDiscoveryService = deviceDiscoveryService;
    }

    public Task StartAsync()
    {
        if (_monitoringTask is not null)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _monitoringTask = Task.WhenAll(
            MonitorNetworkStatusAsync(_cts.Token),
            MonitorLatencyAsync(_cts.Token),
            MonitorDeviceEventsAsync(_cts.Token)
        );

        return Task.CompletedTask;
    }

    private async Task MonitorNetworkStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var status in _networkHealthService.ObserveStatusAsync(cancellationToken))
            {
                // Alert on network status changes
                if (_lastNetworkStatus is not null && _lastNetworkStatus.IsOnline != status.IsOnline)
                {
                    var alert = new NetworkAlert(
                        Severity: status.IsOnline ? AlertSeverity.Info : AlertSeverity.Critical,
                        Title: status.IsOnline ? "Network Online" : "Network Offline",
                        Message: status.Description,
                        CreatedAt: DateTimeOffset.UtcNow,
                        Category: AlertCategory.NetworkStatus
                    );

                    await _alertService.SendAlertAsync(alert, cancellationToken);
                }

                _lastNetworkStatus = status;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task MonitorLatencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in _latencyMonitor.ObserveLatencyAsync(cancellationToken))
            {
                // Alert on high latency
                var latencyMs = snapshot.RoundTripTime.TotalMilliseconds;
                if (latencyMs > 200 && latencyMs < TimeSpan.MaxValue.TotalMilliseconds)
                {
                    var lastLatencyMs = _lastLatencySnapshot?.RoundTripTime.TotalMilliseconds ?? 0;
                    var isNewHighLatency = lastLatencyMs <= 200;

                    if (isNewHighLatency)
                    {
                        var alert = new NetworkAlert(
                            Severity: AlertSeverity.Warning,
                            Title: "High Network Latency",
                            Message: $"Current latency: {latencyMs:F0}ms to router",
                            CreatedAt: DateTimeOffset.UtcNow,
                            Category: AlertCategory.Performance
                        );

                        await _alertService.SendAlertAsync(alert, cancellationToken);
                    }
                }

                _lastLatencySnapshot = snapshot;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task MonitorDeviceEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var deviceEvent in _deviceDiscoveryService.ObserveDeviceEventsAsync(cancellationToken))
            {
                var alert = deviceEvent.EventType switch
                {
                    DeviceEventType.DeviceConnected => new NetworkAlert(
                        Severity: AlertSeverity.Info,
                        Title: "New Device Connected",
                        Message: deviceEvent.Message,
                        CreatedAt: deviceEvent.OccurredAt,
                        Category: AlertCategory.DeviceActivity
                    ),
                    DeviceEventType.DeviceDisconnected => new NetworkAlert(
                        Severity: AlertSeverity.Warning,
                        Title: "Device Disconnected",
                        Message: deviceEvent.Message,
                        CreatedAt: deviceEvent.OccurredAt,
                        Category: AlertCategory.DeviceActivity
                    ),
                    DeviceEventType.HighLatency => new NetworkAlert(
                        Severity: AlertSeverity.Warning,
                        Title: "Device High Latency",
                        Message: deviceEvent.Message,
                        CreatedAt: deviceEvent.OccurredAt,
                        Category: AlertCategory.Performance
                    ),
                    _ => null
                };

                if (alert is not null)
                {
                    await _alertService.SendAlertAsync(alert, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        if (_monitoringTask is not null)
        {
            try
            {
                await _monitoringTask;
            }
            catch
            {
                // Ignore cancellation exceptions
            }
            _monitoringTask = null;
        }
    }
}
