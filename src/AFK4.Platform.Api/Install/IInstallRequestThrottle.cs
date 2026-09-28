namespace AFK4.Platform.Api.Install;

public sealed record InstallRequestThrottleDecision(bool IsRejected, TimeSpan RetryAfter)
{
    public static InstallRequestThrottleDecision Allowed(TimeSpan retryAfter) => new(false, retryAfter);

    public static InstallRequestThrottleDecision Rejected(TimeSpan retryAfter) => new(true, retryAfter);
}
