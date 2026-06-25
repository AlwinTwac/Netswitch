using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Diagnostics;
using Netswitch.Core.Models;
using Netswitch.Core.Options;

namespace Netswitch.Infrastructure.Latency;

public sealed class LatencyMonitor : ILatencyMonitor
{
    private readonly MonitoringOptions _options;

    public LatencyMonitor(MonitoringOptions? options = null)
    {
        _options = options ?? new MonitoringOptions();
    }

    public async IAsyncEnumerable<LatencySnapshot> ObserveLatencyAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return await MeasureLatencyAsync().ConfigureAwait(false);

        using var timer = new PeriodicTimer(_options.LatencyPollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return await MeasureLatencyAsync().ConfigureAwait(false);
        }
    }

    private async Task<LatencySnapshot> MeasureLatencyAsync()
    {
        var gatewayAddress = GetDefaultGateway();
        if (gatewayAddress is null)
        {
            return new LatencySnapshot(TimeSpan.MaxValue, LatencyQuality.Red, DateTimeOffset.UtcNow);
        }

        using var ping = new Ping();
        var reply = await ping.SendPingAsync(gatewayAddress, _options.PingTimeoutMilliseconds);
        var rtt = reply.Status == IPStatus.Success
            ? TimeSpan.FromMilliseconds(reply.RoundtripTime)
            : TimeSpan.MaxValue;

        return new LatencySnapshot(rtt, LatencyClassifier.Classify(rtt), DateTimeOffset.UtcNow);
    }

    private static string? GetDefaultGateway()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Select(nic => nic.GetIPProperties().GatewayAddresses.FirstOrDefault()?.Address)
            .FirstOrDefault(addr => addr is not null)?
            .ToString();
    }
}
