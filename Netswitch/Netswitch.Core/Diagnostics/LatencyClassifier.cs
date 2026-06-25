using Netswitch.Core.Models;

namespace Netswitch.Core.Diagnostics;

public static class LatencyClassifier
{
    public static LatencyQuality Classify(TimeSpan roundTripTime)
    {
        var milliseconds = roundTripTime.TotalMilliseconds;

        if (milliseconds <= 50)
        {
            return LatencyQuality.Green;
        }

        if (milliseconds <= 100)
        {
            return LatencyQuality.Yellow;
        }

        return LatencyQuality.Red;
    }
}
