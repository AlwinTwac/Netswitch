using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Options;
using Netswitch.Infrastructure.Alerts;
using Netswitch.Infrastructure.Devices;
using Netswitch.Infrastructure.Latency;
using Netswitch.Infrastructure.Network;
using Netswitch.Infrastructure.SpeedTest;
using Netswitch.Infrastructure.Usage;
using Netswitch.UI.Controllers;
using Netswitch.UI.ViewModels;

namespace Netswitch.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private DashboardCoordinator? _coordinator;
    private AlertCoordinator? _alertCoordinator;
    private MainWindow? _mainWindow;
    private BubbleWindow? _bubbleWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        _host.Start();

        var viewModel = _host.Services.GetRequiredService<DashboardViewModel>();
        _mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        _bubbleWindow = new BubbleWindow
        {
            DataContext = viewModel
        };
        _bubbleWindow.RestoreRequested += HandleBubbleRestoreRequested;
        _bubbleWindow.Hide();

        _mainWindow.StateChanged += HandleMainWindowStateChanged;
        _mainWindow.Closing += (_, _) => _bubbleWindow?.Close();

        _coordinator = _host.Services.GetRequiredService<DashboardCoordinator>();
        _ = _coordinator.StartAsync();

        _alertCoordinator = _host.Services.GetRequiredService<AlertCoordinator>();
        _ = _alertCoordinator.StartAsync();

        _mainWindow.Show();
        _bubbleWindow.SyncWithMainWindow(_mainWindow.WindowState);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_alertCoordinator is not null)
        {
            await _alertCoordinator.DisposeAsync();
        }

        if (_coordinator is not null)
        {
            await _coordinator.DisposeAsync();
        }

        if (_bubbleWindow is not null)
        {
            _bubbleWindow.RestoreRequested -= HandleBubbleRestoreRequested;
            _bubbleWindow.Close();
            _bubbleWindow = null;
        }

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void HandleMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow is null || _bubbleWindow is null)
        {
            return;
        }

        _bubbleWindow.SyncWithMainWindow(_mainWindow.WindowState);
    }

    private void HandleBubbleRestoreRequested(object? sender, EventArgs e)
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MonitoringOptions>();
        services.AddSingleton<SpeedTestOptions>();

        services.AddSingleton<INetworkHealthService, NetworkHealthService>();
        services.AddSingleton<ILatencyMonitor, LatencyMonitor>();
        services.AddSingleton<INetworkUsageCollector, NetworkUsageCollector>();
        services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<IProcessNetworkMonitor, ProcessNetworkMonitor>();

        services.AddHttpClient<ISpeedTestService, SpeedTestService>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DashboardCoordinator>();
        services.AddSingleton<AlertCoordinator>();
    }
}

