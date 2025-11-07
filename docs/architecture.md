# Netswitch Architecture Overview

## Goals

- Provide real-time visibility into LAN/WLAN availability and health.
- Display instant visual cues (green/yellow/red) for network status and router latency.
- Offer an on-demand bandwidth speed test.
- Track per-application network consumption and list by usage.
- Operate as a Windows desktop experience suitable for enterprise deployment.

## Technology Stack

- **Platform:** .NET 8 (Windows-only) with WPF desktop application.
- **Language:** C#.
- **UI Framework:** Windows Presentation Foundation (WPF) using the MVVM pattern with `CommunityToolkit.Mvvm`.
- **Networking APIs:**
  - `System.Net.NetworkInformation` for interface status and gateway discovery.
  - `Windows.Networking.Connectivity` (via WinRT interop) for per-application usage metrics.
  - ICMP echo requests (via `Ping`) to measure router latency.
- **Speed Test:** Wrap the open-source `SpeedTestSharp` library (Ookla Speedtest CLI compatible) with graceful fallback to a bundled CLI.
- **Data Layer:** In-memory cache for live data, optional persistent logging to SQLite (via `Microsoft.Data.Sqlite`) for historical reporting.
- **Dependency Injection:** `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.DependencyInjection`.
- **Background Scheduling:** `System.Threading.Channels` with hosted services for periodic polling.

## Solution Structure

```
Netswitch.sln
├── Netswitch.Core/          # Core services, models, abstractions
├── Netswitch.UI/            # WPF UI project (MVVM)
├── Netswitch.Infrastructure/# Windows-specific interop (WinRT, performance counters)
└── Netswitch.Tests/         # Unit tests for services (xUnit + FluentAssertions)
```

### Project Responsibilities

- **`Netswitch.Core`**
  - Domain models: `NetworkStatus`, `LatencyStatus`, `SpeedTestResult`, `AppUsageSummary`.
  - Service contracts: `INetworkHealthService`, `ILatencyMonitor`, `ISpeedTestService`, `INetworkUsageCollector`.
  - Eventing: Observables or `IAsyncEnumerable` updates.
  - Business rules for status thresholds and health evaluation.

- **`Netswitch.Infrastructure`**
  - Implementations of service contracts.
  - WinRT interop for application usage metrics (via `Windows.winmd`).
  - Wrapper around `Ping`, `SpeedTestSharp`, and any native calls (ETW counters if needed).
  - Optional SQLite repository for historical storage.

- **`Netswitch.UI`**
  - ViewModels: `DashboardViewModel`, `SpeedTestViewModel`, `AppUsageViewModel`.
  - Views: `MainWindow.xaml` with cards for status, latency, usage list, speed test widget.
  - Value converters for status-to-color mapping.
  - Integration with `IHost` for DI and background services.

- **`Netswitch.Tests`**
  - Unit tests for threshold logic, status evaluation, service behaviors (mocking infrastructure implementations).

## Component Interactions

```mermaid
digraph G {
    subgraph cluster_core {
        label="Core";
        NetworkHealthService;
        LatencyMonitor;
        SpeedTestService;
        UsageCollector;
    }

    subgraph cluster_ui {
        label="UI (MVVM)";
        DashboardViewModel -> MainWindow;
        SpeedTestViewModel -> SpeedTestView;
        AppUsageViewModel -> UsageListView;
    }

    Host[Generic Host/DI];

    Host -> NetworkHealthService;
    Host -> LatencyMonitor;
    Host -> SpeedTestService;
    Host -> UsageCollector;

    NetworkHealthService -> DashboardViewModel;
    LatencyMonitor -> DashboardViewModel;
    SpeedTestService -> SpeedTestViewModel;
    UsageCollector -> AppUsageViewModel;
}
```

## Data Flow Summary

1. **Network availability polling**
   - `NetworkHealthHostedService` queries NIC state every 2 seconds.
   - Emits `NetworkStatus` records to observers.
   - UI updates the status indicator (green/red) and logs transitions.

2. **Router latency monitoring**
   - `LatencyMonitor` resolves the default gateway and performs ICMP ping once per second.
   - Calculates rolling average (last 5 samples) and maps to color bands (green/yellow/red).

3. **Per-application usage**
   - `UsageCollector` queries `NetworkUsageManager` for last interval (e.g., 60 seconds).
   - Aggregates by process and orders descending by total bytes.
   - Publishes top N entries to the UI.

4. **Speed test**
   - `SpeedTestService` executed on demand.
   - Runs in background, publishes progress and final `SpeedTestResult`.
   - UI disables trigger button during execution.

5. **Alerting and history (future extension)**
   - Optional: Persist metrics to SQLite for trend analysis.
   - Optional: Raise toast notifications via Windows notifications on downtime.

## UI Layout (Initial Draft)

- **Header:** "Netswitch" branding with last updated timestamp.
- **Status Panel:** Large indicator with current network health text ("Online", "Offline", etc.)
- **Latency Card:** Gauge or numeric display with color-coded latency band.
- **Usage Table:** Top applications ordered by data usage (columns: Application, Sent, Received, Total).
- **Speed Test Widget:** Button to start/abort test, display download/upload/ping results.
- **Footer:** Optional activity log or summary stats.

## UI Modernization Roadmap (Upcoming)

- **Visual language refresh**
  - Adopt Fluent-inspired cards with subtle gradients, drop shadows, and rounded edges.
  - Introduce status glyphs, accent typography, and animated transitions for state changes.
  - Provide light/dark theme pairing with centralized `ResourceDictionary` tokens.
- **Responsive dashboard**
  - Reflow cards using adaptive columns for wide vs. narrow window sizes.
  - Add compact mode that collapses secondary metrics while preserving critical indicators.
- **Floating bubble companion**
  - Minimal window that docks or floats above other windows and follows mouse drag.
  - Displays: network up/down dot, realtime throughput (kb/s → Gb/s), latency badge.
  - User controls: resize, click-to-cycle opacity (e.g., 30/60/90%), context menu for settings.
  - Synchronize with main dashboard view model so bubble reflects identical live data.
- **Accessibility & usability**
  - Larger touch targets, keyboard shortcuts, and high-contrast palette.
  - Toast notifications for network down events when bubble is visible.

## Monitoring Intervals & Thresholds

- Network health poll: 2s interval.
- Router latency poll: 1s interval with 5-sample rolling average.
- Usage update: refresh every 10s.
- Latency colors: 0-50ms green, 51-100ms yellow, 101ms+ red.
- Network health colors: green when at least one active interface with internet access, red otherwise.

## Deployment Considerations

- Distribute via MSIX installer for smooth enterprise deployment.
- Require Windows 10 (1903+) due to WinRT interop for usage metrics.
- Background speed tests may need elevated network permission; handle gracefully.
- Provide optional telemetry logs for troubleshooting (write to `%ProgramData%\Netswitch\logs`).

## Next Steps

1. Generate the solution scaffold with the outlined projects.
2. Implement core service interfaces and stub implementations.
3. Build the WPF shell with placeholder views.
4. Integrate real monitoring logic iteratively.
5. Add automated tests for business rules.
