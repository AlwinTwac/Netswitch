using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Netswitch.Core.Models;

namespace Netswitch.UI.Services;

public sealed class NetworkConditionMonitor
{
    private readonly List<double> _recentLatencies = new();
    private readonly object _lock = new();
    private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
    private readonly NotificationSettings _settings;
    private NetworkCondition _lastCondition = NetworkCondition.Normal;

    public NetworkConditionMonitor(NotificationSettings settings)
    {
        _settings = settings;
    }

    public void ProcessLatency(double latencyMs, bool isOnline)
    {
        if (!_settings.AreNotificationsEnabled)
            return;

        lock (_lock)
        {
            // Don't spam alerts - minimum 30 seconds between alerts
            if ((DateTimeOffset.UtcNow - _lastAlertTime).TotalSeconds < 30)
                return;

            if (!isOnline)
            {
                if (_lastCondition != NetworkCondition.Disconnected)
                {
                    _lastCondition = NetworkCondition.Disconnected;
                    ShowAlert(new NetworkConditionAlert(
                        NetworkCondition.Disconnected,
                        "Network connection lost. Please check your network adapter.",
                        DateTimeOffset.UtcNow));
                }
                return;
            }

            // Track recent latencies
            _recentLatencies.Add(latencyMs);
            if (_recentLatencies.Count > 10)
            {
                _recentLatencies.RemoveAt(0);
            }

            // Need at least 5 samples
            if (_recentLatencies.Count < 5)
                return;

            var avgLatency = _recentLatencies.Average();
            var variance = _recentLatencies.Select(l => Math.Pow(l - avgLatency, 2)).Average();
            var stdDev = Math.Sqrt(variance);

            // Check for high latency
            if (avgLatency > 200)
            {
                if (_lastCondition != NetworkCondition.HighLatency)
                {
                    _lastCondition = NetworkCondition.HighLatency;
                    ShowAlert(new NetworkConditionAlert(
                        NetworkCondition.HighLatency,
                        "Network latency is very high. Your connection may be slow.",
                        DateTimeOffset.UtcNow,
                        LatencyMs: avgLatency));
                }
            }
            // Check for unstable connection (high variance)
            else if (stdDev > 50 && avgLatency > 50)
            {
                if (_lastCondition != NetworkCondition.Unstable)
                {
                    _lastCondition = NetworkCondition.Unstable;
                    ShowAlert(new NetworkConditionAlert(
                        NetworkCondition.Unstable,
                        "Network connection is unstable. Latency is fluctuating significantly.",
                        DateTimeOffset.UtcNow,
                        LatencyMs: avgLatency));
                }
            }
            // Check for slow connection
            else if (avgLatency > 100)
            {
                if (_lastCondition != NetworkCondition.Slow)
                {
                    _lastCondition = NetworkCondition.Slow;
                    ShowAlert(new NetworkConditionAlert(
                        NetworkCondition.Slow,
                        "Network is slower than usual. You may experience delays.",
                        DateTimeOffset.UtcNow,
                        LatencyMs: avgLatency));
                }
            }
            else
            {
                // Reset to normal
                _lastCondition = NetworkCondition.Normal;
            }
        }
    }

    private void ShowAlert(NetworkConditionAlert alert)
    {
        _lastAlertTime = DateTimeOffset.UtcNow;
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            var notification = new Windows.NetworkAlertWindow(alert, _settings.IsSoundEnabled);
            notification.Show();
        });
    }
}
