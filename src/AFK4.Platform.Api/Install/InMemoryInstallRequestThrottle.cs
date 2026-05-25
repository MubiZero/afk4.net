using System.Collections.Concurrent;

namespace AFK4.Platform.Api.Install;

public sealed class InMemoryInstallRequestThrottle(TimeProvider timeProvider) : IInstallRequestThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, RequestWindow> windows = new(StringComparer.Ordinal);

    public async Task<InstallRequestThrottleDecision> ApplyAsync(string sourceIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceIp))
        {
            return InstallRequestThrottleDecision.Allowed(TimeSpan.Zero);
        }

        var now = timeProvider.GetUtcNow();
        var window = windows.AddOrUpdate(
            sourceIp,
            _ => new RequestWindow(now, Count: 1),
            (_, existing) =>
            {
                if (now - existing.StartedAtUtc >= Window)
                {
                    return new RequestWindow(now, Count: 1);
                }

                return existing with { Count = existing.Count + 1 };
            });

        var delay = window.Count switch
        {
            >= 200 => TimeSpan.FromSeconds(60),
            >= 100 => TimeSpan.FromSeconds(5),
            >= 50 => TimeSpan.FromSeconds(1),
            _ => TimeSpan.Zero
        };

        if (window.Count >= 200)
        {
            return InstallRequestThrottleDecision.Rejected(delay);
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return InstallRequestThrottleDecision.Allowed(delay);
    }

    private sealed record RequestWindow(DateTimeOffset StartedAtUtc, int Count);
}
