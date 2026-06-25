# New Features Added to Netswitch

This document summarizes the three major functionalities added to the Netswitch application:

## 1. Device Discovery & Detection

### What it does
Automatically detects and tracks all devices connected to your local network in real-time.

### Key Components Added

**Core Models:**
- `NetworkDevice` - Represents a discovered network device with IP, MAC, hostname, latency, and connection times
- `DeviceEvent` - Tracks device-related events (connected, disconnected, updated, high latency)

**Services:**
- `IDeviceDiscoveryService` - Interface for device discovery operations
- `DeviceDiscoveryService` - Implementation using ICMP ping and ARP table analysis

### Features
- Scans local network subnet (xxx.xxx.xxx.1-254) for active devices
- Automatically detects new device connections
- Tracks device disconnections (5-minute timeout)
- Records MAC addresses from ARP table
- Resolves hostnames via DNS
- Measures per-device latency via ICMP ping
- Updates device list every 5 seconds in the UI

### UI Integration
- New "Connected Devices" tab in the main window
- Displays: Status (Online/Offline), IP Address, Hostname, MAC Address, Latency, Last Seen
- Color-coded status indicators (green = online, gray = offline)

## 2. Performance Monitoring & Tracking

### What it does
Continuously monitors network device performance and tracks connection quality metrics.

### Features
- **Real-time latency tracking**: Monitors ping times for each discovered device
- **High latency detection**: Automatically detects devices with >100ms latency
- **Connection history**: Tracks first seen and last seen timestamps for all devices
- **Performance alerts**: Triggers alerts when devices experience degraded performance
- **Network usage tracking**: Prepared for per-device bandwidth monitoring (BytesSent/BytesReceived fields)

### Integration
- Integrated with existing `DashboardViewModel` through `UpdateDevices()` method
- Periodic refresh every 5 seconds via `DashboardCoordinator`
- Device events fed into alert system for actionable notifications

## 3. Network Status Alerts

### What it does
Provides intelligent, context-aware alerts for all network events and status changes.

### Key Components Added

**Core Models:**
- `NetworkAlert` - Represents an alert with severity, title, message, timestamp, and category
- `AlertSeverity` - Info, Warning, Error, Critical
- `AlertCategory` - NetworkStatus, DeviceActivity, Performance, SpeedTest

**Services:**
- `IAlertService` - Interface for alert management and delivery
- `AlertService` - Implementation with in-memory queue and Windows notification support
- `AlertCoordinator` - Monitors all network events and generates appropriate alerts

### Alert Types Generated

**Network Status Alerts:**
- Network goes online/offline (Critical/Info severity)
- Connection quality changes

**Device Activity Alerts:**
- New device connected (Info)
- Device disconnected (Warning)
- Device connection history

**Performance Alerts:**
- High network latency >200ms (Warning)
- High device-specific latency >100ms (Warning)

### Features
- **Real-time alert streaming**: Alerts appear immediately as events occur
- **Alert history**: Last 100 alerts kept in memory, viewable in UI
- **Severity-based filtering**: Color-coded by severity level
- **Windows notifications**: Critical and Warning alerts trigger system notifications
- **Alert categories**: Organized by NetworkStatus, DeviceActivity, Performance, SpeedTest
- **Time-based queries**: Retrieve alerts within specified time windows

### UI Integration
- New "Alerts" tab in the main window
- Displays: Severity (color-coded badge), Category, Title, Message, Time
- Most recent alerts appear first
- Severity colors:
  - Critical: Dark red
  - Error: Red
  - Warning: Orange
  - Info: Blue

## Technical Architecture

### Dependency Injection
All new services registered in `App.xaml.cs`:
```csharp
services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
services.AddSingleton<IAlertService, AlertService>();
services.AddSingleton<AlertCoordinator>();
```

### Background Processing
- `DeviceDiscoveryService` runs continuous network scanning in background
- `AlertCoordinator` monitors multiple event streams:
  - Network health status changes
  - Latency threshold violations
  - Device connect/disconnect events
- All background work uses async/await and CancellationToken for clean shutdown

### MVVM Pattern
- `DashboardViewModel` extended with:
  - `ConnectedDevices` collection
  - `RecentAlerts` collection
  - `UpdateDevices()` method
  - `AddAlert()` method
- WPF data binding connects UI to ViewModels
- Value converters added:
  - `AlertSeverityToBrushConverter` - Maps severity to colors
  - `DeviceStatusToBrushConverter` - Online/offline status colors
  - `DeviceStatusTextConverter` - Boolean to "Online"/"Offline"

### Performance Considerations
- Network scanning limited to 20 concurrent pings to prevent flooding
- Device list refreshes every 5 seconds (configurable)
- Alert queue capped at 100 recent items in memory
- UI updates dispatched to main thread safely
- Efficient use of `IAsyncEnumerable` for event streaming

## Building and Running

The application builds without additional dependencies:

```powershell
dotnet build
dotnet run --project Netswitch.UI
```

All new features are automatically activated on startup via the `AlertCoordinator` and `DashboardCoordinator`.

## Future Enhancements

Potential improvements documented for future development:
- Rich Windows toast notifications using Microsoft.Toolkit.Uwp.Notifications
- Per-device bandwidth usage tracking integration
- Alert filtering and search capabilities
- Export alerts to file/database
- Customizable alert thresholds
- Device grouping and tagging
- Network topology visualization
