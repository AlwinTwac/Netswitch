namespace Netswitch.Core.Abstractions;
using Netswitch.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ISecurityMonitorService
{
    Task TrustDeviceAsync(string macAddress);
    Task RevokeTrustAsync(string macAddress);
    Task<bool> IsDeviceTrustedAsync(string macAddress);
    Task<IReadOnlyList<string>> GetTrustedDevicesAsync();
    IAsyncEnumerable<NetworkAlert> ObserveSecurityAlertsAsync(CancellationToken cancellationToken = default);
}
