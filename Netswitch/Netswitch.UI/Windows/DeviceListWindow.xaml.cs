using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Netswitch.Core.Models;

namespace Netswitch.UI.Windows;

public partial class DeviceListWindow : Window
{
    public DeviceListWindow(IEnumerable<NetworkDevice> devices)
    {
        InitializeComponent();
        
        var deviceList = devices.OrderByDescending(d => d.IsOnline).ThenBy(d => d.IpAddress).ToList();
        
        DataContext = new DeviceListViewModel(deviceList);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class DeviceListViewModel
{
    public DeviceListViewModel(List<NetworkDevice> devices)
    {
        Devices = devices;
        OnlineCount = devices.Count(d => d.IsOnline);
        OfflineCount = devices.Count(d => !d.IsOnline);
        TotalCount = devices.Count;
        
        var onlineDevices = devices.Where(d => d.IsOnline && d.LatencyMs > 0).ToList();
        AverageLatency = onlineDevices.Any() 
            ? $"{onlineDevices.Average(d => d.LatencyMs):F0} ms" 
            : "N/A";
    }

    public List<NetworkDevice> Devices { get; }
    public int OnlineCount { get; }
    public int OfflineCount { get; }
    public int TotalCount { get; }
    public string AverageLatency { get; }
    
    public string DeviceCountText => $"{OnlineCount} online, {OfflineCount} offline · Total: {TotalCount} devices";
}
