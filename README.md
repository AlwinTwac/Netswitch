# Netswitch

Netswitch is a Windows desktop application that monitors local network health, router latency, and interface usage while offering an on-demand speed test. It is designed for LAN/WLAN environments that need quick diagnostics during outages.

- **Network availability indicator** showing online/offline status with descriptive context.
- **Router latency monitor** with color-coded thresholds (0–50 ms green, 51–100 ms yellow, 101+ ms red).
- **Top interface usage list** displaying send/receive totals per network interface (placeholder for per-application aggregation).
- **Speed test module** performing HTTP download/upload measurements and latency checks.
- **Device discovery & tracking** automatically detects and monitors all devices on your local network with real-time status, latency, and connection tracking.
- **Smart network alerts** provides instant notifications for network events including new device connections, disconnections, high latency warnings, and network status changes.
- **Modern WPF dashboard UI** with Fluent-inspired cards, live throughput, tabbed interface for devices and alerts, and responsive layout.
- **Floating status bubble** that appears when the main window is minimized, providing draggable, resizable at-a-glance stats.

## Solution Layout

```
Netswitch.sln
{{ ... }}
├── Netswitch.Infrastructure/    # Windows-specific service implementations
├── Netswitch.UI/                # WPF application (MVVM + DI)
└── docs/                        # Architecture notes
```

## Prerequisites

- Windows 10 (1903+) or later
- .NET SDK 9.0+
- Internet access for speed test endpoints

## Getting Started

1. Restore and build the solution:
   ```powershell
   dotnet build
   ```
2. Run the WPF app:
   ```powershell
   dotnet run --project Netswitch.UI
   ```
3. The dashboard window will open and begin polling network status automatically.

### Device Discovery & Monitoring

The application automatically scans your local network to discover connected devices:
- **Real-time device detection**: New devices are detected automatically as they connect.
- **Connection tracking**: Monitor when devices connect and disconnect from your network.
- **Performance metrics**: Track latency and network usage per device.
- **Device information**: View IP addresses, MAC addresses, hostnames, and connection timestamps.

### Network Alerts

Stay informed about your network status with intelligent alerting:
- **Network status changes**: Get notified when your network goes online or offline.
- **Device events**: Receive alerts when new devices connect or existing devices disconnect.
- **Performance warnings**: Automatic alerts for high latency conditions (>200ms).
- **Categorized alerts**: Alerts are organized by severity (Info, Warning, Error, Critical) and category.
- **Alert history**: Review recent alerts in the dedicated Alerts tab.

### Floating Bubble Companion

- Minimizing the main window automatically shows a circular bubble with:
  - Network health indicator (green/red dot).
  - Live total, download, and upload throughput.
  - Current latency summary.
- Drag the bubble by holding the left mouse button.
- Double-click the bubble to restore the main dashboard.
- Use the context menu (⋮ button) to adjust opacity (30–100%) or size (compact, comfort, expanded).

## Configuration

Runtime options are currently hard-coded via `MonitoringOptions` and `SpeedTestOptions` singletons registered in `App.xaml.cs`. Adjust defaults there (intervals, endpoints, payload sizes) as needed.

| Option | Description | Default |
| --- | --- | --- |
| `MonitoringOptions.NetworkPollInterval` | Interval for checking interface status | 2 seconds |
| `MonitoringOptions.LatencyPollInterval` | Interval for pinging router | 1 second |
| `MonitoringOptions.UsageAggregationInterval` | Interval for usage deltas | 10 seconds |
| `SpeedTestOptions.DownloadUrl` | Download endpoint | Cloudflare test file |
| `SpeedTestOptions.UploadUrl` | Upload endpoint | httpbin.org |
| `SpeedTestOptions.LatencyHost` | Ping target | fast.com |

## Current Limitations & Next Steps

- **Per-application usage**: `NetworkUsageCollector` presently aggregates by network interface. Implement WinRT `NetworkUsageManager` integration to map usage per process.
- **Speed test reliability**: Replace HTTP fallback with Ookla-compatible CLI or SpeedTestSharp integration for consistent results.
- **Latency smoothing**: Add rolling average/buffering in `LatencyMonitor` to reduce jitter.
- **Alerting & history**: Persist metrics (e.g., SQLite) and surface alert notifications for downtime.
- **Branding & theming**: Polish UI visuals, add company branding assets, and theme support.

Contributions and refinements are welcome.
