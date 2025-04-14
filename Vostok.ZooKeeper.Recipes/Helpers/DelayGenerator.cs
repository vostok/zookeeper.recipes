using System;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Threading;

namespace Vostok.ZooKeeper.Recipes.Helpers;

internal static class DelayGenerator
{
    public static async Task WaitAsync(TimeSpan delay, double jitterWindow = 0.2, CancellationToken token = default)
    {
        var jitter = jitterWindow * (ThreadSafeRandom.NextDouble() - 0.5);
        var resultDelayTicks = delay.Ticks * (1 + jitter);
        var resultDelay = new TimeSpan((long)resultDelayTicks);
        try
        {
            await Task.Delay(resultDelay, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }
}