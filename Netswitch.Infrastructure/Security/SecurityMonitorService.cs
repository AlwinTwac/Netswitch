using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Netswitch.Core.Abstractions;
using Netswitch.Core.Models;

namespace Netswitch.Infrastructure.Security;

public class SecurityMonitorService : ISecurityMonitorService
{
    private readonly IDeviceDiscoveryService _deviceDiscoveryService;
    private readonly IProcessNetworkMonitor _processNetworkMonitor;
    
    private readonly string _trustedDevicesFilePath;
    private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

    public SecurityMonitorService(IDeviceDiscoveryService deviceDiscoveryService, IProcessNetworkMonitor processNetworkMonitor)
    {
        _deviceDiscoveryService = deviceDiscoveryService;
        _processNetworkMonitor = processNetworkMonitor;
        _trustedDevicesFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Netswitch", "trusted_devices.json");
    }

    private async Task<List<string>> LoadTrustedDevicesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_trustedDevicesFilePath)) return new List<string>();
            var json = await File.ReadAllTextAsync(_trustedDevicesFilePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveTrustedDevicesAsync(List<string> devices)
    {
        await _fileLock.WaitAsync();
        try
        {
            var dir = Path.GetDirectoryName(_trustedDevicesFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(devices);
            await File.WriteAllTextAsync(_trustedDevicesFilePath, json);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task TrustDeviceAsync(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return;
        var devices = await LoadTrustedDevicesAsync();
        if (!devices.Contains(macAddress))
        {
            devices.Add(macAddress);
            await SaveTrustedDevicesAsync(devices);
        }
    }

    public async Task RevokeTrustAsync(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return;
        var devices = await LoadTrustedDevicesAsync();
        if (devices.Remove(macAddress))
        {
            await SaveTrustedDevicesAsync(devices);
        }
    }

    public async Task<bool> IsDeviceTrustedAsync(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return false;
        var devices = await LoadTrustedDevicesAsync();
        return devices.Contains(macAddress);
    }

    public async Task<IReadOnlyList<string>> GetTrustedDevicesAsync()
    {
        var devices = await LoadTrustedDevicesAsync();
        return devices.AsReadOnly();
    }

    public async IAsyncEnumerable<NetworkAlert> ObserveSecurityAlertsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<NetworkAlert>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var tasks = new[]
        {
            Task.Run(() => MonitorDeviceEventsAsync(channel.Writer, cts.Token), cts.Token),
            Task.Run(() => MonitorArpAndGatewayAsync(channel.Writer, cts.Token), cts.Token),
            Task.Run(() => MonitorSuspiciousTrafficAsync(channel.Writer, cts.Token), cts.Token),
            Task.Run(() => MonitorDnsHijackAsync(channel.Writer, cts.Token), cts.Token)
        };
        
        try 
        {
            await foreach(var alert in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return alert;
            }
        }
        finally 
        {
            cts.Cancel();
            await Task.WhenAll(tasks.Select(t => t.ContinueWith(_ => {})));
        }
    }

    private async Task MonitorDeviceEventsAsync(ChannelWriter<NetworkAlert> writer, CancellationToken cancellationToken)
    {
        try 
        {
            await foreach(var evt in _deviceDiscoveryService.ObserveDeviceEventsAsync(cancellationToken))
            {
                if (evt.EventType == DeviceEventType.DeviceConnected && !string.IsNullOrWhiteSpace(evt.Device.MacAddress))
                {
                    bool isTrusted = await IsDeviceTrustedAsync(evt.Device.MacAddress);
                    if (!isTrusted)
                    {
                        await writer.WriteAsync(new NetworkAlert(
                            AlertSeverity.High,
                            "Untrusted Device Detected",
                            $"An unknown device connected. IP: {evt.Device.IpAddress}, MAC: {evt.Device.MacAddress}",
                            DateTimeOffset.UtcNow,
                            AlertCategory.Security
                        ), cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private async Task MonitorArpAndGatewayAsync(ChannelWriter<NetworkAlert> writer, CancellationToken cancellationToken)
    {
        string? knownGatewayMac = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                
                var devices = await _deviceDiscoveryService.GetDevicesAsync(cancellationToken);
                
                var macGroups = devices
                    .Where(d => !string.IsNullOrWhiteSpace(d.MacAddress))
                    .GroupBy(d => d.MacAddress!)
                    .Where(g => g.Select(d => d.IpAddress).Distinct().Count() > 1);

                foreach (var group in macGroups)
                {
                    await writer.WriteAsync(new NetworkAlert(
                        AlertSeverity.Critical,
                        "ARP Spoofing Detected",
                        $"Multiple devices have the same MAC address: {group.Key}. IPs: {string.Join(", ", group.Select(d => d.IpAddress).Distinct())}",
                        DateTimeOffset.UtcNow,
                        AlertCategory.Security
                    ), cancellationToken);
                }

                var gatewayIp = GetDefaultGatewayIp();
                if (!string.IsNullOrEmpty(gatewayIp))
                {
                    var gatewayDevice = devices.FirstOrDefault(d => d.IpAddress == gatewayIp);
                    if (gatewayDevice != null && !string.IsNullOrWhiteSpace(gatewayDevice.MacAddress))
                    {
                        if (knownGatewayMac == null)
                        {
                            knownGatewayMac = gatewayDevice.MacAddress;
                        }
                        else if (knownGatewayMac != gatewayDevice.MacAddress)
                        {
                            await writer.WriteAsync(new NetworkAlert(
                                AlertSeverity.Critical,
                                "Gateway MAC Address Changed",
                                $"The default gateway ({gatewayIp}) MAC address changed from {knownGatewayMac} to {gatewayDevice.MacAddress}. This could indicate a Man-in-the-Middle attack.",
                                DateTimeOffset.UtcNow,
                                AlertCategory.Security
                            ), cancellationToken);
                            knownGatewayMac = gatewayDevice.MacAddress;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }
    }

    private async Task MonitorSuspiciousTrafficAsync(ChannelWriter<NetworkAlert> writer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                var usages = await _processNetworkMonitor.GetProcessNetworkUsageAsync(cancellationToken);
                foreach (var usage in usages)
                {
                    if (usage.ConnectionCount > 100)
                    {
                        await writer.WriteAsync(new NetworkAlert(
                            AlertSeverity.Warning,
                            "Suspicious Traffic",
                            $"Process {usage.ProcessName} (PID: {usage.ProcessId}) has opened {usage.ConnectionCount} connections.",
                            DateTimeOffset.UtcNow,
                            AlertCategory.Security
                        ), cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }
    }

    private async Task MonitorDnsHijackAsync(ChannelWriter<NetworkAlert> writer, CancellationToken cancellationToken)
    {
        var initialDnsServers = GetActiveDnsServers();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                var currentDnsServers = GetActiveDnsServers();
                
                if (initialDnsServers.Length != currentDnsServers.Length || !initialDnsServers.All(currentDnsServers.Contains))
                {
                    await writer.WriteAsync(new NetworkAlert(
                        AlertSeverity.High,
                        "DNS Hijack Detected",
                        $"Active DNS servers changed! Previous: {string.Join(", ", initialDnsServers)}, Current: {string.Join(", ", currentDnsServers)}",
                        DateTimeOffset.UtcNow,
                        AlertCategory.Security
                    ), cancellationToken);
                    
                    initialDnsServers = currentDnsServers;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }
    }

    private string[] GetActiveDnsServers()
    {
        try 
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().DnsAddresses)
                .Select(ip => ip.ToString())
                .Distinct()
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private string? GetDefaultGatewayIp()
    {
        try
        {
            var gateway = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return gateway?.Address.ToString();
        }
        catch
        {
            return null;
        }
    }
}
