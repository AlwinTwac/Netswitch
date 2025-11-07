namespace Netswitch.Core.Models;

public enum NetworkCondition
{
    Normal,
    Slow,
    Unstable,
    HighLatency,
    PacketLoss,
    Disconnected
}

public sealed record NetworkConditionAlert(
    NetworkCondition Condition,
    string Message,
    DateTimeOffset Timestamp,
    double? LatencyMs = null,
    double? PacketLossPercent = null);
