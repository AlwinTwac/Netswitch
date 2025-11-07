using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Netswitch.Core.Models;

namespace Netswitch.UI.Services;

public sealed class DeviceNotificationService
{
    private readonly HashSet<string> _knownDevices = new();
    private readonly HashSet<string> _ignoredDevices = new(); // Router, gateway, etc.
    private readonly object _lock = new();
    private readonly NotificationSettings _settings = new();

    public NotificationSettings Settings => _settings;

    public void Initialize(IEnumerable<NetworkDevice> initialDevices)
    {
        lock (_lock)
        {
            _knownDevices.Clear();
            _ignoredDevices.Clear();
            
            var deviceList = initialDevices.ToList();
            
            // Identify and ignore gateway/router (usually first device or .1/.254)
            foreach (var device in deviceList)
            {
                if (device.IsOnline)
                {
                    _knownDevices.Add(device.IpAddress);
                    
                    // Ignore gateway/router addresses
                    if (IsLikelyGateway(device.IpAddress))
                    {
                        _ignoredDevices.Add(device.IpAddress);
                    }
                }
            }
        }
    }

    public void ProcessDeviceUpdate(NetworkDevice device)
    {
        if (!_settings.AreNotificationsEnabled)
            return;
            
        lock (_lock)
        {
            // Skip notifications for ignored devices (router/gateway)
            if (_ignoredDevices.Contains(device.IpAddress))
                return;
                
            var wasKnown = _knownDevices.Contains(device.IpAddress);

            if (device.IsOnline && !wasKnown)
            {
                // New device connected
                _knownDevices.Add(device.IpAddress);
                ShowNotification(device, isConnected: true);
            }
            else if (!device.IsOnline && wasKnown)
            {
                // Device disconnected
                _knownDevices.Remove(device.IpAddress);
                ShowNotification(device, isConnected: false);
            }
        }
    }

    private void ShowNotification(NetworkDevice device, bool isConnected)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var notification = new Windows.DeviceNotificationWindow(device, isConnected, _settings.IsSoundEnabled);
            notification.Show();
        });
    }

    private static bool IsLikelyGateway(string ipAddress)
    {
        // Common gateway patterns: x.x.x.1, x.x.x.254, x.x.x.0
        var parts = ipAddress.Split('.');
        if (parts.Length != 4)
            return false;
            
        var lastOctet = parts[3];
        return lastOctet is "1" or "254" or "0";
    }
}
