using System;
using System.Threading;
using System.Threading.Tasks;
using Netswitch.Core.Models;

namespace Netswitch.Core.Abstractions;

public interface ISpeedTestService
{
    Task<SpeedTestResult> RunSpeedTestAsync(CancellationToken cancellationToken = default);
    Task<SpeedTestResult> RunSpeedTestAsync(IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken = default);
}
