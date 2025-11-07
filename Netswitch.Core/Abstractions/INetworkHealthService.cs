using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

public interface INetworkHealthService
{
    IAsyncEnumerable<NetworkStatus> ObserveStatusAsync(CancellationToken cancellationToken = default);
}
