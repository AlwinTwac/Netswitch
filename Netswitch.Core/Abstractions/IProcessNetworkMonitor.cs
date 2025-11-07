using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

/// <summary>
/// Service for monitoring network usage per process/application.
/// </summary>
public interface IProcessNetworkMonitor
{
    /// <summary>
    /// Gets current network usage statistics per process.
    /// </summary>
    Task<IReadOnlyList<ProcessNetworkUsage>> GetProcessNetworkUsageAsync(CancellationToken cancellationToken = default);
}
