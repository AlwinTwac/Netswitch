namespace Netswitch.Core.Models;

/// <summary>
/// Represents the overall health of the network.
/// </summary>
public sealed record NetworkStatus(
    bool IsOnline,
    string Description,
    DateTimeOffset CapturedAt);
