namespace Netswitch.Core.Models;

public sealed record SpeedTestProgress(string Phase, double ProgressPercent, double CurrentSpeedMbps);
