using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;
using Netswitch.Core.Options;

namespace Netswitch.Infrastructure.Usage;

public sealed class NetworkUsageCollector : INetworkUsageCollector
{
    private readonly MonitoringOptions _options;
    private readonly int _maxEntries;

    public NetworkUsageCollector(MonitoringOptions? options = null, int maxEntries = 10)
    {
        _options = options ?? new MonitoringOptions();
        _maxEntries = maxEntries;
    }

    public async IAsyncEnumerable<IReadOnlyList<AppUsageSummary>> ObserveUsageAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var previousSnapshot = CaptureSnapshot();
        yield return ImmutableArray<AppUsageSummary>.Empty;

        using var timer = new PeriodicTimer(_options.UsageAggregationInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var nextSnapshot = CaptureSnapshot();
            var summaries = CalculateDiff(previousSnapshot, nextSnapshot, DateTimeOffset.UtcNow);
            previousSnapshot = nextSnapshot;
            yield return summaries;
        }
    }

    private IReadOnlyDictionary<string, InterfaceSnapshot> CaptureSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => new InterfaceSnapshot(
                nic.Id,
                nic.Name,
                nic.GetIPStatistics().BytesSent,
                nic.GetIPStatistics().BytesReceived,
                now))
            .ToDictionary(snapshot => snapshot.Id);
    }

    private IReadOnlyList<AppUsageSummary> CalculateDiff(
        IReadOnlyDictionary<string, InterfaceSnapshot> previous,
        IReadOnlyDictionary<string, InterfaceSnapshot> current,
        DateTimeOffset capturedAt)
    {
        var deltas = new List<AppUsageSummary>();

        foreach (var kvp in current)
        {
            var currentSnapshot = kvp.Value;
            if (!previous.TryGetValue(kvp.Key, out var previousSnapshot))
            {
                deltas.Add(new AppUsageSummary(
                    $"Interface: {currentSnapshot.Name}",
                    currentSnapshot.BytesSent,
                    currentSnapshot.BytesReceived,
                    capturedAt));
                continue;
            }

            var sentDelta = Math.Max(0, currentSnapshot.BytesSent - previousSnapshot.BytesSent);
            var receivedDelta = Math.Max(0, currentSnapshot.BytesReceived - previousSnapshot.BytesReceived);

            if (sentDelta == 0 && receivedDelta == 0)
            {
                continue;
            }

            deltas.Add(new AppUsageSummary(
                $"Interface: {currentSnapshot.Name}",
                sentDelta,
                receivedDelta,
                capturedAt));
        }

        return deltas
            .OrderByDescending(entry => entry.TotalBytes)
            .Take(_maxEntries)
            .ToList();
    }

    private sealed record InterfaceSnapshot(
        string Id,
        string Name,
        long BytesSent,
        long BytesReceived,
        DateTimeOffset CapturedAt);
}
