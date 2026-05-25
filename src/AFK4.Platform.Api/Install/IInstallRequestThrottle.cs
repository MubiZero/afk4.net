namespace AFK4.Platform.Api.Install;

public interface IInstallRequestThrottle
{
    Task<InstallRequestThrottleDecision> ApplyAsync(string sourceIp, CancellationToken cancellationToken);
}

public sealed record InstallRequestThrottleDecision(bool IsRejected, TimeSpan RetryAfter)
{
    public static InstallRequestThrottleDecision Allowed(TimeSpan retryAfter) => new(false, retryAfter);

    public static InstallRequestThrottleDecision Rejected(TimeSpan retryAfter) => new(true, retryAfter);
}
