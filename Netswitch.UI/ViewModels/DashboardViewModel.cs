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
    private DateTimeOffset _sessionStartTime = DateTimeOffset.Now;
    private readonly List<double> _latencyHistory = new();

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
                    _latencyHistory.Add(latencyMs);
                    
                    // Keep only last 100 readings
                    if (_latencyHistory.Count > 100)
                    {
                        _latencyHistory.RemoveAt(0);
                    }
                    
                    AverageLatencyMs = _latencyHistory.Average();
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

    public string SessionTimeText
    {
        get
        {
            var elapsed = DateTimeOffset.Now - _sessionStartTime;
            
            if (elapsed.TotalHours >= 1)
                return $"{elapsed.Hours}h {elapsed.Minutes}m";
            else if (elapsed.TotalMinutes >= 1)
                return $"{elapsed.Minutes}m {elapsed.Seconds}s";
            else
                return $"{elapsed.Seconds}s";
        }
    }

    public ObservableCollection<AppUsageSummary> TopApplications { get; } = new();
    
    public ObservableCollection<NetworkDevice> ConnectedDevices { get; } = new();
    
    public ObservableCollection<NetworkAlert> RecentAlerts { get; } = new();

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

        TopApplications.Clear();
        foreach (var item in ordered)
        {
            TopApplications.Add(item);
        }

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

        ConnectedDevices.Clear();
        foreach (var device in ordered)
        {
            ConnectedDevices.Add(device);
        }
        
        ConnectedDevicesCount = ordered.Count(d => d.IsOnline);
    }

    public void AddAlert(NetworkAlert alert)
    {
        // Keep alerts at max 50 to avoid memory issues
        if (RecentAlerts.Count >= 50)
        {
            RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        }

        RecentAlerts.Insert(0, alert);
        
        // Sort by severity (Critical first) then by time (newest first)
        var sorted = RecentAlerts
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .ToList();
        
        RecentAlerts.Clear();
        foreach (var sortedAlert in sorted)
        {
            RecentAlerts.Add(sortedAlert);
        }
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
