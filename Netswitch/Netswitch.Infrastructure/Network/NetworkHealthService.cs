using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;
using Netswitch.Core.Options;

namespace Netswitch.Infrastructure.Network;

public sealed class NetworkHealthService : INetworkHealthService
{
    private readonly MonitoringOptions _options;

    public NetworkHealthService(MonitoringOptions? options = null)
    {
        _options = options ?? new MonitoringOptions();
    }

    public async IAsyncEnumerable<NetworkStatus> ObserveStatusAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return EvaluateNetworkStatus();

        using var timer = new PeriodicTimer(_options.NetworkPollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return EvaluateNetworkStatus();
        }
    }

    private static NetworkStatus EvaluateNetworkStatus()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToArray();

        var isOnline = interfaces.Any(nic => nic.GetIPProperties().GatewayAddresses.Any());

        var description = isOnline
            ? $"Online via {interfaces.FirstOrDefault()?.Name ?? "unknown"}"
            : "No active network gateway detected";

        return new NetworkStatus(isOnline, description, DateTimeOffset.UtcNow);
    }
}
