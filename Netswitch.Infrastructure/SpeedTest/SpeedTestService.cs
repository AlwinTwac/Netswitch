using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
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

    public Task<SpeedTestResult> RunSpeedTestAsync(CancellationToken cancellationToken = default)
    {
        return RunSpeedTestAsync(null, cancellationToken);
    }

    public async Task<SpeedTestResult> RunSpeedTestAsync(IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var token = timeoutSource.Token;

        progress?.Report(new SpeedTestProgress("Measuring Latency", 0, 0));
        var latency = await MeasureLatencyAsync(token).ConfigureAwait(false);

        progress?.Report(new SpeedTestProgress("Starting Download Test", 0, 0));
        var downloadMbps = await MeasureDownloadAsync(progress, token).ConfigureAwait(false);
        
        progress?.Report(new SpeedTestProgress("Starting Upload Test", 0, 0));
        var uploadMbps = await MeasureUploadAsync(progress, token).ConfigureAwait(false);

        progress?.Report(new SpeedTestProgress("Finished", 100, 0));

        return new SpeedTestResult(downloadMbps, uploadMbps, latency, DateTimeOffset.UtcNow);
    }

    private async Task<double> MeasureDownloadAsync(IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _options.DownloadUrl);
            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            long totalBytes = 0;
            long expectedBytes = _options.ExpectedDownloadBytes;

            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    totalBytes += read;

                    if (progress != null && expectedBytes > 0 && stopwatch.Elapsed.TotalSeconds > 0)
                    {
                        var pct = Math.Min(100.0, (double)totalBytes / expectedBytes * 100.0);
                        var curSpeed = (totalBytes * 8.0) / stopwatch.Elapsed.TotalSeconds / 1_000_000.0;
                        progress.Report(new SpeedTestProgress("Downloading", pct, curSpeed));
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            stopwatch.Stop();
            if (stopwatch.Elapsed.TotalSeconds <= 0.0)
            {
                return 0.0;
            }

            var bits = totalBytes * 8.0;
            return bits / stopwatch.Elapsed.TotalSeconds / 1_000_000.0; // Mbps
        }
        catch
        {
            return 0.0;
        }
    }

    private async Task<double> MeasureUploadAsync(IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var payload = ArrayPool<byte>.Shared.Rent(_options.UploadBytes);
            try
            {
                Random.Shared.NextBytes(payload);

                var content = new ByteArrayContent(payload, 0, _options.UploadBytes);
                var request = new HttpRequestMessage(HttpMethod.Post, _options.UploadUrl)
                {
                    Content = content
                };

                progress?.Report(new SpeedTestProgress("Uploading", 50, 0)); // Simulated progress for POST since we can't easily stream progress with HttpClient

                var stopwatch = Stopwatch.StartNew();
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                stopwatch.Stop();

                if (stopwatch.Elapsed.TotalSeconds <= 0.0)
                {
                    return 0.0;
                }

                var bits = _options.UploadBytes * 8.0;
                var speed = bits / stopwatch.Elapsed.TotalSeconds / 1_000_000.0; // Mbps
                
                progress?.Report(new SpeedTestProgress("Uploading", 100, speed));
                
                return speed;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(payload);
            }
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
            long totalRoundtripTime = 0;
            int successfulPings = 0;

            for (int i = 0; i < 3; i++)
            {
                var reply = await ping.SendPingAsync(_options.LatencyHost, _options.LatencyTimeoutMilliseconds).ConfigureAwait(false);
                
                if (reply.Status == IPStatus.Success)
                {
                    totalRoundtripTime += reply.RoundtripTime;
                    successfulPings++;
                }
            }
            
            if (successfulPings > 0)
            {
                return TimeSpan.FromMilliseconds((double)totalRoundtripTime / successfulPings);
            }
            
            return TimeSpan.MaxValue;
        }
        catch
        {
            return TimeSpan.MaxValue;
        }
    }
}
