using System;
using System.Collections.Generic;

namespace Netswitch.Core.Models;

public sealed record NetworkHistorySnapshot(
    DateTimeOffset Timestamp,
    double AverageLatencyMs,
    long TotalBytesSent,
    long TotalBytesReceived,
    int ConnectedDeviceCount,
    string TopApplication,
    int AlertCount
);
