using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Netswitch.Core.Models;
using Netswitch.UI.Common;

namespace Netswitch.UI.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private NetworkStatus _networkStatus = new(false, "Initializing...", DateTimeOffset.UtcNow);
    private LatencySnapshot _latencySnapshot = new(TimeSpan.Zero, LatencyQuality.Green, DateTimeOffset.UtcNow);
    private bool _isSpeedTestRunning;
    private SpeedTestResult? _lastSpeedTest;
    private RelayCommand _runSpeedTestCommand = RelayCommand.NoOp;
    private RelayCommand _showDeviceListCommand = RelayCommand.NoOp;
    private RelayCommand _refreshCommand = RelayCommand.NoOp;
    private RelayCommand _toggleSoundCommand = RelayCommand.NoOp;
    private RelayCommand _toggleNotificationsCommand = RelayCommand.NoOp;
    private RelayCommand _clearAlertsCommand = RelayCommand.NoOp;
    private bool _isSoundEnabled = true;
    private bool _areNotificationsEnabled = true;
    private bool _isRefreshing = false;
    private bool _hasInternetConnection = true;
    private double _downloadBytesPerSecond;
    private double _uploadBytesPerSecond;
    private double _totalBytesPerSecond;
    private int _connectedDevicesCount;
    private double _currentLatencyMs;
    private double _averageLatencyMs;
    private double _jitterMs;
    private double _speedTestProgressPercent;
    private string _speedTestPhase = string.Empty;
    private int _unreadAlertCount;
    private DateTimeOffset _sessionStartTime = DateTimeOffset.UtcNow;
    private readonly Queue<double> _latencyHistory = new();
    private const int MaxLatencyHistory = 100;
    private const int MaxAlerts = 100;

    public NetworkStatus NetworkStatus
    {
        get => _networkStatus;
        set
        {
            if (SetProperty(ref _networkStatus, value))
            {
                OnPropertyChanged(nameof(StatusSummary));
                OnPropertyChanged(nameof(NetworkStatusTimestamp));
                OnPropertyChanged(nameof(NetworkStrength));
            }
        }
    }
    
    public void UpdateSessionTime()
    {
        OnPropertyChanged(nameof(SessionTimeText));
    }

    public LatencySnapshot LatencySnapshot
    {
        get => _latencySnapshot;
        set
        {
            if (SetProperty(ref _latencySnapshot, value))
            {
                OnPropertyChanged(nameof(LatencyText));
                CurrentLatencyMs = value.RoundTripTime.TotalMilliseconds;
                
                // Track latency history for average calculation
                if (value.RoundTripTime != TimeSpan.MaxValue)
                {
                    var latencyMs = value.RoundTripTime.TotalMilliseconds;
                    _latencyHistory.Enqueue(latencyMs);
                    
                    // Keep only last N readings (O(1) dequeue)
                    while (_latencyHistory.Count > MaxLatencyHistory)
                    {
                        _latencyHistory.Dequeue();
                    }
                    
                    AverageLatencyMs = _latencyHistory.Average();
                }

                // Update jitter from the snapshot if available
                if (value.JitterMs > 0)
                {
                    JitterMs = value.JitterMs;
                }
                
                OnPropertyChanged(nameof(NetworkStrength));
            }
        }
    }

    public double CurrentLatencyMs
    {
        get => _currentLatencyMs;
        set => SetProperty(ref _currentLatencyMs, value);
    }

    public double AverageLatencyMs
    {
        get => _averageLatencyMs;
        set
        {
            if (SetProperty(ref _averageLatencyMs, value))
            {
                OnPropertyChanged(nameof(AverageLatencyText));
            }
        }
    }

    public double JitterMs
    {
        get => _jitterMs;
        set
        {
            if (SetProperty(ref _jitterMs, value))
            {
                OnPropertyChanged(nameof(JitterText));
            }
        }
    }

    public int ConnectedDevicesCount
    {
        get => _connectedDevicesCount;
        set => SetProperty(ref _connectedDevicesCount, value);
    }

    public string NetworkStrength
    {
        get
        {
            if (!NetworkStatus.IsOnline)
                return "Disconnected";
                
            var avgLatency = AverageLatencyMs;
            
            if (avgLatency == 0)
                return "Measuring...";
            
            return avgLatency switch
            {
                <= 20 => "Excellent",
                <= 50 => "Strong",
                <= 100 => "Good",
                <= 200 => "Fair",
                _ => "Weak"
            };
        }
    }

    public string AverageLatencyText => AverageLatencyMs > 0 
        ? $"{AverageLatencyMs:F0} ms avg" 
        : "-- ms avg";

    public string JitterText => JitterMs > 0
        ? $"±{JitterMs:F1} ms"
        : "-- ms";

    public string SessionTimeText
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - _sessionStartTime;
            
            if (elapsed.TotalHours >= 1)
                return $"{elapsed.Hours}h {elapsed.Minutes}m";
            else if (elapsed.TotalMinutes >= 1)
                return $"{elapsed.Minutes}m {elapsed.Seconds}s";
            else
                return $"{elapsed.Seconds}s";
        }
    }

    // Speed test progress properties
    public double SpeedTestProgressPercent
    {
        get => _speedTestProgressPercent;
        set => SetProperty(ref _speedTestProgressPercent, value);
    }

    public string SpeedTestPhase
    {
        get => _speedTestPhase;
        set => SetProperty(ref _speedTestPhase, value);
    }

    public int UnreadAlertCount
    {
        get => _unreadAlertCount;
        set => SetProperty(ref _unreadAlertCount, value);
    }

    // Use BatchObservableCollection for efficient batch updates (single CollectionChanged notification)
    public BatchObservableCollection<AppUsageSummary> TopApplications { get; } = new();
    
    public BatchObservableCollection<NetworkDevice> ConnectedDevices { get; } = new();
    
    public BatchObservableCollection<NetworkAlert> RecentAlerts { get; } = new();

    public bool IsSpeedTestRunning
    {
        get => _isSpeedTestRunning;
        set
        {
            if (SetProperty(ref _isSpeedTestRunning, value))
            {
                _runSpeedTestCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public SpeedTestResult? LastSpeedTest
    {
        get => _lastSpeedTest;
        set
        {
            if (SetProperty(ref _lastSpeedTest, value))
            {
                OnPropertyChanged(nameof(LastSpeedTestDownloadText));
                OnPropertyChanged(nameof(LastSpeedTestUploadText));
                OnPropertyChanged(nameof(LastSpeedTestLatencyText));
            }
        }
    }

    public RelayCommand RunSpeedTestCommand
    {
        get => _runSpeedTestCommand;
        set => SetProperty(ref _runSpeedTestCommand, value);
    }

    public RelayCommand ShowDeviceListCommand
    {
        get => _showDeviceListCommand;
        set => SetProperty(ref _showDeviceListCommand, value);
    }

    private RelayCommand _openSecurityDashboardCommand = RelayCommand.NoOp;
    public RelayCommand OpenSecurityDashboardCommand
    {
        get => _openSecurityDashboardCommand;
        set => SetProperty(ref _openSecurityDashboardCommand, value);
    }

    public RelayCommand RefreshCommand
    {
        get => _refreshCommand;
        set => SetProperty(ref _refreshCommand, value);
    }

    public RelayCommand ToggleSoundCommand
    {
        get => _toggleSoundCommand;
        set => SetProperty(ref _toggleSoundCommand, value);
    }

    public RelayCommand ToggleNotificationsCommand
    {
        get => _toggleNotificationsCommand;
        set => SetProperty(ref _toggleNotificationsCommand, value);
    }

    public RelayCommand ClearAlertsCommand
    {
        get => _clearAlertsCommand;
        set => SetProperty(ref _clearAlertsCommand, value);
    }

    public bool IsSoundEnabled
    {
        get => _isSoundEnabled;
        set => SetProperty(ref _isSoundEnabled, value);
    }

    public bool AreNotificationsEnabled
    {
        get => _areNotificationsEnabled;
        set => SetProperty(ref _areNotificationsEnabled, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public bool HasInternetConnection
    {
        get => _hasInternetConnection;
        set => SetProperty(ref _hasInternetConnection, value);
    }

    public double DownloadBytesPerSecond
    {
        get => _downloadBytesPerSecond;
        private set
        {
            if (SetProperty(ref _downloadBytesPerSecond, value))
            {
                OnPropertyChanged(nameof(DownloadRateText));
            }
        }
    }

    public double UploadBytesPerSecond
    {
        get => _uploadBytesPerSecond;
        private set
        {
            if (SetProperty(ref _uploadBytesPerSecond, value))
            {
                OnPropertyChanged(nameof(UploadRateText));
            }
        }
    }

    public double TotalBytesPerSecond
    {
        get => _totalBytesPerSecond;
        private set
        {
            if (SetProperty(ref _totalBytesPerSecond, value))
            {
                OnPropertyChanged(nameof(TotalRateText));
            }
        }
    }

    public string StatusSummary => NetworkStatus.IsOnline ? "Online" : "Offline";

    public string NetworkStatusTimestamp => $"Updated {NetworkStatus.CapturedAt.ToLocalTime():g}";

    public string LatencyText => LatencySnapshot.RoundTripTime == TimeSpan.MaxValue
        ? "Ping timed out"
        : $"{LatencySnapshot.RoundTripTime.TotalMilliseconds:0} ms";

    public string LastSpeedTestDownloadText => LastSpeedTest is null
        ? "Download: --"
        : $"Download: {LastSpeedTest.DownloadMbps:F1} Mbps";

    public string LastSpeedTestUploadText => LastSpeedTest is null
        ? "Upload: --"
        : $"Upload: {LastSpeedTest.UploadMbps:F1} Mbps";

    public string LastSpeedTestLatencyText
    {
        get
        {
            if (LastSpeedTest is null)
                return "Latency: --";
                
            if (LastSpeedTest.Latency == TimeSpan.MaxValue)
                return "Latency: Failed";
                
            return $"Latency: {LastSpeedTest.Latency.TotalMilliseconds:0} ms";
        }
    }

    public string DownloadRateText => FormatRate(DownloadBytesPerSecond);

    public string UploadRateText => FormatRate(UploadBytesPerSecond);

    public string TotalRateText => FormatRate(TotalBytesPerSecond);

    public void UpdateUsage(IEnumerable<AppUsageSummary> entries, TimeSpan interval)
    {
        var ordered = entries
            .OrderByDescending(item => item.TotalBytes)
            .ToList();

        // Use ReplaceAll for a single CollectionChanged notification instead of Clear+Add loop
        TopApplications.ReplaceAll(ordered);

        var seconds = Math.Max(1.0, interval.TotalSeconds);
        var download = ordered.Sum(item => item.BytesReceived) / seconds;
        var upload = ordered.Sum(item => item.BytesSent) / seconds;

        DownloadBytesPerSecond = download;
        UploadBytesPerSecond = upload;
        TotalBytesPerSecond = download + upload;
    }

    public void UpdateDevices(IEnumerable<NetworkDevice> devices)
    {
        var ordered = devices
            .OrderByDescending(d => d.IsOnline)
            .ThenBy(d => d.LastSeen)
            .ToList();

        // Use ReplaceAll for a single CollectionChanged notification instead of Clear+Add loop
        ConnectedDevices.ReplaceAll(ordered);
        
        ConnectedDevicesCount = ordered.Count(d => d.IsOnline);
    }

    public void AddAlert(NetworkAlert alert)
    {
        // Keep alerts at max limit to avoid memory issues
        while (RecentAlerts.Count >= MaxAlerts)
        {
            RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        }

        // Insert at correct position to maintain sort order (severity desc, then time desc)
        // This avoids the expensive Clear+Re-Add pattern that caused UI flicker
        var insertIndex = 0;
        for (int i = 0; i < RecentAlerts.Count; i++)
        {
            var existing = RecentAlerts[i];
            if (alert.Severity > existing.Severity ||
                (alert.Severity == existing.Severity && alert.CreatedAt >= existing.CreatedAt))
            {
                insertIndex = i;
                break;
            }
            insertIndex = i + 1;
        }

        RecentAlerts.Insert(insertIndex, alert);
        UnreadAlertCount++;
    }

    public void ClearAlerts()
    {
        RecentAlerts.Clear();
        UnreadAlertCount = 0;
    }

    private static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "--";
        }

        var units = new[] { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
        var value = bytesPerSecond;
        var order = 0;
        while (value >= 1024 && order < units.Length - 1)
        {
            value /= 1024;
            order++;
        }

        return order switch
        {
            0 => $"{value:0} {units[order]}",
            1 => $"{value:0.0} {units[order]}",
            _ => $"{value:0.00} {units[order]}"
        };
    }
}
