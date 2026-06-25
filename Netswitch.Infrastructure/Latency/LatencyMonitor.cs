using System;
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
    private readonly Queue<double> _latencyBuffer = new();
    private const int MaxSamples = 5;

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

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(gatewayAddress, _options.PingTimeoutMilliseconds);
            var rtt = reply.Status == IPStatus.Success
                ? TimeSpan.FromMilliseconds(reply.RoundtripTime)
                : TimeSpan.MaxValue;

            double jitterMs = 0;
            TimeSpan? smoothedRtt = null;

            if (rtt != TimeSpan.MaxValue)
            {
                var currentMs = rtt.TotalMilliseconds;
                _latencyBuffer.Enqueue(currentMs);

                while (_latencyBuffer.Count > MaxSamples)
                {
                    _latencyBuffer.Dequeue();
                }

                if (_latencyBuffer.Count > 0)
                {
                    var avg = _latencyBuffer.Average();
                    smoothedRtt = TimeSpan.FromMilliseconds(avg);

                    if (_latencyBuffer.Count > 1)
                    {
                        var sumOfSquaresOfDifferences = _latencyBuffer.Select(val => (val - avg) * (val - avg)).Sum();
                        jitterMs = Math.Sqrt(sumOfSquaresOfDifferences / (_latencyBuffer.Count - 1));
                    }
                }
            }
            else
            {
                _latencyBuffer.Clear();
            }

            return new LatencySnapshot(rtt, LatencyClassifier.Classify(rtt), DateTimeOffset.UtcNow, jitterMs, smoothedRtt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LatencyMonitor] Error measuring latency: {ex}");
            return new LatencySnapshot(TimeSpan.MaxValue, LatencyQuality.Red, DateTimeOffset.UtcNow);
        }
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
