using System;
using System.Threading;
using System.Threading.Tasks;
using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

public interface INetworkHistoryService
{
    Task RecordSnapshotAsync(NetworkHistorySnapshot snapshot, CancellationToken cancellationToken = default);
    Task ExportToCsvAsync(string filePath, TimeSpan timeframe, CancellationToken cancellationToken = default);
}
