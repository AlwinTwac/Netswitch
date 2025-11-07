using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Netswitch.UI.Services;

public sealed class InternetConnectivityMonitor
{
    private bool _hasInternet = true;
    private DateTimeOffset _lastCheck = DateTimeOffset.MinValue;
    private readonly NotificationSettings _settings;

    public event EventHandler<bool>? ConnectivityChanged;
    public bool HasInternet => _hasInternet;

    public InternetConnectivityMonitor(NotificationSettings settings)
    {
        _settings = settings;
    }

    public async Task<bool> CheckInternetConnectivityAsync()
    {
        // Don't spam checks - minimum 5 seconds between checks
        if ((DateTimeOffset.UtcNow - _lastCheck).TotalSeconds < 5)
            return _hasInternet;

        _lastCheck = DateTimeOffset.UtcNow;

        try
        {
            // Ping Google DNS and Cloudflare DNS
            using var ping = new Ping();
            
            // Try Google DNS first
            var reply1 = await ping.SendPingAsync("8.8.8.8", 3000);
            if (reply1.Status == IPStatus.Success)
            {
                UpdateConnectivity(true);
                return true;
            }

            // Try Cloudflare DNS as backup
            var reply2 = await ping.SendPingAsync("1.1.1.1", 3000);
            if (reply2.Status == IPStatus.Success)
            {
                UpdateConnectivity(true);
                return true;
            }

            // Both failed - no internet
            UpdateConnectivity(false);
            return false;
        }
        catch
        {
            UpdateConnectivity(false);
            return false;
        }
    }

    private void UpdateConnectivity(bool hasInternet)
    {
        if (_hasInternet != hasInternet)
        {
            _hasInternet = hasInternet;
            ConnectivityChanged?.Invoke(this, hasInternet);

            // Show notification if enabled
            if (_settings.AreNotificationsEnabled)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var message = hasInternet
                        ? "Internet connection restored"
                        : "Internet connection lost";

                    var alert = new Core.Models.NetworkConditionAlert(
                        hasInternet ? Core.Models.NetworkCondition.Normal : Core.Models.NetworkCondition.Disconnected,
                        message,
                        DateTimeOffset.UtcNow);

                    var notification = new Windows.NetworkAlertWindow(alert, _settings.IsSoundEnabled);
                    notification.Show();
                });
            }
        }
    }
}
