using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;

namespace Netswitch.Infrastructure.Network;

public sealed class ProcessNetworkMonitor : IProcessNetworkMonitor
{
    private readonly Dictionary<int, (long sent, long received, DateTime lastCheck)> _processStats = new();
    private readonly ConcurrentDictionary<int, string> _processNameCache = new();
    
    private static readonly HashSet<string> SystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "system", "services", "smss", "csrss", "wininit", "winlogon",
        "lsass", "dwm", "conhost", "sihost", "taskhostw", "explorer",
        "registry", "idle", "memory compression", "ntoskrnl", "spoolsv",
        "audiodg", "fontdrvhost", "wudfhost", "dashost", "RuntimeBroker",
        "SearchIndexer", "SearchProtocolHost", "SearchFilterHost",
        "wmpnetwk", "mqsvc", "msdtc", "sppsvc", "vssvc", "wmpnscfg",
        "httpd", "postgres", "postgresql", "mysqld", "sqlservr", "mongod",
        "nginx", "apache", "apache2", "tomcat", "java", "javaw", "node",
        "dns", "dhcp", "ftp", "ssh", "telnet", "snmp", "ntp",
        "msiexec", "trustedinstaller", "tiworker"
    };

    public async Task<IReadOnlyList<ProcessNetworkUsage>> GetProcessNetworkUsageAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ProcessNetworkUsage>();

        try
        {
            // Get TCP connections with process IDs
            var connections = await GetTcpConnectionsAsync(cancellationToken);
            
            // Group by process
            var processGroups = connections
                .Where(c => c.ProcessId > 0)
                .GroupBy(c => c.ProcessId);

            foreach (var group in processGroups)
            {
                try
                {
                    var processId = group.Key;
                    var connectionCount = group.Count();
                    
                    if (!_processNameCache.TryGetValue(processId, out string? processName))
                    {
                        using var process = Process.GetProcessById(processId);
                        processName = process.ProcessName;
                        _processNameCache[processId] = processName;
                    }
                    
                    // Filter out system processes and show only user applications
                    if (IsSystemProcess(processName))
                        continue;
                    
                    var friendlyName = GetFriendlyProcessName(processName);
                    
                    // Get performance counter data for this process
                    var (sent, received) = await GetProcessNetworkStatsAsync(processId, cancellationToken);
                    
                    if (sent > 0 || received > 0 || connectionCount > 0)
                    {
                        results.Add(new ProcessNetworkUsage(
                            ProcessName: friendlyName,
                            ProcessId: processId,
                            BytesSent: sent,
                            BytesReceived: received,
                            ConnectionCount: connectionCount
                        ));
                    }
                }
                catch (Exception ex)
                {
                    // Process may have terminated or access denied
                    System.Diagnostics.Debug.WriteLine($"[ProcessNetworkMonitor] Error analyzing process {group.Key}: {ex.Message}");
                    // Invalidate cache for this PID in case it was reused
                    _processNameCache.TryRemove(group.Key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcessNetworkMonitor] Error getting connections: {ex}");
        }

        return results
            .OrderByDescending(p => p.BytesSent + p.BytesReceived)
            .ToList();
    }

    private static bool IsSystemProcess(string processName)
    {
        return SystemProcesses.Contains(processName);
    }

    private static async Task<List<(int ProcessId, string LocalAddress, string RemoteAddress)>> GetTcpConnectionsAsync(
        CancellationToken cancellationToken)
    {
        var connections = new List<(int ProcessId, string LocalAddress, string RemoteAddress)>();

        try
        {
            using var netstat = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            netstat.Start();
            var output = await netstat.StandardOutput.ReadToEndAsync(cancellationToken);
            await netstat.WaitForExitAsync(cancellationToken);

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Format: Protocol  Local Address  Foreign Address  State  PID
                if (parts.Length >= 5 && (parts[0] == "TCP" || parts[0] == "UDP"))
                {
                    if (int.TryParse(parts[^1], out var pid))
                    {
                        connections.Add((pid, parts[1], parts[2]));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcessNetworkMonitor] Error running netstat: {ex}");
        }

        return connections;
    }

    private Task<(long sent, long received)> GetProcessNetworkStatsAsync(int processId, CancellationToken cancellationToken)
    {
        // This is a simplified implementation
        // In production, you'd use Performance Counters or ETW for accurate per-process stats
        
        var now = DateTime.UtcNow;
        
        if (_processStats.TryGetValue(processId, out var stats))
        {
            var elapsed = (now - stats.lastCheck).TotalSeconds;
            if (elapsed > 0)
            {
                // Return accumulated stats
                return Task.FromResult((stats.sent, stats.received));
            }
        }

        // Initialize or update stats
        _processStats[processId] = (0, 0, now);
        
        return Task.FromResult((0L, 0L));
    }

    private static string GetFriendlyProcessName(string processName)
    {
        // Map common process names to friendly names
        return processName.ToLowerInvariant() switch
        {
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "firefox" => "Mozilla Firefox",
            "iexplore" => "Internet Explorer",
            "brave" => "Brave Browser",
            "opera" => "Opera",
            "spotify" => "Spotify",
            "discord" => "Discord",
            "slack" => "Slack",
            "teams" => "Microsoft Teams",
            "zoom" => "Zoom",
            "steamwebhelper" => "Steam",
            "epicgameslauncher" => "Epic Games",
            "onedrive" => "OneDrive",
            "dropbox" => "Dropbox",
            "googledrivesync" => "Google Drive",
            "outlook" => "Microsoft Outlook",
            "thunderbird" => "Thunderbird",
            "skype" => "Skype",
            "telegram" => "Telegram",
            "whatsapp" => "WhatsApp",
            _ => char.ToUpperInvariant(processName[0]) + processName.Substring(1)
        };
    }
}
