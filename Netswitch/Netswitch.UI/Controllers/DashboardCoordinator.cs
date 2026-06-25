using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Options;
using Netswitch.UI.Common;
using Netswitch.UI.ViewModels;

namespace Netswitch.UI.Controllers;

public sealed class DashboardCoordinator : IAsyncDisposable
{
    private readonly DashboardViewModel _viewModel;
    private readonly INetworkHealthService _networkHealthService;
    private readonly ILatencyMonitor _latencyMonitor;
    private readonly INetworkUsageCollector _usageCollector;
    private readonly ISpeedTestService _speedTestService;
    private readonly IDeviceDiscoveryService _deviceDiscoveryService;
    private readonly IAlertService _alertService;
    private readonly IProcessNetworkMonitor _processNetworkMonitor;
    private readonly MonitoringOptions _monitoringOptions;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _cts;
    private Task[]? _backgroundTasks;
    private DateTimeOffset _lastUsageUpdate = DateTimeOffset.UtcNow;

    public DashboardCoordinator(
        DashboardViewModel viewModel,
        INetworkHealthService networkHealthService,
        ILatencyMonitor latencyMonitor,
        INetworkUsageCollector usageCollector,
        ISpeedTestService speedTestService,
        IDeviceDiscoveryService deviceDiscoveryService,
        IAlertService alertService,
        IProcessNetworkMonitor processNetworkMonitor,
        MonitoringOptions monitoringOptions)
    {
        _viewModel = viewModel;
        _networkHealthService = networkHealthService;
        _latencyMonitor = latencyMonitor;
        _usageCollector = usageCollector;
        _speedTestService = speedTestService;
        _deviceDiscoveryService = deviceDiscoveryService;
        _alertService = alertService;
        _processNetworkMonitor = processNetworkMonitor;
        _monitoringOptions = monitoringOptions;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        ConfigureCommands();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        _backgroundTasks = new[]
        {
            Task.Run(() => ObserveNetworkAsync(token), token),
            Task.Run(() => ObserveLatencyAsync(token), token),
            Task.Run(() => ObserveUsageAsync(token), token),
            Task.Run(() => ObserveDevicesAsync(token), token),
            Task.Run(() => ObserveAlertsAsync(token), token),
            Task.Run(() => ObserveProcessNetworkAsync(token), token),
            Task.Run(() => UpdateSessionTimerAsync(token), token)
        };

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        try
        {
            _cts.Cancel();
            if (_backgroundTasks is not null)
            {
                await Task.WhenAll(_backgroundTasks).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down.
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _backgroundTasks = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task ObserveNetworkAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var status in _networkHealthService.ObserveStatusAsync(cancellationToken).ConfigureAwait(false))
            {
                Dispatch(() => _viewModel.NetworkStatus = status);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ObserveLatencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in _latencyMonitor.ObserveLatencyAsync(cancellationToken).ConfigureAwait(false))
            {
                Dispatch(() => _viewModel.LatencySnapshot = snapshot);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ObserveUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var entries in _usageCollector.ObserveUsageAsync(cancellationToken).ConfigureAwait(false))
            {
                var now = DateTimeOffset.UtcNow;
                var interval = now - _lastUsageUpdate;
                _lastUsageUpdate = now;
                Dispatch(() => _viewModel.UpdateUsage(entries, interval));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ObserveDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Periodically refresh device list
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var devices = await _deviceDiscoveryService.GetDevicesAsync(cancellationToken);
                Dispatch(() => _viewModel.UpdateDevices(devices));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ObserveAlertsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var alert in _alertService.ObserveAlertsAsync(cancellationToken).ConfigureAwait(false))
            {
                Dispatch(() => _viewModel.AddAlert(alert));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunSpeedTestAsync(object? parameter)
    {
        if (_cts is null)
        {
            return;
        }

        var token = _cts.Token;
        Dispatch(() => _viewModel.IsSpeedTestRunning = true);

        try
        {
            var result = await _speedTestService.RunSpeedTestAsync(token).ConfigureAwait(false);
            Dispatch(() => _viewModel.LastSpeedTest = result);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Dispatch(() => _viewModel.IsSpeedTestRunning = false);
        }
    }

    private async Task ObserveProcessNetworkAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5)); // Update every 5 seconds

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var processUsages = await _processNetworkMonitor.GetProcessNetworkUsageAsync(cancellationToken);
                var now = DateTimeOffset.UtcNow;
                
                // Convert to AppUsageSummary for display
                var appUsages = processUsages
                    .Select(p => new Core.Models.AppUsageSummary(
                        ApplicationName: p.ProcessName,
                        BytesSent: p.BytesSent,
                        BytesReceived: p.BytesReceived,
                        CapturedAt: now))
                    .Take(15) // Show top 15
                    .ToList();

                Dispatch(() =>
                {
                    _viewModel.TopApplications.Clear();
                    foreach (var app in appUsages)
                    {
                        _viewModel.TopApplications.Add(app);
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
    }

    private async Task UpdateSessionTimerAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1)); // Update every second

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                Dispatch(() => _viewModel.UpdateSessionTime());
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
    }

    private void ConfigureCommands()
    {
        _viewModel.RunSpeedTestCommand = new RelayCommand(
            RunSpeedTestAsync,
            _ => !_viewModel.IsSpeedTestRunning);
            
        _viewModel.ShowDeviceListCommand = new RelayCommand(
            _ => ShowDeviceList(),
            _ => true);
    }

    private void ShowDeviceList()
    {
        Dispatch(() =>
        {
            var deviceListWindow = new Windows.DeviceListWindow(_viewModel.ConnectedDevices);
            deviceListWindow.Owner = System.Windows.Application.Current.MainWindow;
            deviceListWindow.ShowDialog();
        });
    }

    private void Dispatch(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action);
    }
}
