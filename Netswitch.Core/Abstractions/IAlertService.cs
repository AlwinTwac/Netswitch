using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

/// <summary>
/// Service for managing and delivering network alerts.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Observes alerts as they are generated.
    /// </summary>
    IAsyncEnumerable<NetworkAlert> ObserveAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an alert notification.
    /// </summary>
    Task SendAlertAsync(NetworkAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent alerts within the specified time window.
    /// </summary>
    Task<IReadOnlyList<NetworkAlert>> GetRecentAlertsAsync(TimeSpan window, CancellationToken cancellationToken = default);
}
