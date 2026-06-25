using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;
using Netswitch.UI.Common;

namespace Netswitch.UI.ViewModels;

public sealed class SecurityDashboardViewModel : ObservableObject
{
    private readonly ISecurityMonitorService _securityService;
    private readonly IDeviceDiscoveryService _deviceService;
    private readonly INetworkHistoryService _historyService;
    
    private int _trustedCount;
    private int _activeThreats;
    private string _overallStatus = "Secure";
    private RelayCommand _trustDeviceCommand = RelayCommand.NoOp;
    private RelayCommand _revokeTrustCommand = RelayCommand.NoOp;
    private RelayCommand _exportLogsCommand = RelayCommand.NoOp;

    public SecurityDashboardViewModel(
        ISecurityMonitorService securityService,
        IDeviceDiscoveryService deviceService,
        INetworkHistoryService historyService)
    {
        _securityService = securityService;
        _deviceService = deviceService;
        _historyService = historyService;

        TrustDeviceCommand = new RelayCommand(ExecuteTrustDeviceAsync);
        RevokeTrustCommand = new RelayCommand(ExecuteRevokeTrustAsync);
        ExportLogsCommand = new RelayCommand(ExecuteExportLogsAsync);
    }

    public int TrustedCount
    {
        get => _trustedCount;
        set => SetProperty(ref _trustedCount, value);
    }

    public int ActiveThreats
    {
        get => _activeThreats;
        set => SetProperty(ref _activeThreats, value);
    }

    public string OverallStatus
    {
        get => _overallStatus;
        set => SetProperty(ref _overallStatus, value);
    }

    public BatchObservableCollection<NetworkDevice> UntrustedDevices { get; } = new();
    public BatchObservableCollection<NetworkDevice> TrustedDevices { get; } = new();
    public BatchObservableCollection<NetworkAlert> SecurityAlerts { get; } = new();

    public RelayCommand TrustDeviceCommand
    {
        get => _trustDeviceCommand;
        set => SetProperty(ref _trustDeviceCommand, value);
    }

    public RelayCommand RevokeTrustCommand
    {
        get => _revokeTrustCommand;
        set => SetProperty(ref _revokeTrustCommand, value);
    }

    public RelayCommand ExportLogsCommand
    {
        get => _exportLogsCommand;
        set => SetProperty(ref _exportLogsCommand, value);
    }

    private async Task ExecuteExportLogsAsync(object? parameter)
    {
        try
        {
            var timeframe = parameter?.ToString() switch
            {
                "Hour" => TimeSpan.FromHours(1),
                "Day" => TimeSpan.FromDays(1),
                "Week" => TimeSpan.FromDays(7),
                _ => TimeSpan.FromDays(30)
            };

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktop, $"NetworkExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            await _historyService.ExportToCsvAsync(filePath, timeframe);
            
            System.Windows.MessageBox.Show($"Exported successfully to {filePath}", "Export Complete", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task ExecuteTrustDeviceAsync(object? parameter)
    {
        if (parameter is NetworkDevice device && device.MacAddress is not null)
        {
            await _securityService.TrustDeviceAsync(device.MacAddress);
            await RefreshDevicesAsync();
        }
    }

    private async Task ExecuteRevokeTrustAsync(object? parameter)
    {
        if (parameter is NetworkDevice device && device.MacAddress is not null)
        {
            await _securityService.RevokeTrustAsync(device.MacAddress);
            await RefreshDevicesAsync();
        }
    }

    public async Task RefreshDevicesAsync()
    {
        var allDevices = await _deviceService.GetDevicesAsync();
        var trustedMacs = await _securityService.GetTrustedDevicesAsync();

        var trustedList = new List<NetworkDevice>();
        var untrustedList = new List<NetworkDevice>();

        foreach (var device in allDevices)
        {
            if (device.MacAddress is not null && trustedMacs.Contains(device.MacAddress))
            {
                trustedList.Add(device);
            }
            else
            {
                untrustedList.Add(device);
            }
        }

        TrustedDevices.ReplaceAll(trustedList);
        UntrustedDevices.ReplaceAll(untrustedList);

        TrustedCount = trustedList.Count;
        ActiveThreats = untrustedList.Count; // Simplistic metric

        if (ActiveThreats > 0)
        {
            OverallStatus = "At Risk";
        }
        else
        {
            OverallStatus = "Secure";
        }
    }

    public void AddSecurityAlert(NetworkAlert alert)
    {
        SecurityAlerts.Insert(0, alert);
        if (alert.Severity >= AlertSeverity.High)
        {
            ActiveThreats++;
            OverallStatus = "At Risk";
        }
    }
}
