using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;

namespace Netswitch.Infrastructure.Alerts;

/// <summary>
/// Manages network alerts and notifications with Windows toast support.
/// </summary>
public sealed class AlertService : IAlertService
{
    private readonly ConcurrentQueue<NetworkAlert> _recentAlerts = new();
    private readonly Channel<NetworkAlert> _alertChannel = Channel.CreateUnbounded<NetworkAlert>();
    private const int MaxRecentAlerts = 100;

    public async IAsyncEnumerable<NetworkAlert> ObserveAlertsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var alert in _alertChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return alert;
        }
    }

    public async Task SendAlertAsync(NetworkAlert alert, CancellationToken cancellationToken = default)
    {
        // Add to recent alerts queue
        _recentAlerts.Enqueue(alert);
        
        // Maintain max size
        while (_recentAlerts.Count > MaxRecentAlerts)
        {
            _recentAlerts.TryDequeue(out _);
        }

        // Send to observers
        await _alertChannel.Writer.WriteAsync(alert, cancellationToken);

        // Show Windows notification for important alerts
        if (alert.Severity >= AlertSeverity.Warning)
        {
            ShowWindowsNotification(alert);
        }
    }

    public Task<IReadOnlyList<NetworkAlert>> GetRecentAlertsAsync(
        TimeSpan window, 
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - window;
        var alerts = _recentAlerts
            .Where(a => a.CreatedAt >= cutoff)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<NetworkAlert>>(alerts);
    }

    private static void ShowWindowsNotification(NetworkAlert alert)
    {
        try
        {
            // For Windows 10/11 toast notifications, we can use the Windows.UI.Notifications API
            // For now, we'll use a simple approach. In production, integrate with:
            // - Microsoft.Toolkit.Uwp.Notifications for rich toast notifications
            // - Windows.UI.Notifications for native Windows notifications
            
            var iconPath = alert.Severity switch
            {
                AlertSeverity.Critical => "ms-appx:///Assets/error.png",
                AlertSeverity.Error => "ms-appx:///Assets/error.png",
                AlertSeverity.Warning => "ms-appx:///Assets/warning.png",
                _ => "ms-appx:///Assets/info.png"
            };

            // Placeholder for toast notification
            // In production, use ToastContentBuilder from Microsoft.Toolkit.Uwp.Notifications
            System.Diagnostics.Debug.WriteLine($"[ALERT] {alert.Severity}: {alert.Title} - {alert.Message}");
        }
        catch
        {
            // Notification failed - don't crash the application
        }
    }
}
