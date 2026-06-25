using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;

namespace Netswitch.Infrastructure.Usage;

public sealed class NetworkHistoryService : INetworkHistoryService
{
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public NetworkHistoryService()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Netswitch");
        Directory.CreateDirectory(directory);
        _logFilePath = Path.Combine(directory, "network_history.json");
    }

    public async Task RecordSnapshotAsync(NetworkHistorySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var history = await LoadHistoryAsync(cancellationToken);
            
            // Keep only last 30 days
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            history = history.Where(x => x.Timestamp >= cutoff).ToList();
            
            history.Add(snapshot);
            
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_logFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkHistoryService] Failed to record snapshot: {ex}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ExportToCsvAsync(string filePath, TimeSpan timeframe, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var history = await LoadHistoryAsync(cancellationToken);
            
            var cutoff = DateTimeOffset.UtcNow - timeframe;
            var filtered = history.Where(x => x.Timestamp >= cutoff).OrderBy(x => x.Timestamp).ToList();
            
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,AverageLatencyMs,TotalBytesSent,TotalBytesReceived,ConnectedDeviceCount,TopApplication,AlertCount");
            
            foreach (var item in filtered)
            {
                // Escape commas in TopApplication
                var topApp = item.TopApplication ?? "";
                if (topApp.Contains(","))
                {
                    topApp = $"\"{topApp}\"";
                }
                
                sb.AppendLine($"{item.Timestamp:O},{item.AverageLatencyMs},{item.TotalBytesSent},{item.TotalBytesReceived},{item.ConnectedDeviceCount},{topApp},{item.AlertCount}");
            }
            
            await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<NetworkHistorySnapshot>> LoadHistoryAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_logFilePath))
        {
            return new List<NetworkHistorySnapshot>();
        }

        try
        {
            using var stream = File.OpenRead(_logFilePath);
            var result = await JsonSerializer.DeserializeAsync<List<NetworkHistorySnapshot>>(stream, cancellationToken: cancellationToken);
            return result ?? new List<NetworkHistorySnapshot>();
        }
        catch
        {
            return new List<NetworkHistorySnapshot>();
        }
    }
}
