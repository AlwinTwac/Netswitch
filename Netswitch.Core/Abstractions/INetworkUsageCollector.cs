using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

public interface INetworkUsageCollector
{
    IAsyncEnumerable<IReadOnlyList<AppUsageSummary>> ObserveUsageAsync(CancellationToken cancellationToken = default);
}
