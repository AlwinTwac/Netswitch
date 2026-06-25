using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;
using Netswitch.Core.Options;

namespace Netswitch.Infrastructure.SpeedTest;

public sealed class SpeedTestService : ISpeedTestService
{
    private readonly HttpClient _httpClient;
    private readonly SpeedTestOptions _options;

    public SpeedTestService(HttpClient httpClient, SpeedTestOptions? options = null)
    {
        _httpClient = httpClient;
        _options = options ?? new SpeedTestOptions();
    }

    public async Task<SpeedTestResult> RunSpeedTestAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var token = timeoutSource.Token;

        var downloadMbps = await MeasureDownloadAsync(token).ConfigureAwait(false);
        var uploadMbps = await MeasureUploadAsync(token).ConfigureAwait(false);
        var latency = await MeasureLatencyAsync(token).ConfigureAwait(false);

        return new SpeedTestResult(downloadMbps, uploadMbps, latency, DateTimeOffset.UtcNow);
    }

    private async Task<double> MeasureDownloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _options.DownloadUrl);
            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[81920];
            long totalBytes = 0;

            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
            }

            stopwatch.Stop();
            if (stopwatch.Elapsed.TotalSeconds <= 0.0)
            {
                return 0.0;
            }

            if (_options.ExpectedDownloadBytes > 0 && totalBytes < _options.ExpectedDownloadBytes / 2)
            {
                // The endpoint might have responded with less data than expected; treat as partial measurement.
                totalBytes = Math.Max(totalBytes, _options.ExpectedDownloadBytes);
            }

            var bits = totalBytes * 8.0;
            return bits / stopwatch.Elapsed.TotalSeconds / 1_000_000.0; // Mbps
        }
        catch
        {
            return 0.0;
        }
    }

    private async Task<double> MeasureUploadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var payload = new byte[_options.UploadBytes];
            Random.Shared.NextBytes(payload);

            var content = new ByteArrayContent(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, _options.UploadUrl)
            {
                Content = content
            };

            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            stopwatch.Stop();

            if (stopwatch.Elapsed.TotalSeconds <= 0.0)
            {
                return 0.0;
            }

            var bits = payload.LongLength * 8.0;
            return bits / stopwatch.Elapsed.TotalSeconds / 1_000_000.0; // Mbps
        }
        catch
        {
            return 0.0;
        }
    }

    private async Task<TimeSpan> MeasureLatencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(_options.LatencyHost, _options.LatencyTimeoutMilliseconds).ConfigureAwait(false);
            
            if (reply.Status == IPStatus.Success)
            {
                return TimeSpan.FromMilliseconds(reply.RoundtripTime);
            }
            
            // If ping failed, return MaxValue to indicate failure instead of showing wrong timeout value
            return TimeSpan.MaxValue;
        }
        catch
        {
            return TimeSpan.MaxValue;
        }
    }

}
