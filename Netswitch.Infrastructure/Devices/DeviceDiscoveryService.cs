using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;
using Netswitch.Core.Options;

namespace Netswitch.Infrastructure.Devices;

/// <summary>
/// Discovers and tracks network devices using ARP table analysis and ICMP ping.
/// </summary>
public sealed class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly MonitoringOptions _options;
    private readonly ConcurrentDictionary<string, NetworkDevice> _devices = new();
    private readonly Channel<DeviceEvent> _eventChannel = Channel.CreateUnbounded<DeviceEvent>();

    public DeviceDiscoveryService(MonitoringOptions? options = null)
    {
        _options = options ?? new MonitoringOptions();
    }

    public async IAsyncEnumerable<DeviceEvent> ObserveDeviceEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Start background scanning
        _ = Task.Run(() => ScanLoopAsync(cancellationToken), cancellationToken);
        
        // Perform immediate initial scan
        _ = Task.Run(async () => 
        {
            await Task.Delay(500, cancellationToken); // Small delay to let things initialize
            await ScanNetworkAsync(cancellationToken);
        }, cancellationToken);

        // Yield events as they occur
        await foreach (var deviceEvent in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return deviceEvent;
        }
    }

    public Task<IReadOnlyList<NetworkDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = _devices.Values
            .OrderByDescending(d => d.LastSeen)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<NetworkDevice>>(devices);
    }

    public async Task ScanNetworkAsync(CancellationToken cancellationToken = default)
    {
        var localIps = GetLocalNetworkAddresses();
        
        foreach (var baseIp in localIps)
        {
            await ScanSubnetAsync(baseIp, cancellationToken);
        }
    }

    private async Task ScanLoopAsync(CancellationToken cancellationToken)
    {
        using var scanTimer = new PeriodicTimer(_options.NetworkPollInterval);
        using var statusCheckTimer = new PeriodicTimer(TimeSpan.FromSeconds(4)); // Check status every 4 seconds
        
        // Run status checks in parallel with scanning
        var statusCheckTask = Task.Run(async () =>
        {
            while (await statusCheckTimer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await CheckDeviceStatusAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Status check error: {ex.Message}");
                }
            }
        }, cancellationToken);
        
        // Main scanning loop
        while (await scanTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await ScanNetworkAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scan error: {ex.Message}");
            }
        }
        
        await statusCheckTask;
    }

    private async Task ScanSubnetAsync(string baseIp, CancellationToken cancellationToken)
    {
        var ipParts = baseIp.Split('.');
        if (ipParts.Length != 4) return;

        var subnet = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
        var tasks = new List<Task>();

        // Scan full range (1-254) to discover ALL devices on the network
        for (int i = 1; i <= 254; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var ip = $"{subnet}.{i}";
            tasks.Add(PingDeviceAsync(ip, cancellationToken));

            // Limit concurrent pings to 50 for faster scanning while avoiding network flooding
            if (tasks.Count >= 50)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private async Task PingDeviceAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            // Reduced timeout to 300ms for faster scanning
            var reply = await ping.SendPingAsync(ipAddress, 300);

            if (reply.Status == IPStatus.Success)
            {
                await ProcessDeviceResponseAsync(ipAddress, (int)reply.RoundtripTime, cancellationToken);
            }
        }
        catch
        {
            // Device didn't respond - this is normal
        }
    }

    private async Task ProcessDeviceResponseAsync(string ipAddress, int latencyMs, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var macAddress = GetMacAddress(ipAddress);
        var hostName = await GetHostNameAsync(ipAddress, cancellationToken);

        var isNewDevice = !_devices.ContainsKey(ipAddress);

        var device = new NetworkDevice(
            IpAddress: ipAddress,
            MacAddress: macAddress,
            HostName: hostName,
            FirstSeen: isNewDevice ? now : _devices.GetValueOrDefault(ipAddress)?.FirstSeen ?? now,
            LastSeen: now,
            IsOnline: true,
            BytesSent: 0, // Will be updated by usage tracking
            BytesReceived: 0,
            LatencyMs: latencyMs
        );

        _devices.AddOrUpdate(ipAddress, device, (_, _) => device);

        var eventType = isNewDevice ? DeviceEventType.DeviceConnected : DeviceEventType.DeviceUpdated;
        var message = isNewDevice
            ? $"New device connected: {hostName ?? ipAddress}"
            : $"Device updated: {hostName ?? ipAddress}";

        var deviceEvent = new DeviceEvent(eventType, device, now, message);
        await _eventChannel.Writer.WriteAsync(deviceEvent, cancellationToken);

        // Check for high latency
        if (latencyMs > 100 && !isNewDevice)
        {
            var highLatencyEvent = new DeviceEvent(
                DeviceEventType.HighLatency,
                device,
                now,
                $"High latency detected on {hostName ?? ipAddress}: {latencyMs}ms"
            );
            await _eventChannel.Writer.WriteAsync(highLatencyEvent, cancellationToken);
        }
    }

    private async Task CheckDeviceStatusAsync(CancellationToken cancellationToken)
    {
        // Faster timeout - 20 seconds before considering disconnect
        var disconnectTimeout = TimeSpan.FromSeconds(20);
        var verificationThreshold = TimeSpan.FromSeconds(6);
        var now = DateTimeOffset.UtcNow;

        // Actively ping each online device to maintain connectivity status
        var verificationTasks = new List<Task>();
        
        foreach (var (ip, device) in _devices.ToList())
        {
            if (device.IsOnline)
            {
                var timeSinceLastSeen = now - device.LastSeen;
                
                // Stage 1: Verify devices that haven't been seen for 6+ seconds
                if (timeSinceLastSeen > verificationThreshold && timeSinceLastSeen < disconnectTimeout)
                {
                    verificationTasks.Add(VerifyDeviceOnlineAsync(ip, cancellationToken));
                }
                
                // Stage 2: Only mark as disconnected after timeout AND failed verification
                if (timeSinceLastSeen > disconnectTimeout)
                {
                    // Final verification - ping 2 times to confirm disconnection
                    var isStillOnline = await ConfirmDeviceOfflineAsync(ip, cancellationToken);
                    
                    if (!isStillOnline)
                    {
                        // Confirmed offline
                        var offlineDevice = device with { IsOnline = false };
                        _devices[ip] = offlineDevice;

                        var deviceEvent = new DeviceEvent(
                            DeviceEventType.DeviceDisconnected,
                            offlineDevice,
                            now,
                            $"Device disconnected: {device.HostName ?? device.IpAddress}"
                        );

                        await _eventChannel.Writer.WriteAsync(deviceEvent, cancellationToken);
                    }
                }
            }
        }
        
        // Wait for all ping verifications to complete (with timeout to prevent hanging)
        if (verificationTasks.Any())
        {
            var timeout = Task.Delay(5000, cancellationToken);
            var completed = Task.WhenAll(verificationTasks);
            await Task.WhenAny(completed, timeout);
        }
    }

    private async Task VerifyDeviceOnlineAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 1500).ConfigureAwait(false);
            
            if (reply.Status == IPStatus.Success && _devices.TryGetValue(ipAddress, out var device))
            {
                // Update LastSeen time - device is still responding
                var updatedDevice = device with { LastSeen = DateTimeOffset.UtcNow };
                _devices[ipAddress] = updatedDevice;
            }
        }
        catch
        {
            // Ping failed or timed out, but don't mark as offline yet
            // Will be caught in the next check cycle
        }
    }

    private async Task<bool> ConfirmDeviceOfflineAsync(string ipAddress, CancellationToken cancellationToken)
    {
        // Double-check: Ping 2 times with 1 second intervals
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 1500).ConfigureAwait(false);
                
                if (reply.Status == IPStatus.Success)
                {
                    // Device responded! Update LastSeen and return true (still online)
                    if (_devices.TryGetValue(ipAddress, out var device))
                    {
                        var updatedDevice = device with { LastSeen = DateTimeOffset.UtcNow };
                        _devices[ipAddress] = updatedDevice;
                    }
                    return true; // Device is online
                }
            }
            catch
            {
                // Continue to next attempt
            }
            
            // Wait before next attempt (except on last attempt)
            if (attempt < 1)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        
        // Both attempts failed - device is definitely offline
        return false;
    }

    private static List<string> GetLocalNetworkAddresses()
    {
        var addresses = new List<string>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var nic in interfaces)
            {
                var ipProps = nic.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        addresses.Add(addr.Address.ToString());
                    }
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return addresses;
    }

    private static string? GetMacAddress(string ipAddress)
    {
        try
        {
            var arp = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "arp",
                    Arguments = $"-a {ipAddress}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            arp.Start();
            var output = arp.StandardOutput.ReadToEnd();
            arp.WaitForExit();

            // Parse MAC address from ARP output
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains(ipAddress))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var mac = parts[1];
                        if (mac.Contains('-') || mac.Contains(':'))
                        {
                            return mac;
                        }
                    }
                }
            }
        }
        catch
        {
            // MAC address lookup failed
        }

        return null;
    }

    private static async Task<string?> GetHostNameAsync(string ipAddress, CancellationToken cancellationToken)
    {
        // Try DNS first
        var dnsName = await TryGetDnsNameAsync(ipAddress, cancellationToken);
        if (!string.IsNullOrWhiteSpace(dnsName))
            return dnsName;
        
        // Fallback to NetBIOS
        var netbiosName = await TryGetNetBiosNameAsync(ipAddress, cancellationToken);
        if (!string.IsNullOrWhiteSpace(netbiosName))
            return netbiosName;
        
        return null;
    }

    private static async Task<string?> TryGetDnsNameAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3)); // 3 second timeout
            
            var hostEntry = await Dns.GetHostEntryAsync(ipAddress, cts.Token);
            
            if (!string.IsNullOrWhiteSpace(hostEntry.HostName))
            {
                // Clean up the hostname (remove domain suffix if present)
                var hostname = hostEntry.HostName;
                var dotIndex = hostname.IndexOf('.');
                if (dotIndex > 0)
                {
                    hostname = hostname.Substring(0, dotIndex);
                }
                return hostname;
            }
        }
        catch
        {
            // DNS lookup failed
        }
        
        return null;
    }

    private static async Task<string?> TryGetNetBiosNameAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var nbtstat = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "nbtstat",
                    Arguments = $"-A {ipAddress}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                }
            };

            nbtstat.Start();
            
            // Read output asynchronously
            var outputTask = nbtstat.StandardOutput.ReadToEndAsync(cancellationToken);
            var timeoutTask = Task.Delay(2000, cancellationToken); // 2 second timeout
            
            var completedTask = await Task.WhenAny(outputTask, timeoutTask);
            
            if (completedTask == outputTask)
            {
                var output = await outputTask;
                
                if (!nbtstat.HasExited)
                {
                    nbtstat.Kill();
                }
                
                // Parse NetBIOS name from output
                // Looking for lines with <00> (workstation) or <20> (file server)
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    // NetBIOS name table format: "   NAME            <00>  UNIQUE      Registered"
                    if (line.Contains("<00>") && line.Contains("UNIQUE"))
                    {
                        var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && !parts[0].Contains("__MSBROWSE__") && !parts[0].StartsWith("."))
                        {
                            return parts[0];
                        }
                    }
                }
            }
            else
            {
                // Timeout - kill the process
                if (!nbtstat.HasExited)
                {
                    nbtstat.Kill();
                }
            }
        }
        catch
        {
            // NetBIOS lookup failed
        }

        return null;
    }
}
