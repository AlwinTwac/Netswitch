using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

public interface ILatencyMonitor
{
    IAsyncEnumerable<LatencySnapshot> ObserveLatencyAsync(CancellationToken cancellationToken = default);
}
